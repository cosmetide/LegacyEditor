using System.IO;

namespace LegacyEditor.Models;

public class WipeResult
{
    public string RegionFile { get; set; } = "";
    public int ChunkX { get; set; }
    public int ChunkZ { get; set; }
    public string ListKey { get; init; } = "";
    public string EntityId { get; init; } = "";
    public double? PosX { get; init; }
    public double? PosY { get; init; }
    public double? PosZ { get; init; }
}
