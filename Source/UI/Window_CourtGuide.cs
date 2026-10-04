using UnityEngine;
using Verse;

namespace RimCourt.UI
{
	public class Window_CourtGuide : Window
	{
		private static readonly string[] Sections =
			{ "Start", "Seat", "Cases", "Sheet", "Verdicts", "Punish", "Reputation", "Tips" };

		private int _section;
		private Vector2 _scroll;

		public override Vector2 InitialSize => new Vector2(820f, 600f);

		public Window_CourtGuide()
		{
			doCloseX = true;
			draggable = true;
			closeOnClickedOutside = true;
			absorbInputAroundWindow = false;
			preventCameraMotion = false;
		}

		public static void Open()
		{
			if (!Find.WindowStack.IsOpen<Window_CourtGuide>())
				Find.WindowStack.Add(new Window_CourtGuide());
		}

		public override void DoWindowContents(Rect inRect)
		{
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(0f, 0f, inRect.width, 36f), "RimCourt.Guide.Title".Translate());
			Text.Font = GameFont.Small;

			var body = new Rect(0f, 46f, inRect.width, inRect.height - 46f);
			const float navW = 210f;
			var navRect = new Rect(body.x, body.y, navW, body.height);
			var contentRect = new Rect(body.x + navW + 14f, body.y, body.width - navW - 14f, body.height);

			var y = navRect.y;
			for (var i = 0; i < Sections.Length; i++)
			{
				var r = new Rect(navRect.x, y, navRect.width, 38f);
				if (i == _section) Widgets.DrawHighlightSelected(r);
				else if (Mouse.IsOver(r)) Widgets.DrawHighlight(r);

				Widgets.Label(new Rect(r.x + 8f, r.y + 8f, r.width - 12f, r.height),
					("RimCourt.Guide." + Sections[i] + "Title").Translate());

				if (Widgets.ButtonInvisible(r)) { _section = i; _scroll = Vector2.zero; }
				y += 40f;
			}

			Widgets.DrawMenuSection(contentRect);
			var inner = contentRect.ContractedBy(14f);

			var head = ("RimCourt.Guide." + Sections[_section] + "Title").Translate();
			var text = ("RimCourt.Guide." + Sections[_section] + "Body").Translate();

			Text.Font = GameFont.Medium;
			var headH = Text.CalcHeight(head, inner.width);
			Widgets.Label(new Rect(inner.x, inner.y, inner.width, headH), head);
			Text.Font = GameFont.Small;

			var top = inner.y + headH + 10f;
			var viewRect = new Rect(inner.x, top, inner.width, inner.yMax - top);
			var contentWidth = viewRect.width - 18f;
			var textH = Text.CalcHeight(text, contentWidth);
			var scrollView = new Rect(0f, 0f, contentWidth, Mathf.Max(textH, viewRect.height));

			Widgets.BeginScrollView(viewRect, ref _scroll, scrollView);
			Widgets.Label(new Rect(0f, 0f, contentWidth, textH), text);
			Widgets.EndScrollView();
		}
	}
}
