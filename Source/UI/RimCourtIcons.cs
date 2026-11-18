using RimWorld;
using UnityEngine;
using Verse;

namespace RimCourt.UI
{
	[StaticConstructorOnStartup]
	public static class RimCourtIcons
	{
		public static readonly Texture2D HoldCourt =
			ContentFinder<Texture2D>.Get("UI/Commands/RimCourt_HoldCourt", reportFailure: false)
				?? ContentFinder<Texture2D>.Get("UI/Commands/GatherSpotActive", reportFailure: false)
				?? TexCommand.DesirePower;

		public static readonly Texture2D Guide =
			ContentFinder<Texture2D>.Get("UI/Buttons/InfoButton", reportFailure: false)
				?? TexCommand.DesirePower;

		public static readonly Texture2D NameLord =
			ContentFinder<Texture2D>.Get("UI/Commands/RimCourt_NameLord", reportFailure: false)
				?? ContentFinder<Texture2D>.Get("UI/Commands/RimCourt_HoldCourt", reportFailure: false)
				?? TexCommand.DesirePower;

		public static readonly Texture2D Hand =
			ContentFinder<Texture2D>.Get("UI/Commands/RimCourt_Hand", reportFailure: false)
				?? ContentFinder<Texture2D>.Get("UI/Commands/RimCourt_NameLord", reportFailure: false)
				?? TexCommand.DesirePower;

		public static readonly Texture2D Standing =
			ContentFinder<Texture2D>.Get("UI/Commands/RimCourt_Standing", reportFailure: false)
				?? ContentFinder<Texture2D>.Get("UI/Commands/RimCourt_HoldCourt", reportFailure: false)
				?? TexCommand.DesirePower;

		public static readonly Texture2D HearCaptives =
			ContentFinder<Texture2D>.Get("UI/Commands/RimCourt_HearCaptives", reportFailure: false)
				?? ContentFinder<Texture2D>.Get("UI/Commands/RimCourt_HoldCourt", reportFailure: false)
				?? TexCommand.DesirePower;

		public static readonly Texture2D HoldCourtGlyph =
			ContentFinder<Texture2D>.Get("UI/RimCourt_HoldCourtGlyph", reportFailure: false) ?? HoldCourt;

		public static readonly Texture2D HandGlyph =
			ContentFinder<Texture2D>.Get("UI/RimCourt_HandGlyph", reportFailure: false) ?? Hand;

		public static readonly Texture2D ShowMarker = TexCommand.GatherSpotActive;

		public static readonly Texture2D HonourSeat =
			ContentFinder<Texture2D>.Get("UI/Commands/AssignOwner", reportFailure: false)
				?? ContentFinder<Texture2D>.Get("UI/Commands/RimCourt_NameLord", reportFailure: false)
				?? TexCommand.DesirePower;

		public static readonly Texture2D AreaExpand =
			ContentFinder<Texture2D>.Get("UI/Designators/AreaAllowedExpand", reportFailure: false) ?? TexButton.Plus;

		public static readonly Texture2D AreaClear =
			ContentFinder<Texture2D>.Get("UI/Designators/AreaAllowedClear", reportFailure: false) ?? TexButton.Minus;

		public static readonly Texture2D AreaBadge =
			ContentFinder<Texture2D>.Get("UI/Commands/RimCourt_AreaBadge", reportFailure: false);

		public static readonly Texture2D Cancel =
			ContentFinder<Texture2D>.Get("UI/Designators/Cancel", reportFailure: false)
				?? TexCommand.ForbidOff;
	}
}
