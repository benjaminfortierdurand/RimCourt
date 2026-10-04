using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimCourt.UI
{
	public class Dialog_PickFlogger : Window
	{
		private static readonly Color Desk = new Color(0.153f, 0.118f, 0.09f);
		private static readonly Color RowBack = new Color(1f, 1f, 1f, 0.05f);
		private static readonly Color RowHover = new Color(1f, 1f, 1f, 0.11f);
		private static readonly Color Faint = new Color(0.72f, 0.68f, 0.58f);

		private const float RowHeight = 62f;

		private readonly Pawn condemned;
		private readonly List<Pawn> candidates;
		private readonly Pawn lord;
		private readonly Pawn wronged;
		private readonly Action<Pawn> onPicked;
		private readonly string titleKey;
		private readonly string descKey;
		private readonly string noneKey;
		private readonly bool showMelee;
		private Vector2 scroll;

		public override Vector2 InitialSize => new Vector2(580f, 540f);

		public Dialog_PickFlogger(Pawn condemned, List<Pawn> candidates, Pawn lord, Pawn wronged,
			Action<Pawn> onPicked)
			: this(condemned, candidates, lord, wronged, onPicked,
				"RimCourt.FloggerDialogTitle", "RimCourt.FloggerDialogDesc", "RimCourt.FloggerNoOne", true)
		{
		}

		public Dialog_PickFlogger(Pawn condemned, List<Pawn> candidates, Pawn lord, Pawn wronged,
			Action<Pawn> onPicked, string titleKey, string descKey, string noneKey, bool showMelee)
		{
			this.titleKey = titleKey;
			this.descKey = descKey;
			this.noneKey = noneKey;
			this.showMelee = showMelee;
			this.condemned = condemned;
			this.candidates = candidates;
			this.lord = lord;
			this.wronged = wronged;
			this.onPicked = onPicked;
			forcePause = true;
			absorbInputAroundWindow = true;
			closeOnClickedOutside = false;
			closeOnAccept = false;
			closeOnCancel = false;
			doCloseX = false;
			preventCameraMotion = false;
		}

		public override void DoWindowContents(Rect inRect)
		{
			Widgets.DrawBoxSolid(inRect, Desk);
			var pad = inRect.ContractedBy(14f);
			var y = pad.y;

			Text.Font = GameFont.Medium;
			GUI.color = Color.white;
			Widgets.Label(new Rect(pad.x, y, pad.width, 34f), titleKey.Translate());
			y += 36f;

			Text.Font = GameFont.Small;
			GUI.color = Faint;
			var desc = descKey.Translate(condemned.LabelShortCap);
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
				var row = new Rect(0f, ry, viewRect.width, RowHeight);
				DrawCandidate(row, candidates[i]);
				ry += RowHeight + 4f;
			}
			Widgets.EndScrollView();

			var noneRect = new Rect(pad.x, pad.yMax - footerH, pad.width, footerH - 6f);
			if (Widgets.ButtonText(noneRect, noneKey.Translate()))
			{
				Close();
				onPicked(null);
			}
		}

		private void DrawCandidate(Rect row, Pawn pawn)
		{
			var hover = Mouse.IsOver(row);
			Widgets.DrawBoxSolid(row, hover ? RowHover : RowBack);

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

			Text.Anchor = TextAnchor.UpperLeft;
			GUI.color = Color.white;
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
			var melee = pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
			var opinion = pawn.relations?.OpinionOf(condemned) ?? 0;
			string who;
			if (pawn == lord) who = "RimCourt.FloggerLord".Translate(pawn.LabelShortCap);
			else if (pawn == wronged) who = "RimCourt.FloggerWronged".Translate(pawn.LabelShortCap);
			else who = "RimCourt.FloggerAny".Translate(pawn.LabelShortCap);
			return showMelee
				? "RimCourt.FloggerRowInfo".Translate(who, melee, opinion.ToStringWithSign())
				: "RimCourt.EscortRowInfo".Translate(who, opinion.ToStringWithSign());
		}
	}
}
