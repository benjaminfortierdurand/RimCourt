using RimWorld;
using Verse;

namespace RimCourt.Court
{
	public class OutsiderRequest : IExposable
	{
		public Pawn pawn;
		public Pawn otherPawn;
		public PetitionDef def;
		public Faction faction;
		public Faction otherFaction;
		public int silver;
		public string extra;
		public int expiryTick = -1;
		public bool deserter;

		public bool Valid
		{
			get
			{
				if (pawn == null || pawn.Dead || !pawn.Spawned || def == null) return false;
				if (pawn.HostileTo(Faction.OfPlayer)) return false;
				if (otherPawn == null) return true;
				return !otherPawn.Dead && otherPawn.Spawned && !otherPawn.HostileTo(Faction.OfPlayer);
			}
		}

		public void ExposeData()
		{
			Scribe_References.Look(ref pawn, "pawn");
			Scribe_References.Look(ref otherPawn, "otherPawn");
			Scribe_Defs.Look(ref def, "def");
			Scribe_References.Look(ref faction, "faction");
			Scribe_References.Look(ref otherFaction, "otherFaction");
			Scribe_Values.Look(ref silver, "silver");
			Scribe_Values.Look(ref extra, "extra");
			Scribe_Values.Look(ref expiryTick, "expiryTick", -1);
			Scribe_Values.Look(ref deserter, "deserter");
		}
	}
}
