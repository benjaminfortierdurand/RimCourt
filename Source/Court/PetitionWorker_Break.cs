using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimCourt.Court
{
	public class PetitionWorker_Break : PetitionWorker_Quarrel
	{
		public override Petition TryGenerate(Map map, Pawn lord, List<Petition> taken, int thresholdShift)
		{
			if (def.sourceBreaks.NullOrEmpty()) return null;
			if (!(RimCourtMod.Settings?.tryBreaks ?? true)) return null;
			var mgr = CourtManager.Instance;
			if (mgr == null) return null;

			var now = Find.TickManager.TicksGame;
			var maxAge = def.maxMemoryAgeDays * 60000;
			var records = mgr.Breaks;
			var found = new List<Petition>();
			for (var i = 0; i < records.Count; i++)
			{
				var r = records[i];
				if (r.tried || r.Open || now - r.endTick > maxAge) continue;
				if (!def.sourceBreaks.Contains(r.state) || !r.Substantial) continue;
				var accused = r.pawn;
				if (accused == null || accused.Map != map || !CanPlead(accused, lord, taken)) continue;
				var plaintiff = Plaintiff(r, map, lord, taken);
				if (plaintiff == null) continue;
				if (mgr.WasHeardRecently(plaintiff, accused, def)) continue;
				found.Add(new Petition
				{
					def = def,
					petitioner = plaintiff,
					other = accused,
					extra = Encode(r, BreakUtility.Priors(records, r)),
				});
			}
			return found.Count == 0 ? null : found.RandomElement();
		}

		private static Pawn Plaintiff(BreakRecord r, Map map, Pawn lord, List<Petition> taken)
		{
			if (Fit(r.victim, r, map, lord, taken)) return r.victim;
			if (r.kind == BreakRecord.KindMurder) return null;

			var helper = Angriest(r, r.helpers, map, lord, taken);
			if (helper != null) return helper;

			if (r.kind == BreakRecord.KindSlaughter || r.kind == BreakRecord.KindJailbreak)
			{
				var work = r.kind == BreakRecord.KindSlaughter ? WorkTypeDefOf.Handling : WorkTypeDefOf.Warden;
				var skill = r.kind == BreakRecord.KindSlaughter ? SkillDefOf.Animals : SkillDefOf.Social;
				Pawn keeper = null;
				var bestLevel = -1;
				for (var i = 0; i < r.witnesses.Count; i++)
				{
					var w = r.witnesses[i];
					if (!Fit(w, r, map, lord, taken)) continue;
					if (w.workSettings == null || !w.workSettings.WorkIsActive(work)) continue;
					var level = w.skills?.GetSkill(skill)?.Level ?? 0;
					if (level > bestLevel) { bestLevel = level; keeper = w; }
				}
				if (keeper != null) return keeper;
			}

			return Angriest(r, r.witnesses.Count, map, lord, taken);
		}

		private static Pawn Angriest(BreakRecord r, int upTo, Map map, Pawn lord, List<Petition> taken)
		{
			Pawn best = null;
			var worst = int.MaxValue;
			for (var i = 0; i < upTo && i < r.witnesses.Count; i++)
			{
				var w = r.witnesses[i];
				if (!Fit(w, r, map, lord, taken)) continue;
				var opinion = w.relations?.OpinionOf(r.pawn) ?? 0;
				if (opinion < worst) { worst = opinion; best = w; }
			}
			return best;
		}

		private static bool Fit(Pawn p, BreakRecord r, Map map, Pawn lord, List<Petition> taken)
			=> p != null && p != r.pawn && p.Map == map && CanPlead(p, lord, taken);

		private static bool CanPlead(Pawn p, Pawn lord, List<Petition> taken)
		{
			if (p == null || p == lord || p.Dead || p.Downed || !p.Spawned) return false;
			if (p.InMentalState || p.Drafted || !p.IsFreeColonist) return false;
			if (p.ageTracker != null && p.ageTracker.AgeBiologicalYears < 13) return false;
			if (p.health == null || !p.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) return false;
			for (var i = 0; i < taken.Count; i++)
				if (taken[i].Involves(p)) return false;
			return true;
		}

		private static string Encode(BreakRecord r, int priors)
			=> r.startTick + "|" + r.kind + "|" + priors + "|" + r.count + "|" + (r.detail ?? "").Replace('|', '/');

		private struct Facts
		{
			public int startTick;
			public int kind;
			public int priors;
			public int count;
			public string detail;
		}

		private static Facts Decode(string s)
		{
			var f = new Facts();
			if (s.NullOrEmpty()) return f;
			var parts = s.Split(new[] { '|' }, 5);
			if (parts.Length < 5) return f;
			int.TryParse(parts[0], out f.startTick);
			int.TryParse(parts[1], out f.kind);
			int.TryParse(parts[2], out f.priors);
			int.TryParse(parts[3], out f.count);
			f.detail = parts[4];
			return f;
		}

		public override VerdictOption Weigh(Petition p, VerdictOption v)
		{
			if (Decode(p.extra).priors <= 0) return v;
			switch (v.outcome)
			{
				case "FavorOther":
				case "Dismiss":
					return v.WithWeights(0f, 0f, v.venal, 3f);
				case "BloodPrice":
				case "Stocks":
				case "Banish":
					return v.harsh > 0f ? v.WithWeights(v.just + 1f, v.harsh - 1f, v.venal, v.weak) : v;
				default:
					return v;
			}
		}

		public override void ApplyVerdict(Petition p, VerdictOption v, Pawn lord)
		{
			CourtManager.Instance?.NoteBreakTried(p.other, Decode(p.extra).startTick);
			base.ApplyVerdict(p, v, lord);
		}

		public override string CaseText(Petition p)
		{
			var f = Decode(p.extra);
			string detail;
			if (f.detail.NullOrEmpty()) detail = "RimCourt.BreakUnknown".Translate().ToString();
			else if (f.count > 1 && f.kind != BreakRecord.KindJailbreak && f.kind != BreakRecord.KindGrave)
				detail = (f.kind == BreakRecord.KindSlaughter ? "RimCourt.BreakAndOthers" : "RimCourt.BreakAndMore")
					.Translate(f.detail).ToString();
			else detail = f.detail;
			var repeat = f.priors > 0 ? " " + "RimCourt.BreakRepeat".Translate().ToString() : "";
			return def.caseText.Formatted(p.petitioner.Named("PETITIONER"), p.other.Named("OTHER"),
				detail.Named("DETAIL"), repeat.Named("REPEAT"));
		}
	}
}
