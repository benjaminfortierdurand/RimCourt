using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimCourt.Court
{
	public abstract class PetitionWorker_Request : PetitionWorker
	{
		protected static bool CanAsk(Pawn p, Pawn lord, List<Petition> taken)
		{
			if (p == null || p == lord || p.Dead || p.Downed || !p.Spawned) return false;
			if (p.InMentalState || p.Drafted || !p.IsFreeColonist) return false;
			if (p.ageTracker != null && p.ageTracker.AgeBiologicalYears < 16) return false;
			if (p.health == null || !p.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) return false;
			for (var i = 0; i < taken.Count; i++)
				if (taken[i].Involves(p)) return false;
			return true;
		}

		protected Petition Pick(List<Petition> found)
		{
			var mgr = CourtManager.Instance;
			for (var i = found.Count - 1; i >= 0; i--)
				if (mgr != null && mgr.WasHeardRecently(found[i].petitioner, found[i].other, def))
					found.RemoveAt(i);
			return found.Count == 0 ? null : found.RandomElement();
		}

		public override void ApplyVerdict(Petition p, VerdictOption v, Pawn lord)
		{
			var pawn = p.petitioner;

			switch (v.outcome)
			{
				case "GrantMarriage":
					Mem(pawn, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					Mem(p.other, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					Mem(pawn, RimCourtDefOf.RimCourt_BlessedByLord, null);
					Mem(p.other, RimCourtDefOf.RimCourt_BlessedByLord, null);
					if (RimCourtDefOf.RimCourt_Tale_MatchBlessed != null && p.other != null)
						TaleRecorder.RecordTale(RimCourtDefOf.RimCourt_Tale_MatchBlessed, pawn, p.other);
					var pledged = CourtManager.Instance?.PledgeWedding(pawn, p.other) ?? false;
					Messages.Message((pledged ? "RimCourt.MatchPledged" : "RimCourt.MatchBlessed")
							.Translate(pawn.LabelShortCap, p.other?.LabelShortCap ?? "?"),
						new LookTargets(pawn), MessageTypeDefOf.PositiveEvent, historical: false);
					break;

				case "DenyMarriage":
					Mem(pawn, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
					Mem(p.other, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
					Mem(pawn, RimCourtDefOf.RimCourt_RefusedByLord, null);
					Mem(p.other, RimCourtDefOf.RimCourt_RefusedByLord, null);
					break;

				case "GrantExemption":
					if (ClearWork(pawn, p.extra))
					{
						CourtManager.Instance?.RememberExemption(pawn, p.extra);
						Messages.Message("RimCourt.WorkExcused".Translate(pawn.LabelShortCap, WorkLabel(p.extra)),
							new LookTargets(pawn), MessageTypeDefOf.NeutralEvent, historical: false);
					}
					Mem(pawn, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					break;

				case "Apprentice":
					Apprentice(pawn, p.extra, lord);
					break;

				case "LetGo":
					Mem(pawn, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					LetGo(pawn);
					break;

				case "LetGoProvisioned":
					var map = pawn?.MapHeld;
					if (map != null) OutsiderUtility.TryTakeFood(map, 20);
					Mem(pawn, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					LetGo(pawn);
					break;

				case "Honour":
					Mem(pawn, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					Mem(pawn, RimCourtDefOf.RimCourt_HonouredByLord, null);
					HonourBefore(pawn, lord);
					Messages.Message("RimCourt.Honoured".Translate(pawn.LabelShortCap),
						new LookTargets(pawn), MessageTypeDefOf.PositiveEvent, historical: false);
					break;

				case "PayHonour":
					var purse = pawn?.MapHeld;
					if (purse != null && OutsiderUtility.TryPaySilver(purse, p.silver))
					{
						Mem(pawn, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
						Mem(pawn, RimCourtDefOf.RimCourt_HonouredByLord, null);
						Mem(pawn, RimCourtDefOf.RimCourt_PaidInSilver, null);
						HonourBefore(pawn, lord);
						Messages.Message("RimCourt.HonourPaid".Translate(pawn.LabelShortCap, p.silver),
							new LookTargets(pawn), MessageTypeDefOf.PositiveEvent, historical: false);
					}
					else
					{
						Mem(pawn, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
						Mem(pawn, RimCourtDefOf.RimCourt_RefusedByLord, null);
						Messages.Message("RimCourt.HonourNoSilver".Translate(p.silver),
							new LookTargets(pawn), MessageTypeDefOf.NegativeEvent, historical: false);
					}
					break;

				default:
					Mem(pawn, v.outcome == "Defer"
						? RimCourtDefOf.RimCourt_LordDismissedMyCase
						: RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
					break;
			}
		}

		public override void Reactions(Petition p, VerdictOption v, out int petitionerMood, out int otherMood)
		{
			switch (v.outcome)
			{
				case "GrantMarriage":
					petitionerMood = 1;
					otherMood = 1;
					break;
				case "DenyMarriage":
					petitionerMood = -1;
					otherMood = -1;
					break;
				case "GrantExemption":
				case "LetGo":
				case "LetGoProvisioned":
				case "Honour":
				case "PayHonour":
					petitionerMood = 1;
					otherMood = 0;
					break;
				case "Apprentice":
					petitionerMood = 0;
					otherMood = 0;
					break;
				default:
					petitionerMood = -1;
					otherMood = 0;
					break;
			}
		}

		private const float ApprenticeXp = 4000f;

		private static void Apprentice(Pawn pawn, string workDefName, Pawn lord)
		{
			var work = DefDatabase<WorkTypeDef>.GetNamedSilentFail(workDefName);
			if (pawn?.skills == null || work == null || work.relevantSkills.NullOrEmpty()) return;

			SkillDef lowest = null;
			var lowestLevel = int.MaxValue;
			for (var i = 0; i < work.relevantSkills.Count; i++)
			{
				var level = pawn.skills.GetSkill(work.relevantSkills[i])?.Level ?? int.MaxValue;
				if (level < lowestLevel) { lowestLevel = level; lowest = work.relevantSkills[i]; }
			}
			if (lowest == null) return;

			try
			{
				pawn.skills.Learn(lowest, ApprenticeXp);
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] apprentissage impossible (" + e.Message + ").");
				return;
			}

			Mem(pawn, RimCourtDefOf.RimCourt_LordRuledForMe, lord);

			var master = FindMaster(pawn, work);
			if (master != null)
				Messages.Message("RimCourt.Apprenticed".Translate(pawn.LabelShortCap,
						WorkLabel(workDefName), master.LabelShortCap),
					new LookTargets(pawn), MessageTypeDefOf.NeutralEvent, historical: false);
			else
				Messages.Message("RimCourt.ApprenticedAlone".Translate(pawn.LabelShortCap,
						WorkLabel(workDefName)),
					new LookTargets(pawn), MessageTypeDefOf.NeutralEvent, historical: false);
		}

		private static Pawn FindMaster(Pawn apprentice, WorkTypeDef work)
		{
			var map = apprentice.MapHeld;
			if (map == null) return null;
			Pawn best = null;
			var bestAvg = 6f;
			foreach (var c in map.mapPawns.FreeColonistsSpawned)
			{
				if (c == apprentice || c.Dead || c.skills == null) continue;
				if (c.WorkTypeIsDisabled(work)) continue;
				var avg = c.skills.AverageOfRelevantSkillsFor(work);
				if (avg > bestAvg) { bestAvg = avg; best = c; }
			}
			return best;
		}

		private static bool ClearWork(Pawn pawn, string workDefName)
		{
			if (pawn?.workSettings == null || workDefName.NullOrEmpty()) return false;
			var work = DefDatabase<WorkTypeDef>.GetNamedSilentFail(workDefName);
			if (work == null || pawn.WorkTypeIsDisabled(work)) return false;
			try
			{
				pawn.workSettings.SetPriority(work, 0);
				return true;
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] dispense de travail impossible (" + e.Message + ").");
				return false;
			}
		}

		protected static WorkTypeDef WorstWorkFor(Pawn a)
		{
			if (a?.workSettings == null || a.skills == null) return null;
			WorkTypeDef worst = null;
			var worstAvg = float.MaxValue;
			foreach (var work in DefDatabase<WorkTypeDef>.AllDefsListForReading)
			{
				if (work.relevantSkills.NullOrEmpty()) continue;
				if (a.WorkTypeIsDisabled(work)) continue;
				if (a.workSettings.GetPriority(work) <= 0) continue;
				var avg = a.skills.AverageOfRelevantSkillsFor(work);
				if (avg > 6f) continue;
				if (a.skills.MaxPassionOfRelevantSkillsFor(work) != Passion.None) continue;
				if (avg < worstAvg || (avg == worstAvg && Rand.Bool))
				{
					worstAvg = avg;
					worst = work;
				}
			}
			return worst;
		}

		protected static string WorkLabel(string workDefName)
		{
			var work = DefDatabase<WorkTypeDef>.GetNamedSilentFail(workDefName);
			return work?.gerundLabel ?? work?.labelShort ?? workDefName ?? "?";
		}

		private static void LetGo(Pawn pawn)
		{
			if (pawn == null || pawn.Dead || !pawn.Spawned) return;
			try
			{
				PawnBanishUtility.Banish(pawn, giveThoughts: false);
				Messages.Message("RimCourt.LeaveGranted".Translate(pawn.LabelShortCap),
					MessageTypeDefOf.NeutralEvent, historical: false);
			}
			catch (System.Exception e)
			{
				Log.Error("[RimCourt] départ impossible : " + e);
			}
		}

		protected static void HonourBefore(Pawn honoured, Pawn lord)
		{
			var map = honoured?.MapHeld;
			if (map == null || RimCourtDefOf.RimCourt_HonouredHim == null) return;
			foreach (var c in map.mapPawns.FreeColonistsSpawned)
			{
				if (c == honoured) continue;
				c.needs?.mood?.thoughts?.memories?.TryGainMemory(RimCourtDefOf.RimCourt_HonouredHim, honoured);
			}
		}

		protected static void Mem(Pawn p, ThoughtDef thought, Pawn about)
		{
			if (p == null || p.Dead || thought == null) return;
			var mem = p.needs?.mood?.thoughts?.memories;
			if (mem == null) return;
			if (about != null)
			{
				if (about.Dead) return;
				mem.TryGainMemory(thought, about);
			}
			else
			{
				mem.TryGainMemory(thought);
			}
		}
	}

	public class PetitionWorker_Match : PetitionWorker_Request
	{
		public override Petition TryGenerate(Map map, Pawn lord, List<Petition> taken, int thresholdShift)
		{
			var found = new List<Petition>();
			foreach (var a in map.mapPawns.FreeColonistsSpawned)
			{
				if (!CanAsk(a, lord, taken)) continue;
				var partner = a.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Fiance);
				if (partner == null || !CanAsk(partner, lord, taken)) continue;
				if (a.relations.DirectRelationExists(PawnRelationDefOf.Spouse, partner)) continue;
				if (a.thingIDNumber > partner.thingIDNumber) continue;
				found.Add(new Petition { def = def, petitioner = a, other = partner });
			}
			return Pick(found);
		}
	}

	public class PetitionWorker_Exemption : PetitionWorker_Request
	{
		public override Petition TryGenerate(Map map, Pawn lord, List<Petition> taken, int thresholdShift)
		{
			var found = new List<Petition>();
			foreach (var a in map.mapPawns.FreeColonistsSpawned)
			{
				if (!CanAsk(a, lord, taken)) continue;
				var worst = WorstWorkFor(a);
				if (worst != null)
					found.Add(new Petition { def = def, petitioner = a, extra = worst.defName });
			}
			return Pick(found);
		}

		public override string CaseText(Petition p)
			=> def.caseText.Formatted(p.petitioner.Named("PETITIONER"), WorkLabel(p.extra).Named("WORK"));

		public override string VerdictLabel(Petition p, int index)
			=> def.verdicts[index].label.Formatted(p.petitioner.Named("PETITIONER"),
				WorkLabel(p.extra).Named("WORK"));

		public override string PastVerdictLabel(Petition p, int index)
		{
			var v = def.verdicts[index];
			return v.pastLabel.NullOrEmpty()
				? VerdictLabel(p, index)
				: v.pastLabel.Formatted(p.petitioner.Named("PETITIONER"), WorkLabel(p.extra).Named("WORK"));
		}
	}

	public class PetitionWorker_Leave : PetitionWorker_Request
	{
		private const float LeaveMood = 0.30f;

		public override Petition TryGenerate(Map map, Pawn lord, List<Petition> taken, int thresholdShift)
		{
			var found = new List<Petition>();
			foreach (var a in map.mapPawns.FreeColonistsSpawned)
			{
				if (!CanAsk(a, lord, taken)) continue;
				var mood = a.needs?.mood;
				if (mood == null) continue;
				if (mood.CurLevelPercentage > LeaveMood || mood.CurInstantLevelPercentage > LeaveMood) continue;
				found.Add(new Petition { def = def, petitioner = a });
			}
			return Pick(found);
		}
	}

	public class PetitionWorker_Honour : PetitionWorker_Request
	{
		private const float MinKills = 5f;
		private const int SilverPerKill = 25;
		private const int MinReward = 125;
		private const int MaxReward = 1000;

		public override Petition TryGenerate(Map map, Pawn lord, List<Petition> taken, int thresholdShift)
		{
			var found = new List<Petition>();
			foreach (var a in map.mapPawns.FreeColonistsSpawned)
			{
				if (!CanAsk(a, lord, taken) || a.records == null) continue;
				var kills = a.records.GetValue(RecordDefOf.KillsHumanlikes);
				if (kills < MinKills) continue;
				var count = UnityEngine.Mathf.RoundToInt(kills);
				found.Add(new Petition
				{
					def = def,
					petitioner = a,
					extra = count.ToString(),
					silver = UnityEngine.Mathf.Clamp(count * SilverPerKill, MinReward, MaxReward),
				});
			}
			return Pick(found);
		}

		public override string CaseText(Petition p)
			=> def.caseText.Formatted(p.petitioner.Named("PETITIONER"),
				p.silver.Named("SILVER"), (p.extra ?? "?").Named("KILLS"));

		public override string VerdictLabel(Petition p, int index)
			=> def.verdicts[index].label.Formatted(p.petitioner.Named("PETITIONER"),
				p.silver.Named("SILVER"), (p.extra ?? "?").Named("KILLS"));

		public override string PastVerdictLabel(Petition p, int index)
		{
			var v = def.verdicts[index];
			return v.pastLabel.NullOrEmpty()
				? VerdictLabel(p, index)
				: v.pastLabel.Formatted(p.petitioner.Named("PETITIONER"),
					p.silver.Named("SILVER"), (p.extra ?? "?").Named("KILLS"));
		}
	}
}
