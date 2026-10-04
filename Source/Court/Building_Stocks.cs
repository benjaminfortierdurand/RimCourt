using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimCourt.Court
{
	public class Building_Stocks : Building
	{
		private const float MockRange = 7f;
		private const int PassInterval = 60;
		private const float PassChance = 0.3f;
		private const int RareInterval = 250;
		private const int VictimCooldown = 240;
		private const int MockerCooldown = 900;
		private const float PeltShare = 0.4f;
		private const float FlightSeconds = 0.45f;
		private static readonly Color SplatColor = new Color(0.55f, 0.29f, 0.16f, 0.85f);

		private Pawn _occupant;
		private Pawn _escort;
		private bool _tied;
		private int _until = -1;
		private int _ticks;
		private int _lastMockTick = -999999;
		private readonly Dictionary<int, int> _mockerTicks = new Dictionary<int, int>();

		public Pawn Occupant => _occupant;
		public Pawn Escort => _escort;
		public bool Tied => _tied;
		public IntVec3 StandCell => Position;
		public bool IsFree => _occupant == null;

		public int TicksLeft => _occupant == null || !_tied ? 0 : Mathf.Max(0, _until - Find.TickManager.TicksGame);

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_References.Look(ref _occupant, "occupant");
			Scribe_References.Look(ref _escort, "escort");
			Scribe_Values.Look(ref _tied, "tied");
			Scribe_Values.Look(ref _until, "until", -1);
			Scribe_Values.Look(ref _ticks, "ticks");
		}

		public bool Hold(Pawn p, int ticks, Pawn escort)
		{
			if (p == null || p.Dead || !p.Spawned || p.Map != Map || _occupant != null) return false;
			var carried = p.IsPrisonerOfColony;
			if (carried)
			{
				if (!EscortFit(escort, p) || !escort.CanReach(p, PathEndMode.ClosestTouch, Danger.Deadly)) return false;
			}
			else if (!p.CanReach(this, PathEndMode.OnCell, Danger.Deadly)) return false;
			_occupant = p;
			_ticks = ticks;
			_tied = false;
			_until = -1;
			_escort = EscortFit(escort, p) ? escort : null;
			if (!carried && !GiveJob(p)) { Clear(); return false; }
			if (_escort != null && !GiveEscortJob(_escort)) _escort = null;
			if (_escort == null)
			{
				if (carried) { Clear(); return false; }
				Tie();
			}
			p.needs?.mood?.thoughts?.memories?.TryGainMemory(RimCourtDefOf.RimCourt_InStocks);
			return true;
		}

		public void Notify_Placed(Pawn p)
		{
			if (p == null || p != _occupant || _tied) return;
			GiveJob(p);
		}

		private bool EscortFit(Pawn e, Pawn condemned)
			=> e != null && e != condemned && !e.Dead && e.Spawned && e.Map == Map && !e.Downed
				&& !e.InMentalState && e.jobs != null && e.CanReach(this, PathEndMode.Touch, Danger.Deadly);

		private bool GiveJob(Pawn p)
		{
			if (p?.jobs == null) return false;
			try
			{
				var job = JobMaker.MakeJob(RimCourtDefOf.RimCourt_StandInStocks, this);
				job.expiryInterval = -1;
				if (p.jobs.TryTakeOrderedJob(job, JobTag.Misc)) return true;
				p.jobs.StartJob(job, JobCondition.InterruptForced);
				return p.CurJobDef == RimCourtDefOf.RimCourt_StandInStocks;
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] impossible de mettre au pilori (" + e.Message + ").");
				return false;
			}
		}

		private bool GiveEscortJob(Pawn e)
		{
			try
			{
				var job = JobMaker.MakeJob(RimCourtDefOf.RimCourt_EscortToStocks, _occupant, this, StandCell);
				job.count = 1;
				job.expiryInterval = -1;
				return e.jobs.TryTakeOrderedJob(job, JobTag.Misc);
			}
			catch (System.Exception ex)
			{
				Log.Warning("[RimCourt] l'escorte ne part pas (" + ex.Message + ").");
				return false;
			}
		}

		public void Notify_Tied(Pawn by)
		{
			if (_occupant == null || _tied) return;
			Tie();
		}

		private void Tie()
		{
			_tied = true;
			_until = Find.TickManager.TicksGame + Mathf.Max(2500, _ticks);
			_escort = null;
		}

		private void Clear()
		{
			_occupant = null;
			_escort = null;
			_tied = false;
			_until = -1;
			_mockerTicks.Clear();
		}

		public void Release(bool served)
		{
			var p = _occupant;
			var e = _escort;
			Clear();
			if (e != null && !e.Dead && e.CurJobDef == RimCourtDefOf.RimCourt_EscortToStocks)
				e.jobs?.EndCurrentJob(JobCondition.InterruptForced);
			if (p == null || p.Dead) return;
			if (p.Spawned)
				Messages.Message((served ? "RimCourt.StocksReleased" : "RimCourt.StocksFreed").Translate(p.LabelShortCap),
					new LookTargets(p), MessageTypeDefOf.NeutralEvent, historical: false);
			if (p.CurJobDef == RimCourtDefOf.RimCourt_StandInStocks)
				p.jobs?.EndCurrentJob(JobCondition.InterruptForced);
			CaptiveUtility.Ground(p);
			CaptiveUtility.SendBackToCell(p);
		}

		protected override void Tick()
		{
			base.Tick();
			if (_occupant == null) return;
			if (this.IsHashIntervalTick(RareInterval)) RareStep();
			else if (_tied && this.IsHashIntervalTick(PassInterval)) PasserStep();
		}

		private void RareStep()
		{
			var p = _occupant;
			if (p == null) return;
			if (p.Dead || p.MapHeld != Map) { Clear(); return; }
			if (!_tied && p.IsPrisonerOfColony)
			{
				var e = _escort;
				if (e == null || e.Dead || !e.Spawned || e.Downed || e.InMentalState
					|| e.CurJobDef != RimCourtDefOf.RimCourt_EscortToStocks)
					Release(false);
				return;
			}
			if (!p.Spawned) { Clear(); return; }
			if (p.Drafted || p.Downed || p.InMentalState) { Release(false); return; }
			if (p.CurJobDef != RimCourtDefOf.RimCourt_StandInStocks)
			{
				if (!GiveJob(p)) Release(false);
				return;
			}
			if (!_tied)
			{
				var e = _escort;
				if (e == null || e.Dead || !e.Spawned || e.Downed || e.InMentalState
					|| e.CurJobDef != RimCourtDefOf.RimCourt_EscortToStocks)
					Tie();
				return;
			}
			if (Find.TickManager.TicksGame >= _until) Release(true);
		}

		private void PasserStep()
		{
			var victim = _occupant;
			if (victim == null || victim.Position != StandCell || !Rand.Chance(PassChance)) return;
			if (Find.TickManager.TicksGame - _lastMockTick < VictimCooldown) return;
			var colonists = Map.mapPawns.FreeColonistsSpawned;
			Pawn mocker = null;
			var seen = 0;
			for (var i = 0; i < colonists.Count; i++)
			{
				var c = colonists[i];
				if (!CanMock(c, victim, true)) continue;
				if (c.Position.DistanceTo(Position) > MockRange) continue;
				if (c.CurJobDef == RimCourtDefOf.RimCourt_JeerAtStocks) continue;
				seen++;
				if (Rand.Range(0, seen) == 0) mocker = c;
			}
			if (mocker != null) Mock(mocker, victim, PeltShare);
		}

		private bool CanMock(Pawn c, Pawn victim, bool passer)
		{
			if (c == null || c == victim || c.Dead || c.Downed || c.Drafted || c.InMentalState || !c.Awake()) return false;
			if (c.interactions == null || c.interactions.InteractedTooRecentlyToInteract()) return false;
			if (passer && _mockerTicks.TryGetValue(c.thingIDNumber, out var last)
				&& Find.TickManager.TicksGame - last < MockerCooldown) return false;
			return c.interactions.CanInteractNowWith(victim, RimCourtDefOf.RimCourt_Mock);
		}

		public bool TryMockBy(Pawn mocker, float peltShare)
		{
			var victim = _occupant;
			if (victim == null || !_tied || victim.Position != StandCell) return false;
			if (!CanMock(mocker, victim, false)) return false;
			return Mock(mocker, victim, peltShare);
		}

		private bool Mock(Pawn mocker, Pawn victim, float peltShare)
		{
			var pelt = Rand.Chance(peltShare);
			var def = pelt ? RimCourtDefOf.RimCourt_Pelt : RimCourtDefOf.RimCourt_Mock;
			try
			{
				if (!mocker.interactions.TryInteractWith(victim, def)) return false;
				if (pelt) ThrowAt(mocker, victim);
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] moquerie impossible (" + e.Message + ").");
				return false;
			}
			var now = Find.TickManager.TicksGame;
			_lastMockTick = now;
			_mockerTicks[mocker.thingIDNumber] = now;
			return true;
		}

		private void ThrowAt(Pawn thrower, Pawn victim)
		{
			if (RimCourtDefOf.RimCourt_Thrown == null || RimCourtDefOf.RimCourt_Splat == null) return;
			var from = thrower.DrawPos;
			var to = victim.DrawPos;
			var dir = to - from;
			var dist = dir.MagnitudeHorizontal();
			if (dist < 0.5f) return;
			var thrown = FleckMaker.GetDataStatic(from, Map, RimCourtDefOf.RimCourt_Thrown, 0.55f);
			thrown.velocityAngle = dir.AngleFlat();
			thrown.velocitySpeed = dist / FlightSeconds;
			thrown.rotationRate = Rand.Range(-360f, 360f);
			Map.flecks.CreateFleck(thrown);
			var splat = FleckMaker.GetDataStatic(to, Map, RimCourtDefOf.RimCourt_Splat, Rand.Range(0.8f, 1.1f));
			splat.instanceColor = SplatColor;
			splat.rotation = Rand.Range(0f, 360f);
			Map.flecks.CreateFleck(splat);
			if (RimCourtDefOf.RimCourt_FilthSplat != null)
				FilthMaker.TryMakeFilth(victim.Position, Map, RimCourtDefOf.RimCourt_FilthSplat, 1);
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (var g in base.GetGizmos()) yield return g;
			if (_occupant == null) yield break;
			yield return new Command_Action
			{
				defaultLabel = "RimCourt.StocksReleaseLabel".Translate(),
				defaultDesc = "RimCourt.StocksReleaseDesc".Translate(_occupant.LabelShortCap),
				icon = UI.RimCourtIcons.Cancel,
				action = () => Release(false),
			};
		}

		public override string GetInspectString()
		{
			var s = base.GetInspectString();
			if (_occupant == null) return s;
			var line = _tied
				? "RimCourt.StocksInspect".Translate(_occupant.LabelShortCap, TicksLeft.ToStringTicksToPeriod()).ToString()
				: "RimCourt.StocksInspectTaking".Translate(_occupant.LabelShortCap, _escort?.LabelShortCap ?? "?").ToString();
			return s.NullOrEmpty() ? line : s + "\n" + line;
		}

		public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
		{
			if (_occupant != null) Release(false);
			base.DeSpawn(mode);
		}
	}
}
