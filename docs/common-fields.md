# 📐 Common Fields And Patterns

This page explains fields and XML patterns that appear across many entity types. It is meant as a quick orientation page for new modders.

## Entity Identity

Common attributes:

| Attribute | Meaning |
| --- | --- |
| `Name` | Human-readable internal entity name |
| `Guid` | Stable unique identifier used by references |
| `IsAbstract` | Marks template/base entities that may not be used directly |
| `InheritanceMode` | Indicates inheritance behavior |
| `InheritedGuid` | Points to a parent/template entity |

Do not change GUIDs during normal tuning. GUIDs are the reference backbone of the database.

## `Values`

Most gameplay data lives under:

```xml
<Values>
  ...
</Values>
```

Each child of `Values` is effectively a component. An entity can have many components, which is why buildings, units, resources, and objectives should be understood as compositions.

## `Children`

Many entities contain:

```xml
<Children />
```

or nested child entity references. Children can define variants, grouped content, or related entities depending on the file. Treat child relationships carefully because they may participate in inheritance or UI grouping.

## GUID References

GUID references usually appear as element text:

```xml
<Resource>...</Resource>
<Unit>...</Unit>
<Recipe>...</Recipe>
<Category>...</Category>
```

The field name tells you what kind of entity is expected, but the value is still just a GUID. Resolve it before editing.

Useful command:

```powershell
rg --no-ignore "GUID-HERE" .\game-gdb
```

## Null GUIDs

This GUID appears frequently:

```text
00000000-0000-0000-0000-000000000000
```

It usually means "none", "empty", "default", or "not set". The validation script treats it as intentional, not as a broken reference.

Do not replace null GUIDs randomly. Sometimes an empty reference is required.

## Lists

Lists often use this shape:

```xml
<Items>
  <Item>
    <Content>
      ...
    </Content>
  </Item>
</Items>
```

The actual parent name varies, for example:

- `Costs`
- `Recipes`
- `ResourceCosts`
- `ProductionSteps`
- `Tags`
- `Notifications`

When adding or removing list entries, preserve the local structure exactly.

## Amounts

Amounts appear in many contexts:

- construction costs
- recruitment costs
- objective requirements
- resource quantities
- worker counts

An `<Amount>` field does not always mean the same gameplay unit. Always inspect the parent component.

Examples:

| Context | Likely Meaning |
| --- | --- |
| `AspectBuildup/Costs/.../Amount` | Construction resource count |
| `RecruitmentCost/ResourceCosts/.../Amount` | Unit recruitment resource count |
| `Employment/Amount` | Number of workers |
| Objective component amount | Required objective quantity |

## Time Fields

Time values often use `hh:mm:ss` style strings:

```text
00:00:40
00:01:06.6000000
```

For production, timing may be split between building and recipe data:

- recipe steps define input/output/work sequence
- `AspectProduction/Efficiency/TimeOfOptimalWorkStep` provides a building-level timing hint
- recipe `LoopAnimationAmount` is not by itself guaranteed throughput

See [Production Recipes](production.md) for details.

## Asset Paths

Some fields point to assets:

- icons
- meshes
- textures
- audio events
- masks
- terrain textures

Changing these is only safe if the target asset exists and is compatible with the expected type.

## Safe Editing Pattern

When you do not know a field yet:

1. Find three existing examples.
2. Compare the surrounding component.
3. Search all references to any GUID you want to edit.
4. Change one field only.
5. Validate XML and GUIDs.
6. Test in a new save or test map.

This pattern is slower, but it keeps the database understandable.
