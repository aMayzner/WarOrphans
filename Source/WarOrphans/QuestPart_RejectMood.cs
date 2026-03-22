using System.Linq;
using RimWorld;
using Verse;

namespace WarOrphans
{
    public class QuestPart_RejectMood : QuestPart
    {
        public string inSignal;
        public Map map;

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            base.Notify_QuestSignalReceived(signal);
            if (signal.tag != inSignal)
                return;

            ThoughtDef rejected = DefDatabase<ThoughtDef>.GetNamed("WarOrphans_RejectedOrphans");
            foreach (Pawn colonist in map.mapPawns.FreeColonists.ToList())
                colonist.needs?.mood?.thoughts?.memories?.TryGainMemory(rejected);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref inSignal, "inSignal");
            Scribe_References.Look(ref map, "map");
        }
    }
}
