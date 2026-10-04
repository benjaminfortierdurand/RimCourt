using System.Collections.Generic;
using RimCourt.Court;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimCourt.UI
{
	public class Window_CourtStanding : Window
	{
		private static readonly Color TitleColor = new Color(0.95f, 0.86f, 0.55f);
		private static readonly Color HeaderColor = new Color(0.85f, 0.78f, 0.45f);
		private static readonly Color MutedColor = new Color(0.70f, 0.70f, 0.70f);
		private static readonly Color PortraitBg = new Color(0.12f, 0.12f, 0.12f, 0.60f);
		private static readonly Color BarBg = new Color(0.15f, 0.15f, 0.15f, 0.85f);
		private static readonly Color RowBg = new Color(0.16f, 0.16f, 0.18f, 0.85f);
		private static readonly Color ThreatColor = new Color(0.82f, 0.45f, 0.42f);
		private static readonly Color CalmColor = new Color(0.50f, 0.78f, 0.45f);

		private static readonly Color[] AxisColors =
		{
			new Color(0.52f, 0.74f, 0.92f),
			new Color(0.84f, 0.42f, 0.36f),
			new Color(0.85f, 0.70f, 0.30f),
			new Color(0.62f, 0.58f, 0.70f),
		};

		private const float RowH = 52f;
		private const float BannerH = 92f;

		private const int RivalCacheTicks = 30;
		private static readonly Color TabActiveBg = new Color(0.27f, 0.27f, 0.27f, 0.65f);

		private int _tab;
		private Vector2 _recordScroll;
		private float _recordHeight = 400f;
		private Vector2 _bloodScroll;
		private float _bloodHeight = 400f;
		private readonly List<Pawn> _kin = new List<Pawn>();
		private readonly List<string> _kinLabels = new List<string>();
		private Pawn _bloodHeir;
		private int _kinAt = -999999;
		private readonly List<Pawn> _aggrieved = new List<Pawn>();
		private readonly List<int> _grievances = new List<int>();
		private int _aggrievedAt = -999999;

		private class RecordRow
		{
			public string title;
			public string detail;
			public string when;
			public int axis;
		}

		private readonly List<RecordRow> _rows = new List<RecordRow>();
		private int _rowsAt = -999999;

		private Vector2 _scroll;
		private float _viewHeight = 400f;
		private readonly Thing _seat;
		private readonly Map _map;
		private readonly List<Pawn> _rivals = new List<Pawn>();
		private readonly List<int> _opinions = new List<int>();
		private Pawn _heir;
		private int _rivalsAt = -999999;

		public Window_CourtStanding(Thing seat, Map map)
		{
			_seat = seat;
			_map = map;
			draggable = true;
			doCloseX = true;
			closeOnClickedOutside = false;
			absorbInputAroundWindow = false;
			preventCameraMotion = false;
			forcePause = false;
			resizeable = false;
		}

		public override Vector2 InitialSize => new Vector2(620f, 620f);

		protected override void SetInitialSizeAndPosition()
		{
			windowRect = new Rect(Verse.UI.screenWidth - InitialSize.x - 12f, 60f,
				InitialSize.x, InitialSize.y).Rounded();
		}

		public static void Open(Thing seat, Map map)
		{
			if (!Find.WindowStack.IsOpen<Window_CourtStanding>())
				Find.WindowStack.Add(new Window_CourtStanding(seat, map));
		}

		public override void DoWindowContents(Rect inRect)
		{
			var mgr = CourtManager.Instance;
			var map = _seat?.Map ?? _map ?? Find.CurrentMap;
			if (mgr == null || map == null) { Close(); return; }

			var lord = mgr.LordFor(map, _seat);
			var y = inRect.y;

			Text.Font = GameFont.Medium;
			GUI.color = TitleColor;
			Widgets.Label(new Rect(inRect.x, y, inRect.width, 32f), "RimCourt.StandingTitle".Translate());
			GUI.color = Color.white;
			Text.Font = GameFont.Small;
			y += 34f;

			DrawLordBanner(new Rect(inRect.x, y, inRect.width, BannerH), mgr, map, lord);
			y += BannerH + 8f;

			var tabs = new Rect(inRect.x, y, inRect.width, 30f);
			var third = tabs.width / 3f;
			if (TabButton(new Rect(tabs.x, tabs.y, third, tabs.height),
				"RimCourt.StandingTabRank".Translate(), _tab == 0)) _tab = 0;
			if (TabButton(new Rect(tabs.x + third, tabs.y, third, tabs.height),
				"RimCourt.StandingTabRecord".Translate(), _tab == 1)) _tab = 1;
			if (TabButton(new Rect(tabs.x + 2f * third, tabs.y, third, tabs.height),
				"RimCourt.StandingTabBlood".Translate(), _tab == 2)) _tab = 2;
			y += 34f;

			var body = new Rect(inRect.x, y, inRect.width, inRect.yMax - y);
			if (_tab == 1)
			{
				var rview = new Rect(0f, 0f, body.width - 18f, _recordHeight);
				Widgets.BeginScrollView(body, ref _recordScroll, rview);
				var ry = 0f;
				DrawRecord(rview.width, ref ry, mgr, map, lord);
				_recordHeight = ry;
				Widgets.EndScrollView();
				return;
			}
			if (_tab == 2)
			{
				var bview = new Rect(0f, 0f, body.width - 18f, _bloodHeight);
				Widgets.BeginScrollView(body, ref _bloodScroll, bview);
				var by = 0f;
				DrawBlood(bview.width, ref by, mgr, map, lord);
				_bloodHeight = by;
				Widgets.EndScrollView();
				return;
			}

			var view = new Rect(0f, 0f, body.width - 18f, _viewHeight);
			Widgets.BeginScrollView(body, ref _scroll, view);

			var vy = 0f;
			DrawNameSection(view.width, ref vy, mgr.Reputation);
			DrawHallSection(view.width, ref vy, map);
			DrawLegitimacySection(view.width, ref vy, mgr, map, lord);
			_viewHeight = vy;

			Widgets.EndScrollView();
		}

		private static void DrawLordBanner(Rect rect, CourtManager mgr, Map map, Pawn lord)
		{
			Widgets.DrawMenuSection(rect);
			var inner = rect.ContractedBy(8f);
			DrawHandCorner(inner, mgr, map);

			if (lord == null)
			{
				GUI.color = MutedColor;
				Widgets.Label(new Rect(inner.x, inner.y, inner.width - 104f, inner.height),
					"RimCourt.StandingNoLord".Translate());
				GUI.color = Color.white;
				return;
			}

			DrawPortrait(new Rect(inner.x, inner.y, 72f, 72f), lord);
			var textX = inner.x + 82f;
			var textW = inner.width - 82f - 104f;

			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(textX, inner.y, textW, 28f), lord.LabelShortCap);
			Text.Font = GameFont.Small;

			GUI.color = HeaderColor;
			Widgets.Label(new Rect(textX, inner.y + 28f, textW, 22f), SeatClaim(mgr, map, lord));
			GUI.color = MutedColor;
			Widgets.Label(new Rect(textX, inner.y + 48f, textW, 22f), Credentials(lord));
			GUI.color = Color.white;

			LocateOnClick(new Rect(inner.x, inner.y, 72f, 72f), lord);
		}

		private static void DrawHandCorner(Rect inner, CourtManager mgr, Map map)
		{
			var zone = new Rect(inner.xMax - 96f, inner.y, 96f, inner.height);
			var icon = new Rect(zone.x + (zone.width - 40f) / 2f, zone.y + 2f, 40f, 40f);
			var hand = mgr.Hand;

			if (hand != null && !hand.Dead && hand.Spawned)
			{
				DrawPortrait(icon, hand);
			}
			else
			{
				Widgets.DrawBoxSolid(icon, PortraitBg);
				GUI.color = MutedColor;
				GUI.DrawTexture(icon.ContractedBy(5f), RimCourtIcons.Hand);
				GUI.color = Color.white;
			}

			Text.Font = GameFont.Tiny;
			Text.Anchor = TextAnchor.UpperCenter;
			if (hand != null)
			{
				Widgets.Label(new Rect(zone.x, icon.yMax + 3f, zone.width, 16f), hand.LabelShortCap);
				GUI.color = MutedColor;
				Widgets.Label(new Rect(zone.x, icon.yMax + 18f, zone.width, 15f),
					"RimCourt.StandingHandTag".Translate());
				GUI.color = Color.white;
			}
			else
			{
				GUI.color = MutedColor;
				Widgets.Label(new Rect(zone.x, icon.yMax + 3f, zone.width, 30f),
					"RimCourt.NameHandLabel".Translate());
				GUI.color = Color.white;
			}
			Text.Anchor = TextAnchor.UpperLeft;
			Text.Font = GameFont.Small;

			Widgets.DrawHighlightIfMouseover(zone);
			TooltipHandler.TipRegion(zone, "RimCourt.NameHandDesc".Translate());
			if (Widgets.ButtonInvisible(zone))
				Find.WindowStack.Add(new Dialog_PickLord(
					mgr.LordCandidates(map), mgr.Hand, null, null,
					picked => mgr.SetHand(picked),
					"RimCourt.PickHandTitle".Translate().ToString(),
					"RimCourt.PickHandDesc".Translate().ToString(),
					"RimCourt.PickHandNone".Translate().ToString()));
		}

		private static string SeatClaim(CourtManager mgr, Map map, Pawn lord)
		{
			if (mgr.AppointedLord == lord) return "RimCourt.StandingByWord".Translate();
			if (mgr.SeatOwner(map) == lord) return "RimCourt.StandingByThrone".Translate();
			if (mgr.Hand == lord) return "RimCourt.StandingByHand".Translate();
			return "RimCourt.StandingByPrestige".Translate();
		}

		private static string Credentials(Pawn p)
		{
			var title = p.royalty?.MostSeniorTitle;
			var titleText = title != null
				? title.def.GetLabelCapFor(p)
				: "RimCourt.PickLordNoTitle".Translate().ToString();
			var social = p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
			var age = p.ageTracker?.AgeBiologicalYears ?? 0;
			return "RimCourt.StandingCredentials".Translate(titleText, social, age);
		}

		private static void DrawNameSection(float width, ref float y, JudgeReputation rep)
		{
			SectionHeader(width, ref y, "RimCourt.StandingNameSection".Translate());

			Text.Font = GameFont.Medium;
			GUI.color = rep.Stage >= 0 ? AxisColors[rep.Stage] : MutedColor;
			Widgets.Label(new Rect(0f, y, width, 28f), rep.TitleKey.Translate());
			GUI.color = Color.white;
			Text.Font = GameFont.Small;
			y += 30f;

			var top = Mathf.Max(rep.Highest, 1f);
			for (var axis = JudgeReputation.StageJust; axis <= JudgeReputation.StageWeak; axis++)
				DrawAxis(width, ref y, rep, axis, top);

			y += 4f;
			GUI.color = MutedColor;
			var note = Explanation(rep);
			var h = Text.CalcHeight(note, width);
			Widgets.Label(new Rect(0f, y, width, h), note);
			GUI.color = Color.white;
			y += h + 10f;
		}

		private static void DrawAxis(float width, ref float y, JudgeReputation rep, int axis, float top)
		{
			var labelRect = new Rect(0f, y, 90f, 22f);
			var current = rep.Stage == axis;
			GUI.color = current ? AxisColors[axis] : MutedColor;
			Widgets.Label(labelRect, JudgeReputation.KeyOf(axis).Translate());
			GUI.color = Color.white;

			var score = rep.ScoreOf(axis);
			var barRect = new Rect(96f, y + 4f, width - 96f - 46f, 14f);
			Widgets.DrawBoxSolid(barRect, BarBg);
			var fill = Mathf.Clamp01(score / top);
			if (fill > 0f)
			{
				var c = AxisColors[axis];
				if (!current) c.a = 0.55f;
				Widgets.DrawBoxSolid(new Rect(barRect.x, barRect.y, barRect.width * fill, barRect.height), c);
			}
			Widgets.DrawBox(barRect);

			Text.Anchor = TextAnchor.MiddleRight;
			GUI.color = current ? Color.white : MutedColor;
			Widgets.Label(new Rect(barRect.xMax + 4f, y, 42f, 22f), score.ToString("0.0"));
			GUI.color = Color.white;
			Text.Anchor = TextAnchor.UpperLeft;
			y += 24f;
		}

		private static string Explanation(JudgeReputation rep)
		{
			if (rep.Stage == JudgeReputation.StageNone)
				return "RimCourt.StandingUntested".Translate(
					rep.Total.ToString("0.0"), JudgeReputation.EstablishedAt.ToString("0"));

			var effect = "RimCourt.StandingEffect" + rep.Stage;
			var line = effect.Translate().ToString();
			var rival = rep.Rival;
			if (rival != JudgeReputation.StageNone)
				line += "\n\n" + "RimCourt.StandingSwitch".Translate(
					rep.PointsToSwitch.ToString("0.0"), JudgeReputation.KeyOf(rival).Translate());
			return line + "\n\n" + ((RimCourtMod.Settings?.noDecay ?? false)
				? "RimCourt.StandingNoDecay".Translate()
				: "RimCourt.StandingDecay".Translate());
		}

		private void DrawHallSection(float width, ref float y, Map map)
		{
			SectionHeader(width, ref y, "RimCourt.StandingHallSection".Translate());

			var seat = _seat != null && _seat.Spawned ? _seat : HallUtility.SeatOn(map);
			var grade = HallUtility.GradeAt(seat);

			Text.Font = GameFont.Medium;
			GUI.color = grade >= HallUtility.GradeGreat ? TitleColor
				: (grade <= HallUtility.GradeOpen ? ThreatColor : Color.white);
			Widgets.Label(new Rect(0f, y, width, 28f), HallUtility.Label(grade));
			GUI.color = Color.white;
			Text.Font = GameFont.Small;
			y += 30f;

			GUI.color = MutedColor;
			var stats = "RimCourt.StandingHallScore".Translate(
				HallUtility.ScoreAt(seat).ToString("0")) + "   ·   " + HallUtility.Numbers(grade);
			Widgets.Label(new Rect(0f, y, width, 22f), stats);
			y += 24f;

			var note = HallUtility.Note(grade) + "\n\n" + "RimCourt.StandingHallHow".Translate();
			var h = Text.CalcHeight(note, width);
			Widgets.Label(new Rect(0f, y, width, h), note);
			GUI.color = Color.white;
			y += h + 10f;
		}

		private void RefreshRivals(Map map, Pawn lord)
		{
			var now = Find.TickManager.TicksGame;
			if (now - _rivalsAt < RivalCacheTicks) return;
			_rivalsAt = now;

			_rivals.Clear();
			_opinions.Clear();
			_heir = null;
			if (lord == null) return;

			foreach (var c in map.mapPawns.FreeColonistsSpawned)
			{
				if (c == lord || c.Dead) continue;
				if (c.ageTracker != null && c.ageTracker.AgeBiologicalYears < 16) continue;
				_rivals.Add(c);
			}
			_rivals.SortByDescending(c => PetitionWorker_Challenge.WouldRise(c, lord) ? 1f : 0f,
				c => PetitionWorker_Challenge.Prestige(c));

			for (var i = 0; i < _rivals.Count; i++)
			{
				_opinions.Add(_rivals[i].relations?.OpinionOf(lord) ?? 0);
				if (_heir == null && PetitionWorker_Challenge.WouldRise(_rivals[i], lord))
					_heir = _rivals[i];
			}
		}

		private void DrawLegitimacySection(float width, ref float y, CourtManager mgr, Map map, Pawn lord)
		{
			SectionHeader(width, ref y, "RimCourt.StandingThroneSection".Translate());

			var open = PetitionWorker_Challenge.ThroneContestable(mgr.Reputation.Stage);
			GUI.color = open ? ThreatColor : CalmColor;
			var head = open ? "RimCourt.StandingThroneOpen".Translate() : "RimCourt.StandingThroneShut".Translate();
			var hh = Text.CalcHeight(head, width);
			Widgets.Label(new Rect(0f, y, width, hh), head);
			GUI.color = Color.white;
			y += hh + 8f;

			if (lord == null) return;
			RefreshRivals(map, lord);

			if (_heir == null)
			{
				GUI.color = MutedColor;
				var none = "RimCourt.StandingNoRival".Translate(
					PetitionWorker_Challenge.MaxOpinion, PetitionWorker_Challenge.MinSocial);
				var nh = Text.CalcHeight(none, width);
				Widgets.Label(new Rect(0f, y, width, nh), none);
				GUI.color = Color.white;
				y += nh + 8f;
			}

			for (var i = 0; i < _rivals.Count && i < 8; i++)
				DrawRival(width, ref y, _rivals[i], _opinions[i], _rivals[i] == _heir && open);
		}

		private static void DrawRival(float width, ref float y, Pawn p, int opinion, bool isHeir)
		{
			var row = new Rect(0f, y, width, RowH);
			Widgets.DrawBoxSolid(row, RowBg);
			if (isHeir)
			{
				GUI.color = ThreatColor;
				Widgets.DrawBox(row);
				GUI.color = Color.white;
			}

			DrawPortrait(new Rect(row.x + 4f, row.y + 4f, RowH - 8f, RowH - 8f), p);
			var textX = row.x + RowH + 4f;
			var textW = row.width - RowH - 12f;

			Widgets.Label(new Rect(textX, row.y + 4f, textW, 22f), p.LabelShortCap);

			GUI.color = opinion <= PetitionWorker_Challenge.MaxOpinion ? ThreatColor : MutedColor;
			Text.Font = GameFont.Tiny;
			var social = p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
			var title = p.royalty?.MostSeniorTitle;
			var titleText = title != null
				? title.def.GetLabelCapFor(p)
				: "RimCourt.PickLordNoTitle".Translate().ToString();
			Widgets.Label(new Rect(textX, row.y + 24f, textW, 20f),
				"RimCourt.StandingRival".Translate(titleText, social, opinion.ToStringWithSign()));
			Text.Font = GameFont.Small;
			GUI.color = Color.white;

			if (isHeir)
			{
				Text.Anchor = TextAnchor.MiddleRight;
				GUI.color = ThreatColor;
				Widgets.Label(new Rect(row.xMax - 150f, row.y, 146f, RowH), "RimCourt.StandingWouldRise".Translate());
				GUI.color = Color.white;
				Text.Anchor = TextAnchor.UpperLeft;
			}

			LocateOnClick(row, p);
			y += RowH + 4f;
		}

		private void RefreshKin(Map map, Pawn lord)
		{
			var now = Find.TickManager.TicksGame;
			if (now - _kinAt < RivalCacheTicks) return;
			_kinAt = now;

			_kin.Clear();
			_kinLabels.Clear();
			_bloodHeir = null;
			if (lord == null) return;

			_bloodHeir = CourtManager.HeirOf(lord);

			foreach (var c in map.mapPawns.FreeColonistsSpawned)
			{
				if (c == lord || c.Dead || c.relations == null || lord.relations == null) continue;
				var rel = lord.GetMostImportantRelation(c);
				if (rel == null || !rel.familyByBloodRelation) continue;
				_kin.Add(c);
				_kinLabels.Add(rel.GetGenderSpecificLabelCap(c));
			}
		}

		private void DrawBlood(float width, ref float y, CourtManager mgr, Map map, Pawn lord)
		{
			if (lord == null)
			{
				Faded(width, ref y, "RimCourt.StandingNoLord".Translate());
				return;
			}
			RefreshKin(map, lord);

			SectionHeader(width, ref y, "RimCourt.BloodHeirSection".Translate());
			if (_bloodHeir != null)
			{
				DrawKinRow(width, ref y, _bloodHeir, "RimCourt.BloodHeirTag".Translate(), true);
				Faded(width, ref y, "RimCourt.BloodHeirNote".Translate(_bloodHeir.LabelShortCap));
			}
			else
			{
				Faded(width, ref y, "RimCourt.BloodNoHeir".Translate());
			}

			y += 6f;
			SectionHeader(width, ref y, "RimCourt.BloodKinSection".Translate(lord.LabelShortCap));
			if (_kin.Count == 0)
			{
				Faded(width, ref y, "RimCourt.BloodNoKin".Translate(lord.LabelShortCap));
				return;
			}
			for (var i = 0; i < _kin.Count && i < 12; i++)
				DrawKinRow(width, ref y, _kin[i], _kinLabels[i], _kin[i] == _bloodHeir);
		}

		private static void DrawKinRow(float width, ref float y, Pawn p, string tag, bool isHeir)
		{
			var row = new Rect(0f, y, width, 40f);
			Widgets.DrawBoxSolid(row, RowBg);
			if (isHeir)
			{
				GUI.color = TitleColor;
				Widgets.DrawBox(row);
				GUI.color = Color.white;
			}

			DrawPortrait(new Rect(row.x + 4f, row.y + 4f, 32f, 32f), p);
			Widgets.Label(new Rect(row.x + 44f, row.y + 3f, row.width - 200f, 22f), p.LabelShortCap);

			GUI.color = isHeir ? TitleColor : MutedColor;
			Text.Font = GameFont.Tiny;
			Widgets.Label(new Rect(row.x + 44f, row.y + 22f, row.width - 200f, 18f), tag);
			Text.Anchor = TextAnchor.MiddleRight;
			Widgets.Label(new Rect(row.xMax - 150f, row.y, 146f, row.height),
				"RimCourt.BloodYears".Translate(p.ageTracker?.AgeBiologicalYears ?? 0));
			Text.Anchor = TextAnchor.UpperLeft;
			Text.Font = GameFont.Small;
			GUI.color = Color.white;

			LocateOnClick(row, p);
			y += 44f;
		}

		private static bool TabButton(Rect r, string label, bool active)
		{
			if (active) Widgets.DrawBoxSolid(r, TabActiveBg);
			GUI.color = active ? TitleColor : MutedColor;
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(r, label);
			Text.Anchor = TextAnchor.UpperLeft;
			GUI.color = Color.white;
			Widgets.DrawHighlightIfMouseover(r);
			return Widgets.ButtonInvisible(r) && !active;
		}

		private void DrawRecord(float width, ref float y, CourtManager mgr, Map map, Pawn lord)
		{
			SectionHeader(width, ref y, "RimCourt.RecordCasesSection".Translate());

			RefreshRows(mgr);
			if (_rows.Count == 0)
			{
				Faded(width, ref y, "RimCourt.RecordEmpty".Translate());
			}
			else
			{
				for (var i = 0; i < _rows.Count; i++)
					DrawCaseRow(width, ref y, _rows[i]);
			}

			y += 6f;
			SectionHeader(width, ref y, "RimCourt.RecordGrievancesSection".Translate());
			RefreshAggrieved(map, lord);

			if (_aggrieved.Count == 0)
			{
				Faded(width, ref y, "RimCourt.RecordNoGrievance".Translate());
				return;
			}
			for (var i = 0; i < _aggrieved.Count; i++)
				DrawGrievanceRow(width, ref y, _aggrieved[i], _grievances[i]);
		}

		private void RefreshAggrieved(Map map, Pawn lord)
		{
			var now = Find.TickManager.TicksGame;
			if (now - _aggrievedAt < RivalCacheTicks) return;
			_aggrievedAt = now;

			_aggrieved.Clear();
			_grievances.Clear();
			if (lord == null) return;

			foreach (var c in map.mapPawns.FreeColonistsSpawned)
			{
				if (c == lord || c.Dead) continue;
				var n = PetitionWorker_AgainstLord.GrievanceCount(c, lord);
				if (n <= 0) continue;
				_aggrieved.Add(c);
				_grievances.Add(n);
			}
			for (var i = 0; i < _aggrieved.Count; i++)
				for (var j = i + 1; j < _aggrieved.Count; j++)
					if (_grievances[j] > _grievances[i])
					{
						var p = _aggrieved[i]; _aggrieved[i] = _aggrieved[j]; _aggrieved[j] = p;
						var n = _grievances[i]; _grievances[i] = _grievances[j]; _grievances[j] = n;
					}
		}

		private void RefreshRows(CourtManager mgr)
		{
			var now = Find.TickManager.TicksGame;
			if (now - _rowsAt < RivalCacheTicks) return;
			_rowsAt = now;

			_rows.Clear();
			var heard = mgr.Heard;
			for (var i = heard.Count - 1; i >= 0 && i >= heard.Count - 12; i--)
			{
				var h = heard[i];
				var def = DefDatabase<PetitionDef>.GetNamedSilentFail(h.defName);
				var axis = CourtManager.PrecedentAxis(def, h);
				var days = Mathf.FloorToInt((now - h.tick) / 60000f);
				var who = h.bName.NullOrEmpty()
					? (h.aName ?? "?")
					: "RimCourt.RecordPair".Translate(h.aName ?? "?", h.bName).ToString();
				var how = axis >= 0
					? ("RimCourt.Precedent" + axis).Translate().ToString()
					: "RimCourt.RecordNoAnswer".Translate().ToString();
				_rows.Add(new RecordRow
				{
					title = def?.LabelCap ?? h.defName ?? "?",
					detail = "RimCourt.RecordRow".Translate(who, how).ToString(),
					when = days <= 0
						? "RimCourt.RecordToday".Translate().ToString()
						: "RimCourt.RecordDaysAgo".Translate(days).ToString(),
					axis = axis,
				});
			}
		}

		private static void DrawCaseRow(float width, ref float y, RecordRow r)
		{
			var row = new Rect(0f, y, width, 40f);
			Widgets.DrawBoxSolid(row, RowBg);

			GUI.color = MutedColor;
			Text.Font = GameFont.Tiny;
			Text.Anchor = TextAnchor.MiddleRight;
			Widgets.Label(new Rect(row.xMax - 96f, row.y, 92f, row.height), r.when);
			Text.Anchor = TextAnchor.UpperLeft;
			Text.Font = GameFont.Small;
			GUI.color = Color.white;

			Widgets.Label(new Rect(row.x + 8f, row.y + 2f, row.width - 110f, 22f), r.title);

			Text.Font = GameFont.Tiny;
			GUI.color = r.axis >= 0 ? AxisColors[r.axis] : MutedColor;
			Widgets.Label(new Rect(row.x + 8f, row.y + 21f, row.width - 110f, 18f), r.detail);
			Text.Font = GameFont.Small;
			GUI.color = Color.white;

			y += 44f;
		}

		private static void DrawGrievanceRow(float width, ref float y, Pawn p, int count)
		{
			var row = new Rect(0f, y, width, 34f);
			var willAccuse = count >= PetitionWorker_AgainstLord.MinGrievances;
			Widgets.DrawBoxSolid(row, RowBg);
			if (willAccuse)
			{
				GUI.color = ThreatColor;
				Widgets.DrawBox(row);
				GUI.color = Color.white;
			}

			Widgets.Label(new Rect(row.x + 8f, row.y + 6f, row.width - 200f, 22f), p.LabelShortCap);

			GUI.color = willAccuse ? ThreatColor : MutedColor;
			Text.Anchor = TextAnchor.MiddleRight;
			Text.Font = GameFont.Tiny;
			Widgets.Label(new Rect(row.xMax - 190f, row.y, 186f, row.height),
				willAccuse
					? "RimCourt.RecordWillAccuse".Translate()
					: "RimCourt.RecordGrievance".Translate(count, PetitionWorker_AgainstLord.MinGrievances));
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.UpperLeft;
			GUI.color = Color.white;

			LocateOnClick(row, p);
			y += 38f;
		}

		private static void Faded(float width, ref float y, string text)
		{
			GUI.color = MutedColor;
			var h = Text.CalcHeight(text, width);
			Widgets.Label(new Rect(0f, y, width, h), text);
			GUI.color = Color.white;
			y += h + 6f;
		}

		private static void SectionHeader(float width, ref float y, string label)
		{
			y += 4f;
			GUI.color = HeaderColor;
			Widgets.Label(new Rect(0f, y, width, 24f), label);
			GUI.color = Color.white;
			y += 24f;
			Widgets.DrawLineHorizontal(0f, y, width);
			y += 8f;
		}

		private static void DrawPortrait(Rect rect, Pawn p)
		{
			Widgets.DrawBoxSolid(rect, PortraitBg);
			if (p == null || p.Dead || !p.Spawned) return;
			var tex = PortraitsCache.Get(p, new Vector2(rect.width, rect.height), Rot4.South, default, 1f);
			GUI.DrawTexture(rect, tex);
		}

		private static void LocateOnClick(Rect rect, Pawn p)
		{
			if (p == null) return;
			TooltipHandler.TipRegion(rect, new TipSignal("RimCourt.LocateTip".Translate(),
				p.thingIDNumber ^ 0x11CE07));
			if (Widgets.ButtonInvisible(rect)) CameraJumper.TryJumpAndSelect(p);
		}
	}
}
