using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimCourt.Court
{
	public class Betrothal : IExposable
	{
		public Pawn a;
		public Pawn b;
		public int dueTick;
		public int expiryTick;

		public void ExposeData()
		{
			Scribe_References.Look(ref a, "a");
			Scribe_References.Look(ref b, "b");
			Scribe_Values.Look(ref dueTick, "dueTick");
			Scribe_Values.Look(ref expiryTick, "expiryTick");
		}
	}

	public static class BetrothalUtility
	{
		public const int CheckInterval = 500;
		private const int WaitTicks = 60000;
		private const int GraceTicks = 240000;
		private const int RetryTicks = 15000;

		public static Betrothal Pledge(Pawn a, Pawn b)
		{
			if (a == null || b == null || a == b) return null;
			if (a.relations == null || b.relations == null) return null;
			if (a.Dead || b.Dead) return null;
			if (a.relations.DirectRelationExists(PawnRelationDefOf.Spouse, b)) return null;
			if (!a.relations.DirectRelationExists(PawnRelationDefOf.Fiance, b)) return null;

			var now = Find.TickManager.TicksGame;
			return new Betrothal
			{
				a = a,
				b = b,
				dueTick = now + WaitTicks,
				expiryTick = now + WaitTicks + GraceTicks,
			};
		}

		public static void Tick(List<Betrothal> pledges)
		{
			var now = Find.TickManager.TicksGame;
			for (var i = pledges.Count - 1; i >= 0; i--)
			{
				var p = pledges[i];
				if (p == null || !StillPledged(p)) { pledges.RemoveAt(i); continue; }
				if (now < p.dueTick) continue;

				if (now >= p.expiryTick)
				{
					MarryWithoutFeast(p);
					pledges.RemoveAt(i);
					continue;
				}

				try
				{
					if (TryCeremony(p)) p.dueTick = now + RetryTicks;
					else p.dueTick = now + CheckInterval;
				}
				catch (System.Exception e)
				{
					Log.Warning("[RimCourt] noce impossible pour l'instant (" + e.Message + ").");
					p.dueTick = now + RetryTicks;
				}
			}
		}

		private static bool StillPledged(Betrothal p)
		{
			if (p.a == null || p.b == null || p.a.Dead || p.b.Dead) return false;
			if (p.a.relations == null || p.b.relations == null) return false;
			if (p.a.relations.DirectRelationExists(PawnRelationDefOf.Spouse, p.b)) return false;
			return p.a.relations.DirectRelationExists(PawnRelationDefOf.Fiance, p.b);
		}

		private static bool TryCeremony(Betrothal p)
		{
			var map = p.a.MapHeld;
			if (map == null || p.b.MapHeld != map) return false;
			if (!MarriageCeremonyUtility.AcceptableGameConditionsToStartCeremony(map)) return false;
			if (!MarriageCeremonyUtility.FianceReadyToStartCeremony(p.a, p.b)) return false;
			if (!MarriageCeremonyUtility.FianceReadyToStartCeremony(p.b, p.a)) return false;

			var gathering = GatheringDefOf.MarriageCeremony;
			if (gathering == null || !gathering.CanExecute(map, p.a)) return false;
			return gathering.Worker.TryExecute(map, p.a);
		}

		private static void MarryWithoutFeast(Betrothal p)
		{
			try
			{
				MarriageCeremonyUtility.Married(p.a, p.b);
			}
			catch (System.Exception e)
			{
				Log.Error("[RimCourt] mariage forcé impossible : " + e);
				return;
			}

			Find.LetterStack.ReceiveLetter(
				"RimCourt.WeddingKeptLabel".Translate(),
				"RimCourt.WeddingKeptText".Translate(p.a.LabelShort, p.b.LabelShort),
				LetterDefOf.PositiveEvent, new LookTargets(p.a));
		}
	}
}
