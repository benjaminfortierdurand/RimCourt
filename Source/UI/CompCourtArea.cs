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
		public const float MinRadius = 6f;
		public const float MaxRadius = 30f;
		public const float DefRadius = 12f;

		private float radius = DefRadius;
		private bool wholeRoom;
		private List<IntVec3> roomCells;
		private float drawStamp = -999f;

		public static bool InCrowdArea(Thing seat, IntVec3 spot, IntVec3 cell)
		{
			var comp = seat?.TryGetComp<CompCourtArea>();
			if (comp == null) return cell.DistanceTo(spot) <= DefRadius;
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

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref radius, "crowdRadius", DefRadius);
			Scribe_Values.Look(ref wholeRoom, "crowdWholeRoom");
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			yield return new Command_Action
			{
				defaultLabel = "RimCourt.RadiusPlus".Translate(radius.ToString("F0")),
				defaultDesc = "RimCourt.RadiusDesc".Translate(),
				icon = TexButton.Plus,
				action = () => SetRadius(radius + 1f),
			};
			yield return new Command_Action
			{
				defaultLabel = "RimCourt.RadiusMinus".Translate(radius.ToString("F0")),
				defaultDesc = "RimCourt.RadiusDesc".Translate(),
				icon = TexButton.Minus,
				action = () => SetRadius(radius - 1f),
			};
			yield return new Command_Action
			{
				defaultLabel = "RimCourt.RadiusAuto".Translate(),
				defaultDesc = "RimCourt.RadiusAutoDesc".Translate(),
				icon = TexButton.AutoRebuild,
				action = FitHall,
			};
		}

		private void SetRadius(float r)
		{
			radius = Mathf.Clamp(r, MinRadius, MaxRadius);
			wholeRoom = false;
			drawStamp = -999f;
		}

		private void FitHall()
		{
			var room = parent.Spawned ? parent.Position.GetRoom(parent.Map) : null;
			if (room == null || room.PsychologicallyOutdoors)
			{
				Messages.Message("RimCourt.RadiusNotEnclosed".Translate(), parent,
					MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			wholeRoom = true;
			drawStamp = -999f;
			Messages.Message("RimCourt.RadiusFitted".Translate(room.CellCount), parent,
				MessageTypeDefOf.NeutralEvent, historical: false);
		}

		public override void PostDrawExtraSelectionOverlays()
		{
			base.PostDrawExtraSelectionOverlays();
			if (!parent.Spawned) return;
			var room = Hall;
			if (room != null)
			{
				if (roomCells == null || Time.realtimeSinceStartup - drawStamp > 1f)
				{
					roomCells = new List<IntVec3>(room.Cells);
					drawStamp = Time.realtimeSinceStartup;
				}
				GenDraw.DrawFieldEdges(roomCells);
				return;
			}
			var spot = CourtManager.AudienceCellFor(parent.Position, parent.Rotation, parent.Map);
			GenDraw.DrawRadiusRing(spot.IsValid ? spot : parent.Position, radius);
		}

		public override string CompInspectStringExtra()
		{
			return Hall != null
				? "RimCourt.CrowdAreaRoom".Translate()
				: "RimCourt.CrowdAreaRadius".Translate(radius.ToString("F0"));
		}
	}
}
