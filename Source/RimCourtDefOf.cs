using RimWorld;
using Verse;

namespace RimCourt
{
	[DefOf]
	public static class RimCourtDefOf
	{
		public static ThingDef RimCourt_AudienceSpot;
		public static ThingDef RimCourt_LordSeat;
		public static ThingDef RimCourt_Stocks;
		public static JobDef RimCourt_HoldCourt;
		public static JobDef RimCourt_StandInStocks;
		public static JobDef RimCourt_EscortToStocks;
		public static JobDef RimCourt_BringCaptive;
		public static JobDef RimCourt_JeerAtStocks;
		public static JobDef RimCourt_ExecuteCaptive;
		public static JobDef RimCourt_AttendCourt;
		public static JobDef RimCourt_PleadCase;
		public static JobDef RimCourt_Flog;
		public static JobDef RimCourt_TakeLashes;
		public static LetterDef RimCourt_Verdict;
		public static SoundDef RimCourt_Whip;
		public static DamageDef RimCourt_Lash;
		public static ThoughtDef RimCourt_LordRuledForMe;
		public static ThoughtDef RimCourt_LordRuledAgainstMe;
		public static ThoughtDef RimCourt_LordDismissedMyCase;
		public static ThoughtDef RimCourt_SettledBeforeLord;
		public static ThoughtDef RimCourt_ScoldedByLord;
		public static ThoughtDef RimCourt_SawCourtHeld;
		public static ThoughtDef RimCourt_BlessedByLord;
		public static ThoughtDef RimCourt_RefusedByLord;
		public static ThoughtDef RimCourt_HonouredByLord;
		public static ThoughtDef RimCourt_PaidInSilver;
		public static ThoughtDef RimCourt_Freed;
		public static ThoughtDef RimCourt_InStocks;
		public static ThoughtDef RimCourt_KillerFreed;
		public static FleckDef RimCourt_Thrown;
		public static FleckDef RimCourt_Splat;
		public static FleckDef RimCourt_WhipLine;
		public static FleckDef RimCourt_WhipRaised;
		public static ThingDef RimCourt_FilthSplat;
		public static InteractionDef RimCourt_Mock;
		public static InteractionDef RimCourt_Pelt;
		public static ThoughtDef RimCourt_LordAdmittedFault;
		public static ThoughtDef RimCourt_LordJudgedHimself;
		public static ThoughtDef RimCourt_PromiseBroken;
		public static ThoughtDef RimCourt_LordBrokeHisWord;
		public static ThoughtDef RimCourt_LordFavouredHisOwn;
		public static ThoughtDef RimCourt_DefiedTheLord;
		public static ThoughtDef RimCourt_LordWasDefied;
		public static ThoughtDef RimCourt_EstateDenied;
		public static ThoughtDef RimCourt_RuledAgainstNature;
		public static ThoughtDef RimCourt_RuledTrueToNature;
		public static FleckDef RimCourt_HandNamed;
		public static ThoughtDef RimCourt_HonouredHim;
		public static ThoughtDef RimCourt_Deposed;
		public static ThoughtDef RimCourt_TookThrone;
		public static ThoughtDef RimCourt_DefeatedBeforeCourt;
		public static TaleDef RimCourt_Tale_Flogged;
		public static TaleDef RimCourt_Tale_Banished;
		public static TaleDef RimCourt_Tale_TrialByCombat;
		public static TaleDef RimCourt_Tale_MatchBlessed;
		public static TaleDef RimCourt_Tale_ThroneTaken;

		static RimCourtDefOf()
		{
			DefOfHelper.EnsureInitializedInCtor(typeof(RimCourtDefOf));
		}
	}
}
