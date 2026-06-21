using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace LegacyEditor.Views;

public partial class WelcomeWindow : Window
{
    public WelcomeWindow()
    {
        InitializeComponent();
        AllowDrop = true;
        Loaded += (_, _) => LoadRecentFiles();
    }

    void LoadRecentFiles()
    {
        var recent = MainWindow.RecentFiles();
        if (recent.Count == 0) return;

        RecentPanel.Visibility = Visibility.Visible;
        foreach (var path in recent)
        {
            var name = System.IO.Path.GetFileName(path);
            var container = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 3, 6, 3),
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 1, 0, 1),
                Tag = path
            };
            container.MouseDown += (s, _) =>
            {
                if (s is Border b && b.Tag is string p)
                    OpenEditor(p);
            };
            container.Child = new TextBlock
            {
                Text = name,
                FontSize = 12,
                Foreground = FindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray,
                TextTrimming = System.Windows.TextTrimming.CharacterEllipsis
            };
            RecentList.Items.Add(container);
        }
    }

    void RecentList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is Border b && b.Tag is string path)
            OpenEditor(path);
    }

    void BrowseBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select .ms world file",
            Filter = "MS World files (*.ms)|*.ms|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
            OpenEditor(dlg.FileName);
    }

    void DropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0 && files[0].EndsWith(".ms", StringComparison.OrdinalIgnoreCase))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
                DropHint.Text = files[0];
                return;
            }
        }
        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    void DropZone_DragLeave(object sender, DragEventArgs e)
    {
        DropHint.Text = "or click Browse below";
    }

    void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0 && files[0].EndsWith(".ms", StringComparison.OrdinalIgnoreCase))
                OpenEditor(files[0]);
        }
    }

    void OpenEditor(string path)
    {
        try
        {
            var editor = new MainWindow(path);
            Application.Current.MainWindow = editor;
            editor.Show();
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error opening editor:\n{ex.Message}\n\n{ex.StackTrace}",
                "LegacyEditor", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
