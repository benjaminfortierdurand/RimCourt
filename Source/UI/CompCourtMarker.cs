using System.Collections.Generic;
using RimCourt.Court;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimCourt.UI
{
	public class CompProperties_CourtMarker : CompProperties
	{
		public CompProperties_CourtMarker() { compClass = typeof(CompCourtMarker); }
	}

	public class CompCourtMarker : ThingComp
	{
		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			var mgr = Court.CourtManager.Instance;
			if (mgr == null) yield break;

			yield return new Command_Action
			{
				defaultLabel = "RimCourt.GuideGizmoLabel".Translate(),
				defaultDesc = "RimCourt.GuideGizmoDesc".Translate(),
				icon = RimCourtIcons.Guide,
				action = Window_CourtGuide.Open,
			};

			var seat = CourtManager.SeatOf(parent);

			if (parent.Map != null)
			{
				yield return new Command_Action
				{
					defaultLabel = "RimCourt.StandingLabel".Translate(),
					defaultDesc = "RimCourt.StandingDesc".Translate(),
					icon = RimCourtIcons.Standing,
					action = () => Window_CourtStanding.Open(seat, parent.Map),
				};
			}

			if (!mgr.Active && parent.Map != null)
			{
				var map = parent.Map;
				var owner = mgr.SeatOwner(map, seat);
				var name = new Command_Action
				{
					defaultLabel = "RimCourt.NameLordLabel".Translate(),
					defaultDesc = "RimCourt.NameLordDesc".Translate(),
					icon = RimCourtIcons.NameLord,
					action = () => Find.WindowStack.Add(new Dialog_PickLord(
						mgr.LordCandidates(map), mgr.LordFor(map, seat),
						mgr.LordAutomatic(map, seat), owner,
						picked => mgr.SetAppointedLord(picked, map, seat))),
				};
				if (owner != null && mgr.AppointedLord == null)
					name.Disable("RimCourt.NameLordOnThrone".Translate(owner.LabelShortCap));
				yield return name;

				yield return new Command_Action
				{
					defaultLabel = "RimCourt.NameHandLabel".Translate(),
					defaultDesc = "RimCourt.NameHandDesc".Translate(),
					icon = RimCourtIcons.Hand,
					action = () => Find.WindowStack.Add(new Dialog_PickLord(
						mgr.LordCandidates(map), mgr.Hand, null, null,
						picked => mgr.SetHand(picked),
						"RimCourt.PickHandTitle".Translate().ToString(),
						"RimCourt.PickHandDesc".Translate().ToString(),
						"RimCourt.PickHandNone".Translate().ToString())),
				};
			}

			var pending = mgr.PendingVerdict;
			if (pending != null)
			{
				yield return new Command_Action
				{
					defaultLabel = "RimCourt.ReopenSheetLabel".Translate(),
					defaultDesc = "RimCourt.ReopenSheetDesc".Translate(),
					icon = RimCourtIcons.HoldCourt,
					action = () => Dialog_PetitionSheet.OpenFor(pending.id),
				};
			}

			if (mgr.Active)
			{
				yield return new Command_Action
				{
					defaultLabel = "RimCourt.CancelCourtLabel".Translate(),
					defaultDesc = "RimCourt.CancelCourtDesc".Translate(),
					icon = RimCourtIcons.Cancel,
					action = () => Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
						"RimCourt.CancelCourtConfirm".Translate(),
						() => mgr.CancelCourt(),
						destructive: true)),
				};
			}
			else
			{
				var convene = new Command_Action
				{
					defaultLabel = "RimCourt.HoldCourtLabel".Translate(),
					defaultDesc = "RimCourt.HoldCourtDesc".Translate(),
					icon = RimCourtIcons.HoldCourt,
					action = () => mgr.TryConvene(parent),
				};
				if (mgr.OnCooldown)
				{
					var wait = Mathf.CeilToInt(mgr.DaysUntilCourt);
					convene.Disabled = true;
					convene.disabledReason =
						(wait == 1 ? "RimCourt.CourtCooldownGizmoOne" : "RimCourt.CourtCooldownGizmo").Translate(wait);
				}
				yield return convene;
			}
		}

		public override string CompInspectStringExtra()
		{
			var line = Court.CourtManager.Instance?.InspectLine();
			if (parent.def != RimCourtDefOf.RimCourt_LordSeat || parent.Map == null) return line;
			var cell = Court.CourtManager.AudienceCellFor(parent.Position, parent.Rotation, parent.Map);
			var where = cell.IsValid
				? "RimCourt.PetitionersStandHere".Translate(cell.DistanceTo(parent.Position).ToString("F0"))
				: "RimCourt.NoAudienceRoom".Translate();
			var hall = "RimCourt.HallInspect".Translate(
				Court.HallUtility.Label(Court.HallUtility.GradeAt(parent))).ToString();
			var tail = where + "\n" + hall;
			return line.NullOrEmpty() ? tail : line + "\n" + tail;
		}
	}
}
