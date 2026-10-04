using System.Collections.Generic;
using System.Linq;
using RimCourt.Court;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RimCourt
{
	public class IncidentWorker_Arbitration : IncidentWorker
	{
		private const int WaitDays = 3;
		private const int WaitTicks = WaitDays * 60000;

		protected override bool CanFireNowSub(IncidentParms parms)
		{
			if (!base.CanFireNowSub(parms)) return false;
			if (!(parms.target is Map map)) return false;
			if (map.listerThings.ThingsOfDef(RimCourtDefOf.RimCourt_LordSeat).Count == 0
				&& DefDatabase<ThingDef>.GetNamedSilentFail("RimJoust_LordSeat") is ThingDef joust
				&& map.listerThings.ThingsOfDef(joust).Count == 0)
				return false;

			var mgr = CourtManager.Instance;
			if (mgr == null || mgr.Active || mgr.HasWaitingOutsider(map)) return false;
			if (TheDef == null) return false;

			return OutsiderUtility.EligibleFactionsInRandomOrder().Take(2).Count() >= 2
				&& RCellFinder.TryFindRandomPawnEntryCell(out _, map, CellFinder.EdgeRoadChance_Friendly);
		}

		private static PetitionDef TheDef
			=> DefDatabase<PetitionDef>.GetNamedSilentFail("RimCourt_Arbitration");

		public override float BaseChanceThisGame
		{
			get
			{
				var mgr = CourtManager.Instance;
				if (mgr == null) return base.BaseChanceThisGame;
				var hall = HallUtility.VisitorFactorAnywhere();
				switch (mgr.Reputation.Stage)
				{
					case JudgeReputation.StageJust: return base.BaseChanceThisGame * 2f * hall;
					case JudgeReputation.StageHarsh: return base.BaseChanceThisGame * 0.6f * hall;
					case JudgeReputation.StageVenal: return base.BaseChanceThisGame * 0.4f * hall;
					case JudgeReputation.StageWeak: return base.BaseChanceThisGame * 0.2f * hall;
					default: return base.BaseChanceThisGame * 0.5f * hall;
				}
			}
		}

		protected override bool TryExecuteWorker(IncidentParms parms)
		{
			if (!(parms.target is Map map)) return false;
			var mgr = CourtManager.Instance;
			var def = TheDef;
			if (mgr == null || def == null) return false;
			if (!RCellFinder.TryFindRandomPawnEntryCell(out var entry, map, CellFinder.EdgeRoadChance_Friendly))
				return false;

			var factions = OutsiderUtility.EligibleFactionsInRandomOrder().Take(2).ToList();
			if (factions.Count < 2) return false;

			var first = Send(factions[0], map, entry);
			if (first == null) return false;
			var second = Send(factions[1], map, entry);
			if (second == null)
			{
				OutsiderUtility.SendHome(first);
				return false;
			}

			var silver = Mathf.Max(1, Mathf.RoundToInt(def.amountRange.RandomInRange / 5f) * 5);
			var quarrel = ("RimCourt.Quarrel" + Rand.RangeInclusive(0, 2)).Translate().ToString();

			mgr.RegisterOutsider(new OutsiderRequest
			{
				pawn = first,
				otherPawn = second,
				def = def,
				faction = factions[0],
				otherFaction = factions[1],
				silver = silver,
				extra = quarrel,
				expiryTick = Find.TickManager.TicksGame + WaitTicks,
			});

			SendStandardLetter(
				"RimCourt.ArbitrationArrivesLabel".Translate(),
				def.arrivalText.Formatted(first.Named("PETITIONER"), second.Named("OTHER"),
					factions[0].Name.Named("FIRST"), factions[1].Name.Named("SECOND"),
					quarrel.Named("QUARREL"), silver.Named("SILVER"), WaitDays.Named("DAYS")),
				LetterDefOf.NeutralEvent, parms, first);
			return true;
		}

		private static Pawn Send(Faction faction, Map map, IntVec3 entry)
		{
			var pawn = OutsiderUtility.GeneratePetitioner(faction, map.Tile);
			if (pawn == null) return null;
			GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(entry, map, 5), map);
			try
			{
				OutsiderUtility.MakeUnstoppedLord(faction,
					new LordJob_VisitColony(faction, OutsiderUtility.WaitSpotFor(pawn, map), WaitTicks),
					map, new List<Pawn> { pawn });
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] l'envoyé arrive sans escorte de comportement (" + e.Message + ").");
			}
			return pawn;
		}
	}
}
