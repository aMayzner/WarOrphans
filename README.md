# War Orphans

A RimWorld mod that adds a quest where war-torn settlements beg you to take in their orphaned children.

## Features

- **Quest:** Non-hostile factions send word from their real settlements that orphaned children will die without help
- **Up to 7 children** of any age, from babies to teens
- **Mixed xenotypes:** Each child rolls their xenotype independently from the faction's distribution — you might get a mix of Baseliners, Neanderthals, etc.
- **Sibling families:** Orphans have a 60% chance of being siblings, sharing the same deceased parents
- **Dead parents:** All children have a mother and father listed as deceased in the social tab
- **Baby carrying:** Older children carry babies and toddlers who can't walk on their own
- **War trauma:** Children age 3+ get a trauma hediff that scales with age — older children are more affected. It fades naturally over time (~23 days for a 3-year-old, ~100 days for a 13-year-old)
- **Grief:** Children old enough to understand get a "parents killed in war" thought (-12 mood, 30 days)

## Storyteller Integration

- **Charity quest** — no material reward, just a moral choice
- **Population-aware** — more likely to fire when your colony is small, less likely at population cap
- **Not threat-gated** — can fire regardless of colony wealth
- **Earliest day 10** — won't appear in the first 10 days
- **15-day cooldown** between appearances
- **Challenge rating** scales with number of children (1-4 stars)

## Requirements

- RimWorld 1.5
- Biotech DLC

## Installation

### From source

1. Clone this repository into your RimWorld mods folder, or symlink it:
   ```
   mklink /D "C:\...\RimWorld\Mods\WarOrphans" "C:\path\to\WarOrphans"
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
