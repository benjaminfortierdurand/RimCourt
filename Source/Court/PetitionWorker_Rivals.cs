using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimCourt.Court
{
	public class PetitionWorker_Rivals : PetitionWorker_Request
	{
		public override Petition TryGenerate(Map map, Pawn lord, List<Petition> taken, int thresholdShift)
		{
			var found = new List<Petition>();
			var suitors = new List<Pawn>();

			foreach (var loved in map.mapPawns.FreeColonistsSpawned)
			{
				if (loved.Dead || loved.relations == null) continue;
				suitors.Clear();
				foreach (var rel in loved.relations.DirectRelations)
					if (rel.def == PawnRelationDefOf.Lover && rel.otherPawn != null
						&& !rel.otherPawn.Dead)
						suitors.Add(rel.otherPawn);
				if (suitors.Count < 2) continue;

				for (var i = 0; i < suitors.Count; i++)
					for (var j = i + 1; j < suitors.Count; j++)
					{
						if (!CanAsk(suitors[i], lord, taken) || !CanAsk(suitors[j], lord, taken)) continue;
						var first = Rand.Bool ? suitors[i] : suitors[j];
						var second = first == suitors[i] ? suitors[j] : suitors[i];
						found.Add(new Petition
						{
							def = def,
							petitioner = first,
							other = second,
							third = loved,
						});
					}
			}
			return Pick(found);
		}

		public override void ApplyVerdict(Petition p, VerdictOption v, Pawn lord)
		{
			var loved = p.third;

			switch (v.outcome)
			{
				case "OrderRivalOff":
					BreakSuit(p.other, loved);
					Mem(p.petitioner, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					Mem(p.other, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
					Mem(p.other, RimCourtDefOf.RimCourt_ScoldedByLord, null);
					Messages.Message("RimCourt.RivalOrderedOff".Translate(
							p.other?.LabelShortCap ?? "?", loved?.LabelShortCap ?? "?",
							p.petitioner.LabelShortCap),
						new LookTargets(p.petitioner), MessageTypeDefOf.NeutralEvent, historical: false);
					break;

				case "RivalsDuel":
					if (!(CourtManager.Instance?.BeginDuel(p.petitioner, p.other) ?? false))
						Mem(p.petitioner, RimCourtDefOf.RimCourt_LordDismissedMyCase, lord);
					break;

				case "BothDesist":
					BreakSuit(p.petitioner, loved);
					BreakSuit(p.other, loved);
					Mem(p.petitioner, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
					Mem(p.other, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
					Messages.Message("RimCourt.RivalsBothOff".Translate(
							loved?.LabelShortCap ?? "?"),
						new LookTargets(loved), MessageTypeDefOf.NeutralEvent, historical: false);
					break;

				default:
					Mem(p.petitioner, RimCourtDefOf.RimCourt_LordDismissedMyCase, lord);
					break;
			}
		}

		public override void ResolveDuel(Pawn winner, Pawn loser, Pawn lord)
		{
			if (winner == null || loser == null) return;
			var loved = CourtManager.Instance?.Current?.third;
			BreakSuit(loser, loved);
			Mem(loser, RimCourtDefOf.RimCourt_DefeatedBeforeCourt, null);
			Mem(winner, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
			Messages.Message("RimCourt.RivalDuelWon".Translate(winner.LabelShortCap,
					loser.LabelShortCap, loved?.LabelShortCap ?? "?"),
				new LookTargets(winner), MessageTypeDefOf.NeutralEvent, historical: false);
		}

		private static void BreakSuit(Pawn suitor, Pawn loved)
		{
			if (suitor?.relations == null || loved == null) return;
			try
			{
				if (suitor.relations.DirectRelationExists(PawnRelationDefOf.Lover, loved))
				{
					suitor.relations.TryRemoveDirectRelation(PawnRelationDefOf.Lover, loved);
					suitor.relations.AddDirectRelation(PawnRelationDefOf.ExLover, loved);
				}
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] rupture de la cour amoureuse impossible (" + e.Message + ").");
			}
		}

		public override void Reactions(Petition p, VerdictOption v, out int petitionerMood, out int otherMood)
		{
			switch (v.outcome)
			{
				case "OrderRivalOff":
					petitionerMood = 1;
					otherMood = -1;
					break;
				case "RivalsDuel":
					petitionerMood = 0;
					otherMood = 0;
					break;
				case "BothDesist":
					petitionerMood = -1;
					otherMood = -1;
					break;
				default:
					petitionerMood = -1;
					otherMood = 0;
					break;
			}
		}

		public override string CaseText(Petition p)
			=> def.caseText.Formatted(p.petitioner.Named("PETITIONER"),
				(p.other ?? p.petitioner).Named("OTHER"),
				(p.third?.LabelShortCap ?? "?").Named("LOVER"));

		public override string VerdictLabel(Petition p, int index)
			=> def.verdicts[index].label.Formatted(p.petitioner.Named("PETITIONER"),
				(p.other ?? p.petitioner).Named("OTHER"),
				(p.third?.LabelShortCap ?? "?").Named("LOVER"));

		public override string PastVerdictLabel(Petition p, int index)
		{
			var v = def.verdicts[index];
			return v.pastLabel.NullOrEmpty()
				? VerdictLabel(p, index)
				: v.pastLabel.Formatted(p.petitioner.Named("PETITIONER"),
					(p.other ?? p.petitioner).Named("OTHER"),
					(p.third?.LabelShortCap ?? "?").Named("LOVER"));
		}
	}
}
