using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimCourt.UI
{
	public class Building_GuardPost : Building_CourtMarker
	{
		public const int MinGuardAge = 16;

		private Pawn _guard;

		public Pawn Guard
			=> _guard != null && !_guard.Dead && !_guard.Discarded && _guard.Faction == Faction.OfPlayer ? _guard : null;

		public static bool CanGuard(Pawn p)
		{
			if (p == null || p.Dead || !p.IsFreeColonist || p.WorkTagIsDisabled(WorkTags.Violent)) return false;
			return p.ageTracker == null || p.ageTracker.AgeBiologicalYears >= MinGuardAge;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_References.Look(ref _guard, "courtGuard");
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (var g in base.GetGizmos()) yield return g;
			if (Faction != Faction.OfPlayer) yield break;
			var guard = Guard;
			yield return new Command_Action
			{
				defaultLabel = guard == null
					? "RimCourt.GuardPostLabel".Translate()
					: "RimCourt.GuardPostTaken".Translate(guard.LabelShort),
				defaultDesc = "RimCourt.GuardPostDesc".Translate(),
				icon = RimCourtIcons.HonourSeat,
				action = () => Find.WindowStack.Add(new FloatMenu(Options())),
			};
		}

		private List<FloatMenuOption> Options()
		{
			var list = new List<FloatMenuOption>
			{
				new FloatMenuOption("RimCourt.GuardPostNobody".Translate(), () => _guard = null),
			};
			var map = Map;
			if (map == null) return list;
			foreach (var p in map.mapPawns.FreeColonistsSpawned.OrderBy(x => x.LabelShort))
			{
				if (!CanGuard(p)) continue;
				var pawn = p;
				var elsewhere = PostOf(pawn, map);
				var label = elsewhere != null && elsewhere != this
					? "RimCourt.GuardPostElsewhere".Translate(pawn.LabelShortCap).ToString()
					: pawn.LabelShortCap;
				list.Add(new FloatMenuOption(label, () => Assign(pawn)));
			}
			return list;
		}

		private void Assign(Pawn pawn)
		{
			var map = Map;
			if (map != null)
			{
				Unpost(pawn, map);
				CompCourtChair.Unseat(pawn, map);
			}
			_guard = pawn;
		}

		public static Building_GuardPost PostOf(Pawn pawn, Map map)
		{
			if (pawn == null || map == null || RimCourtDefOf.RimCourt_GuardPost == null) return null;
			var posts = map.listerThings.ThingsOfDef(RimCourtDefOf.RimCourt_GuardPost);
			for (var i = 0; i < posts.Count; i++)
				if (posts[i] is Building_GuardPost post && post._guard == pawn) return post;
			return null;
		}

		public static void Unpost(Pawn pawn, Map map)
		{
			var post = PostOf(pawn, map);
			if (post != null) post._guard = null;
		}

		public override string GetInspectString()
		{
			var s = base.GetInspectString();
			var guard = Guard;
			var line = guard == null
				? "RimCourt.GuardPostEmpty".Translate().ToString()
				: "RimCourt.GuardPostInspect".Translate(guard.LabelShort).ToString();
			return s.NullOrEmpty() ? line : s + "\n" + line;
		}
	}
}
