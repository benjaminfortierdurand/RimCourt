using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimCourt.Court
{
	public class PetitionWorker_Outsider : PetitionWorker
	{
		public override Petition TryGenerate(Map map, Pawn lord, List<Petition> taken, int thresholdShift)
			=> null;

		public virtual Petition FromRequest(OutsiderRequest req)
		{
			if (req == null || !req.Valid) return null;
			return new Petition { def = req.def, petitioner = req.pawn, other = null, silver = req.silver,
				deserter = req.deserter };
		}

		public override void ApplyVerdict(Petition p, VerdictOption v, Pawn lord)
		{
			var pawn = p.petitioner;
			var map = pawn?.MapHeld;
			var home = pawn?.Faction;

			switch (v.outcome)
			{
				case "TakeIn":
					if (OutsiderUtility.TakeIntoColony(pawn))
					{
						Messages.Message("RimCourt.OutsiderJoined".Translate(pawn.LabelShortCap),
							new LookTargets(pawn), MessageTypeDefOf.PositiveEvent, historical: false);
						OutsiderUtility.AffectGoodwill(home, 8);
					}
					break;

				case "BuyIn":
					if (map != null)
						OutsiderUtility.DropSilver(DropCell(lord, pawn), map, p.silver);
					if (OutsiderUtility.TakeIntoColony(pawn))
						Messages.Message("RimCourt.OutsiderBoughtIn".Translate(pawn.LabelShortCap, p.silver),
							new LookTargets(pawn), MessageTypeDefOf.NeutralEvent, historical: false);
					break;

				case "PayHim":
					var owed = p.silver;
					if (map != null && OutsiderUtility.TryPaySilver(map, owed))
					{
						Messages.Message("RimCourt.OutsiderPaid".Translate(pawn.LabelShortCap, owed),
							new LookTargets(pawn), MessageTypeDefOf.PositiveEvent, historical: false);
						OutsiderUtility.AffectGoodwill(home, 10);
					}
					else
					{
						Messages.Message("RimCourt.OutsiderNoSilver".Translate(owed),
							MessageTypeDefOf.NegativeEvent, historical: false);
						OutsiderUtility.AffectGoodwill(home, -5);
					}
					OutsiderUtility.SendHome(pawn);
					break;

				case "CourtFee":
					if (map != null)
						OutsiderUtility.DropSilver(DropCell(lord, pawn), map, p.silver);
					Messages.Message("RimCourt.OutsiderFeeTaken".Translate(pawn.LabelShortCap, p.silver),
						new LookTargets(pawn), MessageTypeDefOf.NeutralEvent, historical: false);
					OutsiderUtility.AffectGoodwill(home, -12);
					OutsiderUtility.SendHome(pawn);
					break;

				case "SendAway":
					OutsiderUtility.AffectGoodwill(home, -6);
					OutsiderUtility.SendHome(pawn);
					break;

				default:
					OutsiderUtility.SendHome(pawn);
					break;
			}

			CourtManager.Instance?.CloseRequest(pawn);
		}

		protected static IntVec3 DropCell(Pawn lord, Pawn petitioner)
		{
			if (lord != null && lord.Spawned) return lord.Position;
			return petitioner != null && petitioner.Spawned ? petitioner.Position : IntVec3.Invalid;
		}

		public override void Reactions(Petition p, VerdictOption v, out int petitionerMood, out int otherMood)
		{
			otherMood = 0;
			switch (v.outcome)
			{
				case "TakeIn":
				case "PayHim":
					petitionerMood = 1;
					break;
				case "BuyIn":
					petitionerMood = 0;
					break;
				default:
					petitionerMood = -1;
					break;
			}
		}

		public override string CaseText(Petition p)
			=> def.caseText.Formatted(p.petitioner.Named("PETITIONER"), p.silver.Named("SILVER"));

		public override string VerdictLabel(Petition p, int index)
			=> def.verdicts[index].label.Formatted(p.petitioner.Named("PETITIONER"), p.silver.Named("SILVER"));

		public override string PastVerdictLabel(Petition p, int index)
		{
			var v = def.verdicts[index];
			return v.pastLabel.NullOrEmpty()
				? VerdictLabel(p, index)
				: v.pastLabel.Formatted(p.petitioner.Named("PETITIONER"), p.silver.Named("SILVER"));
		}
	}
}
