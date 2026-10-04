using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimCourt.Court
{
	public static class EmpireDeserterUtility
	{
		public const string DeserterFactionDefName = "VFEE_Deserters";
		public const float StrikeChance = 0.06f;
		public const int CooldownDays = 60;
		public const int CooldownTicks = CooldownDays * 60000;

		public static Faction Deserters()
		{
			var def = DefDatabase<FactionDef>.GetNamedSilentFail(DeserterFactionDefName);
			if (def == null) return null;
			var f = Find.FactionManager?.FirstFactionOfDef(def);
			return f != null && !f.defeated ? f : null;
		}

		public static Faction Empire()
		{
			var all = Find.FactionManager?.AllFactionsListForReading;
			if (all == null) return null;
			for (var i = 0; i < all.Count; i++)
				if (!all[i].defeated && EmpireHonorUtility.IsImperial(all[i])) return all[i];
			return null;
		}

		public static bool LordIsImperial(Pawn lord)
		{
			var titles = lord?.royalty?.AllTitlesInEffectForReading;
			if (titles == null) return false;
			for (var i = 0; i < titles.Count; i++)
				if (EmpireHonorUtility.IsImperial(titles[i].faction)) return true;
			return false;
		}

		public static bool SidedWithDeserters()
		{
			var des = Deserters();
			return des != null && des.PlayerRelationKind == FactionRelationKind.Ally;
		}

		public static Faction Sender(Pawn lord)
		{
			var player = Faction.OfPlayer;
			if (player == null) return null;

			var des = Deserters();
			if (des != null && des.HostileTo(player) && LordIsImperial(lord)) return des;

			var emp = Empire();
			if (emp != null && emp.HostileTo(player) && SidedWithDeserters()) return emp;

			return null;
		}

		public static bool Roll(Pawn lord)
		{
			if (Sender(lord) == null) return false;
			var mgr = CourtManager.Instance;
			if (mgr == null || !mgr.DeserterCooledDown) return false;
			if (!Rand.Chance(StrikeChance)) return false;
			mgr.NoteDeserterSent();
			return true;
		}

		public static void Arm(Pawn pawn)
		{
			if (pawn?.equipment == null || pawn.equipment.Primary != null) return;
			var knife = DefDatabase<ThingDef>.GetNamedSilentFail("MeleeWeapon_Knife");
			if (knife == null) return;
			try
			{
				var stuff = GenStuff.DefaultStuffFor(knife);
				if (ThingMaker.MakeThing(knife, stuff) is ThingWithComps blade)
					pawn.equipment.AddEquipment(blade);
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] lame de l'assassin non remise (" + e.Message + ").");
			}
		}

		public static bool Strike(Pawn attacker, Pawn lord)
		{
			if (attacker == null || attacker.Dead || !attacker.Spawned) return false;
			var map = attacker.Map;
			var faction = Sender(lord);
			if (map == null || faction == null) return false;

			var imperial = EmpireHonorUtility.IsImperial(faction);
			try
			{
				OutsiderUtility.ReleaseFromLord(attacker);
				if (attacker.Faction != faction) attacker.SetFaction(faction);
				OutsiderUtility.MakeUnstoppedLord(faction,
					new LordJob_AssaultColony(faction, canKidnap: false, canTimeoutOrFlee: false),
					map, new List<Pawn> { attacker });
				if (lord != null && !lord.Dead && lord.Spawned && attacker.mindState != null)
					attacker.mindState.enemyTarget = lord;

				Find.LetterStack.ReceiveLetter(
					"RimCourt.DeserterStrikesLabel".Translate(attacker.LabelShortCap),
					(imperial ? "RimCourt.ImperialStrikes" : "RimCourt.DeserterStrikes").Translate(
						attacker.LabelShortCap, lord?.LabelShortCap ?? "?"),
					LetterDefOf.ThreatBig, new LookTargets(attacker));
				return true;
			}
			catch (System.Exception e)
			{
				Log.Warning("[RimCourt] l'assassin n'a pas pu frapper (" + e.Message + ").");
				return false;
			}
		}
	}
}
