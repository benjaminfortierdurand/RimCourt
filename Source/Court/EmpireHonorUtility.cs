using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimCourt.Court
{
	public static class EmpireHonorUtility
	{
		public const string HonorDefName = "VFEE_Negotiator";

		private static bool _looked;
		private static Type _honorDefType;
		private static MethodInfo _workerGetter;
		private static MethodInfo _workerAvailable;
		private static MethodInfo _workerGenerate;
		private static MethodInfo _postMake;
		private static MethodInfo _addHonor;
		private static MethodInfo _honorsOf;
		private static MethodInfo _bestowHonor;

		private static void LookUp()
		{
			if (_looked) return;
			_looked = true;
			try
			{
				_honorDefType = AccessTools.TypeByName("VFEEmpire.HonorDef");
				var honorType = AccessTools.TypeByName("VFEEmpire.Honor");
				var utilityType = AccessTools.TypeByName("VFEEmpire.HonorUtility");
				var trackerType = AccessTools.TypeByName("VFEEmpire.HonorsTracker");
				var workerType = AccessTools.TypeByName("VFEEmpire.HonorWorker");
				if (_honorDefType == null || honorType == null || utilityType == null
					|| trackerType == null || workerType == null) { Clear(); return; }

				_workerGetter = AccessTools.PropertyGetter(_honorDefType, "Worker");
				_workerAvailable = AccessTools.Method(workerType, "Available");
				_workerGenerate = AccessTools.Method(workerType, "Generate");
				_postMake = AccessTools.Method(honorType, "PostMake");
				_addHonor = AccessTools.Method(utilityType, "AddHonor", new[] { typeof(Pawn), honorType });
				_honorsOf = AccessTools.Method(utilityType, "Honors", new[] { typeof(Pawn) });
				_bestowHonor = AccessTools.Method(trackerType, "BestowHonor", new[] { honorType });

				if (_workerGetter == null || _workerAvailable == null || _workerGenerate == null
					|| _postMake == null || _addHonor == null || _honorsOf == null || _bestowHonor == null)
					Clear();
			}
			catch (Exception e)
			{
				Log.Warning("[RimCourt] honneurs de VFE Empire illisibles (" + e.Message + ").");
				Clear();
			}
		}

		private static void Clear()
		{
			_honorDefType = null;
			_workerGetter = null;
			_workerAvailable = null;
			_workerGenerate = null;
			_postMake = null;
			_addHonor = null;
			_honorsOf = null;
			_bestowHonor = null;
		}

		public static bool Active
		{
			get { LookUp(); return _honorDefType != null; }
		}

		public static bool IsImperial(Faction f)
			=> f != null && !f.IsPlayer && f.def != null && !f.def.royalTitleTags.NullOrEmpty();

		public static void NoteJustRuling(Petition p, Pawn lord)
		{
			if (p == null || lord == null || lord.Dead) return;
			if (!IsImperial(p.petitioner?.Faction) || p.petitioner.IsPrisoner) return;
			if (!Active) return;

			try
			{
				var def = GenDefDatabase.GetDefSilentFail(_honorDefType, HonorDefName);
				if (def == null) return;
				var worker = _workerGetter.Invoke(def, null);
				if (worker == null) return;
				if (!(bool)_workerAvailable.Invoke(worker, null)) return;

				var honor = _workerGenerate.Invoke(worker, null);
				if (honor == null) return;
				_postMake.Invoke(honor, null);
				_addHonor.Invoke(null, new[] { lord, honor });
				var tracker = _honorsOf.Invoke(null, new object[] { lord });
				if (tracker != null) _bestowHonor.Invoke(tracker, new[] { honor });

				Find.LetterStack.ReceiveLetter(
					"RimCourt.HonourConferredLabel".Translate(lord.LabelShortCap),
					"RimCourt.HonourConferred".Translate(
						lord.LabelShortCap, p.petitioner.LabelShortCap,
						p.petitioner.Faction.Name),
					LetterDefOf.PositiveEvent, new LookTargets(lord));
			}
			catch (Exception e)
			{
				Log.Warning("[RimCourt] honneur impérial non décerné, fonction coupée pour cette "
					+ "session (" + e.Message + ").");
				Clear();
			}
		}
	}
}
