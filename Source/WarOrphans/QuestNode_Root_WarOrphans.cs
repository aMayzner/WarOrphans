using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WarOrphans
{
    public class QuestNode_Root_WarOrphans : QuestNode_Root_WarOrphansBase
    {
        protected override XenotypeDef PickXenotype(List<XenotypeChance> xenotypeChances, float baselinerChance)
        {
            return RollXenotype(xenotypeChances, baselinerChance) ?? XenotypeDefOf.Baseliner;
        }

        protected override string BuildQuestDescription(string place, string factionName, List<Pawn> orphans)
        {
            string xenotypeSummary = string.Join(" and ", orphans
                .GroupBy(p => p.genes?.Xenotype?.label ?? "Baseliner")
                .Select(g => g.Count() + " " + g.Key));

            return place + " has been devastated by war. " + factionName
                + " are desperate — they have " + xenotypeSummary
                + " orphaned children who will die without someone to care for them."
                + " They beg you to take them in.";
        }

        protected override string BuildLetterLabel(string factionName)
        {
            return "War Orphans from " + factionName;
        }
    }
}
