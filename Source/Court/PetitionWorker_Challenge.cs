using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimCourt.Court
{
	public class PetitionWorker_Challenge : PetitionWorker_Request
	{
		public const int MaxOpinion = -15;
		public const int MinSocial = 5;

		public static bool ThroneContestable(int stage)
			=> stage == JudgeReputation.StageWeak || stage == JudgeReputation.StageVenal;

		public static bool WouldRise(Pawn c, Pawn lord)
		{
			if (c == null || lord == null || c == lord) return false;
			if ((c.relations?.OpinionOf(lord) ?? 0) > MaxOpinion) return false;
			return (c.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0) >= MinSocial;
		}

		public override Petition TryGenerate(Map map, Pawn lord, List<Petition> taken, int thresholdShift)
		{
			var mgr = CourtManager.Instance;
			if (mgr == null || lord == null) return null;
			if (!ThroneContestable(mgr.Reputation.Stage)) return null;

			Pawn best = null;
			var bestScore = float.MinValue;
			foreach (var c in map.mapPawns.FreeColonistsSpawned)
			{
				if (!CanAsk(c, lord, taken) || !WouldRise(c, lord)) continue;
				var score = Prestige(c);
				if (score > bestScore) { bestScore = score; best = c; }
			}
			if (best == null) return null;

			var found = new List<Petition> { new Petition { def = def, petitioner = best } };
			return Pick(found);
		}

		public static float Prestige(Pawn p)
		{
			var s = 0f;
			var title = p.royalty?.MostSeniorTitle;
			if (title != null) s += 10000f + title.def.seniority;
			s += p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
			if (p.ageTracker != null) s += p.ageTracker.AgeBiologicalYears * 0.1f;
			return s;
		}

		public override void ApplyVerdict(Petition p, VerdictOption v, Pawn lord)
		{
			var challenger = p.petitioner;

			switch (v.outcome)
			{
				case "Yield":
					Mem(lord, RimCourtDefOf.RimCourt_Deposed, null);
					Mem(challenger, RimCourtDefOf.RimCourt_TookThrone, null);
					CourtManager.Instance?.AppointLord(challenger, lord);
					break;

				case "BanishChallenger":
					Mem(lord, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					if (challenger != null && !challenger.Dead && challenger.Spawned
						&& challenger.Faction == Faction.OfPlayer)
					{
						try
						{
							PawnBanishUtility.Banish(challenger);
							CourtManager.Instance?.RememberExile(challenger);
							Messages.Message("RimCourt.BanishedByCourt".Translate(challenger.LabelShortCap),
								MessageTypeDefOf.NegativeEvent, historical: false);
						}
						catch (System.Exception e)
						{
							Log.Error("[RimCourt] bannissement du prétendant impossible : " + e);
						}
					}
					break;

				case "StocksChallenger":
					Mem(lord, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					if (!(CourtManager.Instance?.BeginStocks(challenger) ?? false))
					{
						Messages.Message("RimCourt.NoStocksLash".Translate(challenger?.LabelShortCap ?? "?"),
							MessageTypeDefOf.NeutralEvent, historical: false);
						CourtManager.Instance?.BeginFlogging(challenger);
					}
					Mem(challenger, RimCourtDefOf.RimCourt_DefeatedBeforeCourt, null);
					Mem(challenger, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
					break;

				case "ThroneDuel":
					if (!(CourtManager.Instance?.BeginDuel(lord, challenger) ?? false))
						Mem(challenger, RimCourtDefOf.RimCourt_LordDismissedMyCase, lord);
					break;
			}
		}

		public override void ResolveDuel(Pawn winner, Pawn loser, Pawn lord)
		{
			if (winner == null || loser == null) return;
			if (winner == lord)
			{
				Mem(loser, RimCourtDefOf.RimCourt_DefeatedBeforeCourt, null);
				Mem(loser, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
				Messages.Message("RimCourt.ThroneDefended".Translate(lord.LabelShortCap, loser.LabelShortCap),
					new LookTargets(lord), MessageTypeDefOf.NeutralEvent, historical: false);
			}
			else
			{
				Mem(loser, RimCourtDefOf.RimCourt_Deposed, null);
				Mem(winner, RimCourtDefOf.RimCourt_TookThrone, null);
				CourtManager.Instance?.AppointLord(winner, loser);
			}
		}

		public override void Reactions(Petition p, VerdictOption v, out int petitionerMood, out int otherMood)
		{
			otherMood = 0;
			switch (v.outcome)
			{
				case "Yield":
					petitionerMood = 1;
					break;
				case "ThroneDuel":
					petitionerMood = 0;
					break;
				default:
					petitionerMood = -1;
					break;
			}
		}
	}
}
