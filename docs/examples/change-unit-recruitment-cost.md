# Change A Unit Recruitment Cost

This example changes the amount of an existing recruitment cost for a unit.

It is a good first unit edit because it avoids tags, combat behavior, animations, and worker logic.

## Goal

Change one existing `RecruitmentCost` amount.

Do not change unit GUIDs, tags, or components.

## Step 1: Pick A Simple Unit

Open:

```text
generated/catalog/filters/production/units-with-recruitment-cost.md
```

Good first candidates are simple workers such as:

- `Builder`
- `Digger`
- `Miner`
- `Fisher`
- `Explorer`

Avoid combat units for the first experiment.

## Step 2: Find The Unit

Search by unit name or GUID. For example:

```powershell
rg --no-ignore "Builder|efb5e3be-f28a-4d86-84f4-9cc6ec428661" .\game-gdb\core\gdb\units.gd.xml
```

Find:

```text
RecruitmentCost
ResourceCosts
Resource
Amount
NeedsManualRecruitment
```

## Step 3: Change One Amount

Example style of change:

```xml
<Amount>1</Amount>
```

to:

```xml
<Amount>2</Amount>
```

Keep the resource GUID unchanged.

## Step 4: Validate And Regenerate

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate_database.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Check:

```text
generated/catalog/units.md
generated/catalog/filters/production/units-with-recruitment-cost.md
```

## Step 5: Test Recruitment

In a new test save, check:

- the unit can still be recruited
- the recruitment UI shows the changed cost
- the unit appears after recruitment
- the unit can still take jobs
- no required tutorial or objective became blocked

## What Not To Change Yet

Avoid:

- `TaggedUnit`
- combat fields
- animation names
- `SourceRecruitableUnit`
- `NeedsManualRecruitment`
- component structure
- unit GUIDs

## Why This Example Is Useful

Recruitment cost changes teach three important patterns:

- how resources are referenced by GUID
- how unit data is cataloged
- how a small XML amount change flows into gameplay

Once this is comfortable, you can compare worker costs, combat unit costs, and DLC unit costs.
