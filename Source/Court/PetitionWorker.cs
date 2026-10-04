using System.Collections.Generic;
using Verse;

namespace RimCourt.Court
{
	public abstract class PetitionWorker
	{
		public PetitionDef def;

		public abstract Petition TryGenerate(Map map, Pawn lord, List<Petition> taken, int thresholdShift);

		public abstract void ApplyVerdict(Petition p, VerdictOption v, Pawn lord);

		public virtual void ApplyTimeout(Petition p, VerdictOption v, Pawn lord) => ApplyVerdict(p, v, lord);

		public virtual VerdictOption Weigh(Petition p, VerdictOption v) => v;

		public virtual int TimeoutIndex(Petition p) => -1;

		public virtual bool UsesPrecedent => true;

		public virtual int ExtraCallTicks => 0;

		public virtual void ResolveDuel(Pawn winner, Pawn loser, Pawn lord)
		{
		}

		public virtual void Reactions(Petition p, VerdictOption v, out int petitionerMood, out int otherMood)
		{
			petitionerMood = 0;
			otherMood = 0;
		}

		public virtual string CaseText(Petition p)
			=> def.caseText.Formatted(p.petitioner.Named("PETITIONER"), (p.other ?? p.petitioner).Named("OTHER"));

		public virtual string VerdictLabel(Petition p, int index)
			=> def.verdicts[index].label.Formatted(p.petitioner.Named("PETITIONER"), (p.other ?? p.petitioner).Named("OTHER"));

		public virtual string PastVerdictLabel(Petition p, int index)
		{
			var v = def.verdicts[index];
			return v.pastLabel.NullOrEmpty()
				? VerdictLabel(p, index)
				: v.pastLabel.Formatted(p.petitioner.Named("PETITIONER"), (p.other ?? p.petitioner).Named("OTHER"));
		}
	}
}
