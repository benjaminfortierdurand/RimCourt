using UnityEngine;
using Verse;

namespace RimCourt.Court
{
	public class JudgeReputation : IExposable
	{
		public const int StageNone = -1;
		public const int StageJust = 0;
		public const int StageHarsh = 1;
		public const int StageVenal = 2;
		public const int StageWeak = 3;

		public const float EstablishedAt = 8f;
		public const float SwitchMargin = 3f;

		public float just;
		public float harsh;
		public float venal;
		public float weak;

		private int stage = StageNone;

		public float Total => just + harsh + venal + weak;

		public int Stage => stage;

		public float ScoreOf(int which)
		{
			switch (which)
			{
				case StageJust: return just;
				case StageHarsh: return harsh;
				case StageVenal: return venal;
				case StageWeak: return weak;
				default: return 0f;
			}
		}

		public float Highest
		{
			get
			{
				var top = 0f;
				for (var i = StageJust; i <= StageWeak; i++)
					if (ScoreOf(i) > top) top = ScoreOf(i);
				return top;
			}
		}

		public int Rival
		{
			get
			{
				var best = StageNone;
				for (var i = StageJust; i <= StageWeak; i++)
				{
					if (i == stage) continue;
					if (best == StageNone || ScoreOf(i) > ScoreOf(best)) best = i;
				}
				return best;
			}
		}

		public float PointsToSwitch
		{
			get
			{
				var rival = Rival;
				if (stage == StageNone || rival == StageNone) return 0f;
				return Mathf.Max(0f, ScoreOf(stage) + SwitchMargin - ScoreOf(rival));
			}
		}

		public static string KeyOf(int which)
		{
			switch (which)
			{
				case StageJust: return "RimCourt.RepJust";
				case StageHarsh: return "RimCourt.RepHarsh";
				case StageVenal: return "RimCourt.RepVenal";
				case StageWeak: return "RimCourt.RepWeak";
				default: return "RimCourt.RepUntested";
			}
		}

		public void Recompute()
		{
			if (stage == StageNone && Total < EstablishedAt) return;

			var leader = StageJust;
			for (var i = StageHarsh; i <= StageWeak; i++)
				if (ScoreOf(i) > ScoreOf(leader)) leader = i;

			if (stage == StageNone) { stage = leader; return; }
			if (leader != stage && ScoreOf(leader) >= ScoreOf(stage) + SwitchMargin) stage = leader;
		}

		public string TitleKey
		{
			get
			{
				switch (stage)
				{
					case StageJust: return "RimCourt.RepJust";
					case StageHarsh: return "RimCourt.RepHarsh";
					case StageVenal: return "RimCourt.RepVenal";
					case StageWeak: return "RimCourt.RepWeak";
					default: return "RimCourt.RepUntested";
				}
			}
		}

		public int CaseBonus
		{
			get
			{
				switch (stage)
				{
					case StageJust: return 1;
					case StageHarsh: return -1;
					case StageWeak: return 1;
					default: return 0;
				}
			}
		}

		public int ThresholdShift
		{
			get
			{
				switch (stage)
				{
					case StageHarsh: return -8;
					case StageWeak: return 5;
					default: return 0;
				}
			}
		}

		public void Add(VerdictOption v)
		{
			if (v == null) return;
			just += v.just;
			harsh += v.harsh;
			venal += v.venal;
			weak += v.weak;
			Recompute();
		}

		public void AddAsFavour(VerdictOption v)
		{
			if (v == null) return;
			harsh += v.harsh;
			venal += v.venal + v.just;
			weak += v.weak;
			Recompute();
		}

		public void Decay(float days)
		{
			if (days <= 0f || Total <= 0f) return;
			var factor = Mathf.Pow(0.97f, Mathf.Min(days, 90f));
			just *= factor;
			harsh *= factor;
			venal *= factor;
			weak *= factor;
			Recompute();
		}

		public void DebugSet(int which)
		{
			just = harsh = venal = weak = 0f;
			stage = StageNone;
			if (which != StageNone)
			{
				switch (which)
				{
					case StageJust: just = EstablishedAt + 2f; break;
					case StageHarsh: harsh = EstablishedAt + 2f; break;
					case StageVenal: venal = EstablishedAt + 2f; break;
					case StageWeak: weak = EstablishedAt + 2f; break;
				}
			}
			Recompute();
		}

		public void ExposeData()
		{
			Scribe_Values.Look(ref just, "just");
			Scribe_Values.Look(ref harsh, "harsh");
			Scribe_Values.Look(ref venal, "venal");
			Scribe_Values.Look(ref weak, "weak");
			Scribe_Values.Look(ref stage, "stage", StageNone);
			if (Scribe.mode == LoadSaveMode.PostLoadInit) Recompute();
		}
	}
}
