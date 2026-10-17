using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimCourt.UI
{
	public class Building_CourtMarker : Building
	{
		private bool _hidden;

		public override void Print(SectionLayer layer)
		{
			if (!_hidden) base.Print(layer);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref _hidden, "markerHidden");
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (var g in base.GetGizmos()) yield return g;
			if (Faction != Faction.OfPlayer) yield break;
			yield return new Command_Toggle
			{
				defaultLabel = "RimCourt.ShowMarkerLabel".Translate(),
				defaultDesc = "RimCourt.ShowMarkerDesc".Translate(),
				icon = RimCourtIcons.ShowMarker,
				isActive = () => !_hidden,
				toggleAction = () =>
				{
					_hidden = !_hidden;
					if (Spawned) Map.mapDrawer.MapMeshDirty(Position, MapMeshFlagDefOf.Things);
				},
			};
		}

		public override string GetInspectString()
		{
			var s = base.GetInspectString();
			if (!_hidden) return s;
			var line = "RimCourt.MarkerHidden".Translate().ToString();
			return s.NullOrEmpty() ? line : s + "\n" + line;
		}
	}
}
