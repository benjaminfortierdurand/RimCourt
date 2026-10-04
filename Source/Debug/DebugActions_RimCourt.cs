using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimCourt.Court;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimCourt.Debug
{
	public static class DebugActions_RimCourt
	{
		[DebugAction("RimCourt", "Fabriquer une affaire",
			actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void MakeCase()
		{
			var options = new List<DebugMenuOption>
			{
				new DebugMenuOption("Querelle entre deux colons", DebugMenuOptionMode.Action, PickAQuarrel),
				new DebugMenuOption("Dette de sang", DebugMenuOptionMode.Action, MakeBloodDebt),
				new DebugMenuOption("Adultère", DebugMenuOptionMode.Action, MakeAdultery),
				new DebugMenuOption("Triangle amoureux", DebugMenuOptionMode.Action, MakeTriangle),
				new DebugMenuOption("Prétendant lourd", DebugMenuOptionMode.Action, MakePest),
				new DebugMenuOption("Fiançailles à bénir", DebugMenuOptionMode.Action, Betroth),
				new DebugMenuOption("Querelle d'héritage", DebugMenuOptionMode.Action, MakeEstate),
				new DebugMenuOption("Pénitent", DebugMenuOptionMode.Action, MakePenitent),
				new DebugMenuOption("Dette de sauvetage", DebugMenuOptionMode.Action, MakeGratitude),
				new DebugMenuOption("Soldat au compteur de morts", DebugMenuOptionMode.Action, GiveKills),
				new DebugMenuOption("Vétéran mutilé", DebugMenuOptionMode.Action, MakeVeteran),
				new DebugMenuOption("Esclave qui demande sa liberté (Ideology)", DebugMenuOptionMode.Action, MakeSlave),
				new DebugMenuOption("Griefs contre le seigneur", DebugMenuOptionMode.Action, GiveGrievances),
				new DebugMenuOption("Contestation du trône", DebugMenuOptionMode.Action, SetUpChallenge),
				new DebugMenuOption("Pétitionnaire du dehors", DebugMenuOptionMode.Action, SpawnPetitioner),
				new DebugMenuOption("Pétitionnaire du dehors (déserteur)", DebugMenuOptionMode.Action, SpawnDeserter),
				new DebugMenuOption("Exilé qui revient demander grâce", DebugMenuOptionMode.Action, ExileForPardon),
				new DebugMenuOption("Prisonnier à juger", DebugMenuOptionMode.Action, MakeCaptiveMenu),
			};
			Find.WindowStack.Add(new Dialog_DebugOptionListLister(options, "RimCourt debug : quelle affaire ?"));
		}

		[DebugAction("RimCourt", "Forcer un état de la cour",
			actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void ForceState()
		{
			var options = new List<DebugMenuOption>
			{
				new DebugMenuOption("Remettre le délai à zéro", DebugMenuOptionMode.Action, ClearCooldown),
				new DebugMenuOption("Oublier les affaires récentes (lève le blocage des 10/20 jours)", DebugMenuOptionMode.Action, ForgetHeard),
				new DebugMenuOption("Forcer une réputation de juge", DebugMenuOptionMode.Action, SetReputation),
				new DebugMenuOption("Hâter les noces promises", DebugMenuOptionMode.Action, RushWeddings),
				new DebugMenuOption("Bannir : reviendra en raid", DebugMenuOptionMode.Action, ExileAsRaider),
				new DebugMenuOption("Bannir : deviendra chef de guerre", DebugMenuOptionMode.Action, ExileAsWarlord),
				new DebugMenuOption("Faire revenir les exilés maintenant", DebugMenuOptionMode.Action, RushExiles),
				new DebugMenuOption("Mettre un colon au pilori", DebugMenuOptionMode.Action, PutInStocks),
			};
			Find.WindowStack.Add(new Dialog_DebugOptionListLister(options, "RimCourt debug : forcer quoi ?"));
		}

		[DebugAction("RimCourt", "État de la cour",
			actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void DumpState()
		{
			var mgr = CourtManager.Instance;
			if (mgr == null) { Log.Message("[RimCourt] pas de manager."); return; }
			Log.Message(mgr.DebugState());
		}

		private static void PickAQuarrel()
			=> PickPair("RimCourt debug : premier colon", "RimCourt debug : second colon", Embitter);

		private static void PickPair(string firstTitle, string secondTitle, System.Action<Pawn, Pawn> action)
		{
			var map = Find.CurrentMap;
			if (map == null) return;
			var colonists = map.mapPawns.FreeColonistsSpawned
				.Where(p => !p.Dead && !p.Downed && p.RaceProps.Humanlike).ToList();
			if (colonists.Count < 2)
			{
				Messages.Message("RimCourt debug : il faut au moins deux colons.",
					MessageTypeDefOf.RejectInput, historical: false);
				return;
			}

			var options = new List<DebugMenuOption>();
			foreach (var first in colonists)
			{
				var a = first;
				options.Add(new DebugMenuOption(a.LabelShortCap, DebugMenuOptionMode.Action, () =>
				{
					var second = new List<DebugMenuOption>();
					foreach (var other in colonists)
					{
						if (other == a) continue;
						var b = other;
						second.Add(new DebugMenuOption(b.LabelShortCap, DebugMenuOptionMode.Action, () => action(a, b)));
					}
					second.SortBy(o => o.label);
					Find.WindowStack.Add(new Dialog_DebugOptionListLister(second, secondTitle));
				}));
			}
			options.SortBy(o => o.label);
			Find.WindowStack.Add(new Dialog_DebugOptionListLister(options, firstTitle));
		}

		private static void Embitter(Pawn a, Pawn b)
		{
			var insult = DefDatabase<ThoughtDef>.GetNamedSilentFail("Insulted");
			if (insult == null) return;
			for (var i = 0; i < 6; i++)
			{
				a.needs?.mood?.thoughts?.memories?.TryGainMemory(insult, b);
				b.needs?.mood?.thoughts?.memories?.TryGainMemory(insult, a);
			}
			Messages.Message("RimCourt debug : " + a.LabelShortCap + " " + (a.relations?.OpinionOf(b) ?? 0)
					+ " / " + b.LabelShortCap + " " + (b.relations?.OpinionOf(a) ?? 0),
				new LookTargets(a), MessageTypeDefOf.NeutralEvent, historical: false);
		}

		private static void MakeBloodDebt()
		{
			PickPair("RimCourt debug : qui a perdu un proche ?", "RimCourt debug : qui l'a tué ?", (victimKin, killer) =>
			{
				var thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("KilledMyFriend");
				if (thought == null) return;
				var mem = victimKin.needs?.mood?.thoughts?.memories;
				mem?.TryGainMemory(thought, killer);
				var planted = mem != null && mem.Memories.Any(m => m.def == thought && m.otherPawn == killer);
				var lord = CourtManager.Instance?.CurrentLord;
				var warn = "";
				if (victimKin == lord || killer == lord)
					warn = " ATTENTION : le seigneur ne peut pas etre partie a une affaire, elle ne sortira jamais.";
				Messages.Message(planted
						? "RimCourt debug : " + victimKin.LabelShortCap + " tient " + killer.LabelShortCap
							+ " pour responsable d'une mort." + warn
						: "RimCourt debug : souvenir refuse (trait psychopathe ? incapable d'humeur ?) sur "
							+ victimKin.LabelShortCap + ".",
					new LookTargets(victimKin),
					planted ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.RejectInput, historical: false);
			});
		}

		private static void SpawnPetitioner()
		{
			var map = Find.CurrentMap;
			if (map == null) return;
			var incident = DefDatabase<IncidentDef>.GetNamedSilentFail("RimCourt_Petitioner");
			if (incident == null) return;
			var parms = StorytellerUtility.DefaultParmsNow(incident.category, map);
			if (!incident.Worker.TryExecute(parms))
				Messages.Message("RimCourt debug : aucun pétitionnaire n'a pu venir (siège du seigneur posé ? un autre déjà en attente ?).",
					MessageTypeDefOf.RejectInput, historical: false);
		}

		private static void SpawnDeserter()
		{
			var map = Find.CurrentMap;
			var mgr = CourtManager.Instance;
			if (map == null || mgr == null) return;
			if (EmpireDeserterUtility.Sender(mgr.LordFor(map)) == null)
			{
				Messages.Message("RimCourt debug : personne n'a de raison d'envoyer un assassin (VFE Empire absent, seigneur sans titre impérial, ou aucune faction hostile ?).",
					MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			SpawnPetitioner();
			var req = mgr.Waiting.Count > 0 ? mgr.Waiting[mgr.Waiting.Count - 1] : null;
			if (req == null || !req.Valid)
			{
				Messages.Message("RimCourt debug : aucun pétitionnaire n'a pu venir.",
					MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			req.deserter = true;
			EmpireDeserterUtility.Arm(req.pawn);
			Messages.Message("RimCourt debug : " + req.pawn.LabelShortCap
					+ " est un déserteur et frappera au verdict. Tenez audience.",
				new LookTargets(req.pawn), MessageTypeDefOf.NeutralEvent, historical: false);
		}

		private static void MakeCaptiveMenu()
		{
			var options = new List<DebugMenuOption>
			{
				new DebugMenuOption("Un pillard qui n'a touché personne", DebugMenuOptionMode.Action, () => MakeCaptive(0)),
				new DebugMenuOption("Un pillard qui a mis un colon à terre", DebugMenuOptionMode.Action, () => MakeCaptive(1)),
				new DebugMenuOption("Un pillard qui a tué un colon", DebugMenuOptionMode.Action, () => MakeCaptive(2)),
				new DebugMenuOption("Un pillard d'une faction acharnée (3 raids)", DebugMenuOptionMode.Action, () => MakeCaptive(3)),
			};
			Find.WindowStack.Add(new Dialog_DebugOptionListLister(options, "RimCourt debug : quel prisonnier ?"));
		}

		private static void MakeCaptive(int kind)
		{
			var map = Find.CurrentMap;
			var mgr = CourtManager.Instance;
			if (map == null || mgr == null) return;
			Faction faction = null;
			foreach (var f in Find.FactionManager.AllFactionsVisible)
				if (!f.IsPlayer && !f.defeated && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer)
					&& OutsiderUtility.GetHumanlikeKind(f) != null) { faction = f; break; }
			if (faction == null)
			{
				Messages.Message("RimCourt debug : aucune faction hostile humaine.", MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			var pawn = OutsiderUtility.GeneratePetitioner(faction, map.Tile);
			if (pawn == null)
			{
				Messages.Message("RimCourt debug : génération du pillard impossible.", MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			var seat = mgr.SeatFor(map);
			var near = seat?.Position ?? map.Center;
			foreach (var b in map.listerBuildings.allBuildingsColonist)
				if (b is Building_Bed bed && bed.ForPrisoners && !bed.Medical) { near = bed.Position; break; }
			var cell = CellFinder.RandomClosewalkCellNear(near, map, 3);
			GenSpawn.Spawn(pawn, cell, map);
			pawn.guest?.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner);
			pawn.guest?.WaitInsteadOfEscapingForDefaultTicks();

			if (kind == 1 || kind == 2)
			{
				Pawn victim = null;
				foreach (var c in map.mapPawns.FreeColonistsSpawned) { victim = c; break; }
				if (victim != null)
				{
					var tr = kind == 2 ? RulePackDefOf.Transition_Died : RulePackDefOf.Transition_Downed;
					Find.BattleLog.Add(new BattleLogEntry_StateTransition(victim, tr, pawn, null, null));
				}
			}
			if (kind == 3) mgr.DebugNoteRaids(faction, CaptiveUtility.RaidsForHabit);

			Messages.Message("RimCourt debug : " + pawn.LabelShortCap + " (" + faction.Name + ") est prisonnier près "
					+ (near == seat?.Position ? "du siège" : "d'un lit de prison") + ". Tenez audience.",
				new LookTargets(pawn), MessageTypeDefOf.NeutralEvent, historical: false);
		}

		private static void ForgetHeard()
		{
			var mgr = CourtManager.Instance;
			if (mgr == null) return;
			var n = mgr.DebugForgetHeard();
			Messages.Message("RimCourt debug : " + n + " affaire(s) récente(s) oubliée(s). Les mêmes parties peuvent replaider.",
				MessageTypeDefOf.NeutralEvent, historical: false);
		}

		private static void ClearCooldown()
		{
			var mgr = CourtManager.Instance;
			if (mgr == null) return;
			mgr.DebugClearCooldown();
			Messages.Message("RimCourt debug : la cour peut siéger de nouveau.",
				MessageTypeDefOf.NeutralEvent, historical: false);
		}

		private static void SetReputation()
		{
			var mgr = CourtManager.Instance;
			if (mgr == null) return;
			var options = new List<DebugMenuOption>
			{
				new DebugMenuOption("Juste", DebugMenuOptionMode.Action, () => mgr.DebugSetReputation(JudgeReputation.StageJust)),
				new DebugMenuOption("Dur", DebugMenuOptionMode.Action, () => mgr.DebugSetReputation(JudgeReputation.StageHarsh)),
				new DebugMenuOption("Vénal", DebugMenuOptionMode.Action, () => mgr.DebugSetReputation(JudgeReputation.StageVenal)),
				new DebugMenuOption("Faible", DebugMenuOptionMode.Action, () => mgr.DebugSetReputation(JudgeReputation.StageWeak)),
				new DebugMenuOption("Aucune (remise à zéro)", DebugMenuOptionMode.Action, () => mgr.DebugSetReputation(JudgeReputation.StageNone)),
			};
			Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
		}

		private static void Betroth()
			=> PickPair("RimCourt debug : premier fiancé", "RimCourt debug : second fiancé", (a, b) =>
			{
				if (a.relations == null || b.relations == null) return;
				if (a.relations.DirectRelationExists(PawnRelationDefOf.Spouse, b))
				{
					Messages.Message("RimCourt debug : ils sont déjà mariés.",
						MessageTypeDefOf.RejectInput, historical: false);
					return;
				}
				if (!a.relations.DirectRelationExists(PawnRelationDefOf.Fiance, b))
				{
					a.relations.TryRemoveDirectRelation(PawnRelationDefOf.Lover, b);
					a.relations.AddDirectRelation(PawnRelationDefOf.Fiance, b);
				}
				Messages.Message("RimCourt debug : " + a.LabelShortCap + " et " + b.LabelShortCap
						+ " sont fiancés. Tenez audience pour la bénédiction.",
					new LookTargets(a), MessageTypeDefOf.NeutralEvent, historical: false);
			});

		private static void MakeTriangle()
			=> PickPair("RimCourt debug : premier rival", "RimCourt debug : second rival", (a, b) =>
				PickOne("RimCourt debug : qui est aimé des deux ?", loved =>
				{
					if (loved == a || loved == b)
					{
						Messages.Message("RimCourt debug : l'aimé doit être un troisième colon.",
							MessageTypeDefOf.RejectInput, historical: false);
						return;
					}
					if (a.relations == null || b.relations == null) return;
					if (!a.relations.DirectRelationExists(PawnRelationDefOf.Lover, loved))
						a.relations.AddDirectRelation(PawnRelationDefOf.Lover, loved);
					if (!b.relations.DirectRelationExists(PawnRelationDefOf.Lover, loved))
						b.relations.AddDirectRelation(PawnRelationDefOf.Lover, loved);
					Messages.Message("RimCourt debug : " + a.LabelShortCap + " et " + b.LabelShortCap
							+ " courtisent tous deux " + loved.LabelShortCap + ". Tenez audience.",
						new LookTargets(loved), MessageTypeDefOf.NeutralEvent, historical: false);
				}));

		private static void MakePenitent()
			=> PickPair("RimCourt debug : qui a fauté ?", "RimCourt debug : qui garde la rancune ?", (guilty, victim) =>
			{
				var insult = DefDatabase<ThoughtDef>.GetNamedSilentFail("Insulted");
				if (insult == null) return;
				for (var i = 0; i < 6; i++)
					victim.needs?.mood?.thoughts?.memories?.TryGainMemory(insult, guilty);
				Messages.Message("RimCourt debug : " + victim.LabelShortCap + " en veut à "
						+ guilty.LabelShortCap + " (" + (victim.relations?.OpinionOf(guilty) ?? 0)
						+ "), qui viendra demander un prix. Tenez audience.",
					new LookTargets(guilty), MessageTypeDefOf.NeutralEvent, historical: false);
			});

		private static void MakeGratitude()
			=> PickPair("RimCourt debug : qui a été sauvé ?", "RimCourt debug : par qui ?", (saved, rescuer) =>
			{
				var rescued = DefDatabase<ThoughtDef>.GetNamedSilentFail("RescuedMe");
				if (rescued == null) return;
				saved.needs?.mood?.thoughts?.memories?.TryGainMemory(rescued, rescuer);
				Messages.Message("RimCourt debug : " + saved.LabelShortCap + " doit la vie à "
						+ rescuer.LabelShortCap + ". Tenez audience.",
					new LookTargets(saved), MessageTypeDefOf.NeutralEvent, historical: false);
			});

		private static void ExileForPardon()
			=> PickOne("RimCourt debug : qui revient demander grâce ?", p =>
			{
				var map = p.MapHeld ?? Find.CurrentMap;
				if (map == null || p.Faction != Faction.OfPlayer) return;
				try
				{
					PawnBanishUtility.Banish(p);
					if (p.Spawned) p.DeSpawn();
					if (!Find.WorldPawns.Contains(p))
						Find.WorldPawns.PassToWorld(p, RimWorld.Planet.PawnDiscardDecideMode.KeepForever);
					if (ExileUtility.ArriveAsPetitioner(p, map))
						Messages.Message("RimCourt debug : " + p.LabelShortCap
								+ " est à la porte. Tenez audience.",
							new LookTargets(p), MessageTypeDefOf.NeutralEvent, historical: false);
					else
						Messages.Message("RimCourt debug : arrivée impossible (siège posé ? un autre "
								+ "pétitionnaire en attente ? une faction amie existe ?).",
							MessageTypeDefOf.RejectInput, historical: false);
				}
				catch (System.Exception e)
				{
					Log.Error("[RimCourt] exil de debug impossible : " + e);
				}
			});

		private static void MakeAdultery()
			=> PickPair("RimCourt debug : qui a trompé ?", "RimCourt debug : quel époux trahi ?", (cheater, wronged) =>
			{
				var cheated = DefDatabase<ThoughtDef>.GetNamedSilentFail("CheatedOnMe");
				if (cheated == null || cheater.relations == null || wronged.relations == null) return;
				if (!cheater.relations.DirectRelationExists(PawnRelationDefOf.Spouse, wronged))
				{
					cheater.relations.TryRemoveDirectRelation(PawnRelationDefOf.Lover, wronged);
					cheater.relations.TryRemoveDirectRelation(PawnRelationDefOf.Fiance, wronged);
					cheater.relations.AddDirectRelation(PawnRelationDefOf.Spouse, wronged);
				}
				wronged.needs?.mood?.thoughts?.memories?.TryGainMemory(cheated, cheater);
				Messages.Message("RimCourt debug : " + wronged.LabelShortCap + " sait que "
						+ cheater.LabelShortCap + " l'a trompé. Tenez audience.",
					new LookTargets(wronged), MessageTypeDefOf.NeutralEvent, historical: false);
			});

		private static void MakeEstate()
			=> PickPair("RimCourt debug : premier héritier", "RimCourt debug : second héritier", (a, b) =>
			{
				try
				{
					var dead = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
						PawnKindDefOf.Colonist, Faction.OfPlayer));
					a.relations?.AddDirectRelation(PawnRelationDefOf.Parent, dead);
					b.relations?.AddDirectRelation(PawnRelationDefOf.Parent, dead);
					Find.WorldPawns.PassToWorld(dead, RimWorld.Planet.PawnDiscardDecideMode.KeepForever);
					dead.Kill(null);

					var grief = DefDatabase<ThoughtDef>.GetNamedSilentFail(
						dead.gender == Gender.Female ? "MyMotherDied" : "MyFatherDied");
					if (grief != null)
					{
						a.needs?.mood?.thoughts?.memories?.TryGainMemory(grief, dead);
						b.needs?.mood?.thoughts?.memories?.TryGainMemory(grief, dead);
					}
					Messages.Message("RimCourt debug : " + dead.LabelShortCap + " meurt en laissant "
							+ a.LabelShortCap + " et " + b.LabelShortCap + " comme héritiers. Tenez audience.",
						new LookTargets(a), MessageTypeDefOf.NeutralEvent, historical: false);
				}
				catch (System.Exception e)
				{
					Log.Error("[RimCourt] fabrication d'héritage impossible : " + e);
				}
			});

		private static void MakePest()
			=> PickPair("RimCourt debug : qui insiste ?", "RimCourt debug : auprès de qui ?", (suitor, target) =>
			{
				var onMe = DefDatabase<ThoughtDef>.GetNamedSilentFail("FailedRomanceAttemptOnMe");
				var rebuffed = DefDatabase<ThoughtDef>.GetNamedSilentFail("RebuffedMyRomanceAttempt");
				if (onMe == null) return;
				for (var i = 0; i < 3; i++)
				{
					target.needs?.mood?.thoughts?.memories?.TryGainMemory(onMe, suitor);
					if (rebuffed != null)
						suitor.needs?.mood?.thoughts?.memories?.TryGainMemory(rebuffed, target);
				}
				Messages.Message("RimCourt debug : " + suitor.LabelShortCap + " a demandé trois fois, "
						+ target.LabelShortCap + " a refusé trois fois. Tenez audience.",
					new LookTargets(target), MessageTypeDefOf.NeutralEvent, historical: false);
			});

		private static void RushWeddings()
		{
			var mgr = CourtManager.Instance;
			if (mgr == null) return;
			var n = mgr.DebugRushWeddings();
			Messages.Message(n == 0
					? "RimCourt debug : aucune noce promise en attente."
					: "RimCourt debug : " + n + " noce(s) tentée(s) dès maintenant.",
				n == 0 ? MessageTypeDefOf.RejectInput : MessageTypeDefOf.NeutralEvent, historical: false);
		}

		private static void ExileAsRaider()
			=> PickOne("RimCourt debug : qui revient en raid ?", p => SendOut(p, warlord: false));

		private static void ExileAsWarlord()
			=> PickOne("RimCourt debug : qui prend la tête d'une faction ?", p => SendOut(p, warlord: true));

		private static void SendOut(Pawn p, bool warlord)
		{
			var mgr = CourtManager.Instance;
			if (mgr == null || p.Faction != Faction.OfPlayer) return;
			var odds = Mathf.RoundToInt(ExileUtility.ReturnChance(p) * 100f);
			try
			{
				PawnBanishUtility.Banish(p);
				if (p.Spawned) p.DeSpawn();
				if (!Find.WorldPawns.Contains(p))
					Find.WorldPawns.PassToWorld(p, RimWorld.Planet.PawnDiscardDecideMode.KeepForever);
				mgr.DebugExile(p, warlord);
				Messages.Message("RimCourt debug : " + p.LabelShortCap + " est parti et reviendra "
						+ (warlord ? "à la tête d'une faction" : "avec une bande")
						+ " (sa vraie chance de retour aurait été de " + odds + "%). "
						+ "Utilisez Faire revenir les exilés maintenant.",
					MessageTypeDefOf.NeutralEvent, historical: false);
			}
			catch (System.Exception e)
			{
				Log.Error("[RimCourt] bannissement de debug impossible : " + e);
			}
		}

		private static void PutInStocks()
			=> PickOne("RimCourt debug : qui va au pilori ?", p =>
			{
				var mgr = CourtManager.Instance;
				if (mgr == null) return;
				if (!mgr.BeginStocks(p))
					Messages.Message("RimCourt debug : pas de pilori libre et atteignable sur la carte.",
						MessageTypeDefOf.RejectInput, historical: false);
			});

		private static void RushExiles()
		{
			var mgr = CourtManager.Instance;
			if (mgr == null) return;
			var n = mgr.DebugRushExiles();
			Messages.Message(n == 0
					? "RimCourt debug : personne n'a été banni par la cour."
					: "RimCourt debug : " + n + " exilé(s) rappelé(s).",
				n == 0 ? MessageTypeDefOf.RejectInput : MessageTypeDefOf.NeutralEvent, historical: false);
		}

		private static void MakeVeteran()
			=> PickOne("RimCourt debug : quel vétéran ?", p =>
			{
				p.records?.AddTo(RecordDefOf.KillsHumanlikes, 4f);
				try
				{
					BodyPartRecord finger = null;
					foreach (var part in p.RaceProps.body.AllParts)
						if (part.def.defName == "Finger" && !p.health.hediffSet.PartIsMissing(part)) { finger = part; break; }
					if (finger != null) p.health.AddHediff(HediffDefOf.MissingBodyPart, finger);
				}
				catch (System.Exception e) { Log.Warning("[RimCourt] mutilation de debug impossible (" + e.Message + ")."); }
				Messages.Message("RimCourt debug : " + p.LabelShortCap + " a perdu un doigt au combat. Tenez audience.",
					new LookTargets(p), MessageTypeDefOf.NeutralEvent, historical: false);
			});

		private static void MakeSlave()
		{
			if (!ModsConfig.IdeologyActive)
			{
				Messages.Message("RimCourt debug : Ideology absent, pas d'esclavage.", MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			PickOne("RimCourt debug : qui devient esclave ?", p =>
			{
				if (p.guest == null) return;
				p.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Slave);
				Messages.Message("RimCourt debug : " + p.LabelShortCap + " est esclave et viendra demander sa liberté. Tenez audience.",
					new LookTargets(p), MessageTypeDefOf.NeutralEvent, historical: false);
			});
		}

		private static void GiveKills()
			=> PickOne("RimCourt debug : quel soldat ?", p =>
			{
				p.records?.AddTo(RecordDefOf.KillsHumanlikes, 8f);
				var kills = p.records?.GetValue(RecordDefOf.KillsHumanlikes) ?? 0f;
				Messages.Message("RimCourt debug : " + p.LabelShortCap + " compte "
						+ Mathf.RoundToInt(kills) + " morts. Il viendra en demander le prix.",
					new LookTargets(p), MessageTypeDefOf.NeutralEvent, historical: false);
			});

		private static void GiveGrievances()
		{
			var lord = LordHere();
			if (lord == null) return;
			PickOne("RimCourt debug : qui en veut au seigneur ?", p =>
			{
				if (p == lord)
				{
					Messages.Message("RimCourt debug : le seigneur ne peut pas s'accuser lui-même de cette façon.",
						MessageTypeDefOf.RejectInput, historical: false);
					return;
				}
				var memories = p.needs?.mood?.thoughts?.memories;
				for (var i = 0; i < PetitionWorker_AgainstLord.MinGrievances; i++)
					memories?.TryGainMemory(RimCourtDefOf.RimCourt_LordRuledAgainstMe, lord);
				Messages.Message("RimCourt debug : " + p.LabelShortCap + " a "
						+ PetitionWorker_AgainstLord.GrievanceCount(p, lord) + " griefs contre "
						+ lord.LabelShortCap + ".",
					new LookTargets(p), MessageTypeDefOf.NeutralEvent, historical: false);
			});
		}

		private static void SetUpChallenge()
		{
			var mgr = CourtManager.Instance;
			var lord = LordHere();
			if (mgr == null || lord == null) return;
			PickOne("RimCourt debug : qui réclame le siège ?", p =>
			{
				if (p == lord)
				{
					Messages.Message("RimCourt debug : choisissez quelqu'un d'autre que le seigneur.",
						MessageTypeDefOf.RejectInput, historical: false);
					return;
				}
				mgr.DebugSetReputation(JudgeReputation.StageVenal);
				mgr.DebugForgetHeard();
				mgr.DebugClearCooldown();
				var social = p.skills?.GetSkill(SkillDefOf.Social);
				if (social != null && social.Level < PetitionWorker_Challenge.MinSocial)
					social.Level = PetitionWorker_Challenge.MinSocial;
				Embitter(p, lord);
				Messages.Message("RimCourt debug : " + p.LabelShortCap + " pense "
						+ (p.relations?.OpinionOf(lord) ?? 0) + " de " + lord.LabelShortCap + ", réputation vénale, affaires récentes oubliées. "
						+ (PetitionWorker_Challenge.WouldRise(p, lord)
							? "Il se lèvera si c'est bien " + lord.LabelShortCap + " qui siège."
							: "Pas encore assez (opinion > -15 ou social < 5)."),
					new LookTargets(p), MessageTypeDefOf.NeutralEvent, historical: false);
			});
		}

		private static Pawn LordHere()
		{
			var map = Find.CurrentMap;
			var lord = map == null ? null : CourtManager.Instance?.LordFor(map);
			if (lord == null)
				Messages.Message("RimCourt debug : personne ne peut tenir audience (siège du seigneur posé ?).",
					MessageTypeDefOf.RejectInput, historical: false);
			return lord;
		}

		private static void PickOne(string title, System.Action<Pawn> action)
		{
			var map = Find.CurrentMap;
			if (map == null) return;
			var options = new List<DebugMenuOption>();
			foreach (var c in map.mapPawns.FreeColonistsSpawned)
			{
				if (c.Dead || !c.RaceProps.Humanlike) continue;
				var p = c;
				options.Add(new DebugMenuOption(p.LabelShortCap, DebugMenuOptionMode.Action, () => action(p)));
			}
			if (options.Count == 0)
			{
				Messages.Message("RimCourt debug : aucun colon disponible.",
					MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			options.SortBy(o => o.label);
			Find.WindowStack.Add(new Dialog_DebugOptionListLister(options, title));
		}
	}
}
