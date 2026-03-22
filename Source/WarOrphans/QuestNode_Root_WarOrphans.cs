using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.QuestGen;
using Verse;
using Verse.Grammar;

namespace WarOrphans
{
    public class QuestNode_Root_WarOrphans : QuestNode
    {
        private static readonly string[] PlaceNames = new[]
        {
            "Millbrook", "Ashford", "Redwall", "Thornfield", "Dusthaven",
            "Ironvale", "Crowshollow", "Brackenmoor", "Willowstead", "Stonebridge",
            "Ember Creek", "Hollow Pine", "Raven's End", "Burnt Crossing", "Pale Ridge"
        };

        protected override void RunInt()
        {
            Quest quest = QuestGen.quest;
            Slate slate = QuestGen.slate;
            Map map = QuestGen_Get.GetMap();

            int orphanCount = Rand.RangeInclusive(1, 5);
            string place = PlaceNames.RandomElement();

            // Pick a random non-player faction — orphans will match its type
            Faction faction = Find.FactionManager.AllFactions
                .Where(f => f != Faction.OfPlayer && !f.Hidden && f.def.humanlikeFaction)
                .RandomElementWithFallback(null);
            PawnKindDef pawnKind = faction?.def.basicMemberKind ?? PawnKindDefOf.Villager;

            // Get the faction's xenotype if it has one (e.g. Waster, Neanderthal, Yttakin, etc.)
            XenotypeDef xenotype = null;
            if (faction?.def.xenotypeSet?.xenotypeChances != null && faction.def.xenotypeSet.xenotypeChances.Count > 0)
            {
                // Pick from the faction's xenotype distribution
                xenotype = faction.def.xenotypeSet.xenotypeChances.RandomElementByWeight(x => x.chance).xenotype;
            }

            // Store values for quest text generation
            slate.Set("place", place);
            slate.Set("orphanCount", orphanCount);
            slate.Set("map", map);
            slate.Set("faction", faction);

            // Group orphans into sibling families, then generate them
            // Each orphan has a 60% chance of being a sibling of the previous one
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

            // Generate dead parents for each family
            List<Pawn> orphans = new List<Pawn>();
            Pawn[] generated = new Pawn[orphanCount];
            foreach (List<int> family in families)
            {
                // Create a dead mother and father for this family
                Pawn mother = GenerateDeadParent(Gender.Female, pawnKind, xenotype);
                Pawn father = GenerateDeadParent(Gender.Male, pawnKind, xenotype);

                foreach (int idx in family)
                {
                    float age = Rand.Range(0.1f, 13f);
                    PawnGenerationRequest request = new PawnGenerationRequest(
                        kind: pawnKind,
                        faction: null,
                        context: PawnGenerationContext.NonPlayer,
                        forceGenerateNewPawn: true,
                        fixedBiologicalAge: age,
                        fixedChronologicalAge: age,
                        canGeneratePawnRelations: false,
                        colonistRelationChanceFactor: 0f,
                        forcedXenotype: xenotype
                    );
                    Pawn child = PawnGenerator.GeneratePawn(request);

                    // Set parent relations
                    child.relations.AddDirectRelation(PawnRelationDefOf.Parent, mother);
                    child.relations.AddDirectRelation(PawnRelationDefOf.Parent, father);

                    // Siblings in the same family get sibling relations with each other
                    foreach (int sibIdx in family)
                    {
                        if (sibIdx < idx && generated[sibIdx] != null)
                        {
                            child.relations.AddDirectRelation(PawnRelationDefOf.Sibling, generated[sibIdx]);
                        }
                    }

                    generated[idx] = child;
                    orphans.Add(child);
                }
            }

            // Babies/toddlers (under 3) can't walk — an older child carries them
            List<Pawn> toddlers = orphans.Where(p => p.ageTracker.AgeBiologicalYears < 3).ToList();
            List<Pawn> walkers = orphans.Where(p => p.ageTracker.AgeBiologicalYears >= 3).OrderByDescending(p => p.ageTracker.AgeBiologicalYears).ToList();
            int carrierIndex = 0;
            foreach (Pawn toddler in toddlers)
            {
                if (carrierIndex < walkers.Count)
                {
                    walkers[carrierIndex].carryTracker.TryStartCarry(toddler);
                    carrierIndex++;
                }
            }

            slate.Set("orphans", orphans);

            // Build quest text: just count, xenotype, and origin
            string xenotypeName = xenotype != null ? xenotype.label : "Baseliner";
            string factionName = faction != null ? faction.Name : place;

            // Quest description rules
            List<Rule> rules = new List<Rule>
            {
                new Rule_String("place", place),
                new Rule_String("orphanCount", orphanCount.ToString()),
                new Rule_String("xenotypeName", xenotypeName),
                new Rule_String("factionName", factionName)
            };
            QuestGen.AddQuestDescriptionRules(rules);
            QuestGen.AddQuestNameRules(rules);

            // On quest accepted: spawn pawns at map edge and have them join
            quest.ReservePawns(orphans);

            // Spawn and join on accept
            QuestPart_PawnsArrive pawnsArrive = new QuestPart_PawnsArrive();
            pawnsArrive.pawns.AddRange(orphans);
            pawnsArrive.mapParent = map.Parent;
            pawnsArrive.arrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
            pawnsArrive.inSignal = QuestGenUtility.HardcodedSignalWithQuestID("quest.accepted");
            quest.AddPart(pawnsArrive);

            // Make them join the player faction on arrival
            foreach (Pawn orphan in orphans)
            {
                QuestPart_SetFaction setFaction = new QuestPart_SetFaction();
                setFaction.faction = Faction.OfPlayer;
                setFaction.pawn = orphan;
                setFaction.inSignal = QuestGenUtility.HardcodedSignalWithQuestID("quest.accepted");
                quest.AddPart(setFaction);
            }

            // Send letter on acceptance
            quest.Letter(
                LetterDefOf.PositiveEvent,
                text: orphanCount + " " + xenotypeName + " orphans from " + factionName + " have arrived. Please take good care of them.",
                label: "War Orphans Arrived",
                inSignal: QuestGenUtility.HardcodedSignalWithQuestID("quest.accepted")
            );

            // End quest after acceptance
            quest.End(QuestEndOutcome.Success, inSignal: QuestGenUtility.HardcodedSignalWithQuestID("quest.accepted"));

            // Quest offer description
            string questDescription = place + " has been devastated by war. " + factionName
                + " are desperate — they have " + orphanCount + " " + xenotypeName
                + " orphaned children who will die without someone to care for them. They beg you to take them in.";

            slate.Set("questDescription", questDescription);
        }

        private Pawn GenerateDeadParent(Gender gender, PawnKindDef pawnKind, XenotypeDef xenotype)
        {
            float age = Rand.Range(25f, 50f);
            PawnGenerationRequest request = new PawnGenerationRequest(
                kind: pawnKind,
                faction: null,
                context: PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true,
                fixedBiologicalAge: age,
                fixedChronologicalAge: age,
                fixedGender: gender,
                canGeneratePawnRelations: false,
                colonistRelationChanceFactor: 0f,
                forcedXenotype: xenotype
            );
            Pawn parent = PawnGenerator.GeneratePawn(request);
            parent.Kill(null);
            return parent;
        }

        protected override bool TestRunInt(Slate slate)
        {
            // Quest can run as long as the player has a map
            return QuestGen_Get.GetMap() != null;
        }
    }
}
