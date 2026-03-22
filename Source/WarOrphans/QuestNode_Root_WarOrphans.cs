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

            Faction faction = FindValidFaction();

            if (faction == null)
            {
                slate.Set("resolvedQuestDescription", "No suitable faction found.");
                return;
            }

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

            // Group orphans into sibling families
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

            // Generate dead parents and children
            List<Pawn> orphans = new List<Pawn>();
            Pawn[] generated = new Pawn[orphanCount];
            foreach (List<int> family in families)
            {
                Pawn mother = GenerateDeadParent(Gender.Female, pawnKind, RollXenotype(xenotypeChances));
                Pawn father = GenerateDeadParent(Gender.Male, pawnKind, RollXenotype(xenotypeChances));

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

                    // War trauma scaled by age
                    int childAge = child.ageTracker.AgeBiologicalYears;
                    if (childAge >= 3)
                    {
                        float traumaSeverity = childAge / 13f;
                        HediffDef traumaDef = DefDatabase<HediffDef>.GetNamed("WarOrphans_WarTrauma");
                        Hediff trauma = HediffMaker.MakeHediff(traumaDef, child);
                        trauma.Severity = traumaSeverity;
                        child.health.AddHediff(trauma);

                        ThoughtDef parentsDied = DefDatabase<ThoughtDef>.GetNamed("WarOrphans_ParentsDied");
                        child.needs?.mood?.thoughts?.memories?.TryGainMemory(parentsDied);
                    }

                    // Must be a world pawn for quest system to work
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

            // Summarize xenotypes for quest text
            var xenotypeCounts = orphans
                .GroupBy(p => p.genes?.Xenotype?.label ?? "Baseliner")
                .Select(g => g.Count() + " " + g.Key)
                .ToList();
            string xenotypeSummary = string.Join(" and ", xenotypeCounts);

            // Challenge rating
            if (orphanCount <= 1) quest.challengeRating = 1;
            else if (orphanCount <= 3) quest.challengeRating = 2;
            else if (orphanCount <= 5) quest.challengeRating = 3;
            else quest.challengeRating = 4;

            // Quest description
            string questDescription = place + " has been devastated by war. " + factionName
                + " are desperate — they have " + xenotypeSummary
                + " orphaned children who will die without someone to care for them. They beg you to take them in.";
            slate.Set("resolvedQuestDescription", questDescription);

            // Reserve pawns
            quest.ReservePawns(orphans);

            // On accept: set faction then spawn — matches vanilla WandererJoin pattern
            string signalAccept = QuestGenUtility.HardcodedSignalWithQuestID("quest.accepted");
            quest.Signal(signalAccept, delegate
            {
                quest.SetFaction(orphans.Cast<Thing>(), Faction.OfPlayer);
                quest.PawnsArrive(orphans, null, map.Parent, PawnsArrivalModeDefOf.EdgeWalkIn,
                    customLetterLabel: "War Orphans Arrived",
                    customLetterText: xenotypeSummary + " orphans from " + factionName
                        + " have arrived. Please take good care of them.");
                quest.End(QuestEndOutcome.Success);
            });
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

        private Faction FindValidFaction()
        {
            foreach (Faction f in Find.FactionManager.AllFactions)
            {
                if (f == Faction.OfPlayer) continue;
                if (f.Hidden) continue;
                if (!f.def.humanlikeFaction) continue;
                if (f.HostileTo(Faction.OfPlayer)) continue;
                if (!Find.WorldObjects.Settlements.Any(s => s.Faction == f)) continue;
                return f;
            }
            return null;
        }

        protected override bool TestRunInt(Slate slate)
        {
            if (QuestGen_Get.GetMap() == null)
                return false;

            return FindValidFaction() != null;
        }
    }
}
