using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimCourt.Jobs
{
	public class JobDriver_BringCaptive : JobDriver
	{
		private Pawn Captive => job.targetA.Thing as Pawn;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
			=> pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

		private bool Held()
		{
			var mgr = Court.CourtManager.Instance;
			return mgr != null && mgr.IsCaptiveHeld(Captive);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDestroyedOrNull(TargetIndex.A);
			this.FailOnAggroMentalState(TargetIndex.A);
			this.FailOn(() => !Held());

			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch)
				.FailOn(() => Captive == null || !Captive.IsPrisonerOfColony || !Captive.Spawned)
				.FailOnSomeonePhysicallyInteracting(TargetIndex.A);

			yield return Toils_Haul.StartCarryThing(TargetIndex.A);

			var carry = Toils_Haul.CarryHauledThingToCell(TargetIndex.B);
			yield return carry;

			yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.B, carry, false);

			yield return new Toil
			{
				initAction = () => Court.CourtManager.Instance?.Notify_CaptiveBrought(Captive, pawn),
				defaultCompleteMode = ToilCompleteMode.Instant,
			};

			var post = new Toil { defaultCompleteMode = ToilCompleteMode.Instant };
			post.initAction = () =>
			{
				var mgr = Court.CourtManager.Instance;
				var cell = mgr?.GuardCell() ?? IntVec3.Invalid;
				if (!cell.IsValid || !cell.InBounds(pawn.Map) || !cell.Standable(pawn.Map)) cell = pawn.Position;
				job.targetC = cell;
			};
			yield return post;

			yield return Toils_Goto.GotoCell(TargetIndex.C, PathEndMode.OnCell);

			var guard = new Toil { defaultCompleteMode = ToilCompleteMode.Never, handlingFacing = true };
			guard.initAction = () => pawn.pather?.StopDead();
			guard.tickAction = () =>
			{
				var c = Captive;
				if (c == null || c.Dead || !Held()) { EndJobWith(JobCondition.Succeeded); return; }
				if (c.Spawned) pawn.rotationTracker?.FaceCell(c.Position);
			};
			yield return guard;
		}
	}
}
