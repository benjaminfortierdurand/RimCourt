using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimCourt.Jobs
{
	public class JobDriver_PleadCase : JobDriver
	{
		private const int PleadDuration = 140;
		private const int LineInterval = 45;

		private int _pleadTicks;
		private bool _notified;

		private Pawn LordPawn => job.targetB.Thing as Pawn;

		public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref _pleadTicks, "pleadTicks");
			Scribe_Values.Look(ref _notified, "notified");
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOn(() => Court.CourtManager.Instance == null || !Court.CourtManager.Instance.Active);
			this.FailOnDespawnedOrNull(TargetIndex.B);

			yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

			var plead = new Toil { defaultCompleteMode = ToilCompleteMode.Never };
			plead.handlingFacing = true;
			plead.initAction = () => pawn.pather?.StopDead();
			plead.tickAction = () =>
			{
				var mgr = Court.CourtManager.Instance;
				if (mgr == null || !mgr.ShouldStand(pawn))
				{
					EndJobWith(JobCondition.Succeeded);
					return;
				}
				if (LordPawn != null && LordPawn.Spawned)
					pawn.rotationTracker.FaceCell(LordPawn.Position);
				if (_notified) return;
				if (!mgr.IsPetitioner(pawn))
				{
					_notified = true;
					return;
				}
				_pleadTicks++;
				if (_pleadTicks % LineInterval == 1 && pawn.Map != null)
					MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, mgr.NextPleadLine(), 3.5f);
				if (_pleadTicks >= PleadDuration)
				{
					_notified = true;
					mgr.Notify_PleadFinished(pawn);
				}
			};
			yield return plead;
		}
	}
}
