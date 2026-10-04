using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RimCourt.Court
{
	public class Exile : IExposable
	{
		public Pawn pawn;
		public int returnTick;
		public bool warlord;

		public void ExposeData()
		{
			Scribe_References.Look(ref pawn, "pawn");
			Scribe_Values.Look(ref returnTick, "returnTick");
			Scribe_Values.Look(ref warlord, "warlord");
		}
	}

	public static class ExileUtility
	{
		private static readonly TraitDef Bloodlust = DefDatabase<TraitDef>.GetNamedSilentFail("Bloodlust");
		private static readonly TraitDef Psychopath = DefDatabase<TraitDef>.GetNamedSilentFail("Psychopath");
		private static readonly TraitDef Jealous = DefDatabase<TraitDef>.GetNamedSilentFail("Jealous");
		private static readonly TraitDef Abrasive = DefDatabase<TraitDef>.GetNamedSilentFail("Abrasive");
		private static readonly TraitDef Greedy = DefDatabase<TraitDef>.GetNamedSilentFail("Greedy");
		private static readonly TraitDef Kind = DefDatabase<TraitDef>.GetNamedSilentFail("Kind");
		private static readonly TraitDef Wimp = DefDatabase<TraitDef>.GetNamedSilentFail("Wimp");
		private static readonly TraitDef Ascetic = DefDatabase<TraitDef>.GetNamedSilentFail("Ascetic");

		public const int CheckInterval = 2500;
		private const float BaseChance = 0.25f;
		private const float MinChance = 0.05f;
		private const float MaxChance = 0.60f;
		private const int RaidMinDays = 15;
		private const int RaidMaxDays = 45;
		private const int WarlordMinDays = 60;
		private const int WarlordMaxDays = 120;
		private const int WarlordSocial = 8;
		private const float WarlordChance = 0.3f;
		private const float RaidPointFactor = 0.6f;

		public static Exile Remember(Pawn pawn, bool force = false)
		{
			if (pawn == null || pawn.Dead) return null;
			if (!force && !Rand.Chance(ReturnChance(pawn))) return null;

			var now = Find.TickManager.TicksGame;
			var social = pawn.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
			var warlord = social >= WarlordSocial && Rand.Chance(WarlordChance);
			var days = warlord
				? Rand.Range(WarlordMinDays, WarlordMaxDays)
				: Rand.Range(RaidMinDays, RaidMaxDays);
			return new Exile { pawn = pawn, returnTick = now + days * 60000, warlord = warlord };
		}

		public static Exile Forced(Pawn pawn, bool warlord)
		{
			if (pawn == null || pawn.Dead) return null;
			return new Exile
			{
				pawn = pawn,
				returnTick = Find.TickManager.TicksGame,
				warlord = warlord,
			};
		}

		public static float ReturnChance(Pawn pawn)
		{
			if (pawn == null) return 0f;
			var chance = BaseChance;

			var traits = pawn.story?.traits;
			if (traits != null)
			{
				if (Bloodlust != null && traits.HasTrait(Bloodlust)) chance += 0.15f;
				if (Psychopath != null && traits.HasTrait(Psychopath)) chance += 0.12f;
				if (Jealous != null && traits.HasTrait(Jealous)) chance += 0.08f;
				if (Abrasive != null && traits.HasTrait(Abrasive)) chance += 0.08f;
				if (Greedy != null && traits.HasTrait(Greedy)) chance += 0.06f;
				if (Kind != null && traits.HasTrait(Kind)) chance -= 0.12f;
				if (Wimp != null && traits.HasTrait(Wimp)) chance -= 0.10f;
				if (Ascetic != null && traits.HasTrait(Ascetic)) chance -= 0.08f;
			}

			var melee = pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
			var shooting = pawn.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
			var fight = Mathf.Max(melee, shooting);
			if (fight > 6) chance += Mathf.Min(0.15f, (fight - 6) * 0.02f);

			if (pawn.ageTracker != null && pawn.ageTracker.AgeBiologicalYears >= 60) chance -= 0.08f;

			return Mathf.Clamp(chance, MinChance, MaxChance);
		}

		public static void Tick(List<Exile> exiles)
		{
			var now = Find.TickManager.TicksGame;
			for (var i = exiles.Count - 1; i >= 0; i--)
			{
				var e = exiles[i];
				if (e?.pawn == null || e.pawn.Dead || e.pawn.Discarded
					|| e.pawn.Faction == Faction.OfPlayer)
				{
					exiles.RemoveAt(i);
					continue;
				}
				if (now < e.returnTick) continue;

				var done = true;
				try
				{
					done = Return(e);
				}
				catch (System.Exception ex)
				{
					Log.Error("[RimCourt] retour d'exilé impossible : " + ex);
				}

				if (done) exiles.RemoveAt(i);
				else e.returnTick = now + 60000;
			}
		}

		public const float PardonChance = 0.33f;

		private static bool Return(Exile e)
		{
			var map = Find.AnyPlayerHomeMap;
			if (map == null || map.mapPawns.FreeColonistsSpawnedCount == 0) return false;
			if (e.pawn.Spawned) return false;
			if (!Find.WorldPawns.Contains(e.pawn)) return false;

			if (!e.warlord && Rand.Chance(PardonChance) && ArriveAsPetitioner(e.pawn, map))
				return true;

			var faction = HostFactionFor(e.warlord);
			if (faction == null && e.warlord)
			{
				e.warlord = false;
				faction = HostFactionFor(false);
			}
			if (faction == null) return false;

			if (e.pawn.Faction != faction) e.pawn.SetFaction(faction);

			if (e.warlord) BecomeWarlord(e.pawn, faction);
			else return ComeBackWithMen(e.pawn, faction, map);
			return true;
		}

		public static bool ArriveAsPetitioner(Pawn pawn, Map map)
		{
			var mgr = CourtManager.Instance;
			var def = DefDatabase<PetitionDef>.GetNamedSilentFail("RimCourt_Pardon");
			if (mgr == null || def == null || pawn == null || pawn.Dead) return false;
			if (CourtManager.AnySeatOn(map) == null) return false;
			if (mgr.HasWaitingOutsider(map)) return false;

			var host = Find.FactionManager.AllFactionsVisible
				.Where(f => !f.IsPlayer && !f.defeated && !f.def.hidden && !f.temporary
					&& f.def.humanlikeFaction && !f.HostileTo(Faction.OfPlayer)
					&& !f.def.permanentEnemy)
				.InRandomOrder().FirstOrDefault();
			if (host == null) return false;

			if (!RCellFinder.TryFindRandomPawnEntryCell(out var entry, map,
				CellFinder.EdgeRoadChance_Friendly)) return false;

			if (pawn.Faction != host) pawn.SetFaction(host);
			GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(entry, map, 4), map);

			const int waitTicks = 3 * 60000;
			try
			{
				OutsiderUtility.MakeUnstoppedLord(host,
					new LordJob_VisitColony(host, OutsiderUtility.WaitSpotFor(pawn, map), waitTicks),
					map, new List<Pawn> { pawn });
			}
			catch (System.Exception ex)
			{
				Log.Warning("[RimCourt] l'exilé arrive sans escorte de comportement (" + ex.Message + ").");
			}

			mgr.RegisterOutsider(new OutsiderRequest
			{
				pawn = pawn,
				def = def,
				faction = host,
				expiryTick = Find.TickManager.TicksGame + waitTicks,
			});

			Find.LetterStack.ReceiveLetter(
				"RimCourt.ExilePardonLabel".Translate(pawn.LabelShortCap),
				def.arrivalText.Formatted(pawn.Named("PETITIONER"), 3.Named("DAYS")),
				LetterDefOf.NeutralEvent, new LookTargets(pawn));
			return true;
		}

		public static Exile ForcedDelayed(Pawn pawn, int minDays, int maxDays)
		{
			if (pawn == null || pawn.Dead) return null;
			return new Exile
			{
				pawn = pawn,
				returnTick = Find.TickManager.TicksGame + Rand.Range(minDays, maxDays) * 60000,
				warlord = false,
			};
		}

		private static Faction HostFactionFor(bool wantPeaceful)
		{
			var all = Find.FactionManager.AllFactionsVisible
				.Where(f => !f.IsPlayer && !f.defeated && !f.def.hidden && !f.temporary
					&& f.def.humanlikeFaction)
				.InRandomOrder().ToList();

			if (wantPeaceful)
			{
				var led = all.Where(f => RimCourtSettings.CanBeLedByExile(f.def)).ToList();
				return led.FirstOrDefault(f => !f.HostileTo(Faction.OfPlayer))
					?? led.FirstOrDefault();
			}

			return all.FirstOrDefault(f => f.HostileTo(Faction.OfPlayer))
				?? all.FirstOrDefault(f => !f.def.permanentEnemy)
				?? all.FirstOrDefault();
		}

		private static void MakeHostile(Faction faction)
		{
			if (faction == null || faction.HostileTo(Faction.OfPlayer)) return;
			try
			{
				faction.TryAffectGoodwillWith(Faction.OfPlayer,
					faction.GoodwillToMakeHostile(Faction.OfPlayer),
					canSendMessage: false, canSendHostilityLetter: false);
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] passage en hostilité par la faveur impossible (" + e.Message + ").");
			}
			if (!faction.HostileTo(Faction.OfPlayer))
				faction.SetRelationDirect(Faction.OfPlayer, FactionRelationKind.Hostile,
					canSendHostilityLetter: false);
		}

		private static void BecomeWarlord(Pawn pawn, Faction faction)
		{
			faction.leader = pawn;
			MakeHostile(faction);

			Find.LetterStack.ReceiveLetter(
				"RimCourt.ExileWarlordLabel".Translate(pawn.LabelShortCap),
				"RimCourt.ExileWarlordText".Translate(pawn.LabelShortCap, faction.Name),
				LetterDefOf.ThreatBig);
		}

		private static bool ComeBackWithMen(Pawn pawn, Faction faction, Map map)
		{
			MakeHostile(faction);

			if (!RCellFinder.TryFindRandomPawnEntryCell(out var entry, map, CellFinder.EdgeRoadChance_Hostile))
				return false;

			var points = StorytellerUtility.DefaultThreatPointsNow(map) * RaidPointFactor;
			var parms = new PawnGroupMakerParms
			{
				groupKind = PawnGroupKindDefOf.Combat,
				tile = map.Tile,
				faction = faction,
				generateFightersOnly = true,
			};
			parms.points = Mathf.Max(120f, points,
				faction.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat, parms) * 1.05f);
			var band = PawnGroupMakerUtility.GeneratePawns(parms).ToList();

			GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(entry, map, 6), map);
			band.Insert(0, pawn);
			for (var i = 1; i < band.Count; i++)
				GenSpawn.Spawn(band[i], CellFinder.RandomClosewalkCellNear(entry, map, 8), map);

			LordMaker.MakeNewLord(faction,
				new LordJob_AssaultColony(faction, canKidnap: true, canTimeoutOrFlee: true), map, band);

			Find.LetterStack.ReceiveLetter(
				"RimCourt.ExileReturnsLabel".Translate(pawn.LabelShortCap),
				"RimCourt.ExileReturnsText".Translate(pawn.LabelShortCap, faction.Name, band.Count - 1),
				LetterDefOf.ThreatBig, new LookTargets(pawn));
			return true;
		}
	}
}
