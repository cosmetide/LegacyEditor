using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LegacyEditor.Models;
using LegacyEditor.Services;
using Microsoft.Win32;

namespace LegacyEditor.Views;

public partial class MainWindow : Window
{
    private readonly WorldWiperService _service = new();
    private CancellationTokenSource? _cts;
    private bool _dimOverworld = true;
    private bool _dimNether = true;
    private bool _dimEnd = true;
    private WipeMode _wipeMode = WipeMode.Whitelist;

    static readonly HashSet<string> EntityExclusions =
    [
        "LeashKnot", "Painting", "ItemFrame",
        "Pig", "Sheep", "Cow", "Chicken", "Wolf",
        "MushroomCow", "VillagerGolem", "EntityHorse",
        "ArmorStand", "Villager",
        "Furnace", "Chest", "EnderChest", "RecordPlayer",
        "Trap", "Dropper", "Sign", "MobSpawner", "Music",
        "Piston", "Cauldron", "EnchantTable", "Airportal",
        "Beacon", "DLDetector", "Hopper", "Comparator"
    ];

    public MainWindow() : this(null) { }

    public MainWindow(string? inputPath)
    {
        InitializeComponent();
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        Title = $"LegacyEditor v{v.Major}.{v.Minor}.{v.Build}";
        InitPickers();
        if (!string.IsNullOrEmpty(inputPath))
            LoadFile(inputPath);
    }

    HashSet<string>? _whitelistDefaults;

    void InitPickers()
    {
        var allEntityIds = EntityRegistry.Entities.Select(e => e.SaveId).ToHashSet();
        _whitelistDefaults = allEntityIds.Except(EntityExclusions).ToHashSet();
        EntityPickerCtrl.LoadEntities(EntityRegistry.Entities, _whitelistDefaults);
        EntityPickerCtrl.SetMode(_wipeMode);
        TileEntityPickerCtrl.LoadEntities(EntityRegistry.TileEntities, []);
        TileEntityPickerCtrl.SetMode(_wipeMode);
    }

