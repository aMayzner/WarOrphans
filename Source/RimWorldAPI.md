# RimWorld Modding API Reference

Decompiled from `Assembly-CSharp.dll` using `ilspycmd`. Use it to check APIs:
```
"C:\Users\Anna\.dotnet\tools\ilspycmd.exe" -t RimWorld.ClassName "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed\Assembly-CSharp.dll"
```

List all classes: add `-l c` flag instead of `-t`.

## XenotypeSet

`xenotypeChances` is **private**. Access via indexer and `Count`:

```csharp
public class XenotypeSet
{
    private List<XenotypeChance> xenotypeChances; // PRIVATE - can't access directly
    public XenotypeChance this[int index] => xenotypeChances[index];
    public int Count => xenotypeChances.Count;
    public float BaselinerChance { get; }
    public bool Contains(XenotypeDef xenotype);
}
```

To pick a random xenotype from a faction:
```csharp
XenotypeSet set = faction.def.xenotypeSet;
if (set != null && set.Count > 0)
{
    // Iterate with indexer: set[0], set[1], etc.
    // Each element is XenotypeChance with .xenotype and .chance
}
```

## XenotypeChance

```csharp
public struct XenotypeChance
{
    public XenotypeDef xenotype;
    public float chance;
}
```

## QuestPart_SetFaction

Uses `things` (List<Thing>), NOT `pawn`:

```csharp
public class QuestPart_SetFaction : QuestPart
{
    public string inSignal;
    public Faction faction;
    public List<Thing> things = new List<Thing>(); // NOT 'pawn' - it's 'things'
}
```

## QuestPart_PawnsArrive

Has `joinPlayer` flag — if true, auto-sets faction to player. No need for separate QuestPart_SetFaction:

```csharp
public class QuestPart_PawnsArrive : QuestPart
{
    public string inSignal;
    public MapParent mapParent;
    public List<Pawn> pawns = new List<Pawn>();
    public PawnsArrivalModeDef arrivalMode;
    public IntVec3 spawnNear = IntVec3.Invalid;
    public bool joinPlayer;                    // SET THIS to auto-join player faction
    public bool addPawnsToLookTargets = true;
    public string customLetterText;
    public string customLetterLabel;
    public LetterDef customLetterDef;
    public bool sendStandardLetter = true;     // sends its own letter if true
}
```

## Quest Extension Methods (QuestGen_Misc)

Prefer these over manually creating QuestParts:

```csharp
// Spawn pawns - use joinPlayer:true to auto-join colony
quest.PawnsArrive(pawns, inSignal, mapParent, arrivalMode, joinPlayer:true, ...);

// Send a letter
quest.Letter(LetterDefOf.PositiveEvent, inSignal, text:"...", label:"...");

// Full signature:
quest.Letter(LetterDef letterDef, string inSignal = null, string chosenPawnSignal = null,
    Faction relatedFaction = null, MapParent useColonistsOnMap = null,
    bool useColonistsFromCaravanArg = false,
    QuestPart.SignalListenMode signalListenMode = ...,
    IEnumerable<object> lookTargets = null,
    bool filterDeadPawnsFromLookTargets = false,
    string text = null, RulePack textRules = null,
    string label = null, RulePack labelRules = null,
    string getColonistsFromSignal = null);
```

## Quest Extension Methods (QuestGen_Pawns)

```csharp
quest.ReservePawns(IEnumerable<Pawn> pawns);
quest.GeneratePawn(PawnKindDef kindDef, Faction faction, ...);
quest.GeneratePawn(PawnGenerationRequest request, bool ensureNonNumericName = false);
```

## Quest Extension Methods (QuestGen_End)

```csharp
quest.End(QuestEndOutcome outcome, int goodwillChangeAmount = 0,
    Faction goodwillChangeFactionOf = null, string inSignal = null, ...);
```

## QuestGenUtility

```csharp
QuestGenUtility.HardcodedSignalWithQuestID("quest.accepted");
```

## Settlement (RimWorld.Planet)

```csharp
public class Settlement : MapParent, ITrader, ITraderRestockingInfoProvider, INameableWorldObject
{
    public string Name { get; set; }
    public override string Label => nameInt ?? base.Label;  // display name
    public override bool HasName => !nameInt.NullOrEmpty();
}
```

## FactionDef Fields

Key fields on `FactionDef`:
- `basicMemberKind` — PawnKindDef for basic faction members
- `xenotypeSet` — XenotypeSet (xenotype distribution for the faction)
- `humanlikeFaction` — bool

## Faction

```csharp
Faction.OfPlayer               // player faction
faction.Name                    // faction display name
faction.def                     // FactionDef
faction.Hidden                  // is hidden faction
Find.FactionManager.AllFactions // all factions in game
```

## PawnGenerationRequest

Key named params:
- `kind:` PawnKindDef
- `faction:` Faction (null for no faction)
- `context:` PawnGenerationContext.NonPlayer
- `forceGenerateNewPawn:` bool
- `fixedBiologicalAge:` float
- `fixedChronologicalAge:` float
- `fixedGender:` Gender.Female / Gender.Male
- `canGeneratePawnRelations:` bool
- `colonistRelationChanceFactor:` float
- `forcedXenotype:` XenotypeDef

## PawnRelationDefOf

- `PawnRelationDefOf.Parent`
- `PawnRelationDefOf.Sibling`
- `PawnRelationDefOf.Child`

Add relations: `pawn.relations.AddDirectRelation(PawnRelationDefOf.Parent, otherPawn);`

## Common DefOf Classes

- `PawnKindDefOf.Villager`
- `PawnsArrivalModeDefOf.EdgeWalkIn`
- `LetterDefOf.PositiveEvent`
- `XenotypeDefOf.Baseliner`

## World Objects

```csharp
// Get all settlements on the world map
Find.WorldObjects.Settlements  // List<Settlement>

// Filter by faction
Find.WorldObjects.Settlements.Where(s => s.Faction == someFaction)
```
