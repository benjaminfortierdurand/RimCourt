using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimCourt.Court
{
	public class PetitionWorker_Penitent : PetitionWorker_Request
	{
		private const int VictimGrudge = -15;

		public override Petition TryGenerate(Map map, Pawn lord, List<Petition> taken, int thresholdShift)
		{
			if (!def.FromRealEvent) return null;
			var maxAge = def.maxMemoryAgeDays * 60000;
			var found = new List<Petition>();

			foreach (var victim in map.mapPawns.FreeColonistsSpawned)
			{
				if (!CanAsk(victim, lord, taken)) continue;
				var memories = victim.needs?.mood?.thoughts?.memories?.Memories;
				if (memories == null) continue;

				for (var i = 0; i < memories.Count; i++)
				{
					var m = memories[i];
					if (m?.def == null || m.age > maxAge || m.otherPawn == null) continue;
					if (!def.sourceThoughts.Contains(m.def)) continue;
					var guilty = m.otherPawn;
					if (!CanAsk(guilty, lord, taken)) continue;
					if ((victim.relations?.OpinionOf(guilty) ?? 0) > VictimGrudge) continue;
					if ((guilty.relations?.OpinionOf(victim) ?? 0) < 0) continue;
					found.Add(new Petition { def = def, petitioner = guilty, other = victim });
					break;
				}
			}
			return Pick(found);
		}

		public override void ApplyVerdict(Petition p, VerdictOption v, Pawn lord)
		{
			var guilty = p.petitioner;
			var victim = p.other;

			switch (v.outcome)
			{
				case "OrderForgiveness":
					ClearGrudges(victim, guilty);
					Mem(guilty, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					Mem(guilty, RimCourtDefOf.RimCourt_SettledBeforeLord, victim);
					Mem(victim, RimCourtDefOf.RimCourt_SettledBeforeLord, guilty);
					Messages.Message("RimCourt.ForgivenessOrdered".Translate(
							victim?.LabelShortCap ?? "?", guilty.LabelShortCap),
						new LookTargets(guilty), MessageTypeDefOf.PositiveEvent, historical: false);
					break;

				case "Penance":
					ClearGrudges(victim, guilty);
					Mem(guilty, RimCourtDefOf.RimCourt_SettledBeforeLord, victim);
					Mem(victim, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					Mem(victim, RimCourtDefOf.RimCourt_SettledBeforeLord, guilty);
					CourtManager.Instance?.BeginFlogging(guilty);
					break;

				case "GrudgeDeserved":
					Mem(victim, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					Mem(guilty, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
					Messages.Message("RimCourt.GrudgeDeserved".Translate(
							guilty.LabelShortCap, victim?.LabelShortCap ?? "?"),
						new LookTargets(guilty), MessageTypeDefOf.NeutralEvent, historical: false);
					break;

				default:
					Mem(guilty, RimCourtDefOf.RimCourt_LordDismissedMyCase, lord);
					break;
			}
		}

		private void ClearGrudges(Pawn victim, Pawn guilty)
		{
			var mem = victim?.needs?.mood?.thoughts?.memories;
			if (mem == null || guilty == null || def.sourceThoughts == null) return;
			for (var i = 0; i < def.sourceThoughts.Count; i++)
			{
				try { mem.RemoveMemoriesOfDefWhereOtherPawnIs(def.sourceThoughts[i], guilty); }
				catch { }
			}
		}

		public override void Reactions(Petition p, VerdictOption v, out int petitionerMood, out int otherMood)
		{
			switch (v.outcome)
			{
				case "OrderForgiveness":
					petitionerMood = 1;
					otherMood = -1;
					break;
				case "Penance":
					petitionerMood = 0;
					otherMood = 1;
					break;
				case "GrudgeDeserved":
					petitionerMood = -1;
					otherMood = 1;
					break;
				default:
					petitionerMood = -1;
					otherMood = 0;
					break;
			}
		}
	}
}
