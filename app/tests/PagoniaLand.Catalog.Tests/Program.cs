using System.Xml.Linq;
using PagoniaLand.Catalog;
using PagoniaLand.Catalog.Domain;

// Hand-rolled test harness, matching the pattern used by the paker/patcher/manager
// test projects: a list of (name, predicate) pairs, run sequentially, non-zero exit on
// any failure. AOT-friendly and dependency-light; preflight runs it like the others.

// Fixtures use synthetic GUIDs so the tests are version-independent (the live game-gdb
// counts move every game update — those are validated by the PowerShell pipeline, not here).
const string GuidA = "11111111-1111-1111-1111-111111111111";
const string GuidB = "22222222-2222-2222-2222-222222222222";
const string GuidUnknown = "33333333-3333-3333-3333-333333333333";
const string GuidD = "44444444-4444-4444-4444-444444444444";
const string GuidE = "55555555-5555-5555-5555-555555555555";
const string GuidF = "66666666-6666-6666-6666-666666666666";
const string NullGuid = "00000000-0000-0000-0000-000000000000";

var analyzer = new GameDatabaseAnalyzer();
var reader = new GameDatabaseReader();

var tests = new (string Name, Func<bool> Run)[]
{
    ("product name is stable", () => CatalogCoreInfo.ProductName == "Pagonia Land Catalog"),
    ("version is present", () => !string.IsNullOrWhiteSpace(CatalogCoreInfo.Version)),
    ("analyzer counts entities and unique guids", AnalyzerCountsEntities),
    ("analyzer classifies resolved / null / other-unresolved references", AnalyzerClassifiesReferences),
    ("analyzer counts a single-GUID wrapper AND its leaf as two references (fidelity)", AnalyzerCountsNestedWrappers),
    ("analyzer does not count a multi-child wrapper as a reference", AnalyzerIgnoresMixedWrappers),
    ("analyzer groups entities per package, sorted", AnalyzerGroupsPackages),
    ("analyzer treats a duplicate GUID as one unique definition", AnalyzerDeduplicatesGuids),
    ("analyzer reads entity metadata (abstract, parent, children, value types)", AnalyzerReadsMetadata),
    ("game database resolves a GUID to its entity name", GameDatabaseResolvesNames),
    ("resource builder projects entities with a ResourceDescription component", ResourceBuilderProjectsResources),
    ("resource builder ignores non-resource entities", ResourceBuilderIgnoresNonResources),
    ("install locator detects live install / pak dir / extracted / unrecognised", LocatorDetectsLayouts),
    ("install reader reads an extracted layout into the model", InstallReaderReadsExtractedLayout),
    ("RTEX raw R8G8B8A8 parses + decodes to the same pixels", RtexRawRoundTrips),
    ("RTEX takes the base mip as the last bytes (BC7 block size)", RtexBaseMipIsLastBytes),
    ("RTEX rejects a non-RTEX blob", RtexRejectsNonRtex),
    ("RTEX rejects crafted overflow dimensions (tiny blob, huge width)", RtexRejectsOverflowDimensions),
    ("asset reader returns null for a source with no paks", AssetReaderHandlesNoPaks),
    ("building builder projects entities with a Building component", BuildingBuilderProjectsBuildings),
    ("building builder ignores non-building entities", BuildingBuilderIgnoresNonBuildings),
    ("recipe builder projects inputs / outputs / work steps", RecipeBuilderProjectsRecipes),
    ("recipe builder drops a null-GUID output (matches the catalog's '(none)')", RecipeBuilderDropsNullGuidOutput),
    ("recipe builder sums repeated input steps (the real no-Amount schema)", RecipeBuilderSumsRepeatedSteps),
    ("unit builder projects icon / recruitment / source / tags", UnitBuilderProjectsUnits),
    ("objective builder projects category / hidden / sort / types / title", ObjectiveBuilderProjectsObjectives),
    ("catalog snapshot survives a JSON round-trip (references intact)", CatalogSnapshotRoundTrips),
    ("catalog cache saves + reloads a snapshot and its icons", CatalogCacheRoundTrips),
    ("search index builder produces entity + domain items", SearchIndexBuilderProducesItems),
    ("parses library paths from a Steam libraryfolders.vdf", ParsesSteamLibraryPaths),
    ("game version is null for a non-live layout", GameVersionNullWithoutExe),
};

int failed = 0;
foreach (var (name, run) in tests)
{
    bool ok;
    try
    {
        ok = run();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAIL {name}: {ex.Message}");
        failed++;
        continue;
    }

    Console.WriteLine(ok ? $"PASS {name}" : $"FAIL {name}");
    if (!ok)
    {
        failed++;
    }
}

Console.WriteLine(failed == 0
    ? $"All {tests.Length} tests passed."
    : $"{failed} of {tests.Length} tests failed.");

return failed == 0 ? 0 : 1;

// ---- test bodies ----

