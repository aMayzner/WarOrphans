using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace WarOrphans
{
    public class QuestNode_Root_SoleSurvivor : QuestNode_Root_WarOrphansBase
    {
        protected override int GetOrphanCount() => 1;
        protected override float GetMinAge() => 7f;
        protected override float GetMaxAge() => 13f;
        protected override float GetTraumaSeverity(int childAge) => 1.0f;
        protected override float GetMalnutritionSeverity() => Rand.Range(0.4f, 0.8f);
        protected override float GetMissingPartChance(int childAge) => 0.4f;
        protected override float GetMinClothDurability() => 0.05f;
        protected override float GetMaxClothDurability() => 0.2f;
        protected override float GetTaintedChance() => 0.4f;

        protected override XenotypeDef PickXenotype(List<XenotypeChance> xenotypeChances, float baselinerChance)
        {
            return RollXenotype(xenotypeChances, baselinerChance) ?? XenotypeDefOf.Baseliner;
        }

        protected override void ApplyExtraSkills(Pawn child)
        {
            if (child.skills != null)
            {
                child.skills.GetSkill(SkillDefOf.Melee).Level += Rand.RangeInclusive(2, 4);
                child.skills.GetSkill(SkillDefOf.Plants).Level += Rand.RangeInclusive(1, 3);
                child.skills.GetSkill(SkillDefOf.Animals).Level += Rand.RangeInclusive(1, 3);
                child.skills.GetSkill(SkillDefOf.Medicine).Level += Rand.RangeInclusive(1, 2);
            }
        }

        protected override void ApplyExtraEffects(Pawn child, Quest quest, Map map, Faction faction, string signalAccept)
        {
            // Small chance of follow-up raid (20%) targeting the colony
            // Delayed 3-7 days after acceptance
            if (Rand.Chance(0.2f))
            {
                Faction hostileFaction = Find.FactionManager.AllFactions
                    .Where(f => f != Faction.OfPlayer && !f.Hidden && f.HostileTo(Faction.OfPlayer)
                        && f.def.humanlikeFaction)
                    .RandomElementWithFallback(null);

                if (hostileFaction != null)
                {
                    string childName = child.Name.ToStringShort;
                    int raidDelay = Rand.RangeInclusive(3, 7) * 60000;
                    quest.Signal(signalAccept, delegate
                    {
                        quest.Delay(raidDelay, delegate
                        {
                            quest.Letter(LetterDefOf.ThreatBig, null, null, hostileFaction, null, false,
                                QuestPart.SignalListenMode.OngoingOnly, null, false,
                                text: "Attackers have come looking for the war orphan you took in. "
                                    + hostileFaction.Name + " wants " + childName + " dead.",
                                label: "Retribution for " + childName);
                            float raidPoints = StorytellerUtility.DefaultThreatPointsNow(map) * Rand.Range(0.5f, 0.8f);
                            IncidentParms parms = new IncidentParms();
                            parms.target = map;
                            parms.faction = hostileFaction;
                            parms.points = raidPoints;
                            IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
                        });
                    });
                }
            }
        }

        protected override string BuildQuestDescription(string place, string factionName, List<Pawn> orphans)
        {
            Pawn child = orphans[0];
            string childName = child.Name.ToStringShort;
            return childName + " is the sole survivor of " + place + "'s destruction. "
                + factionName + " found " + child.gender.GetPronoun() + " wandering alone, "
                + "barely alive, covered in wounds. Everyone " + childName
                + " ever knew is dead. "
                + childName + " has nowhere left to go. Will you take "
                + child.gender.GetObjective() + " in?";
        }

        protected override string BuildLetterLabel(string place, string factionName)
        {
            return "Sole Survivor from " + place;
        }
    }
}
