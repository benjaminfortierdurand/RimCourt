using System.Collections.Generic;
using RimCourt.Court;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimCourt.UI
{
	public class CompProperties_CourtArea : CompProperties
	{
		public CompProperties_CourtArea() { compClass = typeof(CompCourtArea); }
	}

	public class CompCourtArea : ThingComp
	{
		public const float DefRadius = 12f;
		public const float MaxReach = 30f;

		private float radius = DefRadius;
		private bool wholeRoom;
		private List<IntVec3> regionCells;
		private HashSet<IntVec3> regionSet;
		private List<IntVec3> drawCells;
		private float drawStamp = -999f;

		public bool HasRegion => regionCells != null && regionCells.Count > 0;

		private HashSet<IntVec3> Region
		{
			get
			{
				if (!HasRegion) return null;
				if (regionSet == null) regionSet = new HashSet<IntVec3>(regionCells);
				return regionSet;
			}
		}

		public static bool InCrowdArea(Thing seat, IntVec3 spot, IntVec3 cell)
		{
			var comp = seat?.TryGetComp<CompCourtArea>();
			if (comp == null) return cell.DistanceTo(spot) <= DefRadius;
			if (comp.HasRegion) return comp.Region.Contains(cell);
			var room = comp.Hall;
			if (room != null) return cell.GetRoom(seat.Map) == room;
			return cell.DistanceTo(spot) <= comp.radius;
		}

		private Room Hall
		{
			get
			{
				if (!wholeRoom || !parent.Spawned) return null;
				var room = parent.Position.GetRoom(parent.Map);
				return room != null && !room.PsychologicallyOutdoors ? room : null;
			}
		}

		private IntVec3 Spot => CourtManager.AudienceCellFor(parent.Position, parent.Rotation, parent.Map);

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref radius, "crowdRadius", DefRadius);
			Scribe_Values.Look(ref wholeRoom, "crowdWholeRoom");
			Scribe_Collections.Look(ref regionCells, "crowdCells", LookMode.Value);
			if (Scribe.mode == LoadSaveMode.PostLoadInit) regionSet = null;
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			yield return new Designator_CourtArea(this, add: true);
			yield return new Designator_CourtArea(this, add: false);
		}

		public bool InReach(IntVec3 c) => parent.Spawned && c.DistanceTo(parent.Position) <= MaxReach + 0.5f;

		public bool InArea(IntVec3 c) => parent.Spawned && InCrowdArea(parent, Spot, c);

		private List<IntVec3> CurrentCells()
		{
			if (HasRegion) return new List<IntVec3>(regionCells);
			var list = new List<IntVec3>();
			if (!parent.Spawned) return list;
			var room = Hall;
			if (room != null)
			{
				list.AddRange(room.Cells);
				return list;
			}
			var spot = Spot;
			var center = spot.IsValid ? spot : parent.Position;
			var n = GenRadial.NumCellsInRadius(radius);
			for (var i = 0; i < n; i++)
			{
				var c = center + GenRadial.RadialPattern[i];
				if (c.InBounds(parent.Map)) list.Add(c);
			}
			return list;
		}

		public void Paint(List<IntVec3> cells, bool add)
		{
			if (!parent.Spawned || cells == null) return;
			var set = HasRegion ? new HashSet<IntVec3>(regionCells)
				: add ? new HashSet<IntVec3>()
				: new HashSet<IntVec3>(CurrentCells());
			for (var i = 0; i < cells.Count; i++)
			{
				if (add) set.Add(cells[i]);
				else set.Remove(cells[i]);
			}
			regionCells = set.Count > 0 ? new List<IntVec3>(set) : null;
			regionSet = null;
			wholeRoom = false;
			drawStamp = -999f;
		}

		public void DrawArea()
		{
			if (!parent.Spawned) return;
			if (drawCells == null || Time.realtimeSinceStartup - drawStamp > 1f)
			{
				drawCells = CurrentCells();
				drawStamp = Time.realtimeSinceStartup;
			}
			GenDraw.DrawFieldEdges(drawCells);
		}

		public override void PostDrawExtraSelectionOverlays()
		{
			base.PostDrawExtraSelectionOverlays();
			DrawArea();
		}

		public override string CompInspectStringExtra()
		{
			if (HasRegion) return "RimCourt.CrowdAreaPainted".Translate(regionCells.Count);
			return Hall != null
				? "RimCourt.CrowdAreaRoom".Translate()
				: "RimCourt.CrowdAreaRadius".Translate(radius.ToString("F0"));
		}
	}
}
