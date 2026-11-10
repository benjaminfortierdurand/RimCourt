using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimCourt.Court
{
	public class BreakRecord : IExposable
	{
		public const int KindOther = 0;
		public const int KindFire = 1;
		public const int KindTantrum = 2;
		public const int KindGrave = 3;
		public const int KindMurder = 4;
		public const int KindSlaughter = 5;
		public const int KindJailbreak = 6;

		public Pawn pawn;
		public MentalStateDef state;
		public int kind;
		public int startTick;
		public int endTick = -1;
		public Pawn victim;
		public List<Pawn> witnesses = new List<Pawn>();
		public int helpers;
		public string detail;
		public float detailValue;
		public int count;
		public bool tried;

		public Thing watching;
		public string watchingLabel;
		public float watchingValue;
		public int watchingHp = -1;
		public int lastJobId = -1;

		public bool Open => endTick < 0;

		public bool Substantial
		{
			get
			{
				switch (kind)
				{
					case KindMurder: return victim != null && victim != pawn;
					case KindGrave: return count > 0 && !detail.NullOrEmpty();
					case KindOther: return endTick - startTick >= BreakUtility.MinGenericTicks;
					default: return count > 0;
				}
			}
		}

		public void ExposeData()
		{
			Scribe_References.Look(ref pawn, "pawn");
			Scribe_Defs.Look(ref state, "state");
			Scribe_Values.Look(ref kind, "kind");
			Scribe_Values.Look(ref startTick, "startTick");
			Scribe_Values.Look(ref endTick, "endTick", -1);
			Scribe_References.Look(ref victim, "victim");
			Scribe_Collections.Look(ref witnesses, "witnesses", LookMode.Reference);
			Scribe_Values.Look(ref helpers, "helpers");
			Scribe_Values.Look(ref detail, "detail");
			Scribe_Values.Look(ref detailValue, "detailValue");
			Scribe_Values.Look(ref count, "count");
			Scribe_Values.Look(ref tried, "tried");
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (witnesses == null) witnesses = new List<Pawn>();
				var before = witnesses.Count;
				witnesses.RemoveAll(w => w == null);
				if (witnesses.Count != before) helpers = 0;
			}
		}
	}

	public static class BreakUtility
	{
		public const int ScanInterval = 90;
		public const int KeepTicks = 60 * 60000;
		public const int MinGenericTicks = 600;
		private const int MaxWitnesses = 12;

		private static HashSet<MentalStateDef> _tracked;

		public static bool Tracked(MentalStateDef d)
		{
			if (d == null) return false;
			if (_tracked == null)
			{
				_tracked = new HashSet<MentalStateDef>();
				var defs = DefDatabase<PetitionDef>.AllDefsListForReading;
				for (var i = 0; i < defs.Count; i++)
				{
					var list = defs[i].sourceBreaks;
					if (list == null) continue;
					for (var j = 0; j < list.Count; j++)
						if (list[j] != null) _tracked.Add(list[j]);
				}
			}
			return _tracked.Contains(d);
		}

		public static int KindOf(MentalState st)
		{
			if (st is MentalState_MurderousRage) return BreakRecord.KindMurder;
			if (st is MentalState_CorpseObsession) return BreakRecord.KindGrave;
			if (st is MentalState_FireStartingSpree) return BreakRecord.KindFire;
			if (st is MentalState_Slaughterer) return BreakRecord.KindSlaughter;
			if (st is MentalState_Jailbreaker) return BreakRecord.KindJailbreak;
			if (st is MentalState_Tantrum) return BreakRecord.KindTantrum;
			return BreakRecord.KindOther;
		}

		public static void Scan(List<BreakRecord> records)
		{
			var now = Find.TickManager.TicksGame;
			var maps = Find.Maps;
			for (var m = 0; m < maps.Count; m++)
			{
				var map = maps[m];
				if (!map.IsPlayerHome) continue;
				var colonists = map.mapPawns.FreeColonistsSpawned;
				for (var i = 0; i < colonists.Count; i++)
				{
					var c = colonists[i];
					var st = c.MentalState;
					if (st == null || !Tracked(st.def)) continue;
					Observe(records, c, st, now);
				}
			}

			for (var i = records.Count - 1; i >= 0; i--)
			{
				var r = records[i];
				if (r.pawn == null || r.state == null) { records.RemoveAt(i); continue; }
				if (r.Open)
				{
					if (!r.pawn.Dead && r.pawn.Spawned && r.pawn.MentalStateDef == r.state) continue;
					Close(r, now);
					if (!r.Substantial) records.RemoveAt(i);
					continue;
				}
				if (now - r.endTick > KeepTicks || r.pawn.Dead || r.pawn.Faction != Faction.OfPlayer)
					records.RemoveAt(i);
			}
		}

		private static void Observe(List<BreakRecord> records, Pawn c, MentalState st, int now)
		{
			BreakRecord r = null;
			for (var i = 0; i < records.Count; i++)
			{
				var x = records[i];
				if (x.Open && x.pawn == c && x.state == st.def) { r = x; break; }
			}
			if (r == null)
			{
				r = new BreakRecord { pawn = c, state = st.def, kind = KindOf(st), startTick = now };
				records.Add(r);
			}

			switch (r.kind)
			{
				case BreakRecord.KindMurder:
					var target = (st as MentalState_MurderousRage)?.target;
					if (target != null && target != c && target.RaceProps.Humanlike)
					{
						r.victim = target;
						r.detail = target.LabelShortCap;
					}
					break;

				case BreakRecord.KindGrave:
					var obsession = st as MentalState_CorpseObsession;
					var dead = obsession?.corpse?.InnerPawn;
					if (dead != null)
					{
						r.detail = dead.LabelShortCap;
						if (r.victim == null) r.victim = Mourner(dead, c);
					}
					if (obsession != null && obsession.alreadyHauledCorpse) r.count = 1;
					break;

				case BreakRecord.KindTantrum:
					Watch(r, (st as MentalState_Tantrum)?.target, c);
					break;

				case BreakRecord.KindSlaughter:
					var job = c.CurJob;
					if (job != null && job.def == JobDefOf.Slaughter) Watch(r, job.targetA.Thing, c);
					break;

				case BreakRecord.KindFire:
					NoteJob(r, c, JobDefOf.Ignite, true);
					NoteFirefighters(r, c.Map);
					break;

				case BreakRecord.KindJailbreak:
					NoteJob(r, c, JobDefOf.InducePrisonerToEscape, false);
					break;
			}
		}

		private static void Watch(BreakRecord r, Thing thing, Pawn offender)
		{
			if (thing == r.watching) return;
			Resolve(r);
			if (thing == null || thing == offender) return;
			r.watching = thing;
			r.watchingLabel = LabelOf(thing);
			r.watchingValue = ValueOf(thing);
			r.watchingHp = thing.def.useHitPoints ? thing.HitPoints : -1;
			if (r.victim == null) r.victim = OwnerOf(thing, offender);
		}

		private static void Resolve(BreakRecord r)
		{
			var t = r.watching;
			r.watching = null;
			if (t == null) return;
			var hit = t is Pawn p
				? p.Dead
				: t.Destroyed || (r.watchingHp > 0 && t.HitPoints < r.watchingHp);
			if (!hit) return;
			r.count++;
			if (r.detail.NullOrEmpty() || r.watchingValue > r.detailValue)
			{
				r.detail = r.watchingLabel;
				r.detailValue = r.watchingValue;
			}
		}

		private static void NoteJob(BreakRecord r, Pawn c, JobDef jobDef, bool ownerMatters)
		{
			var job = c.CurJob;
			if (job == null || job.def != jobDef || job.loadID == r.lastJobId) return;
			r.lastJobId = job.loadID;
			r.count++;
			var t = job.targetA.Thing;
			if (t == null) return;
			var value = ValueOf(t);
			if (r.detail.NullOrEmpty() || value > r.detailValue)
			{
				r.detail = LabelOf(t);
				r.detailValue = value;
			}
			if (ownerMatters && r.victim == null) r.victim = OwnerOf(t, c);
		}

		private static void NoteFirefighters(BreakRecord r, Map map)
		{
			if (map == null) return;
			var colonists = map.mapPawns.FreeColonistsSpawned;
			for (var i = 0; i < colonists.Count && r.witnesses.Count < MaxWitnesses; i++)
			{
				var c = colonists[i];
				if (c == r.pawn || c.CurJobDef != JobDefOf.BeatFire || r.witnesses.Contains(c)) continue;
				r.witnesses.Add(c);
				r.helpers = r.witnesses.Count;
			}
		}

		private static void Close(BreakRecord r, int now)
		{
			Resolve(r);
			r.endTick = now;
			var map = r.pawn?.MapHeld;
			if (map == null) return;
			var colonists = map.mapPawns.FreeColonistsSpawned;
			for (var i = 0; i < colonists.Count && r.witnesses.Count < MaxWitnesses; i++)
			{
				var c = colonists[i];
				if (c == r.pawn || c.Dead || r.witnesses.Contains(c)) continue;
				r.witnesses.Add(c);
			}
		}

		private static float ValueOf(Thing t)
		{
			try { return t.MarketValue * System.Math.Max(1, t.stackCount); }
			catch { return 0f; }
		}

		private static string LabelOf(Thing t)
		{
			var worker = Find.ActiveLanguageWorker;
			if (t is Pawn p)
			{
				if (p.RaceProps.Humanlike || (p.Name != null && !p.Name.Numerical)) return p.LabelShortCap;
				return worker.WithIndefiniteArticle(p.KindLabel);
			}
			var label = t.LabelNoParenthesis;
			if (t.def.category == ThingCategory.Plant || t.def.stackLimit > 1)
				return worker.WithDefiniteArticle(label);
			return worker.WithIndefiniteArticle(label);
		}

		private static Pawn OwnerOf(Thing t, Pawn offender)
		{
			if (t is Pawn animal)
				return animal.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond,
					x => x != offender && x.IsColonist && !x.Dead);

			if (t is Building_Bed bed)
			{
				var owners = bed.OwnersForReading;
				for (var i = 0; i < owners.Count; i++)
					if (owners[i] != offender && owners[i].IsColonist) return owners[i];
			}

			var assign = t.TryGetComp<CompAssignableToPawn>();
			if (assign != null)
				foreach (var p in assign.AssignedPawns)
					if (p != offender && p.IsColonist) return p;

			if (!t.Spawned) return null;
			var room = t.GetRoom();
			if (room == null) return null;
			foreach (var p in room.Owners)
				if (p != offender && p.IsColonist) return p;
			return null;
		}

		private static Pawn Mourner(Pawn dead, Pawn offender)
		{
			Pawn best = null;
			var bestScore = 19f;
			var colonists = PawnsFinder.AllMaps_FreeColonistsSpawned;
			for (var i = 0; i < colonists.Count; i++)
			{
				var c = colonists[i];
				if (c == offender || c == dead || c.Dead || c.relations == null) continue;
				var rel = c.GetMostImportantRelation(dead);
				var score = rel != null ? 1000f + rel.importance : c.relations.OpinionOf(dead);
				if (score > bestScore) { bestScore = score; best = c; }
			}
			return best;
		}

		public static int Priors(List<BreakRecord> records, BreakRecord r)
		{
			var n = 0;
			for (var i = 0; i < records.Count; i++)
			{
				var x = records[i];
				if (x == r || x.pawn != r.pawn || x.kind != r.kind || x.Open) continue;
				if (x.startTick < r.startTick) n++;
			}
			return n;
		}
	}
}
