using System.Collections.Generic;
using System.Linq;
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

            // 40% chance of gaining a positive trait from overcoming trauma
            if (Rand.Chance(0.4f))
                TryGainRecoveryTrait();

            Find.LetterStack.ReceiveLetter(
                Pawn.Name.ToStringShort + " has recovered",
                Pawn.Name.ToStringShort + " has overcome the war trauma. The nightmares have stopped. "
                    + "This place is home now.",
                LetterDefOf.PositiveEvent, Pawn);
        }

        private void TryGainRecoveryTrait()
        {
            if (Pawn.story?.traits == null)
                return;

            // Gather all positive trait degrees from the database
            List<Trait> candidates = new List<Trait>();
            foreach (TraitDef traitDef in DefDatabase<TraitDef>.AllDefsListForReading)
            {
                if (Pawn.story.traits.HasTrait(traitDef))
                    continue;

                foreach (TraitDegreeData degreeData in traitDef.degreeDatas)
                {
                    // Only positive degrees (RimWorld convention: positive degree = good trait)
                    if (degreeData.degree < 0)
                        continue;
                    if (degreeData.commonality <= 0f)
                        continue;
                    // Skip traits that cause mental breaks
                    if (degreeData.randomMentalState != null || degreeData.forcedMentalState != null)
                        continue;

                    Trait candidate = new Trait(traitDef, degreeData.degree);
                    if (Pawn.story.traits.allTraits.Any(t => t.def.ConflictsWith(candidate)))
                        continue;

                    candidates.Add(candidate);
                }
            }

            if (candidates.Count == 0)
                return;

            Trait newTrait = candidates.RandomElement();
            Pawn.story.traits.GainTrait(newTrait);

            Find.LetterStack.ReceiveLetter(
                Pawn.Name.ToStringShort + " has grown",
                Pawn.Name.ToStringShort + " has emerged from the trauma with a new strength: "
                    + newTrait.LabelCap + ". What they went through shaped who they've become.",
                LetterDefOf.PositiveEvent, Pawn);
        }

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref lastStageIndex, "lastStageIndex", -1);
        }
    }
}
