using System.Linq;
using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace WarOrphans
{
    public class ScenPart_RecurringOrphans : ScenPart
    {
        private float nextOrphanTick;
        private bool firstEventFired;

        // First event within 10 days, then every 30-90 days
        private const float FirstEventMaxDays = 10f;
        private const float MinIntervalDays = 30f;
        private const float MaxIntervalDays = 90f;

        public override void PostGameStart()
        {
            // Schedule first event within the first 10 days
            nextOrphanTick = Find.TickManager.TicksGame + Rand.Range(3f, FirstEventMaxDays) * 60000f;
            firstEventFired = false;
        }

        public override void Tick()
        {
            if (Find.TickManager.TicksGame < nextOrphanTick)
                return;

            // Pick a random orphan quest
            string[] questDefs = { "WarOrphans_Rescue", "WarOrphans_Persecuted", "WarOrphans_SoleSurvivor" };
            float[] weights = { 0.5f, 0.3f, 0.2f };

            // Weight selection
            float totalWeight = weights.Sum();
            float roll = Rand.Range(0f, totalWeight);
            string chosenDef = questDefs[0];
            float cumulative = 0f;
            for (int i = 0; i < questDefs.Length; i++)
            {
                cumulative += weights[i];
                if (roll <= cumulative)
                {
                    chosenDef = questDefs[i];
                    break;
                }
            }

            QuestScriptDef questScript = DefDatabase<QuestScriptDef>.GetNamedSilentFail(chosenDef);
            if (questScript != null)
            {
                Slate slate = new Slate();
                if (questScript.CanRun(slate, Find.AnyPlayerHomeMap))
                {
                    QuestUtility.GenerateQuestAndMakeAvailable(questScript, slate);
                }
            }

            // Schedule next event
            nextOrphanTick = Find.TickManager.TicksGame + Rand.Range(MinIntervalDays, MaxIntervalDays) * 60000f;
            firstEventFired = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextOrphanTick, "nextOrphanTick", 0f);
            Scribe_Values.Look(ref firstEventFired, "firstEventFired", false);
        }
    }
}
