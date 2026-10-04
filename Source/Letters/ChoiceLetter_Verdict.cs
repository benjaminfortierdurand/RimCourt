using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimCourt.Letters
{
	public class ChoiceLetter_Verdict : ChoiceLetter
	{
		public int petitionId;

		public override bool CanDismissWithRightClick => false;

		public override IEnumerable<DiaOption> Choices
		{
			get
			{
				var mgr = Court.CourtManager.Instance;
				var p = mgr?.GetPetition(petitionId);
				if (p != null && p.state == Court.Petition.StateAwaitingVerdict)
				{
					for (var i = 0; i < p.def.verdicts.Count; i++)
					{
						var index = i;
						yield return new DiaOption(p.def.Worker.VerdictLabel(p, index))
						{
							action = () => mgr.ChooseVerdict(petitionId, index),
							resolveTree = true,
						};
					}
				}
				yield return Option_Postpone;
			}
		}

		public override void OpenLetter()
		{
			UI.Dialog_PetitionSheet.OpenFor(petitionId);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref petitionId, "petitionId");
		}
	}
}
