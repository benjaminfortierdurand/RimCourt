using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimCourt.Court
{
	public class RaidMark : IExposable
	{
		public Faction faction;
		public int tick;
		public int lordId;

		public void ExposeData()
		{
			Scribe_References.Look(ref faction, "faction");
			Scribe_Values.Look(ref tick, "tick");
			Scribe_Values.Look(ref lordId, "lordId");
		}
	}

	public struct Dossier
	{
		public int tier;
		public string victim;
		public bool killed;
		public int raids;
	}

	public static class CaptiveUtility
	{
		public const int TierNobody = 0;
		public const int TierHurt = 1;
		public const int TierBlood = 2;
		public const int RaidsForHabit = 3;
		public const int RaidMemoryDays = 60;
		public const int MinAge = 13;

		private const float RansomShare = 0.4f;
		private const int RansomMin = 200;
		private const int RansomMax = 800;

		private static readonly FieldInfo SubjectField = AccessTools.Field(typeof(BattleLogEntry_StateTransition), "subjectPawn");
		private static readonly FieldInfo InitiatorField = AccessTools.Field(typeof(BattleLogEntry_StateTransition), "initiator");
		private static readonly FieldInfo TransitionField = AccessTools.Field(typeof(BattleLogEntry_StateTransition), "transitionDef");

		public static bool CanBeTried(Pawn p, Map map, Pawn lord, List<Petition> taken)
		{
			if (p == null || p == lord || p.Dead || !p.Spawned || p.Map != map) return false;
			if (!p.IsPrisonerOfColony || p.guest == null || p.guest.Released || p.IsSlave) return false;
			if (p.Downed || p.InMentalState || !p.RaceProps.Humanlike) return false;
			if (p.Faction == null || p.Faction.IsPlayer || !p.Faction.HostileTo(Faction.OfPlayer)) return false;
			if (p.ageTracker != null && p.ageTracker.AgeBiologicalYears < MinAge) return false;
			if (p.health == null || !p.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) return false;
			if (p.guest.ExclusiveInteractionMode == PrisonerInteractionModeDefOf.Execution) return false;
			if (p.guest.IsInteractionEnabled(PrisonerInteractionModeDefOf.Release)) return false;
			if (PrisonBreakUtility.IsPrisonBreaking(p)) return false;
			var mgr = CourtManager.Instance;
			if (mgr != null && mgr.CaptiveTried(p)) return false;
			for (var i = 0; i < taken.Count; i++)
				if (taken[i].Involves(p)) return false;
			return true;
		}

		public static Dossier Investigate(Pawn captive)
		{
			var d = new Dossier();
			if (captive == null) return d;
			var log = Find.BattleLog;
			if (log != null && SubjectField != null && InitiatorField != null && TransitionField != null)
			{
				var battles = log.Battles;
				for (var i = 0; i < battles.Count; i++)
				{
					var entries = battles[i].Entries;
					for (var j = 0; j < entries.Count; j++)
					{
						if (!(entries[j] is BattleLogEntry_StateTransition st)) continue;
						if (!(InitiatorField.GetValue(st) is Pawn who) || who != captive) continue;
						if (!(SubjectField.GetValue(st) is Pawn subject) || subject == captive) continue;
						if (subject.Faction != Faction.OfPlayer || !subject.RaceProps.Humanlike) continue;
						var tr = TransitionField.GetValue(st) as RulePackDef;
						if (tr == RulePackDefOf.Transition_Died || tr == RulePackDefOf.Transition_DiedExplosive)
						{
							d.victim = subject.LabelShortCap;
							d.killed = true;
						}
						else if (tr == RulePackDefOf.Transition_Downed && !d.killed && d.victim.NullOrEmpty())
						{
							d.victim = subject.LabelShortCap;
						}
					}
				}
			}
			d.raids = CourtManager.Instance?.RaidsBy(captive.Faction) ?? 0;
			d.tier = d.killed ? TierBlood
				: (!d.victim.NullOrEmpty() || d.raids >= RaidsForHabit) ? TierHurt
				: TierNobody;
			return d;
		}

		public static string Encode(Dossier d)
			=> d.tier + "|" + (d.killed ? 1 : 0) + "|" + d.raids + "|" + (d.victim ?? "");

		public static Dossier Decode(string s)
		{
			var d = new Dossier();
			if (s.NullOrEmpty()) return d;
			var parts = s.Split('|');
			if (parts.Length < 4) return d;
			int.TryParse(parts[0], out d.tier);
			d.killed = parts[1] == "1";
			int.TryParse(parts[2], out d.raids);
			d.victim = parts[3];
			return d;
		}

		public static string DossierText(Pawn captive, Dossier d)
		{
			var name = captive?.LabelShortCap ?? "?";
			var sb = new System.Text.StringBuilder();
			if (d.killed)
				sb.Append("RimCourt.CaptiveBlood".Translate(name, d.victim));
			else if (!d.victim.NullOrEmpty())
				sb.Append("RimCourt.CaptiveHurt".Translate(name, d.victim));
			if (d.raids >= RaidsForHabit)
			{
				if (sb.Length > 0) sb.Append(' ');
				sb.Append("RimCourt.CaptiveRaids".Translate(name, captive?.Faction?.Name ?? "?", d.raids));
			}
			if (sb.Length == 0)
				sb.Append("RimCourt.CaptiveNobody".Translate(name));
			return sb.ToString();
		}

		public static int RansomFor(Pawn p)
		{
			var value = p?.MarketValue ?? 0f;
			var silver = Mathf.Clamp(value * RansomShare, RansomMin, RansomMax);
			return Mathf.RoundToInt(silver / 10f) * 10;
		}

		public static bool IsRaidJob(LordJob job)
		{
			if (job == null) return false;
			if (job is LordJob_AssaultColony || job is LordJob_Siege || job is LordJob_StageThenAttack
				|| job is LordJob_SleepThenAssaultColony) return true;
			var n = job.GetType().Name;
			return n.Contains("Assault") || n.Contains("Siege") || n.Contains("Raid");
		}

		public static Pawn PickGuard(Map map, Pawn captive, Pawn lord)
		{
			var list = FlogUtility.FloggerCandidates(map, captive);
			Pawn best = null;
			var bestScore = float.MinValue;
			for (var i = 0; i < list.Count; i++)
			{
				var c = list[i];
				if (c == lord || c.Drafted || c.CurJobDef == RimCourtDefOf.RimCourt_PleadCase) continue;
				if (c.CurJobDef == RimCourtDefOf.RimCourt_EscortToStocks || c.CurJobDef == RimCourtDefOf.RimCourt_Flog) continue;
				float score = c.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
				if (c.CurJobDef == RimCourtDefOf.RimCourt_AttendCourt) score += 4f;
				if (c.workSettings != null && c.workSettings.WorkIsActive(WorkTypeDefOf.Warden)) score += 3f;
				if (score > bestScore) { bestScore = score; best = c; }
			}
			return best;
		}

		public static bool StartBring(Pawn guard, Pawn captive, IntVec3 cell)
		{
			if (guard?.jobs == null || captive == null || !cell.IsValid) return false;
			try
			{
				var job = JobMaker.MakeJob(RimCourtDefOf.RimCourt_BringCaptive, captive, cell);
				job.count = 1;
				job.expiryInterval = -1;
				return guard.jobs.TryTakeOrderedJob(job, JobTag.Misc);
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] " + guard.LabelShort + " ne va pas chercher le prisonnier (" + e.Message + ").");
				return false;
			}
		}

		public static void Ground(Pawn p)
		{
			if (p == null || p.Spawned) return;
			var carrier = p.CarriedBy;
			if (carrier == null) return;
			if (carrier.CurJobDef == RimCourtDefOf.RimCourt_BringCaptive)
				carrier.jobs?.EndCurrentJob(JobCondition.InterruptForced);
			if (!p.Spawned && p.CarriedBy == carrier)
				carrier.carryTracker?.TryDropCarriedThing(carrier.Position, ThingPlaceMode.Near, out _);
		}

		public static void SendBackToCell(Pawn p)
		{
			if (p == null || p.Dead || !p.Spawned || !p.IsPrisonerOfColony || p.guest == null || p.guest.Released) return;
			if (p.Downed || p.InMentalState || p.jobs == null) return;
			var cur = p.CurJobDef;
			if (cur == RimCourtDefOf.RimCourt_StandInStocks || cur == JobDefOf.LayDown) return;
			if (cur == JobDefOf.Goto && p.CurJob != null && !p.CurJob.exitMapOnArrival) return;
			var bed = p.ownership?.OwnedBed;
			if (bed == null || bed.Map != p.Map || !bed.ForPrisoners)
				bed = RestUtility.FindBedFor(p, p, false, false, GuestStatus.Prisoner);
			if (bed != null && p.CanReach(bed, PathEndMode.OnCell, Danger.Deadly))
			{
				try
				{
					p.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto, bed.Position), JobCondition.InterruptForced);
					return;
				}
				catch (System.Exception e)
				{
					Log.Warning("[RimCourt] " + p.LabelShort + " ne retourne pas dans sa cellule (" + e.Message + ").");
				}
			}
			p.guest.WaitInsteadOfEscapingForDefaultTicks();
		}

		public static bool Release(Pawn p)
		{
			Ground(p);
			if (p == null || p.Dead || !p.Spawned || !p.IsPrisonerOfColony) return false;
			var map = p.Map;
			GenGuest.PrisonerRelease(p);
			if (!PawnBanishUtility.WouldBeLeftToDie(p, map.Tile))
				GenGuest.AddHealthyPrisonerReleasedThoughts(p);
			QuestUtility.SendQuestTargetSignals(p.questTags, "Released", p.Named("SUBJECT"));
			return true;
		}

		public static bool Ransom(Pawn p, int silver, IntVec3 at)
		{
			var map = p?.MapHeld;
			if (map == null || !Release(p)) return false;
			if (silver <= 0) return true;
			var t = ThingMaker.MakeThing(ThingDefOf.Silver);
			t.stackCount = silver;
			GenPlace.TryPlaceThing(t, at.IsValid && at.InBounds(map) ? at : p.Position, map, ThingPlaceMode.Near);
			return true;
		}

		public static Pawn Execute(Pawn victim, Pawn preferred, Pawn lord, int tier)
		{
			Ground(victim);
			if (victim == null || victim.Dead || !victim.Spawned) return null;
			var exec = Willing(preferred) ? preferred : null;
			if (exec == null)
			{
				var list = FlogUtility.FloggerCandidates(victim.Map, victim);
				var bestD = float.MaxValue;
				for (var i = 0; i < list.Count; i++)
				{
					var c = list[i];
					if (c == lord || !Willing(c)) continue;
					var d = c.Position.DistanceToSquared(victim.Position);
					if (d < bestD) { bestD = d; exec = c; }
				}
			}
			if (exec == null) return null;
			if (tier > TierNobody) victim.guilt?.Notify_Guilty();
			ExecutionUtility.DoExecutionByCut(exec, victim);
			ThoughtUtility.GiveThoughtsForPawnExecuted(victim, exec, PawnExecutionKind.GenericBrutal);
			if (TaleDefOf.ExecutedPrisoner != null) TaleRecorder.RecordTale(TaleDefOf.ExecutedPrisoner, exec, victim);
			return exec;
		}

		public static List<Pawn> Executioners(Map map, Pawn victim, Pawn lord)
		{
			var list = FlogUtility.FloggerCandidates(map, victim);
			for (var i = list.Count - 1; i >= 0; i--)
				if (list[i] == lord || !Willing(list[i])) list.RemoveAt(i);
			return list;
		}

		public static void MarkForExecution(Pawn p)
		{
			if (p?.guest == null || p.Dead || !p.IsPrisonerOfColony) return;
			p.guest.SetExclusiveInteraction(PrisonerInteractionModeDefOf.Execution);
			SendBackToCell(p);
		}

		private static bool Willing(Pawn p)
		{
			if (p == null || p.Dead || !p.Spawned || p.Downed || p.InMentalState || !p.IsFreeColonist) return false;
			if (p.WorkTagIsDisabled(WorkTags.Violent)) return false;
			if (p.ageTracker != null && p.ageTracker.AgeBiologicalYears < FlogUtility.MinFloggerAge) return false;
			return !ModsConfig.IdeologyActive || IdeoUtility.DoerWillingToDo(HistoryEventDefOf.ExecutedPrisoner, p);
		}
	}
}
