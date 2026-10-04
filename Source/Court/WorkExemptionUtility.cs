using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimCourt.Court
{
	public class WorkExemption : IExposable
	{
		public Pawn pawn;
		public string workDefName;
		public int expiryTick;

		public void ExposeData()
		{
			Scribe_References.Look(ref pawn, "pawn");
			Scribe_Values.Look(ref workDefName, "workDefName");
			Scribe_Values.Look(ref expiryTick, "expiryTick");
		}
	}

	public static class WorkExemptionUtility
	{
		public const int CheckInterval = 500;
		private const int RememberedDays = 15;

		public static WorkExemption Grant(Pawn pawn, string workDefName)
		{
			if (pawn == null || workDefName.NullOrEmpty()) return null;
			return new WorkExemption
			{
				pawn = pawn,
				workDefName = workDefName,
				expiryTick = Find.TickManager.TicksGame + RememberedDays * 60000,
			};
		}

		public static void Tick(List<WorkExemption> exemptions, JudgeReputation reputation, Pawn lord)
		{
			var now = Find.TickManager.TicksGame;
			for (var i = exemptions.Count - 1; i >= 0; i--)
			{
				var e = exemptions[i];
				if (e?.pawn == null || e.pawn.Dead || e.pawn.Destroyed
					|| e.pawn.Faction != Faction.OfPlayer || e.pawn.workSettings == null)
				{
					exemptions.RemoveAt(i);
					continue;
				}
				if (now >= e.expiryTick) { exemptions.RemoveAt(i); continue; }
				if (!e.pawn.Spawned) continue;

				var work = DefDatabase<WorkTypeDef>.GetNamedSilentFail(e.workDefName);
				if (work == null || e.pawn.WorkTypeIsDisabled(work)) { exemptions.RemoveAt(i); continue; }
				if (e.pawn.workSettings.GetPriority(work) <= 0) continue;

				exemptions.RemoveAt(i);
				Break(e.pawn, work, reputation, lord);
			}
		}

		private static void Break(Pawn pawn, WorkTypeDef work, JudgeReputation reputation, Pawn lord)
		{
			var memories = pawn.needs?.mood?.thoughts?.memories;
			if (memories != null)
			{
				if (RimCourtDefOf.RimCourt_PromiseBroken != null)
					memories.TryGainMemory(RimCourtDefOf.RimCourt_PromiseBroken);
				if (RimCourtDefOf.RimCourt_LordBrokeHisWord != null && lord != null && !lord.Dead)
					memories.TryGainMemory(RimCourtDefOf.RimCourt_LordBrokeHisWord, lord);
			}

			if (reputation != null)
			{
				reputation.weak += 2f;
				reputation.Recompute();
			}

			Messages.Message("RimCourt.PromiseBroken".Translate(pawn.LabelShortCap,
					work.gerundLabel ?? work.labelShort ?? work.defName),
				new LookTargets(pawn), MessageTypeDefOf.NegativeEvent, historical: false);
		}
	}
}
