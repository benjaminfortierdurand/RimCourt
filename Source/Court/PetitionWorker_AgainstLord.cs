using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimCourt.Court
{
	public class PetitionWorker_AgainstLord : PetitionWorker_Request
	{
		public const int MinGrievances = 2;

		public static PetitionDef TheDef
			=> DefDatabase<PetitionDef>.GetNamedSilentFail("RimCourt_AgainstLord");

		public static int GrievanceCount(Pawn p, Pawn lord)
		{
			var d = TheDef;
			if (d?.sourceThoughts == null || p == null || lord == null) return 0;
			var memories = p.needs?.mood?.thoughts?.memories?.Memories;
			if (memories == null) return 0;
			var maxAge = d.maxMemoryAgeDays * 60000;
			var count = 0;
			for (var i = 0; i < memories.Count; i++)
			{
				var m = memories[i];
				if (m?.def == null || m.age > maxAge) continue;
				if (!d.sourceThoughts.Contains(m.def)) continue;
				if (m.otherPawn != null && m.otherPawn != lord) continue;
				count++;
			}
			return count;
		}

		public override Petition TryGenerate(Map map, Pawn lord, List<Petition> taken, int thresholdShift)
		{
			if (!def.FromRealEvent || lord == null) return null;
			var found = new List<Petition>();

			foreach (var a in map.mapPawns.FreeColonistsSpawned)
			{
				if (!CanAsk(a, lord, taken)) continue;
				var count = GrievanceCount(a, lord);
				if (count < MinGrievances) continue;
				found.Add(new Petition { def = def, petitioner = a, extra = count.ToString() });
			}
			return Pick(found);
		}

		public override void ApplyVerdict(Petition p, VerdictOption v, Pawn lord)
		{
			var pawn = p.petitioner;

			switch (v.outcome)
			{
				case "AdmitFault":
					ClearGrievances(pawn, lord);
					Mem(pawn, RimCourtDefOf.RimCourt_LordAdmittedFault, lord);
					Mem(pawn, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					Messages.Message("RimCourt.LordAdmitted".Translate(
							lord?.LabelShortCap ?? "?", pawn.LabelShortCap),
						new LookTargets(pawn), MessageTypeDefOf.PositiveEvent, historical: false);
					break;

				case "RuleForSelf":
					Mem(pawn, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
					Mem(pawn, RimCourtDefOf.RimCourt_LordJudgedHimself, lord);
					Messages.Message("RimCourt.LordCleared".Translate(lord?.LabelShortCap ?? "?"),
						new LookTargets(pawn), MessageTypeDefOf.NegativeEvent, historical: false);
					break;

				case "FlogAccuser":
					Mem(pawn, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
					Mem(pawn, RimCourtDefOf.RimCourt_ScoldedByLord, null);
					CourtManager.Instance?.BeginFlogging(pawn);
					break;

				default:
					Mem(pawn, RimCourtDefOf.RimCourt_LordDismissedMyCase, lord);
					break;
			}
		}

		private void ClearGrievances(Pawn p, Pawn lord)
		{
			var memories = p?.needs?.mood?.thoughts?.memories;
			if (memories == null || lord == null || def.sourceThoughts == null) return;
			for (var i = 0; i < def.sourceThoughts.Count; i++)
			{
				try { memories.RemoveMemoriesOfDefWhereOtherPawnIs(def.sourceThoughts[i], lord); }
				catch { }
			}
		}

		public override void Reactions(Petition p, VerdictOption v, out int petitionerMood, out int otherMood)
		{
			otherMood = 0;
			switch (v.outcome)
			{
				case "AdmitFault":
					petitionerMood = 1;
					break;
				default:
					petitionerMood = -1;
					break;
			}
		}

		public override string CaseText(Petition p)
			=> def.caseText.Formatted(p.petitioner.Named("PETITIONER"),
				LordName(p).Named("LORD"), (p.extra ?? "?").Named("COUNT"));

		public override string VerdictLabel(Petition p, int index)
			=> def.verdicts[index].label.Formatted(p.petitioner.Named("PETITIONER"),
				LordName(p).Named("LORD"), (p.extra ?? "?").Named("COUNT"));

		public override string PastVerdictLabel(Petition p, int index)
		{
			var v = def.verdicts[index];
			return v.pastLabel.NullOrEmpty()
				? VerdictLabel(p, index)
				: v.pastLabel.Formatted(p.petitioner.Named("PETITIONER"),
					LordName(p).Named("LORD"), (p.extra ?? "?").Named("COUNT"));
		}

		private static string LordName(Petition p)
			=> CourtManager.Instance?.CurrentLord?.LabelShortCap ?? "?";
	}
}
