using RimWorld;
using Verse;

namespace RimCourt.Court
{
	public class PetitionWorker_Arbitration : PetitionWorker_Outsider
	{
		private const int WinnerGoodwill = 18;
		private const int LoserGoodwill = -15;
		private const int RefusedGoodwill = -10;
		private const int BribedWinner = 6;
		private const int BribedLoser = -20;

		public override Petition FromRequest(OutsiderRequest req)
		{
			if (req == null || !req.Valid || req.otherPawn == null) return null;
			return new Petition
			{
				def = req.def,
				petitioner = req.pawn,
				other = req.otherPawn,
				silver = req.silver,
				extra = req.extra,
			};
		}

		public override void ApplyVerdict(Petition p, VerdictOption v, Pawn lord)
		{
			var first = p.petitioner?.Faction;
			var second = p.other?.Faction;
			var map = p.petitioner?.MapHeld;

			switch (v.outcome)
			{
				case "RuleForFirst":
					Settle(first, WinnerGoodwill, second, LoserGoodwill);
					Messages.Message("RimCourt.ArbitrationRuled".Translate(
							Name(first), Name(second)),
						new LookTargets(p.petitioner), MessageTypeDefOf.NeutralEvent, historical: false);
					break;

				case "RuleForSecond":
					Settle(first, LoserGoodwill, second, WinnerGoodwill);
					Messages.Message("RimCourt.ArbitrationRuled".Translate(
							Name(second), Name(first)),
						new LookTargets(p.other), MessageTypeDefOf.NeutralEvent, historical: false);
					break;

				case "ArbitrationFee":
					var paid = Rand.Bool;
					Settle(first, paid ? BribedWinner : BribedLoser,
						second, paid ? BribedLoser : BribedWinner);
					if (map != null) OutsiderUtility.DropSilver(DropCell(lord, p.petitioner), map, p.silver);
					Messages.Message("RimCourt.ArbitrationBought".Translate(
							Name(paid ? first : second), p.silver, Name(paid ? second : first)),
						new LookTargets(p.petitioner), MessageTypeDefOf.NeutralEvent, historical: false);
					break;

				default:
					Settle(first, RefusedGoodwill, second, RefusedGoodwill);
					Messages.Message("RimCourt.ArbitrationRefused".Translate(Name(first), Name(second)),
						MessageTypeDefOf.NegativeEvent, historical: false);
					break;
			}

			CourtManager.Instance?.CloseRequest(p.petitioner);
			OutsiderUtility.SendHome(p.petitioner);
			OutsiderUtility.SendHome(p.other);
		}

		private static void Settle(Faction a, int da, Faction b, int db)
		{
			OutsiderUtility.AffectGoodwill(a, da);
			OutsiderUtility.AffectGoodwill(b, db);
		}

		private static string Name(Faction f) => f?.Name ?? "?";

		public override void Reactions(Petition p, VerdictOption v, out int petitionerMood, out int otherMood)
		{
			switch (v.outcome)
			{
				case "RuleForFirst":
					petitionerMood = 1;
					otherMood = -1;
					break;
				case "RuleForSecond":
					petitionerMood = -1;
					otherMood = 1;
					break;
				case "ArbitrationFee":
					petitionerMood = 0;
					otherMood = 0;
					break;
				default:
					petitionerMood = -1;
					otherMood = -1;
					break;
			}
		}

		public override string CaseText(Petition p)
			=> def.caseText.Formatted(p.petitioner.Named("PETITIONER"), p.other.Named("OTHER"),
				Name(p.petitioner?.Faction).Named("FIRST"), Name(p.other?.Faction).Named("SECOND"),
				p.silver.Named("SILVER"), (p.extra ?? "?").Named("QUARREL"));

		public override string VerdictLabel(Petition p, int index)
			=> def.verdicts[index].label.Formatted(p.petitioner.Named("PETITIONER"), p.other.Named("OTHER"),
				Name(p.petitioner?.Faction).Named("FIRST"), Name(p.other?.Faction).Named("SECOND"),
				p.silver.Named("SILVER"), (p.extra ?? "?").Named("QUARREL"));

		public override string PastVerdictLabel(Petition p, int index)
		{
			var v = def.verdicts[index];
			return v.pastLabel.NullOrEmpty() ? VerdictLabel(p, index) : v.pastLabel.Formatted(
				p.petitioner.Named("PETITIONER"), p.other.Named("OTHER"),
				Name(p.petitioner?.Faction).Named("FIRST"), Name(p.other?.Faction).Named("SECOND"),
				p.silver.Named("SILVER"), (p.extra ?? "?").Named("QUARREL"));
		}
	}
}
