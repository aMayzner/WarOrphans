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
        private const int TimeoutTicks = 120000; // 2 days to decide

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
            XenotypeSet xenoSet = faction.def.xenotypeSet;
            if (xenoSet != null && xenoSet.Count > 0)
            {
                for (int i = 0; i < xenoSet.Count; i++)
                    xenotypeChances.Add(xenoSet[i]);
            }

            int orphanCount = Rand.RangeInclusive(1, 7);

            // Group into sibling families
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
                Pawn mother = GenerateDeadParent(Gender.Female, pawnKind, RollXenotype(xenotypeChances), faction);
                Pawn father = GenerateDeadParent(Gender.Male, pawnKind, RollXenotype(xenotypeChances), faction);

                foreach (int idx in family)
                {
                    float age = Rand.Range(0.1f, 13f);
                    PawnGenerationRequest request = new PawnGenerationRequest(
                        kind: pawnKind,
                        faction: faction,
                        context: PawnGenerationContext.NonPlayer,
                        forceGenerateNewPawn: true,
                        canGeneratePawnRelations: false,
                        colonistRelationChanceFactor: 0f,
                        forcedXenotype: RollXenotype(xenotypeChances) ?? XenotypeDefOf.Baseliner
                    );
                    Pawn child = PawnGenerator.GeneratePawn(request);

                    // Set age after generation to avoid mod conflicts
                    long ageTicks = (long)(age * 3600000f);
                    child.ageTracker.AgeBiologicalTicks = ageTicks;
                    child.ageTracker.AgeChronologicalTicks = ageTicks;

                    child.relations.AddDirectRelation(PawnRelationDefOf.Parent, mother);
                    child.relations.AddDirectRelation(PawnRelationDefOf.Parent, father);

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

                    if (!child.IsWorldPawn())
                        Find.WorldPawns.PassToWorld(child);

                    orphans.Add(child);
                }
            }

            // Xenotype summary
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

            // Description
            string questDescription = place + " has been devastated by war. " + factionName
                + " are desperate — they have " + xenotypeSummary
                + " orphaned children who will die without someone to care for them. They beg you to take them in.";
            slate.Set("resolvedQuestDescription", questDescription);

            // --- Accept/Reject signals (vanilla WandererJoin pattern) ---
            string signalAccept = QuestGenUtility.HardcodedSignalWithQuestID("Accept");
            string signalReject = QuestGenUtility.HardcodedSignalWithQuestID("Reject");

            // On accept: set faction, spawn, end success
            quest.Signal(signalAccept, delegate
            {
                quest.SetFaction(orphans.Cast<Thing>(), Faction.OfPlayer);
                quest.PawnsArrive(orphans, null, map.Parent, PawnsArrivalModeDefOf.EdgeWalkIn);
                quest.End(QuestEndOutcome.Success);
            });

            // On reject: end fail
            quest.Signal(signalReject, delegate
            {
                quest.End(QuestEndOutcome.Fail);
            });

            // Timeout: auto-reject after 2 days
            quest.Delay(TimeoutTicks, delegate
            {
                quest.End(QuestEndOutcome.Fail);
            });

            // Send accept/reject letter (like vanilla wanderer join)
            TaggedString letterLabel = "War Orphans from " + factionName;
            TaggedString letterText = questDescription;

            ChoiceLetter_AcceptJoiner letter = (ChoiceLetter_AcceptJoiner)LetterMaker.MakeLetter(
                letterLabel, letterText, LetterDefOf.AcceptJoiner);
            letter.signalAccept = signalAccept;
            letter.signalReject = signalReject;
            letter.quest = quest;
            letter.StartTimeout(TimeoutTicks);
            Find.LetterStack.ReceiveLetter(letter);
        }

        private XenotypeDef RollXenotype(List<XenotypeChance> chances)
        {
            if (chances.NullOrEmpty())
                return null;
            return chances.RandomElementByWeight(x => x.chance).xenotype;
        }

        private Pawn GenerateDeadParent(Gender gender, PawnKindDef pawnKind, XenotypeDef xenotype, Faction faction)
        {
            float age = Rand.Range(25f, 50f);
            PawnGenerationRequest request = new PawnGenerationRequest(
                kind: pawnKind,
                faction: faction,
                context: PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true,
                fixedGender: gender,
                canGeneratePawnRelations: false,
                colonistRelationChanceFactor: 0f,
                forcedXenotype: xenotype ?? XenotypeDefOf.Baseliner
            );
            Pawn parent = PawnGenerator.GeneratePawn(request);
            long ageTicks = (long)(age * 3600000f);
            parent.ageTracker.AgeBiologicalTicks = ageTicks;
            parent.ageTracker.AgeChronologicalTicks = ageTicks;
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
