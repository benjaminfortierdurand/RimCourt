using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimCourt
{
	public class RimCourtSettings : ModSettings
	{
		public int verdictHours = 2;
		public int casesPerSession = 3;
		public IntRange casesRange = new IntRange(DefCasesMin, DefCasesMax);
		public int cooldownDays = 5;
		public bool autoOpenSheet = true;
		public bool shapeLord = true;
		public bool noDecay;
		public bool epithets = true;
		public int stocksHours = DefStocksHours;
		public bool advancedOpen;
		public List<string> barredLeaders = new List<string>();
		public bool barredSeeded;

		public const int DefVerdictHours = 2;
		public const int DefCasesPerSession = 3;
		public const int DefCasesMin = 1;
		public const int DefCasesMax = 3;
		public const int DefCooldownDays = 5;
		public const int DefStocksHours = 12;
		public const bool DefAutoOpenSheet = true;

		public static bool BarredByDefault(FactionDef d)
			=> d == null
				|| !d.fixedLeaderKinds.NullOrEmpty()
				|| !d.royalTitleTags.NullOrEmpty()
				|| d.permanentEnemy
				|| d.raidsForbidden;

		public static bool CanBeLedByExile(FactionDef d)
		{
			if (d == null) return false;
			var s = RimCourtMod.Settings;
			if (s == null) return !BarredByDefault(d);
			s.SeedBarred();
			return !s.barredLeaders.Contains(d.defName);
		}

		public static IEnumerable<FactionDef> LeadableCandidates()
		{
			var all = DefDatabase<FactionDef>.AllDefsListForReading;
			for (var i = 0; i < all.Count; i++)
			{
				var d = all[i];
				if (d.isPlayer || d.hidden || !d.humanlikeFaction) continue;
				yield return d;
			}
		}

		public void SeedBarred()
		{
			if (barredSeeded) return;
			barredSeeded = true;
			if (barredLeaders == null) barredLeaders = new List<string>();
			foreach (var d in LeadableCandidates())
				if (BarredByDefault(d) && !barredLeaders.Contains(d.defName))
					barredLeaders.Add(d.defName);
		}

		public void ResetBarred()
		{
			barredLeaders = new List<string>();
			barredSeeded = false;
			SeedBarred();
		}

		public void ResetToDefaults()
		{
			verdictHours = DefVerdictHours;
			casesPerSession = DefCasesPerSession;
			casesRange = new IntRange(DefCasesMin, DefCasesMax);
			cooldownDays = DefCooldownDays;
			autoOpenSheet = DefAutoOpenSheet;
			shapeLord = true;
			noDecay = false;
			epithets = true;
			stocksHours = DefStocksHours;
			ResetBarred();
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref verdictHours, "verdictHours", DefVerdictHours);
			Scribe_Values.Look(ref casesPerSession, "casesPerSession", DefCasesPerSession);
			Scribe_Values.Look(ref casesRange, "casesRange", new IntRange(-1, -1));
			if (Scribe.mode != LoadSaveMode.Saving && casesRange.min < 0)
				casesRange = new IntRange(DefCasesMin,
					UnityEngine.Mathf.Clamp(casesPerSession, DefCasesMin, 6));
			Scribe_Values.Look(ref cooldownDays, "cooldownDays", DefCooldownDays);
			Scribe_Values.Look(ref autoOpenSheet, "autoOpenSheet", DefAutoOpenSheet);
			Scribe_Values.Look(ref shapeLord, "shapeLord", true);
			Scribe_Values.Look(ref noDecay, "noDecay");
			Scribe_Values.Look(ref epithets, "epithets", true);
			Scribe_Values.Look(ref stocksHours, "stocksHours", DefStocksHours);
			Scribe_Collections.Look(ref barredLeaders, "barredLeaders", LookMode.Value);
			Scribe_Values.Look(ref barredSeeded, "barredSeeded");
			if (Scribe.mode == LoadSaveMode.PostLoadInit && barredLeaders == null)
				barredLeaders = new List<string>();
		}
	}
}
