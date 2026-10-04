using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimCourt.Court
{
	public class CourtManager : GameComponent
	{
		private const int PhaseNone = 0;
		private const int PhaseConvening = 1;
		private const int PhaseHearing = 2;
		private const int PhaseReact = 3;
		private const int PhaseDepart = 4;
		private const int PhasePunishment = 5;
		private const int PhaseDuel = 6;
		private const int DuelTimeoutTicks = 5000;
		private const int PunishTimeoutTicks = 2500;
		private const int StalledPunishmentTicks = 900;

		private const int ConveneMaxTicks = 3600;
		private const int CallTimeoutTicks = 2400;
		private const int PleadTicks = 140;
		private const int ReactTicks = 300;
		private const int DepartTicks = 120;
		private const int GatherInterval = 250;
		private const int OutsiderCheckInterval = 250;
		private const int HeardMemoryDays = 20;
		private const int SamePairDays = 10;
		private const float ConsistencyBonus = 1f;
		private const float ReversalPenalty = 2f;
		private const float KinImportance = 175f;

		private List<Pawn> _walkedOut = new List<Pawn>();
		private const int LordCheckInterval = 250;
		private const int PleadNudgeInterval = 60;
		private const float SeatSearchRadius = 15f;
		private const int AudienceDistance = 4;
		private const int MinAudienceDistance = 2;
		private const float CrowdMinRadius = 2.9f;
		private const float CrowdMaxRadius = 6.5f;

		private JudgeReputation _reputation = new JudgeReputation();
		private List<OutsiderRequest> _waiting = new List<OutsiderRequest>();
		private List<Petition> _queue = new List<Petition>();
		private List<HeardCase> _heard = new List<HeardCase>();
		private Petition _current;
		private int _nextPetitionId = 1;
		private int _lastSessionTick = -1;
		private int _lastDecayTick = -1;
		private int _lastDeserterTick = -999999;
		private Pawn _epithetPawn;
		private string _epithetText;

		private bool _active;
		private Map _map;
		private Pawn _lord;
		private Pawn _lastLord;
		private Pawn _appointedLord;
		private Pawn _hand;
		private Pawn _shapedLord;
		private int _shapedVerdicts;
		private List<Betrothal> _pledges = new List<Betrothal>();
		private List<WorkExemption> _exemptions = new List<WorkExemption>();
		private List<Exile> _exiles = new List<Exile>();
		private List<RaidMark> _raids = new List<RaidMark>();
		private List<int> _triedCaptives = new List<int>();
		private Pawn _captiveGuard;
		private int _captiveTries;
		private int _callGrace;
		private bool _executionPending;
		private int _executionTier;
		private const int RaidScanInterval = 1000;
		private const int MaxGuardTries = 2;
		private const int MaxArrivalTicks = 20000;
		private const int ExecutionTimeoutTicks = 3000;

		private static readonly List<Thing> _seatScratch = new List<Thing>();

		private const int LordCacheTicks = 30;
		private Map _cacheMap;
		private Thing _cacheSeat;
		private int _cacheTick = -999999;
		private Pawn _cacheLord;
		private Pawn _cacheAuto;
		private Pawn _cacheOwner;
		private IntVec3 _spotCell = IntVec3.Invalid;
		private Thing _seat;
		private int _phase;
		private int _phaseTicks;
		private int _sessionTotal;
		private int _resolvedCount;
		private List<string> _verdictLog = new List<string>();
		private int _gatherTicks;
		private int _lordTicks;
		private int _tickFailures;
		private int _wedgeKicks;
		private Pawn _condemned;
		private Pawn _duelA;
		private Pawn _duelB;
		private bool _floggingDone;
		private bool _flogMenuPending;
		private Pawn _flogger;
		private int _lashesLanded;

		public static CourtManager Instance { get; private set; }

		public CourtManager(Game game) { Instance = this; }

		public override void FinalizeInit()
		{
			base.FinalizeInit();
			Instance = this;
		}

		public bool Active => _active;
		public Pawn Lord => _lord;
		public JudgeReputation Reputation => _reputation;

		public Pawn CurrentLord
		{
			get
			{
				if (_lord != null && !_lord.Dead) return _lord;
				if (_lastLord != null && !_lastLord.Dead) return _lastLord;
				return null;
			}
		}
		public Petition Current => _current;

		public Petition PendingVerdict
			=> _current != null && _current.state == Petition.StateAwaitingVerdict ? _current : null;

		private static int CooldownTicks => Mathf.Max(0, RimCourtMod.Settings?.cooldownDays ?? RimCourtSettings.DefCooldownDays) * 60000;
		public bool OnCooldown => _lastSessionTick >= 0 && CooldownTicks > 0
			&& Find.TickManager.TicksGame < _lastSessionTick + CooldownTicks;
		public float DaysUntilCourt => Mathf.Max(0, _lastSessionTick + CooldownTicks - Find.TickManager.TicksGame) / 60000f;

		private static int VerdictTicks => Mathf.Max(1, RimCourtMod.Settings?.verdictHours ?? RimCourtSettings.DefVerdictHours) * 2500;

		public bool TryConvene(Thing marker)
		{
			if (_active) { Reject("RimCourt.CourtAlreadyRunning".Translate()); return false; }
			var map = marker?.Map;
			if (map == null) return false;
			var wait = Mathf.CeilToInt(DaysUntilCourt);
			if (OnCooldown)
			{
				Reject((wait == 1 ? "RimCourt.CourtOnCooldownOne" : "RimCourt.CourtOnCooldown").Translate(wait));
				return false;
			}

			var seat = marker.def == RimCourtDefOf.RimCourt_AudienceSpot ? FindSeatNear(marker) : marker;
			if (seat == null) { Reject("RimCourt.NeedSeat".Translate()); return false; }

			var spotCell = FindAudienceCell(seat, map);
			if (!spotCell.IsValid) { Reject("RimCourt.NoAudienceRoom".Translate()); return false; }

			var lord = PickLord(map, seat);
			if (lord == null) { Reject("RimCourt.NeedLord".Translate()); return false; }

			if (_lastDecayTick >= 0 && !(RimCourtMod.Settings?.noDecay ?? false))
				_reputation.Decay((Find.TickManager.TicksGame - _lastDecayTick) / 60000f);
			SyncEpithet();

			var cases = GeneratePetitions(map, lord);
			if (cases.Count == 0) { Reject("RimCourt.NoPetitions".Translate()); return false; }

			_active = true;
			_map = map;
			_lord = lord;
			_lastLord = lord;
			SyncEpithet();
			_spotCell = spotCell;
			_seat = seat;
			_queue = cases;
			_current = null;
			_sessionTotal = cases.Count;
			_resolvedCount = 0;
			_verdictLog.Clear();
			_phase = PhaseConvening;
			_phaseTicks = 0;
			_gatherTicks = 0;
			_lordTicks = 0;
			_wedgeKicks = 0;

			SendLordToSeat();
			GatherCourtCrowd();

			Messages.Message((cases.Count == 1 ? "RimCourt.CourtConvenedOne" : "RimCourt.CourtConvened")
					.Translate(lord.LabelShort, cases.Count),
				new LookTargets(seat), MessageTypeDefOf.NeutralEvent, historical: false);
			return true;
		}

		public Pawn AppointedLord => _appointedLord;

		public Pawn LordFor(Map map, Thing seat = null) { RefreshLordCache(map, seat); return _cacheLord; }

		public Pawn LordAutomatic(Map map, Thing seat = null) { RefreshLordCache(map, seat); return _cacheAuto; }

		public Pawn SeatOwner(Map map, Thing seat = null) { RefreshLordCache(map, seat); return _cacheOwner; }

		public Thing SeatFor(Map map) => SeatThing(map);

		public bool DeserterCooledDown
			=> Find.TickManager.TicksGame - _lastDeserterTick >= EmpireDeserterUtility.CooldownTicks;

		public void NoteDeserterSent() { _lastDeserterTick = Find.TickManager.TicksGame; }

		public Pawn CaptiveGuard => _captiveGuard;

		public int ExecutionTier => _executionTier;

		public bool IsAwaitingExecution(Pawn p)
			=> _active && _phase == PhasePunishment && _executionPending && p != null && p == _condemned && !_floggingDone;

		public void Notify_Executed(Pawn victim)
		{
			if (victim != null && victim == _condemned) _floggingDone = true;
		}

		public bool BeginExecution(Pawn condemned, Pawn executioner, int tier)
		{
			if (!_active || condemned == null || condemned.Dead || !condemned.Spawned || executioner == null) return false;
			if (_current == null || _current.petitioner != condemned) return false;
			if (executioner.Dead || !executioner.Spawned || executioner.Downed || executioner.jobs == null) return false;

			_condemned = condemned;
			_flogger = executioner;
			_executionTier = tier;
			_executionPending = true;
			_floggingDone = false;
			_lashesLanded = 0;
			_flogMenuPending = false;
			_phase = PhasePunishment;
			_phaseTicks = 0;

			try
			{
				var stand = PleadCellFor(true);
				if (!stand.IsValid || !stand.InBounds(_map)) stand = condemned.Position;
				var wait = JobMaker.MakeJob(RimCourtDefOf.RimCourt_TakeLashes, stand, _seat?.Position ?? _spotCell);
				if (condemned.jobs != null && !condemned.jobs.TryTakeOrderedJob(wait, JobTag.Misc))
					condemned.jobs.StartJob(wait, JobCondition.InterruptForced);
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] le condamné ne veut pas attendre la lame (" + e.Message + ").");
			}
			SendExecutioner(executioner, condemned);
			return true;
		}

		private void SendExecutioner(Pawn executioner, Pawn condemned)
		{
			try
			{
				var job = JobMaker.MakeJob(RimCourtDefOf.RimCourt_ExecuteCaptive, condemned);
				job.expiryInterval = -1;
				executioner.jobs?.TryTakeOrderedJob(job, JobTag.Misc);
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] " + executioner.LabelShort + " ne prend pas la lame (" + e.Message + ").");
			}
		}

		private void ExecutionTick()
		{
			var c = _condemned;
			var done = _floggingDone || c == null || c.Dead || !c.Spawned;
			if (!done && _flogger != null && _phaseTicks % PleadNudgeInterval == 0
				&& _flogger.CurJobDef != RimCourtDefOf.RimCourt_ExecuteCaptive
				&& !_flogger.Dead && _flogger.Spawned && !_flogger.Downed && !_flogger.InMentalState)
				SendExecutioner(_flogger, c);
			if (!done && _phaseTicks >= ExecutionTimeoutTicks)
			{
				Log.Warning("[RimCourt] le bourreau n'est pas venu, la sentence est appliquée directement.");
				var exec = CaptiveUtility.Execute(c, _flogger, _lord, _executionTier);
				if (exec != null)
					Messages.Message("RimCourt.CaptiveExecuted".Translate(exec.LabelShortCap, c.LabelShortCap),
						new LookTargets(exec), MessageTypeDefOf.NegativeEvent, historical: false);
				else
					CaptiveUtility.MarkForExecution(c);
				done = true;
			}
			if (!done) return;
			_condemned = null;
			_flogger = null;
			_executionPending = false;
			_floggingDone = false;
			_phase = PhaseReact;
			_phaseTicks = 0;
		}

		private bool CaptiveEnRoute(Petition p)
		{
			if (!(p.def.Worker is PetitionWorker_Captive)) return false;
			var c = p.petitioner;
			if (c == null || c.Dead) return false;
			if (!c.Spawned) return c.CarriedBy != null;
			if (_captiveGuard == null || _captiveGuard.CurJobDef != RimCourtDefOf.RimCourt_BringCaptive) return false;
			return c.Position.DistanceTo(PleadCellFor(true)) > 2.5f;
		}

		public bool IsCaptiveHeld(Pawn p)
			=> _active && p != null && _current != null && _current.petitioner == p && _phase == PhaseHearing;

		public IntVec3 GuardCell() => _active && _seat != null ? PleadCellFor(false) : IntVec3.Invalid;

		public bool CaptiveTried(Pawn p) => p != null && _triedCaptives.Contains(p.thingIDNumber);

		public void NoteCaptiveTried(Pawn p)
		{
			if (p != null && !_triedCaptives.Contains(p.thingIDNumber)) _triedCaptives.Add(p.thingIDNumber);
		}

		public void Notify_CaptiveBrought(Pawn captive, Pawn guard)
		{
			if (!IsCaptiveHeld(captive)) return;
			EnsurePleadJob(captive, PleadCellFor(true));
		}

		public int RaidsBy(Faction f)
		{
			if (f == null) return 0;
			var n = 0;
			for (var i = 0; i < _raids.Count; i++)
				if (_raids[i].faction == f) n++;
			return n;
		}

		public void DebugNoteRaids(Faction f, int n)
		{
			if (f == null) return;
			var now = Find.TickManager.TicksGame;
			for (var i = 0; i < n; i++)
				_raids.Add(new RaidMark { faction = f, tick = now, lordId = -1000 - _raids.Count });
		}

		private void ScanRaids()
		{
			var now = Find.TickManager.TicksGame;
			var limit = CaptiveUtility.RaidMemoryDays * 60000;
			_raids.RemoveAll(r => r == null || r.faction == null || now - r.tick > limit);
			var maps = Find.Maps;
			for (var m = 0; m < maps.Count; m++)
			{
				var map = maps[m];
				if (!map.IsPlayerHome) continue;
				var lords = map.lordManager.lords;
				for (var i = 0; i < lords.Count; i++)
				{
					var lord = lords[i];
					var f = lord.faction;
					if (f == null || f.IsPlayer || !f.HostileTo(Faction.OfPlayer)) continue;
					if (!CaptiveUtility.IsRaidJob(lord.LordJob)) continue;
					var seen = false;
					for (var j = 0; j < _raids.Count; j++)
						if (_raids[j].lordId == lord.loadID) { seen = true; break; }
					if (seen) continue;
					_raids.Add(new RaidMark { faction = f, tick = now, lordId = lord.loadID });
				}
			}
		}

		public void InvalidateLordCache() { _cacheTick = -999999; _cacheMap = null; }

		private void RefreshLordCache(Map map, Thing seat)
		{
			if (seat == null) seat = SeatThing(map);
			var now = Find.TickManager.TicksGame;
			var stale = _cacheLord != null && (_cacheLord.Dead || !_cacheLord.Spawned);
			if (!stale && map == _cacheMap && seat == _cacheSeat && now - _cacheTick < LordCacheTicks) return;

			_cacheMap = map;
			_cacheSeat = seat;
			_cacheTick = now;
			_cacheOwner = SeatOwnerOf(seat);
			var ownerFit = FitToPreside(_cacheOwner, map) ? _cacheOwner : null;
			var handFit = FitToPreside(_hand, map) ? _hand : null;
			_cacheAuto = ownerFit ?? handFit ?? PickByPrestige(map);
			_cacheLord = FitToPreside(_appointedLord, map) ? _appointedLord : _cacheAuto;
		}

		private static bool FitToPreside(Pawn p, Map map)
		{
			if (!CanHoldCourt(p, map)) return false;
			if (p.ageTracker != null && p.ageTracker.AgeBiologicalYears < 16) return false;
			if (p.health?.hediffSet != null && p.health.hediffSet.AnyHediffMakesSickThought) return false;
			return true;
		}

		public List<Pawn> LordCandidates(Map map)
		{
			var list = new List<Pawn>();
			if (map == null) return list;
			foreach (var c in map.mapPawns.FreeColonistsSpawned)
			{
				if (c.Dead || c.Downed || c.InMentalState) continue;
				if (c.ageTracker != null && c.ageTracker.AgeBiologicalYears < 16) continue;
				if (c.health == null || !c.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) continue;
				list.Add(c);
			}
			list.SortByDescending(Prestige);
			return list;
		}

		public void RememberExile(Pawn pawn, bool force = false)
		{
			var e = ExileUtility.Remember(pawn, force);
			if (e == null) return;
			_exiles.RemoveAll(x => x == null || x.pawn == pawn);
			_exiles.Add(e);
		}

		public void DebugExile(Pawn pawn, bool warlord)
		{
			var e = ExileUtility.Forced(pawn, warlord);
			if (e == null) return;
			_exiles.RemoveAll(x => x == null || x.pawn == pawn);
			_exiles.Add(e);
		}

		public void RegisterSpurnedExile(Pawn pawn, int minDays, int maxDays)
		{
			var e = ExileUtility.ForcedDelayed(pawn, minDays, maxDays);
			if (e == null) return;
			_exiles.RemoveAll(x => x == null || x.pawn == pawn);
			_exiles.Add(e);
		}

		public int DebugRushExiles()
		{
			if (_exiles.Count == 0) return 0;
			var now = Find.TickManager.TicksGame;
			for (var i = 0; i < _exiles.Count; i++)
				if (_exiles[i] != null) _exiles[i].returnTick = now;
			var n = _exiles.Count;
			ExileUtility.Tick(_exiles);
			return n;
		}

		public void RememberExemption(Pawn pawn, string workDefName)
		{
			var e = WorkExemptionUtility.Grant(pawn, workDefName);
			if (e == null) return;
			_exemptions.RemoveAll(x => x == null || (x.pawn == pawn && x.workDefName == workDefName));
			_exemptions.Add(e);
		}

		public bool PledgeWedding(Pawn a, Pawn b)
		{
			var pledge = BetrothalUtility.Pledge(a, b);
			if (pledge == null) return false;
			_pledges.Add(pledge);
			return true;
		}

		private const int SuccessionInterval = 2500;

		public Pawn Hand => _hand;

		private void CheckSuccession()
		{
			var fallen = _lastLord;
			if (fallen == null || !fallen.Dead) return;
			_lastLord = null;

			var heir = HeirOf(fallen);
			if (heir == null) return;

			_appointedLord = heir;
			InvalidateLordCache();
			Find.LetterStack.ReceiveLetter(
				"RimCourt.SeatInheritedLabel".Translate(heir.LabelShortCap),
				"RimCourt.SeatInheritedText".Translate(fallen.LabelShortCap, heir.LabelShortCap),
				LetterDefOf.NeutralEvent, new LookTargets(heir));
		}

		public static Pawn HeirOf(Pawn fallen)
		{
			var royalty = fallen?.royalty;
			if (royalty == null) return null;
			try
			{
				foreach (var faction in Find.FactionManager.AllFactionsListForReading)
				{
					if (faction == null || !royalty.HasAnyTitleIn(faction)) continue;
					var heir = royalty.GetHeir(faction);
					if (heir != null && !heir.Dead && heir.Spawned
						&& heir.IsFreeColonist && heir != fallen)
						return heir;
				}
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] recherche d'héritier impossible (" + e.Message + ").");
			}
			return null;
		}

		public void SetHand(Pawn pawn)
		{
			var changed = pawn != null && pawn != _hand;
			_hand = pawn;
			InvalidateLordCache();
			if (!changed || !pawn.Spawned || pawn.Map == null) return;
			if (RimCourtDefOf.RimCourt_HandNamed == null) return;
			try
			{
				var data = FleckMaker.GetDataThrowMetaIcon(pawn.Position, pawn.Map,
					RimCourtDefOf.RimCourt_HandNamed);
				data.scale = 1.4f;
				data.rotationRate = Rand.Range(90f, 140f) * (Rand.Bool ? 1f : -1f);
				pawn.Map.flecks.CreateFleck(data);
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] la chaîne de la Main ne s'est pas montrée (" + e.Message + ").");
			}
		}

		public void SetAppointedLord(Pawn pawn, Map map = null, Thing seat = null)
		{
			_appointedLord = pawn != null && map != null && pawn == PickAutomatic(map, seat)
				? null
				: pawn;
			InvalidateLordCache();
		}

		public void AppointLord(Pawn usurper, Pawn deposed)
		{
			if (usurper == null || usurper.Dead) return;
			_appointedLord = usurper;
			InvalidateLordCache();
			_reputation.DebugSet(JudgeReputation.StageNone);
			if (RimCourtDefOf.RimCourt_Tale_ThroneTaken != null && deposed != null)
				TaleRecorder.RecordTale(RimCourtDefOf.RimCourt_Tale_ThroneTaken, usurper, deposed);
			Find.LetterStack.ReceiveLetter(
				"RimCourt.ThroneTakenLabel".Translate(),
				"RimCourt.ThroneTakenText".Translate(usurper.LabelShort, deposed?.LabelShort ?? "?"),
				LetterDefOf.NeutralEvent, new LookTargets(usurper));
		}

		public void CancelCourt()
		{
			if (!_active) return;
			Abort("RimCourt.CourtCancelled".Translate());
		}

		public IReadOnlyList<OutsiderRequest> Waiting => _waiting;

		public bool HasWaitingOutsider(Map map)
		{
			for (var i = 0; i < _waiting.Count; i++)
				if (_waiting[i].Valid && _waiting[i].pawn.MapHeld == map) return true;
			return false;
		}

		public OutsiderRequest RequestFor(Pawn pawn)
		{
			if (pawn == null) return null;
			for (var i = 0; i < _waiting.Count; i++)
				if (_waiting[i].pawn == pawn || _waiting[i].otherPawn == pawn) return _waiting[i];
			return null;
		}

		public void CloseRequest(Pawn pawn)
		{
			var req = RequestFor(pawn);
			if (req != null) _waiting.Remove(req);
		}

		public void RegisterOutsider(OutsiderRequest req)
		{
			if (req == null || !req.Valid) return;
			_waiting.Add(req);
		}

		public bool WasHeardRecently(Pawn a, Pawn b, PetitionDef def)
		{
			if (a == null || def == null) return false;
			var now = Find.TickManager.TicksGame;
			var limit = HeardMemoryDays * 60000;
			var bid = b?.thingIDNumber ?? -1;
			var pairLimit = SamePairDays * 60000;
			for (var i = 0; i < _heard.Count; i++)
			{
				var h = _heard[i];
				var samePair = (h.a == a.thingIDNumber && h.b == bid)
					|| (h.a == bid && h.b == a.thingIDNumber);
				if (!samePair) continue;
				if (now - h.tick <= pairLimit) return true;
				if (h.defName == def.defName && now - h.tick <= limit) return true;
			}
			return false;
		}

		private void RecordHeard(Petition p, VerdictOption v)
		{
			if (p?.def == null || p.petitioner == null) return;
			var now = Find.TickManager.TicksGame;
			var limit = HeardMemoryDays * 60000;
			_heard.RemoveAll(h => now - h.tick > limit);
			_heard.Add(new HeardCase
			{
				a = p.petitioner.thingIDNumber,
				b = p.other?.thingIDNumber ?? -1,
				aName = p.petitioner.LabelShortCap,
				bName = p.other?.LabelShortCap,
				defName = p.def.defName,
				outcome = v?.outcome,
				axis = v == null ? HeardCase.AxisUnset : AxisOf(v),
				tick = now,
			});
		}

		public List<HeardCase> Heard => _heard;

		public int DebugRushWeddings()
		{
			if (_pledges.Count == 0) return 0;
			var now = Find.TickManager.TicksGame;
			for (var i = 0; i < _pledges.Count; i++)
				if (_pledges[i] != null) _pledges[i].dueTick = now;
			var n = _pledges.Count;
			BetrothalUtility.Tick(_pledges);
			return n;
		}

		public HeardCase PrecedentFor(PetitionDef def)
		{
			if (def == null || !def.Worker.UsesPrecedent) return null;
			var now = Find.TickManager.TicksGame;
			var limit = HeardMemoryDays * 60000;
			HeardCase best = null;
			for (var i = 0; i < _heard.Count; i++)
			{
				var h = _heard[i];
				if (h.defName != def.defName || h.outcome.NullOrEmpty()) continue;
				if (now - h.tick > limit) continue;
				if (best == null || h.tick > best.tick) best = h;
			}
			return best;
		}

		public static PawnRelationDef KinToLord(Pawn party, Pawn lord)
		{
			if (party == null || lord == null || party == lord) return null;
			if (party.relations == null || lord.relations == null) return null;
			var rel = lord.GetMostImportantRelation(party);
			return rel != null && rel.importance >= KinImportance ? rel : null;
		}

		public static Pawn FavouredParty(Petition p, VerdictOption v)
		{
			if (p == null || v == null) return null;
			if (p.def?.Worker is PetitionWorker_Captive)
				return v.outcome == "Release" || v.outcome == "Ransom" ? p.petitioner : null;
			if (v.outcome == "FavorOther") return p.other;
			if (v.outcome == "Reconcile" || v.outcome == "ThroneDuel") return null;
			return AxisOf(v) == JudgeReputation.StageJust ? p.petitioner : null;
		}

		public static Pawn WrongedParty(Petition p, VerdictOption v)
		{
			if (p == null || v == null) return null;
			var favoured = FavouredParty(p, v);
			if (favoured != null) return favoured == p.petitioner ? p.other : p.petitioner;
			return p.petitioner;
		}

		private bool ApplyRefusal(Pawn p, VerdictOption v)
		{
			if (p == null || _lord == null) return false;
			if (!RefusalUtility.CanBeRefused(v)) return false;
			if (!RefusalUtility.Refuses(p, _lord, _reputation)) return false;

			_reputation.weak += 2f;
			_reputation.Recompute();

			Mem(p, RimCourtDefOf.RimCourt_DefiedTheLord);
			if (_map != null && RimCourtDefOf.RimCourt_LordWasDefied != null && !_lord.Dead)
			{
				foreach (var c in _map.mapPawns.FreeColonistsSpawned)
				{
					if (c == _lord || c == p) continue;
					c.needs?.mood?.thoughts?.memories?.TryGainMemory(
						RimCourtDefOf.RimCourt_LordWasDefied, _lord);
				}
			}

			WalkOut(p);
			Messages.Message("RimCourt.VerdictRefused".Translate(p.LabelShortCap, _lord.LabelShort),
				new LookTargets(p), MessageTypeDefOf.NegativeEvent, historical: false);
			return true;
		}

		private static void Mem(Pawn p, ThoughtDef thought)
		{
			if (p == null || p.Dead || thought == null) return;
			p.needs?.mood?.thoughts?.memories?.TryGainMemory(thought);
		}

		private void WalkOut(Pawn p)
		{
			if (!_walkedOut.Contains(p)) _walkedOut.Add(p);
			if (_map == null || p.jobs == null) return;
			try
			{
				p.jobs.StopAll();
				var away = p.Position - _spotCell;
				var dir = away.LengthHorizontalSquared > 0
					? away.ToVector3().normalized
					: Rand.InsideUnitCircleVec3.normalized;
				for (var d = 14; d >= 6; d--)
				{
					var cell = p.Position + IntVec3.FromVector3(dir * d);
					if (!cell.InBounds(_map) || !cell.Standable(_map)) continue;
					if (!p.CanReach(cell, PathEndMode.OnCell, Danger.Deadly)) continue;
					p.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, cell), JobTag.Misc);
					return;
				}
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] sortie de salle impossible (" + e.Message + ").");
			}
		}

		private void NoteFavour(Pawn favoured, PawnRelationDef rel)
		{
			if (favoured == null || rel == null) return;
			if (_map != null && RimCourtDefOf.RimCourt_LordFavouredHisOwn != null
				&& _lord != null && !_lord.Dead)
			{
				foreach (var c in _map.mapPawns.FreeColonistsSpawned)
				{
					if (c == _lord || c == favoured) continue;
					c.needs?.mood?.thoughts?.memories?.TryGainMemory(
						RimCourtDefOf.RimCourt_LordFavouredHisOwn, _lord);
				}
			}
			Messages.Message("RimCourt.Nepotism".Translate(_lord?.LabelShortCap ?? "?",
					favoured.LabelShortCap, rel.GetGenderSpecificLabel(favoured)),
				new LookTargets(favoured), MessageTypeDefOf.NegativeEvent, historical: false);
		}

		public static int AxisOf(VerdictOption v)
		{
			if (v == null) return JudgeReputation.StageNone;
			var best = JudgeReputation.StageNone;
			var top = 0f;
			if (v.just > top) { top = v.just; best = JudgeReputation.StageJust; }
			if (v.harsh > top) { top = v.harsh; best = JudgeReputation.StageHarsh; }
			if (v.venal > top) { top = v.venal; best = JudgeReputation.StageVenal; }
			if (v.weak > top) { top = v.weak; best = JudgeReputation.StageWeak; }
			return best;
		}

		public static VerdictOption OptionByOutcome(PetitionDef def, string outcome)
		{
			if (def?.verdicts == null || outcome.NullOrEmpty()) return null;
			for (var i = 0; i < def.verdicts.Count; i++)
				if (def.verdicts[i].outcome == outcome) return def.verdicts[i];
			return null;
		}

		public static int PrecedentAxis(PetitionDef def, HeardCase past)
		{
			if (past == null) return JudgeReputation.StageNone;
			if (past.axis != HeardCase.AxisUnset) return past.axis;
			return AxisOf(OptionByOutcome(def, past.outcome));
		}

		private void ApplyPrecedent(Petition p, VerdictOption v, HeardCase past)
		{
			var then = PrecedentAxis(p.def, past);
			var now = AxisOf(v);
			if (then == JudgeReputation.StageNone || now == JudgeReputation.StageNone) return;

			if (then == now)
			{
				if (now == JudgeReputation.StageWeak) return;
				_reputation.just += ConsistencyBonus;
				Messages.Message("RimCourt.PrecedentHeld".Translate(_lord?.LabelShort ?? "?"),
					MessageTypeDefOf.PositiveEvent, historical: false);
			}
			else
			{
				_reputation.weak += ReversalPenalty;
				Messages.Message("RimCourt.PrecedentBroken".Translate(_lord?.LabelShort ?? "?"),
					MessageTypeDefOf.NegativeEvent, historical: false);
			}
			_reputation.Recompute();
		}

		public Petition GetPetition(int id)
		{
			if (_current != null && _current.id == id) return _current;
			for (var i = 0; i < _queue.Count; i++)
				if (_queue[i].id == id) return _queue[i];
			return null;
		}

		public bool ShouldStand(Pawn pawn)
		{
			if (!_active || _current == null) return false;
			if (!_current.Involves(pawn)) return false;
			if (pawn == _condemned) return false;
			if (_phase == PhaseDuel) return false;
			return _phase == PhaseHearing || _phase == PhaseReact || _phase == PhasePunishment;
		}

		public bool IsBeingFlogged(Pawn pawn)
			=> _active && _phase == PhasePunishment && pawn != null && pawn == _condemned && !_floggingDone;

		public void Notify_FloggingDone(Pawn victim)
		{
			if (victim != null && victim == _condemned) _floggingDone = true;
		}

		public void Notify_LashLanded(Pawn victim)
		{
			if (victim != null && victim == _condemned) _lashesLanded++;
		}

		public bool IsPetitioner(Pawn pawn)
			=> _current != null && _current.petitioner == pawn && _current.state == Petition.StatePleading;

		public string NextPleadLine()
			=> ("RimCourt.Plead" + Rand.RangeInclusive(1, 4)).Translate();

		public float HoursLeft(Petition p)
			=> p == null || p.deadlineTick < 0 ? 0f : Mathf.Max(0f, (p.deadlineTick - Find.TickManager.TicksGame) / 2500f);

		public void Notify_PleadFinished(Pawn pawn)
		{
			var p = _current;
			if (p == null || p.state != Petition.StatePleading || pawn != p.petitioner) return;
			if (!PartiesInPlace(p)) return;
			SendVerdictLetter(p);
		}

		private bool PartiesInPlace(Petition p)
		{
			if (p?.other == null) return true;
			if (!PartyOk(p.other)) return true;
			var seat = _seat?.Position ?? _spotCell;
			return p.other.Position.DistanceTo(_spotCell) <= 6f
				|| p.other.Position.DistanceTo(seat) <= 8f;
		}

		public void ChooseVerdict(int petitionId, int index, bool timedOut = false)
		{
			var p = GetPetition(petitionId);
			if (p == null || p.state != Petition.StateAwaitingVerdict) return;
			if (index < 0 || index >= p.def.verdicts.Count) index = DismissIndex(p.def);
			var v = p.def.Worker.Weigh(p, p.def.verdicts[index]);
			p.verdictIndex = index;
			p.state = Petition.StateResolved;
			RemoveVerdictLetter(p);

			var stageBefore = _reputation.Stage;
			var past = timedOut ? null : PrecedentFor(p.def);
			var favoured = timedOut ? null : FavouredParty(p, v);
			var kin = KinToLord(favoured, _lord);
			var refused = false;
			try
			{
				if (timedOut) p.def.Worker.ApplyTimeout(p, v, _lord);
				else p.def.Worker.ApplyVerdict(p, v, _lord);
				if (timedOut)
				{
					_reputation.weak += 2f;
					_reputation.Recompute();
				}
				else if (kin != null)
				{
					_reputation.AddAsFavour(v);
					NoteFavour(favoured, kin);
				}
				else
				{
					_reputation.Add(v);
				}
				LordNature.Note(_lord, timedOut ? JudgeReputation.StageWeak : AxisOf(v));
				if (_lord != _shapedLord) { _shapedLord = _lord; _shapedVerdicts = 0; }
				_shapedVerdicts++;
				if ((RimCourtMod.Settings?.shapeLord ?? true)
					&& LordNature.TryShape(_lord, _reputation, _shapedVerdicts))
					_shapedVerdicts = 0;
				if (!timedOut) refused = ApplyRefusal(WrongedParty(p, v), v);
				if (past != null && !refused) ApplyPrecedent(p, v, past);
				if (!timedOut && !refused && kin == null
					&& AxisOf(v) == JudgeReputation.StageJust
					&& _reputation.Stage == JudgeReputation.StageJust)
					EmpireHonorUtility.NoteJustRuling(p, _lord);
			}
			catch (System.Exception e)
			{
				Log.Error("[RimCourt] application du verdict : " + e);
			}
			SyncEpithet();
			NotifyReputationShift(stageBefore);

			var label = timedOut
				? "RimCourt.NoAnswerLabel".Translate().ToString()
				: p.def.Worker.PastVerdictLabel(p, index);
			_verdictLog.Add(p.other != null
				? "RimCourt.LogVerdict".Translate(p.petitioner.LabelShortCap, p.other.LabelShortCap, label)
				: "RimCourt.LogVerdictSolo".Translate(p.petitioner.LabelShortCap, label));
			_resolvedCount++;
			RecordHeard(p, timedOut || refused ? null : v);

			ThrowReactions(p, v);
			if (_lord != null)
				Messages.Message(timedOut
						? "RimCourt.NoAnswerMessage".Translate(_lord.LabelShort, p.petitioner.LabelShortCap)
						: "RimCourt.VerdictRendered".Translate(_lord.LabelShort, label.CapitalizeFirst()),
					new LookTargets(p.petitioner), MessageTypeDefOf.NeutralEvent, historical: false);

			if (p == _current && _phase != PhasePunishment && _phase != PhaseDuel)
			{
				_phase = PhaseReact;
				_phaseTicks = 0;
			}

			if (p.deserter)
			{
				p.deserter = false;
				Step(() => EmpireDeserterUtility.Strike(p.petitioner, _lord));
			}
		}

		public bool BeginDuel(Pawn a, Pawn b)
		{
			if (!_active || a == null || b == null) return false;
			if (a.Dead || b.Dead || !a.Spawned || !b.Spawned) return false;
			if (a.interactions == null) return false;
			try
			{
				a.interactions.StartSocialFight(b, "RimCourt.DuelStarted");
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] le duel n'a pas pu commencer (" + e.Message + ").");
				return false;
			}
			_duelA = a;
			_duelB = b;
			_phase = PhaseDuel;
			_phaseTicks = 0;
			return true;
		}

		private void DuelTick()
		{
			var stillFighting = InDuel(_duelA) || InDuel(_duelB);
			if (stillFighting && _phaseTicks < DuelTimeoutTicks) return;

			var loser = PickDuelLoser();
			if (loser != null && _current != null && _lord != null)
			{
				var winner = loser == _duelA ? _duelB : _duelA;
				Step(() => _current.def.Worker.ResolveDuel(winner, loser, _lord));
				Step(() =>
				{
					if (RimCourtDefOf.RimCourt_Tale_TrialByCombat != null)
						TaleRecorder.RecordTale(RimCourtDefOf.RimCourt_Tale_TrialByCombat, winner, loser);
				});
				Messages.Message("RimCourt.DuelWon".Translate(winner.LabelShortCap, loser.LabelShortCap),
					new LookTargets(winner), MessageTypeDefOf.NeutralEvent, historical: false);
			}
			_duelA = null;
			_duelB = null;
			_phase = PhaseReact;
			_phaseTicks = 0;
		}

		private static bool InDuel(Pawn p)
			=> p != null && !p.Dead && p.Spawned && p.InMentalState
				&& p.MentalStateDef == MentalStateDefOf.SocialFighting;

		private Pawn PickDuelLoser()
		{
			if (_duelA == null || _duelB == null) return null;
			if (_duelA.Dead || _duelA.Downed) return _duelA;
			if (_duelB.Dead || _duelB.Downed) return _duelB;
			var ha = _duelA.health?.summaryHealth?.SummaryHealthPercent ?? 1f;
			var hb = _duelB.health?.summaryHealth?.SummaryHealthPercent ?? 1f;
			return ha <= hb ? _duelA : _duelB;
		}

		public bool BeginStocks(Pawn condemned)
		{
			if (condemned == null || condemned.Dead || !condemned.Spawned) return false;
			var map = condemned.Map;
			if (map == null) return false;
			var list = map.listerThings.ThingsOfDef(RimCourtDefOf.RimCourt_Stocks);
			Building_Stocks best = null;
			var bestD = float.MaxValue;
			for (var i = 0; i < list.Count; i++)
			{
				if (!(list[i] is Building_Stocks st) || !st.IsFree) continue;
				var d = st.Position.DistanceToSquared(condemned.Position);
				if (d < bestD) { bestD = d; best = st; }
			}
			if (best == null) return false;
			var post = best;
			var hours = RimCourtMod.Settings?.stocksHours ?? RimCourtSettings.DefStocksHours;
			var candidates = FlogUtility.FloggerCandidates(map, condemned);
			if (candidates.Count == 0) { HoldAtPost(post, condemned, hours, null); return true; }
			Find.WindowStack.Add(new UI.Dialog_PickFlogger(condemned, candidates, _lord, _current?.petitioner,
				picked => HoldAtPost(post, condemned, hours, picked),
				"RimCourt.EscortDialogTitle", "RimCourt.EscortDialogDesc",
				condemned.IsPrisonerOfColony ? "RimCourt.EscortNoOneCaptive" : "RimCourt.EscortNoOne", false));
			return true;
		}

		private void HoldAtPost(Building_Stocks post, Pawn condemned, int hours, Pawn escort)
		{
			var captive = condemned.IsPrisonerOfColony;
			if (captive && escort == null) escort = CaptiveUtility.PickGuard(condemned.Map, condemned, _lord);
			if (!post.Hold(condemned, hours * 2500, escort))
			{
				if (captive)
				{
					Messages.Message("RimCourt.CaptiveCantReachPost".Translate(condemned.LabelShortCap),
						new LookTargets(condemned), MessageTypeDefOf.NeutralEvent, historical: false);
					CaptiveUtility.SendBackToCell(condemned);
					return;
				}
				Messages.Message("RimCourt.StocksUnreachableLash".Translate(condemned.LabelShortCap),
					MessageTypeDefOf.NeutralEvent, historical: false);
				BeginFlogging(condemned);
				return;
			}
			if (post.Escort != null)
				Messages.Message("RimCourt.StocksTaken".Translate(condemned.LabelShortCap, post.Escort.LabelShortCap, hours),
					new LookTargets(post), MessageTypeDefOf.NegativeEvent, historical: false);
			else
				Messages.Message("RimCourt.PutInStocks".Translate(condemned.LabelShortCap, hours),
					new LookTargets(post), MessageTypeDefOf.NegativeEvent, historical: false);
		}

		public void BeginFlogging(Pawn condemned)
		{
			if (!_active || condemned == null || condemned.Dead || !condemned.Spawned) return;

			_condemned = condemned;
			_floggingDone = false;
			_lashesLanded = 0;
			_phase = PhasePunishment;
			_phaseTicks = 0;
			_flogMenuPending = true;

			try
			{
				var stand = PleadCellFor(false);
				if (!stand.IsValid || !stand.InBounds(_map)) stand = condemned.Position;
				var wait = JobMaker.MakeJob(RimCourtDefOf.RimCourt_TakeLashes,
					stand, _seat?.Position ?? _spotCell);
				if (condemned.jobs != null && !condemned.jobs.TryTakeOrderedJob(wait, JobTag.Misc))
					condemned.jobs.StartJob(wait, JobCondition.InterruptForced);
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] le condamné ne veut pas se présenter (" + e.Message + ").");
			}

		}


		private void OpenFloggerMenu()
		{
			_flogMenuPending = false;
			var condemned = _condemned;
			if (condemned == null || condemned.Dead || !condemned.Spawned) { _floggingDone = true; return; }

			var candidates = FlogUtility.FloggerCandidates(_map, condemned);
			if (candidates.Count == 0)
			{
				for (var i = 0; i < FlogUtility.DefaultLashes; i++)
				{
					if (condemned.Dead || condemned.Downed) break;
					FlogUtility.OneLash(null, condemned);
				}
				Messages.Message("RimCourt.NoFlogger".Translate(condemned.LabelShortCap),
					new LookTargets(condemned), MessageTypeDefOf.NeutralEvent, historical: false);
				_floggingDone = true;
				return;
			}

			Find.WindowStack.Add(new UI.Dialog_PickFlogger(condemned, candidates, _lord,
				_current?.petitioner,
				picked =>
				{
					if (picked != null) { SendFlogger(picked, condemned); return; }
					for (var i = 0; i < FlogUtility.DefaultLashes; i++)
					{
						if (condemned.Dead || condemned.Downed) break;
						FlogUtility.OneLash(null, condemned);
					}
					_floggingDone = true;
				}));
		}

		private void SendFlogger(Pawn flogger, Pawn condemned)
		{
			if (flogger == null || condemned == null) return;
			_flogger = flogger;
			try
			{
				var job = JobMaker.MakeJob(RimCourtDefOf.RimCourt_Flog, condemned);
				job.count = FlogUtility.DefaultLashes;
				var took = flogger.jobs != null && flogger.jobs.TryTakeOrderedJob(job, JobTag.Misc);
				if (Prefs.DevMode)
					Log.Message("[RimCourt] " + flogger.LabelShort + " prend le fouet : " + took
						+ " (job=" + (flogger.CurJobDef?.defName ?? "-") + ")");
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] " + flogger.LabelShort + " refuse de tenir le fouet (" + e.Message + ").");
				_floggingDone = true;
			}
		}

		public void DebugClearCooldown() => _lastSessionTick = -1;

		public int DebugForgetHeard()
		{
			var n = _heard.Count;
			_heard.Clear();
			return n;
		}

		public void DebugSetReputation(int stage)
		{
			_reputation.DebugSet(stage);
			SyncEpithet();
			Messages.Message("RimCourt debug : " + _reputation.TitleKey.Translate(),
				MessageTypeDefOf.NeutralEvent, historical: false);
		}

		public string DebugState()
		{
			var sb = new System.Text.StringBuilder();
			sb.AppendLine("[RimCourt] active=" + _active + " phase=" + _phase + " phaseTicks=" + _phaseTicks);
			sb.AppendLine("  lord=" + (_lord?.LabelShort ?? "-") + " lastLord=" + (_lastLord?.LabelShort ?? "-"));
			sb.AppendLine("  seat=" + (_seat?.Position.ToString() ?? "-")
				+ " spotCell=" + (_spotCell.IsValid ? _spotCell.ToString() : "-"));
			sb.AppendLine("  queue=" + _queue.Count + " current=" + (_current?.def?.defName ?? "-")
				+ " state=" + (_current?.state ?? -1));
			sb.AppendLine("  just=" + _reputation.just + " harsh=" + _reputation.harsh
				+ " venal=" + _reputation.venal + " weak=" + _reputation.weak);
			sb.AppendLine("  lashesLanded=" + _lashesLanded);
			sb.AppendLine("  heard=" + _heard.Count + " waiting=" + _waiting.Count
				+ " pledges=" + _pledges.Count + " exemptions=" + _exemptions.Count
				+ " exiles=" + _exiles.Count);
			var now = Find.TickManager.TicksGame;
			for (var i = _heard.Count - 1; i >= 0 && i >= _heard.Count - 5; i--)
			{
				var h = _heard[i];
				sb.AppendLine("    " + h.defName + " " + (h.aName ?? "?")
					+ (h.bName.NullOrEmpty() ? "" : " / " + h.bName)
					+ " -> " + (h.outcome ?? "(sans réponse)")
					+ " il y a " + ((now - h.tick) / 60000f).ToString("F1") + " j");
			}
			sb.AppendLine("  condemned=" + (_condemned?.LabelShort ?? "-")
				+ " flogger=" + (_flogger?.LabelShort ?? "-")
				+ " floggingDone=" + _floggingDone + " menuPending=" + _flogMenuPending);
			if (_flogger != null)
				sb.AppendLine("  floggerJob=" + (_flogger.CurJobDef?.defName ?? "-")
					+ " toil=" + (_flogger.jobs?.curDriver?.CurToilIndex ?? -1));
			if (_condemned != null)
				sb.AppendLine("  condemnedJob=" + (_condemned.CurJobDef?.defName ?? "-")
					+ " health=" + (_condemned.health?.summaryHealth?.SummaryHealthPercent ?? -1f).ToString("F2"));
			sb.Append("  stage=" + _reputation.Stage + " cases" + _reputation.CaseBonus.ToStringWithSign()
				+ " threshold" + _reputation.ThresholdShift.ToStringWithSign());
			return sb.ToString();
		}

		private void NotifyReputationShift(int stageBefore)
		{
			var stageNow = _reputation.Stage;
			if (stageNow == stageBefore || stageNow == JudgeReputation.StageNone) return;
			var title = _reputation.TitleKey.Translate();
			var body = stageBefore == JudgeReputation.StageNone
				? "RimCourt.RepEarnedText".Translate(_lord?.LabelShort ?? "?", title)
				: "RimCourt.RepShiftedText".Translate(_lord?.LabelShort ?? "?", title);
			Find.LetterStack.ReceiveLetter(
				"RimCourt.RepLetterLabel".Translate(title),
				body + "\n\n" + ("RimCourt.RepEffect" + stageNow).Translate()
					+ EpithetLetterLine(),
				LetterDefOf.NeutralEvent,
				_lord != null ? new LookTargets(_lord) : default);
		}

		private string EpithetLetterLine()
		{
			if (_epithetPawn == null || _epithetPawn != _lord) return "";
			return "\n\n" + "RimCourt.EpithetLine".Translate(_lord.Name.ToStringShort).ToString();
		}

		private Pawn EpithetCandidate => _lord ?? _lastLord;

		private void SyncEpithet()
		{
			var on = RimCourtMod.Settings?.epithets ?? true;
			var stage = _reputation.Stage;
			var cand = EpithetCandidate;
			var target = on && stage != JudgeReputation.StageNone && cand != null && !cand.Dead ? cand : null;
			var text = target != null ? EpithetFor(target, stage) : null;

			if (_epithetPawn != null && (_epithetPawn != target || _epithetText != text))
			{
				var old = _epithetPawn;
				var oldText = _epithetText;
				_epithetPawn = null;
				_epithetText = null;
				if (!old.Dead && StripEpithet(old, oldText) && old.Spawned)
					Messages.Message("RimCourt.EpithetDropped".Translate(old.Name.ToStringShort, oldText.Trim()),
						new LookTargets(old), MessageTypeDefOf.NeutralEvent, historical: false);
			}

			if (target != null && _epithetPawn == null && ApplyEpithet(target, text))
			{
				_epithetPawn = target;
				_epithetText = text;
			}
		}

		private static string EpithetFor(Pawn p, int stage)
		{
			var key = (p.gender == Gender.Female ? "RimCourt.EpithetF" : "RimCourt.Epithet") + stage;
			return " " + key.Translate().ToString().Trim();
		}

		private static bool ApplyEpithet(Pawn p, string text)
		{
			if (p?.Name == null || text.NullOrEmpty()) return false;
			try
			{
				if (p.Name.GetType() == typeof(NameTriple))
				{
					var n = (NameTriple)p.Name;
					if (n.Nick.EndsWith(text)) return true;
					p.Name = new NameTriple(n.First, n.Nick + text, n.Last);
					return true;
				}
				if (p.Name.GetType() == typeof(NameSingle))
				{
					var n = (NameSingle)p.Name;
					if (n.Name.EndsWith(text)) return true;
					p.Name = new NameSingle(n.Name + text, false);
					return true;
				}
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] épithète non posée (" + e.Message + ").");
			}
			return false;
		}

		private static bool StripEpithet(Pawn p, string text)
		{
			if (p?.Name == null || text.NullOrEmpty()) return false;
			try
			{
				if (p.Name.GetType() == typeof(NameTriple))
				{
					var n = (NameTriple)p.Name;
					if (!n.Nick.EndsWith(text)) return false;
					p.Name = new NameTriple(n.First, n.Nick.Substring(0, n.Nick.Length - text.Length), n.Last);
					return true;
				}
				if (p.Name.GetType() == typeof(NameSingle))
				{
					var n = (NameSingle)p.Name;
					if (!n.Name.EndsWith(text)) return false;
					p.Name = new NameSingle(n.Name.Substring(0, n.Name.Length - text.Length), false);
					return true;
				}
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] épithète non retirée (" + e.Message + ").");
			}
			return false;
		}

		public string InspectLine()
		{
			var rep = "RimCourt.RepLine".Translate(_reputation.TitleKey.Translate()).ToString();
			if (_reputation.Total > 0f)
				rep += "\n" + "RimCourt.RepDetail".Translate(
					Mathf.RoundToInt(_reputation.just), Mathf.RoundToInt(_reputation.harsh),
					Mathf.RoundToInt(_reputation.venal), Mathf.RoundToInt(_reputation.weak));
			if (!_active)
			{
				var map = Find.CurrentMap;
				var who = map != null ? LordFor(map) : null;
				if (who != null)
				{
					string key;
					if (_appointedLord == who) key = "RimCourt.LordNamed";
					else if (SeatOwner(map) == who) key = "RimCourt.LordByThrone";
					else if (_hand == who) key = "RimCourt.LordByHand";
					else key = "RimCourt.LordByPrestige";
					rep += "\n" + key.Translate(who.LabelShortCap);
				}
				return rep;
			}
			var hearing = Mathf.Min(_resolvedCount + 1, _sessionTotal);
			return "RimCourt.InspectHearing".Translate(hearing, _sessionTotal) + "\n" + rep;
		}

		public override void GameComponentTick()
		{
			if (_waiting.Count > 0 && Find.TickManager.TicksGame % OutsiderCheckInterval == 0)
				TickWaitingOutsiders();
			if (_pledges.Count > 0 && Find.TickManager.TicksGame % BetrothalUtility.CheckInterval == 0)
				BetrothalUtility.Tick(_pledges);
			if (_exemptions.Count > 0 && Find.TickManager.TicksGame % WorkExemptionUtility.CheckInterval == 0)
				WorkExemptionUtility.Tick(_exemptions, _reputation, CurrentLord);
			if (_exiles.Count > 0 && Find.TickManager.TicksGame % ExileUtility.CheckInterval == 0)
				ExileUtility.Tick(_exiles);
			if (_lastLord != null && Find.TickManager.TicksGame % SuccessionInterval == 0)
				CheckSuccession();
			if (Find.TickManager.TicksGame % RaidScanInterval == 0) ScanRaids();
			if (!_active) return;
			try
			{
				SessionTick();
				_tickFailures = 0;
			}
			catch (System.Exception e)
			{
				_tickFailures++;
				if (_tickFailures == 1)
					Log.Error("[RimCourt] erreur pendant le tick de l'audience : " + e);
				if (_tickFailures >= 300)
				{
					Log.Error("[RimCourt] l'audience plante en boucle, annulation automatique.");
					try { Abort("RimCourt.CourtCancelled".Translate()); }
					catch { _active = false; }
				}
			}
		}

		private void TickWaitingOutsiders()
		{
			if (_active) return;

			for (var i = _waiting.Count - 1; i >= 0; i--)
			{
				var invalid = _waiting[i];
				if (invalid.Valid) continue;
				_waiting.RemoveAt(i);
				Step(() => FarewellOutsider(invalid.pawn));
				Step(() => FarewellOutsider(invalid.otherPawn));
			}

			var now = Find.TickManager.TicksGame;
			for (var i = _waiting.Count - 1; i >= 0; i--)
			{
				var req = _waiting[i];
				if (req.expiryTick < 0 || now < req.expiryTick) continue;
				_waiting.RemoveAt(i);
				Messages.Message("RimCourt.OutsiderGaveUp".Translate(req.pawn.LabelShortCap),
					new LookTargets(req.pawn), MessageTypeDefOf.NeutralEvent, historical: false);
				Step(() => OutsiderUtility.AffectGoodwill(req.faction, -3));
				Step(() => OutsiderUtility.SendHome(req.pawn));
				if (req.otherPawn != null)
				{
					Step(() => OutsiderUtility.AffectGoodwill(req.otherFaction, -3));
					Step(() => OutsiderUtility.SendHome(req.otherPawn));
				}
			}
		}

		private void SessionTick()
		{
			if (_map == null || !Find.Maps.Contains(_map)) { Abort("RimCourt.CourtCancelled".Translate()); return; }
			var lordDueling = _phase == PhaseDuel && (_lord == _duelA || _lord == _duelB);
			if (_lord == null || _lord.Dead || !_lord.Spawned || _lord.Map != _map
				|| (!lordDueling && (_lord.Downed || _lord.InMentalState)))
			{
				Abort("RimCourt.CourtLordGone".Translate());
				return;
			}
			if (!_spotCell.IsValid || _seat == null || _seat.Destroyed)
			{
				Abort("RimCourt.CourtCancelled".Translate());
				return;
			}
			if (_map.dangerWatcher.DangerRating == StoryDanger.High)
			{
				Abort("RimCourt.CourtScattered".Translate());
				return;
			}

			if (++_gatherTicks >= GatherInterval)
			{
				_gatherTicks = 0;
				GatherCourtCrowd();
			}
			if (!lordDueling && ++_lordTicks >= LordCheckInterval)
			{
				_lordTicks = 0;
				KeepLordSeated();
			}

			_phaseTicks++;
			switch (_phase)
			{
				case PhaseConvening: ConveningTick(); break;
				case PhaseHearing: HearingTick(); break;
				case PhaseReact: ReactTick(); break;
				case PhasePunishment: PunishmentTick(); break;
				case PhaseDuel: DuelTick(); break;
				case PhaseDepart: DepartTick(); break;
				default: WedgeKick(); break;
			}
		}

		private void ConveningTick()
		{
			var seated = _lord.Position.DistanceTo(_seat.Position) <= 2.5f;
			if (seated || _phaseTicks >= ConveneMaxTicks)
				StartNextCaseOrAdjourn();
		}

		private void HearingTick()
		{
			var p = _current;
			if (p == null) { StartNextCaseOrAdjourn(); return; }

			if (!PartyOk(p.petitioner) || (p.other != null && !PartyOk(p.other)))
			{
				SkipCase(p);
				return;
			}

			if (_phaseTicks % PleadNudgeInterval == 0) EnsurePleading(p);

			if (p.state == Petition.StatePleading)
			{
				if (_phaseTicks < MaxArrivalTicks && CaptiveEnRoute(p)) _callGrace = _phaseTicks;
				else if (_phaseTicks - _callGrace > CallTimeoutTicks + p.def.Worker.ExtraCallTicks + PleadTicks)
					SendVerdictLetter(p);
			}
			else if (p.state == Petition.StateAwaitingVerdict)
			{
				if (Find.TickManager.TicksGame >= p.deadlineTick)
				{
					var idx = p.def.Worker.TimeoutIndex(p);
					ChooseVerdict(p.id, idx >= 0 && idx < p.def.verdicts.Count ? idx : DismissIndex(p.def), timedOut: true);
				}
			}
		}

		private void PunishmentTick()
		{
			if (_executionPending) { ExecutionTick(); return; }
			if (_flogMenuPending) { OpenFloggerMenu(); return; }

			if (_flogger != null && _condemned != null && !_floggingDone
				&& _phaseTicks % PleadNudgeInterval == 0
				&& _flogger.CurJobDef != RimCourtDefOf.RimCourt_Flog
				&& !_flogger.Dead && _flogger.Spawned && !_flogger.Downed && !_flogger.InMentalState)
			{
				var again = JobMaker.MakeJob(RimCourtDefOf.RimCourt_Flog, _condemned);
				again.count = Mathf.Max(1, FlogUtility.DefaultLashes - _lashesLanded);
				_flogger.jobs?.TryTakeOrderedJob(again, JobTag.Misc);
			}

			if (_lashesLanded == 0 && _phaseTicks == StalledPunishmentTicks && _condemned != null)
			{
				Log.Warning("[RimCourt] le bourreau n'a pas frappé, la sentence est appliquée directement.");
				for (var i = 0; i < FlogUtility.DefaultLashes; i++)
				{
					if (_condemned.Dead || _condemned.Downed) break;
					FlogUtility.OneLash(_flogger, _condemned);
				}
				_floggingDone = true;
			}

			var done = _floggingDone
				|| _condemned == null || _condemned.Dead || !_condemned.Spawned
				|| _phaseTicks >= PunishTimeoutTicks;
			if (!done) return;
			if (_condemned != null && !_condemned.Dead && _lashesLanded > 0
				&& RimCourtDefOf.RimCourt_Tale_Flogged != null)
				Step(() => TaleRecorder.RecordTale(RimCourtDefOf.RimCourt_Tale_Flogged, _lord, _condemned));
			_condemned = null;
			_duelA = null;
			_duelB = null;
			_flogger = null;
			_floggingDone = false;
			_flogMenuPending = false;
			_phase = PhaseReact;
			_phaseTicks = 0;
		}

		private void ReactTick()
		{
			if (_phaseTicks < ReactTicks) return;
			SendPartiesBack(_current);
			_phase = PhaseDepart;
			_phaseTicks = 0;
		}

		private void DepartTick()
		{
			if (_phaseTicks < DepartTicks) return;
			_current = null;
			StartNextCaseOrAdjourn();
		}

		private void WedgeKick()
		{
			_wedgeKicks++;
			if (_wedgeKicks >= 20)
			{
				Log.Error("[RimCourt] l'audience n'avance plus malgré les relances, annulation.");
				Abort("RimCourt.CourtCancelled".Translate());
				return;
			}
			Log.Warning("[RimCourt] audience sans état actif, relance de l'enchaînement (" + _wedgeKicks + "/20).");
			StartNextCaseOrAdjourn();
		}

		private void StartNextCaseOrAdjourn()
		{
			while (_queue.Count > 0)
			{
				var next = _queue[0];
				_queue.RemoveAt(0);
				if (!PartyOk(next.petitioner) || (next.other != null && !PartyOk(next.other))) continue;
				if (_walkedOut.Contains(next.petitioner)
					|| (next.other != null && _walkedOut.Contains(next.other))) continue;
				_current = next;
				next.state = Petition.StatePleading;
				_phase = PhaseHearing;
				_phaseTicks = 0;
				_captiveGuard = null;
				_captiveTries = 0;
				_callGrace = 0;
				EnsurePleading(next);
				Messages.Message("RimCourt.CaseCalled".Translate(next.petitioner.LabelShort),
					new LookTargets(next.petitioner), MessageTypeDefOf.NeutralEvent, historical: false);
				return;
			}
			Adjourn();
		}

		private void SkipCase(Petition p)
		{
			RemoveVerdictLetter(p);
			Messages.Message("RimCourt.CaseSkipped".Translate(p.petitioner?.LabelShort ?? "?"),
				MessageTypeDefOf.NeutralEvent, historical: false);
			SendPartiesBack(p);
			Step(() => FarewellOutsider(p.petitioner));
			Step(() => FarewellOutsider(p.other));
			_current = null;
			StartNextCaseOrAdjourn();
		}

		private void Adjourn()
		{
			var lord = _lord;
			var log = _verdictLog.ToList();
			var resolved = _resolvedCount;
			var repKey = _reputation.TitleKey;
			EndSession();
			_lastSessionTick = Find.TickManager.TicksGame;

			var sb = new System.Text.StringBuilder();
			sb.Append((resolved == 1 ? "RimCourt.AdjournLetterTextOne" : "RimCourt.AdjournLetterText")
				.Translate(lord?.LabelShort ?? "?", resolved));
			foreach (var line in log)
			{
				sb.AppendLine();
				sb.Append("  - " + line);
			}
			sb.AppendLine();
			sb.AppendLine();
			sb.Append("RimCourt.RepLine".Translate(repKey.Translate()));
			Find.LetterStack.ReceiveLetter("RimCourt.AdjournLetterLabel".Translate(),
				sb.ToString(), LetterDefOf.NeutralEvent, lord != null ? new LookTargets(lord) : default);
		}

		private void Abort(string reason)
		{
			var current = _current;
			Step(() => { if (current != null) RemoveVerdictLetter(current); });
			Step(() => SendPartiesBack(current));
			EndSession();
			_lastSessionTick = -1;
			Messages.Message(reason, MessageTypeDefOf.NegativeEvent, historical: false);
		}

		private void EndSession()
		{
			Step(RewardSpectators);
			Step(RestoreWaitingOutsiders);
			Step(ReleaseLord);
			Step(ReleaseCrowd);
			Step(ClearLetters);
			_active = false;
			_phase = PhaseNone;
			_phaseTicks = 0;
			_current = null;
			_queue.Clear();
			_map = null;
			_lord = null;
			_spotCell = IntVec3.Invalid;
			_seat = null;
			_walkedOut.Clear();
			_condemned = null;
			_duelA = null;
			_duelB = null;
			_flogger = null;
			_floggingDone = false;
			_flogMenuPending = false;
			_lashesLanded = 0;
			_captiveGuard = null;
			_captiveTries = 0;
			_callGrace = 0;
			_executionPending = false;
			_executionTier = 0;
			_lastDecayTick = Find.TickManager.TicksGame;
		}

		private void RestoreWaitingOutsiders()
		{
			if (_map == null) return;
			for (var i = 0; i < _waiting.Count; i++)
			{
				var req = _waiting[i];
				if (req.Valid && req.pawn.Map == _map) OutsiderUtility.ResumeWaiting(req, _map);
			}
		}

		private void ReleaseLord()
		{
			if (_lord?.jobs != null && _lord.CurJobDef == RimCourtDefOf.RimCourt_HoldCourt)
				_lord.jobs.EndCurrentJob(JobCondition.InterruptForced);
		}

		private void ReleaseCrowd()
		{
			if (_map == null) return;
			foreach (var p in _map.mapPawns.FreeColonistsSpawned.ToList())
				if (p.CurJobDef == RimCourtDefOf.RimCourt_AttendCourt || p.CurJobDef == RimCourtDefOf.RimCourt_PleadCase
					|| p.CurJobDef == RimCourtDefOf.RimCourt_BringCaptive)
					p.jobs?.EndCurrentJob(JobCondition.InterruptForced);
		}

		private void RewardSpectators()
		{
			if (_map == null || _resolvedCount == 0) return;
			foreach (var p in _map.mapPawns.FreeColonistsSpawned)
				if (p.CurJobDef == RimCourtDefOf.RimCourt_AttendCourt)
					p.needs?.mood?.thoughts?.memories?.TryGainMemory(RimCourtDefOf.RimCourt_SawCourtHeld);
		}

		private void ClearLetters()
		{
			var letters = Find.LetterStack.LettersListForReading
				.OfType<Letters.ChoiceLetter_Verdict>().ToList();
			foreach (var l in letters) Find.LetterStack.RemoveLetter(l);
		}

		private void RemoveVerdictLetter(Petition p)
		{
			var letter = Find.LetterStack.LettersListForReading
				.OfType<Letters.ChoiceLetter_Verdict>().FirstOrDefault(l => l.petitionId == p.id);
			if (letter != null) Find.LetterStack.RemoveLetter(letter);
		}

		private void SendVerdictLetter(Petition p)
		{
			p.state = Petition.StateAwaitingVerdict;
			p.deadlineTick = Find.TickManager.TicksGame + VerdictTicks;
			var letter = (Letters.ChoiceLetter_Verdict)LetterMaker.MakeLetter(
				"RimCourt.VerdictLetterLabel".Translate(p.petitioner.LabelShort),
				p.def.Worker.CaseText(p),
				RimCourtDefOf.RimCourt_Verdict,
				new LookTargets(p.petitioner));
			letter.petitionId = p.id;
			letter.StartTimeout(VerdictTicks);
			Find.LetterStack.ReceiveLetter(letter);
			if (RimCourtMod.Settings?.autoOpenSheet ?? true)
				UI.Dialog_PetitionSheet.OpenFor(p.id);
		}

		private static readonly string[] RefusalOutcomes =
			{ "SendAway", "RefuseArms", "RefuseProvisions", "KeepHim", "DenyMarriage", "DenyExemption", "DenyHonour" };

		private static int DismissIndex(PetitionDef def)
		{
			for (var i = 0; i < def.verdicts.Count; i++)
				if (def.verdicts[i].outcome == "Dismiss") return i;
			for (var i = 0; i < def.verdicts.Count; i++)
				if (def.verdicts[i].outcome == "Defer") return i;
			var best = -1;
			var bestWeak = 0f;
			for (var i = 0; i < def.verdicts.Count; i++)
				if (def.verdicts[i].weak > bestWeak) { bestWeak = def.verdicts[i].weak; best = i; }
			if (best >= 0) return best;
			for (var i = 0; i < def.verdicts.Count; i++)
				if (System.Array.IndexOf(RefusalOutcomes, def.verdicts[i].outcome) >= 0) return i;
			return def.verdicts.Count - 1;
		}

		private void ThrowReactions(Petition p, VerdictOption v)
		{
			p.def.Worker.Reactions(p, v, out var pm, out var om);
			ThrowReaction(p.petitioner, pm);
			ThrowReaction(p.other, om);
		}

		private static void ThrowReaction(Pawn pawn, int mood)
		{
			if (pawn == null || !pawn.Spawned || mood == 0 || pawn.Map == null) return;
			var key = mood > 0 ? "RimCourt.ReactHappy" : "RimCourt.ReactAngry";
			MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, (key + Rand.RangeInclusive(1, 2)).Translate(), 4f);
		}

		private void EnsurePleading(Petition p)
		{
			if (p.def.Worker is PetitionWorker_Captive) { EnsureCaptiveBrought(p); return; }
			EnsurePleadJob(p.petitioner, PleadCellFor(true));
			if (p.other != null) EnsurePleadJob(p.other, PleadCellFor(false));
		}

		private void EnsureCaptiveBrought(Petition p)
		{
			var c = p.petitioner;
			if (c == null || c.Dead || !c.Spawned) return;
			var cell = PleadCellFor(true);
			if (c.CurJobDef == RimCourtDefOf.RimCourt_PleadCase || c.Position.DistanceTo(cell) <= 2.5f)
			{
				EnsurePleadJob(c, cell);
				return;
			}
			if (_captiveGuard != null && _captiveGuard.CurJobDef == RimCourtDefOf.RimCourt_BringCaptive) return;
			if (_captiveTries >= MaxGuardTries) { EnsurePleadJob(c, cell); return; }
			_captiveTries++;
			var guard = CaptiveUtility.PickGuard(_map, c, _lord);
			if (guard == null || !CaptiveUtility.StartBring(guard, c, cell))
			{
				_captiveTries = MaxGuardTries;
				Messages.Message("RimCourt.CaptiveNoGuard".Translate(c.LabelShortCap),
					new LookTargets(c), MessageTypeDefOf.NeutralEvent, historical: false);
				EnsurePleadJob(c, cell);
				return;
			}
			_captiveGuard = guard;
			Messages.Message("RimCourt.CaptiveBrought".Translate(guard.LabelShortCap, c.LabelShortCap),
				new LookTargets(guard), MessageTypeDefOf.NeutralEvent, historical: false);
		}

		private void EnsurePleadJob(Pawn pawn, IntVec3 cell)
		{
			if (pawn == null || !pawn.Spawned || pawn.Drafted || pawn.jobs == null) return;
			if (pawn.CurJobDef == RimCourtDefOf.RimCourt_PleadCase) return;
			if (pawn.Faction != Faction.OfPlayer) OutsiderUtility.ReleaseFromLord(pawn);
			try
			{
				pawn.jobs.StartJob(JobMaker.MakeJob(RimCourtDefOf.RimCourt_PleadCase, cell, _lord),
					JobCondition.InterruptForced);
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] " + pawn.LabelShort + " refuse de s'avancer (" + e.Message + ").");
			}
		}

		private IntVec3 PleadCellFor(bool petitioner)
		{
			var baseCell = _spotCell;
			if (petitioner) return baseCell;
			var toSeat = _seat.Position - baseCell;
			var side = System.Math.Abs(toSeat.x) >= System.Math.Abs(toSeat.z)
				? new IntVec3(0, 0, 1)
				: new IntVec3(1, 0, 0);
			var cand = baseCell + side * 2;
			if (cand.InBounds(_map) && cand.Standable(_map)) return cand;
			cand = baseCell - side * 2;
			if (cand.InBounds(_map) && cand.Standable(_map)) return cand;
			return CellFinder.RandomClosewalkCellNear(baseCell, _map, 2);
		}

		private void FarewellOutsider(Pawn pawn)
		{
			if (pawn == null || pawn.Dead || !pawn.Spawned) return;
			if (pawn.Faction == null || pawn.Faction.IsPlayer || pawn.IsPrisoner) return;
			if (pawn.HostileTo(Faction.OfPlayer)) { CloseRequest(pawn); return; }
			CloseRequest(pawn);
			OutsiderUtility.SendHome(pawn);
		}

		private void SendPartiesBack(Petition p)
		{
			if (p == null) return;
			if (p.petitioner != null && p.petitioner.IsPrisonerOfColony)
			{
				CaptiveUtility.Ground(p.petitioner);
				CaptiveUtility.SendBackToCell(p.petitioner);
				return;
			}
			SendBack(p.petitioner);
			SendBack(p.other);
		}

		private void SendBack(Pawn pawn)
		{
			if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed || pawn.Drafted || pawn.jobs == null) return;
			if (pawn.Faction != Faction.OfPlayer) return;
			if (pawn.CurJobDef == RimCourtDefOf.RimCourt_StandInStocks
				|| pawn.CurJobDef == RimCourtDefOf.RimCourt_EscortToStocks
				|| pawn.CurJobDef == RimCourtDefOf.RimCourt_BringCaptive
				|| pawn.CurJobDef == RimCourtDefOf.RimCourt_ExecuteCaptive) return;
			if (_map == null || !_spotCell.IsValid || _seat == null) return;
			var cell = CellFinder.RandomClosewalkCellNear(CrowdAnchor(), _map, 3);
			try
			{
				pawn.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto, cell), JobCondition.InterruptForced);
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] " + pawn.LabelShort + " refuse de retourner dans la foule (" + e.Message + ").");
			}
		}

		private IntVec3 CrowdAnchor()
		{
			var away = _spotCell - _seat.Position;
			var dx = System.Math.Sign(away.x);
			var dz = System.Math.Sign(away.z);
			return _spotCell + new IntVec3(dx * 4, 0, dz * 4);
		}

		private void KeepLordSeated()
		{
			if (_lord.Drafted || _lord.jobs == null) return;
			if (_lord.CurJobDef == RimCourtDefOf.RimCourt_HoldCourt) return;
			SendLordToSeat();
		}

		private void SendLordToSeat()
		{
			if (_lord?.jobs == null) return;
			if (_lord.Drafted && _lord.drafter != null) _lord.drafter.Drafted = false;
			var chair = FindSeatChair(_seat);
			var job = chair != null
				? JobMaker.MakeJob(RimCourtDefOf.RimCourt_HoldCourt, chair, _spotCell)
				: JobMaker.MakeJob(RimCourtDefOf.RimCourt_HoldCourt, _seat.Position, _spotCell);
			try
			{
				_lord.jobs.StartJob(job, JobCondition.InterruptForced);
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] le seigneur refuse de siéger (" + e.Message + ").");
			}
		}

		private static Thing FindSeatChair(Thing seat) => ChairAt(seat.Position, seat.Map);

		public static Thing ChairAt(IntVec3 pos, Map map)
		{
			if (map == null || !pos.InBounds(map)) return null;
			foreach (var t in pos.GetThingList(map))
				if (t.def?.building != null && t.def.building.isSittable) return t;
			foreach (var off in GenAdj.AdjacentCells)
			{
				var c = pos + off;
				if (!c.InBounds(map)) continue;
				foreach (var t in c.GetThingList(map))
					if (t.def?.building != null && t.def.building.isSittable) return t;
			}
			return null;
		}

		public static Thing FindAudienceSpotNear(IntVec3 seatPos, Map map)
		{
			Thing best = null;
			var bestD = SeatSearchRadius * SeatSearchRadius;
			var spots = map.listerThings.ThingsOfDef(RimCourtDefOf.RimCourt_AudienceSpot);
			for (var i = 0; i < spots.Count; i++)
			{
				if (spots[i].Position == seatPos) continue;
				float d = spots[i].Position.DistanceToSquared(seatPos);
				if (d < bestD) { bestD = d; best = spots[i]; }
			}
			return best;
		}

		public static IntVec3 AudienceCellFor(IntVec3 seatPos, Rot4 fallbackRot, Map map)
		{
			if (map == null) return IntVec3.Invalid;
			var spot = FindAudienceSpotNear(seatPos, map);
			if (spot != null) return spot.Position;
			var chair = ChairAt(seatPos, map);
			var dir = (chair != null ? chair.Rotation : fallbackRot).FacingCell;
			for (var d = AudienceDistance; d >= MinAudienceDistance; d--)
			{
				var cell = seatPos + dir * d;
				if (cell.InBounds(map) && cell.Standable(map) && cell.GetEdifice(map) == null)
					return cell;
			}
			return IntVec3.Invalid;
		}

		private static IntVec3 FindAudienceCell(Thing seat, Map map)
			=> seat == null ? IntVec3.Invalid : AudienceCellFor(seat.Position, seat.Rotation, map);

		public static Thing AnySeatOn(Map map)
		{
			if (map == null) return null;
			var seats = map.listerThings.ThingsOfDef(RimCourtDefOf.RimCourt_LordSeat);
			if (seats.Count > 0) return seats[0];
			var joustSeat = DefDatabase<ThingDef>.GetNamedSilentFail("RimJoust_LordSeat");
			if (joustSeat == null) return null;
			var others = map.listerThings.ThingsOfDef(joustSeat);
			return others.Count > 0 ? others[0] : null;
		}

		public static Thing SeatOf(Thing marker)
		{
			if (marker == null || marker.Map == null) return null;
			return marker.def == RimCourtDefOf.RimCourt_AudienceSpot ? FindSeatNear(marker) : marker;
		}

		private static Thing FindSeatNear(Thing spot)
		{
			var map = spot.Map;
			Thing best = null;
			var bestD = SeatSearchRadius * SeatSearchRadius;
			var seats = _seatScratch;
			seats.Clear();
			seats.AddRange(map.listerThings.ThingsOfDef(RimCourtDefOf.RimCourt_LordSeat));
			var joustSeat = DefDatabase<ThingDef>.GetNamedSilentFail("RimJoust_LordSeat");
			if (joustSeat != null) seats.AddRange(map.listerThings.ThingsOfDef(joustSeat));
			for (var i = 0; i < seats.Count; i++)
			{
				if (seats[i].Position == spot.Position) continue;
				float d = seats[i].Position.DistanceToSquared(spot.Position);
				if (d < bestD) { bestD = d; best = seats[i]; }
			}
			return best;
		}

		private Pawn PickLord(Map map, Thing seat = null)
		{
			if (CanHoldCourt(_appointedLord, map)) return _appointedLord;
			return PickAutomatic(map, seat);
		}

		private Pawn PickAutomatic(Map map, Thing seat = null)
			=> SeatOwnerOf(seat ?? SeatThing(map)) ?? PickByPrestige(map);

		private Thing SeatThing(Map map)
			=> _active && _seat != null && _seat.Map == map ? _seat : AnySeatOn(map);

		public static Pawn SeatOwnerOf(Thing seat)
		{
			if (seat == null || seat.Map == null) return null;
			var chair = ChairAt(seat.Position, seat.Map);
			var comp = chair?.TryGetComp<CompAssignableToPawn>();
			if (comp == null || comp.AssignedPawnsForReading.Count == 0) return null;
			var owner = comp.AssignedPawnsForReading[0];
			return CanHoldCourt(owner, seat.Map) ? owner : null;
		}

		private static bool CanHoldCourt(Pawn p, Map map)
			=> p != null && !p.Dead && p.Spawned && p.Map == map && p.IsFreeColonist
				&& !p.Downed && !p.InMentalState && p.health != null
				&& p.health.capacities.CapableOf(PawnCapacityDefOf.Moving);

		private static Pawn PickByPrestige(Map map)
		{
			if (map == null) return null;
			Pawn best = null;
			var bestScore = float.MinValue;
			foreach (var c in map.mapPawns.FreeColonistsSpawned)
			{
				if (c.Dead || c.Downed || c.InMentalState) continue;
				if (c.health == null || !c.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) continue;
				var score = Prestige(c);
				if (score > bestScore) { bestScore = score; best = c; }
			}
			return best;
		}

		private static float Prestige(Pawn c)
		{
			var score = 0f;
			var title = c.royalty?.MostSeniorTitle;
			if (title != null) score += 10000f + title.def.seniority;
			score += c.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
			if (c.ageTracker != null) score += c.ageTracker.AgeBiologicalYears * 0.1f;
			return score;
		}

		private List<Petition> GeneratePetitions(Map map, Pawn lord)
		{
			var result = new List<Petition>();
			for (var i = 0; i < _waiting.Count; i++)
			{
				var req = _waiting[i];
				if (!req.Valid || req.pawn.MapHeld != map) continue;
				var p = (req.def.Worker as PetitionWorker_Outsider)?.FromRequest(req);
				if (p == null) continue;
				p.id = _nextPetitionId++;
				result.Add(p);
			}

			var range = RimCourtMod.Settings?.casesRange
				?? new IntRange(RimCourtSettings.DefCasesMin, RimCourtSettings.DefCasesMax);
			var max = Mathf.Clamp(range.RandomInRange + _reputation.CaseBonus, 1, 6);
			var shift = _reputation.ThresholdShift;
			var defs = DefDatabase<PetitionDef>.AllDefsListForReading
				.OrderByDescending(d => d.priority).ToList();
			var progress = true;
			while (result.Count < max && progress)
			{
				progress = false;
				foreach (var def in defs)
				{
					if (result.Count >= max) break;
					Petition p = null;
					try { p = def.Worker.TryGenerate(map, lord, result, shift); }
					catch (System.Exception e) { Log.Error("[RimCourt] génération de pétition " + def.defName + " : " + e); }
					if (p == null) continue;
					p.id = _nextPetitionId++;
					result.Add(p);
					progress = true;
				}
			}
			return result;
		}

		private void GatherCourtCrowd()
		{
			if (_map == null || !_spotCell.IsValid || _seat == null) return;
			var spotPos = _spotCell;
			var seatPos = _seat.Position;
			var spotToSeat = spotPos.DistanceTo(seatPos);

			var standing = new List<IntVec3>();
			var num = GenRadial.NumCellsInRadius(CrowdMaxRadius);
			for (var i = 0; i < num; i++)
			{
				var cell = spotPos + GenRadial.RadialPattern[i];
				if (!cell.InBounds(_map) || !cell.Standable(_map)) continue;
				if (cell.DistanceTo(spotPos) < CrowdMinRadius) continue;
				if (cell.DistanceTo(seatPos) < spotToSeat) continue;
				standing.Add(cell);
			}

			var targets = new List<LocalTargetInfo>();
			var chairCells = new List<IntVec3>();
			var allBld = _map.listerBuildings.allBuildingsColonist;
			for (var i = 0; i < allBld.Count; i++)
			{
				var bld = allBld[i];
				if (bld?.def?.building == null || !bld.def.building.isSittable) continue;
				if (!UI.CompCourtArea.InCrowdArea(_seat, spotPos, bld.Position)) continue;
				if (bld.Position.DistanceTo(seatPos) < 3f) continue;
				foreach (var cell in bld.OccupiedRect())
					if (cell.InBounds(_map) && cell.Standable(_map)
						&& cell.DistanceTo(seatPos) >= spotToSeat - 1f)
						chairCells.Add(cell);
			}
			chairCells.Sort((x, y) => x.DistanceToSquared(spotPos).CompareTo(y.DistanceToSquared(spotPos)));
			for (var i = 0; i < chairCells.Count; i++) targets.Add(chairCells[i]);
			standing.Sort((x, y) => x.DistanceToSquared(spotPos).CompareTo(y.DistanceToSquared(spotPos)));
			for (var i = 0; i < standing.Count; i++) targets.Add(standing[i]);
			if (targets.Count == 0) return;

			var ti = 0;
			foreach (var c in _map.mapPawns.FreeColonistsSpawned.ToList())
			{
				if (!EligibleSpectator(c)) continue;
				if (c.CurJobDef == RimCourtDefOf.RimCourt_AttendCourt) continue;

				var target = LocalTargetInfo.Invalid;
				while (ti < targets.Count)
				{
					var cand = targets[ti++];
					if (c.CanReserveAndReach(cand, PathEndMode.OnCell, Danger.Deadly)) { target = cand; break; }
				}
				if (!target.IsValid) break;

				var job = JobMaker.MakeJob(RimCourtDefOf.RimCourt_AttendCourt, target, spotPos);
				c.jobs.StartJob(job, JobCondition.InterruptForced);
			}
		}

		private bool EligibleSpectator(Pawn c)
		{
			if (c == null || !c.Spawned || c.Dead || c.Downed) return false;
			if (c.CurJobDef == RimCourtDefOf.RimCourt_StandInStocks || c.CurJobDef == RimCourtDefOf.RimCourt_EscortToStocks
				|| c.CurJobDef == RimCourtDefOf.RimCourt_BringCaptive) return false;
			if (c.CurJobDef == RimCourtDefOf.RimCourt_Flog || c.CurJobDef == RimCourtDefOf.RimCourt_TakeLashes
				|| c.CurJobDef == RimCourtDefOf.RimCourt_ExecuteCaptive) return false;
			if (c == _lord || c == _flogger || c == _condemned) return false;
			if (_walkedOut.Contains(c)) return false;
			if (_current != null && _current.Involves(c)) return false;
			if (c.Drafted || c.InMentalState) return false;
			if (c.CurJobDef == RimCourtDefOf.RimCourt_PleadCase || c.CurJobDef == RimCourtDefOf.RimCourt_HoldCourt) return false;
			if (c.health == null || !c.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) return false;
			if (c.CurJobDef == JobDefOf.LayDown || c.CurJobDef == JobDefOf.TendPatient) return false;
			var food = c.needs?.food;
			var rest = c.needs?.rest;
			if (food != null && food.CurLevelPercentage < 0.12f) return false;
			if (rest != null && rest.CurLevelPercentage < 0.12f) return false;
			return true;
		}

		private bool PartyOk(Pawn p)
		{
			if (p == null || p.Dead || p.Downed || p.InMentalState) return false;
			if (p.IsPrisoner)
				return p.IsPrisonerOfColony && p.MapHeld == _map && (p.Spawned || p.CarriedBy != null)
					&& p.guest != null && !p.guest.Released && !PrisonBreakUtility.IsPrisonBreaking(p);
			return p.Spawned && p.Map == _map && !p.HostileTo(Faction.OfPlayer);
		}

		private static void Step(System.Action step)
		{
			try { step(); }
			catch (System.Exception e) { Log.Error("[RimCourt] nettoyage de fin d'audience : " + e); }
		}

		private static void Reject(string msg)
			=> Messages.Message(msg, MessageTypeDefOf.RejectInput, historical: false);

		public override void ExposeData()
		{
			Scribe_Deep.Look(ref _reputation, "reputation");
			Scribe_Collections.Look(ref _waiting, "waiting", LookMode.Deep);
			Scribe_Collections.Look(ref _queue, "queue", LookMode.Deep);
			Scribe_Collections.Look(ref _heard, "heard", LookMode.Deep);
			Scribe_Collections.Look(ref _pledges, "pledges", LookMode.Deep);
			Scribe_Collections.Look(ref _exemptions, "exemptions", LookMode.Deep);
			Scribe_Collections.Look(ref _exiles, "exiles", LookMode.Deep);
			Scribe_Collections.Look(ref _raids, "raids", LookMode.Deep);
			Scribe_Collections.Look(ref _triedCaptives, "triedCaptives", LookMode.Value);
			Scribe_References.Look(ref _captiveGuard, "captiveGuard");
			Scribe_Values.Look(ref _captiveTries, "captiveTries");
			Scribe_Values.Look(ref _callGrace, "callGrace");
			Scribe_Values.Look(ref _executionPending, "executionPending");
			Scribe_Values.Look(ref _executionTier, "executionTier");
			Scribe_Collections.Look(ref _walkedOut, "walkedOut", LookMode.Reference);
			Scribe_Deep.Look(ref _current, "current");
			Scribe_Values.Look(ref _nextPetitionId, "nextPetitionId", 1);
			Scribe_Values.Look(ref _lastSessionTick, "lastSessionTick", -1);
			Scribe_Values.Look(ref _active, "active");
			Scribe_References.Look(ref _map, "map");
			Scribe_References.Look(ref _lord, "lord");
			Scribe_References.Look(ref _lastLord, "lastLord");
			Scribe_References.Look(ref _appointedLord, "appointedLord");
			Scribe_References.Look(ref _hand, "hand");
			Scribe_References.Look(ref _shapedLord, "shapedLord");
			Scribe_Values.Look(ref _shapedVerdicts, "shapedVerdicts");
			Scribe_Values.Look(ref _spotCell, "spotCell", IntVec3.Invalid);
			Scribe_References.Look(ref _seat, "seat");
			Scribe_Values.Look(ref _phase, "phase");
			Scribe_Values.Look(ref _phaseTicks, "phaseTicks");
			Scribe_Values.Look(ref _sessionTotal, "sessionTotal");
			Scribe_Values.Look(ref _resolvedCount, "resolvedCount");
			Scribe_References.Look(ref _condemned, "condemned");
			Scribe_References.Look(ref _duelA, "duelA");
			Scribe_References.Look(ref _duelB, "duelB");
			Scribe_References.Look(ref _flogger, "flogger");
			Scribe_Values.Look(ref _floggingDone, "floggingDone");
			Scribe_Values.Look(ref _flogMenuPending, "flogMenuPending");
			Scribe_Values.Look(ref _lashesLanded, "lashesLanded");
			Scribe_Values.Look(ref _lastDecayTick, "lastDecayTick", -1);
			Scribe_Values.Look(ref _lastDeserterTick, "lastDeserterTick", -999999);
			Scribe_References.Look(ref _epithetPawn, "epithetPawn");
			Scribe_Values.Look(ref _epithetText, "epithetText");
			Scribe_Collections.Look(ref _verdictLog, "verdictLog", LookMode.Value);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (_reputation == null) _reputation = new JudgeReputation();
				if (_waiting == null) _waiting = new List<OutsiderRequest>();
				_waiting.RemoveAll(r => r == null || r.pawn == null || r.def == null);
				if (_queue == null) _queue = new List<Petition>();
				if (_heard == null) _heard = new List<HeardCase>();
				if (_pledges == null) _pledges = new List<Betrothal>();
				_pledges.RemoveAll(p => p == null || p.a == null || p.b == null);
				if (_walkedOut == null) _walkedOut = new List<Pawn>();
				_walkedOut.RemoveAll(x => x == null);
				if (_exiles == null) _exiles = new List<Exile>();
				_exiles.RemoveAll(x => x == null || x.pawn == null);
				if (_raids == null) _raids = new List<RaidMark>();
				_raids.RemoveAll(r => r == null || r.faction == null);
				if (_triedCaptives == null) _triedCaptives = new List<int>();
				if (_exemptions == null) _exemptions = new List<WorkExemption>();
				_exemptions.RemoveAll(e => e == null || e.pawn == null || e.workDefName.NullOrEmpty());
				_heard.RemoveAll(h => h == null || h.defName.NullOrEmpty());
				if (_verdictLog == null) _verdictLog = new List<string>();
				_queue.RemoveAll(p => p == null || p.petitioner == null || p.def == null);
				if (_current != null && (_current.petitioner == null || _current.def == null)) _current = null;
				if (_appointedLord != null && _appointedLord.Dead) _appointedLord = null;
				if (_hand != null && _hand.Dead) _hand = null;
				if (_active && (_map == null || _lord == null || !_spotCell.IsValid || _seat == null))
				{
					_active = false;
					_phase = PhaseNone;
					_current = null;
				}
			}
		}
	}
}
