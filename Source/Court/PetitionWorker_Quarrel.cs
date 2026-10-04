using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimCourt.Court
{
	public class PetitionWorker_Quarrel : PetitionWorker
	{
		public override Petition TryGenerate(Map map, Pawn lord, List<Petition> taken, int thresholdShift)
		{
			var mgr = CourtManager.Instance;
			var threshold = def.opinionThreshold + thresholdShift;
			var found = new List<Petition>();
			var colonists = map.mapPawns.FreeColonistsSpawned;
			for (var i = 0; i < colonists.Count; i++)
			{
				var a = colonists[i];
				if (!Eligible(a, lord, taken)) continue;
				for (var j = 0; j < colonists.Count; j++)
				{
					if (i == j) continue;
					var b = colonists[j];
					if (!Eligible(b, lord, taken)) continue;
					var ab = a.relations?.OpinionOf(b) ?? 0;
					if (ab > threshold) continue;
					var ba = b.relations?.OpinionOf(a) ?? 0;
					if (def.mutualGrudge && ba > threshold) continue;
					if (!def.mutualGrudge && ba <= threshold) continue;
					if (mgr != null && mgr.WasHeardRecently(a, b, def)) continue;
					found.Add(new Petition { def = def, petitioner = a, other = b });
				}
			}
			return found.Count == 0 ? null : found.RandomElement();
		}

		private static bool Eligible(Pawn p, Pawn lord, List<Petition> taken)
		{
			if (p == null || p == lord || p.Dead || p.Downed || !p.Spawned) return false;
			if (p.InMentalState || p.Drafted) return false;
			if (p.ageTracker != null && p.ageTracker.AgeBiologicalYears < 13) return false;
			if (p.health == null || !p.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) return false;
			for (var i = 0; i < taken.Count; i++)
				if (taken[i].Involves(p)) return false;
			return true;
		}

		public override void ApplyVerdict(Petition p, VerdictOption v, Pawn lord)
		{
			switch (v.outcome)
			{
				case "Reconcile":
					Mem(p.petitioner, RimCourtDefOf.RimCourt_SettledBeforeLord, p.other);
					Mem(p.other, RimCourtDefOf.RimCourt_SettledBeforeLord, p.petitioner);
					Mem(p.petitioner, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					Mem(p.other, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					break;
				case "PunishBoth":
					Mem(p.petitioner, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
					Mem(p.other, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
					Mem(p.petitioner, RimCourtDefOf.RimCourt_ScoldedByLord, null);
					Mem(p.other, RimCourtDefOf.RimCourt_ScoldedByLord, null);
					Mem(p.petitioner, RimCourtDefOf.RimCourt_SettledBeforeLord, p.other);
					Mem(p.other, RimCourtDefOf.RimCourt_SettledBeforeLord, p.petitioner);
					break;
				case "Dismiss":
					Mem(p.petitioner, RimCourtDefOf.RimCourt_LordDismissedMyCase, lord);
					if (def.mutualGrudge)
						Mem(p.other, RimCourtDefOf.RimCourt_LordDismissedMyCase, lord);
					break;
				case "FavorPetitioner":
					Mem(p.petitioner, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					Mem(p.petitioner, RimCourtDefOf.RimCourt_SettledBeforeLord, p.other);
					Mem(p.other, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
					break;
				case "FavorOther":
					Mem(p.other, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					Mem(p.petitioner, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
					break;

				case "BloodPrice":
					CourtManager.Instance?.BeginFlogging(p.other);
					ClearGrudge(p.petitioner, p.other);
					Mem(p.petitioner, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					Mem(p.petitioner, RimCourtDefOf.RimCourt_SettledBeforeLord, p.other);
					Mem(p.other, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
					Mem(p.other, RimCourtDefOf.RimCourt_ScoldedByLord, null);
					break;

				case "Stocks":
					if (!(CourtManager.Instance?.BeginStocks(p.other) ?? false))
					{
						Messages.Message("RimCourt.NoStocksLash".Translate(p.other?.LabelShortCap ?? "?"),
							MessageTypeDefOf.NeutralEvent, historical: false);
						CourtManager.Instance?.BeginFlogging(p.other);
					}
					ClearGrudge(p.petitioner, p.other);
					Mem(p.petitioner, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					Mem(p.petitioner, RimCourtDefOf.RimCourt_SettledBeforeLord, p.other);
					Mem(p.other, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
					Mem(p.other, RimCourtDefOf.RimCourt_ScoldedByLord, null);
					break;

				case "Duel":
					if (!(CourtManager.Instance?.BeginDuel(p.petitioner, p.other) ?? false))
						Mem(p.petitioner, RimCourtDefOf.RimCourt_LordDismissedMyCase, lord);
					break;

				case "Banish":
					Mem(p.petitioner, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					if (RimCourtDefOf.RimCourt_Tale_Banished != null && p.other != null && lord != null)
						TaleRecorder.RecordTale(RimCourtDefOf.RimCourt_Tale_Banished, lord, p.other);
					Banish(p.other);
					break;
			}
		}

		public override void Reactions(Petition p, VerdictOption v, out int petitionerMood, out int otherMood)
		{
			switch (v.outcome)
			{
				case "Reconcile":
					petitionerMood = 1;
					otherMood = 1;
					break;
				case "PunishBoth":
					petitionerMood = -1;
					otherMood = -1;
					break;
				case "Dismiss":
					petitionerMood = -1;
					otherMood = def.mutualGrudge ? -1 : 0;
					break;
				case "Stocks":
				case "BloodPrice":
					petitionerMood = 1;
					otherMood = -1;
					break;
				case "Duel":
					petitionerMood = 0;
					otherMood = 0;
					break;
				case "Banish":
					petitionerMood = 1;
					otherMood = -1;
					break;
				case "FavorPetitioner":
					petitionerMood = 1;
					otherMood = -1;
					break;
				case "FavorOther":
					petitionerMood = -1;
					otherMood = 1;
					break;
				default:
					petitionerMood = 0;
					otherMood = 0;
					break;
			}
		}

		public override void ResolveDuel(Pawn winner, Pawn loser, Pawn lord)
		{
			if (winner == null || loser == null) return;
			ClearGrudge(winner, loser);
			ClearGrudge(loser, winner);
			Mem(winner, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
			Mem(loser, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
			Mem(winner, RimCourtDefOf.RimCourt_SettledBeforeLord, loser);
			Mem(loser, RimCourtDefOf.RimCourt_SettledBeforeLord, winner);
		}

		private void ClearGrudge(Pawn wronged, Pawn other)
		{
			var mem = wronged?.needs?.mood?.thoughts?.memories;
			if (mem == null || other == null || def.sourceThoughts == null) return;
			for (var i = 0; i < def.sourceThoughts.Count; i++)
				mem.RemoveMemoriesOfDefWhereOtherPawnIs(def.sourceThoughts[i], other);
		}


		private static void Banish(Pawn p)
		{
			if (p == null || p.Dead || !p.Spawned || p.Faction != Faction.OfPlayer) return;
			try
			{
				PawnBanishUtility.Banish(p);
				CourtManager.Instance?.RememberExile(p);
				Messages.Message("RimCourt.BanishedByCourt".Translate(p.LabelShortCap),
					MessageTypeDefOf.NegativeEvent, historical: false);
			}
			catch (System.Exception e)
			{
				Log.Error("[RimCourt] bannissement impossible : " + e);
			}
		}

		private static void Mem(Pawn p, ThoughtDef thought, Pawn about)
		{
			if (p == null || p.Dead || thought == null) return;
			var mem = p.needs?.mood?.thoughts?.memories;
			if (mem == null) return;
			if (about != null)
			{
				if (about.Dead) return;
				mem.TryGainMemory(thought, about);
			}
			else
			{
				mem.TryGainMemory(thought);
			}
		}
	}
}
