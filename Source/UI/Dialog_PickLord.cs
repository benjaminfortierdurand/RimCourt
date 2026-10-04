using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimCourt.UI
{
	public class Dialog_PickLord : Window
	{
		private static readonly Color Desk = new Color(0.153f, 0.118f, 0.09f);
		private static readonly Color RowBack = new Color(1f, 1f, 1f, 0.05f);
		private static readonly Color RowHover = new Color(1f, 1f, 1f, 0.11f);
		private static readonly Color RowPicked = new Color(0.74f, 0.61f, 0.25f, 0.22f);
		private static readonly Color Faint = new Color(0.72f, 0.68f, 0.58f);

		private const float RowHeight = 62f;

		private readonly List<Pawn> candidates;
		private readonly Pawn current;
		private readonly Pawn automatic;
		private readonly Pawn seatOwner;
		private readonly Action<Pawn> onPicked;
		private Vector2 scroll;

		public override Vector2 InitialSize => new Vector2(580f, 560f);

		private readonly string titleOverride;
		private readonly string descOverride;
		private readonly string autoOverride;

		public Dialog_PickLord(List<Pawn> candidates, Pawn current, Pawn automatic, Pawn seatOwner,
			Action<Pawn> onPicked, string titleOverride = null, string descOverride = null,
			string autoOverride = null)
		{
			this.candidates = candidates;
			this.current = current;
			this.automatic = automatic;
			this.seatOwner = seatOwner;
			this.onPicked = onPicked;
			this.titleOverride = titleOverride;
			this.descOverride = descOverride;
			this.autoOverride = autoOverride;
			forcePause = true;
			absorbInputAroundWindow = true;
			doCloseX = true;
			draggable = true;
			preventCameraMotion = false;
		}

		public override void DoWindowContents(Rect inRect)
		{
			Widgets.DrawBoxSolid(inRect, Desk);
			var pad = inRect.ContractedBy(14f);
			var y = pad.y;

			Text.Font = GameFont.Medium;
			GUI.color = Color.white;
			Widgets.Label(new Rect(pad.x, y, pad.width, 34f),
				titleOverride ?? "RimCourt.PickLordTitle".Translate().ToString());
			y += 36f;

			Text.Font = GameFont.Small;
			GUI.color = Faint;
			var desc = descOverride != null
				? (TaggedString)descOverride
				: seatOwner != null
					? "RimCourt.PickLordThroneDesc".Translate(seatOwner.LabelShortCap)
					: "RimCourt.PickLordDesc".Translate();
			var descH = Text.CalcHeight(desc, pad.width);
			Widgets.Label(new Rect(pad.x, y, pad.width, descH), desc);
			y += descH + 10f;
			GUI.color = Color.white;

			const float footerH = 40f;
			var listRect = new Rect(pad.x, y, pad.width, pad.yMax - y - footerH - 10f);
			var viewRect = new Rect(0f, 0f, listRect.width - 18f, candidates.Count * (RowHeight + 4f));
			Widgets.BeginScrollView(listRect, ref scroll, viewRect);
			var ry = 0f;
			for (var i = 0; i < candidates.Count; i++)
			{
				DrawCandidate(new Rect(0f, ry, viewRect.width, RowHeight), candidates[i]);
				ry += RowHeight + 4f;
			}
			Widgets.EndScrollView();

			var autoRect = new Rect(pad.x, pad.yMax - footerH, pad.width, footerH - 6f);
			if (Widgets.ButtonText(autoRect, autoOverride ?? "RimCourt.PickLordAuto".Translate(
				automatic?.LabelShortCap ?? "?").ToString()))
			{
				Close();
				onPicked(null);
			}
		}

		private void DrawCandidate(Rect row, Pawn pawn)
		{
			var picked = pawn == current;
			Widgets.DrawBoxSolid(row, picked ? RowPicked : (Mouse.IsOver(row) ? RowHover : RowBack));

			var frame = new Rect(row.x + 4f, row.y + 4f, RowHeight - 8f, RowHeight - 8f);
			if (pawn != null && !pawn.Destroyed)
			{
				var tex = PortraitsCache.Get(pawn, new Vector2(frame.width, frame.height), Rot4.South,
					new Vector3(0f, 0f, 0.3f), 1.6f, supersample: true, compensateForUIScale: true,
					renderHeadgear: false);
				GUI.DrawTexture(frame, tex);
			}

			var textX = frame.xMax + 10f;
			var textW = row.width - (textX - row.x) - 10f;

			GUI.color = Color.white;
			Text.Anchor = TextAnchor.UpperLeft;
			Widgets.Label(new Rect(textX, row.y + 8f, textW, 24f), pawn.LabelShortCap);

			GUI.color = Faint;
			Text.Font = GameFont.Tiny;
			Widgets.Label(new Rect(textX, row.y + 30f, textW, 22f), Qualifier(pawn));
			Text.Font = GameFont.Small;
			GUI.color = Color.white;

			if (Widgets.ButtonInvisible(row))
			{
				Close();
				onPicked(pawn);
			}
		}

		private string Qualifier(Pawn pawn)
		{
			var social = pawn.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
			var title = pawn.royalty?.MostSeniorTitle;
			var titleText = title != null
				? title.def.GetLabelCapFor(pawn)
				: "RimCourt.PickLordNoTitle".Translate().ToString();
			var line = "RimCourt.PickLordRow".Translate(titleText, social).ToString();
			if (pawn == seatOwner) line += "  ·  " + "RimCourt.PickLordSeatOwner".Translate();
			return line;
		}
	}
}
