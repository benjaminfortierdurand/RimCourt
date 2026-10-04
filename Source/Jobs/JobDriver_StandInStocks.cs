using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RimCourt.Jobs
{
	public class JobDriver_StandInStocks : JobDriver
	{
		private Court.Building_Stocks Stocks => job.targetA.Thing as Court.Building_Stocks;

		public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDespawnedOrNull(TargetIndex.A);
			this.FailOn(() => Stocks == null || Stocks.Occupant != pawn);

			var follow = new Toil { defaultCompleteMode = ToilCompleteMode.Never };
			follow.initAction = () =>
			{
				var s = Stocks;
				if (s == null || s.Escort == null || s.Tied) ReadyForNextToil();
			};
			follow.tickAction = () =>
			{
				var s = Stocks;
				var e = s?.Escort;
				if (s == null || e == null || e.Dead || !e.Spawned || s.Tied
					|| e.CurJobDef != RimCourtDefOf.RimCourt_EscortToStocks
					|| e.Position.AdjacentTo8WayOrInside(s.Position))
				{
					ReadyForNextToil();
					return;
				}
				if (pawn.Position.DistanceTo(e.Position) > 2f)
				{
					if (pawn.pather == null || !pawn.pather.Moving || pawn.pather.Destination.Cell != e.Position)
						pawn.pather?.StartPath(e.Position, PathEndMode.Touch);
				}
				else if (pawn.pather != null && pawn.pather.Moving)
				{
					pawn.pather.StopDead();
				}
			};
			yield return follow;

			yield return Toils_Goto.GotoCell(Stocks.StandCell, PathEndMode.OnCell);

			var stand = new Toil { defaultCompleteMode = ToilCompleteMode.Never, handlingFacing = true };
			stand.initAction = () => pawn.pather?.StopDead();
			stand.tickAction = () =>
			{
				var s = Stocks;
				if (s == null) { ReadyForNextToil(); return; }
				if (pawn.Position != s.StandCell) { pawn.pather?.StartPath(s.StandCell, PathEndMode.OnCell); return; }
				pawn.rotationTracker?.FaceCell(s.StandCell + IntVec3.South);
			};
			yield return stand;
		}
	}
}
