using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace RimCourt.Jobs
{
	public class JobDriver_Flog : JobDriver
	{
		private const int WindUpTicks = 90;
		private const int BetweenLashTicks = 70;
		private const int RaiseTicks = 18;

		private int _lashesLeft = -1;
		private int _tick;
		private int _nextLashAt = -1;

		private Pawn Condemned => job.targetA.Thing as Pawn;

		public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref _lashesLeft, "lashesLeft", -1);
			Scribe_Values.Look(ref _tick, "tick");
			Scribe_Values.Look(ref _nextLashAt, "nextLashAt", -1);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDespawnedOrNull(TargetIndex.A);
			this.FailOn(() => Condemned == null || Condemned.Dead);
			this.FailOn(() =>
			{
				var mgr = Court.CourtManager.Instance;
				return mgr == null || !mgr.IsBeingFlogged(Condemned);
			});

			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);

			var lash = new Toil { defaultCompleteMode = ToilCompleteMode.Never };
			lash.initAction = () =>
			{
				pawn.pather?.StopDead();
				if (_lashesLeft < 0) _lashesLeft = job.count > 0 ? job.count : Court.FlogUtility.DefaultLashes;
				_tick = 0;
				_nextLashAt = WindUpTicks;
			};
			lash.tickAction = () =>
			{
				var victim = Condemned;
				if (victim == null || victim.Dead) { ReadyForNextToil(); return; }
				if (!pawn.Position.AdjacentTo8WayOrInside(victim.Position))
				{
					if (pawn.pather != null && (!pawn.pather.Moving || pawn.pather.Destination.Thing != victim))
						pawn.pather.StartPath(victim, PathEndMode.Touch);
					return;
				}
				if (pawn.pather != null && pawn.pather.Moving) pawn.pather.StopDead();
				pawn.rotationTracker?.FaceCell(victim.Position);

				_tick++;
				if (_nextLashAt < 0) _nextLashAt = WindUpTicks;
				if (_tick == _nextLashAt - RaiseTicks) Court.FlogUtility.Crack(pawn, victim, false);
				if (_tick < _nextLashAt) return;
				_nextLashAt = _tick + BetweenLashTicks;

				Court.FlogUtility.OneLash(pawn, victim);
				Court.CourtManager.Instance?.Notify_LashLanded(victim);
				_lashesLeft--;
				if (_lashesLeft <= 0 || victim.Dead || victim.Downed)
				{
					if (Prefs.DevMode)
						Log.Message("[RimCourt] fin du châtiment, coups restants=" + _lashesLeft
							+ " victime à terre=" + victim.Downed);
					Court.CourtManager.Instance?.Notify_FloggingDone(victim);
					ReadyForNextToil();
				}
			};
			yield return lash;
		}
	}

	public class JobDriver_TakeLashes : JobDriver
	{
		public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOn(() => Court.CourtManager.Instance == null || !Court.CourtManager.Instance.Active);
			rotateToFace = TargetIndex.B;

			if (job.targetA.IsValid)
				yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

			var endure = new Toil { defaultCompleteMode = ToilCompleteMode.Never };
			endure.initAction = () => pawn.pather?.StopDead();
			endure.tickAction = () =>
			{
				var mgr = Court.CourtManager.Instance;
				if (mgr == null || !mgr.IsBeingFlogged(pawn)) { EndJobWith(JobCondition.Succeeded); return; }
				if (pawn.pather != null && pawn.pather.Moving) pawn.pather.StopDead();
			};
			yield return endure;
		}
	}

}
