# War Orphans

A RimWorld mod that adds a quest where war-torn settlements beg you to take in their orphaned children.

## Features

- **Quest:** Factions devastated by war send word that their orphaned children will die without help
- **Up to 5 children** of any age, from babies to teens
- **Faction xenotypes:** Children match the sending faction's xenotype (Waster, Neanderthal, Yttakin, etc.)
- **Sibling families:** Orphans have a chance of being siblings, sharing the same deceased parents
- **Dead parents:** All children have a mother and father listed as deceased in the social tab
- **Baby carrying:** Older children carry babies and toddlers who can't walk on their own

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
