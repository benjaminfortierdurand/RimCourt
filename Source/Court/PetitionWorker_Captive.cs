using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimCourt.Court
{
	public class PetitionWorker_Captive : PetitionWorker_Request
	{
		private const int ReleaseIndex = 0;

		public override bool UsesPrecedent => false;

		public override int ExtraCallTicks => 2400;

		public override int TimeoutIndex(Petition p) => ReleaseIndex;

		public override Petition TryGenerate(Map map, Pawn lord, List<Petition> taken, int thresholdShift)
		{
			var found = new List<Petition>();
			var prisoners = map.mapPawns.PrisonersOfColonySpawned;
			for (var i = 0; i < prisoners.Count; i++)
			{
				var c = prisoners[i];
				if (!CaptiveUtility.CanBeTried(c, map, lord, taken)) continue;
				found.Add(new Petition
				{
					def = def,
					petitioner = c,
					extra = CaptiveUtility.Encode(CaptiveUtility.Investigate(c)),
					silver = CaptiveUtility.RansomFor(c),
				});
			}
			return Pick(found);
		}

		public override VerdictOption Weigh(Petition p, VerdictOption v)
		{
			var tier = CaptiveUtility.Decode(p.extra).tier;
			switch (v.outcome)
			{
				case "Release":
					return tier == CaptiveUtility.TierBlood ? v.WithWeights(0, 0, 0, 3)
						: tier == CaptiveUtility.TierHurt ? v.WithWeights(0, 0, 0, 2)
						: v.WithWeights(2, 0, 0, 0);
				case "Ransom":
					return tier == CaptiveUtility.TierBlood ? v.WithWeights(0, 0, 3, 0)
						: v.WithWeights(1, 0, 1, 0);
				case "StocksCaptive":
					return tier == CaptiveUtility.TierBlood ? v.WithWeights(1, 0, 0, 2)
						: tier == CaptiveUtility.TierHurt ? v.WithWeights(2, 0, 0, 0)
						: v.WithWeights(0, 2, 0, 0);
				case "Execute":
					return tier == CaptiveUtility.TierBlood ? v.WithWeights(3, 0, 0, 0)
						: tier == CaptiveUtility.TierHurt ? v.WithWeights(2, 1, 0, 0)
						: v.WithWeights(0, 3, 0, 0);
				default:
					return v;
			}
		}

		public override void ApplyVerdict(Petition p, VerdictOption v, Pawn lord)
		{
			var c = p.petitioner;
			var mgr = CourtManager.Instance;
			mgr?.NoteCaptiveTried(c);
			var d = CaptiveUtility.Decode(p.extra);
			var faction = c?.Faction?.Name ?? "?";
			switch (v.outcome)
			{
				case "Release":
					if (CaptiveUtility.Release(c))
					{
						if (d.killed) TellKillerFreed(c, lord);
						Messages.Message("RimCourt.CaptiveReleased".Translate(c.LabelShortCap, faction),
							new LookTargets(c), MessageTypeDefOf.NeutralEvent, historical: false);
					}
					break;

				case "Ransom":
					var seat = mgr?.SeatFor(c?.MapHeld);
					if (CaptiveUtility.Ransom(c, p.silver, seat?.Position ?? IntVec3.Invalid))
					{
						if (d.killed) TellKillerFreed(c, lord);
						Messages.Message("RimCourt.CaptiveRansomed".Translate(c.LabelShortCap, faction, p.silver),
							new LookTargets(c), MessageTypeDefOf.PositiveEvent, historical: false);
					}
					break;

				case "StocksCaptive":
					CaptiveUtility.Ground(c);
					if (mgr == null || !mgr.BeginStocks(c))
					{
						Messages.Message("RimCourt.CaptiveNoStocks".Translate(c?.LabelShortCap ?? "?"),
							MessageTypeDefOf.NeutralEvent, historical: false);
						CaptiveUtility.SendBackToCell(c);
					}
					break;

				case "Execute":
					CaptiveUtility.Ground(c);
					var map = c?.Map;
					var candidates = map == null ? new List<Pawn>() : CaptiveUtility.Executioners(map, c, lord);
					if (candidates.Count == 0)
					{
						CaptiveUtility.MarkForExecution(c);
						Messages.Message("RimCourt.CaptiveNoExecutioner".Translate(c?.LabelShortCap ?? "?"),
							MessageTypeDefOf.NeutralEvent, historical: false);
						break;
					}
					var tier = d.tier;
					var guard = mgr?.CaptiveGuard;
					Find.WindowStack.Add(new UI.Dialog_PickFlogger(c, candidates, lord, null,
						picked =>
						{
							if (picked != null && mgr != null && mgr.BeginExecution(c, picked, tier)) return;
							ExecuteNow(c, guard, lord, tier);
						},
						"RimCourt.ExecutionerDialogTitle", "RimCourt.ExecutionerDialogDesc", "RimCourt.ExecutionerNoOne", true));
					break;

				default:
					Keep(c);
					break;
			}
		}

		private static void ExecuteNow(Pawn c, Pawn preferred, Pawn lord, int tier)
		{
			var exec = CaptiveUtility.Execute(c, preferred, lord, tier);
			if (exec != null)
			{
				Messages.Message("RimCourt.CaptiveExecuted".Translate(exec.LabelShortCap, c.LabelShortCap),
					new LookTargets(exec), MessageTypeDefOf.NegativeEvent, historical: false);
				return;
			}
			CaptiveUtility.MarkForExecution(c);
			Messages.Message("RimCourt.CaptiveNoExecutioner".Translate(c?.LabelShortCap ?? "?"),
				MessageTypeDefOf.NeutralEvent, historical: false);
		}

		public override void ApplyTimeout(Petition p, VerdictOption v, Pawn lord)
		{
			CourtManager.Instance?.NoteCaptiveTried(p.petitioner);
			Keep(p.petitioner);
		}

		private static void Keep(Pawn c)
		{
			CaptiveUtility.Ground(c);
			CaptiveUtility.SendBackToCell(c);
			if (c != null && !c.Dead)
				Messages.Message("RimCourt.CaptiveKept".Translate(c.LabelShortCap),
					new LookTargets(c), MessageTypeDefOf.NeutralEvent, historical: false);
		}

		private static void TellKillerFreed(Pawn captive, Pawn lord)
		{
			var map = captive?.MapHeld;
			if (map == null || lord == null || RimCourtDefOf.RimCourt_KillerFreed == null) return;
			var colonists = map.mapPawns.FreeColonistsSpawned;
			for (var i = 0; i < colonists.Count; i++)
			{
				var c = colonists[i];
				if (c == lord) continue;
				c.needs?.mood?.thoughts?.memories?.TryGainMemory(RimCourtDefOf.RimCourt_KillerFreed, lord);
			}
		}

		public override void Reactions(Petition p, VerdictOption v, out int petitionerMood, out int otherMood)
		{
			otherMood = 0;
			switch (v.outcome)
			{
				case "Release":
				case "Ransom":
					petitionerMood = 1;
					break;
				default:
					petitionerMood = -1;
					break;
			}
		}

		public override string CaseText(Petition p)
			=> def.caseText.Formatted(p.petitioner.Named("PETITIONER"),
				(p.petitioner.Faction?.Name ?? "?").Named("FACTION"),
				p.silver.Named("SILVER"),
				CaptiveUtility.DossierText(p.petitioner, CaptiveUtility.Decode(p.extra)).Named("DOSSIER"));

		public override string VerdictLabel(Petition p, int index)
			=> def.verdicts[index].label.Formatted(p.petitioner.Named("PETITIONER"),
				(p.petitioner.Faction?.Name ?? "?").Named("FACTION"), p.silver.Named("SILVER"));

		public override string PastVerdictLabel(Petition p, int index)
		{
			var v = def.verdicts[index];
			return v.pastLabel.NullOrEmpty()
				? VerdictLabel(p, index)
				: v.pastLabel.Formatted(p.petitioner.Named("PETITIONER"),
					(p.petitioner.Faction?.Name ?? "?").Named("FACTION"), p.silver.Named("SILVER"));
		}
	}
}
