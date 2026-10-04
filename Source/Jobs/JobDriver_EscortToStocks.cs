using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RimCourt.Jobs
{
	public class JobDriver_EscortToStocks : JobDriver
	{
		private const int TieTicks = 150;

		private Pawn Condemned => job.targetA.Thing as Pawn;
		private Court.Building_Stocks Post => job.targetB.Thing as Court.Building_Stocks;
		private bool Carried => Condemned != null && Condemned.IsPrisonerOfColony;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
			=> !Carried || pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDespawnedOrNull(TargetIndex.B);
			this.FailOn(() => Condemned == null || Condemned.Dead || Post == null || Post.Occupant != Condemned || Post.Tied);

			if (Carried)
			{
				yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch)
					.FailOn(() => !Condemned.Spawned)
					.FailOnSomeonePhysicallyInteracting(TargetIndex.A);
				yield return Toils_Haul.StartCarryThing(TargetIndex.A);
				var carry = Toils_Haul.CarryHauledThingToCell(TargetIndex.C);
				yield return carry;
				yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.C, carry, false);
				yield return new Toil
				{
					initAction = () => Post?.Notify_Placed(Condemned),
					defaultCompleteMode = ToilCompleteMode.Instant,
				};
			}
			else
			{
				this.FailOnDespawnedOrNull(TargetIndex.A);
				yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
				yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
			}

			var wait = new Toil { defaultCompleteMode = ToilCompleteMode.Never, handlingFacing = true };
			wait.initAction = () => pawn.pather?.StopDead();
			wait.tickAction = () =>
			{
				var c = Condemned;
				var p = Post;
				if (c == null || p == null) { ReadyForNextToil(); return; }
				pawn.rotationTracker?.FaceCell(p.Position);
				if (c.Spawned && c.Position == p.Position) ReadyForNextToil();
			};
			yield return wait;

			var tie = Toils_General.Wait(TieTicks, TargetIndex.B);
			tie.handlingFacing = true;
			tie.tickAction = () => pawn.rotationTracker?.FaceCell(Post?.Position ?? pawn.Position);
			tie.WithProgressBarToilDelay(TargetIndex.B);
			yield return tie;

			yield return new Toil
			{
				initAction = () => Post?.Notify_Tied(pawn),
				defaultCompleteMode = ToilCompleteMode.Instant,
			};
		}
	}
}
