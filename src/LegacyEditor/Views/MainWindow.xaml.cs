using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LegacyEditor.Models;
using LegacyEditor.Services;
using Microsoft.Win32;

namespace LegacyEditor.Views;

public partial class MainWindow : Window
{
    private readonly WorldWiperService _service = new();
    bool _hasChanges;
    bool _wasCompressed;
    string? _loadedFileName;
    private CancellationTokenSource? _cts;
    private bool _dimOverworld = true;
    private bool _dimNether = true;
    private bool _dimEnd = true;
    private WipeMode _wipeMode = WipeMode.Whitelist;

    // Dimension reset state
    private bool _dimResetOverworld;
    private bool _dimResetNether;
    private bool _dimResetEnd;

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
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName()?.Version;
        if (v != null) Title = $"LegacyEditor v{v.Major}.{v.Minor}.{v.Build}";
        InitPickers();
        if (!string.IsNullOrEmpty(inputPath))
            LoadFile(inputPath);

        // Load settings
        var settings = App.CurrentSettings;

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
            System.IO.Path.GetFileNameWithoutExtension(inputPath) + "_modified.ms");
        OutputPathBox.Text = defaultOutput;

        LogBox.Clear();
        StatusText.Text = "Use Wipe Entities or XUID tab to make changes, then click Save Changes";

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

    void EnableSaveIfDirty()
    {
        ProcessBtn.IsEnabled = _hasChanges;
        var baseTitle = _loadedFileName != null
            ? $"LegacyEditor - {_loadedFileName}"
            : "LegacyEditor";
        Title = _hasChanges ? $"{baseTitle} - Unsaved Changes" : baseTitle;
    }

    void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_hasChanges && !ConfirmAction("You have unsaved changes.\nAre you sure you want to exit?"))
            e.Cancel = true;
    }

    bool ConfirmAction(string message)
    {
        var win = new Window
        {
            Title = "LegacyEditor",
            Width = 400, Height = 240,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Background = FindResource("BgBaseBrush") as Brush ?? Brushes.Black,
        };
        var border = new Border
        {
            Background = FindResource("BgSurfaceBrush") as Brush ?? Brushes.Black,
            BorderBrush = FindResource("BorderBrush") as Brush ?? Brushes.Gray,
            BorderThickness = new Thickness(1),
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
                Text = "Confirm",
                FontSize = 14, FontWeight = FontWeights.SemiBold,
                Foreground = FindResource("TextPrimaryBrush") as Brush ?? Brushes.White
            }
        };
        titleBar.MouseDown += (_, e) => { if (e.LeftButton == MouseButtonState.Pressed) win.DragMove(); };
        Grid.SetRow(titleBar, 0);
        var body = new TextBlock
        {
            Text = message, FontSize = 13,
            Foreground = FindResource("TextPrimaryBrush") as Brush ?? Brushes.White,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16, 14, 16, 14),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(body, 1);
        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 12, 10) };
        var yesBtn = new Button { Content = "Yes", Width = 70, Height = 30, Margin = new Thickness(0, 0, 8, 0), Style = FindResource("DangerButton") as Style ?? new Style() };
        var noBtn = new Button { Content = "No", Width = 70, Height = 30, Style = FindResource("PrimaryButton") as Style ?? new Style() };
        bool result = false;
        yesBtn.Click += (_, _) => { result = true; win.Close(); };
        noBtn.Click += (_, _) => win.Close();
        btnPanel.Children.Add(yesBtn);
        btnPanel.Children.Add(noBtn);
        Grid.SetRow(btnPanel, 2);
        grid.Children.Add(titleBar);
        grid.Children.Add(body);
        grid.Children.Add(btnPanel);
        border.Child = grid;
        win.Content = border;
        DialogOverlay.Visibility = Visibility.Visible;
        win.ShowDialog();
        DialogOverlay.Visibility = Visibility.Collapsed;
        return result;
    }

    void UpdateXuidButtons()
    {
        var hasImport = _importedXuids != null;
        EraseAllBtn.Visibility = hasImport ? Visibility.Collapsed : Visibility.Visible;
        WipeXuidBtn.Visibility = hasImport ? Visibility.Visible : Visibility.Collapsed;
    }

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
            Background = FindResource("BgBaseBrush") as Brush ?? Brushes.Black,
        };
        var border = new Border
        {
            Background = FindResource("BgSurfaceBrush") as Brush ?? Brushes.Black,
            BorderBrush = FindResource("BorderBrush") as Brush ?? Brushes.Gray,
            BorderThickness = new Thickness(1),
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
        titleBar.MouseDown += (_, e) => { if (e.LeftButton == MouseButtonState.Pressed) win.DragMove(); };
        Grid.SetRow(okBtn, 2);

        grid.Children.Add(titleBar);
        grid.Children.Add(body);
        grid.Children.Add(okBtn);
        border.Child = grid;
        win.Content = border;
        DialogOverlay.Visibility = Visibility.Visible;
        win.ShowDialog();
        DialogOverlay.Visibility = Visibility.Collapsed;
    }

    void MinBtn_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

    void SettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        var settings = App.CurrentSettings;
        var win = new Window
        {
            Title = "Settings",
            Width = 450, Height = 480,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Background = FindResource("BgBaseBrush") as Brush ?? Brushes.Black,
        };

        var border = new Border
        {
            Background = FindResource("BgSurfaceBrush") as Brush ?? Brushes.Black,
            BorderBrush = FindResource("BorderBrush") as Brush ?? Brushes.Gray,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16, 0, 16, 16)
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var titleGrid = new Grid();
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titleText = new TextBlock
        {
            Text = "Settings", FontSize = 14, FontWeight = FontWeights.SemiBold,
            Foreground = FindResource("TextPrimaryBrush") as Brush ?? Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        };
        var titleCloseBtn = new Button
        {
            Content = "\u2716", Width = 34, Height = 26, FontSize = 12, Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = FindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray
        };
        titleCloseBtn.Click += (_, _) => win.Close();
        Grid.SetColumn(titleText, 0);
        Grid.SetColumn(titleCloseBtn, 1);
        titleGrid.Children.Add(titleText);
        titleGrid.Children.Add(titleCloseBtn);

        var titleBar = new Border
        {
            Background = FindResource("BgSurfaceBrush") as Brush ?? Brushes.Black,
            BorderBrush = FindResource("BorderBrush") as Brush ?? Brushes.Gray,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 10, 8, 10),
            Child = titleGrid
        };
        Grid.SetRow(titleBar, 0);

        var panel = new StackPanel { Margin = new Thickness(0, 18, 0, 0) };

        // Helpers for themed controls
        Brush bgElevated = FindResource("BgElevatedBrush") as Brush ?? Brushes.Gray;
        Brush textPrimary = FindResource("TextPrimaryBrush") as Brush ?? Brushes.White;
        Brush textSecondary = FindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray;
        Brush borderBrush = FindResource("BorderBrush") as Brush ?? Brushes.Gray;

        TextBlock SectionTitle(string text) => new()
        {
            Text = text, FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = textPrimary, Margin = new Thickness(0, 0, 0, 10)
        };


        // Logs section
        var logHeader = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        logHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        logHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        logHeader.Children.Add(new TextBlock
        {
            Text = "Logs", FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = textPrimary, VerticalAlignment = VerticalAlignment.Center
        });
        var copyBtn = new Button
        {
            Content = "Copy", Width = 60, Height = 24, FontSize = 11,
            Style = FindResource("SmallButton") as Style ?? new Style()
        };
        copyBtn.Click += (_, _) => { try { Clipboard.SetText(LogBox.Text); } catch { } };
        Grid.SetColumn(copyBtn, 1);
        logHeader.Children.Add(copyBtn);
        panel.Children.Add(logHeader);

        var logBox = new TextBox
        {
            IsReadOnly = true, Background = bgElevated,
            Foreground = textPrimary, FontFamily = new FontFamily("Consolas"), FontSize = 11,
            Text = string.Join("\n", LogBox.Text.Split('\n').Take(50)),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            BorderThickness = new Thickness(1), BorderBrush = borderBrush,
            Padding = new Thickness(10), Height = 120,
            Margin = new Thickness(0, 0, 0, 18)
        };
        panel.Children.Add(logBox);


        // Wipe Settings section
        panel.Children.Add(SectionTitle("Wipe Settings"));
        var wipeCard = new Border
        {
            Background = FindResource("BgElevatedBrush") as Brush ?? Brushes.Gray,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18, 16, 18, 16)
        };
        var wipeStack = new StackPanel();

        var xpRow = new Grid();
        xpRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        xpRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        xpRow.Margin = new Thickness(0, 0, 0, 14);
        var xpLabel = new TextBlock
        {
            Text = "Min XP Level", FontSize = 13, FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = textPrimary
        };
        var xpBox = new TextBox
        {
            Text = settings.WipeEmptyXpLevel.ToString(),
            Width = 100, FontSize = 14,
            Foreground = textPrimary,
            Margin = new Thickness(14, 0, 0, 0)
        };
        xpBox.CaretBrush = new SolidColorBrush(Colors.White);
        var xpTemplate = new ControlTemplate(typeof(TextBox));
        var xpBorder = new FrameworkElementFactory(typeof(Border));
        xpBorder.SetValue(Border.BackgroundProperty, bgElevated);
        xpBorder.SetValue(Border.BorderBrushProperty, borderBrush);
        xpBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        xpBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        xpBorder.SetValue(Border.PaddingProperty, new Thickness(6, 4, 6, 4));
        xpBorder.AppendChild(new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost"));
        xpTemplate.VisualTree = xpBorder;
        xpBox.Template = xpTemplate;
        Grid.SetColumn(xpLabel, 0); Grid.SetColumn(xpBox, 1);
        xpRow.Children.Add(xpLabel); xpRow.Children.Add(xpBox);
        wipeStack.Children.Add(xpRow);

        var xpDesc = new TextBlock
        {
            Text = "Players with XP level below or equal to this value will be removed.",
            FontSize = 11, Foreground = textSecondary,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0)
        };
        wipeStack.Children.Add(xpDesc);

        var itemRow = new Grid();
        itemRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        itemRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        itemRow.Margin = new Thickness(0, 18, 0, 14);
        var itemLabel = new TextBlock
        {
            Text = "Min Item Count", FontSize = 13, FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = textPrimary
        };
        var itemBox = new TextBox
        {
            Text = settings.WipeEmptyItemCount.ToString(),
            Width = 100, FontSize = 14,
            Foreground = textPrimary,
            Margin = new Thickness(8, 0, 0, 0)
        };
        itemBox.Margin = new Thickness(14, 0, 0, 0);
        itemBox.CaretBrush = new SolidColorBrush(Colors.White);
        var itemTemplate = new ControlTemplate(typeof(TextBox));
        var itemBorder = new FrameworkElementFactory(typeof(Border));
        itemBorder.SetValue(Border.BackgroundProperty, bgElevated);
        itemBorder.SetValue(Border.BorderBrushProperty, borderBrush);
        itemBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        itemBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        itemBorder.SetValue(Border.PaddingProperty, new Thickness(6, 4, 6, 4));
        itemBorder.AppendChild(new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost"));
        itemTemplate.VisualTree = itemBorder;
        itemBox.Template = itemTemplate;
        Grid.SetColumn(itemLabel, 0); Grid.SetColumn(itemBox, 1);
        itemRow.Children.Add(itemLabel); itemRow.Children.Add(itemBox);
        wipeStack.Children.Add(itemRow);

        var itemDesc = new TextBlock
        {
            Text = "Players with this many items total or fewer will be removed.",
            FontSize = 11, Foreground = textSecondary,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0)
        };
        wipeStack.Children.Add(itemDesc);

        wipeCard.Child = wipeStack;
        panel.Children.Add(wipeCard);

                win.Closed += (_, _) =>
        {
            if (int.TryParse(xpBox.Text, out var xp) && xp >= 0)
                App._currentSettings.WipeEmptyXpLevel = xp;
            if (int.TryParse(itemBox.Text, out var ic) && ic >= 0)
                App._currentSettings.WipeEmptyItemCount = ic;
            App.SaveSettings();
        };

        var scroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(scroll, 1);

        grid.Children.Add(titleBar);
        grid.Children.Add(scroll);
        border.Child = grid;
        win.Content = border;
        win.ShowDialog();
    }


    // ========== World Tab: Dimension Reset ==========

    void UpdateResetDimButton()
    {
        ResetDimensionBtn.IsEnabled = _dimResetOverworld || _dimResetNether || _dimResetEnd;
    }

    void ToggleDimResetBorder(Border border, ref bool state)
    {
        state = !state;
        border.BorderBrush = FindBrush(state ? "AccentBrush" : "BorderBrush");
        border.Background = FindBrush(state ? "BgHoverBrush" : "BgElevatedBrush");
        UpdateResetDimButton();
    }

    void DimResetOverworld_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        ToggleDimResetBorder(DimResetOverworld, ref _dimResetOverworld);

    void DimResetNether_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        ToggleDimResetBorder(DimResetNether, ref _dimResetNether);

    void DimResetEnd_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        ToggleDimResetBorder(DimResetEnd, ref _dimResetEnd);

    async void DiagnoseWorld_Click(object sender, RoutedEventArgs e)
    {
        if (_rawArchiveData == null)
        {
            ShowAlert("Load a world file first.");
            return;
        }

        DiagnoseBtn.IsEnabled = false;
        DiagnoseBtn.Content = "Scanning...";
        DiagnosisResultBox.Clear();

        void Report(string line) => Dispatcher.Invoke(() =>
        {
            DiagnosisResultBox.AppendText(line + "\n");
            DiagnosisResultBox.ScrollToEnd();
        });

        int badEntries = 0, overlaps = 0, duplicates = 0;
        int regionCount = 0, totalChunks = 0, totalFailures = 0, hugeRegions = 0;
        bool levelDatOk = false;

        await Task.Run(() =>
        {
            var archive = MsArchive.Parse(_rawArchiveData);
            var entries = archive.Entries;

            Report("=== World Diagnostics ===");
            Report($"File size: {_rawArchiveData.Length:N0} bytes");
            Report("");

            Report("=== Archive Entries ===");
            for (int i = 0; i < entries.Count; i++)
            {
                var en = entries[i];
                bool bad = false;
                if (en.StartOffset < 0 || en.StartOffset >= _rawArchiveData.Length)
                { Report($"  [{i}] BAD offset: '{en.Filename}' offset={en.StartOffset}"); bad = true; }
                if (en.Length <= 0)
                { Report($"  [{i}] BAD length: '{en.Filename}' length={en.Length}"); bad = true; }
                if (!bad && en.StartOffset + en.Length > _rawArchiveData.Length)
                { Report($"  [{i}] BAD range: '{en.Filename}' offset={en.StartOffset}+{en.Length} > {_rawArchiveData.Length}"); bad = true; }
                if (bad) badEntries++;
            }

            var sorted = entries.Where(en => en.StartOffset >= 0 && en.Length > 0 && en.StartOffset + en.Length <= _rawArchiveData.Length).OrderBy(en => en.StartOffset).ToList();
            for (int i = 1; i < sorted.Count; i++)
            {
                if (sorted[i - 1].StartOffset + sorted[i - 1].Length > sorted[i].StartOffset)
                {
                    Report($"  OVERLAP: '{sorted[i - 1].Filename}' ends at {sorted[i - 1].StartOffset + sorted[i - 1].Length}, '{sorted[i].Filename}' starts at {sorted[i].StartOffset}");
                    overlaps++;
                }
            }

            var dupeGroups = entries.GroupBy(en => en.Filename).Where(g => g.Count() > 1);
            foreach (var g in dupeGroups)
            { Report($"  DUPLICATE: '{g.Key}' appears {g.Count()} times"); duplicates++; }

            Report($"Total entries: {entries.Count}, Bad: {badEntries}, Overlaps: {overlaps}, Dups: {duplicates}");
            Report("");

            Report("=== Region Files ===");
            foreach (var entry in entries)
            {
                if (!entry.Filename.EndsWith(".mca", StringComparison.OrdinalIgnoreCase) &&
                    !entry.Filename.EndsWith(".mcr", StringComparison.OrdinalIgnoreCase))
                    continue;

                regionCount++;
                if (entry.StartOffset < 0 || entry.StartOffset + entry.Length > _rawArchiveData.Length)
                { Report($"  {entry.Filename}: SKIPPED (bad bounds)"); continue; }

                var regionData = new byte[entry.Length];
                Array.Copy(_rawArchiveData, entry.StartOffset, regionData, 0, entry.Length);
                if (entry.Length > 10_000_000) hugeRegions++;

                int chunks = 0, failures = 0;
                for (int cz = 0; cz < 32; cz++)
                    for (int cx = 0; cx < 32; cx++)
                    {
                        int idx = cx + cz * 32;
                        if (idx * 4 + 4 > regionData.Length) continue;
                        int off = regionData[idx * 4] << 16 | regionData[idx * 4 + 1] << 8 | regionData[idx * 4 + 2];
                        if (off == 0) continue;
                        chunks++;
                        if (RegionFile.ReadChunk(regionData, cx, cz) == null) failures++;
                    }

                Report($"  {entry.Filename}: {chunks} chunks, {failures} failures ({entry.Length:N0} bytes)");
                totalChunks += chunks; totalFailures += failures;
            }

            Report($"Regions: {regionCount}, Chunks: {totalChunks}, Failures: {totalFailures}" + (hugeRegions > 0 ? $", >10MB: {hugeRegions}" : ""));
            Report("");

            Report("=== level.dat ===");
            var levelEntry = entries.FirstOrDefault(en => en.Filename.EndsWith("level.dat", StringComparison.OrdinalIgnoreCase));
            if (levelEntry != null)
            {
                if (levelEntry.StartOffset < 0 || levelEntry.StartOffset + levelEntry.Length > _rawArchiveData.Length)
                { Report("  FOUND but bad entry bounds"); }
                else
                {
                    var ldRaw = new byte[levelEntry.Length];
                    Array.Copy(_rawArchiveData, levelEntry.StartOffset, ldRaw, 0, levelEntry.Length);
                    Report($"  Raw: {levelEntry.Length:N0} bytes");
                    var decompressed = TryDecompressLevelDat(ldRaw);
                    if (decompressed == null || decompressed.Length == 0) { Report("  FAILED to decompress"); }
                    else
                    {
                        Report($"  Decompressed: {decompressed.Length:N0} bytes");
                        try
                        {
                            var tag = NbtParser.Parse(decompressed);
                            levelDatOk = tag?.Value is Dictionary<string, NbtParser.NbtTag>;
                            Report(levelDatOk ? "  NBT: OK" : "  NBT: FAILED (no root)");
                        }
                        catch (Exception ex) { Report($"  NBT: FAILED ({ex.Message})"); }
                    }
                }
            }
            else { Report("  NOT FOUND"); }

            Report("");
            Report("=== Diagnostics Complete ===");
        });

        var issues = new List<string>();
        if (badEntries > 0) issues.Add($"{badEntries} bad entries");
        if (overlaps > 0) issues.Add($"{overlaps} overlapping entries");
        if (duplicates > 0) issues.Add($"{duplicates} duplicate names");
        if (totalFailures > 0) issues.Add($"{totalFailures} chunk failures");
        if (!levelDatOk) issues.Add("level.dat invalid");

        if (issues.Count > 0)
            ShowAlert($"Issues found:\n- {string.Join("\n- ", issues)}\n\nCheck the results in the diagnostics card above.");
        else
            ShowAlert("No issues found. The archive looks healthy.");

        DiagnoseBtn.IsEnabled = true;
        DiagnoseBtn.Content = "Run Diagnostics";
    }

    async void ResetDimension_Click(object sender, RoutedEventArgs e)
    {
        if (_rawArchiveData == null)
        {
            ShowAlert("Load a world file first.");
            return;
        }

        var dims = new List<string>();
        if (_dimResetOverworld) dims.Add("Overworld");
        if (_dimResetNether) dims.Add("Nether");
        if (_dimResetEnd) dims.Add("End");

        if (dims.Count == 0)
        {
            ShowAlert("Select at least one dimension to reset.");
            return;
        }

        if (!ConfirmAction($"Reset {string.Join(" and ", dims)}?\n\nAll region files in selected dimensions will be removed. The game will regenerate them.\n\nUse Save Changes to write to disk."))
            return;

        ResetDimensionBtn.IsEnabled = false;
        ProgressTitle.Text = "Resetting dimensions...";
        ProgressBar.IsIndeterminate = true;
        ProgressStatus.Text = "Removing region files...";
        ProgressOverlay.Visibility = Visibility.Visible;

        try
        {
            var result = await Task.Run(() =>
            {
                var archive = MsArchive.Parse(_rawArchiveData);
                var toRemove = new HashSet<string>();

                foreach (var entry in archive.Entries)
                {
                    var fn = entry.Filename;
                    if (!fn.EndsWith(".mcr", StringComparison.OrdinalIgnoreCase) &&
                        !fn.EndsWith(".mca", StringComparison.OrdinalIgnoreCase))
                        continue;

                    bool isNether = fn.StartsWith("DIM-1", StringComparison.OrdinalIgnoreCase);
                    bool isEnd = fn.StartsWith("DIM1", StringComparison.OrdinalIgnoreCase) ||
                                 fn.StartsWith("DIM1/", StringComparison.OrdinalIgnoreCase);

                    if (_dimResetOverworld && !isNether && !isEnd)
                        toRemove.Add(fn);
                    else if (_dimResetNether && isNether)
                        toRemove.Add(fn);
                    else if (_dimResetEnd && isEnd)
                        toRemove.Add(fn);
                }

                if (toRemove.Count == 0) return _rawArchiveData;
                return archive.Rebuild(_rawArchiveData, toRemove);
            });

            if (result != _rawArchiveData)
            {
                _rawArchiveData = result;
                _hasChanges = true;
                EnableSaveIfDirty();
                Log($"Reset {string.Join(" and ", dims)} ({result.Length} bytes)");
            }
            else
            {
                ShowAlert("No region files found to remove.");
            }
        }
        catch (Exception ex)
        {
            Log($"Error: {ex.Message}");
            ShowAlert($"Error: {ex.Message}");
        }
        finally
        {
            ProgressOverlay.Visibility = Visibility.Collapsed;
            ResetDimensionBtn.IsEnabled = true;
            ProgressBar.IsIndeterminate = false;
        }
    }

    async Task ScanMapsAsync()
    {
        if (_rawArchiveData == null || _allPlayers == null)
        {
            ShowAlert("Load a world file first.");
            return;
        }

        ScanMapsBtn.IsEnabled = false;
        ScanMapsBtn.Content = "Scanning...";
        MapScanResultBox.Clear();
        WipeMapsBtn.IsEnabled = false;
        WipeAllMapsBtn.IsEnabled = false;

        void Report(string line) => Dispatcher.Invoke(() =>
        {
            MapScanResultBox.AppendText(line + "\n");
            MapScanResultBox.ScrollToEnd();
        });

        try
        {
            var analysis = await Task.Run(() =>
                MapAnalyzerService.Analyze(_rawArchiveData, _allPlayers));

            Report("=== Map Analysis ===");
            Report($"Map files (map_*.dat):     {analysis.TotalMapFiles}");
            Report($"In player inventories:     {analysis.InPlayerInventories}");
            Report($"In player ender chests:    {analysis.InPlayerEnderChest}");
            Report($"Placed total:              {analysis.PlacedTotal}");
            Report($"Unused map files:          {analysis.UnusedMaps}");
            Report("");
            if (analysis.MappingEntries > 0)
            {
                Report($"=== XUID Mapping Analysis ===");
                Report($"largeMapDataMappings entries: {analysis.MappingEntries}");
                Report($"Maps owned by current players: {analysis.KnownPlayerMaps}");
                Report($"Unlinked map files:         {analysis.UnlinkedMaps}");
                Report($"Stale mapping entries:      {analysis.StaleMappingEntries}");
            }
            else
            {
                Report("No largeMapDataMappings.dat found for XUID-based unlinked map detection.");
            }

            WipeMapsBtn.IsEnabled = true;
            WipeAllMapsBtn.IsEnabled = true;
        }
        catch (Exception ex)
        {
            Report($"Error: {ex.Message}");
        }
        finally
        {
            ScanMapsBtn.IsEnabled = true;
            ScanMapsBtn.Content = "Analyze Maps";
        }
    }

    async void ScanMaps_Click(object sender, RoutedEventArgs e) => await ScanMapsAsync();

    async void WipeMaps_Click(object sender, RoutedEventArgs e)
    {
        if (_rawArchiveData == null || _allPlayers == null)
        {
            ShowAlert("Load a world file first.");
            return;
        }

        var options = new List<string>();
        if (WipeOrphanedCheck.IsChecked == true) options.Add("unlinked");
        if (WipeInvCheck.IsChecked == true) options.Add("inventories");
        if (WipeUnusedFilesCheck.IsChecked == true) options.Add("unused files");

        if (options.Count == 0)
        {
            ShowAlert("Select at least one wipe option.");
            return;
        }

        if (!ConfirmAction($"Wipe {string.Join(", ", options)} from the world?\n\nThis cannot be undone. Save Changes to write to disk."))
            return;

        WipeMapsBtn.IsEnabled = false;
        WipeAllMapsBtn.IsEnabled = false;
        ScanMapsBtn.IsEnabled = false;

        ProgressTitle.Text = "Wiping maps...";
        ProgressBar.IsIndeterminate = false;
        ProgressStatus.Text = "Starting...";
        ProgressOverlay.Visibility = Visibility.Visible;

        try
        {
            bool doUnlinked = WipeOrphanedCheck.IsChecked == true;
            bool doInv = WipeInvCheck.IsChecked == true;
            bool doUnused = WipeUnusedFilesCheck.IsChecked == true;

            var progress = new Progress<(int current, int total, string status)>(update =>
            {
                Dispatcher.Invoke(() =>
                {
                    ProgressBar.Value = update.total > 0 ? (double)update.current / update.total * 100 : 0;
                    ProgressBar.Maximum = update.total > 0 ? update.total : 1;
                    ProgressBar.Value = Math.Min(update.current, update.total);
                    ProgressStatus.Text = update.status;
                });
            });

            var result = await Task.Run(() =>
            {
                byte[] data = _rawArchiveData;
                bool changed = false;

                if (doUnlinked)
                {
                    Dispatcher.Invoke(() => ProgressStatus.Text = "Finding unlinked maps...");
                    var unlinkedIds = MapWipeService.FindUnlinkedMapIds(data, _allPlayers);
                    if (unlinkedIds.Count > 0)
                    {
                        Dispatcher.Invoke(() => ProgressStatus.Text = $"Wiping {unlinkedIds.Count} unlinked map items from inventories...");

                        data = MapWipeService.WipePlacedMapsFiltered(data, _allPlayers, doInv, false, false, unlinkedIds, progress);
                        changed = true;
                    }
                }
                else
                {
                    // Non-orphaned wipe: just wipe from selected locations
                    data = MapWipeService.WipePlacedMapsFiltered(data, _allPlayers, doInv, false, false, null, progress);
                    changed = true;
                }

                if (doUnused)
                {
                    data = MapWipeService.WipeUnusedMaps(data, _allPlayers);
                    changed = true;
                }

                // Validate final archive before returning
                if (changed && !MsArchive.TryValidate(data, out var valError))
                    throw new InvalidOperationException($"Archive validation failed after wipe: {valError}");

                return changed ? data : _rawArchiveData;
            });

            if (result != _rawArchiveData)
            {
                _rawArchiveData = result;
                _hasChanges = true;
                EnableSaveIfDirty();
                Log($"Wiped maps: {string.Join(", ", options)}");
                // Reload player data from modified archive so counts are accurate
                _allPlayers = await Task.Run(() => PlayerDataService.LoadPlayers(_rawArchiveData));
                await Task.Run(() => ResolveMapOwnership(_allPlayers, _rawArchiveData));
                _importedXuids = null;
                _undoStack.Clear();
                if (XuidListBox.IsLoaded)
                    ApplyPlayerFilterAndSort();
                ProgressOverlay.Visibility = Visibility.Collapsed;
                ShowAlert("Map cleanup complete! Use Save Changes to write to disk.");
                await ScanMapsAsync();
            }
            else
            {
                ProgressOverlay.Visibility = Visibility.Collapsed;
                ShowAlert("No maps were found to wipe.");
            }
        }
        catch (Exception ex)
        {
            ProgressOverlay.Visibility = Visibility.Collapsed;
            Log($"Error: {ex.Message}");
            ShowAlert($"Error: {ex.Message}");
        }
        finally
        {
            WipeMapsBtn.IsEnabled = true;
            WipeAllMapsBtn.IsEnabled = true;
            ScanMapsBtn.IsEnabled = true;
        }
    }

    async void WipeAllMaps_Click(object sender, RoutedEventArgs e)
    {
        if (_rawArchiveData == null || _allPlayers == null)
        {
            ShowAlert("Load a world file first.");
            return;
        }

        if (!ConfirmAction("Wipe ALL maps from the world?\n\nThis removes all map items from inventories, containers, item frames, and all map_*.dat files.\n\nUse Save Changes to write to disk."))
            return;

        WipeMapsBtn.IsEnabled = false;
        WipeAllMapsBtn.IsEnabled = false;
        ScanMapsBtn.IsEnabled = false;

        ProgressTitle.Text = "Wiping all maps...";
        ProgressBar.IsIndeterminate = true;
        ProgressStatus.Text = "Removing all maps...";
        ProgressOverlay.Visibility = Visibility.Visible;

        try
        {
            var progress = new Progress<(int current, int total, string status)>(update =>
            {
                Dispatcher.Invoke(() =>
                {
                    ProgressBar.Value = update.total > 0 ? (double)update.current / update.total * 100 : 0;
                    ProgressBar.Maximum = update.total > 0 ? update.total : 1;
                    ProgressBar.Value = Math.Min(update.current, update.total);
                    ProgressStatus.Text = update.status;
                });
            });

            var result = await Task.Run(() =>
                MapWipeService.WipeAllMaps(_rawArchiveData, _allPlayers, progress));

            if (result != _rawArchiveData)
            {
                if (!MsArchive.TryValidate(result, out var valError))
                {
                    Log($"Archive validation failed after wipe all: {valError}");
                    ShowAlert($"Error: {valError}");
                    return;
                }
                _rawArchiveData = result;
                _hasChanges = true;
                EnableSaveIfDirty();
                Log("Wiped all maps");
                ProgressOverlay.Visibility = Visibility.Collapsed;
                ShowAlert("All maps removed! Use Save Changes to write to disk.");
                await ScanMapsAsync();
            }
            else
            {
                ProgressOverlay.Visibility = Visibility.Collapsed;
                ShowAlert("No maps found to wipe.");
            }
        }
        catch (Exception ex)
        {
            ProgressOverlay.Visibility = Visibility.Collapsed;
            Log($"Error: {ex.Message}");
            ShowAlert($"Error: {ex.Message}");
        }
        finally
        {
            WipeMapsBtn.IsEnabled = true;
            WipeAllMapsBtn.IsEnabled = true;
            ScanMapsBtn.IsEnabled = true;
        }
    }

    static byte[]? TryDecompressLevelDat(byte[] data)
    {
        try { return ZLibDecompress(data); } catch { }
        try { return GZipDecompress(data); } catch { }
        try { return DeflateDecompress(data); } catch { }
        return data;
    }

    static byte[] ZLibDecompress(byte[] compressed)
    {
        using var compStream = new MemoryStream(compressed);
        using var zlib = new System.IO.Compression.ZLibStream(compStream, System.IO.Compression.CompressionMode.Decompress);
        using var result = new MemoryStream();
        zlib.CopyTo(result);
        return result.ToArray();
    }

    static byte[] GZipDecompress(byte[] compressed)
    {
        using var compStream = new MemoryStream(compressed);
        using var gzip = new System.IO.Compression.GZipStream(compStream, System.IO.Compression.CompressionMode.Decompress);
        using var result = new MemoryStream();
        gzip.CopyTo(result);
        return result.ToArray();
    }

    static byte[] DeflateDecompress(byte[] compressed)
    {
        using var compStream = new MemoryStream(compressed);
        using var deflate = new System.IO.Compression.DeflateStream(compStream, System.IO.Compression.CompressionMode.Decompress);
        using var result = new MemoryStream();
        deflate.CopyTo(result);
        return result.ToArray();
    }

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

        var loadLogLines = new ObservableCollection<string>();
        IProgress<string> progress = new Progress<string>(msg =>
        {
            ProgressTitle.Text = "Loading Archive";
            ProgressStatus.Text = msg;
            loadLogLines.Add(msg);
            if (loadLogLines.Count > 100)
                loadLogLines.RemoveAt(0);
        });

        ProgressOverlay.Visibility = Visibility.Visible;
        ProgressTitle.Text = "Loading Archive";
        ProgressLog.ItemsSource = null;
        ProgressLog.Items.Clear();
        ProgressLog.ItemsSource = loadLogLines;
        ProgressBar.IsIndeterminate = true;
        XuidSearchBox.IsEnabled = false;
        XuidSortBox.IsEnabled = false;
        XuidSortDirBox.IsEnabled = false;

        try
        {
            progress.Report("Reading file...");
            var rawBytes = await Task.Run(() => File.ReadAllBytes(inputPath));
            progress.Report($"Read {rawBytes.Length:N0} bytes");

            progress.Report("Decompressing archive...");
            _wasCompressed = false;
            var rawData = await Task.Run(() => WorldWiperService.MaybeDecompressMsStatic(rawBytes, out _wasCompressed));
            progress.Report(_wasCompressed ? "Archive was compressed — decompressed OK" : "Archive was not compressed");

            progress.Report("Scanning players & counting maps...");
            var players = await Task.Run(() => PlayerDataService.LoadPlayers(rawData, progress));

            progress.Report("Resolving map ownership...");
            await Task.Run(() => ResolveMapOwnership(players, rawData));
            progress.Report("Map ownership resolved");

            _rawArchiveData = rawData;
            _loadedFileName = System.IO.Path.GetFileName(inputPath);
            _allPlayers = players;
            _importedXuids = null;
            _undoStack.Clear();
            _hasChanges = false;
            EnableSaveIfDirty();
            UndoBtn.IsEnabled = false;
            WipeStatusText.Text = "";
            WipeXuidBtn.IsEnabled = false;
            UpdateXuidButtons();
            ApplyPlayerFilterAndSort();
            progress.Report($"Done — loaded {_allPlayers.Count} players from archive");
            Log($"Loaded {_allPlayers.Count} players from archive");
        }
        catch (Exception ex)
        {
            Log($"Failed to load player data: {ex.Message}");
            progress.Report($"Error: {ex.Message}");
        }
        finally
        {
            await Task.Delay(800);
            ProgressOverlay.Visibility = Visibility.Collapsed;
            XuidSearchBox.IsEnabled = true;
            XuidSortBox.IsEnabled = true;
            XuidSortDirBox.IsEnabled = true;
        }
    }

    static void ResolveMapOwnership(List<PlayerData> players, byte[] archiveData)
    {
        var mappings = MapWipeService.ParseLargeMapMappings(archiveData);
        if (mappings.Count > 0)
        {
            var xuidLookup = players.GroupBy(p => p.XUID).ToDictionary(g => g.Key, g => g.First());
            foreach (var kv in mappings)
                if (xuidLookup.TryGetValue(kv.Value, out var player))
                    player.OwnedMapIds.Add(kv.Key);
        }
        foreach (var p in players)
        {
            if (p.OwnedMapIds.Count == 0)
            {
                var seen = new HashSet<int>();
                foreach (var item in p.Inventory.Concat(p.EnderChest))
                    if (item.Id == 358 && seen.Add(item.Damage))
                        p.OwnedMapIds.Add(item.Damage);
            }
            p.MapCount = p.OwnedMapIds.Count;
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
            2 => descending ? query.OrderByDescending(p => p.MapCount) : query.OrderBy(p => p.MapCount),
            _ => descending ? query.OrderByDescending(p => p.TotalItemCount) : query.OrderBy(p => p.TotalItemCount),
        };

        _filteredPlayers = query.ToList();
        XuidListBox.ItemsSource = _filteredPlayers;
        PlayerCountText.Text = $"{_filteredPlayers.Count} players";
    }

    void XuidTab_Loaded(object sender, RoutedEventArgs e)
    {
        if (_allPlayers != null)
            ApplyPlayerFilterAndSort();
    }

    void XuidSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyPlayerFilterAndSort();

    void XuidSort_Changed(object sender, SelectionChangedEventArgs e) => ApplyPlayerFilterAndSort();

    void XuidList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
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
            DetailMapsOwned.Text = player.MapCount.ToString();
            DetailXpTotal.Text = player.XpTotal.ToString("N0");

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

            var maps = player.OwnedMapIds.OrderBy(id => id).ToList();
            DetailOwnedMapsText.Text = maps.Count > 200
                ? string.Join(", ", maps.Take(200).Select(id => $"Map #{id}")) + $"\n… and {maps.Count - 200} more"
                : string.Join(", ", maps.Select(id => $"Map #{id}"));
        }
        catch (Exception ex)
        {
            Log($"Error showing player details: {ex.Message}");
            DetailContent.Visibility = Visibility.Collapsed;
            DetailEmptyText.Visibility = Visibility.Visible;
        }
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
        if (!MsArchive.TryValidate(newData, out var valError))
        {
            Log($"Archive validation failed in RefreshWipeState: {valError}");
            ShowAlert($"Archive corruption detected: {valError}. Operation aborted.");
            return;
        }
        _rawArchiveData = newData;
        var players = await Task.Run(() => PlayerDataService.LoadPlayers(newData));
        try
        {
            await Task.Run(() => ResolveMapOwnership(players, newData));
        }
        catch (Exception ex)
        {
            Log($"ResolveMapOwnership error: {ex.Message}");
        }
        _allPlayers = players;
        _importedXuids = null;
        WipeStatusText.Text = logMessage;
        WipeXuidBtn.IsEnabled = false;
        UpdateXuidButtons();
        ApplyPlayerFilterAndSort();
    }

    async void WipeEmpty_Click(object sender, RoutedEventArgs e)
    {
        if (_rawArchiveData == null || _allPlayers == null)
        {
            ShowAlert("Load a world file first.");
            return;
        }

        var settings = App.CurrentSettings;
        var xpThreshold = settings.WipeEmptyXpLevel;
        var itemThreshold = settings.WipeEmptyItemCount;

        var emptyCount = _allPlayers.Count(p => p.XpLevel <= xpThreshold && p.TotalItemCount <= itemThreshold);
        if (emptyCount == 0)
        {
            ShowAlert("No players matching wipe criteria found.");
            return;
        }
        if (!ConfirmAction($"Remove {emptyCount} player(s) with XP < {xpThreshold} and items <= {itemThreshold}?"))
            return;

        var archive = MsArchive.Parse(_rawArchiveData);
        var emptyFiles = _allPlayers
            .Where(p => p.XpLevel <= xpThreshold && p.TotalItemCount <= itemThreshold)
            .Select(p => $"players\\{p.XUID}.dat")
            .ToHashSet();
        var keepCount = archive.Entries.Count(e => !emptyFiles.Contains(e.Filename));
        var logLines = new ObservableCollection<string>();

        WipeEmptyBtn.IsEnabled = false;
        ProgressTitle.Text = "Removing low level players...";
        ProgressBar.Minimum = 0;
        ProgressBar.Maximum = keepCount;
        ProgressBar.Value = 0;
        ProgressStatus.Text = $"Processing 0/{keepCount} entries...";
        ProgressLog.ItemsSource = null;
        ProgressLog.Items.Clear();
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

            var result = await Task.Run(() => XuidWipeService.WipeEmptyPlayers(
                _rawArchiveData, _allPlayers, xpThreshold, itemThreshold, progress));

            if (result == _rawArchiveData)
            {
                ProgressOverlay.Visibility = Visibility.Collapsed;
                ShowAlert("No players were removed.");
                return;
            }

            RemoveEmptyLog(logLines, emptyFiles);

            ProgressStatus.Text = "Done!";
            logLines.Add($"Removed {emptyCount} low level player(s)");
            logLines.Add("Note: map files for removed players still exist in archive — re-scan Maps tab if needed");

            PushUndo();
            await RefreshWipeState(result, "Low level players removed");
            _hasChanges = true;
            EnableSaveIfDirty();
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
        UpdateXuidButtons();
    }

    async void WipeXuid_Click(object sender, RoutedEventArgs e)
    {
        if (_rawArchiveData == null || _importedXuids == null)
        {
            ShowAlert("Import an XUID list or Authy DB first.");
            return;
        }

        var removeCount = _allPlayers?.Count(p => !_importedXuids.Contains(p.XUID)) ?? 0;
        if (!ConfirmAction($"Remove {removeCount} player(s) not in the imported list?\n\nPlayers in the imported list will be kept."))
            return;

        var allXuids = _allPlayers?.Select(p => p.XUID).ToHashSet() ?? [];
        var keepCount = _allPlayers?.Count(p => _importedXuids.Contains(p.XUID)) ?? 0;
        var logLines = new ObservableCollection<string>();

        WipeXuidBtn.IsEnabled = false;
        ProgressTitle.Text = "Removing players not in imported list...";
        ProgressBar.Minimum = 0;
        ProgressBar.Maximum = keepCount;
        ProgressBar.Value = 0;
        ProgressStatus.Text = $"Processing 0/{keepCount} entries...";
        ProgressLog.ItemsSource = null;
        ProgressLog.Items.Clear();
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
            _hasChanges = true;
            EnableSaveIfDirty();
        }
        catch (Exception ex)
        {
            logLines.Add($"Error: {ex.Message}");
            Log($"WipeXuid error: {ex}");
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
        if (!ConfirmAction($"Delete {selected.Count} selected player(s)?"))
            return;

        PushUndo();
        DeleteSelBtn.IsEnabled = false;
        var logLines = new ObservableCollection<string>();
        ProgressTitle.Text = "Deleting selected players...";
        ProgressBar.Minimum = 0;
        ProgressBar.Maximum = selected.Count;
        ProgressBar.Value = 0;
        ProgressStatus.Text = $"Processing 0/{selected.Count} entries...";
        ProgressLog.ItemsSource = null;
        ProgressLog.Items.Clear();
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

            var newData = await Task.Run(() => XuidWipeService.DeletePlayers(_rawArchiveData, selected, progress));
            var kept = _allPlayers.Count - selected.Count;
            logLines.Add($"Kept {kept} of {_allPlayers.Count} players");
            logLines.Add($"Removed {selected.Count} player(s)");
            await RefreshWipeState(newData, $"Deleted {selected.Count} player(s), {kept} remaining");
            _hasChanges = true;
            EnableSaveIfDirty();
        }
        catch (Exception ex)
        {
            logLines.Add($"Error: {ex.Message}");
            Log($"DeleteSelected error: {ex}");
            await Task.Delay(3000);
        }
        finally
        {
            ProgressOverlay.Visibility = Visibility.Collapsed;
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
        UpdateXuidButtons();
        UndoBtn.IsEnabled = _undoStack.Count > 0;
        _hasChanges = true;
        EnableSaveIfDirty();
        ApplyPlayerFilterAndSort();
    }

    async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (_rawArchiveData == null) return;
        var players = await Task.Run(() => PlayerDataService.LoadPlayers(_rawArchiveData));
        try
        {
            await Task.Run(() => ResolveMapOwnership(players, _rawArchiveData));
        }
        catch (Exception ex)
        {
            Log($"ResolveMapOwnership error: {ex.Message}");
        }
        _allPlayers = players;
        _importedXuids = null;
        WipeStatusText.Text = "Refreshed";
        WipeXuidBtn.IsEnabled = false;
        UpdateXuidButtons();
        ApplyPlayerFilterAndSort();
    }

    async void ProcessBtn_Click(object sender, RoutedEventArgs e)
    {
        var outputPath = OutputPathBox.Text;

        if (_rawArchiveData == null)
        {
            ShowAlert("Load a world file first.");
            return;
        }
        if (string.IsNullOrEmpty(outputPath))
        {
            ShowAlert("Please select an output path.");
            return;
        }

        ProcessBtn.IsEnabled = false;
        CancelBtn.IsEnabled = true;
        StatusText.Text = "Saving...";
        FileStatus.Text = "Saving...";

        ProgressTitle.Text = "Saving...";
        ProgressBar.IsIndeterminate = true;
        ProgressStatus.Text = "Writing file...";
        ProgressOverlay.Visibility = Visibility.Visible;

        try
        {
            var data = _rawArchiveData;
            Log($"Save: archive data is {data.Length:N0} bytes, wasCompressed={_wasCompressed}");
            if (!MsArchive.TryValidate(data, out var archiveError))
            {
                Log($"Archive validation failed: {archiveError}");
                ShowAlert($"Save aborted — archive data is corrupt: {archiveError}");
                return;
            }

            await File.WriteAllBytesAsync(outputPath, data);
            _hasChanges = false;
            EnableSaveIfDirty();
            Log($"Saved to {outputPath}");
            FileStatus.Text = "Saved";
        }
        catch (Exception ex)
        {
            Log($"Error: {ex.Message}");
            FileStatus.Text = "Error";
            ShowAlert($"Error: {ex.Message}");
        }
        finally
        {
            ProgressOverlay.Visibility = Visibility.Collapsed;
            ProcessBtn.IsEnabled = true;
            CancelBtn.IsEnabled = false;
        }
    }

    async void EraseAll_Click(object sender, RoutedEventArgs e)
    {
        if (_rawArchiveData == null || _allPlayers == null)
        {
            ShowAlert("Load a world file first.");
            return;
        }

        if (!ConfirmAction($"Remove all {_allPlayers.Count} players from the archive?\n\nThis will delete every player.dat file. Use Save Changes to write to disk."))
            return;

        PushUndo();
        EraseAllBtn.IsEnabled = false;
        var logLines = new ObservableCollection<string>();
        ProgressTitle.Text = "Removing all players...";
        ProgressBar.Minimum = 0;
        ProgressBar.Maximum = _allPlayers.Count;
        ProgressBar.Value = 0;
        ProgressStatus.Text = $"Processing 0/{_allPlayers.Count} players...";
        ProgressLog.ItemsSource = null;
        ProgressLog.Items.Clear();
        ProgressLog.ItemsSource = logLines;
        ProgressOverlay.Visibility = Visibility.Visible;

        try
        {
            var toRemove = _allPlayers.Select(p => $"players\\{p.XUID}.dat").ToHashSet();
            var archive = MsArchive.Parse(_rawArchiveData);
            var newData = await Task.Run(() => archive.Rebuild(_rawArchiveData, toRemove));
            _hasChanges = true;
            EnableSaveIfDirty();
            logLines.Add($"Removed all {_allPlayers.Count} players");
            await RefreshWipeState(newData, "All players removed");
        }
        catch (Exception ex)
        {
            logLines.Add($"Error: {ex.Message}");
            Log($"EraseAll error: {ex}");
            await Task.Delay(3000);
        }
        finally
        {
            ProgressOverlay.Visibility = Visibility.Collapsed;
            EraseAllBtn.IsEnabled = true;
        }
    }

    async void WipeEntities_Click(object sender, RoutedEventArgs e)
    {
        if (_rawArchiveData == null)
        {
            ShowAlert("Load a world file first.");
            return;
        }

        if (!ConfirmAction("Wipe selected entities from selected dimensions?\n\nThis operation is in-memory until Save Changes."))
            return;

        WipeEntitiesBtn.IsEnabled = false;
        CancelBtn.IsEnabled = true;
        var logLines = new ObservableCollection<string>();
        ProgressTitle.Text = "Wiping entities...";
        ProgressBar.IsIndeterminate = true;
        ProgressStatus.Text = "Processing...";
        ProgressLog.ItemsSource = null;
        ProgressLog.Items.Clear();
        ProgressLog.ItemsSource = logLines;
        ProgressOverlay.Visibility = Visibility.Visible;

        var config = AdvModeToggle.IsChecked == true ? GatherAdvancedConfig() : GatherConfig();
        _cts = new CancellationTokenSource();
        var progress = new Progress<string>(msg =>
        {
            Dispatcher.Invoke(() => logLines.Add(msg));
        });

        try
        {
            var result = await Task.Run(() => _service.ProcessWipeMemory(_rawArchiveData, config, progress), _cts.Token);
            if (result == null)
            {
                logLines.Add("No entities found to remove.");
                await Task.Delay(2000);
                return;
            }
            _rawArchiveData = result;
            if (!MsArchive.TryValidate(_rawArchiveData, out var valError))
            {
                logLines.Add($"Archive validation failed: {valError}");
                Log($"Entity wipe archive validation failed: {valError}");
                _rawArchiveData = result; // still keep in memory for inspection
            }
            _hasChanges = true;
            EnableSaveIfDirty();
            logLines.Add("Entity wipe complete. Use Save Changes to write to disk.");
            ProgressStatus.Text = "Done!";
        }
        catch (OperationCanceledException)
        {
            logLines.Add("Cancelled.");
        }
        catch (Exception ex)
        {
            logLines.Add($"Error: {ex.Message}");
            ShowAlert($"Error: {ex.Message}");
        }
        finally
        {
            ProgressOverlay.Visibility = Visibility.Collapsed;
            WipeEntitiesBtn.IsEnabled = true;
            CancelBtn.IsEnabled = false;
        }
    }
}

