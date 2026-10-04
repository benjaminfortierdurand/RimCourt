using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimCourt.Court
{
	public class PetitionDef : Def
	{
		public System.Type workerClass = typeof(PetitionWorker_Quarrel);
		public string caseText;
		public string arrivalText;
		public bool mutualGrudge;
		public int opinionThreshold = -15;
		public List<ThoughtDef> sourceThoughts;
		public int maxMemoryAgeDays = 30;
		public float priority = 10f;
		public IntRange amountRange = new IntRange(80, 260);

		public bool FromRealEvent => sourceThoughts != null && sourceThoughts.Count > 0;
		public List<VerdictOption> verdicts = new List<VerdictOption>();

		[Unsaved(false)]
		private PetitionWorker workerInt;

		public PetitionWorker Worker
		{
			get
			{
				if (workerInt == null)
				{
					workerInt = (PetitionWorker)System.Activator.CreateInstance(workerClass);
					workerInt.def = this;
				}
				return workerInt;
			}
		}

		public override IEnumerable<string> ConfigErrors()
		{
			foreach (var e in base.ConfigErrors()) yield return e;
			if (verdicts == null || verdicts.Count < 3 || verdicts.Count > 4) yield return "needs 3 or 4 verdicts";
			if (caseText.NullOrEmpty()) yield return "missing caseText";
			if (workerClass == null || !typeof(PetitionWorker).IsAssignableFrom(workerClass)) yield return "bad workerClass";
		}
	}

	public class VerdictOption
	{
		public string label;
		public string pastLabel;
		public string outcome;
		public float just;
		public float harsh;
		public float venal;
		public float weak;

		public VerdictOption WithWeights(float j, float h, float v, float w)
			=> new VerdictOption { label = label, pastLabel = pastLabel, outcome = outcome, just = j, harsh = h, venal = v, weak = w };
	}
}
