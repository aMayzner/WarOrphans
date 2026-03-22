using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace WarOrphans
{
    public class QuestNode_Root_PersecutedOrphans : QuestNode_Root_WarOrphansBase
    {
        private XenotypeDef chosenXenotype;

        protected override void RunInt()
        {
            // Reset per generation — RimWorld reuses the same node instance
            chosenXenotype = null;

            // Pick a random non-Baseliner xenotype
            List<XenotypeDef> candidates = DefDatabase<XenotypeDef>.AllDefsListForReading
                .Where(x => x != XenotypeDefOf.Baseliner
                    && x.inheritable
                    && !x.defName.Contains("Android"))
                .ToList();

            chosenXenotype = candidates.Count > 0
                ? candidates.RandomElement()
                : XenotypeDefOf.Baseliner;

            base.RunInt();
        }

        protected override XenotypeDef PickXenotype(List<XenotypeChance> xenotypeChances, float baselinerChance)
        {
            return chosenXenotype;
        }

        protected override string BuildQuestDescription(string place, string factionName, List<Pawn> orphans)
        {
            string xenoLabel = chosenXenotype?.label ?? "Baseliner";
            int count = orphans.Count;

            return place + " has turned against its own. The " + xenoLabel
                + " people there are being hunted and killed for what they are. "
                + factionName + " managed to smuggle out " + count + " " + xenoLabel
                + " children before the worst happened, but their parents weren't so lucky."
                + " These children have nowhere to go. Will you take them in?";
        }

        protected override string BuildLetterLabel(string place, string factionName)
        {
            string xenoLabel = chosenXenotype?.label ?? "refugees";
            return "Persecuted " + xenoLabel + " children of " + place;
        }
    }
}
