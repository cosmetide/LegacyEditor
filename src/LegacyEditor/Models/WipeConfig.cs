namespace LegacyEditor.Models;

public enum WipeMode
{
    Whitelist,
    Blacklist
}

public class WipeConfig
{
    public HashSet<string> EntitiesToWipe { get; set; } = [];
    public bool WipeOverworld { get; set; } = true;
    public bool WipeNether { get; set; } = true;
    public bool WipeEnd { get; set; } = true;
    public bool AdvancedMode { get; set; }
    public WipeMode Mode { get; set; } = WipeMode.Whitelist;
    public string? InputPath { get; set; }
    public string? OutputPath { get; set; }
}
