using System.Collections.Generic;
using System.Linq;
using RimCourt.Court;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RimCourt
{
	public class IncidentWorker_Petitioner : IncidentWorker
	{
		private const int WaitDays = 3;
		private const int WaitTicks = WaitDays * 60000;

		protected override bool CanFireNowSub(IncidentParms parms)
		{
			if (!base.CanFireNowSub(parms)) return false;
			if (!(parms.target is Map map)) return false;
			if (!CourtReady(map)) return false;

			var mgr = CourtManager.Instance;
			if (mgr == null || mgr.Active || mgr.HasWaitingOutsider(map)) return false;

			return OutsiderDefs().Any()
				&& OutsiderUtility.EligibleFactionsInRandomOrder().Any()
				&& RCellFinder.TryFindRandomPawnEntryCell(out _, map, CellFinder.EdgeRoadChance_Friendly);
		}

		public override float BaseChanceThisGame
		{
			get
			{
				var mgr = CourtManager.Instance;
				if (mgr == null) return base.BaseChanceThisGame;
				var hall = HallUtility.VisitorFactorAnywhere();
				switch (mgr.Reputation.Stage)
				{
					case JudgeReputation.StageJust: return base.BaseChanceThisGame * 1.6f * hall;
					case JudgeReputation.StageHarsh: return base.BaseChanceThisGame * 0.5f * hall;
					case JudgeReputation.StageVenal: return base.BaseChanceThisGame * 0.7f * hall;
					default: return base.BaseChanceThisGame * hall;
				}
			}
		}

		private static bool CourtReady(Map map)
		{
			if (map.listerThings.ThingsOfDef(RimCourtDefOf.RimCourt_LordSeat).Count > 0) return true;
			var joustSeat = DefDatabase<ThingDef>.GetNamedSilentFail("RimJoust_LordSeat");
			return joustSeat != null && map.listerThings.ThingsOfDef(joustSeat).Count > 0;
		}

		private static IEnumerable<PetitionDef> OutsiderDefs()
			=> DefDatabase<PetitionDef>.AllDefsListForReading
				.Where(d => typeof(PetitionWorker_Outsider).IsAssignableFrom(d.workerClass)
					&& !typeof(PetitionWorker_Arbitration).IsAssignableFrom(d.workerClass)
					&& !typeof(PetitionWorker_Pardon).IsAssignableFrom(d.workerClass));

		protected override bool TryExecuteWorker(IncidentParms parms)
		{
			if (!(parms.target is Map map)) return false;
			var mgr = CourtManager.Instance;
			if (mgr == null) return false;
			if (!RCellFinder.TryFindRandomPawnEntryCell(out var entry, map, CellFinder.EdgeRoadChance_Friendly))
				return false;

			var def = OutsiderDefs().RandomElementWithFallback();
			if (def == null) return false;

			foreach (var faction in OutsiderUtility.EligibleFactionsInRandomOrder())
			{
				var pawn = OutsiderUtility.GeneratePetitioner(faction, map.Tile);
				if (pawn == null) continue;

				GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(entry, map, 4), map);

				try
				{
					OutsiderUtility.MakeUnstoppedLord(faction,
						new LordJob_VisitColony(faction, OutsiderUtility.WaitSpotFor(pawn, map), WaitTicks),
						map, new List<Pawn> { pawn });
				}
				catch (System.Exception e)
				{
					Log.Warning("[RimCourt] le pétitionnaire arrive sans escorte de comportement (" + e.Message + ").");
				}

				var deserter = EmpireDeserterUtility.Roll(mgr.LordFor(map));
				if (deserter) EmpireDeserterUtility.Arm(pawn);

				var silver = Mathf.Max(1, Mathf.RoundToInt(def.amountRange.RandomInRange / 5f) * 5);
				mgr.RegisterOutsider(new OutsiderRequest
				{
					pawn = pawn,
					def = def,
					faction = faction,
					silver = silver,
					expiryTick = Find.TickManager.TicksGame + WaitTicks,
					deserter = deserter,
				});

				SendStandardLetter(
					"RimCourt.PetitionerArrivesLabel".Translate(pawn.LabelShortCap),
					def.arrivalText.Formatted(pawn.Named("PETITIONER"), faction.Name.Named("FACTION"),
						silver.Named("SILVER"), WaitDays.Named("DAYS")),
					LetterDefOf.NeutralEvent, parms, pawn);
				return true;
			}
			return false;
		}
	}
}
