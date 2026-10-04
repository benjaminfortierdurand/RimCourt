using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimCourt.Court
{
	public class PetitionWorker_Veteran : PetitionWorker_Request
	{
		private const float MinKills = 1f;
		private const int SilverPerKill = 30;
		private const int MinReward = 150;
		private const int MaxReward = 800;

		public override Petition TryGenerate(Map map, Pawn lord, List<Petition> taken, int thresholdShift)
		{
			var found = new List<Petition>();
			foreach (var a in map.mapPawns.FreeColonistsSpawned)
			{
				if (!CanAsk(a, lord, taken) || a.records == null || !Maimed(a)) continue;
				var kills = a.records.GetValue(RecordDefOf.KillsHumanlikes);
				if (kills < MinKills) continue;
				var work = WorstWorkFor(a);
				if (work == null) continue;
				var count = Mathf.RoundToInt(kills);
				found.Add(new Petition
				{
					def = def,
					petitioner = a,
					extra = work.defName,
					silver = Mathf.RoundToInt(Mathf.Clamp(count * SilverPerKill, MinReward, MaxReward) / 5f) * 5,
				});
			}
			return Pick(found);
		}

		private static bool Maimed(Pawn a)
		{
			var set = a.health?.hediffSet;
			if (set == null) return false;
			var list = set.hediffs;
			for (var i = 0; i < list.Count; i++)
			{
				var h = list[i];
				if (h is Hediff_MissingPart) return true;
				if (h is Hediff_Injury && h.IsPermanent()) return true;
			}
			return false;
		}

		private static string Kills(Pawn a)
			=> Mathf.RoundToInt(a?.records?.GetValue(RecordDefOf.KillsHumanlikes) ?? 0f).ToString();

		public override string CaseText(Petition p)
			=> def.caseText.Formatted(p.petitioner.Named("PETITIONER"),
				WorkLabel(p.extra).Named("WORK"), p.silver.Named("SILVER"),
				Kills(p.petitioner).Named("KILLS"));

		public override string VerdictLabel(Petition p, int index)
			=> def.verdicts[index].label.Formatted(p.petitioner.Named("PETITIONER"),
				WorkLabel(p.extra).Named("WORK"), p.silver.Named("SILVER"));

		public override string PastVerdictLabel(Petition p, int index)
		{
			var v = def.verdicts[index];
			return v.pastLabel.NullOrEmpty()
				? VerdictLabel(p, index)
				: v.pastLabel.Formatted(p.petitioner.Named("PETITIONER"),
					WorkLabel(p.extra).Named("WORK"), p.silver.Named("SILVER"));
		}
	}
}
