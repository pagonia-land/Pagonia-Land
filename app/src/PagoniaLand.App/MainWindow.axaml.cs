using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform; // Avalonia 12: IClipboard.SetTextAsync is an extension here now
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PagoniaLand.Catalog;
using PagoniaLand.Catalog.Assets;
using PagoniaLand.Catalog.Domain;

namespace PagoniaLand.App;

public partial class MainWindow : Window
{
    // The game's loading windmill (RTEX BC7, 512²) — spun while a Generate runs.
    private const string LoadingIconPath = "core/ui/loading_icon_01.bc.texture";

    // GUID -> (tab index, row) for cross-navigation from a clicked detail-pane link.
    private readonly Dictionary<string, (int Tab, CatalogRow Row)> _index = new(System.StringComparer.OrdinalIgnoreCase);

    // Browser-style history for Back/Forward (buttons + the mouse back/forward buttons). EVERY
    // selection — a manual row click or a cross-nav link jump — is a navigation: it pushes the
    // current spot onto _back and clears _forward. _current tracks where we are; _restoring
    // suppresses recording while Back/Forward put us there programmatically.
    // _back is a linked list (not a Stack) so an over-cap buffer drops its OLDEST entry.
    private const int HistoryLimit = 500;
    private readonly LinkedList<(int Tab, CatalogRow? Row)> _back = new();
    private readonly Stack<(int Tab, CatalogRow? Row)> _forward = new();
    private (int Tab, CatalogRow? Row) _current = (0, null);
    private bool _restoring;

    // The full, unfiltered row lists per domain; the grids show a free-text-filtered view of these.
    private IReadOnlyList<ResourceRow> _allResources = System.Array.Empty<ResourceRow>();
    private IReadOnlyList<BuildingRow> _allBuildings = System.Array.Empty<BuildingRow>();
    private IReadOnlyList<RecipeRow> _allRecipes = System.Array.Empty<RecipeRow>();
    private IReadOnlyList<UnitRow> _allUnits = System.Array.Empty<UnitRow>();
    private IReadOnlyList<ObjectiveRow> _allObjectives = System.Array.Empty<ObjectiveRow>();

    private readonly AppSettings _settings = AppSettings.Load();

    // Tracked normal-state (non-maximized) window bounds, so we can persist them even if the
    // window is closed while maximized.
    private PixelPoint _normalPosition;
    private double _normalWidth;
    private double _normalHeight;
    private bool _opened;
    private bool _suppressNormalCapture;
    private bool _restorePending;
    private bool _generated;
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();

        DetectButton.Click += OnAutoDetect;
        BrowseButton.Click += OnBrowse;
        GenerateButton.Click += OnGenerate;
        CacheFolderButton.Click += OnOpenCacheFolder;

        // Brand → the catalog view; gear → the settings view.
        BrandButton.Click += (_, _) => ShowCatalog();
        SettingsButton.Click += (_, _) => ShowSettings();

        // Overview cards jump to their domain tab (0 = Overview, 1..4 = the domains).
        GoResources.Click += (_, _) => Tabs.SelectedIndex = 1;
        GoBuildings.Click += (_, _) => Tabs.SelectedIndex = 2;
        GoRecipes.Click += (_, _) => Tabs.SelectedIndex = 3;
        GoUnits.Click += (_, _) => Tabs.SelectedIndex = 4;
        GoObjectives.Click += (_, _) => Tabs.SelectedIndex = 5;

        // Double-click any row, in any domain, to copy its GUID to the clipboard.
        foreach (var grid in new[] { ResourceGrid, BuildingGrid, RecipeGrid, UnitGrid, ObjectiveGrid })
        {
            grid.DoubleTapped += OnRowDoubleTapped;
            grid.SelectionChanged += OnGridSelectionChanged;
        }

        // A clicked detail-pane link (a navigable reference) jumps to that entity.
        AddHandler(Button.ClickEvent, OnDetailLinkClicked, RoutingStrategies.Bubble);

        // Back / Forward across navigation jumps — buttons and the mouse back/forward buttons.
        BackButton.Click += (_, _) => GoBack();
        ForwardButton.Click += (_, _) => GoForward();
        AddHandler(PointerPressedEvent, OnPointerNav, RoutingStrategies.Tunnel);

        // Live free-text filter over the open tab's grid.
        FilterBox.TextChanged += (_, _) => ApplyFilter();
        Tabs.SelectionChanged += (_, _) => UpdateFilterCount();

        // Global "jump to anything" search across every domain.
        GlobalSearch.ItemFilter = (search, item) =>
            item is SearchResult result && result.Row.Matches((search ?? string.Empty).Trim().ToLowerInvariant());
        GlobalSearch.SelectionChanged += OnGlobalSearchSelected;