bool AnalyzerCountsEntities()
{
    var docs = new[]
    {
        ("core/gdb/a.gd.xml", Xml($@"
          <EntityGroup Name='Root'><Entities>
            <Entity Name='A' Guid='{GuidA}'><Values><Building /></Values></Entity>
            <Entity Name='B' Guid='{GuidB}'><Values /></Entity>
          </Entities></EntityGroup>")),
    };

    var summary = analyzer.AnalyzeDocuments(docs).Summary;
    return summary.XmlFiles == 1 && summary.TotalEntities == 2 && summary.UniqueGuids == 2;
}

bool AnalyzerClassifiesReferences()
{
    var docs = new[]
    {
        ("core/gdb/a.gd.xml", Xml($@"
          <EntityGroup Name='Root'><Entities>
            <Entity Name='A' Guid='{GuidA}'><Values /></Entity>
            <Entity Name='B' Guid='{GuidB}'><Values><Aspect>
              <Ref>{GuidA}</Ref><Ref>{NullGuid}</Ref><Ref>{GuidUnknown}</Ref>
            </Aspect></Values></Entity>
          </Entities></EntityGroup>")),
    };

    var s = analyzer.AnalyzeDocuments(docs).Summary;
    return s.GuidLikeReferences == 3
        && s.ResolvedReferences == 1
        && s.NullGuidReferences == 1
        && s.OtherUnresolvedReferences == 1;
}

bool AnalyzerCountsNestedWrappers()
{
    // <Content> wraps a single <Resource> GUID leaf; an <Identifier> sibling keeps the
    // enclosing <Recipe>'s concatenated text from being a bare GUID. So exactly two
    // elements (Content + Resource) read as the same GUID — the script's behaviour.
    var docs = new[]
    {
        ("core/gdb/a.gd.xml", Xml($@"
          <EntityGroup Name='Root'><Entities>
            <Entity Name='Prod' Guid='{GuidA}'><Values /></Entity>
            <Entity Name='Recipe' Guid='{GuidB}'><Values><Recipe>
              <Identifier>weapon</Identifier>
              <Content><Resource>{GuidA}</Resource></Content>
            </Recipe></Values></Entity>
          </Entities></EntityGroup>")),
    };

    var result = analyzer.AnalyzeDocuments(docs);
    var elements = result.References.Select(r => r.SourceElement).OrderBy(e => e, StringComparer.Ordinal).ToArray();
    return result.Summary.GuidLikeReferences == 2
        && result.References.All(r => r.Resolved && r.Guid == GuidA)
        && elements.SequenceEqual(new[] { "Content", "Resource" });
}

bool AnalyzerIgnoresMixedWrappers()
{
    // <Item> holds the GUID leaf plus other text, so its concatenated value is not a bare
    // GUID and must not count — only the <Resource> leaf does.
    var docs = new[]
    {
        ("core/gdb/a.gd.xml", Xml($@"
          <EntityGroup Name='Root'><Entities>
            <Entity Name='A' Guid='{GuidA}'><Values /></Entity>
            <Entity Name='B' Guid='{GuidB}'><Values><Item>
              <Animation>cycle_001</Animation><Resource>{GuidA}</Resource>
            </Item></Values></Entity>
          </Entities></EntityGroup>")),
    };

    var result = analyzer.AnalyzeDocuments(docs);
    return result.Summary.GuidLikeReferences == 1
        && result.References.Single().SourceElement == "Resource";
}

bool AnalyzerGroupsPackages()
{
    var docs = new[]
    {
        ("dlc1/gdb/b.gd.xml", Xml($"<EntityGroup Name='R'><Entities><Entity Name='B' Guid='{GuidB}'><Values /></Entity></Entities></EntityGroup>")),
        ("core/gdb/a.gd.xml", Xml($"<EntityGroup Name='R'><Entities><Entity Name='A' Guid='{GuidA}'><Values /></Entity></Entities></EntityGroup>")),
    };

    var packages = analyzer.AnalyzeDocuments(docs).Summary.Packages;
    return packages.Count == 2
        && packages[0] is { Package: "core", Entities: 1 }
        && packages[1] is { Package: "dlc1", Entities: 1 };
}

bool AnalyzerDeduplicatesGuids()
{
    var docs = new[]
    {
        ("core/gdb/a.gd.xml", Xml($@"
          <EntityGroup Name='R'><Entities>
            <Entity Name='First' Guid='{GuidA}'><Values /></Entity>
            <Entity Name='Dup' Guid='{GuidA}'><Values /></Entity>
          </Entities></EntityGroup>")),
    };

    var summary = analyzer.AnalyzeDocuments(docs).Summary;
    return summary.TotalEntities == 2 && summary.UniqueGuids == 1;
}

bool AnalyzerReadsMetadata()
{
    var docs = new[]
    {
        ("core/gdb/a.gd.xml", Xml($@"
          <EntityGroup Name='Root'><Entities>
            <Entity Name='Parent' Guid='{GuidA}'>
              <Values><Building /><Buildable /></Values>
              <Children>
                <Entity Name='Child' Guid='{GuidB}' IsAbstract='true'><Values /></Entity>
              </Children>
            </Entity>
          </Entities></EntityGroup>")),
    };

    var result = analyzer.AnalyzeDocuments(docs);
    var parent = result.Entities.Single(e => e.Guid == GuidA);
    var child = result.Entities.Single(e => e.Guid == GuidB);

    return parent.ChildEntityCount == 1
        && parent.ValueTypes.SequenceEqual(new[] { "Building", "Buildable" })
        && parent.GroupPath == "Root"
        && !parent.IsAbstract
        && child.IsAbstract
        && child.ParentEntityGuid == GuidA
        && child.ParentEntityName == "Parent";
}

bool GameDatabaseResolvesNames()
{
    var docs = new[]
    {
        ("core/gdb/a.gd.xml", Xml($"<EntityGroup Name='R'><Entities><Entity Name='Wood' Guid='{GuidA}'><Values /></Entity></Entities></EntityGroup>")),
    };

    var db = reader.ReadDocuments(docs);
    return db.ResolveName(GuidA) == "Wood"
        && db.ResolveName(GuidUnknown) == string.Empty
        && db.ResolveName(null) == string.Empty;
}

bool ResourceBuilderProjectsResources()
{
    var docs = new[]
    {
        ("core/gdb/resources.gd.xml", Xml($@"
          <EntityGroup Name='Root'><Entities>
            <Entity Name='Wood' Guid='{GuidA}'><Values>
              <ResourceDescription>
                <ResourceCategory>{GuidB}</ResourceCategory>
                <Name>res.wood.name</Name>
                <Icon>ui/icons/wood.image</Icon>
                <CarryType>Stackable</CarryType>
                <Tags><Item><Content><Tag>{GuidUnknown}</Tag></Content></Item></Tags>
              </ResourceDescription>
            </Values></Entity>
            <Entity Name='RawMaterials' Guid='{GuidB}'><Values><ResourceCategory /></Values></Entity>
            <Entity Name='Flammable' Guid='{GuidUnknown}'><Values><Tag /></Values></Entity>
            <Entity Name='Sawmill' Guid='{GuidD}'><Values><Building /></Values></Entity>
          </Entities></EntityGroup>")),
    };

    var resources = ResourceCatalogBuilder.Build(reader.ReadDocuments(docs));
    if (resources.Count != 1)
    {
        return false;
    }

    var wood = resources[0];
    return wood.Name == "Wood"
        && wood.Category == "RawMaterials"      // resolved from the ResourceCategory GUID
        && wood.Icon == "ui/icons/wood.image"
        && wood.CarryType == "Stackable"
        && wood.NameKey == "res.wood.name"
        && wood.Tags.SequenceEqual(new[] { "Flammable" })   // resolved from the tag GUID
        && wood.Components.Contains("ResourceDescription");
}

bool ResourceBuilderIgnoresNonResources()
{
    var docs = new[]
    {
        ("core/gdb/buildings.gd.xml", Xml($"<EntityGroup Name='R'><Entities><Entity Name='Sawmill' Guid='{GuidA}'><Values><Building /><Buildable /></Values></Entity></Entities></EntityGroup>")),
    };

    return ResourceCatalogBuilder.Build(reader.ReadDocuments(docs)).Count == 0;
}

bool LocatorDetectsLayouts()
{
    var tmp = Directory.CreateTempSubdirectory("plcat_").FullName;
    try
    {
        var extracted = Path.Combine(tmp, "extracted");
        Directory.CreateDirectory(Path.Combine(extracted, "core", "gdb"));
        File.WriteAllText(Path.Combine(extracted, "core", "gdb", "x.gd.xml"), "<EntityGroup/>");

        var pakDir = Path.Combine(tmp, "paks");
        Directory.CreateDirectory(pakDir);
        File.WriteAllText(Path.Combine(pakDir, "core.pak"), "stub");

        var install = Path.Combine(tmp, "install");
        Directory.CreateDirectory(Path.Combine(install, "pak"));
        File.WriteAllText(Path.Combine(install, "pak", "core.pak"), "stub");

        var empty = Path.Combine(tmp, "empty");
        Directory.CreateDirectory(empty);

        return GameInstallLocator.Detect(extracted) == GameInstallKind.ExtractedLayout
            && GameInstallLocator.Detect(pakDir) == GameInstallKind.PakDirectory
            && GameInstallLocator.Detect(install) == GameInstallKind.LiveInstall
            && GameInstallLocator.Detect(empty) == GameInstallKind.Unrecognised
            && GameInstallLocator.Detect(Path.Combine(tmp, "missing")) == GameInstallKind.Unrecognised;
    }
    finally
    {
        Directory.Delete(tmp, recursive: true);
    }
}

bool InstallReaderReadsExtractedLayout()
{
    var tmp = Directory.CreateTempSubdirectory("plcat_").FullName;
    try
    {
        Directory.CreateDirectory(Path.Combine(tmp, "core", "gdb"));
        File.WriteAllText(
            Path.Combine(tmp, "core", "gdb", "resources.gd.xml"),
            $"<EntityGroup Name='R'><Entities><Entity Name='Wood' Guid='{GuidA}'><Values><ResourceDescription /></Values></Entity></Entities></EntityGroup>");

        var db = new GameInstallReader().Read(tmp);
        var resources = ResourceCatalogBuilder.Build(db);

        return db.Entities.Count == 1
            && db.Entities[0].Package == "core"
            && resources.Count == 1
            && resources[0].Name == "Wood";
    }
    finally
    {
        Directory.Delete(tmp, recursive: true);
    }
}

bool RtexRawRoundTrips()
{
    // 2x2 raw R8G8B8A8 image; RTEX stores the base mip as the file's last w*h*4 bytes.
    var pixels = new byte[] { 1, 2, 3, 255, 4, 5, 6, 128, 7, 8, 9, 0, 10, 11, 12, 255 };
    var tex = PagoniaLand.Catalog.Assets.RtexTexture.Parse(BuildRtex(PagoniaLand.Catalog.Assets.RtexTexture.FormatR8G8B8A8, 2, 2, pixels));
    if (tex is null)
    {
        return false;
    }

    var img = PagoniaLand.Catalog.Assets.TextureDecoder.Decode(tex);
    return tex.Width == 2 && tex.Height == 2 && tex.BaseMip.Length == 16
        && img is not null && img.Width == 2 && img.Height == 2 && img.Rgba.SequenceEqual(pixels);
}

bool RtexBaseMipIsLastBytes()
{
    // A 4x4 BC7 image = one 16-byte block; the base mip must be exactly those last 16 bytes,
    // even when other bytes (smaller-mip / stride filler) precede it.
    var block = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
    var blob = BuildRtex(PagoniaLand.Catalog.Assets.RtexTexture.FormatBc7Srgb, 4, 4, block);
    var tex = PagoniaLand.Catalog.Assets.RtexTexture.Parse(blob);
    return tex is not null
        && tex.Format == PagoniaLand.Catalog.Assets.RtexTexture.FormatBc7Srgb
        && tex.Width == 4 && tex.Height == 4
        && tex.BaseMip.SequenceEqual(block);
}

bool RtexRejectsNonRtex()
{
    return PagoniaLand.Catalog.Assets.RtexTexture.Parse(new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4 }) is null
        && PagoniaLand.Catalog.Assets.RtexTexture.Parse(Array.Empty<byte>()) is null;
}

bool RtexRejectsOverflowDimensions()
{
    // A crafted BC7 header whose width is huge: in unchecked int32 arithmetic blocks*16 wraps to a
    // small positive value (16) that would pass the bounds check while Width/Height stay enormous,
    // making the BCn decoder allocate Width*Height pixels for a 16-byte blob. The long-arithmetic
    // BaseMipSize must reject it (return null) rather than yield a texture.
    var blob = BuildRtex(PagoniaLand.Catalog.Assets.RtexTexture.FormatBc7Srgb, 1073741825, 4, new byte[16]);
    return PagoniaLand.Catalog.Assets.RtexTexture.Parse(blob) is null;
}

bool AssetReaderHandlesNoPaks()
{
    var tmp = Directory.CreateTempSubdirectory("plcat_").FullName;
    try
    {
        Directory.CreateDirectory(Path.Combine(tmp, "core", "gdb"));
        File.WriteAllText(Path.Combine(tmp, "core", "gdb", "x.gd.xml"), "<EntityGroup/>");
        return PagoniaLand.Catalog.Assets.AssetReader.ForInstall(tmp) is null; // extracted layout has no paks
    }
    finally
    {
        Directory.Delete(tmp, recursive: true);
    }
}

bool BuildingBuilderProjectsBuildings()
{
    var docs = new[]
    {
        ("core/gdb/buildings.gd.xml", Xml($@"
          <EntityGroup Name='Root'><Entities>
            <Entity Name='Sawmill' Guid='{GuidA}'><Values>
              <Building><Icon>core/gui/icons/buildings/icon_build_sawmill.image</Icon></Building>
              <Buildable><Category>{GuidB}</Category><UiBuildingGroup>Wood</UiBuildingGroup></Buildable>
              <AspectBuildup>
                <Costs><Item><Content><Resource>{GuidE}</Resource><Amount>5</Amount></Content></Item></Costs>
                <Employment><Unit>{GuidD}</Unit><SecondaryUnit>77777777-7777-7777-7777-777777777777</SecondaryUnit><Amount>1</Amount></Employment>
              </AspectBuildup>
              <AspectProduction>
                <Recipes><Item><Content><Recipe>{GuidF}</Recipe></Content></Item></Recipes>
                <Employment><Unit>{GuidD}</Unit><Amount>2</Amount></Employment>
                <Efficiency><TimeOfOptimalWorkStep>3.5</TimeOfOptimalWorkStep></Efficiency>
              </AspectProduction>
            </Values></Entity>
            <Entity Name='WoodBuildings' Guid='{GuidB}'><Values><BuildingCategory /></Values></Entity>
            <Entity Name='Plank' Guid='{GuidE}'><Values><ResourceDescription /></Values></Entity>
            <Entity Name='Carpenter' Guid='{GuidD}'><Values><Unit /></Values></Entity>
            <Entity Name='Helper' Guid='77777777-7777-7777-7777-777777777777'><Values><Unit /></Values></Entity>
            <Entity Name='SawmillRecipe' Guid='{GuidF}'><Values><ProductionRecipe /></Values></Entity>
          </Entities></EntityGroup>")),
    };

    var buildings = BuildingCatalogBuilder.Build(reader.ReadDocuments(docs));
    if (buildings.Count != 1)
    {
        return false;
    }

    var b = buildings[0];
    return b.Name == "Sawmill"
        && b.Category == "WoodBuildings"          // resolved from Buildable/Category
        && b.UiGroup == "Wood"
        && b.Icon == "core/gui/icons/buildings/icon_build_sawmill.image"
        && b.ConstructionCosts.Single() is { Display: "5 Plank", Guid: GuidE }   // carries the target GUID
        && b.Builder is { Display: "Carpenter", Guid: GuidD }   // amount 1 → no leading "1"
        && b.SecondaryBuilder is { Name: "Helper", Guid: "77777777-7777-7777-7777-777777777777" }   // secondary builder unit projected (no own amount)
        && b.ProductionRecipes.Single() is { Name: "SawmillRecipe", Guid: GuidF }
        && b.ProductionWorker is { Display: "2 Carpenter", Guid: GuidD }
        && b.OptimalWorkStep == "3.5";
}

bool BuildingBuilderIgnoresNonBuildings()
{
    var docs = new[]
    {
        ("core/gdb/resources.gd.xml", Xml($"<EntityGroup Name='R'><Entities><Entity Name='Wood' Guid='{GuidA}'><Values><ResourceDescription /></Values></Entity></Entities></EntityGroup>")),
    };

    return BuildingCatalogBuilder.Build(reader.ReadDocuments(docs)).Count == 0;
}

bool RecipeBuilderProjectsRecipes()
{
    var docs = new[]
    {
        ("core/gdb/productionrecipes.gd.xml", Xml($@"
          <EntityGroup Name='Root'><Entities>
            <Entity Name='PlankRecipe' Guid='{GuidA}'><Values>
              <ProductionRecipe>
                <RecipeIdentifier>plank</RecipeIdentifier>
                <DefaultState>Active</DefaultState>
                <ProductionSteps>
                  <Item><Content><Type>Input</Type><InputOutput><Resource>{GuidB}</Resource><Amount>1</Amount></InputOutput></Content></Item>
                  <Item><Content><Type>Work</Type></Content></Item>
                  <Item><Content><Type>Output</Type><InputOutput><Resource>{GuidE}</Resource><Amount>4</Amount></InputOutput></Content></Item>
                </ProductionSteps>
              </ProductionRecipe>
            </Values></Entity>
            <Entity Name='Trunk' Guid='{GuidB}'><Values><ResourceDescription /></Values></Entity>
            <Entity Name='Plank' Guid='{GuidE}'><Values><ResourceDescription /></Values></Entity>
          </Entities></EntityGroup>")),
    };

    var recipes = RecipeCatalogBuilder.Build(reader.ReadDocuments(docs));
    if (recipes.Count != 1)
    {
        return false;
    }

    var r = recipes[0];
    return r.Name == "PlankRecipe" && r.Identifier == "plank" && r.DefaultState == "Active"
        && r.Inputs.Single() is { Display: "Trunk", Guid: GuidB }   // explicit Amount 1 → no leading "1"
        && r.Outputs.Single() is { Display: "4 Plank", Guid: GuidE }
        && r.WorkSteps == 1
        && r.StepTypes.SequenceEqual(new[] { "Input", "Work", "Output" });
}

bool RecipeBuilderDropsNullGuidOutput()
{
    // Real game data (e.g. core BaseRecipe1In3Out) carries Output steps whose <Resource> is the
    // engine's all-zero null GUID — an intentionally-empty output. The authoritative catalog renders
    // that as "(none)"; the builder must drop it, not emit a blank, unresolved reference line.
    var docs = new[]
    {
        ("core/gdb/productionrecipes.gd.xml", Xml($@"
          <EntityGroup Name='Root'><Entities>
            <Entity Name='NullOutRecipe' Guid='{GuidA}'><Values>
              <ProductionRecipe>
                <ProductionSteps>
                  <Item><Content><Type>Input</Type><InputOutput><Resource>{GuidB}</Resource><Amount>1</Amount></InputOutput></Content></Item>
                  <Item><Content><Type>Output</Type><InputOutput><Resource>{NullGuid}</Resource><Amount>3</Amount></InputOutput></Content></Item>
                </ProductionSteps>
              </ProductionRecipe>
            </Values></Entity>
            <Entity Name='Trunk' Guid='{GuidB}'><Values><ResourceDescription /></Values></Entity>
          </Entities></EntityGroup>")),
    };

    var r = RecipeCatalogBuilder.Build(reader.ReadDocuments(docs)).Single();
    return r.Inputs.Single() is { Guid: GuidB }
        && r.Outputs.Count == 0;   // the null-GUID output is dropped, not a blank line
}

bool RecipeBuilderSumsRepeatedSteps()
{
    // Real recipes carry NO <Amount> on InputOutput; quantity comes from repeating identical steps
    // (e.g. AxeCopperRecipe has 5 Copper-Ore inputs). Exercise the Aggregate repeat-count path the
    // projection actually relies on — the explicit-<Amount> fixtures above never hit it.
    var docs = new[]
    {
        ("core/gdb/productionrecipes.gd.xml", Xml($@"
          <EntityGroup Name='Root'><Entities>
            <Entity Name='RepeatRecipe' Guid='{GuidA}'><Values>
              <ProductionRecipe>
                <ProductionSteps>
                  <Item><Content><Type>Input</Type><InputOutput><Resource>{GuidB}</Resource></InputOutput></Content></Item>
                  <Item><Content><Type>Input</Type><InputOutput><Resource>{GuidB}</Resource></InputOutput></Content></Item>
                  <Item><Content><Type>Input</Type><InputOutput><Resource>{GuidB}</Resource></InputOutput></Content></Item>
                  <Item><Content><Type>Input</Type><InputOutput><Resource>{GuidE}</Resource></InputOutput></Content></Item>
                  <Item><Content><Type>Output</Type><InputOutput><Resource>{GuidE}</Resource></InputOutput></Content></Item>
                </ProductionSteps>
              </ProductionRecipe>
            </Values></Entity>
            <Entity Name='Copper Ore' Guid='{GuidB}'><Values><ResourceDescription /></Values></Entity>
            <Entity Name='Plank' Guid='{GuidE}'><Values><ResourceDescription /></Values></Entity>
          </Entities></EntityGroup>")),
    };

    var r = RecipeCatalogBuilder.Build(reader.ReadDocuments(docs)).Single();
    // The 3 repeated Copper-Ore steps collapse to one "3 Copper Ore" line; the single Plank input
    // stays a singleton (display-count policy is asserted elsewhere — here we only pin the sum + dedup).
    return r.Inputs.Count == 2
        && r.Inputs.Any(x => x.Display == "3 Copper Ore" && x.Guid == GuidB)
        && r.Inputs.Any(x => x.Guid == GuidE)
        && r.Outputs.Single().Guid == GuidE;
}

bool UnitBuilderProjectsUnits()
{
    var docs = new[]
    {
        ("core/gdb/units.gd.xml", Xml($@"
          <EntityGroup Name='Root'><Entities>
            <Entity Name='Quarrier' Guid='{GuidA}'><Values>
              <Unit><Icon>core/gui/icons/characters/icon_quarrier.image</Icon></Unit>
              <RecruitmentCost>
                <ResourceCosts><Item><Content><Resource>{GuidB}</Resource><Amount>2</Amount></Content></Item></ResourceCosts>
                <NeedsManualRecruitment>True</NeedsManualRecruitment>
                <SourceRecruitableUnit>{GuidD}</SourceRecruitableUnit>
              </RecruitmentCost>
              <UnitTags><Item><Content><Tag>{GuidF}</Tag></Content></Item></UnitTags>
            </Values></Entity>
            <Entity Name='Tools' Guid='{GuidB}'><Values><ResourceDescription /></Values></Entity>
            <Entity Name='Carrier' Guid='{GuidD}'><Values /></Entity>
            <Entity Name='Worker' Guid='{GuidF}'><Values><Tag /></Values></Entity>
          </Entities></EntityGroup>")),
    };

    var units = UnitCatalogBuilder.Build(reader.ReadDocuments(docs));
    if (units.Count != 1)
    {
        return false;
    }

    var u = units[0];
    return u.Name == "Quarrier"
        && u.Icon == "core/gui/icons/characters/icon_quarrier.image"
        && u.RecruitmentCosts.Single() is { Display: "2 Tools", Guid: GuidB }
        && u.NeedsManualRecruitment == "True"
        && u.SourceRecruitableUnit is { Name: "Carrier", Guid: GuidD }
        && u.Tags.SequenceEqual(new[] { "Worker" });
}

bool ParsesSteamLibraryPaths()
{
    // A trimmed libraryfolders.vdf: two libraries, paths with doubled backslashes (as Steam writes them).
    var vdf = "\"libraryfolders\"\n{\n" +
              "\t\"0\"\n\t{\n\t\t\"path\"\t\t\"C:\\\\Program Files (x86)\\\\Steam\"\n\t}\n" +
              "\t\"1\"\n\t{\n\t\t\"path\"\t\t\"D:\\\\SteamLibrary\"\n\t}\n}";

    var paths = GameInstallLocator.ParseSteamLibraryPaths(vdf);
    return paths.Count == 2
        && paths[0] == "C:\\Program Files (x86)\\Steam"
        && paths[1] == "D:\\SteamLibrary";
}

bool GameVersionNullWithoutExe()
{
    // An extracted-layout dir (a .gd.xml, no exe) → no version.
    var temp = Path.Combine(Path.GetTempPath(), "pl-ver-" + System.Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(Path.Combine(temp, "core", "gdb"));
    File.WriteAllText(Path.Combine(temp, "core", "gdb", "dummy.gd.xml"), "<EntityGroup />");
    try
    {
        return GameVersion.TryRead(temp) is null;
    }
    finally
    {
        try { Directory.Delete(temp, true); } catch { /* best-effort */ }
    }
}

bool CatalogSnapshotRoundTrips()
{
    var docs = new[]
    {
        ("core/gdb/x.gd.xml", Xml($@"
          <EntityGroup Name='Root'><Entities>
            <Entity Name='PlankRecipe' Guid='{GuidA}'><Values>
              <ProductionRecipe>
                <RecipeIdentifier>plank</RecipeIdentifier>
                <ProductionSteps>
                  <Item><Content><Type>Input</Type><InputOutput><Resource>{GuidB}</Resource><Amount>1</Amount></InputOutput></Content></Item>
                  <Item><Content><Type>Output</Type><InputOutput><Resource>{GuidE}</Resource><Amount>4</Amount></InputOutput></Content></Item>
                </ProductionSteps>
              </ProductionRecipe>
            </Values></Entity>
            <Entity Name='Trunk' Guid='{GuidB}'><Values><ResourceDescription /></Values></Entity>
            <Entity Name='Plank' Guid='{GuidE}'><Values><ResourceDescription /></Values></Entity>
          </Entities></EntityGroup>")),
    };

    var db = reader.ReadDocuments(docs);
    var snapshot = new CatalogSnapshot(
        ResourceCatalogBuilder.Build(db), BuildingCatalogBuilder.Build(db), RecipeCatalogBuilder.Build(db),
        UnitCatalogBuilder.Build(db), ObjectiveCatalogBuilder.Build(db),
        System.Array.Empty<PagoniaLand.Catalog.Assets.PakSummary>());

    var json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(snapshot);
    var back = System.Text.Json.JsonSerializer.Deserialize<CatalogSnapshot>(json);

    return back is not null
        && back.Resources.Count == 2
        && back.Recipes.Count == 1
        && back.Recipes[0].Inputs.Single() is { Display: "Trunk", Guid: GuidB }    // Reference survives the round-trip (amount 1 suppressed)
        && back.Recipes[0].Outputs.Single() is { Display: "4 Plank", Guid: GuidE };
}

bool CatalogCacheRoundTrips()
{
    var temp = Path.Combine(Path.GetTempPath(), "pl-cache-" + System.Guid.NewGuid().ToString("N"));
    var root = Path.Combine(temp, "root");
    Directory.CreateDirectory(Path.Combine(root, "core", "gdb"));
    File.WriteAllText(Path.Combine(root, "core", "gdb", "dummy.gd.xml"), "<EntityGroup />");

    var docs = new[]
    {
        ("core/gdb/x.gd.xml", Xml($"<EntityGroup Name='R'><Entities><Entity Name='Wood' Guid='{GuidA}'><Values><ResourceDescription /></Values></Entity></Entities></EntityGroup>")),
    };
    var db = reader.ReadDocuments(docs);
    var snapshot = new CatalogSnapshot(
        ResourceCatalogBuilder.Build(db), BuildingCatalogBuilder.Build(db), RecipeCatalogBuilder.Build(db),
        UnitCatalogBuilder.Build(db), ObjectiveCatalogBuilder.Build(db),
        System.Array.Empty<PagoniaLand.Catalog.Assets.PakSummary>());
    var icons = new Dictionary<string, PagoniaLand.Catalog.Assets.RgbaImage>
    {
        ["icon_a.image"] = new PagoniaLand.Catalog.Assets.RgbaImage(2, 1, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }),
    };

    var previous = CatalogCache.Directory;
    CatalogCache.Directory = Path.Combine(temp, "cache");
    try
    {
        if (CatalogCache.TryLoad(root, out _, out _))
        {
            return false; // nothing cached yet
        }

        // A cache from a different (older) fingerprint must be evicted on Save.
        Directory.CreateDirectory(CatalogCache.Directory);
        var stale = Path.Combine(CatalogCache.Directory, "OLDFINGERPRINT0000.v2.icons.bin");
        File.WriteAllText(stale, "stale");

        var searchIndex = SearchIndexBuilder.Build(db, snapshot, "2026-01-01T00:00:00+00:00");
        CatalogCache.Save(root, snapshot, icons, searchIndex);

        if (!CatalogCache.TryLoad(root, out var loaded, out var loadedIcons) || loaded is null)
        {
            return false;
        }

        return loaded.Resources.Count == 1
            && loaded.Resources[0].Name == "Wood"
            && loadedIcons.TryGetValue("icon_a.image", out var img)
            && img.Width == 2 && img.Height == 1
            && img.Rgba.SequenceEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })
            && Directory.GetFiles(CatalogCache.Directory, "*.search-index.json").Length == 1   // index written alongside
            && !File.Exists(stale);                                                            // old-fingerprint cache evicted
    }
    finally
    {
        CatalogCache.Directory = previous;
        try { Directory.Delete(temp, true); } catch { /* best-effort cleanup */ }
    }
}

bool SearchIndexBuilderProducesItems()
{
    var docs = new[]
    {
        ("core/gdb/x.gd.xml", Xml($"<EntityGroup Name='R'><Entities>"
            + $"<Entity Name='Wood' Guid='{GuidA}'><Values><ResourceDescription /></Values></Entity>"
            + $"<Entity Name='Other' Guid='{GuidB}'><Values><SomeComponent /></Values></Entity>"
            + $"</Entities></EntityGroup>")),
    };
    var db = reader.ReadDocuments(docs);
    var snapshot = new CatalogSnapshot(
        ResourceCatalogBuilder.Build(db), BuildingCatalogBuilder.Build(db), RecipeCatalogBuilder.Build(db),
        UnitCatalogBuilder.Build(db), ObjectiveCatalogBuilder.Build(db),
        System.Array.Empty<PagoniaLand.Catalog.Assets.PakSummary>());

    var index = SearchIndexBuilder.Build(db, snapshot, "2026-01-01T00:00:00+00:00");

    // 2 entities + 1 resource (Wood) = 3 items.
    if (index.ItemCount != 3 || index.Items.Count != 3)
    {
        return false;
    }

    var wood = index.Items.First(i => i.Type == "entity" && i.Title == "Wood");
    var resource = index.Items.First(i => i.Type == "resource");
    return index.GeneratedAt == "2026-01-01T00:00:00+00:00"
        && wood.Subtitle == "Entity | Wood"
        && wood.Guid == GuidA
        && wood.Terms.Contains(GuidA)
        && wood.Fields["Kind"] == "Entity"
        && resource.Title == "Wood"
        && resource.Subtitle == "Resource | Wood";
}

bool ObjectiveBuilderProjectsObjectives()
{
    var docs = new[]
    {
        ("core/gdb/objectives.gd.xml", Xml($@"
          <EntityGroup Name='Root'><Entities>
            <Entity Name='BuildSawmill' Guid='{GuidA}'><Values>
              <GeneralObjective>
                <Category>{GuidB}</Category>
                <SortOrder>5</SortOrder>
                <Hidden>True</Hidden>
              </GeneralObjective>
              <ObjectiveMilestone>
                <Title>BuildSawmill ShortTitle</Title>
                <Description>BuildSawmill Description</Description>
              </ObjectiveMilestone>
              <ObjectiveOwnBuildableBuildingOfType>
                <Building>{GuidE}</Building>
              </ObjectiveOwnBuildableBuildingOfType>
              <ObjectiveNotifications />
            </Values></Entity>
            <Entity Name='Settlement' Guid='{GuidB}'><Values><GeneralObjectiveCategory /></Values></Entity>
            <Entity Name='Sawmill' Guid='{GuidE}'><Values><Building /></Values></Entity>
            <Entity Name='NotAnObjective' Guid='{GuidD}'><Values><Unit /></Values></Entity>
          </Entities></EntityGroup>")),
    };

    var objectives = ObjectiveCatalogBuilder.Build(reader.ReadDocuments(docs));
    if (objectives.Count != 1)
    {
        return false;
    }

    var o = objectives[0];
    return o.Name == "BuildSawmill"
        && o.Category == "Settlement"                  // GUID resolved to the category entity's name
        && o.Hidden == "True"
        && o.SortOrder == "5"
        && o.Title == "BuildSawmill ShortTitle"
        && o.Description == "BuildSawmill Description"
        && o.ObjectiveTypes.SequenceEqual(new[] { "ObjectiveMilestone", "ObjectiveOwnBuildableBuildingOfType", "ObjectiveNotifications" })
        && o.References.Single() is { Name: "Sawmill", Guid: GuidE };   // category (GuidB) + self excluded
}

// Build a minimal RTEX blob: 36-byte header (magic/version/format/width/height, rest zero so
// mip-skip is 0) followed by the base-mip data as the trailing bytes.
static byte[] BuildRtex(uint format, int width, int height, byte[] data)
{
    var header = new byte[36];
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0), PagoniaLand.Catalog.Assets.RtexTexture.Magic);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), 2);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), format);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12), (uint)width);
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16), (uint)height);
    return header.Concat(data).ToArray();
}

XDocument Xml(string xml) => XDocument.Parse(xml);
