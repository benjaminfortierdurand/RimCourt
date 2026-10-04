using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimCourt.UI
{
	public class PlaceWorker_CourtLayout : PlaceWorker
	{
		private static readonly List<IntVec3> OneCell = new List<IntVec3> { IntVec3.Invalid };

		public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
		{
			var map = Find.CurrentMap;
			if (map == null) return;
			var cell = Court.CourtManager.AudienceCellFor(center, rot, map);
			if (!cell.IsValid) return;
			OneCell[0] = cell;
			GenDraw.DrawFieldEdges(OneCell, Color.white);
		}
	}
}
