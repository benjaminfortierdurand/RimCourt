using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimCourt.Court
{
	public static class AttackUtility
	{
		public const int MaxAttemptTicks = 2000;
		public const int MinAttackerAge = 16;
		private const float MaxLungeChance = 0.6f;
		private const float OutsiderBaseChance = 0.04f;

		private static readonly MentalStateDef Rage = DefDatabase<MentalStateDef>.GetNamedSilentFail("MurderousRage");
		private static readonly TraitDef Kind = DefDatabase<TraitDef>.GetNamedSilentFail("Kind");
		private static readonly TraitDef Wimp = DefDatabase<TraitDef>.GetNamedSilentFail("Wimp");
		private static readonly TraitDef Bloodlust = DefDatabase<TraitDef>.GetNamedSilentFail("Bloodlust");
		private static readonly TraitDef Psychopath = DefDatabase<TraitDef>.GetNamedSilentFail("Psychopath");
		private static readonly TraitDef Abrasive = DefDatabase<TraitDef>.GetNamedSilentFail("Abrasive");
		private static readonly TraitDef Brawler = DefDatabase<TraitDef>.GetNamedSilentFail("Brawler");
		private static readonly TraitDef Nerves = DefDatabase<TraitDef>.GetNamedSilentFail("Nerves");

		public static bool Enabled => Rage != null && (RimCourtMod.Settings?.attempts ?? true);

		private static bool CanAttack(Pawn p, Pawn lord)
		{
			if (!Enabled || p == null || lord == null || p == lord) return false;
			if (p.Dead || p.Downed || !p.Spawned || lord.Dead || !lord.Spawned || p.Map != lord.Map) return false;
			if (!p.RaceProps.Humanlike || p.InMentalState || p.WorkTagIsDisabled(WorkTags.Violent)) return false;
			if (p.ageTracker != null && p.ageTracker.AgeBiologicalYears < MinAttackerAge) return false;
			return p.health != null && p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation);
		}

		public static float LungeChance(Pawn p, Pawn lord)
		{
			if (!CanAttack(p, lord)) return 0f;
			if (CourtManager.KinToLord(p, lord) != null) return 0f;
			var traits = p.story?.traits;
			if (traits == null) return 0f;
			if (Kind != null && traits.HasTrait(Kind)) return 0f;
			if (Wimp != null && traits.HasTrait(Wimp)) return 0f;

			var chance = 0f;
			if (Bloodlust != null && traits.HasTrait(Bloodlust)) chance += 0.30f;
			if (Psychopath != null && traits.HasTrait(Psychopath)) chance += 0.20f;
			if (Brawler != null && traits.HasTrait(Brawler)) chance += 0.15f;
			if (Abrasive != null && traits.HasTrait(Abrasive)) chance += 0.10f;
			if (Nerves != null && traits.DegreeOfTrait(Nerves) == -2) chance += 0.25f;

			var opinion = p.relations?.OpinionOf(lord) ?? 0;
			if (opinion <= -60) chance += 0.25f;
			else if (opinion <= -30) chance += 0.10f;
			return Mathf.Clamp(chance, 0f, MaxLungeChance);
		}

		public static bool OutsiderSnaps(Petition p, VerdictOption v, JudgeReputation rep)
		{
			var pawn = p?.petitioner;
			if (pawn == null || v == null || p.other != null) return false;
			if (pawn.Faction == null || pawn.Faction.IsPlayer || pawn.IsPrisoner) return false;
			if (!(p.def.Worker is PetitionWorker_Outsider) || p.def.Worker is PetitionWorker_Pardon) return false;
			p.def.Worker.Reactions(p, v, out var mood, out _);
			if (mood >= 0) return false;
			var chance = OutsiderBaseChance;
			switch (rep?.Stage ?? JudgeReputation.StageNone)
			{
				case JudgeReputation.StageVenal: chance += 0.04f; break;
				case JudgeReputation.StageWeak: chance += 0.06f; break;
				case JudgeReputation.StageHarsh: chance -= 0.02f; break;
			}
			return Rand.Chance(chance);
		}

		public static bool Lunge(Pawn attacker, Pawn lord)
		{
			if (!CanAttack(attacker, lord)) return false;
			var handler = attacker.mindState?.mentalStateHandler;
			if (handler == null) return false;
			try
			{
				if (!handler.TryStartMentalState(Rage, null, forced: true, forceWake: true, transitionSilently: true))
					return false;
				if (!(attacker.MentalState is MentalState_MurderousRage rage))
				{
					attacker.MentalState?.RecoverFromState();
					return false;
				}
				rage.target = lord;
				rage.forceRecoverAfterTicks = MaxAttemptTicks;
				if (attacker.Faction != Faction.OfPlayer) attacker.guilt?.Notify_Guilty();
				if (attacker.CurJob != null) attacker.jobs.EndCurrentJob(JobCondition.InterruptForced);
				return true;
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] " + attacker.LabelShort + " n'a pas pu se jeter sur le seigneur (" + e.Message + ").");
				return false;
			}
		}
	}
}
