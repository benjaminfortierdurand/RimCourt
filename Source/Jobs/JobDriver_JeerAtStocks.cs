using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimCourt.Jobs
{
	public class JobDriver_JeerAtStocks : JobDriver
	{
		private const int FirstJeerTicks = 40;
		private const int JeerInterval = 140;
		private const float JeerChance = 0.7f;
		private const float PeltShare = 0.5f;

		private int _ticks;

		private Court.Building_Stocks Post => job.targetA.Thing as Court.Building_Stocks;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
			=> pawn.ReserveSittableOrSpot(job.targetB.Cell, job, errorOnFailed);

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref _ticks, "ticks");
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.EndOnDespawnedOrNull(TargetIndex.A);
			this.FailOn(() => Post == null || !Post.Tied || Post.Occupant == null);

			yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);

			var jeer = new Toil
			{
				defaultCompleteMode = ToilCompleteMode.Delay,
				defaultDuration = job.def.joyDuration,
				handlingFacing = true,
				socialMode = RandomSocialMode.Quiet,
			};
			jeer.initAction = () => pawn.pather?.StopDead();
			jeer.tickAction = () =>
			{
				var post = Post;
				if (post == null) { EndJobWith(JobCondition.Incompletable); return; }
				pawn.rotationTracker?.FaceCell(post.Position);
				_ticks++;
				if ((_ticks == FirstJeerTicks || (_ticks > FirstJeerTicks && _ticks % JeerInterval == 0)) && Rand.Chance(JeerChance))
					post.TryMockBy(pawn, PeltShare);
				JoyUtility.JoyTickCheckEnd(pawn, 1, JoyTickFullJoyAction.EndJob);
			};
			yield return jeer;
		}
	}
}
