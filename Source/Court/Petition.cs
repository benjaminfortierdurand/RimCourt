using System.Collections.Generic;
using Verse;

namespace RimCourt.Court
{
	public class Petition : IExposable
	{
		public const int StateQueued = 0;
		public const int StatePleading = 1;
		public const int StateAwaitingVerdict = 2;
		public const int StateResolved = 3;

		public int id;
		public PetitionDef def;
		public Pawn petitioner;
		public Pawn other;
		public Pawn third;
		public int state;
		public int silver;
		public string extra;
		public int deadlineTick = -1;
		public int verdictIndex = -1;
		public int stampedIndex = -1;
		public bool deserter;
		public List<VerdictStampMark> stamps = new List<VerdictStampMark>();

		public bool Involves(Pawn p) => p != null && (p == petitioner || p == other);

		public void ExposeData()
		{
			Scribe_Values.Look(ref id, "id");
			Scribe_Defs.Look(ref def, "def");
			Scribe_References.Look(ref petitioner, "petitioner");
			Scribe_References.Look(ref other, "other");
			Scribe_References.Look(ref third, "third");
			Scribe_Values.Look(ref state, "state");
			Scribe_Values.Look(ref silver, "silver");
			Scribe_Values.Look(ref extra, "extra");
			Scribe_Values.Look(ref deadlineTick, "deadlineTick", -1);
			Scribe_Values.Look(ref verdictIndex, "verdictIndex", -1);
			Scribe_Values.Look(ref stampedIndex, "stampedIndex", -1);
			Scribe_Values.Look(ref deserter, "deserter");
			Scribe_Collections.Look(ref stamps, "stamps", LookMode.Deep);
			if (Scribe.mode == LoadSaveMode.PostLoadInit && stamps == null)
				stamps = new List<VerdictStampMark>();
		}
	}

	public class HeardCase : IExposable
	{
		public int a;
		public int b;
		public string aName;
		public string bName;
		public string defName;
		public string outcome;
		public int tick;
		public int axis = AxisUnset;

		public const int AxisUnset = -2;

		public void ExposeData()
		{
			Scribe_Values.Look(ref axis, "axis", AxisUnset);
			Scribe_Values.Look(ref a, "a");
			Scribe_Values.Look(ref b, "b");
			Scribe_Values.Look(ref aName, "aName");
			Scribe_Values.Look(ref bName, "bName");
			Scribe_Values.Look(ref defName, "defName");
			Scribe_Values.Look(ref outcome, "outcome");
			Scribe_Values.Look(ref tick, "tick");
		}
	}

	public class VerdictStampMark : IExposable
	{
		public int row;
		public float x;
		public float angle;

		public void ExposeData()
		{
			Scribe_Values.Look(ref row, "row");
			Scribe_Values.Look(ref x, "x");
			Scribe_Values.Look(ref angle, "angle");
		}
	}
}
