using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WarOrphans
{
    public class QuestNode_Root_PersecutedOrphans : QuestNode_Root_WarOrphansBase
    {
        private XenotypeDef chosenXenotype;

        protected override XenotypeDef PickXenotype(List<XenotypeChance> xenotypeChances, float baselinerChance)
        {
            // All children share the same xenotype — they're persecuted for what they are
            if (chosenXenotype == null)
            {
                // Pick a random non-Baseliner xenotype from the game
                List<XenotypeDef> candidates = DefDatabase<XenotypeDef>.AllDefsListForReading
                    .Where(x => x != XenotypeDefOf.Baseliner
                        && x.inheritable
                        && !x.defName.Contains("Android"))
                    .ToList();

                if (candidates.Count > 0)
                    chosenXenotype = candidates.RandomElement();
                else
                    chosenXenotype = XenotypeDefOf.Baseliner;
            }
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

        protected override string BuildLetterLabel(string factionName)
        {
            string xenoLabel = chosenXenotype?.label ?? "refugees";
            return "Persecuted " + xenoLabel + " orphans";
        }
    }
}
