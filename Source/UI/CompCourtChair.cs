using System.Collections.Generic;
using System.Linq;
using RimCourt.Court;
using RimWorld;
using Verse;

namespace RimCourt.UI
{
	public class CompProperties_CourtChair : CompProperties
	{
		public CompProperties_CourtChair() { compClass = typeof(CompCourtChair); }
	}

	public class CompCourtChair : ThingComp
	{
		private Pawn _guest;

		public Pawn Guest
			=> _guest != null && !_guest.Dead && !_guest.Discarded && _guest.Faction == Faction.OfPlayer ? _guest : null;

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_References.Look(ref _guest, "courtGuest");
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			if (parent.Faction != Faction.OfPlayer || CourtManager.CourtSeatForChair(parent) == null) yield break;
			var guest = Guest;
			yield return new Command_Action
			{
				defaultLabel = guest == null
					? "RimCourt.HonourSeatLabel".Translate()
					: "RimCourt.HonourSeatTaken".Translate(guest.LabelShort),
				defaultDesc = "RimCourt.HonourSeatDesc".Translate(),
				icon = RimCourtIcons.HonourSeat,
				action = () => Find.WindowStack.Add(new FloatMenu(Options())),
			};
		}

		private List<FloatMenuOption> Options()
		{
			var list = new List<FloatMenuOption>
			{
				new FloatMenuOption("RimCourt.HonourSeatNobody".Translate(), () => _guest = null),
			};
			var map = parent.Map;
			if (map == null) return list;
			foreach (var p in map.mapPawns.FreeColonistsSpawned.OrderBy(x => x.LabelShort))
			{
				var pawn = p;
				var elsewhere = SeatOf(pawn, map);
				var label = elsewhere != null && elsewhere != this
					? "RimCourt.HonourSeatElsewhere".Translate(pawn.LabelShortCap).ToString()
					: pawn.LabelShortCap;
				list.Add(new FloatMenuOption(label, () => Assign(pawn)));
			}
			return list;
		}

		private void Assign(Pawn pawn)
		{
			var map = parent.Map;
			if (map != null)
			{
				var old = SeatOf(pawn, map);
				if (old != null && old != this) old._guest = null;
				Building_GuardPost.Unpost(pawn, map);
			}
			_guest = pawn;
		}

		public static void Unseat(Pawn pawn, Map map)
		{
			if (pawn == null || map == null) return;
			var seat = SeatOf(pawn, map);
			if (seat != null) seat._guest = null;
		}

		private static CompCourtChair SeatOf(Pawn pawn, Map map)
		{
			var all = map.listerBuildings.allBuildingsColonist;
			for (var i = 0; i < all.Count; i++)
			{
				var comp = all[i].TryGetComp<CompCourtChair>();
				if (comp != null && comp._guest == pawn) return comp;
			}
			return null;
		}

		public override string CompInspectStringExtra()
		{
			var guest = Guest;
			return guest == null ? null : "RimCourt.HonourSeatInspect".Translate(guest.LabelShort).ToString();
		}
	}

	[StaticConstructorOnStartup]
	public static class CourtChairInjector
	{
		static CourtChairInjector()
		{
			foreach (var d in DefDatabase<ThingDef>.AllDefsListForReading)
			{
				if (d.building == null || !d.building.isSittable) continue;
				if (d.thingClass == null || !typeof(ThingWithComps).IsAssignableFrom(d.thingClass)) continue;
				if (d.comps == null) d.comps = new List<CompProperties>();
				if (d.comps.Any(c => c is CompProperties_CourtChair)) continue;
				d.comps.Add(new CompProperties_CourtChair());
			}
		}
	}
}
