using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace WarOrphans
{
    public class QuestNode_Root_SoleSurvivor : QuestNode
    {
        private const int TimeoutTicks = 120000;

        protected override void RunInt()
        {
            Quest quest = QuestGen.quest;
            Slate slate = QuestGen.slate;
            Map map = QuestGen_Get.GetMap();

            slate.Set("resolvedQuestDescription", "A sole survivor needs your help.");

            // Find faction
            Faction faction = Find.FactionManager.AllFactions
                .Where(f => f != Faction.OfPlayer
                    && !f.Hidden
                    && f.def.humanlikeFaction
                    && !f.HostileTo(Faction.OfPlayer)
                    && Find.WorldObjects.Settlements.Any(s => s.Faction == f))
                .RandomElementWithFallback(null);

            if (faction == null)
                return;

            Settlement settlement = Find.WorldObjects.Settlements
                .Where(s => s.Faction == faction)
                .RandomElement();
            string place = settlement.Label;
            string factionName = faction.Name;
            PawnKindDef pawnKind = faction.def.basicMemberKind ?? PawnKindDefOf.Villager;

            // Xenotype from faction
            List<XenotypeChance> xenotypeChances = new List<XenotypeChance>();
            float baselinerChance = 1f;
            XenotypeSet xenoSet = faction.def.xenotypeSet;
            if (xenoSet != null && xenoSet.Count > 0)
            {
                for (int i = 0; i < xenoSet.Count; i++)
                    xenotypeChances.Add(xenoSet[i]);
                baselinerChance = xenoSet.BaselinerChance;
            }
            XenotypeDef xenotype = RollXenotype(xenotypeChances, baselinerChance);

            // Generate one child, age 7-13 (old enough to have survived alone)
            float age = Rand.Range(7f, 13f);
            Pawn child = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                kind: pawnKind,
                faction: faction,
                context: PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true,
                canGeneratePawnRelations: false,
                colonistRelationChanceFactor: 0f,
                forcedXenotype: xenotype ?? XenotypeDefOf.Baseliner,
                developmentalStages: DevelopmentalStage.Child,
                fixedBiologicalAge: age,
                fixedChronologicalAge: age
            ));

            // Survival skills — this child kept themselves alive alone
            if (child.skills != null)
            {
                child.skills.GetSkill(SkillDefOf.Melee).Level += Rand.RangeInclusive(2, 4);
                child.skills.GetSkill(SkillDefOf.Plants).Level += Rand.RangeInclusive(1, 3);
                child.skills.GetSkill(SkillDefOf.Animals).Level += Rand.RangeInclusive(1, 3);
                child.skills.GetSkill(SkillDefOf.Medicine).Level += Rand.RangeInclusive(1, 2);
            }

            // Dead parents
            Pawn mother = GenerateDeadParent(Gender.Female, pawnKind, xenotype, faction);
            Pawn father = GenerateDeadParent(Gender.Male, pawnKind, xenotype, faction);
            child.relations.AddDirectRelation(PawnRelationDefOf.Parent, mother);
            child.relations.AddDirectRelation(PawnRelationDefOf.Parent, father);

            // Extreme trauma — sole survivors have it worst
            Hediff trauma = HediffMaker.MakeHediff(
                DefDatabase<HediffDef>.GetNamed("WarOrphans_WarTrauma"), child);
            trauma.Severity = 1.0f; // always max
            child.health.AddHediff(trauma);

            child.needs?.mood?.thoughts?.memories?.TryGainMemory(
                DefDatabase<ThoughtDef>.GetNamed("WarOrphans_ParentsDied"));

            // Severe malnutrition — survived alone for a while
            Hediff malnutrition = HediffMaker.MakeHediff(HediffDefOf.Malnutrition, child);
            malnutrition.Severity = Rand.Range(0.4f, 0.8f);
            child.health.AddHediff(malnutrition);

            // Multiple injuries
            int childAge = child.ageTracker.AgeBiologicalYears;
            for (int i = 0; i < Rand.RangeInclusive(2, 4); i++)
            {
                BodyPartRecord part = child.health.hediffSet.GetRandomNotMissingPart(
                    DamageDefOf.Cut, BodyPartHeight.Undefined, BodyPartDepth.Outside);
                if (part == null) break;
                Hediff_Injury injury = (Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut, child, part);
                injury.Severity = Rand.Range(2f, 7f);
                child.health.AddHediff(injury, part);
            }

            // Higher chance of missing part — 40%
            if (Rand.Chance(0.4f))
            {
                BodyPartRecord limbPart = child.health.hediffSet.GetNotMissingParts(
                        BodyPartHeight.Undefined, BodyPartDepth.Outside)
                    .Where(p => !p.def.tags.NullOrEmpty()
                        && (p.def.tags.Contains(BodyPartTagDefOf.ManipulationLimbSegment)
                            || p.def.tags.Contains(BodyPartTagDefOf.MovingLimbSegment)
                            || p.def.tags.Contains(BodyPartTagDefOf.ManipulationLimbDigit)
                            || p.def.tags.Contains(BodyPartTagDefOf.MovingLimbDigit)
                            || p.def.tags.Contains(BodyPartTagDefOf.SightSource)
                            || p.def.tags.Contains(BodyPartTagDefOf.HearingSource)))
                    .RandomElementWithFallback(null);

                if (limbPart != null)
                {
                    Hediff_MissingPart missing = (Hediff_MissingPart)HediffMaker.MakeHediff(
                        HediffDefOf.MissingBodyPart, child, limbPart);
                    missing.lastInjury = HediffDefOf.Cut;
                    missing.IsFresh = false;
                    child.health.AddHediff(missing, limbPart);
                }
            }

            // Tattered clothes
            if (child.apparel != null)
            {
                foreach (Apparel ap in child.apparel.WornApparel)
                {
                    ap.HitPoints = (int)(ap.MaxHitPoints * Rand.Range(0.05f, 0.2f));
                    if (Rand.Chance(0.4f))
                        ap.WornByCorpse = true;
                }
            }

            if (child.def != ThingDefOf.Human)
                return;

            if (!child.IsWorldPawn())
                Find.WorldPawns.PassToWorld(child);

            // Gratitude toward colonists
            ThoughtDef rescuedMe = DefDatabase<ThoughtDef>.GetNamed("WarOrphans_RescuedMe");
            ThoughtDef rescuedOrphan = DefDatabase<ThoughtDef>.GetNamed("WarOrphans_RescuedOrphan");
            List<Pawn> colonists = map.mapPawns.FreeColonists.ToList();
            foreach (Pawn colonist in colonists)
            {
                child.needs?.mood?.thoughts?.memories?.TryGainMemory(rescuedMe, colonist);
                colonist.needs?.mood?.thoughts?.memories?.TryGainMemory(rescuedOrphan, child);
            }

            // Quest text
            string childName = child.Name.ToStringShort;
            string questDescription = childName + " is the sole survivor of " + place + "'s destruction. "
                + factionName + " found " + child.gender.GetPronoun() + " wandering alone, "
                + "barely alive, covered in wounds. Everyone " + childName
                + " ever knew is dead. "
                + childName + " has nowhere left to go. Will you take "
                + child.gender.GetObjective() + " in?";
            slate.Set("resolvedQuestDescription", questDescription);

            quest.challengeRating = 3;

            // Accept/Reject
            string signalAccept = QuestGenUtility.HardcodedSignalWithQuestID("Accept");
            string signalReject = QuestGenUtility.HardcodedSignalWithQuestID("Reject");

            List<Pawn> orphans = new List<Pawn> { child };

            quest.Signal(signalAccept, delegate
            {
                quest.SetFaction(orphans.Cast<Thing>(), Faction.OfPlayer);
                quest.PawnsArrive(orphans, null, map.Parent, PawnsArrivalModeDefOf.EdgeWalkIn);
                QuestGen_End.End(quest, QuestEndOutcome.Success,
                    goodwillChangeAmount: 15, goodwillChangeFactionOf: faction);
            });

            quest.Signal(signalReject, delegate
            {
                QuestGen_End.End(quest, QuestEndOutcome.Fail,
                    goodwillChangeAmount: -10, goodwillChangeFactionOf: faction);
            });

            // Reject/timeout mood penalty
            ThoughtDef rejectedOrphans = DefDatabase<ThoughtDef>.GetNamed("WarOrphans_RejectedOrphans");
            quest.Signal(signalReject, delegate
            {
                foreach (Pawn colonist in map.mapPawns.FreeColonists)
                    colonist.needs?.mood?.thoughts?.memories?.TryGainMemory(rejectedOrphans);
            });

            quest.Delay(TimeoutTicks, delegate
            {
                QuestGen_End.End(quest, QuestEndOutcome.Fail,
                    goodwillChangeAmount: -5, goodwillChangeFactionOf: faction);
            });

            // Colony mood boost on accept
            ThoughtDef tookInOrphans = DefDatabase<ThoughtDef>.GetNamed("WarOrphans_TookInOrphans");
            quest.Signal(signalAccept, delegate
            {
                foreach (Pawn colonist in map.mapPawns.FreeColonists)
                    colonist.needs?.mood?.thoughts?.memories?.TryGainMemory(tookInOrphans);
            });

            // Letter
            ChoiceLetter_AcceptJoiner letter = (ChoiceLetter_AcceptJoiner)LetterMaker.MakeLetter(
                "Sole Survivor from " + place, questDescription, LetterDefOf.AcceptJoiner);
            letter.signalAccept = signalAccept;
            letter.signalReject = signalReject;
            letter.quest = quest;
            letter.overrideMap = map;
            letter.StartTimeout(TimeoutTicks);
            Find.LetterStack.ReceiveLetter(letter);

            // Small chance of follow-up raid (20%) targeting the colony
            // Delayed 3-7 days after acceptance
            if (Rand.Chance(0.2f))
            {
                Faction hostileFaction = Find.FactionManager.AllFactions
                    .Where(f => f != Faction.OfPlayer && !f.Hidden && f.HostileTo(Faction.OfPlayer)
                        && f.def.humanlikeFaction)
                    .RandomElementWithFallback(null);

                if (hostileFaction != null)
                {
                    int raidDelay = Rand.RangeInclusive(3, 7) * 60000;
                    quest.Signal(signalAccept, delegate
                    {
                        quest.Delay(raidDelay, delegate
                        {
                            quest.Letter(LetterDefOf.ThreatBig, null, null, hostileFaction, null, false,
                                QuestPart.SignalListenMode.OngoingOnly, null, false,
                                text: "Attackers have come looking for the war orphan you took in. "
                                    + hostileFaction.Name + " wants " + childName + " dead.",
                                label: "Retribution for " + childName);
                            float raidPoints = StorytellerUtility.DefaultThreatPointsNow(map) * Rand.Range(0.5f, 0.8f);
                            IncidentParms parms = new IncidentParms();
                            parms.target = map;
                            parms.faction = hostileFaction;
                            parms.points = raidPoints;
                            IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
                        });
                    });
                }
            }
        }

        private XenotypeDef RollXenotype(List<XenotypeChance> chances, float baselinerChance)
        {
            if (baselinerChance > 0f && Rand.Chance(baselinerChance))
                return XenotypeDefOf.Baseliner;
            if (chances.NullOrEmpty())
                return XenotypeDefOf.Baseliner;
            return chances.RandomElementByWeight(x => x.chance).xenotype;
        }

        private Pawn GenerateDeadParent(Gender gender, PawnKindDef pawnKind, XenotypeDef xenotype, Faction faction)
        {
            Pawn parent = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                kind: pawnKind,
                faction: faction,
                context: PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true,
                fixedGender: gender,
                canGeneratePawnRelations: false,
                colonistRelationChanceFactor: 0f,
                forcedXenotype: xenotype ?? XenotypeDefOf.Baseliner
            ));
            long ageTicks = (long)(Rand.Range(25f, 50f) * 3600000f);
            parent.ageTracker.AgeBiologicalTicks = ageTicks;
            parent.ageTracker.AgeChronologicalTicks = ageTicks;
            parent.Kill(null);
            return parent;
        }

        protected override bool TestRunInt(Slate slate)
        {
            if (QuestGen_Get.GetMap() == null)
                return false;
            return Find.FactionManager.AllFactions.Any(f =>
                f != Faction.OfPlayer && !f.Hidden && f.def.humanlikeFaction
                && !f.HostileTo(Faction.OfPlayer)
                && Find.WorldObjects.Settlements.Any(s => s.Faction == f));
        }
    }
}
