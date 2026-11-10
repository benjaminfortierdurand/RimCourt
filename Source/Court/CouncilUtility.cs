using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimCourt.Court
{
	public static class CouncilUtility
	{
		public const int MaxCouncil = 3;
		private const float StakeWeight = 2.5f;
		private const int StrongOpinion = 25;
		private const float CloseKin = 175f;

		private static readonly TraitDef Kind = DefDatabase<TraitDef>.GetNamedSilentFail("Kind");
		private static readonly TraitDef Bloodlust = DefDatabase<TraitDef>.GetNamedSilentFail("Bloodlust");
		private static readonly TraitDef Abrasive = DefDatabase<TraitDef>.GetNamedSilentFail("Abrasive");
		private static readonly TraitDef Wimp = DefDatabase<TraitDef>.GetNamedSilentFail("Wimp");
		private static readonly TraitDef Ascetic = DefDatabase<TraitDef>.GetNamedSilentFail("Ascetic");
		private static readonly TraitDef Greedy = DefDatabase<TraitDef>.GetNamedSilentFail("Greedy");
		private static readonly TraitDef Jealous = DefDatabase<TraitDef>.GetNamedSilentFail("Jealous");
		private static readonly TraitDef Psychopath = DefDatabase<TraitDef>.GetNamedSilentFail("Psychopath");
		private static readonly TraitDef Nerves = DefDatabase<TraitDef>.GetNamedSilentFail("Nerves");

		public static void Convene(Petition p, List<Pawn> seated)
		{
			p.council.Clear();
			p.counsel.Clear();
			p.counselWhy.Clear();
			if (seated == null) return;
			for (var i = 0; i < seated.Count && p.council.Count < MaxCouncil; i++)
			{
				var c = seated[i];
				if (c == null || p.Involves(c) || p.council.Contains(c)) continue;
				int pick;
				string why;
				try { pick = Advise(c, p, out why); }
				catch (System.Exception e)
				{
					Log.Warning("[RimCourt] " + c.LabelShort + " n'a pas d'avis (" + e.Message + ").");
					continue;
				}
				if (pick < 0) continue;
				p.council.Add(c);
				p.counsel.Add(pick);
				p.counselWhy.Add(why ?? "");
			}
		}

		public static int Advise(Pawn c, Petition p, out string why)
		{
			why = null;
			var verdicts = p.def?.verdicts;
			if (c == null || verdicts == null || verdicts.Count == 0) return -1;

			Taste(c, out var tj, out var th, out var tv, out var tw);
			var kind = Has(c, Kind);
			var bloody = Has(c, Bloodlust);
			var sp = Stake(c, p.petitioner);
			var so = Stake(c, p.other);

			var best = -1;
			var bestScore = float.MinValue;
			var bestPm = 0;
			var bestOm = 0;
			VerdictOption bestOption = null;
			for (var i = 0; i < verdicts.Count; i++)
			{
				var v = p.def.Worker.Weigh(p, verdicts[i]);
				p.def.Worker.Reactions(p, v, out var pm, out var om);
				var score = v.just * tj + v.harsh * th + v.venal * tv + v.weak * tw;
				score += (sp * pm + so * om) * StakeWeight;
				if (kind && !RefusalUtility.CanBeRefused(v)) score -= 2.5f;
				if (bloody && DrawsBlood(v.outcome)) score += 1f;
				score += Rand.ValueSeeded(Gen.HashCombineInt(c.thingIDNumber, p.id * 31 + i)) * 0.25f;
				if (score <= bestScore) continue;
				bestScore = score;
				best = i;
				bestPm = pm;
				bestOm = om;
				bestOption = v;
			}
			if (best < 0) return -1;

			if (sp * bestPm > 0)
				why = (sp > 0 ? "RimCourt.CounselFriend" : "RimCourt.CounselFoe")
					.Translate(c, p.petitioner.LabelShortCap).ToString();
			else if (so * bestOm > 0)
				why = (so > 0 ? "RimCourt.CounselFriend" : "RimCourt.CounselFoe")
					.Translate(c, p.other.LabelShortCap).ToString();
			else
			{
				var trait = TraitBehind(c, CourtManager.AxisOf(bestOption));
				why = trait != null
					? "RimCourt.CounselTrait".Translate(c, trait.Label).ToString()
					: "RimCourt.CounselPlain".Translate(c).ToString();
			}
			return best;
		}

		private static void Taste(Pawn c, out float just, out float harsh, out float venal, out float weak)
		{
			just = 1f;
			harsh = 0.3f;
			venal = 0.05f;
			weak = 0.15f;
			var traits = c.story?.traits;
			if (traits == null) return;

			if (Kind != null && traits.HasTrait(Kind)) { harsh -= 1.5f; weak += 0.85f; }
			if (Bloodlust != null && traits.HasTrait(Bloodlust)) harsh += 1.6f;
			if (Psychopath != null && traits.HasTrait(Psychopath)) { harsh += 0.8f; venal += 0.5f; }
			if (Abrasive != null && traits.HasTrait(Abrasive)) harsh += 0.7f;
			if (Greedy != null && traits.HasTrait(Greedy)) venal += 2.2f;
			if (Jealous != null && traits.HasTrait(Jealous)) venal += 1f;
			if (Ascetic != null && traits.HasTrait(Ascetic)) venal -= 1f;
			if (Wimp != null && traits.HasTrait(Wimp)) { weak += 1.4f; harsh -= 0.6f; }
			if (Nerves == null) return;
			var degree = traits.DegreeOfTrait(Nerves);
			if (degree == -2) harsh += 0.7f;
			else if (degree == -1) weak += 0.6f;
			else if (degree >= 1) { weak -= 0.5f; just += 0.2f; }
		}

		private static Trait TraitBehind(Pawn c, int axis)
		{
			var traits = c.story?.traits;
			if (traits == null) return null;
			switch (axis)
			{
				case JudgeReputation.StageJust:
					return Get(traits, Kind) ?? NervesAt(traits, 1, 2);
				case JudgeReputation.StageHarsh:
					return Get(traits, Bloodlust) ?? Get(traits, Psychopath) ?? Get(traits, Abrasive) ?? NervesAt(traits, -2, -2);
				case JudgeReputation.StageVenal:
					return Get(traits, Greedy) ?? Get(traits, Jealous) ?? Get(traits, Psychopath);
				case JudgeReputation.StageWeak:
					return Get(traits, Wimp) ?? Get(traits, Kind) ?? NervesAt(traits, -1, -1);
				default:
					return null;
			}
		}

		private static Trait Get(TraitSet traits, TraitDef def)
			=> def != null && traits.HasTrait(def) ? traits.GetTrait(def) : null;

		private static Trait NervesAt(TraitSet traits, int min, int max)
		{
			if (Nerves == null || !traits.HasTrait(Nerves)) return null;
			var degree = traits.DegreeOfTrait(Nerves);
			return degree >= min && degree <= max ? traits.GetTrait(Nerves) : null;
		}

		private static bool Has(Pawn c, TraitDef def)
			=> def != null && c.story?.traits != null && c.story.traits.HasTrait(def);

		private static bool DrawsBlood(string outcome)
		{
			if (outcome.NullOrEmpty()) return false;
			return outcome == "BloodPrice" || outcome == "Death" || outcome == "Execute"
				|| outcome.Contains("Flog") || outcome.Contains("Duel");
		}

		private static int Stake(Pawn c, Pawn party)
		{
			if (party == null || party == c || c.relations == null || !party.RaceProps.Humanlike) return 0;
			var opinion = c.relations.OpinionOf(party);
			if (opinion <= -StrongOpinion) return -1;
			if (opinion >= StrongOpinion) return 1;
			var rel = c.GetMostImportantRelation(party);
			return rel != null && rel.importance >= CloseKin && opinion >= 0 ? 1 : 0;
		}
	}
}
