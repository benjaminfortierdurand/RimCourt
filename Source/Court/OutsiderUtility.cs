using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimCourt.Court
{
	public static class OutsiderUtility
	{
		public static IEnumerable<Faction> EligibleFactionsInRandomOrder()
		{
			return Find.FactionManager.AllFactionsVisible
				.Where(f => !f.IsPlayer && !f.defeated && !f.def.hidden
					&& !f.HostileTo(Faction.OfPlayer) && GetHumanlikeKind(f) != null)
				.InRandomOrder();
		}

		public static PawnKindDef GetHumanlikeKind(Faction f)
		{
			PawnKindDef best = null;
			var bestPower = float.MaxValue;
			if (f.def.pawnGroupMakers != null)
				foreach (var g in f.def.pawnGroupMakers)
				{
					if (g.options == null) continue;
					foreach (var o in g.options)
					{
						var k = o.kind;
						if (!IsHumanlike(k) || k.factionLeader) continue;
						if (k.combatPower < bestPower) { bestPower = k.combatPower; best = k; }
					}
				}
			if (best != null) return best;
			return IsHumanlike(f.def.basicMemberKind) ? f.def.basicMemberKind : null;
		}

		private static bool IsHumanlike(PawnKindDef k) => k?.race?.race?.Humanlike ?? false;

		public static Pawn GeneratePetitioner(Faction faction, int tile)
		{
			var kind = GetHumanlikeKind(faction);
			if (kind == null) return null;

			var req = new PawnGenerationRequest(
				kind, faction, PawnGenerationContext.NonPlayer,
				tile: tile,
				forceGenerateNewPawn: true,
				canGeneratePawnRelations: false,
				developmentalStages: DevelopmentalStage.Adult);
			var p = PawnGenerator.GeneratePawn(req);
			if (p == null) return null;

			if (!CanRenderSilhouette(p))
			{
				if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
				Find.WorldPawns.PassToWorld(p, RimWorld.Planet.PawnDiscardDecideMode.Discard);
				return null;
			}
			return p;
		}

		private static bool CanRenderSilhouette(Pawn p)
		{
			if (p.RaceProps == null || !p.RaceProps.Humanlike) return true;
			try
			{
				return p.ageTracker?.CurLifeStage?.silhouetteGraphicData?.Graphic != null;
			}
			catch
			{
				return false;
			}
		}

		private static System.Reflection.FieldInfo _tollSuppress;
		private static bool _tollLookedUp;

		private static System.Reflection.FieldInfo TollSuppressField()
		{
			if (_tollLookedUp) return _tollSuppress;
			_tollLookedUp = true;
			var type = HarmonyLib.AccessTools.TypeByName("RimToll.GameComponent_TollState");
			_tollSuppress = type == null ? null : HarmonyLib.AccessTools.Field(type, "SuppressPatch");
			return _tollSuppress;
		}

		public static Lord MakeUnstoppedLord(Faction faction, LordJob job, Map map, List<Pawn> pawns)
		{
			var field = TollSuppressField();
			var previous = false;
			try
			{
				if (field != null)
				{
					previous = (bool)field.GetValue(null);
					field.SetValue(null, true);
				}
				return LordMaker.MakeNewLord(faction, job, map, pawns);
			}
			finally
			{
				if (field != null) field.SetValue(null, previous);
			}
		}

		public static void ReleaseFromLord(Pawn pawn)
		{
			pawn?.GetLord()?.Notify_PawnLost(pawn, PawnLostCondition.ForcedByPlayerAction);
		}

		public static IntVec3 WaitSpotFor(Pawn pawn, Map map)
		{
			var seat = CourtManager.AnySeatOn(map);
			if (seat != null && pawn != null)
			{
				var near = CellFinder.RandomClosewalkCellNear(seat.Position, map, 7);
				if (near.IsValid && near.Standable(map)
					&& pawn.CanReach(near, PathEndMode.OnCell, Danger.Deadly))
					return near;
			}
			if (pawn != null && RCellFinder.TryFindRandomSpotJustOutsideColony(pawn.Position, map, out var outside))
				return outside;
			return pawn?.Position ?? map.Center;
		}

		public static void ResumeWaiting(OutsiderRequest req, Map map)
		{
			ResumeOne(req, req?.pawn, map);
			ResumeOne(req, req?.otherPawn, map);
		}

		private static void ResumeOne(OutsiderRequest req, Pawn pawn, Map map)
		{
			if (pawn == null || pawn.Dead || !pawn.Spawned || map == null) return;
			if (pawn.Faction == null || pawn.Faction.IsPlayer) return;
			if (pawn.GetLord() != null) return;
			var left = req.expiryTick - Find.TickManager.TicksGame;
			if (left <= 0) { SendHome(pawn); return; }
			try
			{
				MakeUnstoppedLord(pawn.Faction,
					new LordJob_VisitColony(pawn.Faction, WaitSpotFor(pawn, map), left), map,
					new List<Pawn> { pawn });
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] " + pawn.LabelShort + " reste sans conduite après l'audience ("
					+ e.Message + ").");
			}
		}

		public static void SendHome(Pawn pawn)
		{
			if (pawn == null || pawn.Dead || !pawn.Spawned) return;
			ReleaseFromLord(pawn);
			try
			{
				MakeUnstoppedLord(pawn.Faction,
					new LordJob_ExitMapBest(LocomotionUrgency.Walk, canDig: false),
					pawn.Map, new List<Pawn> { pawn });
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] le pétitionnaire ne trouve pas la sortie (" + e.Message + ").");
			}
		}

		public static bool TakeIntoColony(Pawn pawn)
		{
			if (pawn == null || pawn.Dead || !pawn.Spawned) return false;
			ReleaseFromLord(pawn);
			try
			{
				pawn.SetFaction(Faction.OfPlayer);
				pawn.guest?.SetGuestStatus(null);
				return true;
			}
			catch (System.Exception e)
			{
				Log.Error("[RimCourt] impossible d'accueillir le pétitionnaire : " + e);
				return false;
			}
		}

		public static int SilverAvailable(Map map)
		{
			if (map == null) return 0;
			var total = 0;
			var silver = map.listerThings.ThingsOfDef(ThingDefOf.Silver);
			for (var i = 0; i < silver.Count; i++)
			{
				var t = silver[i];
				if (t.Spawned && (map.areaManager.Home[t.Position] || t.IsInAnyStorage()))
					total += t.stackCount;
			}
			return total;
		}

		public static bool TryPaySilver(Map map, int amount)
		{
			if (map == null || amount <= 0) return amount <= 0;
			if (SilverAvailable(map) < amount) return false;
			var silver = map.listerThings.ThingsOfDef(ThingDefOf.Silver)
				.Where(t => t.Spawned && (map.areaManager.Home[t.Position] || t.IsInAnyStorage()))
				.ToList();
			foreach (var t in silver)
			{
				var take = Mathf.Min(t.stackCount, amount);
				t.SplitOff(take).Destroy();
				amount -= take;
				if (amount <= 0) return true;
			}
			return false;
		}

		public static int FoodAvailable(Map map)
		{
			if (map == null) return 0;
			var total = 0;
			foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree))
			{
				if (t is Pawn || t.def?.ingestible == null) continue;
				if (!t.def.IsNutritionGivingIngestible) continue;
				if (t.def.ingestible.preferability < FoodPreferability.RawBad) continue;
				if (!t.Spawned || !(map.areaManager.Home[t.Position] || t.IsInAnyStorage())) continue;
				total += t.stackCount;
			}
			return total;
		}

		public static bool TryTakeFood(Map map, int amount)
		{
			if (map == null || amount <= 0) return amount <= 0;
			if (FoodAvailable(map) < amount) return false;
			var stock = new List<Thing>();
			foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree))
			{
				if (t is Pawn || t.def?.ingestible == null) continue;
				if (!t.def.IsNutritionGivingIngestible) continue;
				if (t.def.ingestible.preferability < FoodPreferability.RawBad) continue;
				if (!t.Spawned || !(map.areaManager.Home[t.Position] || t.IsInAnyStorage())) continue;
				stock.Add(t);
			}
			stock.SortBy(t => t.MarketValue);
			foreach (var t in stock)
			{
				var take = Mathf.Min(t.stackCount, amount);
				t.SplitOff(take).Destroy();
				amount -= take;
				if (amount <= 0) return true;
			}
			return false;
		}

		public static bool TryTakeWeapons(Map map, int count)
		{
			if (map == null || count <= 0) return count <= 0;
			var stock = new List<Thing>();
			foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon))
			{
				if (!t.Spawned || t.def == null) continue;
				if (!(map.areaManager.Home[t.Position] || t.IsInAnyStorage())) continue;
				if (t.def.IsApparel || t.ParentHolder is Pawn_EquipmentTracker) continue;
				stock.Add(t);
			}
			if (stock.Count < count) return false;
			stock.SortBy(t => t.MarketValue);
			var taken = 0;
			foreach (var t in stock)
			{
				t.Destroy();
				taken++;
				if (taken >= count) return true;
			}
			return false;
		}

		public static void DropSilver(IntVec3 cell, Map map, int amount)
		{
			if (map == null || amount <= 0) return;
			if (!cell.IsValid || !cell.InBounds(map)) cell = map.Center;
			var rest = amount;
			while (rest > 0)
			{
				var coin = ThingMaker.MakeThing(ThingDefOf.Silver);
				coin.stackCount = Mathf.Min(coin.def.stackLimit, rest);
				rest -= coin.stackCount;
				GenPlace.TryPlaceThing(coin, cell, map, ThingPlaceMode.Near);
			}
		}

		public static void AffectGoodwill(Faction faction, int delta)
		{
			if (faction == null || delta == 0 || faction.IsPlayer || faction.defeated) return;
			try
			{
				faction.TryAffectGoodwillWith(Faction.OfPlayer, delta, canSendMessage: true,
					canSendHostilityLetter: true);
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] goodwill non appliqué (" + e.Message + ").");
			}
		}
	}
}
