using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimCourt.Court
{
	public class PetitionWorker_Gratitude : PetitionWorker_Request
	{
		public override Petition TryGenerate(Map map, Pawn lord, List<Petition> taken, int thresholdShift)
		{
			if (!def.FromRealEvent) return null;
			var source = def.sourceThoughts[0];
			var maxAge = def.maxMemoryAgeDays * 60000;
			var found = new List<Petition>();

			foreach (var saved in map.mapPawns.FreeColonistsSpawned)
			{
				if (!CanAsk(saved, lord, taken)) continue;
				var memories = saved.needs?.mood?.thoughts?.memories?.Memories;
				if (memories == null) continue;

				for (var i = 0; i < memories.Count; i++)
				{
					var m = memories[i];
					if (m?.def != source || m.age > maxAge || m.otherPawn == null) continue;
					if (!CanAsk(m.otherPawn, lord, taken)) continue;
					found.Add(new Petition { def = def, petitioner = saved, other = m.otherPawn });
					break;
				}
			}
			return Pick(found);
		}

		public override void ApplyVerdict(Petition p, VerdictOption v, Pawn lord)
		{
			var saved = p.petitioner;
			var rescuer = p.other;

			switch (v.outcome)
			{
				case "HonourRescuer":
					Mem(saved, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					Mem(rescuer, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					Mem(rescuer, RimCourtDefOf.RimCourt_HonouredByLord, null);
					HonourBefore(rescuer, lord);
					Messages.Message("RimCourt.RescuerHonoured".Translate(
							rescuer?.LabelShortCap ?? "?", saved.LabelShortCap),
						new LookTargets(rescuer), MessageTypeDefOf.PositiveEvent, historical: false);
					break;

				case "OnlyDuty":
					Mem(saved, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
					Mem(rescuer, RimCourtDefOf.RimCourt_RefusedByLord, null);
					break;

				default:
					Mem(saved, RimCourtDefOf.RimCourt_LordDismissedMyCase, lord);
					break;
			}
		}

		public override void Reactions(Petition p, VerdictOption v, out int petitionerMood, out int otherMood)
		{
			switch (v.outcome)
			{
				case "HonourRescuer":
					petitionerMood = 1;
					otherMood = 1;
					break;
				case "OnlyDuty":
					petitionerMood = -1;
					otherMood = -1;
					break;
				default:
					petitionerMood = -1;
					otherMood = 0;
					break;
			}
		}
	}
}
