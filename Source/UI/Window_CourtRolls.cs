using System.Collections.Generic;
using RimCourt.Court;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimCourt.UI
{
	public class Window_CourtRolls : Window
	{
		private static readonly Color TitleColor = new Color(0.95f, 0.86f, 0.55f);
		private static readonly Color MutedColor = new Color(0.70f, 0.70f, 0.70f);
		private static readonly Color RowBg = new Color(0.16f, 0.16f, 0.18f, 0.85f);
		private static readonly Color TabActiveBg = new Color(0.27f, 0.27f, 0.27f, 0.65f);

		private static readonly Color[] AxisColors =
		{
			new Color(0.52f, 0.74f, 0.92f),
			new Color(0.84f, 0.42f, 0.36f),
			new Color(0.85f, 0.70f, 0.30f),
			new Color(0.62f, 0.58f, 0.70f),
		};

		private const int FilterAll = -2;
		private const int FilterNone = -1;
		private const float RowH = 64f;
		private const int CacheTicks = 60;
		private const string Dot = "  ·  ";

		private class Line
		{
			public string title;
			public string when;
			public string what;
			public string how;
			public int axis;
		}

		private readonly List<Line> _lines = new List<Line>();
		private readonly int[] _counts = new int[5];
		private int _total;
		private int _filter = FilterAll;
		private int _builtAt = -999999;
		private int _builtFilter = FilterAll;
		private int _builtCount = -1;
		private Vector2 _scroll;

		public override Vector2 InitialSize => new Vector2(640f, 660f);

		public Window_CourtRolls()
		{
			draggable = true;
			doCloseX = true;
			closeOnClickedOutside = false;
			absorbInputAroundWindow = false;
			preventCameraMotion = false;
			forcePause = false;
			resizeable = false;
		}

		public static void Open()
		{
			if (!Find.WindowStack.IsOpen<Window_CourtRolls>())
				Find.WindowStack.Add(new Window_CourtRolls());
		}

		public override void DoWindowContents(Rect inRect)
		{
			var mgr = CourtManager.Instance;
			if (mgr == null) { Close(); return; }
			Refresh(mgr);

			var y = inRect.y;
			Text.Font = GameFont.Medium;
			GUI.color = TitleColor;
			Widgets.Label(new Rect(inRect.x, y, inRect.width, 32f), "RimCourt.RollsTitle".Translate());
			GUI.color = Color.white;
			Text.Font = GameFont.Small;
			y += 34f;

			GUI.color = MutedColor;
			Text.Font = GameFont.Tiny;
			Widgets.Label(new Rect(inRect.x, y, inRect.width, 20f),
				_total == 0
					? "RimCourt.RollsEmpty".Translate()
					: "RimCourt.RollsSummary".Translate(_total, _counts[0], _counts[1], _counts[2], _counts[3], _counts[4]));
			Text.Font = GameFont.Small;
			GUI.color = Color.white;
			y += 26f;

			var tabs = new Rect(inRect.x, y, inRect.width, 28f);
			var w = tabs.width / 6f;
			FilterButton(new Rect(tabs.x, tabs.y, w, tabs.height), "RimCourt.RollsAll".Translate(), FilterAll);
			for (var i = 0; i < 4; i++)
				FilterButton(new Rect(tabs.x + (i + 1) * w, tabs.y, w, tabs.height),
					JudgeReputation.KeyOf(i).Translate().CapitalizeFirst(), i);
			FilterButton(new Rect(tabs.x + 5f * w, tabs.y, w, tabs.height), "RimCourt.RollsNoRuling".Translate(), FilterNone);
			y += 34f;

			var body = new Rect(inRect.x, y, inRect.width, inRect.yMax - y);
			if (_lines.Count == 0)
			{
				GUI.color = MutedColor;
				Widgets.Label(body, (_total == 0 ? "RimCourt.RollsEmptyLong" : "RimCourt.RollsNothingHere").Translate());
				GUI.color = Color.white;
				return;
			}

			var view = new Rect(0f, 0f, body.width - 18f, _lines.Count * RowH);
			Widgets.BeginScrollView(body, ref _scroll, view);
			var first = Mathf.Max(0, Mathf.FloorToInt(_scroll.y / RowH) - 1);
			var last = Mathf.Min(_lines.Count - 1, first + Mathf.CeilToInt(body.height / RowH) + 2);
			for (var i = first; i <= last; i++)
				DrawLine(new Rect(0f, i * RowH, view.width, RowH - 4f), _lines[i]);
			Widgets.EndScrollView();
		}

		private void FilterButton(Rect r, string label, int filter)
		{
			var active = _filter == filter;
			if (active) Widgets.DrawBoxSolid(r, TabActiveBg);
			GUI.color = active ? TitleColor : filter >= 0 ? AxisColors[filter] : MutedColor;
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(r, label);
			Text.Anchor = TextAnchor.UpperLeft;
			GUI.color = Color.white;
			Widgets.DrawHighlightIfMouseover(r);
			if (Widgets.ButtonInvisible(r) && !active)
			{
				_filter = filter;
				_scroll = Vector2.zero;
			}
		}

		private static void DrawLine(Rect row, Line l)
		{
			Widgets.DrawBoxSolid(row, RowBg);
			var tint = l.axis >= 0 ? AxisColors[l.axis] : MutedColor;
			Widgets.DrawBoxSolid(new Rect(row.x, row.y, 4f, row.height), tint);

			GUI.color = MutedColor;
			Text.Font = GameFont.Tiny;
			Text.Anchor = TextAnchor.UpperRight;
			Widgets.Label(new Rect(row.xMax - 196f, row.y + 4f, 190f, 18f), l.when);
			Text.Anchor = TextAnchor.UpperLeft;

			Text.Font = GameFont.Small;
			GUI.color = Color.white;
			Widgets.Label(new Rect(row.x + 12f, row.y + 2f, row.width - 214f, 22f), l.title);

			Text.Font = GameFont.Tiny;
			var wrap = Text.WordWrap;
			Text.WordWrap = false;
			GUI.color = tint;
			Widgets.Label(new Rect(row.x + 12f, row.y + 23f, row.width - 20f, 18f), l.what);
			GUI.color = MutedColor;
			Widgets.Label(new Rect(row.x + 12f, row.y + 40f, row.width - 20f, 18f), l.how);
			Text.WordWrap = wrap;
			Text.Font = GameFont.Small;
			GUI.color = Color.white;

			if (Mouse.IsOver(row)) TooltipHandler.TipRegion(row, l.what + "\n" + l.how);
		}

		private void Refresh(CourtManager mgr)
		{
			var rolls = mgr.Rolls;
			var now = Find.TickManager.TicksGame;
			if (_builtFilter == _filter && _builtCount == rolls.Count && now - _builtAt < CacheTicks && now >= _builtAt) return;
			_builtAt = now;
			_builtFilter = _filter;
			_builtCount = rolls.Count;

			_lines.Clear();
			for (var i = 0; i < _counts.Length; i++) _counts[i] = 0;
			_total = rolls.Count;
			for (var i = rolls.Count - 1; i >= 0; i--)
			{
				var e = rolls[i];
				var bucket = e.outcome == RollEntry.Ruled && e.axis >= 0 && e.axis < 4 ? e.axis : 4;
				_counts[bucket]++;
				if (_filter != FilterAll && (_filter == FilterNone ? bucket != 4 : bucket != _filter)) continue;
				_lines.Add(Build(e, bucket));
			}
		}

		private static Line Build(RollEntry e, int bucket)
		{
			var def = DefDatabase<PetitionDef>.GetNamedSilentFail(e.defName);
			var title = def != null ? def.LabelCap.ToString() : (e.caseLabel ?? e.defName ?? "?").CapitalizeFirst();

			var who = e.bName.NullOrEmpty()
				? (e.aName ?? "?")
				: "RimCourt.RecordPair".Translate(e.aName ?? "?", e.bName).ToString();
			var what = e.verdict.NullOrEmpty() ? who : who + ": " + e.verdict;

			var how = new List<string>();
			if (!e.lordName.NullOrEmpty()) how.Add("RimCourt.RollsBy".Translate(e.lordName));
			if (e.outcome == RollEntry.Unanswered) how.Add("RimCourt.RollsUnanswered".Translate());
			else if (e.axis >= 0 && e.axis < 4)
				how.Add("RimCourt.RollsHow".Translate(("RimCourt.Precedent" + e.axis).Translate()));
			if (e.outcome == RollEntry.Defied) how.Add("RimCourt.RollsDefied".Translate());
			if (e.kin) how.Add("RimCourt.RollsKin".Translate());
			if (e.hearing) how.Add("RimCourt.RollsHearing".Translate());

			return new Line
			{
				title = title,
				when = GenDate.DateFullStringAt(GenDate.TickGameToAbs(e.tick), new Vector2(e.longitude, 0f)),
				what = what,
				how = string.Join(Dot, how),
				axis = bucket < 4 ? bucket : -1,
			};
		}
	}
}
