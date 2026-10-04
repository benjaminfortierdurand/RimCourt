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

		public static readonly Texture2D Cancel =
			ContentFinder<Texture2D>.Get("UI/Designators/Cancel", reportFailure: false)
				?? TexCommand.ForbidOff;
	}
}