    static string RecentPath =>
        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "recent.txt");

    void LoadFile(string inputPath)
    {
        InputPathBox.Text = inputPath;
        FileTitle.Text = System.IO.Path.GetFileName(inputPath);
        FileStatus.Text = "Ready";

        var defaultOutput = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(inputPath) ?? "",
            System.IO.Path.GetFileNameWithoutExtension(inputPath) + "_cleaned.ms");
        OutputPathBox.Text = defaultOutput;

        LogBox.Clear();
        StatusText.Text = "Configure options and click Process World";

        AddRecentFile(inputPath);
        LoadPlayerData();
    }

    static void AddRecentFile(string path)
    {
        try
        {
            var recent = RecentFiles();
            recent.Remove(path);
            recent.Insert(0, path);
            if (recent.Count > 10) recent = recent.Take(10).ToList();
            File.WriteAllLines(RecentPath, recent);
        }
        catch { }
    }

    public static List<string> RecentFiles()
    {
        try
        {
            if (File.Exists(RecentPath))
                return [.. File.ReadAllLines(RecentPath).Where(f => File.Exists(f))];
        }
        catch { }
        return [];
    }

    WipeConfig GatherConfig()
    {
        return new WipeConfig
        {
            EntitiesToWipe = EntityPickerCtrl.GetSelectedIds(),
            WipeOverworld = _dimOverworld,
            WipeNether = _dimNether,
            WipeEnd = _dimEnd,
            AdvancedMode = AdvModeToggle.IsChecked == true,
            Mode = _wipeMode,
            InputPath = InputPathBox.Text,
            OutputPath = OutputPathBox.Text
        };
    }

    WipeConfig GatherAdvancedConfig()
    {
        var cfg = GatherConfig();
        cfg.EntitiesToWipe = TileEntityPickerCtrl.GetSelectedIds();
        return cfg;
    }

    void Log(string msg)
    {
        Dispatcher.Invoke(() =>
        {
            LogBox.AppendText(msg + "\n");
            LogBox.ScrollToEnd();
            StatusText.Text = msg;
        });
    }

    void BackBtn_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        var welcome = new WelcomeWindow();
        Application.Current.MainWindow = welcome;
        welcome.Show();
        Close();
    }

    void BrowseInput_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select .ms world file",
            Filter = "MS World files (*.ms)|*.ms|All files (*.*)|*.*",
            FileName = InputPathBox.Text
        };
        if (dlg.ShowDialog() == true)
            LoadFile(dlg.FileName);
    }

    void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Save output .ms as",
            Filter = "MS World files (*.ms)|*.ms|All files (*.*)|*.*",
            FileName = System.IO.Path.GetFileName(OutputPathBox.Text)
        };
        if (dlg.ShowDialog() == true)
            OutputPathBox.Text = dlg.FileName;
    }

    void AdvMode_Changed(object sender, RoutedEventArgs e)
    {
        TileEntityCard.Visibility = AdvModeToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        LogBox.Clear();
    }

    void DimOverworld_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _dimOverworld = !_dimOverworld;
        DimOverworld.BorderBrush = FindBrush(_dimOverworld ? "AccentBrush" : "BorderBrush");
        DimOverworld.Background = FindBrush(_dimOverworld ? "BgHoverBrush" : "BgElevatedBrush");
    }

    void DimNether_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _dimNether = !_dimNether;
        DimNether.BorderBrush = FindBrush(_dimNether ? "AccentBrush" : "BorderBrush");
        DimNether.Background = FindBrush(_dimNether ? "BgHoverBrush" : "BgElevatedBrush");
    }

    void DimEnd_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _dimEnd = !_dimEnd;
        DimEnd.BorderBrush = FindBrush(_dimEnd ? "AccentBrush" : "BorderBrush");
        DimEnd.Background = FindBrush(_dimEnd ? "BgHoverBrush" : "BgElevatedBrush");
    }

    void WhitelistBtn_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _wipeMode = WipeMode.Whitelist;
        WhitelistBtn.Background = FindBrush("AccentBrush");
        WhitelistBtn.BorderBrush = FindBrush("AccentBrush");
        ((TextBlock)WhitelistBtn.Child).Foreground = FindBrush("TextPrimaryBrush") ?? Brushes.White;
        ((TextBlock)WhitelistBtn.Child).FontWeight = FontWeights.SemiBold;
        BlacklistBtn.Background = FindBrush("BgElevatedBrush");
        BlacklistBtn.BorderBrush = FindBrush("BorderBrush");
        ((TextBlock)BlacklistBtn.Child).Foreground = FindBrush("TextSecondaryBrush") ?? Brushes.Gray;
        ((TextBlock)BlacklistBtn.Child).FontWeight = FontWeights.Normal;
        EntityPickerCtrl.SetMode(_wipeMode);
        TileEntityPickerCtrl.SetMode(_wipeMode);
        EntityPickerCtrl.ResetToDefaults(_whitelistDefaults);
        TileEntityPickerCtrl.ResetToDefaults(_whitelistDefaults);
        ModeDesc.Text = "Only checked entities will be wiped";
    }

    void BlacklistBtn_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _wipeMode = WipeMode.Blacklist;
        BlacklistBtn.Background = FindBrush("AccentBrush");
        BlacklistBtn.BorderBrush = FindBrush("AccentBrush");
        ((TextBlock)BlacklistBtn.Child).Foreground = FindBrush("TextPrimaryBrush") ?? Brushes.White;
        ((TextBlock)BlacklistBtn.Child).FontWeight = FontWeights.SemiBold;
        WhitelistBtn.Background = FindBrush("BgElevatedBrush");
        WhitelistBtn.BorderBrush = FindBrush("BorderBrush");
        ((TextBlock)WhitelistBtn.Child).Foreground = FindBrush("TextSecondaryBrush") ?? Brushes.Gray;
        ((TextBlock)WhitelistBtn.Child).FontWeight = FontWeights.Normal;
        EntityPickerCtrl.SetMode(_wipeMode);
        TileEntityPickerCtrl.SetMode(_wipeMode);
        EntityPickerCtrl.ResetToDefaults(_whitelistDefaults);
        TileEntityPickerCtrl.ResetToDefaults(_whitelistDefaults);
        ModeDesc.Text = "Checked entities will be protected (not wiped)";
    }

    Brush FindBrush(string key) => (TryFindResource(key) as Brush) ?? Brushes.Gray;

    void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
    }

    void ShowResultPopup(WipeSummary summary)
    {
        Popup(new WipeCompleteWindow(summary, this));
    }

    static void Popup(Window w) => w.ShowDialog();

    void ShowAlert(string msg)
    {
        var win = new Window
        {
            Title = "LegacyEditor",
            Width = 360, Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            AllowsTransparency = true,
            Background = FindResource("BgBaseBrush") as Brush ?? Brushes.Black,
        };
        var border = new Border
        {
            Background = FindResource("BgSurfaceBrush") as Brush ?? Brushes.Black,
            BorderBrush = FindResource("BorderBrush") as Brush ?? Brushes.Gray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8)
        };
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var titleBar = new Border
        {
            Background = FindResource("BgSurfaceBrush") as Brush ?? Brushes.Black,
            BorderBrush = FindResource("BorderBrush") as Brush ?? Brushes.Gray,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 10, 16, 10),
            Child = new TextBlock
            {
                Text = "LegacyEditor",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = FindResource("TextPrimaryBrush") as Brush ?? Brushes.White
            }
        };
        Grid.SetRow(titleBar, 0);

        var body = new TextBlock
        {
            Text = msg,
            FontSize = 13,
            Foreground = FindResource("TextPrimaryBrush") as Brush ?? Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(16, 14, 16, 14),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(body, 1);

        var okBtn = new Button
        {
            Content = "OK",
            Width = 70,
            Height = 30,
            Margin = new Thickness(0, 0, 12, 10),
            HorizontalAlignment = HorizontalAlignment.Right,
            Style = FindResource("PrimaryButton") as Style ?? new Style()
        };
        okBtn.Click += (_, _) => win.Close();
        Grid.SetRow(okBtn, 2);

        grid.Children.Add(titleBar);
        grid.Children.Add(body);
        grid.Children.Add(okBtn);
        border.Child = grid;
        win.Content = border;
        win.ShowDialog();
    }

    void MinBtn_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

    // ========== XUID Tab ==========

    byte[]? _rawArchiveData;
    List<PlayerData>? _allPlayers;
    List<PlayerData>? _filteredPlayers;
    HashSet<ulong>? _importedXuids;
    readonly Stack<(byte[] data, List<PlayerData> players)> _undoStack = new();
    const int MaxUndo = 20;

    async void LoadPlayerData()
    {
        var inputPath = InputPathBox.Text;
        if (string.IsNullOrEmpty(inputPath) || !File.Exists(inputPath)) return;

        XuidLoadingOverlay.Visibility = Visibility.Visible;
        XuidLoadingText.Text = "Loading player data...";
        XuidSearchBox.IsEnabled = false;
        XuidSortBox.IsEnabled = false;
        XuidSortDirBox.IsEnabled = false;

        try
        {
            var rawData = await Task.Run(() =>
            {
                var bytes = File.ReadAllBytes(inputPath);
                return WorldWiperService.MaybeDecompressMsStatic(bytes, out _);
            });
            var players = await Task.Run(() => PlayerDataService.LoadPlayers(rawData));
            _rawArchiveData = rawData;
            _allPlayers = players;
            _importedXuids = null;
            _undoStack.Clear();
            UndoBtn.IsEnabled = false;
            WipeStatusText.Text = "";
            WipeXuidBtn.IsEnabled = false;
            ApplyPlayerFilterAndSort();
            Log($"Loaded {_allPlayers.Count} players from archive");
        }
        catch (Exception ex)
        {
            Log($"Failed to load player data: {ex.Message}");
        }
        finally
        {
            XuidLoadingOverlay.Visibility = Visibility.Collapsed;
            XuidSearchBox.IsEnabled = true;
            XuidSortBox.IsEnabled = true;
            XuidSortDirBox.IsEnabled = true;
        }
    }

    void ApplyPlayerFilterAndSort()
    {
        if (_allPlayers == null) return;

        var search = XuidSearchBox.Text?.Trim().ToLower() ?? "";
        IEnumerable<PlayerData> query = _allPlayers;

        if (search.Length > 0)
            query = query.Where(p => p.XUID.ToString().Contains(search) ||
                                     p.Username.Contains(search, StringComparison.OrdinalIgnoreCase));

        var sortIdx = XuidSortBox.SelectedIndex;
        var descending = XuidSortDirBox.SelectedIndex == 0;
        query = sortIdx switch
        {
            1 => descending ? query.OrderByDescending(p => p.XpLevel) : query.OrderBy(p => p.XpLevel),
            _ => descending ? query.OrderByDescending(p => p.TotalItemCount) : query.OrderBy(p => p.TotalItemCount),
        };

        _filteredPlayers = query.ToList();
        XuidListBox.ItemsSource = _filteredPlayers;
        PlayerCountText.Text = $"{_filteredPlayers.Count} players";
    }

    void XuidSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyPlayerFilterAndSort();

    void XuidSort_Changed(object sender, SelectionChangedEventArgs e) => ApplyPlayerFilterAndSort();

    void XuidList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        DeleteSelBtn.IsEnabled = XuidListBox.SelectedItems.Count > 0;

        var player = XuidListBox.SelectedItem as PlayerData;
        if (player == null)
        {
            DetailContent.Visibility = Visibility.Collapsed;
            DetailEmptyText.Visibility = Visibility.Visible;
            return;
        }
        DetailEmptyText.Visibility = Visibility.Collapsed;
        DetailContent.Visibility = Visibility.Visible;

        DetailUsername.Text = player.Username;
        DetailXuid.Text = $"XUID: 0x{player.XUID:X16}";
        DetailHealth.Text = $"{player.Health:F1}";
        DetailHunger.Text = player.Hunger.ToString();
        DetailItems.Text = player.TotalItemCount.ToString("N0");
        DetailScore.Text = player.Score.ToString("N0");

        var inv = player.Inventory
            .GroupBy(i => i.Name)
            .Select(g => new { Name = $"{g.Key} x{g.Sum(i => i.Count)}", g.First().Count })
            .ToList();
        DetailInvList.ItemsSource = inv;

        DetailArmorList.ItemsSource = player.Armor.Select(a => new { a.Name, a.Count }).ToList();

        var ec = player.EnderChest
            .GroupBy(i => i.Name)
            .Select(g => new { Name = $"{g.Key} x{g.Sum(i => i.Count)}", g.First().Count })
            .ToList();
        DetailEnderChestList.ItemsSource = ec;
    }

    void PushUndo()
    {
        if (_rawArchiveData == null || _allPlayers == null) return;
        _undoStack.Push((_rawArchiveData.ToArray(), _allPlayers.ToList()));
        if (_undoStack.Count > MaxUndo)
        {
            var arr = _undoStack.ToArray();
            _undoStack.Clear();
            for (int i = arr.Length - 1; i > 0; i--) _undoStack.Push(arr[i]);
        }
        UndoBtn.IsEnabled = true;
    }

    async Task RefreshWipeState(byte[] newData, string logMessage)
    {
        _rawArchiveData = newData;
        var players = await Task.Run(() => PlayerDataService.LoadPlayers(newData));
        _allPlayers = players;
        _importedXuids = null;
        WipeStatusText.Text = logMessage;
        WipeXuidBtn.IsEnabled = false;
        ApplyPlayerFilterAndSort();
    }

    async void WipeEmpty_Click(object sender, RoutedEventArgs e)
    {
        if (_rawArchiveData == null || _allPlayers == null)
        {
            ShowAlert("Load a world file first.");
            return;
        }

        var emptyCount = _allPlayers.Count(p => p.TotalItems == 0 && p.XpLevel == 0 && p.EnderChest.Count == 0);
        if (emptyCount == 0)
        {
            ShowAlert("No empty players found.");
            return;
        }

        var archive = MsArchive.Parse(_rawArchiveData);
        var emptyFiles = _allPlayers
            .Where(p => p.TotalItems == 0 && p.XpLevel == 0 && p.EnderChest.Count == 0)
            .Select(p => $"players\\{p.XUID}.dat")
            .ToHashSet();
        var keepCount = archive.Entries.Count(e => !emptyFiles.Contains(e.Filename));
        var logLines = new ObservableCollection<string>();

        WipeEmptyBtn.IsEnabled = false;
        ProgressTitle.Text = "Removing empty players...";
        ProgressBar.Minimum = 0;
        ProgressBar.Maximum = keepCount;
        ProgressBar.Value = 0;
        ProgressStatus.Text = $"Processing 0/{keepCount} entries...";
        ProgressLog.ItemsSource = logLines;
        ProgressOverlay.Visibility = Visibility.Visible;

        try
        {
            var progress = new Progress<(int current, int total)>(update =>
            {
                ProgressBar.Value = update.current;
                ProgressBar.Maximum = update.total;
                ProgressStatus.Text = $"Processing {update.current}/{update.total} entries...";
            });

            var result = await Task.Run(() => XuidWipeService.WipeEmptyPlayers(_rawArchiveData, _allPlayers, progress));
            if (result == _rawArchiveData)
            {
                ProgressOverlay.Visibility = Visibility.Collapsed;
                ShowAlert("No players were removed.");
                return;
            }

            RemoveEmptyLog(logLines, emptyFiles);

            ProgressStatus.Text = "Done!";
            logLines.Add($"Removed {emptyCount} empty player(s)");

            PushUndo();
            await RefreshWipeState(result, "Empty players removed");
        }
        catch (Exception ex)
        {
            logLines.Add($"Error: {ex.Message}");
            await Task.Delay(3000);
        }
        finally
        {
            ProgressOverlay.Visibility = Visibility.Collapsed;
            WipeEmptyBtn.IsEnabled = true;
        }
    }

    static void RemoveEmptyLog(ObservableCollection<string> log, HashSet<string> emptyFiles)
    {
        foreach (var f in emptyFiles)
            log.Add($"Removed {f}");
    }

    void ImportXuidList_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select XUID list file",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;
        ApplyImportedXuids(XuidWipeService.LoadFromTextFile(dlg.FileName));
    }

    void ImportAuthyDb_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select Authy database file",
            Filter = "SQLite DB (*.db)|*.db|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            ApplyImportedXuids(XuidWipeService.LoadFromAuthyDb(dlg.FileName));
        }
        catch (Exception ex)
        {
            ShowAlert($"Failed to read Authy DB:\n{ex.Message}");
        }
    }

    void ApplyImportedXuids(HashSet<ulong> imported)
    {
        if (_allPlayers == null)
        {
            ShowAlert("Load a world file first.");
            return;
        }
        _importedXuids = imported;
        var allXuids = _allPlayers.Select(p => p.XUID).ToHashSet();
        var kept = XuidWipeService.CountKept(allXuids, imported);
        WipeStatusText.Text = $"{kept} of {_allPlayers.Count} players will be kept";
        WipeXuidBtn.IsEnabled = true;
    }

    async void WipeXuid_Click(object sender, RoutedEventArgs e)
    {
        if (_rawArchiveData == null || _importedXuids == null)
        {
            ShowAlert("Import an XUID list or Authy DB first.");
            return;
        }

        var allXuids = _allPlayers?.Select(p => p.XUID).ToHashSet() ?? [];
        var keepCount = _allPlayers?.Count(p => _importedXuids.Contains(p.XUID)) ?? 0;
        var logLines = new ObservableCollection<string>();

        WipeXuidBtn.IsEnabled = false;
        ProgressTitle.Text = "Removing players not in imported list...";
        ProgressBar.Minimum = 0;
        ProgressBar.Maximum = keepCount;
        ProgressBar.Value = 0;
        ProgressStatus.Text = $"Processing 0/{keepCount} entries...";
        ProgressLog.ItemsSource = logLines;
        ProgressOverlay.Visibility = Visibility.Visible;

        try
        {
            var progress = new Progress<(int current, int total)>(update =>
            {
                ProgressBar.Value = update.current;
                ProgressBar.Maximum = update.total;
                ProgressStatus.Text = $"Processing {update.current}/{update.total} entries...";
            });

            var newArchive = await Task.Run(() => XuidWipeService.WipePlayers(_rawArchiveData, _importedXuids, progress));
            if (newArchive == _rawArchiveData)
            {
                ProgressOverlay.Visibility = Visibility.Collapsed;
                ShowAlert("No players were removed.");
                return;
            }

            var removed = _allPlayers?.Count(p => !_importedXuids.Contains(p.XUID)) ?? 0;
            logLines.Add($"Kept {keepCount} of {_allPlayers?.Count ?? 0} players");
            logLines.Add($"Removed {removed} player(s)");

            ProgressStatus.Text = "Done!";

            PushUndo();
            await RefreshWipeState(newArchive, "Players outside import list removed");
        }
        catch (Exception ex)
        {
            logLines.Add($"Error: {ex.Message}");
            await Task.Delay(3000);
        }
        finally
        {
            ProgressOverlay.Visibility = Visibility.Collapsed;
            WipeXuidBtn.IsEnabled = _importedXuids != null;
        }
    }

    async void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_rawArchiveData == null || _allPlayers == null) return;
        var selected = XuidListBox.SelectedItems.Cast<PlayerData>().ToList();
        if (selected.Count == 0) return;

        if (_allPlayers.Count == selected.Count)
        {
            ShowAlert("Cannot delete all players. At least one player must remain.");
            return;
        }

        PushUndo();
        DeleteSelBtn.IsEnabled = false;
        try
        {
            var newData = XuidWipeService.DeletePlayers(_rawArchiveData, selected);
            var kept = _allPlayers.Count - selected.Count;
            await Task.Run(() => RefreshWipeState(newData, $"Deleted {selected.Count} player(s), {kept} remaining"));
        }
        catch (Exception ex)
        {
            ShowAlert($"Error: {ex.Message}");
        }
        finally
        {
            DeleteSelBtn.IsEnabled = XuidListBox.SelectedItems.Count > 0;
        }
    }

    void XuidListBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Delete || e.Key == System.Windows.Input.Key.Back)
            DeleteSelected_Click(sender, e);
        else if (e.Key == System.Windows.Input.Key.Z && 
                 (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
            Undo_Click(sender, e);
    }

    void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_undoStack.Count == 0) return;
        var (prevData, prevPlayers) = _undoStack.Pop();
        _rawArchiveData = prevData;
        _allPlayers = prevPlayers;
        _importedXuids = null;
        WipeStatusText.Text = "Undone";
        WipeXuidBtn.IsEnabled = false;
        UndoBtn.IsEnabled = _undoStack.Count > 0;
        ApplyPlayerFilterAndSort();
    }

    async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (_rawArchiveData == null) return;
        var players = await Task.Run(() => PlayerDataService.LoadPlayers(_rawArchiveData));
        _allPlayers = players;
        _importedXuids = null;
        WipeStatusText.Text = "Refreshed";
        WipeXuidBtn.IsEnabled = false;
        ApplyPlayerFilterAndSort();
    }

    async void ProcessBtn_Click(object sender, RoutedEventArgs e)
    {
        var outputPath = OutputPathBox.Text;

        if (_rawArchiveData == null && (string.IsNullOrEmpty(InputPathBox.Text) || !File.Exists(InputPathBox.Text)))
        {
            ShowAlert("Please select a valid input .ms file.");
            return;
        }
        if (string.IsNullOrEmpty(outputPath))
        {
            ShowAlert("Please select an output path.");
            return;
        }

        ProcessBtn.IsEnabled = false;
        CancelBtn.IsEnabled = true;
        LogBox.Clear();
        StatusText.Text = "Processing...";
        FileStatus.Text = "Processing...";

        var config = AdvModeToggle.IsChecked == true ? GatherAdvancedConfig() : GatherConfig();
        _cts = new CancellationTokenSource();
        var progress = new Progress<string>(Log);

        try
        {
            WipeSummary summary;
            if (_rawArchiveData != null)
            {
                var wasCompressed = DetectCompressed(InputPathBox.Text);
                summary = await Task.Run(async () =>
                    await _service.ProcessWorld(_rawArchiveData, outputPath, config, progress, wasCompressed, InputPathBox.Text, _cts.Token),
                    _cts.Token);
            }
            else
            {
                summary = await Task.Run(async () =>
                    await _service.ProcessWorld(InputPathBox.Text, outputPath, config, progress, _cts.Token),
                    _cts.Token);
            }
            Log("Processing complete!");
            FileStatus.Text = "Complete";
            ShowResultPopup(summary);
        }
        catch (OperationCanceledException)
        {
            Log("Cancelled.");
            FileStatus.Text = "Cancelled";
        }
        catch (Exception ex)
        {
            Log($"Error: {ex.Message}");
            FileStatus.Text = "Error";
            ShowAlert($"Error: {ex.Message}");
        }
        finally
        {
            ProcessBtn.IsEnabled = true;
            CancelBtn.IsEnabled = false;
        }
    }

    static bool DetectCompressed(string path)
    {
        try
        {
            var data = new byte[8];
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            fs.Read(data, 0, 8);
            return BitConverter.ToInt32(data, 0) == 0;
        }
        catch { return false; }
    }
}
