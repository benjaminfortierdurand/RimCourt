using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimCourt.Jobs
{
	public class JobDriver_ExecuteCaptive : JobDriver
	{
		private const int WindUpTicks = 120;

		private Pawn Victim => job.targetA.Thing as Pawn;

		public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDespawnedOrNull(TargetIndex.A);
			this.FailOn(() =>
			{
				var mgr = Court.CourtManager.Instance;
				return Victim == null || Victim.Dead || mgr == null || !mgr.IsAwaitingExecution(Victim);
			});

			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);

			var raise = Toils_General.Wait(WindUpTicks, TargetIndex.A);
			raise.handlingFacing = true;
			raise.tickAction = () =>
			{
				var v = Victim;
				if (v != null && v.Spawned) pawn.rotationTracker?.FaceCell(v.Position);
			};
			raise.WithProgressBarToilDelay(TargetIndex.A);
			yield return raise;

			yield return new Toil
			{
				initAction = () =>
				{
					var v = Victim;
					var mgr = Court.CourtManager.Instance;
					if (v == null || v.Dead || mgr == null) return;
					if ((pawn.Position - v.Position).LengthHorizontalSquared > 2f) return;
					var exec = Court.CaptiveUtility.Execute(v, pawn, mgr.CurrentLord, mgr.ExecutionTier);
					if (exec != null)
						Messages.Message("RimCourt.CaptiveExecuted".Translate(exec.LabelShortCap, v.LabelShortCap),
							new LookTargets(exec), MessageTypeDefOf.NegativeEvent, historical: false);
					mgr.Notify_Executed(v);
				},
				defaultCompleteMode = ToilCompleteMode.Instant,
			};
		}
	}
}
