using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LegacyEditor.Models;

namespace LegacyEditor.Controls;

public partial class EntityPicker : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(EntityPicker),
            new PropertyMetadata("Entities"));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    private List<EntityItem> _allItems = [];
    private bool _isExpanded;
    private WipeMode _mode = WipeMode.Whitelist;
    private HashSet<string>? _defaultWhitelistSelection;

    public event Action<HashSet<string>>? SelectionChanged;

    public EntityPicker()
    {
        InitializeComponent();
        HeaderText.Text = Title;
    }

    public void LoadEntities(IEnumerable<EntityInfo> entities, HashSet<string> selectedIds)
    {
        _allItems = entities
            .OrderBy(e => e.DisplayName)
            .Select(e => new EntityItem
            {
                SaveId = e.SaveId,
                DisplayName = e.DisplayName,
                IsSelected = selectedIds.Contains(e.SaveId)
            })
            .ToList();

        _defaultWhitelistSelection = selectedIds;
        EntityListBox.ItemsSource = _allItems;
        SearchBox.Text = "";
        ApplyFilter();
        UpdateTags();
        UpdateCounts();
    }

    public void SetMode(WipeMode mode)
    {
        _mode = mode;
        UpdateCounts();
        UpdateTags();
    }

    public void ResetToDefaults(HashSet<string>? whitelistDefaults = null)
    {
        var whitelist = whitelistDefaults ?? _defaultWhitelistSelection ?? [];
        foreach (var item in _allItems)
            item.IsSelected = _mode == WipeMode.Whitelist
                ? whitelist.Contains(item.SaveId)
                : !whitelist.Contains(item.SaveId);
        EntityListBox.Items.Refresh();
        UpdateTags();
        UpdateCounts();
        SelectionChanged?.Invoke(GetSelectedIds());
    }

    public HashSet<string> GetSelectedIds()
    {
        return [.. _allItems.Where(i => i.IsSelected).Select(i => i.SaveId)];
    }

    void ApplyFilter()
    {
        var filter = SearchBox.Text?.Trim().ToLowerInvariant() ?? "";
        if (string.IsNullOrEmpty(filter))
        {
            EntityListBox.ItemsSource = _allItems;
        }
        else
        {
            EntityListBox.ItemsSource = _allItems
                .Where(i => i.DisplayName.ToLowerInvariant().Contains(filter) ||
                            i.SaveId.ToLowerInvariant().Contains(filter))
                .ToList();
        }
        EntityListBox.Items.Refresh();
    }

    void UpdateTags()
    {
        TagsPanel.Children.Clear();
        var selected = _allItems.Where(i => i.IsSelected).ToList();
        var count = selected.Count;

        foreach (var item in selected.Take(20))
        {
            var tag = CreateTag(item);
            TagsPanel.Children.Add(tag);
        }

        if (count > 20)
        {
            TagsPanel.Children.Add(new TextBlock
            {
                Text = $"+{count - 20} more...",
                Foreground = FindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            });
        }

        if (count == 0)
        {
            var hint = _mode == WipeMode.Whitelist
                ? "Nothing selected — nothing will be wiped"
                : "Nothing selected — everything will be wiped";
            TagsPanel.Children.Add(new TextBlock
            {
                Text = hint,
                Foreground = FindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray,
                FontSize = 12,
                FontStyle = FontStyles.Italic,
                VerticalAlignment = VerticalAlignment.Center
            });
        }
    }

    Border CreateTag(EntityItem item)
    {
        var bg = FindResource("BgHoverBrush") as Brush ?? Brushes.Gray;

        var removeBtn = new TextBlock
        {
            Text = "\u00D7",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = FindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
            Cursor = Cursors.Hand
        };

        removeBtn.MouseDown += (_, _) =>
        {
            item.IsSelected = false;
            UpdateTags();
            UpdateCounts();
            SelectionChanged?.Invoke(GetSelectedIds());
        };

        var border = new Border
        {
            Background = bg,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 1, 4, 1),
            Cursor = Cursors.Hand
        };

        border.MouseDown += (_, _) =>
        {
            item.IsSelected = false;
            UpdateTags();
            UpdateCounts();
            SelectionChanged?.Invoke(GetSelectedIds());
        };

        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(new TextBlock
        {
            Text = item.DisplayName,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        });
        sp.Children.Add(removeBtn);
        border.Child = sp;
        return border;
    }

    void UpdateCounts()
    {
        var total = _allItems.Count;
        var sel = _allItems.Count(i => i.IsSelected);
        var label = _mode == WipeMode.Whitelist ? "to Wipe" : "Protected";
        HeaderText.Text = $"{Title} ({sel} {label})";
        CountText.Text = $"{sel} of {total} selected";
    }

    void ToggleExpand_Click(object sender, RoutedEventArgs e)
    {
        _isExpanded = !_isExpanded;
        CompactView.Visibility = _isExpanded ? Visibility.Collapsed : Visibility.Visible;
        ExpandedView.Visibility = _isExpanded ? Visibility.Visible : Visibility.Collapsed;
        ExpandBtn.Content = _isExpanded ? "Collapse" : "Edit Selection";
        if (_isExpanded)
        {
            SearchBox.Focus();
            ApplyFilter();
        }
    }

    void CompactView_MouseDown(object sender, MouseButtonEventArgs e)
    {
        ToggleExpand_Click(sender, e);
    }

    void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _allItems)
            item.IsSelected = true;
        EntityListBox.Items.Refresh();
        UpdateTags();
        UpdateCounts();
        SelectionChanged?.Invoke(GetSelectedIds());
    }

    void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _allItems)
            item.IsSelected = false;
        EntityListBox.Items.Refresh();
        UpdateTags();
        UpdateCounts();
        SelectionChanged?.Invoke(GetSelectedIds());
    }

    void Default_Click(object sender, RoutedEventArgs e)
    {
        ResetToDefaults(_defaultWhitelistSelection);
    }
}

public class EntityItem : System.ComponentModel.INotifyPropertyChanged
{
    private bool _isSelected;
    public string DisplayName { get; set; } = "";
    public string SaveId { get; set; } = "";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this,
                    new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}
