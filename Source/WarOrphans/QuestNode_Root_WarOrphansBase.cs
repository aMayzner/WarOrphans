using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace WarOrphans
{
    public abstract class QuestNode_Root_WarOrphansBase : QuestNode
    {
        private const int TimeoutTicks = 120000; // 2 days to decide

        protected abstract string BuildQuestDescription(string place, string factionName, List<Pawn> orphans);
        protected abstract string BuildLetterLabel(string factionName);

        protected abstract XenotypeDef PickXenotype(List<XenotypeChance> xenotypeChances, float baselinerChance);

        protected override void RunInt()
        {
            Quest quest = QuestGen.quest;
            Slate slate = QuestGen.slate;
            Map map = QuestGen_Get.GetMap();

            slate.Set("resolvedQuestDescription", "War orphans need your help.");

            Faction faction = FindValidFaction();
            if (faction == null)
                return;

            Settlement settlement = Find.WorldObjects.Settlements
                .Where(s => s.Faction == faction)
                .RandomElement();
            string place = settlement.Label;
            string factionName = faction.Name;
            PawnKindDef pawnKind = faction.def.basicMemberKind ?? PawnKindDefOf.Villager;

            // Build xenotype chance list
            List<XenotypeChance> xenotypeChances = new List<XenotypeChance>();
            float baselinerChance = 1f;
            XenotypeSet xenoSet = faction.def.xenotypeSet;
            if (xenoSet != null && xenoSet.Count > 0)
            {
                for (int i = 0; i < xenoSet.Count; i++)
                    xenotypeChances.Add(xenoSet[i]);
                baselinerChance = xenoSet.BaselinerChance;
            }

            // Group orphans into sibling families (60% chance of being siblings)
            int orphanCount = Rand.RangeInclusive(1, 7);
            List<List<int>> families = new List<List<int>>();
            List<int> currentFamily = new List<int> { 0 };
            families.Add(currentFamily);
            for (int i = 1; i < orphanCount; i++)
            {
                if (Rand.Chance(0.6f))
                    currentFamily.Add(i);
                else
                {
                    currentFamily = new List<int> { i };
                    families.Add(currentFamily);
                }
            }

            // Generate orphans
            List<Pawn> orphans = new List<Pawn>();
            foreach (List<int> family in families)
            {
                XenotypeDef parentXeno = PickXenotype(xenotypeChances, baselinerChance);
                Pawn mother = GenerateDeadParent(Gender.Female, pawnKind, parentXeno, faction);
                Pawn father = GenerateDeadParent(Gender.Male, pawnKind, parentXeno, faction);

                for (int i = 0; i < family.Count; i++)
                {
                    float age = Rand.Range(3f, 13f);
                    XenotypeDef childXeno = PickXenotype(xenotypeChances, baselinerChance);
                    Pawn child = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                        kind: pawnKind,
                        faction: faction,
                        context: PawnGenerationContext.NonPlayer,
                        forceGenerateNewPawn: true,
                        canGeneratePawnRelations: false,
                        colonistRelationChanceFactor: 0f,
                        forcedXenotype: childXeno ?? XenotypeDefOf.Baseliner,
                        developmentalStages: DevelopmentalStage.Child,
                        fixedBiologicalAge: age,
                        fixedChronologicalAge: age
                    ));

                    child.relations.AddDirectRelation(PawnRelationDefOf.Parent, mother);
                    child.relations.AddDirectRelation(PawnRelationDefOf.Parent, father);

                    // War trauma — older children are more affected
                    int childAge = child.ageTracker.AgeBiologicalYears;
                    if (childAge >= 3)
                    {
                        Hediff trauma = HediffMaker.MakeHediff(
                            DefDatabase<HediffDef>.GetNamed("WarOrphans_WarTrauma"), child);
                        trauma.Severity = childAge / 13f;
                        child.health.AddHediff(trauma);

                        child.needs?.mood?.thoughts?.memories?.TryGainMemory(
                            DefDatabase<ThoughtDef>.GetNamed("WarOrphans_ParentsDied"));
                    }

                    // Malnutrition
                    Hediff malnutrition = HediffMaker.MakeHediff(HediffDefOf.Malnutrition, child);
                    malnutrition.Severity = Rand.Range(0.15f, 0.6f);
                    child.health.AddHediff(malnutrition);

                    // Injuries — older children had more exposure
                    float injuryChance = childAge / 13f;
                    int injuryCount = 0;
                    while (injuryCount < 3 && Rand.Chance(injuryChance))
                    {
                        BodyPartRecord part = child.health.hediffSet.GetRandomNotMissingPart(
                            DamageDefOf.Cut, BodyPartHeight.Undefined, BodyPartDepth.Outside);
                        if (part == null) break;
                        Hediff_Injury injury = (Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut, child, part);
                        injury.Severity = Rand.Range(1f, 5f);
                        child.health.AddHediff(injury, part);
                        injuryCount++;
                        injuryChance *= 0.6f;
                    }

                    // Tattered clothes
                    if (child.apparel != null)
                    {
                        foreach (Apparel ap in child.apparel.WornApparel)
                        {
                            ap.HitPoints = (int)(ap.MaxHitPoints * Rand.Range(0.1f, 0.4f));
                            if (Rand.Chance(0.25f))
                                ap.WornByCorpse = true;
                        }
                    }

                    if (!child.IsWorldPawn())
                        Find.WorldPawns.PassToWorld(child);

                    orphans.Add(child);
                }
            }

            // Filter out non-human pawns
            orphans.RemoveAll(p => p.def != ThingDefOf.Human);
            if (orphans.Count == 0)
                return;

            // Permanent "escaped war together" social bond between orphans
            ThoughtDef escapedTogether = DefDatabase<ThoughtDef>.GetNamed("WarOrphans_EscapedWarTogether");
            for (int a = 0; a < orphans.Count; a++)
            {
                for (int b = 0; b < orphans.Count; b++)
                {
                    if (a != b)
                    {
                        Thought_MemorySocial thought = (Thought_MemorySocial)ThoughtMaker.MakeThought(escapedTogether);
                        thought.permanent = true;
                        orphans[a].needs?.mood?.thoughts?.memories?.TryGainMemory(thought, orphans[b]);
                    }
                }
            }

            // Mutual gratitude between orphans and existing colonists
            ThoughtDef rescuedMe = DefDatabase<ThoughtDef>.GetNamed("WarOrphans_RescuedMe");
            ThoughtDef rescuedOrphan = DefDatabase<ThoughtDef>.GetNamed("WarOrphans_RescuedOrphan");
            List<Pawn> colonists = map.mapPawns.FreeColonists.ToList();
            foreach (Pawn orphan in orphans)
            {
                foreach (Pawn colonist in colonists)
                {
                    // Orphan is grateful to each colonist
                    orphan.needs?.mood?.thoughts?.memories?.TryGainMemory(rescuedMe, colonist);
                    // Colonist feels good about each orphan
                    colonist.needs?.mood?.thoughts?.memories?.TryGainMemory(rescuedOrphan, orphan);
                }
            }

            // Quest text
            string questDescription = BuildQuestDescription(place, factionName, orphans);
            slate.Set("resolvedQuestDescription", questDescription);

            // Challenge rating
            if (orphans.Count <= 1) quest.challengeRating = 1;
            else if (orphans.Count <= 3) quest.challengeRating = 2;
            else if (orphans.Count <= 5) quest.challengeRating = 3;
            else quest.challengeRating = 4;

            // Accept/Reject signals
            string signalAccept = QuestGenUtility.HardcodedSignalWithQuestID("Accept");
            string signalReject = QuestGenUtility.HardcodedSignalWithQuestID("Reject");

            quest.Signal(signalAccept, delegate
            {
                quest.SetFaction(orphans.Cast<Thing>(), Faction.OfPlayer);
                quest.PawnsArrive(orphans, null, map.Parent, PawnsArrivalModeDefOf.EdgeWalkIn);
                QuestGen_End.End(quest, QuestEndOutcome.Success,
                    goodwillChangeAmount: 15, goodwillChangeFactionOf: faction);
            });

            quest.Signal(signalReject, delegate
            {
                QuestGen_End.End(quest, QuestEndOutcome.Fail);
            });

            quest.Delay(TimeoutTicks, delegate
            {
                QuestGen_End.End(quest, QuestEndOutcome.Fail);
            });

            // Accept/reject letter
            ChoiceLetter_AcceptJoiner letter = (ChoiceLetter_AcceptJoiner)LetterMaker.MakeLetter(
                BuildLetterLabel(factionName), questDescription, LetterDefOf.AcceptJoiner);
            letter.signalAccept = signalAccept;
            letter.signalReject = signalReject;
            letter.quest = quest;
            letter.overrideMap = map;
            letter.StartTimeout(TimeoutTicks);
            Find.LetterStack.ReceiveLetter(letter);
        }

        protected XenotypeDef RollXenotype(List<XenotypeChance> chances, float baselinerChance)
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

        protected Faction FindValidFaction()
        {
            return Find.FactionManager.AllFactions
                .Where(f => f != Faction.OfPlayer
                    && !f.Hidden
                    && f.def.humanlikeFaction
                    && !f.HostileTo(Faction.OfPlayer)
                    && Find.WorldObjects.Settlements.Any(s => s.Faction == f))
                .RandomElementWithFallback(null);
        }

        protected override bool TestRunInt(Slate slate)
        {
            if (QuestGen_Get.GetMap() == null)
                return false;
            return FindValidFaction() != null;
        }
    }
}
