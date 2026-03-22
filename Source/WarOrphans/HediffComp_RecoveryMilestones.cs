using RimWorld;
using Verse;

namespace WarOrphans
{
    public class HediffCompProperties_RecoveryMilestones : HediffCompProperties
    {
        public HediffCompProperties_RecoveryMilestones()
        {
            compClass = typeof(HediffComp_RecoveryMilestones);
        }
    }

    public class HediffComp_RecoveryMilestones : HediffComp
    {
        private int lastStageIndex = -1;

        private static readonly string[] milestoneThoughts = new[]
        {
            null,                              // stage 0 (fading) — final recovery triggers removal thought
            "WarOrphans_Recovery_FeelingSafer", // entered stage 1 (troubled) from worse
            "WarOrphans_Recovery_Improving",    // entered stage 2 (severe) from worse
            null                               // stage 3 (extreme) — no positive thought here
        };

        public override void CompPostTick(ref float severityAdjustment)
        {
            int currentStage = parent.CurStageIndex;

            // Only fire when moving to a LOWER stage (getting better)
            if (lastStageIndex > currentStage && currentStage >= 0 && currentStage < milestoneThoughts.Length)
            {
                string thoughtName = milestoneThoughts[currentStage];
                if (thoughtName != null)
                {
                    ThoughtDef thought = DefDatabase<ThoughtDef>.GetNamedSilentFail(thoughtName);
                    if (thought != null)
                        Pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(thought);
                }

                // When reaching fading stage (0), send a letter
                if (currentStage == 0)
                {
                    Find.LetterStack.ReceiveLetter(
                        Pawn.Name.ToStringShort + " is healing",
                        Pawn.Name.ToStringShort + " is starting to feel safe again. The war trauma is fading. "
                            + "With time and care, " + Pawn.gender.GetPronoun() + " will recover.",
                        LetterDefOf.PositiveEvent, Pawn);
                }
            }

            lastStageIndex = currentStage;
        }

        // When trauma fully heals (hediff removed)
        public override void CompPostPostRemoved()
        {
            ThoughtDef thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("WarOrphans_Recovery_Healed");
            if (thought != null)
                Pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(thought);

            Find.LetterStack.ReceiveLetter(
                Pawn.Name.ToStringShort + " has recovered",
                Pawn.Name.ToStringShort + " has overcome the war trauma. The nightmares have stopped. "
                    + "This place is home now.",
                LetterDefOf.PositiveEvent, Pawn);
        }

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref lastStageIndex, "lastStageIndex", -1);
        }
    }
}
