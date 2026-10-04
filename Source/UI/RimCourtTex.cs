using UnityEngine;
using Verse;

namespace RimCourt.UI
{
	[StaticConstructorOnStartup]
	public static class RimCourtTex
	{
		public static readonly Texture2D Sheet =
			ContentFinder<Texture2D>.Get("UI/RimCourt_Sheet", reportFailure: false);

		public static readonly Texture2D Photo =
			ContentFinder<Texture2D>.Get("UI/RimCourt_Photo", reportFailure: false);

		public static readonly Texture2D Stamp =
			ContentFinder<Texture2D>.Get("UI/RimCourt_Stamp", reportFailure: false);

		public static readonly Texture2D StampMark =
			ContentFinder<Texture2D>.Get("UI/RimCourt_StampMark", reportFailure: false);

		public static readonly Color ParchmentDeepColor = new Color(0.780f, 0.714f, 0.545f);

		public static readonly Texture2D ParchmentDeep =
			SolidColorMaterials.NewSolidColorTexture(ParchmentDeepColor);

		public static readonly Texture2D ParchmentEdge =
			SolidColorMaterials.NewSolidColorTexture(new Color(0.541f, 0.478f, 0.322f));
	}
}
