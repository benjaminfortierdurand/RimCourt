using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimCourt.Court
{
	public class PetitionWorker_Pestered : PetitionWorker_Request
	{
		private const int MinAttempts = 2;

		private static readonly ThoughtDef Rebuffed =
			DefDatabase<ThoughtDef>.GetNamedSilentFail("RebuffedMyRomanceAttempt");
		private static readonly ThoughtDef RebuffedMood =
			DefDatabase<ThoughtDef>.GetNamedSilentFail("RebuffedMyRomanceAttemptMood");

		public override Petition TryGenerate(Map map, Pawn lord, List<Petition> taken, int thresholdShift)
		{
			if (!def.FromRealEvent) return null;
			var source = def.sourceThoughts[0];
			var maxAge = def.maxMemoryAgeDays * 60000;
			var found = new List<Petition>();
			var counts = new Dictionary<Pawn, int>();

			foreach (var a in map.mapPawns.FreeColonistsSpawned)
			{
				if (!CanAsk(a, lord, taken)) continue;
				var memories = a.needs?.mood?.thoughts?.memories?.Memories;
				if (memories == null) continue;

				counts.Clear();
				for (var i = 0; i < memories.Count; i++)
				{
					var m = memories[i];
					if (m?.def != source || m.age > maxAge || m.otherPawn == null) continue;
					counts.TryGetValue(m.otherPawn, out var n);
					counts[m.otherPawn] = n + 1;
				}

				foreach (var pair in counts)
				{
					if (pair.Value < MinAttempts) continue;
					if (!CanAsk(pair.Key, lord, taken)) continue;
					found.Add(new Petition
					{
						def = def,
						petitioner = a,
						other = pair.Key,
						extra = pair.Value.ToString(),
					});
				}
			}
			return Pick(found);
		}

		public override void ApplyVerdict(Petition p, VerdictOption v, Pawn lord)
		{
			var pestered = p.petitioner;
			var suitor = p.other;

			switch (v.outcome)
			{
				case "OrderPeace":
					ClearBothWays(pestered, suitor);
					Mem(pestered, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					Mem(pestered, RimCourtDefOf.RimCourt_SettledBeforeLord, suitor);
					Mem(suitor, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
					Mem(suitor, RimCourtDefOf.RimCourt_ScoldedByLord, null);
					Messages.Message("RimCourt.PesteredOrdered".Translate(
							suitor?.LabelShortCap ?? "?", pestered.LabelShortCap),
						new LookTargets(pestered), MessageTypeDefOf.PositiveEvent, historical: false);
					break;

				case "StocksSuitor":
					ClearBothWays(pestered, suitor);
					if (!(CourtManager.Instance?.BeginStocks(suitor) ?? false))
					{
						Messages.Message("RimCourt.NoStocksLash".Translate(suitor?.LabelShortCap ?? "?"),
							MessageTypeDefOf.NeutralEvent, historical: false);
						CourtManager.Instance?.BeginFlogging(suitor);
					}
					Mem(pestered, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					Mem(pestered, RimCourtDefOf.RimCourt_SettledBeforeLord, suitor);
					Mem(suitor, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
					Mem(suitor, RimCourtDefOf.RimCourt_ScoldedByLord, null);
					break;

				case "NoHarmAsking":
					Mem(pestered, RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
					Mem(pestered, RimCourtDefOf.RimCourt_RefusedByLord, null);
					Mem(suitor, RimCourtDefOf.RimCourt_LordRuledForMe, lord);
					break;

				default:
					Mem(pestered, RimCourtDefOf.RimCourt_LordDismissedMyCase, lord);
					break;
			}
		}

		private void ClearBothWays(Pawn pestered, Pawn suitor)
		{
			var source = def.sourceThoughts[0];
			try
			{
				var mem = pestered?.needs?.mood?.thoughts?.memories;
				if (mem != null && suitor != null)
					mem.RemoveMemoriesOfDefWhereOtherPawnIs(source, suitor);

				var his = suitor?.needs?.mood?.thoughts?.memories;
				if (his != null && pestered != null)
				{
					if (Rebuffed != null) his.RemoveMemoriesOfDefWhereOtherPawnIs(Rebuffed, pestered);
					if (RebuffedMood != null) his.RemoveMemoriesOfDefWhereOtherPawnIs(RebuffedMood, pestered);
				}
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] apaisement des avances impossible (" + e.Message + ").");
			}
		}

		public override void Reactions(Petition p, VerdictOption v, out int petitionerMood, out int otherMood)
		{
			switch (v.outcome)
			{
				case "StocksSuitor":
				case "OrderPeace":
					petitionerMood = 1;
					otherMood = -1;
					break;
				case "NoHarmAsking":
					petitionerMood = -1;
					otherMood = 1;
					break;
				default:
					petitionerMood = -1;
					otherMood = 0;
					break;
			}
		}

		public override string CaseText(Petition p)
			=> def.caseText.Formatted(p.petitioner.Named("PETITIONER"),
				(p.other ?? p.petitioner).Named("OTHER"), (p.extra ?? "?").Named("COUNT"));
	}
}
