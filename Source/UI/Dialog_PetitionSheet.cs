using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimCourt.UI
{
	public class Dialog_PetitionSheet : Window
	{
		private static readonly Color Parchment = new Color(0.867f, 0.812f, 0.663f);
		private static readonly Color Ink = new Color(0.161f, 0.129f, 0.078f);
		private static readonly Color InkFaint = new Color(0.451f, 0.384f, 0.243f);
		private static readonly Color Wax = new Color(0.494f, 0.153f, 0.125f);

		private string _precedent;
		private bool _precedentDone;
		private string _kin;
		private bool _kinDone;
		private string _caseText;
		private string[] _verdictLabels;

		private static readonly Color Desk = new Color(0.153f, 0.118f, 0.09f);
		private static readonly Color SheetShadow = new Color(0f, 0f, 0f, 0.35f);

		private const float PortraitZoom = 2.3f;
		private const float PortraitRise = 0.42f;
		private const float RowHeight = 46f;

		public readonly int petitionId;
		private bool armed;

		public override Vector2 InitialSize => new Vector2(620f, 710f);

		public Dialog_PetitionSheet(int petitionId)
		{
			this.petitionId = petitionId;
			forcePause = true;
			absorbInputAroundWindow = true;
			doCloseX = true;
			draggable = true;
			preventCameraMotion = false;
		}

		public static void OpenFor(int petitionId)
		{
			var open = Find.WindowStack.Windows.ToList();
			for (var i = 0; i < open.Count; i++)
				if (open[i] is Dialog_PetitionSheet d && d.petitionId == petitionId) return;
			Find.WindowStack.Add(new Dialog_PetitionSheet(petitionId));
		}

		public override void DoWindowContents(Rect inRect)
		{
			var mgr = Court.CourtManager.Instance;
			var p = mgr?.GetPetition(petitionId);
			if (p == null || p.state != Court.Petition.StateAwaitingVerdict)
			{
				Close();
				return;
			}

			Widgets.DrawBoxSolid(inRect, Desk);
			const float toolsH = 64f;
			const float buttonH = 36f;
			var sheet = new Rect(inRect.x + 12f, inRect.y + 14f, inRect.width - 24f,
				inRect.height - toolsH - buttonH - 44f);

			var matrix = GUI.matrix;
			Verse.UI.RotateAroundPivot(-1.2f, sheet.center);
			if (RimCourtTex.Sheet != null)
			{
				GUI.color = Color.white;
				var ex = sheet.width * (20f / 1080f);
				var ey = sheet.height * (20f / 760f);
				GUI.DrawTexture(new Rect(sheet.x - ex, sheet.y - ey, sheet.width + ex * 2f, sheet.height + ey * 2f),
					RimCourtTex.Sheet);
			}
			else
			{
				Widgets.DrawBoxSolid(new Rect(sheet.x + 4f, sheet.y + 6f, sheet.width, sheet.height), SheetShadow);
			}
			DrawSheet(sheet, mgr, p);
			GUI.matrix = matrix;

			var tools = new Rect(inRect.x, sheet.yMax + 10f, inRect.width, toolsH);
			DrawStampTool(tools, mgr, p);

			if (p.stampedIndex >= 0)
			{
				var btn = new Rect(inRect.x + inRect.width / 2f - 130f, tools.yMax + 2f, 260f, buttonH);
				if (Widgets.ButtonText(btn, "RimCourt.SheetPronounce".Translate()))
				{
					var chosen = p.stampedIndex;
					Close();
					mgr.ChooseVerdict(petitionId, chosen);
					return;
				}
			}

			HandleStampInput(sheet, p);
		}

		private void DrawSheet(Rect sheet, Court.CourtManager mgr, Court.Petition p)
		{
			if (RimCourtTex.Sheet == null)
			{
				Widgets.DrawBoxSolid(sheet, Parchment);
				Widgets.DrawBox(sheet, 2, RimCourtTex.ParchmentEdge);
				Widgets.DrawBox(sheet.ContractedBy(7f), 1, RimCourtTex.ParchmentDeep);
			}

			var body = sheet.ContractedBy(24f);
			var y = body.y;

			Text.Anchor = TextAnchor.UpperCenter;
			Text.Font = GameFont.Medium;
			GUI.color = Ink;
			Widgets.Label(new Rect(body.x, y, body.width, 32f), "RimCourt.SheetTitle".Translate());
			y += 32f;
			Text.Font = GameFont.Small;
			GUI.color = InkFaint;
			Widgets.Label(new Rect(body.x, y, body.width, 24f), p.def.LabelCap);
			y += 28f;
			Text.Anchor = TextAnchor.UpperLeft;

			GUI.color = RimCourtTex.ParchmentDeepColor;
			Widgets.DrawLineHorizontal(body.x, y, body.width);
			GUI.color = Color.white;
			y += 10f;

			const float portraitW = 118f;
			var rowCount = Mathf.Max(1, p.def.verdicts.Count);
			var rowsTop = body.yMax - rowCount * RowHeight - 26f;
			var precedent = PrecedentLine(mgr, p);
			var kin = KinLine(mgr, p);
			var noteH = 22f;
			if (!precedent.NullOrEmpty()) noteH += 18f;
			if (!kin.NullOrEmpty()) noteH += 18f;
			var caseRect = new Rect(body.x, y, body.width - portraitW - 16f, rowsTop - noteH - y - 6f);
			if (_caseText == null) _caseText = p.def.Worker.CaseText(p);
			var caseText = _caseText;
			GUI.color = Ink;
			Text.Font = GameFont.Small;
			if (Text.CalcHeight(caseText, caseRect.width) > caseRect.height)
				Text.Font = GameFont.Tiny;
			Widgets.Label(caseRect, caseText);
			Text.Font = GameFont.Small;

			DrawPortrait(new Rect(body.xMax - portraitW, y, portraitW, portraitW), p.petitioner,
				"RimCourt.SheetPetitioner".Translate());
			if (p.other != null)
				DrawPortrait(new Rect(body.xMax - portraitW, y + portraitW + 24f, portraitW, portraitW), p.other,
					"RimCourt.SheetDefendant".Translate());

			GUI.color = InkFaint;
			Text.Font = GameFont.Tiny;
			var noteY = rowsTop - 20f;
			if (!precedent.NullOrEmpty())
			{
				noteY -= 18f;
				Widgets.Label(new Rect(body.x, noteY, body.width, 20f), precedent);
			}
			if (!kin.NullOrEmpty())
			{
				noteY -= 18f;
				GUI.color = Wax;
				Widgets.Label(new Rect(body.x, noteY, body.width, 20f), kin);
				GUI.color = InkFaint;
			}
			Widgets.Label(new Rect(body.x, rowsTop - 20f, body.width, 20f), "RimCourt.SheetVerdicts".Translate());
			Text.Font = GameFont.Small;

			for (var i = 0; i < rowCount; i++)
			{
				var row = VerdictRowRect(sheet, i, rowCount);
				GUI.color = RimCourtTex.ParchmentDeepColor;
				Widgets.DrawLineHorizontal(row.x + 8f, row.yMax - 6f, row.width - 16f);
				GUI.color = Ink;
				Text.Anchor = TextAnchor.MiddleLeft;
				if (_verdictLabels == null)
				{
					_verdictLabels = new string[rowCount];
					for (var k = 0; k < rowCount; k++)
						_verdictLabels[k] = (k + 1) + ". " + p.def.Worker.VerdictLabel(p, k);
				}
				Widgets.Label(new Rect(row.x + 12f, row.y, row.width - 24f, row.height - 6f),
					_verdictLabels[i]);
				Text.Anchor = TextAnchor.UpperLeft;
			}

			for (var i = 0; i < p.stamps.Count; i++)
			{
				var mark = p.stamps[i];
				if (mark.row < 0 || mark.row >= rowCount) continue;
				var last = i == p.stamps.Count - 1;
				DrawMarkAt(VerdictRowRect(sheet, mark.row, rowCount), mark.x, mark.angle, last ? 0.72f : 0.3f);
			}

			GUI.color = InkFaint;
			Text.Font = GameFont.Tiny;
			Text.Anchor = TextAnchor.LowerLeft;
			Widgets.Label(new Rect(body.x, body.yMax - 2f, body.width, 18f),
				"RimCourt.SheetFooter".Translate(mgr.Lord?.LabelShortCap ?? ""));
			Text.Anchor = TextAnchor.UpperLeft;
			Text.Font = GameFont.Small;
			GUI.color = Color.white;
		}

		private static void DrawPortrait(Rect frame, Pawn pawn, string caption)
		{
			GUI.color = Color.white;
			var matrix = GUI.matrix;
			Verse.UI.RotateAroundPivot(-3f, frame.center);
			if (RimCourtTex.Photo != null)
			{
				var ex = frame.width * (10f / 320f);
				GUI.DrawTexture(new Rect(frame.x - ex, frame.y - ex, frame.width + ex * 2f, frame.height + ex * 2f),
					RimCourtTex.Photo);
			}
			else
			{
				Widgets.DrawBoxSolid(frame, new Color(0.925f, 0.894f, 0.796f));
				Widgets.DrawBox(frame, 1, RimCourtTex.ParchmentEdge);
			}
			if (pawn != null && !pawn.Destroyed)
			{
				var tex = PortraitsCache.Get(pawn,
					new Vector2(frame.width - 10f, frame.height - 10f), Rot4.South,
					new Vector3(0f, 0f, PortraitRise), PortraitZoom,
					supersample: true, compensateForUIScale: true, renderHeadgear: false);
				GUI.DrawTexture(frame.ContractedBy(5f), tex);
			}
			Text.Anchor = TextAnchor.UpperCenter;
			Text.Font = GameFont.Tiny;
			GUI.color = InkFaint;
			Widgets.Label(new Rect(frame.x - 10f, frame.yMax + 2f, frame.width + 20f, 20f), caption);
			GUI.matrix = matrix;
			Text.Anchor = TextAnchor.UpperLeft;
			Text.Font = GameFont.Small;
			GUI.color = Color.white;
		}

		private string KinLine(Court.CourtManager mgr, Court.Petition p)
		{
			if (_kinDone) return _kin;
			_kinDone = true;
			var lord = mgr.CurrentLord;
			var a = Court.CourtManager.KinToLord(p.petitioner, lord);
			var b = Court.CourtManager.KinToLord(p.other, lord);
			if (a != null && b != null)
				_kin = "RimCourt.SheetKinBoth".Translate(
					p.petitioner.LabelShortCap, a.GetGenderSpecificLabel(p.petitioner),
					p.other.LabelShortCap, b.GetGenderSpecificLabel(p.other)).ToString();
			else if (a != null)
				_kin = "RimCourt.SheetKin".Translate(
					p.petitioner.LabelShortCap, a.GetGenderSpecificLabel(p.petitioner)).ToString();
			else if (b != null)
				_kin = "RimCourt.SheetKin".Translate(
					p.other.LabelShortCap, b.GetGenderSpecificLabel(p.other)).ToString();
			return _kin;
		}

		private string PrecedentLine(Court.CourtManager mgr, Court.Petition p)
		{
			if (_precedentDone) return _precedent;
			_precedentDone = true;
			var past = mgr.PrecedentFor(p.def);
			var axis = Court.CourtManager.PrecedentAxis(p.def, past);
			if (axis != Court.JudgeReputation.StageNone)
				_precedent = "RimCourt.SheetPrecedent".Translate(
					("RimCourt.Precedent" + axis).Translate()).ToString();
			return _precedent;
		}

		private static Rect VerdictRowRect(Rect sheet, int index, int count)
		{
			var body = sheet.ContractedBy(24f);
			return new Rect(body.x, body.yMax - (count - index) * RowHeight - 4f, body.width, RowHeight);
		}

		private void DrawStampTool(Rect area, Court.CourtManager mgr, Court.Petition p)
		{
			const float size = 54f;
			var r = new Rect(area.x + area.width / 2f - size / 2f, area.y + 4f, size, size);
			if (armed) Widgets.DrawBoxSolid(r.ExpandedBy(4f), new Color(1f, 1f, 1f, 0.14f));
			else if (Mouse.IsOver(r)) Widgets.DrawBoxSolid(r.ExpandedBy(2f), new Color(1f, 1f, 1f, 0.07f));
			GUI.color = Color.white;
			if (RimCourtTex.Stamp != null) GUI.DrawTexture(r, RimCourtTex.Stamp);
			else Widgets.DrawBoxSolid(r.ContractedBy(10f), Wax);
			TooltipHandler.TipRegion(r, "RimCourt.StampTip".Translate());
			if (Widgets.ButtonInvisible(r)) armed = !armed;

			Text.Anchor = TextAnchor.MiddleLeft;
			Text.Font = GameFont.Tiny;
			GUI.color = new Color(0.8f, 0.75f, 0.6f);
			Widgets.Label(new Rect(area.x + 12f, area.y, r.x - area.x - 20f, area.height),
				"RimCourt.SheetAwait".Translate(mgr.HoursLeft(p).ToString("F1")));
			Text.Anchor = TextAnchor.UpperLeft;
			Text.Font = GameFont.Small;
			GUI.color = Color.white;
		}

		private void HandleStampInput(Rect sheet, Court.Petition p)
		{
			if (!armed) return;
			var m = Event.current.mousePosition;
			var hoverRow = -1;
			var rowCount = Mathf.Max(1, p.def.verdicts.Count);
			for (var i = 0; i < rowCount; i++)
				if (VerdictRowRect(sheet, i, rowCount).Contains(m)) { hoverRow = i; break; }

			if (hoverRow >= 0)
			{
				var row = VerdictRowRect(sheet, hoverRow, rowCount);
				DrawMarkAt(row, Mathf.InverseLerp(row.x, row.xMax, m.x), -8f, 0.35f);
			}
			else
			{
				var tex = RimCourtTex.Stamp;
				if (tex != null)
				{
					GUI.color = new Color(1f, 1f, 1f, 0.85f);
					GUI.DrawTexture(new Rect(m.x - 20f, m.y - 34f, 40f, 40f), tex);
					GUI.color = Color.white;
				}
			}

			if (Event.current.type != EventType.MouseDown) return;
			if (Event.current.button == 1)
			{
				armed = false;
				Event.current.Use();
				return;
			}
			if (Event.current.button != 0) return;
			if (hoverRow < 0)
			{
				armed = false;
				return;
			}
			var target = VerdictRowRect(sheet, hoverRow, rowCount);
			if (p.stampedIndex != hoverRow) p.stamps.Clear();
			p.stamps.Add(new Court.VerdictStampMark
			{
				row = hoverRow,
				x = Mathf.Clamp(Mathf.InverseLerp(target.x, target.xMax, m.x), 0.15f, 0.85f),
				angle = -8f + Rand.Range(-5f, 5f),
			});
			if (p.stamps.Count > 6) p.stamps.RemoveAt(0);
			p.stampedIndex = hoverRow;
			armed = false;
			SoundDefOf.Designate_PlaceBuilding.PlayOneShotOnCamera();
			Event.current.Use();
		}

		private static void DrawMarkAt(Rect row, float nx, float angle, float alpha)
		{
			const float w = 132f;
			const float h = 42f;
			var r = new Rect(row.x + nx * row.width - w / 2f, row.center.y - h / 2f, w, h);
			var matrix = GUI.matrix;
			Verse.UI.RotateAroundPivot(angle, r.center);
			var ink = Wax;
			ink.a = alpha;
			GUI.color = ink;
			if (RimCourtTex.StampMark != null) GUI.DrawTexture(r, RimCourtTex.StampMark);
			Text.Anchor = TextAnchor.MiddleCenter;
			Text.Font = GameFont.Small;
			Widgets.Label(r, "RimCourt.StampSealed".Translate());
			Text.Anchor = TextAnchor.UpperLeft;
			GUI.color = Color.white;
			GUI.matrix = matrix;
		}
	}
}
