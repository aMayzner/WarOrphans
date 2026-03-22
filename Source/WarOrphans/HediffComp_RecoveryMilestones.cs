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

        // Positive traits that can emerge from overcoming trauma
        private static readonly TraitDef[] recoveryTraits = new[]
        {
            TraitDefOf.Kind,
            TraitDefOf.Industriousness,
            TraitDefOf.GreatMemory,
        };

        // Trait degree for Industriousness (1 = Industrious, 2 = Hard Worker)
        private static readonly Dictionary<TraitDef, int> traitDegrees = new Dictionary<TraitDef, int>
        {
            { TraitDefOf.Industriousness, 1 }
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

            // Shuffle and try each trait until one works
            List<TraitDef> candidates = recoveryTraits.ToList();
            candidates.Shuffle();

            foreach (TraitDef traitDef in candidates)
            {
                // Skip if pawn already has this trait or a conflicting one
                int degree = traitDegrees.TryGetValue(traitDef, out int d) ? d : 0;
                if (Pawn.story.traits.HasTrait(traitDef))
                    continue;

                Trait newTrait = new Trait(traitDef, degree);
                if (Pawn.story.traits.allTraits.Any(t => t.def.ConflictsWith(newTrait)))
                    continue;

                Pawn.story.traits.GainTrait(newTrait);

                Find.LetterStack.ReceiveLetter(
                    Pawn.Name.ToStringShort + " has grown",
                    Pawn.Name.ToStringShort + " has emerged from the trauma with a new strength: "
                        + newTrait.LabelCap + ". What they went through shaped who they've become.",
                    LetterDefOf.PositiveEvent, Pawn);
                break;
            }
        }

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref lastStageIndex, "lastStageIndex", -1);
        }
    }
}
