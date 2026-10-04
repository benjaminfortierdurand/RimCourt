using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace RimCourt.Court
{
	public static class FlogUtility
	{
		public const int DefaultLashes = 4;
		public const int MinFloggerAge = 16;
		private const int WhipSegments = 9;
		private static readonly Color Leather = new Color(0.72f, 0.5f, 0.3f);

		public static void Crack(Pawn flogger, Pawn victim, bool strike)
		{
			var def = strike ? RimCourtDefOf.RimCourt_WhipLine : RimCourtDefOf.RimCourt_WhipRaised;
			var map = flogger?.Map;
			if (def == null || map == null || victim == null || !flogger.Spawned || !victim.Spawned || victim.Map != map) return;
			if (!flogger.Position.AdjacentTo8WayOrInside(victim.Position)) return;
			var from = flogger.DrawPos;
			var to = victim.DrawPos;
			var dir = to - from;
			dir.y = 0f;
			if (dir.sqrMagnitude < 0.01f) return;
			dir.Normalize();
			var perp = new Vector3(-dir.z, 0f, dir.x);
			if (perp.z < 0f) perp = -perp;
			var hand = from + dir * 0.22f + new Vector3(0f, 0f, 0.2f);
			Vector3 end, ctrl;
			if (strike)
			{
				end = to + dir * 0.45f + new Vector3(0f, 0f, -0.05f);
				ctrl = (hand + end) * 0.5f + perp * 0.45f + new Vector3(0f, 0f, 0.7f);
			}
			else
			{
				end = hand - dir * 0.85f + new Vector3(0f, 0f, 0.75f);
				ctrl = hand - dir * 0.1f + perp * 0.3f + new Vector3(0f, 0f, 1.1f);
			}
			var prev = hand;
			for (var i = 1; i <= WhipSegments; i++)
			{
				var t = i / (float)WhipSegments;
				var u = 1f - t;
				var p = u * u * hand + 2f * u * t * ctrl + t * t * end;
				Segment(map, def, prev, p, Mathf.Lerp(0.3f, 0.12f, t));
				prev = p;
			}
		}

		private static void Segment(Map map, FleckDef def, Vector3 a, Vector3 b, float width)
		{
			var v = b - a;
			var len = v.MagnitudeHorizontal();
			if (len < 0.01f) return;
			var data = FleckMaker.GetDataStatic(a + v * 0.5f, map, def);
			data.exactScale = new Vector3(len * 1.1f, 1f, width);
			data.rotation = Mathf.Atan2(-v.z, v.x) * Mathf.Rad2Deg;
			data.instanceColor = Leather;
			map.flecks.CreateFleck(data);
		}

		public static void OneLash(Pawn flogger, Pawn victim)
		{
			if (victim == null || victim.Dead || victim.health == null) return;
			if (victim.health.summaryHealth.SummaryHealthPercent < 0.35f)
			{
				if (Prefs.DevMode)
					Log.Message("[RimCourt] coup retenu : " + victim.LabelShort + " est déjà trop mal en point.");
				return;
			}
			try
			{
				var parts = new List<BodyPartRecord>(
					victim.health.hediffSet.GetNotMissingParts(BodyPartHeight.Middle, BodyPartDepth.Outside));
				var part = parts.Count > 0 ? parts.RandomElement() : null;
				var lashDef = RimCourtDefOf.RimCourt_Lash ?? DamageDefOf.Scratch;
				var lash = new DamageInfo(lashDef, Rand.Range(5f, 9f), 2f, -1f,
					flogger, part, null, DamageInfo.SourceCategory.ThingOrUnknown, victim,
					instigatorGuilty: false, checkForJobOverride: false);
				lash.SetAllowDamagePropagation(false);
				var result = victim.TakeDamage(lash);
				if (Prefs.DevMode)
					Log.Message("[RimCourt] coup de fouet sur " + victim.LabelShort
						+ " : " + result.totalDamageDealt.ToString("F1") + " de dégâts.");

				if (victim.Map != null)
				{
					Crack(flogger, victim, true);
					var where = new TargetInfo(victim.Position, victim.Map);
					if (RimCourtDefOf.RimCourt_Whip != null) RimCourtDefOf.RimCourt_Whip.PlayOneShot(where);
					MoteMaker.ThrowText(victim.DrawPos, victim.Map, "RimCourt.FlogMote".Translate(), 3.2f);
				}
			}
			catch (System.Exception e)
			{
				Log.Error("[RimCourt] coup de fouet impossible : " + e);
			}
		}

		private static bool AtPost(Map map, Pawn p)
		{
			var posts = map.listerThings.ThingsOfDef(RimCourtDefOf.RimCourt_Stocks);
			for (var i = 0; i < posts.Count; i++)
				if (posts[i] is Building_Stocks st && (st.Occupant == p || st.Escort == p)) return true;
			return false;
		}

		public static List<Pawn> FloggerCandidates(Map map, Pawn condemned)
		{
			var list = new List<Pawn>();
			if (map == null) return list;
			foreach (var p in map.mapPawns.FreeColonistsSpawned)
			{
				if (p == condemned || p.Dead || p.Downed || p.InMentalState) continue;
				if (p.IsPrisoner || p.IsSlave) continue;
				if (p.CurJobDef == RimCourtDefOf.RimCourt_StandInStocks || p.CurJobDef == RimCourtDefOf.RimCourt_EscortToStocks) continue;
				if (AtPost(map, p)) continue;
				if (p.ageTracker != null && p.ageTracker.AgeBiologicalYears < MinFloggerAge) continue;
				if (p.WorkTagIsDisabled(WorkTags.Violent)) continue;
				if (p.health == null || !p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) continue;
				if (condemned != null && !p.CanReach(condemned, PathEndMode.Touch, Danger.Deadly)) continue;
				list.Add(p);
			}
			return list;
		}

	}
}
