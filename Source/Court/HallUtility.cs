using RimWorld;
using Verse;

namespace RimCourt.Court
{
	public static class HallUtility
	{
		public const int GradeOpen = 0;
		public const int GradeGreat = 5;

		private static readonly float[] Thresholds = { 30f, 50f, 85f, 170f };
		private static readonly float[] Visitors = { 0.75f, 0.85f, 0.95f, 1f, 1.10f, 1.20f };
		private static readonly float[] Refusal = { 0.08f, 0.04f, 0.02f, 0f, -0.02f, -0.04f };

		public static int GradeAt(Thing seat)
		{
			if (seat == null || !seat.Spawned || seat.Map == null) return GradeOpen;
			var room = seat.Position.GetRoom(seat.Map);
			if (room == null || room.PsychologicallyOutdoors) return GradeOpen;
			var score = room.GetStat(RoomStatDefOf.Impressiveness);
			var grade = 1;
			for (var i = 0; i < Thresholds.Length; i++)
				if (score >= Thresholds[i]) grade++;
			return grade;
		}

		public static float ScoreAt(Thing seat)
		{
			if (seat == null || !seat.Spawned || seat.Map == null) return 0f;
			var room = seat.Position.GetRoom(seat.Map);
			if (room == null || room.PsychologicallyOutdoors) return 0f;
			return room.GetStat(RoomStatDefOf.Impressiveness);
		}

		public static Thing SeatOn(Map map)
		{
			if (map == null) return null;
			var mgr = CourtManager.Instance;
			return mgr != null ? mgr.SeatFor(map) : CourtManager.AnySeatOn(map);
		}

		public static int GradeOn(Map map) => GradeAt(SeatOn(map));

		public static float RefusalShiftOn(Map map) => Refusal[GradeOn(map)];

		public static float VisitorFactor(int grade) => Visitors[grade];

		public static float VisitorFactorAnywhere()
		{
			var best = GradeOpen;
			var maps = Find.Maps;
			for (var i = 0; i < maps.Count; i++)
			{
				if (!maps[i].IsPlayerHome) continue;
				var g = GradeOn(maps[i]);
				if (g > best) best = g;
			}
			return Visitors[best];
		}

		public static string Label(int grade) => ("RimCourt.Hall" + grade).Translate();

		public static string Note(int grade) => ("RimCourt.HallNote" + grade).Translate();

		public static string Numbers(int grade)
		{
			var visitors = Visitors[grade];
			var shift = Refusal[grade];
			var visitorText = visitors == 1f
				? "RimCourt.HallNoChange".Translate().ToString()
				: (visitors > 1f ? "+" : "") + ((visitors - 1f) * 100f).ToString("0") + "%";
			var refusalText = shift == 0f
				? "RimCourt.HallNoChange".Translate().ToString()
				: "RimCourt.HallPoints".Translate(
					(shift > 0f ? "+" : "") + (shift * 100f).ToString("0")).ToString();
			return "RimCourt.HallNumbers".Translate(visitorText, refusalText);
		}
	}
}
