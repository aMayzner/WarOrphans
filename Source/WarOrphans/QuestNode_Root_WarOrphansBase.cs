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

        protected List<Pawn> lastGeneratedOrphans;

        protected abstract string BuildQuestDescription(string place, string factionName, List<Pawn> orphans);
        protected abstract string BuildLetterLabel(string place, string factionName);
        protected abstract XenotypeDef PickXenotype(List<XenotypeChance> xenotypeChances, float baselinerChance);

        protected virtual int GetOrphanCount() => Rand.RangeInclusive(1, 7);
        protected virtual float GetMinAge() => 3f;
        protected virtual float GetMaxAge() => 13f;
        protected virtual float GetTraumaSeverity(int childAge) => childAge / 13f;
        protected virtual float GetMalnutritionSeverity() => Rand.Range(0.15f, 0.6f);
        protected virtual float GetMissingPartChance(int childAge) => childAge / 90f;
        protected virtual float GetMinClothDurability() => 0.1f;
        protected virtual float GetMaxClothDurability() => 0.4f;
        protected virtual float GetTaintedChance() => 0.25f;
        protected virtual void ApplyExtraEffects(Pawn child, Quest quest, Map map, Faction faction, string signalAccept) { }
        protected virtual void ApplyExtraSkills(Pawn child) { }

        protected static BodyPartRecord GetRandomNonVitalOutsidePart(Pawn pawn)
        {
            return pawn.health.hediffSet.GetNotMissingParts(
                    BodyPartHeight.Undefined, BodyPartDepth.Outside)
                .Where(p => !p.def.tags.NullOrEmpty()
                    && (p.def.tags.Contains(BodyPartTagDefOf.ManipulationLimbSegment)
                        || p.def.tags.Contains(BodyPartTagDefOf.MovingLimbSegment)
                        || p.def.tags.Contains(BodyPartTagDefOf.ManipulationLimbDigit)
                        || p.def.tags.Contains(BodyPartTagDefOf.MovingLimbDigit)
                        || p.def.tags.Contains(BodyPartTagDefOf.SightSource)
                        || p.def.tags.Contains(BodyPartTagDefOf.HearingSource)))
                .RandomElementWithFallback(null);
        }

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
            int orphanCount = GetOrphanCount();
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
                    float age = Rand.Range(GetMinAge(), GetMaxAge());
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

                    ApplyExtraSkills(child);

                    // War trauma -- older children are more affected
                    int childAge = child.ageTracker.AgeBiologicalYears;
                    if (childAge >= 3)
                    {
                        HediffDef traumaDef = DefDatabase<HediffDef>.GetNamedSilentFail("WarOrphans_WarTrauma");
                        if (traumaDef != null)
                        {
                            Hediff trauma = HediffMaker.MakeHediff(traumaDef, child);
                            trauma.Severity = GetTraumaSeverity(childAge);
                            child.health.AddHediff(trauma);
                        }

                        ThoughtDef parentsDied = DefDatabase<ThoughtDef>.GetNamedSilentFail("WarOrphans_ParentsDied");
                        if (parentsDied != null)
                            child.needs?.mood?.thoughts?.memories?.TryGainMemory(parentsDied);
                    }

                    // Malnutrition
                    Hediff malnutrition = HediffMaker.MakeHediff(HediffDefOf.Malnutrition, child);
                    malnutrition.Severity = GetMalnutritionSeverity();
                    child.health.AddHediff(malnutrition);

                    // Temperature exposure based on current map conditions
                    float temperature = map.mapTemperature.OutdoorTemp;
                    if (temperature < 0f)
                    {
                        Hediff hypo = HediffMaker.MakeHediff(HediffDefOf.Hypothermia, child);
                        hypo.Severity = Rand.Range(0.1f, 0.4f);
                        child.health.AddHediff(hypo);
                    }
                    else if (temperature > 35f)
                    {
                        Hediff heat = HediffMaker.MakeHediff(HediffDefOf.Heatstroke, child);
                        heat.Severity = Rand.Range(0.1f, 0.4f);
                        child.health.AddHediff(heat);
                    }

                    // Injuries -- older children had more exposure
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

                    // Small chance of missing body part
                    float missingPartChance = GetMissingPartChance(childAge);
                    if (Rand.Chance(missingPartChance))
                    {
                        BodyPartRecord limbPart = GetRandomNonVitalOutsidePart(child);

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
                            ap.HitPoints = (int)(ap.MaxHitPoints * Rand.Range(GetMinClothDurability(), GetMaxClothDurability()));
                            if (Rand.Chance(GetTaintedChance()))
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

            lastGeneratedOrphans = orphans;

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

            // Apply social thoughts AFTER pawns arrive (needs must be initialized)
            QuestPart_ApplyOrphanThoughts thoughtsPart = new QuestPart_ApplyOrphanThoughts();
            thoughtsPart.inSignal = signalAccept;
            thoughtsPart.orphans.AddRange(orphans);
            thoughtsPart.map = map;
            quest.AddPart(thoughtsPart);

            quest.Signal(signalReject, delegate
            {
                QuestGen_End.End(quest, QuestEndOutcome.Fail,
                    goodwillChangeAmount: -10, goodwillChangeFactionOf: faction);
            });

            // Reject mood penalty via QuestPart too
            QuestPart_RejectMood rejectPart = new QuestPart_RejectMood();
            rejectPart.inSignal = signalReject;
            rejectPart.map = map;
            quest.AddPart(rejectPart);

            quest.Delay(TimeoutTicks, delegate
            {
                QuestGen_End.End(quest, QuestEndOutcome.Fail,
                    goodwillChangeAmount: -5, goodwillChangeFactionOf: faction);
            });

            // Accept/reject letter -- named after the settlement
            ChoiceLetter_AcceptJoiner letter = (ChoiceLetter_AcceptJoiner)LetterMaker.MakeLetter(
                BuildLetterLabel(place, factionName), questDescription, LetterDefOf.AcceptJoiner);
            letter.signalAccept = signalAccept;
            letter.signalReject = signalReject;
            letter.quest = quest;
            letter.overrideMap = map;
            letter.StartTimeout(TimeoutTicks);
            Find.LetterStack.ReceiveLetter(letter);

            // Subclass hook for extra effects (e.g. raid follow-up)
            foreach (Pawn orphan in orphans)
                ApplyExtraEffects(orphan, quest, map, faction, signalAccept);
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
