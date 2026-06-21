namespace LegacyEditor.Models;

public enum EntityCategory
{
    Entity,
    TileEntity
}

public class EntityInfo
{
    public string SaveId { get; }
    public string DisplayName { get; }
    public EntityCategory Category { get; }
    public string? Description { get; init; }

    public EntityInfo(string saveId, string displayName, EntityCategory category)
    {
        SaveId = saveId;
        DisplayName = displayName;
        Category = category;
    }

    public override string ToString() => DisplayName;
}
