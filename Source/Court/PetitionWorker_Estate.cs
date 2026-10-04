using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimCourt.Court
{
	public class PetitionWorker_Estate : PetitionWorker_Request
	{
		private const float KinImportanceFloor = 200f;

		public override Petition TryGenerate(Map map, Pawn lord, List<Petition> taken, int thresholdShift)
		{
			var maxAge = def.maxMemoryAgeDays * 60000;
			var claimants = new Dictionary<Pawn, List<Pawn>>();

			foreach (var c in map.mapPawns.FreeColonistsSpawned)
			{
				if (!CanAsk(c, lord, taken)) continue;
				var memories = c.needs?.mood?.thoughts?.memories?.Memories;
				if (memories == null) continue;

				for (var i = 0; i < memories.Count; i++)
				{
					var m = memories[i];
					if (m?.def == null || m.age > maxAge) continue;
					var dead = m.otherPawn;
					if (dead == null || !dead.Dead || dead.RaceProps?.Humanlike != true) continue;
					var name = m.def.defName;
					if (!name.StartsWith("My") || !name.EndsWith("Died")) continue;
					if (!IsHeir(c, dead)) continue;

					if (!claimants.TryGetValue(dead, out var list))
						claimants[dead] = list = new List<Pawn>();
					if (!list.Contains(c)) list.Add(c);
				}
			}

			var found = new List<Petition>();
			foreach (var pair in claimants)
			{
				if (pair.Value.Count < 2) continue;
				pair.Value.SortByDescending(h => h.GetMostImportantRelation(pair.Key)?.importance ?? 0f);
				found.Add(new Petition
				{
					def = def,
					petitioner = pair.Value[0],
					other = pair.Value[1],
					extra = pair.Key.LabelShortCap,
				});
			}
			return Pick(found);
		}

		private static bool IsHeir(Pawn c, Pawn dead)
		{
			if (dead.Faction == null || !dead.Faction.IsPlayer) return false;
			var rel = c.GetMostImportantRelation(dead);
			if (rel == null) return false;
			return rel.familyByBloodRelation || rel.importance >= KinImportanceFloor;
		}

		public override void ApplyVerdict(Petition p, VerdictOption v, Pawn lord)
		{
			var first = p.petitioner;
			var second = p.other;

			switch (v.outcome)
			{
				case "FavorPetitioner":
					Award(first, second, lord, p.extra);
					break;

				case "FavorOther":
					Award(second, first, lord, p.extra);
					break;

				case "NobodyInherits":
					Mem(first, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
					Mem(second, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
					Messages.Message("RimCourt.EstateNobody".Translate(p.extra ?? "?"),
						new LookTargets(first), MessageTypeDefOf.NeutralEvent, historical: false);
					break;

				default:
					Mem(first, RimCourtDefOf.RimCourt_LordDismissedMyCase, lord);
					Mem(second, RimCourtDefOf.RimCourt_LordDismissedMyCase, lord);
					break;
			}
		}

		private static void Award(Pawn winner, Pawn loser, Pawn lord, string deadName)
		{
			Mem(winner, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
			Mem(loser, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
			Mem(loser, RimCourtDefOf.RimCourt_EstateDenied, winner);
			Messages.Message("RimCourt.EstateRuled".Translate(
					winner?.LabelShortCap ?? "?", deadName ?? "?", loser?.LabelShortCap ?? "?"),
				new LookTargets(winner), MessageTypeDefOf.NeutralEvent, historical: false);
		}

		public override void Reactions(Petition p, VerdictOption v, out int petitionerMood, out int otherMood)
		{
			switch (v.outcome)
			{
				case "FavorPetitioner":
					petitionerMood = 1;
					otherMood = -1;
					break;
				case "FavorOther":
					petitionerMood = -1;
					otherMood = 1;
					break;
				default:
					petitionerMood = -1;
					otherMood = -1;
					break;
			}
		}

		public override string CaseText(Petition p)
			=> def.caseText.Formatted(p.petitioner.Named("PETITIONER"),
				(p.other ?? p.petitioner).Named("OTHER"), (p.extra ?? "?").Named("DEAD"));
	}
}
