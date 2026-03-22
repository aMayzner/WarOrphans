using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace WarOrphans
{
    public class QuestNode_Root_WarOrphans : QuestNode
    {
        protected override void RunInt()
        {
            Quest quest = QuestGen.quest;
            Slate slate = QuestGen.slate;
            Map map = QuestGen_Get.GetMap();

            int orphanCount = Rand.RangeInclusive(1, 7);

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

            // Build the faction's xenotype chance list for per-pawn rolls
            List<XenotypeChance> xenotypeChances = new List<XenotypeChance>();
            XenotypeSet xenoSet = faction.def.xenotypeSet;
            if (xenoSet != null && xenoSet.Count > 0)
            {
                for (int i = 0; i < xenoSet.Count; i++)
                    xenotypeChances.Add(xenoSet[i]);
            }

            string factionName = faction.Name;

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

            // Generate dead parents for each family, each child rolls their own xenotype
            List<Pawn> orphans = new List<Pawn>();
            Pawn[] generated = new Pawn[orphanCount];
            foreach (List<int> family in families)
            {
                Pawn mother = GenerateDeadParent(Gender.Female, pawnKind, RollXenotype(xenotypeChances));
                Pawn father = GenerateDeadParent(Gender.Male, pawnKind, RollXenotype(xenotypeChances));

                // Dead parents must be world pawns for relations to show up
                Find.WorldPawns.PassToWorld(mother);
                Find.WorldPawns.PassToWorld(father);

                foreach (int idx in family)
                {
                    float age = Rand.Range(0.1f, 13f);
                    XenotypeDef childXenotype = RollXenotype(xenotypeChances);
                    PawnGenerationRequest request = new PawnGenerationRequest(
                        kind: pawnKind,
                        faction: null,
                        context: PawnGenerationContext.NonPlayer,
                        forceGenerateNewPawn: true,
                        fixedBiologicalAge: age,
                        fixedChronologicalAge: age,
                        canGeneratePawnRelations: false,
                        colonistRelationChanceFactor: 0f,
                        forcedXenotype: childXenotype
                    );
                    Pawn child = PawnGenerator.GeneratePawn(request);

                    child.relations.AddDirectRelation(PawnRelationDefOf.Parent, mother);
                    child.relations.AddDirectRelation(PawnRelationDefOf.Parent, father);

                    foreach (int sibIdx in family)
                    {
                        if (sibIdx < idx && generated[sibIdx] != null)
                            child.relations.AddDirectRelation(PawnRelationDefOf.Sibling, generated[sibIdx]);
                    }

                    // War trauma — severity scales with age (older = more aware = worse)
                    // Babies and toddlers (under 3) are too young to understand
                    int childAge = child.ageTracker.AgeBiologicalYears;
                    if (childAge >= 3)
                    {
                        float traumaSeverity = childAge / 13f; // age 3 = 0.23, age 13 = 1.0
                        HediffDef traumaDef = DefDatabase<HediffDef>.GetNamed("WarOrphans_WarTrauma");
                        Hediff trauma = HediffMaker.MakeHediff(traumaDef, child);
                        trauma.Severity = traumaSeverity;
                        child.health.AddHediff(trauma);

                        // Sad memory: parents died
                        ThoughtDef parentsDied = DefDatabase<ThoughtDef>.GetNamed("WarOrphans_ParentsDied");
                        child.needs?.mood?.thoughts?.memories?.TryGainMemory(parentsDied);
                    }

                    // Register as world pawn so RimWorld can spawn them on quest accept
                    if (!child.IsWorldPawn())
                        Find.WorldPawns.PassToWorld(child);

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

            // Summarize xenotypes for quest text (e.g. "3 Baseliner and 2 Neanderthal")
            var xenotypeCounts = orphans
                .GroupBy(p => p.genes?.Xenotype?.label ?? "Baseliner")
                .Select(g => g.Count() + " " + g.Key)
                .ToList();
            string xenotypeSummary = string.Join(" and ", xenotypeCounts);

            // Challenge rating scales with number of children
            if (orphanCount <= 1) quest.challengeRating = 1;
            else if (orphanCount <= 3) quest.challengeRating = 2;
            else if (orphanCount <= 5) quest.challengeRating = 3;
            else quest.challengeRating = 4;

            // Quest description — set directly on slate to bypass grammar resolution
            string questDescription = place + " has been devastated by war. " + factionName
                + " are desperate — they have " + xenotypeSummary
                + " orphaned children who will die without someone to care for them. They beg you to take them in.";
            slate.Set("resolvedQuestDescription", questDescription);

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
                customLetterText: xenotypeSummary + " orphans from " + factionName
                    + " have arrived. Please take good care of them."
            );

            // End quest after acceptance
            quest.End(QuestEndOutcome.Success, inSignal: acceptSignal);

        }

        private XenotypeDef RollXenotype(List<XenotypeChance> chances)
        {
            if (chances.NullOrEmpty())
                return null;
            return chances.RandomElementByWeight(x => x.chance).xenotype;
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
