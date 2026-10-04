using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RimCourt.Jobs
{
	public class JobDriver_HoldCourt : JobDriver
	{
		private IntVec3 FocusCell => job.targetB.Cell;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed: false);
			return true;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOn(() => Court.CourtManager.Instance == null || !Court.CourtManager.Instance.Active);
			if (job.targetA.HasThing) this.FailOnDespawnedOrNull(TargetIndex.A);

			yield return job.targetA.HasThing
				? Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.OnCell)
				: Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

			var sit = new Toil { defaultCompleteMode = ToilCompleteMode.Never };
			sit.handlingFacing = true;
			sit.initAction = () => pawn.pather?.StopDead();
			sit.tickAction = () => pawn.rotationTracker.FaceCell(FocusCell);
			yield return sit;
		}
	}
}
