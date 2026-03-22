# War Orphans

A RimWorld mod that adds events where war-torn settlements beg you to take in their orphaned children. Three quest variants with deep narrative, trauma mechanics, and recovery arcs.

## Quests

### War Orphans
Non-hostile factions send word from their real settlements that 1-7 orphaned children will die without help. Accept or reject via letter.

### Persecuted Orphans
A settlement has turned against a xenotype. All children share the same non-Baseliner race — they're being hunted for what they are. Rarer, with different narrative.

### Sole Survivor
Very rare. A single child, the only one left alive from a destroyed settlement. Arrives in the worst condition — max trauma, severe malnutrition, multiple injuries, 40% chance of a missing body part. Comes with survival skills (melee, plants, animals, medicine). 20% chance of a retribution raid 3-7 days after accepting.

## Features

- **Up to 7 children** ages 3-13, generated as proper children with child-appropriate skills
- **Faction xenotypes:** Children match the sending faction's xenotype distribution, accounting for Baseliner chance
- **Sibling families:** 60% chance of being siblings, sharing the same deceased parents
- **Dead parents:** Mother and father listed as deceased in the social tab
- **War trauma hediff** that scales with age and fades over ~100 days at max severity
  - Four stages: extreme → severe → troubled → fading
  - Affects mood, mental break threshold, learning, rest quality, social fights, psychic sensitivity, consciousness, movement, work speed
- **Recovery milestones:** Positive thoughts and letter notifications as trauma heals
  - "The nightmares are less frequent" (+3 mood)
  - "Starting to feel safe" (+6 mood)
  - "This place is home" (+10 mood) when fully recovered
- **Grief:** "Parents killed in war" thought (-12 mood, 30 days)
- **Malnutrition:** All children arrive underfed (15-60% severity)
- **Injuries:** Cuts scaling with age, small chance of missing body part (healed)
- **Tattered clothes:** 10-40% durability, 25% chance tainted
- **Social bonds:**
  - "Escaped war together" — permanent +25 opinion between all orphans
  - "Rescued me from war" — orphans +20 toward colonists (60 days)
  - "Rescued war orphan" — colonists +15 toward orphans (60 days)
- **Faction goodwill:** +15 with the sending faction on accept
- **No androids:** Non-human pawns are filtered out
- **Ideology:** Accepting/rejecting triggers charity fulfilled/refused events

## Storyteller Integration

- **Charity events** — appear as accept/reject letters, not quest tab entries
- **Population-aware** — more likely when colony is small
- **Not threat-gated** — can fire regardless of colony wealth
- **Challenge rating** scales with number of children (1-4 stars)

| Quest | Weight | Earliest Day | Cooldown |
|-------|--------|-------------|----------|
| War Orphans | 1.0 | 10 | 15 days |
| Persecuted | 0.5 | 15 | 30 days |
| Sole Survivor | 0.3 | 20 | 45 days |

## Requirements

- RimWorld 1.5 / 1.6
- Biotech DLC

## Installation

### From source

1. Clone this repository into your RimWorld mods folder, or create a junction:
   ```
   powershell -Command "New-Item -ItemType Junction -Path 'C:\...\RimWorld\Mods\WarOrphans' -Target 'C:\path\to\WarOrphans'"
   ```
2. Install the [.NET SDK](https://dotnet.microsoft.com/download)
3. Build the mod:
   ```
   dotnet build Source/WarOrphans/WarOrphans.csproj
   ```
4. Enable **War Orphans** in the RimWorld mod manager

### Build configuration

The `RimWorldPath` property in `Source/WarOrphans/WarOrphans.csproj` defaults to:
```
C:\Program Files (x86)\Steam\steamapps\common\RimWorld
```
Update this if your RimWorld is installed elsewhere.

## License

MIT
