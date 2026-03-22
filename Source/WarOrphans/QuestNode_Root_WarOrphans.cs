using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;
using Verse.Grammar;

namespace WarOrphans
{
    public class QuestNode_Root_WarOrphans : QuestNode
    {
        protected override void RunInt()
        {
            Quest quest = QuestGen.quest;
            Slate slate = QuestGen.slate;
            Map map = QuestGen_Get.GetMap();

            int orphanCount = Rand.RangeInclusive(1, 5);

            // Pick a random non-hostile, non-player humanlike faction that has settlements
            Faction faction = Find.FactionManager.AllFactions
                .Where(f => f != Faction.OfPlayer
                    && !f.Hidden
                    && f.def.humanlikeFaction
                    && !f.HostileTo(Faction.OfPlayer)
                    && Find.WorldObjects.Settlements.Any(s => s.Faction == f))
                .RandomElementWithFallback(null);

            if (faction == null)
                return;

            // Pick one of this faction's real settlements
            Settlement settlement = Find.WorldObjects.Settlements
                .Where(s => s.Faction == faction)
                .RandomElement();
            string place = settlement.Label;

            PawnKindDef pawnKind = faction.def.basicMemberKind ?? PawnKindDefOf.Villager;

            // Get the faction's xenotype if it has one
            XenotypeDef xenotype = null;
            XenotypeSet xenoSet = faction.def.xenotypeSet;
            if (xenoSet != null && xenoSet.Count > 0)
            {
                // Build a weighted list from the indexer
                List<XenotypeChance> chances = new List<XenotypeChance>();
                for (int i = 0; i < xenoSet.Count; i++)
                    chances.Add(xenoSet[i]);
                xenotype = chances.RandomElementByWeight(x => x.chance).xenotype;
            }

            // Store values for quest text generation
            string xenotypeName = xenotype != null ? xenotype.label : "Baseliner";
            string factionName = faction.Name;

            slate.Set("orphanCount", orphanCount);
            slate.Set("map", map);
            slate.Set("faction", faction);

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

                    child.relations.AddDirectRelation(PawnRelationDefOf.Parent, mother);
                    child.relations.AddDirectRelation(PawnRelationDefOf.Parent, father);

                    foreach (int sibIdx in family)
                    {
                        if (sibIdx < idx && generated[sibIdx] != null)
                            child.relations.AddDirectRelation(PawnRelationDefOf.Sibling, generated[sibIdx]);
                    }

                    generated[idx] = child;
                    orphans.Add(child);
                }
            }

            // Babies/toddlers (under 3) can't walk — an older child carries them
            List<Pawn> toddlers = orphans.Where(p => p.ageTracker.AgeBiologicalYears < 3).ToList();
            List<Pawn> walkers = orphans.Where(p => p.ageTracker.AgeBiologicalYears >= 3)
                .OrderByDescending(p => p.ageTracker.AgeBiologicalYears).ToList();
            int carrierIndex = 0;
            foreach (Pawn toddler in toddlers)
            {
                if (carrierIndex < walkers.Count)
                {
                    walkers[carrierIndex].carryTracker.TryStartCarry(toddler);
                    carrierIndex++;
                }
            }

            // Reserve pawns and set up arrival
            quest.ReservePawns(orphans);

            string acceptSignal = QuestGenUtility.HardcodedSignalWithQuestID("quest.accepted");

            // Use PawnsArrive with joinPlayer:true — handles faction assignment automatically
            quest.PawnsArrive(
                pawns: orphans,
                inSignal: acceptSignal,
                mapParent: map.Parent,
                arrivalMode: PawnsArrivalModeDefOf.EdgeWalkIn,
                joinPlayer: true,
                customLetterLabel: "War Orphans Arrived",
                customLetterText: orphanCount + " " + xenotypeName + " orphans from " + factionName
                    + " have arrived. Please take good care of them."
            );

            // End quest after acceptance
            quest.End(QuestEndOutcome.Success, inSignal: acceptSignal);

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
            // Quest requires a map and at least one non-hostile faction with settlements
            if (QuestGen_Get.GetMap() == null)
                return false;

            return Find.FactionManager.AllFactions.Any(f =>
                f != Faction.OfPlayer
                && !f.Hidden
                && f.def.humanlikeFaction
                && !f.HostileTo(Faction.OfPlayer)
                && Find.WorldObjects.Settlements.Any(s => s.Faction == f));
        }
    }
}
