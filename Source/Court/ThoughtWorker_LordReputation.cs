using RimWorld;
using Verse;

namespace RimCourt.Court
{
	public class ThoughtWorker_LordReputation : ThoughtWorker
	{
		protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn other)
		{
			var mgr = CourtManager.Instance;
			if (mgr == null) return ThoughtState.Inactive;
			if (other != mgr.CurrentLord || p == other) return ThoughtState.Inactive;
			var stage = mgr.Reputation.Stage;
			if (stage == JudgeReputation.StageNone) return ThoughtState.Inactive;
			if (!p.IsColonist || p.Faction != other.Faction) return ThoughtState.Inactive;
			return ThoughtState.ActiveAtStage(stage);
		}
	}
}
