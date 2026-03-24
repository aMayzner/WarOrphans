using RimWorld;
using Verse;

namespace WarOrphans
{
    public class ScenPart_RecurringOrphans : ScenPart
    {
        private float nextOrphanTick = float.MaxValue;
        private bool firstEventFired;

        private const float FirstEventMinDays = 5f;
        private const float FirstEventMaxDays = 10f;
        private const float MinIntervalDays = 45f;
        private const float MaxIntervalDays = 120f;

        public override void PostGameStart()
        {
            nextOrphanTick = Find.TickManager.TicksGame + Rand.Range(FirstEventMinDays, FirstEventMaxDays) * 60000f;
            firstEventFired = false;
        }

        public override void Tick()
        {
            if (Find.TickManager.TicksGame < nextOrphanTick)
                return;

            Map map = Find.AnyPlayerHomeMap;
            if (map == null)
                return;

            // Prevent double-firing
            nextOrphanTick = float.MaxValue;

            // Fire the orphan arrival incident directly
            IncidentDef incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail("WarOrphans_OrphanArrival");
            if (incidentDef != null)
            {
                IncidentParms parms = StorytellerUtility.DefaultParmsNow(incidentDef.category, map);
                incidentDef.Worker.TryExecute(parms);
            }

            // Schedule next
            nextOrphanTick = Find.TickManager.TicksGame + Rand.Range(MinIntervalDays, MaxIntervalDays) * 60000f;
            firstEventFired = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextOrphanTick, "nextOrphanTick", float.MaxValue);
            Scribe_Values.Look(ref firstEventFired, "firstEventFired", false);
        }
    }
}
