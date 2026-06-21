namespace LegacyEditor.Models;

public class PlayerData
{
    public ulong XUID { get; set; }
    public string Username { get; set; } = "Unknown";
    public double Health { get; set; }
    public int Hunger { get; set; }
    public int XpLevel { get; set; }
    public int XpTotal { get; set; }
    public int Score { get; set; }
    public int TotalItems { get; set; }
    public int TotalItemCount { get; set; }
    public List<ItemStack> Inventory { get; set; } = [];
    public List<ItemStack> Armor { get; set; } = [];
    public List<ItemStack> EnderChest { get; set; } = [];
    public double PosX { get; set; }
    public double PosY { get; set; }
    public double PosZ { get; set; }
}

public class ItemStack
{
    public string Name { get; set; } = "";
    public int Count { get; set; }
    public int Damage { get; set; }
}
