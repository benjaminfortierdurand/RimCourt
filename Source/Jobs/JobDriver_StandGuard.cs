using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RimCourt.Jobs
{
	public class JobDriver_StandGuard : JobDriver
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

			yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

			var watch = new Toil { defaultCompleteMode = ToilCompleteMode.Never };
			watch.handlingFacing = true;
			watch.initAction = () => pawn.pather?.StopDead();
			watch.tickAction = () =>
			{
				var food = pawn.needs?.food;
				var rest = pawn.needs?.rest;
				if ((food != null && food.CurLevelPercentage < 0.08f)
					|| (rest != null && rest.CurLevelPercentage < 0.08f))
				{
					EndJobWith(JobCondition.InterruptForced);
					return;
				}
				pawn.rotationTracker.FaceCell(FocusCell);
			};
			yield return watch;
		}
	}
}
