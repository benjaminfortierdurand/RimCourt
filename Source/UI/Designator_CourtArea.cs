using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimCourt.UI
{
	public class Designator_CourtArea : Designator_Cells
	{
		private const float BadgeSize = 0.46f;

		private readonly CompCourtArea area;
		private readonly bool add;

		public override bool DragDrawMeasurements => true;

		public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.Areas;

		public Designator_CourtArea(CompCourtArea area, bool add)
		{
			this.area = area;
			this.add = add;
			defaultLabel = (add ? "RimCourt.AreaPaint" : "RimCourt.AreaErase").Translate();
			defaultDesc = (add ? "RimCourt.AreaPaintDesc" : "RimCourt.AreaEraseDesc").Translate();
			icon = add ? RimCourtIcons.AreaExpand : RimCourtIcons.AreaClear;
			useMouseIcon = true;
			soundDragSustain = add ? SoundDefOf.Designate_DragAreaAdd : SoundDefOf.Designate_DragAreaDelete;
			soundDragChanged = add ? SoundDefOf.Designate_DragZone_Changed : null;
			soundSucceeded = add ? SoundDefOf.Designate_ZoneAdd_AllowedArea : SoundDefOf.Designate_ZoneDelete;
		}

		private bool AreaOk => area != null && area.parent.Spawned && area.parent.Map == Map;

		public override AcceptanceReport CanDesignateCell(IntVec3 c)
		{
			if (!AreaOk || !c.InBounds(Map)) return false;
			if (add)
			{
				if (c.Fogged(Map)) return false;
				if (!area.InReach(c)) return "RimCourt.AreaTooFar".Translate(Mathf.RoundToInt(CompCourtArea.MaxReach));
				return !area.HasRegion || !area.InArea(c);
			}
			return area.InArea(c);
		}

		public override void DesignateMultiCell(IEnumerable<IntVec3> cells)
		{
			var picked = new List<IntVec3>();
			foreach (var c in cells)
				if (CanDesignateCell(c).Accepted) picked.Add(c);
			if (picked.Count > 0) area.Paint(picked, add);
			Finalize(picked.Count > 0);
		}

		public override void DesignateSingleCell(IntVec3 c)
		{
			area.Paint(new List<IntVec3> { c }, add);
		}

		public override bool CanRemainSelected() => AreaOk;

		public override void SelectedUpdate()
		{
			GenUI.RenderMouseoverBracket();
			if (AreaOk) area.DrawArea();
		}

		public override void DrawIcon(Rect rect, Material buttonMat, GizmoRenderParms parms)
		{
			base.DrawIcon(rect, buttonMat, parms);
			if (RimCourtIcons.AreaBadge == null) return;
			var size = rect.width * BadgeSize;
			var spot = new Rect(rect.xMax - size - rect.width * 0.04f, rect.yMax - size - rect.height * 0.04f, size, size);
			GUI.color = parms.lowLight ? new Color(1f, 1f, 1f, 0.6f) : Color.white;
			GUI.DrawTexture(spot, RimCourtIcons.AreaBadge);
			GUI.DrawTexture(spot.ContractedBy(size * 0.16f), RimCourtIcons.HoldCourtGlyph, ScaleMode.ScaleToFit);
			GUI.color = Color.white;
		}
	}
}
