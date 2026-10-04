using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimCourt.Jobs
{
	public class JoyGiver_JeerAtStocks : JoyGiver
	{
		private const int MinOpinionToSpare = 20;
		private const float KinImportance = 100f;
		private const int Radius = 4;

		public override float GetChance(Pawn pawn) => FindPost(pawn) == null ? 0f : base.GetChance(pawn);

		public override Job TryGiveJob(Pawn pawn)
		{
			var post = FindPost(pawn);
			if (post == null || !TryFindCell(post, pawn, out var cell)) return null;
			return JobMaker.MakeJob(def.jobDef, post, cell);
		}

		private static Court.Building_Stocks FindPost(Pawn pawn)
		{
			var map = pawn?.Map;
			if (map == null || !pawn.IsFreeColonist || pawn.interactions == null) return null;
			var list = map.listerThings.ThingsOfDef(RimCourtDefOf.RimCourt_Stocks);
			Court.Building_Stocks best = null;
			var bestD = float.MaxValue;
			for (var i = 0; i < list.Count; i++)
			{
				if (!(list[i] is Court.Building_Stocks st) || !st.Tied || st.Occupant == null) continue;
				var v = st.Occupant;
				if (v == pawn || !v.Spawned || v.Position != st.StandCell) continue;
				if (st.IsForbidden(pawn) || !WouldJeer(pawn, v)) continue;
				if (!pawn.CanReach(st, PathEndMode.Touch, Danger.None)) continue;
				var d = st.Position.DistanceToSquared(pawn.Position);
				if (d < bestD) { bestD = d; best = st; }
			}
			return best;
		}

		private static bool WouldJeer(Pawn pawn, Pawn victim)
		{
			if (pawn.relations == null) return true;
			if (pawn.relations.OpinionOf(victim) >= MinOpinionToSpare) return false;
			var rel = pawn.GetMostImportantRelation(victim);
			return rel == null || rel.importance < KinImportance;
		}

		private static bool TryFindCell(Court.Building_Stocks post, Pawn pawn, out IntVec3 cell)
		{
			var map = post.Map;
			var origin = post.Position;
			var candidates = new List<IntVec3>();
			foreach (var c in GenRadial.RadialCellsAround(origin, Radius, false))
			{
				if (!c.InBounds(map) || !c.Standable(map) || c == post.StandCell) continue;
				if (c.DistanceToSquared(origin) < 2) continue;
				if (!GenSight.LineOfSight(c, origin, map, true)) continue;
				if (!pawn.CanReserveSittableOrSpot(c) || !pawn.CanReach(c, PathEndMode.OnCell, Danger.None)) continue;
				candidates.Add(c);
			}
			if (candidates.Count == 0) { cell = IntVec3.Invalid; return false; }
			cell = candidates.RandomElementByWeight(c => c.z < origin.z ? 3f : 1f);
			return true;
		}
	}
}
