using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimCourt
{
	public class RimCourtMod : Mod
	{
		public static RimCourtSettings Settings { get; private set; }

		private Vector2 _leaderScroll;
		private Vector2 _pageScroll;
		private float _pageHeight = 600f;
		private const float LeaderListH = 300f;

		public RimCourtMod(ModContentPack content) : base(content)
		{
			Settings = GetSettings<RimCourtSettings>();
			Log.Message("[RimCourt] Mod chargé, la cour attend.");
		}

		public override string SettingsCategory() => "RimCourt";

		public override void DoSettingsWindowContents(Rect inRect)
		{
			var view = new Rect(0f, 0f, inRect.width - 18f, _pageHeight);
			Widgets.BeginScrollView(inRect, ref _pageScroll, view);
			var l = new Listing_Standard();
			l.Begin(view);

			l.Label("RimCourt.Settings.Intro".Translate());
			l.GapLine();

			l.Label("RimCourt.Settings.VerdictHours".Translate() + ": " + Settings.verdictHours);
			Settings.verdictHours = Mathf.RoundToInt(l.Slider(Settings.verdictHours, 1f, 12f));

			l.Label("RimCourt.Settings.CasesPerSession".Translate() + ": "
				+ (Settings.casesRange.min == Settings.casesRange.max
					? Settings.casesRange.min.ToString()
					: Settings.casesRange.min + " - " + Settings.casesRange.max));
			l.IntRange(ref Settings.casesRange, 1, 6);

			l.Label("RimCourt.Settings.CooldownDays".Translate() + ": " + Settings.cooldownDays);
			Settings.cooldownDays = Mathf.RoundToInt(l.Slider(Settings.cooldownDays, 0f, 15f));

			l.Label("RimCourt.Settings.StocksHours".Translate() + ": " + Settings.stocksHours);
			Settings.stocksHours = Mathf.RoundToInt(l.Slider(Settings.stocksHours, 2f, 24f));

			l.GapLine();
			l.CheckboxLabeled("RimCourt.Settings.AutoOpenSheet".Translate(), ref Settings.autoOpenSheet,
				"RimCourt.Settings.AutoOpenSheetHint".Translate());
			l.CheckboxLabeled("RimCourt.Settings.ShapeLord".Translate(), ref Settings.shapeLord,
				"RimCourt.Settings.ShapeLordHint".Translate());
			l.CheckboxLabeled("RimCourt.Settings.Epithets".Translate(), ref Settings.epithets,
				"RimCourt.Settings.EpithetsHint".Translate());
			l.CheckboxLabeled("RimCourt.Settings.NoDecay".Translate(), ref Settings.noDecay,
				"RimCourt.Settings.NoDecayHint".Translate());

			l.GapLine();
			if (l.ButtonText("RimCourt.Settings.OpenGuide".Translate()))
				UI.Window_CourtGuide.Open();

			l.Gap();
			if (l.ButtonText("RimCourt.Settings.Reset".Translate()))
				Settings.ResetToDefaults();

			l.GapLine();
			if (l.ButtonText((Settings.advancedOpen ? "- " : "+ ")
					+ "RimCourt.Settings.Advanced".Translate()))
				Settings.advancedOpen = !Settings.advancedOpen;

			var used = l.CurHeight;
			l.End();

			if (Settings.advancedOpen)
				DrawLeaderList(new Rect(0f, used + 8f, view.width, LeaderListH));
			_pageHeight = used + 8f + (Settings.advancedOpen ? LeaderListH : 0f) + 12f;
			Widgets.EndScrollView();
		}

		private void DrawLeaderList(Rect rect)
		{
			Settings.SeedBarred();

			Text.Font = GameFont.Tiny;
			GUI.color = new Color(0.72f, 0.7f, 0.66f);
			var intro = "RimCourt.Settings.AdvancedIntro".Translate();
			var introH = Text.CalcHeight(intro, rect.width);
			Widgets.Label(new Rect(rect.x, rect.y, rect.width, introH), intro);
			GUI.color = Color.white;
			Text.Font = GameFont.Small;

			var top = rect.y + introH + 4f;
			var footer = 34f;
			var listRect = new Rect(rect.x, top, rect.width, rect.yMax - top - footer);

			var defs = new List<FactionDef>(RimCourtSettings.LeadableCandidates());
			defs.SortBy(d => d.LabelCap.ToString());

			const float rowH = 26f;
			var view = new Rect(0f, 0f, listRect.width - 18f, defs.Count * rowH);
			Widgets.BeginScrollView(listRect, ref _leaderScroll, view);
			for (var i = 0; i < defs.Count; i++)
			{
				var d = defs[i];
				var row = new Rect(0f, i * rowH, view.width, rowH);
				var allowed = !Settings.barredLeaders.Contains(d.defName);
				var before = allowed;
				Widgets.CheckboxLabeled(row, d.LabelCap, ref allowed);
				if (allowed == before) continue;
				if (allowed) Settings.barredLeaders.Remove(d.defName);
				else Settings.barredLeaders.Add(d.defName);
			}
			Widgets.EndScrollView();

			if (Widgets.ButtonText(new Rect(rect.x, rect.yMax - footer + 4f, rect.width, footer - 8f),
				"RimCourt.Settings.LeaderReset".Translate()))
				Settings.ResetBarred();
		}
	}
}
