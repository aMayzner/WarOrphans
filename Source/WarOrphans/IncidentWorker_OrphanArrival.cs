using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WarOrphans
{
    public class IncidentWorker_OrphanArrival : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            return map != null && FindValidFaction() != null;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (map == null) return false;

            Faction faction = FindValidFaction();
            if (faction == null) return false;

            Settlement settlement = Find.WorldObjects.Settlements
                .Where(s => s.Faction == faction)
                .RandomElementWithFallback(null);
            if (settlement == null) return false;

            string place = settlement.Label;
            string factionName = faction.Name;
            PawnKindDef pawnKind = faction.def.basicMemberKind ?? PawnKindDefOf.Villager;

            // Xenotype chances
            List<XenotypeChance> xenotypeChances = new List<XenotypeChance>();
            float baselinerChance = 1f;
            XenotypeSet xenoSet = faction.def.xenotypeSet;
            if (xenoSet != null && xenoSet.Count > 0)
            {
                for (int i = 0; i < xenoSet.Count; i++)
                    xenotypeChances.Add(xenoSet[i]);
                baselinerChance = xenoSet.BaselinerChance;
            }

            // Generate 1-5 orphans for the incident version
            int orphanCount = Rand.RangeInclusive(1, 5);
            List<Pawn> orphans = new List<Pawn>();

            // Sibling families
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

            foreach (List<int> family in families)
            {
                XenotypeDef parentXeno = RollXenotype(xenotypeChances, baselinerChance);
                Pawn mother = GenerateDeadParent(Gender.Female, pawnKind, parentXeno, faction);
                Pawn father = GenerateDeadParent(Gender.Male, pawnKind, parentXeno, faction);

                for (int i = 0; i < family.Count; i++)
                {
                    float age = Rand.Range(3f, 13f);
                    XenotypeDef childXeno = RollXenotype(xenotypeChances, baselinerChance);
                    Pawn child = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                        kind: pawnKind,
                        faction: Faction.OfPlayer,
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

                    // War trauma
                    int childAge = child.ageTracker.AgeBiologicalYears;
                    HediffDef traumaDef = DefDatabase<HediffDef>.GetNamedSilentFail("WarOrphans_WarTrauma");
                    if (traumaDef != null && childAge >= 3)
                    {
                        Hediff trauma = HediffMaker.MakeHediff(traumaDef, child);
                        trauma.Severity = childAge / 13f;
                        child.health.AddHediff(trauma);
                    }

                    ThoughtDef parentsDied = DefDatabase<ThoughtDef>.GetNamedSilentFail("WarOrphans_ParentsDied");
                    if (parentsDied != null && childAge >= 3)
                        child.needs?.mood?.thoughts?.memories?.TryGainMemory(parentsDied);

                    // Malnutrition
                    Hediff malnutrition = HediffMaker.MakeHediff(HediffDefOf.Malnutrition, child);
                    malnutrition.Severity = Rand.Range(0.15f, 0.6f);
                    child.health.AddHediff(malnutrition);

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

                    // Filter non-human
                    if (child.def != ThingDefOf.Human)
                        continue;

                    orphans.Add(child);
                }
            }

            if (orphans.Count == 0)
                return false;

            // Spawn at map edge
            IntVec3 spawnSpot;
            if (!CellFinder.TryFindRandomEdgeCellWith(
                c => c.Standable(map) && !c.Fogged(map), map, CellFinder.EdgeRoadChance_Friendly, out spawnSpot))
                return false;

            foreach (Pawn orphan in orphans)
                GenSpawn.Spawn(orphan, CellFinder.RandomClosewalkCellNear(spawnSpot, map, 5), map);

            // Escaped war together bond
            ThoughtDef escapedTogether = DefDatabase<ThoughtDef>.GetNamedSilentFail("WarOrphans_EscapedWarTogether");
            if (escapedTogether != null && orphans.Count > 1)
            {
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
            }

            // Xenotype summary
            string xenotypeSummary = string.Join(" and ", orphans
                .GroupBy(p => p.genes?.Xenotype?.label ?? "Baseliner")
                .Select(g => g.Count() + " " + g.Key));

            // Letter
            Find.LetterStack.ReceiveLetter(
                "Orphans from " + place,
                xenotypeSummary + " orphaned children from " + place
                    + " have arrived at your colony. " + factionName
                    + " could not care for them any longer. They are yours now.",
                LetterDefOf.PositiveEvent,
                new TargetInfo(spawnSpot, map));

            return true;
        }

        private Faction FindValidFaction()
        {
            return Find.FactionManager.AllFactions
                .Where(f => f != Faction.OfPlayer
                    && !f.Hidden
                    && f.def.humanlikeFaction
                    && !f.HostileTo(Faction.OfPlayer)
                    && Find.WorldObjects.Settlements.Any(s => s.Faction == f))
                .RandomElementWithFallback(null);
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
    }
}
