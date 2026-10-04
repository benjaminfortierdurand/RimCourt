using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimCourt.Court
{
	public class PetitionWorker_Adultery : PetitionWorker_Request
	{
		public override Petition TryGenerate(Map map, Pawn lord, List<Petition> taken, int thresholdShift)
		{
			if (!def.FromRealEvent) return null;
			var source = def.sourceThoughts[0];
			var maxAge = def.maxMemoryAgeDays * 60000;
			var found = new List<Petition>();

			foreach (var b in map.mapPawns.FreeColonistsSpawned)
			{
				if (!CanAsk(b, lord, taken)) continue;
				var a = b.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Spouse);
				if (a == null || !CanAsk(a, lord, taken)) continue;

				var memories = b.needs?.mood?.thoughts?.memories?.Memories;
				if (memories == null) continue;
				var fresh = false;
				for (var i = 0; i < memories.Count; i++)
				{
					var m = memories[i];
					if (m?.def == source && m.otherPawn == a && m.age <= maxAge) { fresh = true; break; }
				}
				if (!fresh) continue;

				var lover = a.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Lover);
				found.Add(new Petition
				{
					def = def,
					petitioner = b,
					other = a,
					extra = lover?.LabelShortCap ?? "RimCourt.SomeoneElse".Translate().ToString(),
				});
			}
			return Pick(found);
		}

		public override void ApplyVerdict(Petition p, VerdictOption v, Pawn lord)
		{
			var wronged = p.petitioner;
			var adulterer = p.other;

			switch (v.outcome)
			{
				case "Divorce":
					ClearBetrayal(wronged, adulterer);
					Mem(wronged, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					Mem(adulterer, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
					try
					{
						if (wronged.relations.DirectRelationExists(PawnRelationDefOf.Spouse, adulterer))
							SpouseRelationUtility.DoDivorce(wronged, adulterer);
						Messages.Message("RimCourt.AdulteryDivorced".Translate(
								wronged.LabelShortCap, adulterer?.LabelShortCap ?? "?"),
							new LookTargets(wronged), MessageTypeDefOf.NeutralEvent, historical: false);
					}
					catch (System.Exception e)
					{
						Log.Error("[RimCourt] dissolution du mariage impossible : " + e);
					}
					break;

				case "EndAffair":
					BreakAffair(adulterer);
					Mem(wronged, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					Mem(adulterer, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
					Mem(adulterer, RimCourtDefOf.RimCourt_ScoldedByLord, null);
					Messages.Message("RimCourt.AdulteryOrdered".Translate(
							adulterer?.LabelShortCap ?? "?", p.extra ?? "?"),
						new LookTargets(adulterer), MessageTypeDefOf.NeutralEvent, historical: false);
					break;

				case "FlogAdulterer":
					BreakAffair(adulterer);
					ClearBetrayal(wronged, adulterer);
					Mem(wronged, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					Mem(wronged, RimCourtDefOf.RimCourt_SettledBeforeLord, adulterer);
					Mem(adulterer, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
					Mem(adulterer, RimCourtDefOf.RimCourt_ScoldedByLord, null);
					CourtManager.Instance?.BeginFlogging(adulterer);
					break;

				default:
					Mem(wronged, RimCourtDefOf.RimCourt_LordDismissedMyCase, lord);
					break;
			}
		}

		private void ClearBetrayal(Pawn wronged, Pawn adulterer)
		{
			try
			{
				var mem = wronged?.needs?.mood?.thoughts?.memories;
				if (mem != null && adulterer != null)
					mem.RemoveMemoriesOfDefWhereOtherPawnIs(def.sourceThoughts[0], adulterer);
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] apaisement de la trahison impossible (" + e.Message + ").");
			}
		}

		private static void BreakAffair(Pawn adulterer)
		{
			var relations = adulterer?.relations;
			if (relations == null) return;
			try
			{
				var lovers = new List<Pawn>();
				foreach (var rel in relations.DirectRelations)
					if (rel.def == PawnRelationDefOf.Lover && rel.otherPawn != null)
						lovers.Add(rel.otherPawn);
				for (var i = 0; i < lovers.Count; i++)
				{
					relations.TryRemoveDirectRelation(PawnRelationDefOf.Lover, lovers[i]);
					relations.AddDirectRelation(PawnRelationDefOf.ExLover, lovers[i]);
				}
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] rupture de la liaison impossible (" + e.Message + ").");
			}
		}

		public override void Reactions(Petition p, VerdictOption v, out int petitionerMood, out int otherMood)
		{
			switch (v.outcome)
			{
				case "Divorce":
				case "EndAffair":
				case "FlogAdulterer":
					petitionerMood = 1;
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
				(p.other ?? p.petitioner).Named("OTHER"), (p.extra ?? "?").Named("LOVER"));

		public override string VerdictLabel(Petition p, int index)
			=> def.verdicts[index].label.Formatted(p.petitioner.Named("PETITIONER"),
				(p.other ?? p.petitioner).Named("OTHER"), (p.extra ?? "?").Named("LOVER"));

		public override string PastVerdictLabel(Petition p, int index)
		{
			var v = def.verdicts[index];
			return v.pastLabel.NullOrEmpty()
				? VerdictLabel(p, index)
				: v.pastLabel.Formatted(p.petitioner.Named("PETITIONER"),
					(p.other ?? p.petitioner).Named("OTHER"), (p.extra ?? "?").Named("LOVER"));
		}
	}
}
