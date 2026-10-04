using RimWorld;
using Verse;

namespace RimCourt.Court
{
	public class PetitionWorker_Envoy : PetitionWorker_Outsider
	{
		public override void ApplyVerdict(Petition p, VerdictOption v, Pawn lord)
		{
			var pawn = p.petitioner;
			var map = pawn?.MapHeld;
			var home = pawn?.Faction;

			switch (v.outcome)
			{
				case "GiveArms":
					if (map != null && OutsiderUtility.TryTakeWeapons(map, p.silver))
					{
						Messages.Message("RimCourt.EnvoyArmsGiven".Translate(home?.Name ?? "?", p.silver),
							new LookTargets(pawn), MessageTypeDefOf.PositiveEvent, historical: false);
						OutsiderUtility.AffectGoodwill(home, 22);
					}
					else
					{
						Messages.Message("RimCourt.EnvoyNoArms".Translate(p.silver),
							MessageTypeDefOf.NegativeEvent, historical: false);
						OutsiderUtility.AffectGoodwill(home, -8);
					}
					break;

				case "SellArms":
					if (map != null && OutsiderUtility.TryTakeWeapons(map, p.silver))
					{
						OutsiderUtility.DropSilver(DropCell(lord, pawn), map, p.silver * 60);
						Messages.Message("RimCourt.EnvoyArmsSold".Translate(home?.Name ?? "?", p.silver * 60),
							new LookTargets(pawn), MessageTypeDefOf.NeutralEvent, historical: false);
						OutsiderUtility.AffectGoodwill(home, -6);
					}
					else
					{
						Messages.Message("RimCourt.EnvoyNoArms".Translate(p.silver),
							MessageTypeDefOf.NegativeEvent, historical: false);
						OutsiderUtility.AffectGoodwill(home, -8);
					}
					break;

				case "GiveProvisions":
					if (map != null && OutsiderUtility.TryTakeFood(map, p.silver))
					{
						Messages.Message("RimCourt.EnvoyFoodGiven".Translate(home?.Name ?? "?", p.silver),
							new LookTargets(pawn), MessageTypeDefOf.PositiveEvent, historical: false);
						OutsiderUtility.AffectGoodwill(home, 20);
					}
					else
					{
						Messages.Message("RimCourt.EnvoyNoFood".Translate(p.silver),
							MessageTypeDefOf.NegativeEvent, historical: false);
						OutsiderUtility.AffectGoodwill(home, -8);
					}
					break;

				case "SellProvisions":
					if (map != null && OutsiderUtility.TryTakeFood(map, p.silver))
					{
						OutsiderUtility.DropSilver(DropCell(lord, pawn), map, p.silver * 4);
						Messages.Message("RimCourt.EnvoyFoodSold".Translate(home?.Name ?? "?", p.silver * 4),
							new LookTargets(pawn), MessageTypeDefOf.NeutralEvent, historical: false);
						OutsiderUtility.AffectGoodwill(home, -6);
					}
					else
					{
						Messages.Message("RimCourt.EnvoyNoFood".Translate(p.silver),
							MessageTypeDefOf.NegativeEvent, historical: false);
						OutsiderUtility.AffectGoodwill(home, -8);
					}
					break;

				default:
					Messages.Message("RimCourt.EnvoyRefused".Translate(home?.Name ?? "?"),
						new LookTargets(pawn), MessageTypeDefOf.NegativeEvent, historical: false);
					OutsiderUtility.AffectGoodwill(home, -12);
					break;
			}

			OutsiderUtility.SendHome(pawn);
			CourtManager.Instance?.CloseRequest(pawn);
		}

		public override void Reactions(Petition p, VerdictOption v, out int petitionerMood, out int otherMood)
		{
			otherMood = 0;
			switch (v.outcome)
			{
				case "GiveArms":
				case "GiveProvisions":
					petitionerMood = 1;
					break;
				case "SellArms":
				case "SellProvisions":
					petitionerMood = 0;
					break;
				default:
					petitionerMood = -1;
					break;
			}
		}

		private static string HomeName(Petition p) => p.petitioner?.Faction?.Name ?? "?";

		public override string CaseText(Petition p)
			=> def.caseText.Formatted(p.petitioner.Named("PETITIONER"), p.silver.Named("SILVER"),
				HomeName(p).Named("FACTION"));

		public override string VerdictLabel(Petition p, int index)
			=> def.verdicts[index].label.Formatted(p.petitioner.Named("PETITIONER"),
				p.silver.Named("SILVER"), HomeName(p).Named("FACTION"));

		public override string PastVerdictLabel(Petition p, int index)
		{
			var v = def.verdicts[index];
			return v.pastLabel.NullOrEmpty()
				? VerdictLabel(p, index)
				: v.pastLabel.Formatted(p.petitioner.Named("PETITIONER"),
					p.silver.Named("SILVER"), HomeName(p).Named("FACTION"));
		}
	}
}
