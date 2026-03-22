using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WarOrphans
{
    public class QuestPart_ApplyOrphanThoughts : QuestPart
    {
        public string inSignal;
        public List<Pawn> orphans = new List<Pawn>();
        public Map map;

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            base.Notify_QuestSignalReceived(signal);
            if (signal.tag != inSignal)
                return;

            ThoughtDef escapedTogether = DefDatabase<ThoughtDef>.GetNamed("WarOrphans_EscapedWarTogether");
            ThoughtDef rescuedMe = DefDatabase<ThoughtDef>.GetNamed("WarOrphans_RescuedMe");
            ThoughtDef rescuedOrphan = DefDatabase<ThoughtDef>.GetNamed("WarOrphans_RescuedOrphan");
            ThoughtDef tookIn = DefDatabase<ThoughtDef>.GetNamed("WarOrphans_TookInOrphans");

            // Permanent bond between orphans
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

            // Mutual gratitude between orphans and colonists
            List<Pawn> colonists = map?.mapPawns?.FreeColonists?.ToList();
            if (colonists != null)
            {
                foreach (Pawn orphan in orphans)
                {
                    foreach (Pawn colonist in colonists)
                    {
                        if (orphan == colonist) continue;
                        orphan.needs?.mood?.thoughts?.memories?.TryGainMemory(rescuedMe, colonist);
                        colonist.needs?.mood?.thoughts?.memories?.TryGainMemory(rescuedOrphan, orphan);
                    }
                }

                // Colony mood boost
                foreach (Pawn colonist in colonists)
                    colonist.needs?.mood?.thoughts?.memories?.TryGainMemory(tookIn);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref inSignal, "inSignal");
            Scribe_Collections.Look(ref orphans, "orphans", LookMode.Reference);
            Scribe_References.Look(ref map, "map");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                orphans.RemoveAll(p => p == null);
        }
    }
}
