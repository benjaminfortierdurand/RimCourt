using RimWorld;
using Verse;

namespace RimCourt.Court
{
	public class PetitionWorker_Pardon : PetitionWorker_Outsider
	{
		private const int SpurnMinDays = 10;
		private const int SpurnMaxDays = 25;

		public override void ApplyVerdict(Petition p, VerdictOption v, Pawn lord)
		{
			var pawn = p.petitioner;

			switch (v.outcome)
			{
				case "PardonHim":
					CourtManager.Instance?.CloseRequest(pawn);
					if (OutsiderUtility.TakeIntoColony(pawn))
					{
						pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(
							RimCourtDefOf.RimCourt_LordRuledForMe, lord);
						Messages.Message("RimCourt.ExilePardoned".Translate(pawn.LabelShortCap),
							new LookTargets(pawn), MessageTypeDefOf.PositiveEvent, historical: false);
					}
					break;

				case "SpurnHim":
					CourtManager.Instance?.CloseRequest(pawn);
					OutsiderUtility.SendHome(pawn);
					CourtManager.Instance?.RegisterSpurnedExile(pawn, SpurnMinDays, SpurnMaxDays);
					Messages.Message("RimCourt.ExileSpurned".Translate(pawn.LabelShortCap),
						new LookTargets(pawn), MessageTypeDefOf.NegativeEvent, historical: false);
					break;

				default:
					CourtManager.Instance?.CloseRequest(pawn);
					OutsiderUtility.SendHome(pawn);
					if (Rand.Chance(0.5f))
						CourtManager.Instance?.RegisterSpurnedExile(pawn, SpurnMinDays, SpurnMaxDays);
					Messages.Message("RimCourt.ExileTurnedAway".Translate(pawn.LabelShortCap),
						MessageTypeDefOf.NeutralEvent, historical: false);
					break;
			}
		}

		public override void Reactions(Petition p, VerdictOption v, out int petitionerMood, out int otherMood)
		{
			otherMood = 0;
			petitionerMood = v.outcome == "PardonHim" ? 1 : -1;
		}
	}
}
