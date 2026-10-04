using RimWorld;
using Verse;

namespace RimCourt.Court
{
	public static class LordNature
	{
		private static readonly TraitDef Kind = DefDatabase<TraitDef>.GetNamedSilentFail("Kind");
		private static readonly TraitDef Bloodlust = DefDatabase<TraitDef>.GetNamedSilentFail("Bloodlust");
		private static readonly TraitDef Abrasive = DefDatabase<TraitDef>.GetNamedSilentFail("Abrasive");
		private static readonly TraitDef Wimp = DefDatabase<TraitDef>.GetNamedSilentFail("Wimp");
		private static readonly TraitDef Ascetic = DefDatabase<TraitDef>.GetNamedSilentFail("Ascetic");
		private static readonly TraitDef Greedy = DefDatabase<TraitDef>.GetNamedSilentFail("Greedy");
		private static readonly TraitDef Jealous = DefDatabase<TraitDef>.GetNamedSilentFail("Jealous");
		private static readonly TraitDef Psychopath = DefDatabase<TraitDef>.GetNamedSilentFail("Psychopath");
		private static readonly TraitDef Nerves = DefDatabase<TraitDef>.GetNamedSilentFail("Nerves");

		public const int Uneasy = -1;
		public const int Neutral = 0;
		public const int AtEase = 1;

		public static int Feeling(Pawn lord, int axis)
		{
			var traits = lord?.story?.traits;
			if (traits == null || axis == JudgeReputation.StageNone) return Neutral;
			if (Psychopath != null && traits.HasTrait(Psychopath)) return Neutral;

			var score = 0;
			switch (axis)
			{
				case JudgeReputation.StageJust:
					if (Kind != null && traits.HasTrait(Kind)) score++;
					if (Bloodlust != null && traits.HasTrait(Bloodlust)) score--;
					break;

				case JudgeReputation.StageHarsh:
					if (Bloodlust != null && traits.HasTrait(Bloodlust)) score++;
					if (Abrasive != null && traits.HasTrait(Abrasive)) score++;
					if (Nerves != null && traits.DegreeOfTrait(Nerves) == -2) score++;
					if (Kind != null && traits.HasTrait(Kind)) score--;
					if (Wimp != null && traits.HasTrait(Wimp)) score--;
					break;

				case JudgeReputation.StageVenal:
					if (Greedy != null && traits.HasTrait(Greedy)) score++;
					if (Jealous != null && traits.HasTrait(Jealous)) score++;
					if (Ascetic != null && traits.HasTrait(Ascetic)) score--;
					break;

				case JudgeReputation.StageWeak:
					if (Nerves != null && traits.DegreeOfTrait(Nerves) >= 1) score--;
					if (Kind != null && traits.HasTrait(Kind)) score++;
					break;
			}

			if (score > 0) return AtEase;
			if (score < 0) return Uneasy;
			return Neutral;
		}

		public const int VerdictsToShape = 12;
		public const float PointsToShape = 30f;

		public static bool TryShape(Pawn lord, JudgeReputation rep, int verdictsByThisLord)
		{
			if (lord == null || lord.Dead || rep == null) return false;
			if (verdictsByThisLord < VerdictsToShape) return false;

			var stage = rep.Stage;
			if (stage == JudgeReputation.StageNone || stage == JudgeReputation.StageJust) return false;
			if (rep.ScoreOf(stage) < PointsToShape) return false;

			var traits = lord.story?.traits;
			if (traits == null) return false;

			TraitDef gained = null;
			var degree = 0;
			switch (stage)
			{
				case JudgeReputation.StageHarsh:
					gained = Bloodlust;
					break;
				case JudgeReputation.StageVenal:
					if (Ascetic != null && traits.HasTrait(Ascetic)) return false;
					if (Jealous != null && traits.HasTrait(Jealous)) return false;
					gained = Greedy;
					break;
				case JudgeReputation.StageWeak:
					gained = Nerves;
					degree = -1;
					break;
			}
			if (gained == null || traits.HasTrait(gained)) return false;

			var lostKind = gained == Bloodlust && Kind != null && traits.HasTrait(Kind);
			try
			{
				if (lostKind) traits.RemoveTrait(traits.GetTrait(Kind));
				lord.story.traits.GainTrait(new Trait(gained, degree));
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] le siège n'a pas marqué le seigneur (" + e.Message + ").");
				return false;
			}

			var text = ("RimCourt.Shaped" + stage).Translate(lord.LabelShortCap,
				gained.DataAtDegree(degree).LabelCap).ToString();
			if (lostKind) text += "\n\n" + "RimCourt.ShapedLostKind".Translate(lord.LabelShortCap).ToString();
			Find.LetterStack.ReceiveLetter(
				"RimCourt.ShapedLabel".Translate(lord.LabelShortCap),
				text, LetterDefOf.NegativeEvent, new LookTargets(lord));
			return true;
		}

		public static void Note(Pawn lord, int axis)
		{
			if (lord == null || lord.Dead) return;
			var memories = lord.needs?.mood?.thoughts?.memories;
			if (memories == null) return;

			var feeling = Feeling(lord, axis);
			if (feeling == Neutral) return;

			var thought = feeling == Uneasy
				? RimCourtDefOf.RimCourt_RuledAgainstNature
				: RimCourtDefOf.RimCourt_RuledTrueToNature;
			if (thought == null) return;

			try { memories.TryGainMemory(thought); }
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] la nature du seigneur n'a rien dit (" + e.Message + ").");
			}
		}
	}
}
