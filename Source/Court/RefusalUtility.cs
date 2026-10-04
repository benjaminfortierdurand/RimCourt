using RimWorld;
using UnityEngine;
using Verse;

namespace RimCourt.Court
{
	public static class RefusalUtility
	{
		private static readonly TraitDef Abrasive = DefDatabase<TraitDef>.GetNamedSilentFail("Abrasive");
		private static readonly TraitDef Bloodlust = DefDatabase<TraitDef>.GetNamedSilentFail("Bloodlust");
		private static readonly TraitDef Psychopath = DefDatabase<TraitDef>.GetNamedSilentFail("Psychopath");
		private static readonly TraitDef Greedy = DefDatabase<TraitDef>.GetNamedSilentFail("Greedy");
		private static readonly TraitDef Jealous = DefDatabase<TraitDef>.GetNamedSilentFail("Jealous");
		private static readonly TraitDef Kind = DefDatabase<TraitDef>.GetNamedSilentFail("Kind");
		private static readonly TraitDef Wimp = DefDatabase<TraitDef>.GetNamedSilentFail("Wimp");
		private static readonly TraitDef Ascetic = DefDatabase<TraitDef>.GetNamedSilentFail("Ascetic");
		private static readonly TraitDef Nerves = DefDatabase<TraitDef>.GetNamedSilentFail("Nerves");

		private const float MaxChance = 0.55f;

		public static bool CanBeRefused(VerdictOption v)
		{
			if (v == null) return false;
			switch (v.outcome)
			{
				case "Banish":
				case "BanishChallenger":
				case "Stocks":
				case "StocksSuitor":
				case "StocksChallenger":
				case "StocksCaptive":
				case "Execute":
				case "BloodPrice":
				case "PunishBoth":
				case "FlogAccuser":
				case "FlogAdulterer":
				case "Divorce":
				case "Penance":
				case "RivalsDuel":
				case "Duel":
				case "ThroneDuel":
					return false;
				default:
					return true;
			}
		}

		public static float Chance(Pawn p, Pawn lord, JudgeReputation rep)
		{
			if (p == null || lord == null || p == lord || p.Dead || !p.Spawned) return 0f;
			if (p.Downed || p.InMentalState || !p.IsFreeColonist) return 0f;
			if (p.ageTracker != null && p.ageTracker.AgeBiologicalYears < 16) return 0f;

			var chance = BaseFor(rep?.Stage ?? JudgeReputation.StageNone);
			chance += FromTraits(p);
			chance += FromStanding(p, lord);
			chance += HallUtility.RefusalShiftOn(p.Map);
			return Mathf.Clamp(chance, 0f, MaxChance);
		}

		public static bool Refuses(Pawn p, Pawn lord, JudgeReputation rep)
			=> Rand.Chance(Chance(p, lord, rep));

		private static float BaseFor(int stage)
		{
			switch (stage)
			{
				case JudgeReputation.StageHarsh: return 0.02f;
				case JudgeReputation.StageJust: return 0.04f;
				case JudgeReputation.StageVenal: return 0.12f;
				case JudgeReputation.StageWeak: return 0.20f;
				default: return 0.07f;
			}
		}

		private static float FromTraits(Pawn p)
		{
			var traits = p.story?.traits;
			if (traits == null) return 0f;
			var d = 0f;
			if (Abrasive != null && traits.HasTrait(Abrasive)) d += 0.10f;
			if (Psychopath != null && traits.HasTrait(Psychopath)) d += 0.10f;
			if (Bloodlust != null && traits.HasTrait(Bloodlust)) d += 0.08f;
			if (Greedy != null && traits.HasTrait(Greedy)) d += 0.06f;
			if (Jealous != null && traits.HasTrait(Jealous)) d += 0.06f;
			if (Kind != null && traits.HasTrait(Kind)) d -= 0.10f;
			if (Wimp != null && traits.HasTrait(Wimp)) d -= 0.10f;
			if (Ascetic != null && traits.HasTrait(Ascetic)) d -= 0.05f;

			if (Nerves != null)
			{
				switch (traits.DegreeOfTrait(Nerves))
				{
					case -2: d += 0.10f; break;
					case -1: d += 0.05f; break;
					case 1: d += 0.04f; break;
					case 2: d += 0.08f; break;
				}
			}
			return d;
		}

		private static float FromStanding(Pawn p, Pawn lord)
		{
			var d = 0f;

			var opinion = p.relations?.OpinionOf(lord) ?? 0;
			if (opinion < 0) d += Mathf.Min(0.20f, -opinion / 400f);

			var mood = p.needs?.mood;
			if (mood != null && mood.CurLevelPercentage < 0.35f) d += 0.08f;

			var mine = p.royalty?.MostSeniorTitle;
			var his = lord.royalty?.MostSeniorTitle;
			if (mine != null && (his == null || mine.def.seniority > his.def.seniority)) d += 0.12f;

			return d;
		}
	}
}
