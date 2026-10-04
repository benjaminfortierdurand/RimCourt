using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimCourt.Court
{
	public class PetitionWorker_Memory : PetitionWorker_Quarrel
	{
		public override Petition TryGenerate(Map map, Pawn lord, List<Petition> taken, int thresholdShift)
		{
			if (!def.FromRealEvent) return null;

			var mgr = CourtManager.Instance;
			var maxAge = def.maxMemoryAgeDays * 60000;
			var found = new List<Petition>();

			var colonists = map.mapPawns.FreeColonistsSpawned;
			for (var i = 0; i < colonists.Count; i++)
			{
				var a = colonists[i];
				if (!CanPlead(a, lord, taken)) continue;
				var memories = a.needs?.mood?.thoughts?.memories?.Memories;
				if (memories == null) continue;

				for (var j = 0; j < memories.Count; j++)
				{
					var m = memories[j];
					if (m?.def == null || m.age > maxAge) continue;
					if (!def.sourceThoughts.Contains(m.def)) continue;
					var other = m.otherPawn;
					if (other == null || other == a) continue;
					if (!CanPlead(other, lord, taken)) continue;
					if (mgr != null && mgr.WasHeardRecently(a, other, def)) continue;
					found.Add(new Petition { def = def, petitioner = a, other = other });
				}
			}

			return found.Count == 0 ? null : found.RandomElement();
		}

		private static bool CanPlead(Pawn p, Pawn lord, List<Petition> taken)
		{
			if (p == null || p == lord || p.Dead || p.Downed || !p.Spawned) return false;
			if (p.InMentalState || p.Drafted || !p.IsFreeColonist) return false;
			if (p.ageTracker != null && p.ageTracker.AgeBiologicalYears < 13) return false;
			if (p.health == null || !p.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) return false;
			for (var i = 0; i < taken.Count; i++)
				if (taken[i].Involves(p)) return false;
			return true;
		}
	}
}
