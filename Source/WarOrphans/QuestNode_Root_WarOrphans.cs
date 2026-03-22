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

            // Fallback description in case anything below fails
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

            // Build xenotype chance list for per-pawn rolls
            List<XenotypeChance> xenotypeChances = new List<XenotypeChance>();
            XenotypeSet xenoSet = faction.def.xenotypeSet;
            if (xenoSet != null && xenoSet.Count > 0)
            {
                for (int i = 0; i < xenoSet.Count; i++)
                    xenotypeChances.Add(xenoSet[i]);
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

            // Generate orphans with dead parents, trauma, and xenotypes
            List<Pawn> orphans = new List<Pawn>();
            foreach (List<int> family in families)
            {
                Pawn mother = GenerateDeadParent(Gender.Female, pawnKind, RollXenotype(xenotypeChances), faction);
                Pawn father = GenerateDeadParent(Gender.Male, pawnKind, RollXenotype(xenotypeChances), faction);

                for (int i = 0; i < family.Count; i++)
                {
                    float age = Rand.Range(0.1f, 13f);
                    Pawn child = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                        kind: pawnKind,
                        faction: faction,
                        context: PawnGenerationContext.NonPlayer,
                        forceGenerateNewPawn: true,
                        canGeneratePawnRelations: false,
                        colonistRelationChanceFactor: 0f,
                        forcedXenotype: RollXenotype(xenotypeChances) ?? XenotypeDefOf.Baseliner
                    ));

                    // Set age after generation to avoid conflicts with other mods
                    long ageTicks = (long)(age * 3600000f);
                    child.ageTracker.AgeBiologicalTicks = ageTicks;
                    child.ageTracker.AgeChronologicalTicks = ageTicks;

                    // Sibling relation is implied automatically from sharing parents
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

                    if (!child.IsWorldPawn())
                        Find.WorldPawns.PassToWorld(child);

                    orphans.Add(child);
                }
            }

            // Build quest text
            string xenotypeSummary = string.Join(" and ", orphans
                .GroupBy(p => p.genes?.Xenotype?.label ?? "Baseliner")
                .Select(g => g.Count() + " " + g.Key));

            string questDescription = place + " has been devastated by war. " + factionName
                + " are desperate — they have " + xenotypeSummary
                + " orphaned children who will die without someone to care for them."
                + " They beg you to take them in.";
            slate.Set("resolvedQuestDescription", questDescription);

            // Challenge rating scales with number of children
            if (orphanCount <= 1) quest.challengeRating = 1;
            else if (orphanCount <= 3) quest.challengeRating = 2;
            else if (orphanCount <= 5) quest.challengeRating = 3;
            else quest.challengeRating = 4;

            // Accept/Reject signals (vanilla WandererJoin pattern)
            string signalAccept = QuestGenUtility.HardcodedSignalWithQuestID("Accept");
            string signalReject = QuestGenUtility.HardcodedSignalWithQuestID("Reject");

            quest.Signal(signalAccept, delegate
            {
                quest.SetFaction(orphans.Cast<Thing>(), Faction.OfPlayer);
                quest.PawnsArrive(orphans, null, map.Parent, PawnsArrivalModeDefOf.EdgeWalkIn);
                QuestGen_End.End(quest, QuestEndOutcome.Success);
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
                "War Orphans from " + factionName, questDescription, LetterDefOf.AcceptJoiner);
            letter.signalAccept = signalAccept;
            letter.signalReject = signalReject;
            letter.quest = quest;
            letter.overrideMap = map;
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
