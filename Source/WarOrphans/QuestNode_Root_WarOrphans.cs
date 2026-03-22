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
            // XenotypeSet only stores non-Baseliner chances; Baseliner is implicit
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

            // Generate orphans with dead parents, trauma, and xenotypes
            List<Pawn> orphans = new List<Pawn>();
            foreach (List<int> family in families)
            {
                Pawn mother = GenerateDeadParent(Gender.Female, pawnKind, RollXenotype(xenotypeChances, baselinerChance), faction);
                Pawn father = GenerateDeadParent(Gender.Male, pawnKind, RollXenotype(xenotypeChances, baselinerChance), faction);

                for (int i = 0; i < family.Count; i++)
                {
                    float age = Rand.Range(3f, 13f);
                    Pawn child = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                        kind: pawnKind,
                        faction: faction,
                        context: PawnGenerationContext.NonPlayer,
                        forceGenerateNewPawn: true,
                        canGeneratePawnRelations: false,
                        colonistRelationChanceFactor: 0f,
                        forcedXenotype: RollXenotype(xenotypeChances, baselinerChance) ?? XenotypeDefOf.Baseliner,
                        developmentalStages: DevelopmentalStage.Child,
                        fixedBiologicalAge: age,
                        fixedChronologicalAge: age
                    ));

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

                    // Malnutrition — all children are underfed
                    Hediff malnutrition = HediffMaker.MakeHediff(HediffDefOf.Malnutrition, child);
                    malnutrition.Severity = Rand.Range(0.15f, 0.6f);
                    child.health.AddHediff(malnutrition);

                    // Injuries — older children had more exposure to violence
                    float injuryChance = childAge / 13f; // age 3 = 23%, age 13 = 100%
                    int injuryCount = 0;
                    while (injuryCount < 3 && Rand.Chance(injuryChance))
                    {
                        BodyPartRecord part = child.health.hediffSet.GetRandomNotMissingPart(
                            DamageDefOf.Cut, BodyPartHeight.Undefined, BodyPartDepth.Outside);
                        if (part == null) break;
                        DamageDef dmgType = HealthUtility.RandomViolenceDamageType();
                        float dmgAmount = Rand.Range(1f, 6f);
                        DamageInfo dinfo = new DamageInfo(dmgType, dmgAmount, 0f, -1f, null, part);
                        dinfo.SetAllowDamagePropagation(false);
                        if (!child.health.WouldDieAfterAddingHediff(
                            HealthUtility.GetHediffDefFromDamage(dmgType, child, part), part, dmgAmount))
                        {
                            child.TakeDamage(dinfo);
                        }
                        injuryCount++;
                        injuryChance *= 0.6f; // diminishing chance for each additional injury
                    }

                    // Tatter their clothes — these children fled a war
                    if (child.apparel != null)
                    {
                        foreach (Apparel ap in child.apparel.WornApparel)
                        {
                            // Damage to 10-40% durability
                            ap.HitPoints = (int)(ap.MaxHitPoints * Rand.Range(0.1f, 0.4f));
                            // 25% chance each piece is tainted (worn by the dead)
                            if (Rand.Chance(0.25f))
                                ap.WornByCorpse = true;
                        }
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

        private XenotypeDef RollXenotype(List<XenotypeChance> chances, float baselinerChance)
        {
            // Roll Baseliner based on the implicit chance (1 - sum of explicit chances)
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