        // Keyboard: Ctrl+F focuses search, Esc clears the active search/filter, Enter generates.
        AddHandler(KeyDownEvent, OnGlobalKeyDown, RoutingStrategies.Tunnel);
        PathBox.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                OnGenerate(s, e);
            }
        };

        // Texture → PNG dump helper (an export tool; ⓘ next to the button explains it).
        DumpButton.Click += OnDumpTextures;

        // Prefer the last path the user generated from; otherwise auto-detect / a local layout.
        PathBox.Text = !string.IsNullOrWhiteSpace(_settings.LastPath)
                       && GameInstallLocator.Detect(_settings.LastPath) != GameInstallKind.Unrecognised
            ? _settings.LastPath
            : GuessInitialPath();

        // Generate is enabled only for a recognised install path (and never while busy).
        PathBox.TextChanged += (_, _) => UpdateGenerateEnabled();
        UpdateGenerateEnabled();

        // Show the "Generate to load…" hint over the still-empty domain grids.
        UpdateEmptyHints();

        // Start on the catalog if it'll auto-load on open; otherwise on settings (first run).
        if (ShouldAutoLoad())
        {
            ShowCatalog();
        }
        else
        {
            ShowSettings();
        }

        // Small version line at the bottom of settings (from the assembly's InformationalVersion).
        var assembly = typeof(MainWindow).Assembly;
        var info = System.Attribute.GetCustomAttribute(assembly, typeof(System.Reflection.AssemblyInformationalVersionAttribute))
            as System.Reflection.AssemblyInformationalVersionAttribute;
        var version = info?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? string.Empty;
        var plus = version.IndexOf('+');
        VersionInfo.Text = $"Pagonia Land {(plus >= 0 ? version[..plus] : version)}";

        BuildDownloadLinks();

        // Restore the last window size/position (or centre on first launch), and keep the
        // normal-state bounds in sync as the user moves/resizes.
        ConfigureWindowPlacement();
        PositionChanged += (_, _) => ScheduleCapture();
        Resized += (_, _) => OnResizedTracked();
    }

    // Direct download links for the latest GitHub release, picked for the running platform. The
    // tool/app archive name carries the RID (win-x64 / linux-x64 / osx-x64 / osx-arm64); schemas +
    // checksums are platform-independent. Built in code since the RID is runtime-specific.
    private void BuildDownloadLinks()
    {
        const string baseUrl = "https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/";

        string rid, ext, platformLabel;
        bool hasApp; // the app currently ships win-x64 + linux-x64 (no macOS build yet)
        if (OperatingSystem.IsWindows()) { rid = "win-x64"; ext = "zip"; platformLabel = "Windows x64"; hasApp = true; }
        else if (OperatingSystem.IsLinux()) { rid = "linux-x64"; ext = "tar.gz"; platformLabel = "Linux x64"; hasApp = true; }
        else if (OperatingSystem.IsMacOS())
        {
            var arm = RuntimeInformation.OSArchitecture == Architecture.Arm64;
            rid = arm ? "osx-arm64" : "osx-x64";
            ext = "tar.gz";
            platformLabel = arm ? "macOS (Apple Silicon)" : "macOS (Intel)";
            hasApp = false;
        }
        else { rid = "win-x64"; ext = "zip"; platformLabel = "your platform"; hasApp = true; }

        DownloadsHeading.Text = $"Latest release — direct downloads for {platformLabel}:";

        void Add(string label, string file, string? tip = null)
        {
            var link = new HyperlinkButton
            {
                Content = label,
                NavigateUri = new System.Uri(baseUrl + file),
                FontSize = 13.5,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 2, 20, 2),
            };
            if (tip is not null) ToolTip.SetTip(link, tip);
            DownloadsLinks.Children.Add(link);
        }

        Add("Manager", $"pagonia-manager-{rid}.{ext}", "pagonia-manager — bundles the patcher + paker");
        Add("Patcher", $"pagonia-patcher-{rid}.{ext}");
        Add("Paker", $"pagonia-paker-{rid}.{ext}");
        if (hasApp) Add("App", $"pagonia-land-app-{rid}.{ext}", "A fresh build of this app.");
        Add("Schemas", "pagonia-schemas.zip", "All JSON Schemas (platform-independent).");
        Add("SHA256SUMS", "SHA256SUMS.txt", "Checksums for every archive in the release.");
    }

    private void ConfigureWindowPlacement()
    {
        if (_settings.WindowWidth is double w and > 0 && _settings.WindowHeight is double h and > 0
            && _settings.WindowX is int x && _settings.WindowY is int y)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Width = w;
            Height = h;
            Position = new PixelPoint(x, y);
            _normalPosition = new PixelPoint(x, y);
            _normalWidth = w;
            _normalHeight = h;
        }
        else
        {
            // First launch (or no valid saved bounds): centre on the screen it opens on.
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    // Defer the capture: a maximize fires a Resized at the maximized size while WindowState is
    // still Normal, and only *then* flips to Maximized. By the time this deferred capture runs the
    // state is Maximized, so it skips — keeping the maximized bounds out of _normal*.
    private void ScheduleCapture() => Dispatcher.UIThread.Post(CaptureNormalBounds, DispatcherPriority.Background);

    private void CaptureNormalBounds()
    {
        if (_opened && !_suppressNormalCapture && WindowState == WindowState.Normal)
        {
            _normalPosition = Position;
            _normalWidth = Width;
            _normalHeight = Height;
        }
    }

    private void OnResizedTracked()
    {
        if (_restorePending && WindowState == WindowState.Normal)
        {
            _restorePending = false;
            ApplyNormalBounds();
            return;
        }

        ScheduleCapture();
    }

    private void ApplyNormalBounds()
    {
        if (_normalWidth <= 0 || _normalHeight <= 0)
        {
            return;
        }

        _suppressNormalCapture = true;
        Width = _normalWidth;
        Height = _normalHeight;
        Position = _normalPosition;
        Dispatcher.UIThread.Post(() => _suppressNormalCapture = false, DispatcherPriority.Background);
    }

    protected override void OnPropertyChanged(Avalonia.AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Leaving maximized/minimized: return to the exact pre-change bounds. The OS restore
        // bounds can't be trusted when the window was opened directly into the maximized state,
        // so we own this. The override happens in OnResizedTracked (right after the OS resize);
        // a deferred fallback covers the case where no resize event fires.
        if (change.Property == WindowStateProperty
            && _opened
            && change.GetNewValue<WindowState>() == WindowState.Normal
            && change.GetOldValue<WindowState>() != WindowState.Normal
            && _normalWidth > 0 && _normalHeight > 0)
        {
            _restorePending = true;
            _suppressNormalCapture = true;
            Dispatcher.UIThread.Post(
                () =>
                {
                    if (_restorePending)
                    {
                        _restorePending = false;
                        ApplyNormalBounds();
                    }
                },
                DispatcherPriority.Background);
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _opened = true;
        MaybeAutoGenerate();

        if (WindowStartupLocation != WindowStartupLocation.Manual)
        {
            return;
        }

        if (_settings.WindowMaximized)
        {
            // Put the window on the monitor it was maximized on, then maximize — deferred so the
            // position is applied first (otherwise it maximizes on whatever monitor it opened on).
            var screen = MaximizedTargetScreen();
            if (screen is not null)
            {
                Position = SavedNormalPositionOn(screen) ?? screen.WorkingArea.Position;
            }
            else if (!SavedPlacementIsVisible())
            {
                CenterOnPrimaryScreen();
            }

            Dispatcher.UIThread.Post(() => WindowState = WindowState.Maximized, DispatcherPriority.Loaded);
        }
        else if (SavedPlacementIsVisible())
        {
            Position = new PixelPoint(_settings.WindowX!.Value, _settings.WindowY!.Value);
        }
        else
        {
            CenterOnPrimaryScreen(); // a remembered monitor is gone — don't open off-screen
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        var maximized = WindowState == WindowState.Maximized;
        _settings.WindowMaximized = maximized;
        if (maximized)
        {
            // Identify the monitor by the maximized window's centre point.
            var centre = new PixelPoint(Position.X + (int)(ClientSize.Width / 2), Position.Y + (int)(ClientSize.Height / 2));
            var screen = Screens.ScreenFromPoint(centre);
            if (screen is not null)
            {
                _settings.MaximizedScreenX = screen.Bounds.X;
                _settings.MaximizedScreenY = screen.Bounds.Y;
            }
        }

        if (_normalWidth > 0 && _normalHeight > 0)
        {
            _settings.WindowX = _normalPosition.X;
            _settings.WindowY = _normalPosition.Y;
            _settings.WindowWidth = _normalWidth;
            _settings.WindowHeight = _normalHeight;
        }

        _settings.Save();
        base.OnClosing(e);
    }

    // The screen the window should re-maximize onto: the remembered maximize monitor if it still
    // exists, else the one holding the saved normal position, else primary.
    private Screen? MaximizedTargetScreen()
    {
        if (_settings.MaximizedScreenX is int sx && _settings.MaximizedScreenY is int sy)
        {
            var match = Screens.All.FirstOrDefault(s => s.Bounds.X == sx && s.Bounds.Y == sy);
            if (match is not null)
            {
                return match;
            }
        }

        if (_settings.WindowX is int x && _settings.WindowY is int y)
        {
            var byPosition = Screens.ScreenFromPoint(new PixelPoint(x, y));
            if (byPosition is not null)
            {
                return byPosition;
            }
        }

        return Screens.Primary ?? (Screens.All.Count > 0 ? Screens.All[0] : null);
    }

    private PixelPoint? SavedNormalPositionOn(Screen screen)
    {
        if (_settings.WindowX is int x && _settings.WindowY is int y)
        {
            var point = new PixelPoint(x, y);
            if (screen.Bounds.Contains(point))
            {
                return point;
            }
        }

        return null;
    }

    private bool SavedPlacementIsVisible()
    {
        if (_settings.WindowX is not int x || _settings.WindowY is not int y
            || _settings.WindowWidth is not double w || _settings.WindowHeight is not double h)
        {
            return false;
        }

        if (Screens.All.Count == 0)
        {
            return true; // no screen info available — trust the saved position
        }

        var rect = new PixelRect(x, y, (int)w, (int)h);
        foreach (var screen in Screens.All)
        {
            if (screen.Bounds.Intersects(rect))
            {
                return true;
            }
        }

        return false;
    }

    private void CenterOnPrimaryScreen()
    {
        var screen = Screens.Primary ?? (Screens.All.Count > 0 ? Screens.All[0] : null);
        if (screen is null)
        {
            return;
        }

        var area = screen.WorkingArea;
        var width = (int)(double.IsNaN(Width) ? ClientSize.Width : Width);
        var height = (int)(double.IsNaN(Height) ? ClientSize.Height : Height);
        Position = new PixelPoint(
            area.X + System.Math.Max(0, (area.Width - width) / 2),
            area.Y + System.Math.Max(0, (area.Height - height) / 2));
    }

    /// <summary>Auto-detected install, else a local game-gdb / game-paks next to the working dir.</summary>
    private static string GuessInitialPath()
    {
        if (GameInstallLocator.TryFindDefaultInstall(out var install) && install is not null)
        {
            return install;
        }

        foreach (var local in new[] { "game-gdb", "game-paks" })
        {
            var full = Path.GetFullPath(local);
            if (GameInstallLocator.Detect(full) != GameInstallKind.Unrecognised)
            {
                return full;
            }
        }

        return string.Empty;
    }

    /// <summary>Set the info banner: a bold primary line and an optional italic second line.</summary>
    private void SetStatus(string text, string? detail = null)
    {
        StatusText.Text = text;
        StatusDetail.Text = detail ?? string.Empty;
        StatusDetail.IsVisible = !string.IsNullOrEmpty(detail);

        // The header status lines ellipsize; expose the full text on hover so nothing's lost.
        var full = string.IsNullOrEmpty(detail) ? text : $"{text}\n{detail}";
        ToolTip.SetTip(StatusText, full);
        ToolTip.SetTip(StatusDetail, full);
    }

    private static string FormatSize(long bytes) =>
        bytes >= 1L << 30
            ? $"{bytes / (double)(1L << 30):0.0} GB"
            : $"{System.Math.Max(1, bytes >> 20)} MB";

    private void Index(IEnumerable<CatalogRow> rows, int tab)
    {
        foreach (var row in rows)
        {
            if (!string.IsNullOrEmpty(row.Guid))
            {
                _index[row.Guid] = (tab, row);
            }
        }
    }

    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            GlobalSearch.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrEmpty(GlobalSearch.Text))
            {
                GlobalSearch.Text = string.Empty;
                e.Handled = true;
            }
            else if (!string.IsNullOrEmpty(FilterBox.Text))
            {
                FilterBox.Text = string.Empty;
                e.Handled = true;
            }
        }
    }

    private void OnGlobalSearchSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (GlobalSearch.SelectedItem is not SearchResult result)
        {
            return;
        }

        SelectRow(result.Tab, result.Row);

        // Reset the box so it's ready for the next search (deferred to avoid re-entrancy).
        Dispatcher.UIThread.Post(() =>
        {
            GlobalSearch.SelectedItem = null;
            GlobalSearch.Text = string.Empty;
        });
    }

    private void OnDetailLinkClicked(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Button { DataContext: DetailLine line } && !string.IsNullOrEmpty(line.TargetGuid))
        {
            NavigateTo(line.TargetGuid!);
        }
    }

    private void NavigateTo(string guid)
    {
        if (_index.TryGetValue(guid, out var target))
        {
            // The selection change SelectRow makes is what records the jump (see OnGridSelectionChanged).
            // No status update: the detail pane already shows the target, so a "→ name" echo would
            // just overwrite the real status (pak sources / errors) with redundant noise.
            SelectRow(target.Tab, target.Row);
        }
        else
        {
            SetStatus("That reference isn't its own catalog entry (e.g. a category or tag).");
        }
    }

    // Filter every grid to the (already-lower-cased) query; empty shows all.
    private void ApplyFilter()
    {
        var query = (FilterBox.Text ?? string.Empty).Trim().ToLowerInvariant();
        ResourceGrid.ItemsSource = _allResources.Where(r => r.Matches(query)).ToList();
        BuildingGrid.ItemsSource = _allBuildings.Where(r => r.Matches(query)).ToList();
        RecipeGrid.ItemsSource = _allRecipes.Where(r => r.Matches(query)).ToList();
        UnitGrid.ItemsSource = _allUnits.Where(r => r.Matches(query)).ToList();
        ObjectiveGrid.ItemsSource = _allObjectives.Where(r => r.Matches(query)).ToList();
        UpdateEmptyHints();
        UpdateFilterCount();
    }

    // Show "shown / total" for the open domain tab while a filter is active; blank otherwise.
    private void UpdateFilterCount()
    {
        var query = (FilterBox.Text ?? string.Empty).Trim();
        if (query.Length == 0 || Tabs.SelectedIndex is < 1 or > 5)
        {
            FilterCount.Text = string.Empty;
            return;
        }

        var shown = GridForTab(Tabs.SelectedIndex).ItemsSource?.Cast<object>().Count() ?? 0;
        var total = Tabs.SelectedIndex switch
        {
            1 => _allResources.Count,
            2 => _allBuildings.Count,
            3 => _allRecipes.Count,
            4 => _allUnits.Count,
            _ => _allObjectives.Count,
        };
        FilterCount.Text = $"{shown} / {total}";
    }

    // Show a centred hint over any empty domain grid: before generation, or "no matches" once a
    // filter empties it.
    private void UpdateEmptyHints()
    {
        var message = _generated ? "No matches." : "Generate to load the catalog.";
        foreach (var (grid, label) in new[]
        {
            (ResourceGrid, ResourceEmpty), (BuildingGrid, BuildingEmpty), (RecipeGrid, RecipeEmpty),
            (UnitGrid, UnitEmpty), (ObjectiveGrid, ObjectiveEmpty),
        })
        {
            label.Text = message;
            label.IsVisible = grid.ItemsSource is null || !grid.ItemsSource.Cast<object>().Any();
        }
    }

    /// <summary>Switch to <paramref name="tab"/> and, if given, select + scroll to <paramref name="row"/>.</summary>
    private void SelectRow(int tab, CatalogRow? row)
    {
        // Clear any active filter so the target row is present in its grid before we select it.
        if (!string.IsNullOrEmpty(FilterBox.Text))
        {
            FilterBox.Text = string.Empty;
        }

        Tabs.SelectedIndex = tab;
        if (row is null)
        {
            return;
        }

        var grid = GridForTab(tab);
        grid.SelectedItem = row;

        // Switching tabs realises/lays out the target grid only after this pass, so an immediate
        // ScrollIntoView often no-ops. Defer it (and retry once) so it lands.
        Dispatcher.UIThread.Post(
            () =>
            {
                grid.ScrollIntoView(row, null);
                Dispatcher.UIThread.Post(() => grid.ScrollIntoView(row, null), DispatcherPriority.Background);
            },
            DispatcherPriority.Loaded);
    }

    private void OnGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_restoring || sender is not DataGrid grid || grid.SelectedItem is not CatalogRow row)
        {
            return;
        }

        var location = (TabForGrid(grid), (CatalogRow?)row);
        if (location == _current)
        {
            return;
        }

        // A new place — remember where we were, drop any forward trail (like a browser).
        PushBack(_current);
        _forward.Clear();
        _current = location;
        UpdateNavButtons();
    }

    // Push onto the back buffer, capping its size by dropping the oldest entry.
    private void PushBack((int Tab, CatalogRow? Row) location)
    {
        _back.AddLast(location);
        while (_back.Count > HistoryLimit)
        {
            _back.RemoveFirst();
        }
    }

    private void GoBack()
    {
        if (_back.Count == 0)
        {
            return;
        }

        _forward.Push(_current);
        _current = _back.Last!.Value;
        _back.RemoveLast();
        RestoreCurrent();
    }

    private void GoForward()
    {
        if (_forward.Count == 0)
        {
            return;
        }

        PushBack(_current);
        _current = _forward.Pop();
        RestoreCurrent();
    }

    // Put us at _current without recording it as a new navigation.
    private void RestoreCurrent()
    {
        _restoring = true;
        SelectRow(_current.Tab, _current.Row);
        _restoring = false;
        UpdateNavButtons();
    }

    private int TabForGrid(DataGrid grid) =>
        grid == ResourceGrid ? 1 : grid == BuildingGrid ? 2 : grid == RecipeGrid ? 3 : grid == UnitGrid ? 4 : 5;

    private void UpdateNavButtons()
    {
        BackButton.IsEnabled = _back.Count > 0;
        ForwardButton.IsEnabled = _forward.Count > 0;
    }

    private void OnPointerNav(object? sender, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsXButton1Pressed)
        {
            GoBack();
            e.Handled = true;
        }
        else if (properties.IsXButton2Pressed)
        {
            GoForward();
            e.Handled = true;
        }
    }

    private DataGrid GridForTab(int tab) => tab switch
    {
        1 => ResourceGrid,
        2 => BuildingGrid,
        3 => RecipeGrid,
        4 => UnitGrid,
        _ => ObjectiveGrid,
    };

    private async void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is DataGrid { SelectedItem: CatalogRow row } && !string.IsNullOrEmpty(row.Guid))
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is not null)
            {
                // async void: a faulted clipboard await is an unobserved exception that would crash
                // the app, so guard it (the clipboard can be unavailable on some surfaces).
                try
                {
                    await clipboard.SetTextAsync(row.Guid);
                    SetStatus("Copied GUID to clipboard", row.Guid);
                }
                catch (System.Exception ex)
                {
                    SetStatus("Couldn't copy to clipboard.", ex.Message);
                }
            }
        }
    }

    private async void OnOpenCacheFolder(object? sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(CatalogCache.Directory);
            var top = TopLevel.GetTopLevel(this);
            if (top is not null)
            {
                await top.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(CatalogCache.Directory));
            }
        }
        catch (System.Exception ex)
        {
            SetStatus("Couldn't open the cache folder.", ex.Message);
        }
    }

    private void OnAutoDetect(object? sender, RoutedEventArgs e)
    {
        if (GameInstallLocator.TryFindDefaultInstall(out var install) && install is not null)
        {
            PathBox.Text = install;
            var version = GameVersion.TryRead(install);
            SetStatus("Found a Steam install. Now Generate!", version is null ? install : $"{install}  ·  Pioneers of Pagonia {version}");
        }
        else
        {
            SetStatus("No Steam install found automatically.", "Use Browse… to pick the game folder.");
        }
    }

    private async void OnBrowse(object? sender, RoutedEventArgs e)
    {
        // async void: guard the picker await so a faulted/cancelled pick can't crash the app.
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select your game install, a pak folder, or an extracted game-gdb",
                AllowMultiple = false,
            });

            if (folders.Count > 0)
            {
                PathBox.Text = folders[0].Path.LocalPath;
            }
        }
        catch (System.Exception ex)
        {
            SetStatus("Couldn't open the folder picker.", ex.Message);
        }
    }

    private async void OnGenerate(object? sender, RoutedEventArgs e)
    {
        if (_busy)
        {
            return; // a generation is already in flight (e.g. Enter pressed twice, or auto-load + Enter)
        }

        var path = PathBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(path) || GameInstallLocator.Detect(path) == GameInstallKind.Unrecognised)
        {
            SetStatus("Pick a valid game install, pak folder, or extracted game-gdb first.");
            return;
        }

        _busy = true;
        UpdateGenerateEnabled();
        SetStatus("Reading your install…", "The big core.pak can take a moment.");

        try
        {
            // Fingerprint inside the try: it stats the install's files and can throw if the path
            // went away since the Detect() check (drive disconnect, deletion) — caught below.
            var fingerprint = CatalogCache.Fingerprint(path);
            var gameChanged = !string.IsNullOrEmpty(_settings.LastFingerprint)
                              && !string.Equals(_settings.LastFingerprint, fingerprint, System.StringComparison.OrdinalIgnoreCase);

            // Warm path: if the game is unchanged, load the snapshot + decoded icons from the
            // disk cache (no pak read, no BC7 decode). Cold path: spin the loading icon, generate,
            // and cache the result for next time.
            var cached = await Task.Run(() =>
            {
                var hit = CatalogCache.TryLoad(path, out var s, out var i);
                return (Snapshot: s, Icons: i, Hit: hit);
            });

            CatalogSnapshot snapshot;
            Dictionary<string, RgbaImage> icons;
            if (cached.Hit)
            {
                snapshot = cached.Snapshot!;
                icons = cached.Icons;
            }
            else
            {
                var assets = await Task.Run(() => AssetReader.ForInstall(path));
                var loading = await Task.Run(() => assets?.LoadImage(LoadingIconPath));
                if (loading is not null)
                {
                    Spinner.Source = ToBitmap(loading);
                    Spinner.IsVisible = true;
                }

                var generated = await Task.Run(() => CatalogGenerator.Generate(path));
                snapshot = generated.Snapshot;
                icons = generated.Icons;
                await Task.Run(() => CatalogCache.Save(path, snapshot, icons, generated.SearchIndex));
            }

            // Reverse references so a resource shows who produces / consumes / costs it (clickable).
            var producedBy = Reverse(snapshot.Recipes, x => x.Outputs, x => new Reference(x.Name, x.Guid));
            var gatheredBy = Reverse(snapshot.Buildings, x => x.GatherOutputs, x => new Reference(x.Name, x.Guid));
            var consumedBy = Reverse(snapshot.Recipes, x => x.Inputs, x => new Reference(x.Name, x.Guid));
            var buildsWith = Reverse(snapshot.Buildings, x => x.ConstructionCosts, x => new Reference(x.Name, x.Guid));
            var recruitsWith = Reverse(snapshot.Units, x => x.RecruitmentCosts, x => new Reference(x.Name, x.Guid));

            // recipe -> buildings that run it; unit -> buildings it builds / works in; unit -> units recruited from it.
            var runIn = Reverse(snapshot.Buildings, x => x.ProductionRecipes, x => new Reference(x.Name, x.Guid));
            var builds = Reverse(snapshot.Buildings, x => One(x.Builder).Concat(One(x.SecondaryBuilder)), x => new Reference(x.Name, x.Guid));
            var worksIn = Reverse(snapshot.Buildings, x => One(x.ProductionWorker), x => new Reference(x.Name, x.Guid));
            var recruitedInto = Reverse(snapshot.Units, x => One(x.SourceRecruitableUnit), x => new Reference(x.Name, x.Guid));

            // entity -> the objectives that reference it (ties the objectives domain back into the rest).
            var inObjectives = Reverse(snapshot.Objectives, x => x.References ?? Enumerable.Empty<Reference>(), x => new Reference(x.Name, x.Guid));

            var resourceRows = snapshot.Resources.Select(r => new ResourceRow
            {
                Resource = r,
                Icon = ToBitmap(IconFor(icons, r.Icon)),
                ProducedBy = LinksFor(producedBy, r.Guid),
                GatheredBy = LinksFor(gatheredBy, r.Guid),
                ConsumedBy = LinksFor(consumedBy, r.Guid),
                UsedToBuild = LinksFor(buildsWith, r.Guid),
                UsedToRecruit = LinksFor(recruitsWith, r.Guid),
                InObjectives = LinksFor(inObjectives, r.Guid),
            }).ToList();
            var buildingRows = snapshot.Buildings.Select(b => new BuildingRow { Building = b, Icon = ToBitmap(IconFor(icons, b.Icon)), InObjectives = LinksFor(inObjectives, b.Guid) }).ToList();
            var recipeRows = snapshot.Recipes.Select(r => new RecipeRow { Recipe = r, RunIn = LinksFor(runIn, r.Guid) }).ToList();
            var unitRows = snapshot.Units.Select(u => new UnitRow
            {
                Unit = u,
                Icon = ToBitmap(IconFor(icons, u.Icon)),
                Builds = LinksFor(builds, u.Guid),
                WorksIn = LinksFor(worksIn, u.Guid),
                RecruitedInto = LinksFor(recruitedInto, u.Guid),
                InObjectives = LinksFor(inObjectives, u.Guid),
            }).ToList();
            // Classify each objective's references by domain so they render as clickable links.
            var buildingGuids = new HashSet<string>(snapshot.Buildings.Select(b => b.Guid), System.StringComparer.OrdinalIgnoreCase);
            var unitGuids = new HashSet<string>(snapshot.Units.Select(u => u.Guid), System.StringComparer.OrdinalIgnoreCase);
            var resourceGuids = new HashSet<string>(snapshot.Resources.Select(r => r.Guid), System.StringComparer.OrdinalIgnoreCase);
            var objectiveGuids = new HashSet<string>(snapshot.Objectives.Select(x => x.Guid), System.StringComparer.OrdinalIgnoreCase);

            var objectiveRows = snapshot.Objectives.Select(o =>
            {
                var refs = o.References ?? (IReadOnlyList<Reference>)System.Array.Empty<Reference>();
                return new ObjectiveRow
                {
                    Objective = o,
                    RelatedObjectives = refs.Where(r => objectiveGuids.Contains(r.Guid)).ToList(),
                    Buildings = refs.Where(r => buildingGuids.Contains(r.Guid)).ToList(),
                    Units = refs.Where(r => unitGuids.Contains(r.Guid)).ToList(),
                    Resources = refs.Where(r => resourceGuids.Contains(r.Guid)).ToList(),
                };
            }).ToList();

            // GUID -> icon across the domains that have one, so reference lines in the detail pane
            // can show the target entity's icon. Recipes/objectives have none and resolve to null.
            CatalogRow.IconsByGuid = resourceRows.Cast<CatalogRow>()
                .Concat(buildingRows)
                .Concat(unitRows)
                .Where(r => r.Icon is not null)
                .GroupBy(r => r.Guid, System.StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Icon, System.StringComparer.OrdinalIgnoreCase);

            _allResources = resourceRows;
            _allBuildings = buildingRows;
            _allRecipes = recipeRows;
            _allUnits = unitRows;
            _allObjectives = objectiveRows;
            _generated = true;
            FilterBox.Text = string.Empty;
            ApplyFilter();

            // One flat pool for the global search, tagged with each row's domain + tab.
            GlobalSearch.ItemsSource = resourceRows.Select(r => new SearchResult(r, 1, "Resource"))
                .Concat(buildingRows.Select(r => new SearchResult(r, 2, "Building")))
                .Concat(recipeRows.Select(r => new SearchResult(r, 3, "Recipe")))
                .Concat(unitRows.Select(r => new SearchResult(r, 4, "Unit")))
                .Concat(objectiveRows.Select(r => new SearchResult(r, 5, "Objective")))
                .ToList();

            // Rebuild the cross-navigation index (tab indices: 1 Resources … 5 Objectives).
            _index.Clear();
            Index(resourceRows, 1);
            Index(buildingRows, 2);
            Index(recipeRows, 3);
            Index(unitRows, 4);
            Index(objectiveRows, 5);

            // Fresh rows — the old history points at stale objects, so reset navigation.
            _back.Clear();
            _forward.Clear();
            _current = (0, null);
            UpdateNavButtons();

            CountResources.Text = $"{snapshot.Resources.Count}";
            CountBuildings.Text = $"{snapshot.Buildings.Count}";
            CountRecipes.Text = $"{snapshot.Recipes.Count}";
            CountUnits.Text = $"{snapshot.Units.Count}";
            CountObjectives.Text = $"{snapshot.Objectives.Count}";

            // A representative game icon per Overview card, matched on a stable icon-path fragment.
            SetOverviewIcon(IconResources, icons, "icon_com_plank_softwood");  // a plank — basic good
            SetOverviewIcon(IconBuildings, icons, "icon_build_sawmill");        // the sawmill
            SetOverviewIcon(IconRecipes, icons, "icon_item_sword_copper");      // a crafted output
            SetOverviewIcon(IconUnits, icons, "icon_char_pioneer");             // a Pioneer
            SetOverviewIcon(IconObjectives, icons, "icon_com_map_treasure");    // a treasure map — a goal
            var version = GameVersion.TryRead(path);
            var versionPrefix = version is null ? string.Empty : $"Pioneers of Pagonia {version} · ";
            OverviewSummary.Text = $"{versionPrefix}{snapshot.Resources.Count} resources · {snapshot.Buildings.Count} buildings · {snapshot.Recipes.Count} recipes · {snapshot.Units.Count} units · {snapshot.Objectives.Count} objectives — read from your install. Click a card to open it.";

            SourcesList.ItemsSource = snapshot.Paks
                .Select(p => new SourceRow(p.Name, FormatSize(p.SizeBytes), $"{p.Entries:n0}", $"{p.GameDatabaseFiles} gd.xml", $"{p.Assets:n0}"))
                .ToList();
            SourcesHeader.Text = $"Sources — {snapshot.Paks.Count} paks · {snapshot.Paks.Sum(p => p.GameDatabaseFiles)} gd.xml · {snapshot.Paks.Sum(p => p.Assets):n0} assets";

            var pakNames = string.Join(" + ", snapshot.Paks.Select(p => Path.GetFileNameWithoutExtension(p.Name)));
            string? detail;
            if (cached.Hit)
            {
                detail = "Loaded from cache — game unchanged.";
            }
            else if (snapshot.Paks.Count > 0)
            {
                detail = (gameChanged ? "Game updated — catalog refreshed from " : "Read from ") + $"{snapshot.Paks.Count} paks: {pakNames}";
            }
            else
            {
                detail = null;
            }

            SetStatus(
                $"{snapshot.Resources.Count} resources · {snapshot.Buildings.Count} buildings · {snapshot.Recipes.Count} recipes · {snapshot.Units.Count} units · {snapshot.Objectives.Count} objectives",
                detail);

            // Remember this install + its fingerprint for next launch (game-update detection).
            _settings.LastPath = path;
            _settings.LastFingerprint = fingerprint;
            _settings.Save();

            // A successful generation switches to the catalog (whether auto-loaded or from settings).
            ShowCatalog();
        }
        catch (System.Exception ex)
        {
            SetStatus("Couldn't read that install.", ex.Message);
        }
        finally
        {
            Spinner.IsVisible = false;
            _busy = false;
            UpdateGenerateEnabled();
        }
    }

    // Warm cache for the current game, or the game changed since the last catalog → load on launch.
    // First run (no prior catalog) → wait for the user to Generate from the settings view.
    private bool ShouldAutoLoad()
    {
        var path = PathBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(path) || GameInstallLocator.Detect(path) == GameInstallKind.Unrecognised)
        {
            return false;
        }

        var fingerprint = CatalogCache.Fingerprint(path);
        var gameChanged = !string.IsNullOrEmpty(_settings.LastFingerprint)
                          && !string.Equals(_settings.LastFingerprint, fingerprint, System.StringComparison.OrdinalIgnoreCase);
        return CatalogCache.HasCache(path) || gameChanged;
    }

    private void MaybeAutoGenerate()
    {
        if (ShouldAutoLoad())
        {
            OnGenerate(this, new RoutedEventArgs());
        }
    }

    private void ShowSettings()
    {
        SettingsView.IsVisible = true;
        CatalogView.IsVisible = false;
    }

    private void ShowCatalog()
    {
        SettingsView.IsVisible = false;
        CatalogView.IsVisible = true;
    }

    private void UpdateGenerateEnabled()
    {
        var path = PathBox.Text?.Trim();
        var valid = !string.IsNullOrWhiteSpace(path) && GameInstallLocator.Detect(path) != GameInstallKind.Unrecognised;
        GenerateButton.IsEnabled = valid && !_busy;
    }

    private async void OnDumpTextures(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<IStorageFolder> folders;
        try
        {
            // async void: the picker await is before the work try/catch below — guard it too so a
            // faulted/cancelled pick can't escape as an unobserved exception.
            folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Pick a folder of .image / .texture files to export as PNG",
                AllowMultiple = false,
            });
        }
        catch (System.Exception ex)
        {
            SetStatus("Couldn't open the folder picker.", ex.Message);
            return;
        }

        if (folders.Count == 0)
        {
            return;
        }

        var folder = folders[0].Path.LocalPath;
        DumpButton.IsEnabled = false;
        SetStatus("Dumping textures to PNG…", folder);

        try
        {
            var (written, skipped) = await Task.Run(() => TextureDump.DumpFolder(folder));
            SetStatus($"Dumped {written} PNG(s) ({skipped} skipped).", $"Written next to the source files in {folder}");
        }
        catch (System.Exception ex)
        {
            SetStatus("Dump failed.", ex.Message);
        }
        finally
        {
            DumpButton.IsEnabled = true;
        }
    }

    private static RgbaImage? IconFor(IReadOnlyDictionary<string, RgbaImage> icons, string path) =>
        !string.IsNullOrEmpty(path) && icons.TryGetValue(path, out var image) ? image : null;

    // Invert a domain's outgoing references into a map: referenced GUID -> the sources that point
    // at it (each as a clickable Reference). Used for resources' "produced by / used to build" etc.
    private static Dictionary<string, List<Reference>> Reverse<T>(
        IEnumerable<T> sources, System.Func<T, IEnumerable<Reference>> referencesOf, System.Func<T, Reference> linkTo)
    {
        var map = new Dictionary<string, List<Reference>>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            var link = linkTo(source);
            foreach (var reference in referencesOf(source))
            {
                if (string.IsNullOrEmpty(reference.Guid))
                {
                    continue;
                }

                if (!map.TryGetValue(reference.Guid, out var list))
                {
                    map[reference.Guid] = list = new List<Reference>();
                }

                if (list.All(existing => existing.Guid != link.Guid))
                {
                    list.Add(link);
                }
            }
        }

        return map;
    }

    private static IReadOnlyList<Reference> LinksFor(Dictionary<string, List<Reference>> map, string guid) =>
        map.TryGetValue(guid, out var list)
            ? list.OrderBy(r => r.Name, System.StringComparer.OrdinalIgnoreCase).ToList()
            : System.Array.Empty<Reference>();

    // A nullable single reference as a 0/1 sequence, for feeding Reverse over single-ref fields.
    private static IEnumerable<Reference> One(Reference? reference) =>
        reference is null ? Enumerable.Empty<Reference>() : new[] { reference };

    // Icons render at ~30px in the grid but large (a hero icon) in the detail pane, so keep a
    // mid-res thumbnail — capped (never upscaled past native) so the big view stays crisp while
    // hundreds of them stay affordable in memory. Null image → no icon.
    // Pick a representative icon for an Overview card by a stable icon-path fragment (entity names
    // carry campaign junk, but the texture paths are clean). Hidden if this install lacks the texture.
    private static void SetOverviewIcon(Image target, IReadOnlyDictionary<string, RgbaImage> icons, string pathNeedle)
    {
        var key = icons.Keys.FirstOrDefault(k => k.Contains(pathNeedle, System.StringComparison.OrdinalIgnoreCase));
        var bitmap = key is null ? null : ToBitmap(icons[key]);
        target.Source = bitmap;
        target.IsVisible = bitmap is not null;
    }

    private static Bitmap? ToBitmap(RgbaImage? image)
    {
        // A 0-sized image (e.g. a corrupt icons.bin entry, whose width/height ReadIcons trusts
        // unvalidated) would make WriteableBitmap throw on PixelSize(0,0); skip it like a null icon.
        if (image is null || image.Width <= 0 || image.Height <= 0)
        {
            return null;
        }

        const int target = 200;
        var tw = System.Math.Min(image.Width, target);
        var th = System.Math.Min(image.Height, target);
        var thumb = new byte[tw * th * 4];
        for (var y = 0; y < th; y++)
        {
            var sy = y * image.Height / th;
            for (var x = 0; x < tw; x++)
            {
                var src = ((sy * image.Width) + (x * image.Width / tw)) * 4;
                var dst = ((y * tw) + x) * 4;
                thumb[dst] = image.Rgba[src];
                thumb[dst + 1] = image.Rgba[src + 1];
                thumb[dst + 2] = image.Rgba[src + 2];
                thumb[dst + 3] = image.Rgba[src + 3];
            }
        }

        var bitmap = new WriteableBitmap(new PixelSize(tw, th), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        using var frame = bitmap.Lock();
        Marshal.Copy(thumb, 0, frame.Address, thumb.Length);
        return bitmap;
    }
}
