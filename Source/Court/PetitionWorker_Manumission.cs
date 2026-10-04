using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimCourt.Court
{
	public class PetitionWorker_Manumission : PetitionWorker_Request
	{
		public override Petition TryGenerate(Map map, Pawn lord, List<Petition> taken, int thresholdShift)
		{
			if (!ModsConfig.IdeologyActive) return null;
			var found = new List<Petition>();
			var slaves = map.mapPawns.SlavesOfColonySpawned;
			for (var i = 0; i < slaves.Count; i++)
			{
				var s = slaves[i];
				if (!CanPlead(s, lord, taken)) continue;
				found.Add(new Petition { def = def, petitioner = s });
			}
			return Pick(found);
		}

		private static bool CanPlead(Pawn p, Pawn lord, List<Petition> taken)
		{
			if (p == null || p == lord || p.Dead || p.Downed || !p.Spawned) return false;
			if (p.InMentalState || p.Drafted || !p.IsSlaveOfColony) return false;
			if (p.ageTracker != null && p.ageTracker.AgeBiologicalYears < 16) return false;
			if (p.health == null || !p.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) return false;
			for (var i = 0; i < taken.Count; i++)
				if (taken[i].Involves(p)) return false;
			return true;
		}

		public override void ApplyVerdict(Petition p, VerdictOption v, Pawn lord)
		{
			var pawn = p.petitioner;
			switch (v.outcome)
			{
				case "Free":
					if (Free(pawn))
					{
						Mem(pawn, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
						Mem(pawn, RimCourtDefOf.RimCourt_Freed, null);
						Messages.Message("RimCourt.SlaveFreed".Translate(pawn.LabelShortCap),
							new LookTargets(pawn), MessageTypeDefOf.PositiveEvent, historical: false);
					}
					else
					{
						Mem(pawn, RimCourtDefOf.RimCourt_LordDismissedMyCase, lord);
					}
					break;

				case "FlogSlave":
					CourtManager.Instance?.BeginFlogging(pawn);
					Mem(pawn, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
					Mem(pawn, RimCourtDefOf.RimCourt_ScoldedByLord, null);
					break;

				default:
					base.ApplyVerdict(p, v, lord);
					break;
			}
		}

		public override void Reactions(Petition p, VerdictOption v, out int petitionerMood, out int otherMood)
		{
			otherMood = 0;
			switch (v.outcome)
			{
				case "Free": petitionerMood = 1; break;
				default: petitionerMood = -1; break;
			}
		}

		private static bool Free(Pawn pawn)
		{
			if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.guest == null) return false;
			try
			{
				pawn.guest.SetGuestStatus(null);
				return pawn.IsFreeColonist;
			}
			catch (System.Exception e)
			{
				Log.Error("[RimCourt] affranchissement impossible : " + e);
				return false;
			}
		}
	}
}
