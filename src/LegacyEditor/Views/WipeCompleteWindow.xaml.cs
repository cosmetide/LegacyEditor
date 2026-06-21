using System.Windows;
using LegacyEditor.Models;
using LegacyEditor.Services;

namespace LegacyEditor.Views;

public partial class WipeCompleteWindow : Window
{
    public WipeCompleteWindow(WipeSummary summary, Window? owner = null)
    {
        InitializeComponent();
        Owner = owner;
        TotalRemovedText.Text = summary.TotalRemoved.ToString("N0");
        ChunksScannedText.Text = $"Chunks scanned:  {summary.TotalChunks:N0}";
        if (summary.ReadFailures > 0)
            ChunksScannedText.Text += $"  |  Failures: {summary.ReadFailures}";

        int overworld = 0, nether = 0, end = 0;
        var byType = new Dictionary<string, int>();
        foreach (var r in summary.Removed)
        {
            byType[r.EntityId] = byType.GetValueOrDefault(r.EntityId) + 1;
            var rf = r.RegionFile.Replace("/", "\\");
            if (rf.Contains("\\DIM-1\\")) nether++;
            else if (rf.Contains("\\DIM1\\")) end++;
            else overworld++;
        }

        DimOverworld.Text = overworld > 0 ? $"Overworld:  {overworld:N0}" : "";
        DimNether.Text = nether > 0 ? $"Nether:     {nether:N0}" : "";
        DimEnd.Text = end > 0 ? $"End:        {end:N0}" : "";

        var top = byType.OrderByDescending(kv => kv.Value).Take(15)
            .Select(kv => new { Name = EntityRegistry.GetDisplayName(kv.Key), Count = kv.Value.ToString("N0") })
            .ToList();
        TopEntitiesList.ItemsSource = top;
    }

    void CloseBtn_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    void OkBtn_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
