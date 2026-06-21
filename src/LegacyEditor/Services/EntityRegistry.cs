using LegacyEditor.Models;

namespace LegacyEditor.Services;

public static class EntityRegistry
{
    public static readonly Dictionary<string, EntityInfo> All = new()
    {
        // === Entities ===
        ["Item"] = new("Item", "Item", EntityCategory.Entity),
        ["XPOrb"] = new("XPOrb", "XP Orb", EntityCategory.Entity),
        ["LeashKnot"] = new("LeashKnot", "Leash Knot", EntityCategory.Entity),
        ["Painting"] = new("Painting", "Painting", EntityCategory.Entity),
        ["Arrow"] = new("Arrow", "Arrow", EntityCategory.Entity),
        ["Snowball"] = new("Snowball", "Snowball", EntityCategory.Entity),
        ["Fireball"] = new("Fireball", "Fireball", EntityCategory.Entity),
        ["SmallFireball"] = new("SmallFireball", "Small Fireball", EntityCategory.Entity),
        ["ThrownEnderpearl"] = new("ThrownEnderpearl", "Ender Pearl", EntityCategory.Entity),
        ["EyeOfEnderSignal"] = new("EyeOfEnderSignal", "Eye of Ender", EntityCategory.Entity),
        ["ThrownPotion"] = new("ThrownPotion", "Potion", EntityCategory.Entity),
        ["ThrownExpBottle"] = new("ThrownExpBottle", "XP Bottle", EntityCategory.Entity),
        ["ItemFrame"] = new("ItemFrame", "Item Frame", EntityCategory.Entity),
        ["WitherSkull"] = new("WitherSkull", "Wither Skull", EntityCategory.Entity),
        ["PrimedTnt"] = new("PrimedTnt", "Primed TNT", EntityCategory.Entity),
        ["FallingSand"] = new("FallingSand", "Falling Block", EntityCategory.Entity),
        ["FireworksRocketEntity"] = new("FireworksRocketEntity", "Firework Rocket", EntityCategory.Entity),
        ["Boat"] = new("Boat", "Boat", EntityCategory.Entity),
        ["MinecartRideable"] = new("MinecartRideable", "Minecart", EntityCategory.Entity),
        ["MinecartChest"] = new("MinecartChest", "Chest Minecart", EntityCategory.Entity),
        ["MinecartFurnace"] = new("MinecartFurnace", "Furnace Minecart", EntityCategory.Entity),
        ["MinecartTNT"] = new("MinecartTNT", "TNT Minecart", EntityCategory.Entity),
        ["MinecartHopper"] = new("MinecartHopper", "Hopper Minecart", EntityCategory.Entity),
        ["MinecartSpawner"] = new("MinecartSpawner", "Spawner Minecart", EntityCategory.Entity),
        ["Creeper"] = new("Creeper", "Creeper", EntityCategory.Entity),
        ["Skeleton"] = new("Skeleton", "Skeleton", EntityCategory.Entity),
        ["Spider"] = new("Spider", "Spider", EntityCategory.Entity),
        ["Giant"] = new("Giant", "Giant Zombie", EntityCategory.Entity),
        ["Zombie"] = new("Zombie", "Zombie", EntityCategory.Entity),
        ["Slime"] = new("Slime", "Slime", EntityCategory.Entity),
        ["Ghast"] = new("Ghast", "Ghast", EntityCategory.Entity),
        ["PigZombie"] = new("PigZombie", "Zombie Pigman", EntityCategory.Entity),
        ["Enderman"] = new("Enderman", "Enderman", EntityCategory.Entity),
        ["CaveSpider"] = new("CaveSpider", "Cave Spider", EntityCategory.Entity),
        ["Silverfish"] = new("Silverfish", "Silverfish", EntityCategory.Entity),
        ["Blaze"] = new("Blaze", "Blaze", EntityCategory.Entity),
        ["LavaSlime"] = new("LavaSlime", "Magma Cube", EntityCategory.Entity),
        ["EnderDragon"] = new("EnderDragon", "Ender Dragon", EntityCategory.Entity),
        ["WitherBoss"] = new("WitherBoss", "Wither", EntityCategory.Entity),
        ["Bat"] = new("Bat", "Bat", EntityCategory.Entity),
        ["Witch"] = new("Witch", "Witch", EntityCategory.Entity),
        ["Endermite"] = new("Endermite", "Endermite", EntityCategory.Entity),
        ["Guardian"] = new("Guardian", "Guardian", EntityCategory.Entity),
        ["ElderGuardian"] = new("ElderGuardian", "Elder Guardian", EntityCategory.Entity),
        ["Pig"] = new("Pig", "Pig", EntityCategory.Entity),
        ["Sheep"] = new("Sheep", "Sheep", EntityCategory.Entity),
        ["Cow"] = new("Cow", "Cow", EntityCategory.Entity),
        ["Chicken"] = new("Chicken", "Chicken", EntityCategory.Entity),
        ["Squid"] = new("Squid", "Squid", EntityCategory.Entity),
        ["Wolf"] = new("Wolf", "Wolf", EntityCategory.Entity),
        ["MushroomCow"] = new("MushroomCow", "Mooshroom", EntityCategory.Entity),
        ["SnowMan"] = new("SnowMan", "Snow Golem", EntityCategory.Entity),
        ["Ozelot"] = new("Ozelot", "Ocelot", EntityCategory.Entity),
        ["VillagerGolem"] = new("VillagerGolem", "Iron Golem", EntityCategory.Entity),
        ["EntityHorse"] = new("EntityHorse", "Horse", EntityCategory.Entity),
        ["Rabbit"] = new("Rabbit", "Rabbit", EntityCategory.Entity),
        ["ArmorStand"] = new("ArmorStand", "Armor Stand", EntityCategory.Entity),
        ["Villager"] = new("Villager", "Villager", EntityCategory.Entity),
        ["EnderCrystal"] = new("EnderCrystal", "End Crystal", EntityCategory.Entity),
        ["DragonFireball"] = new("DragonFireball", "Dragon Fireball", EntityCategory.Entity),

        // === Tile Entities ===
        ["Furnace"] = new("Furnace", "Furnace", EntityCategory.TileEntity),
        ["Chest"] = new("Chest", "Chest", EntityCategory.TileEntity),
        ["EnderChest"] = new("EnderChest", "Ender Chest", EntityCategory.TileEntity),
        ["RecordPlayer"] = new("RecordPlayer", "Jukebox", EntityCategory.TileEntity),
        ["Trap"] = new("Trap", "Dispenser", EntityCategory.TileEntity),
        ["Dropper"] = new("Dropper", "Dropper", EntityCategory.TileEntity),
        ["Sign"] = new("Sign", "Sign", EntityCategory.TileEntity),
        ["MobSpawner"] = new("MobSpawner", "Spawner", EntityCategory.TileEntity),
        ["Music"] = new("Music", "Note Block", EntityCategory.TileEntity),
        ["Piston"] = new("Piston", "Piston", EntityCategory.TileEntity),
        ["Cauldron"] = new("Cauldron", "Brewing Stand", EntityCategory.TileEntity),
        ["EnchantTable"] = new("EnchantTable", "Enchantment Table", EntityCategory.TileEntity),
        ["Airportal"] = new("Airportal", "End Portal", EntityCategory.TileEntity),
        ["Control"] = new("Control", "Command Block", EntityCategory.TileEntity),
        ["Beacon"] = new("Beacon", "Beacon", EntityCategory.TileEntity),
        ["Skull"] = new("Skull", "Skull", EntityCategory.TileEntity),
        ["DLDetector"] = new("DLDetector", "Daylight Detector", EntityCategory.TileEntity),
        ["Hopper"] = new("Hopper", "Hopper", EntityCategory.TileEntity),
        ["Comparator"] = new("Comparator", "Redstone Comparator", EntityCategory.TileEntity),
    };

    public static List<EntityInfo> Entities => All.Values.Where(e => e.Category == EntityCategory.Entity).ToList();
    public static List<EntityInfo> TileEntities => All.Values.Where(e => e.Category == EntityCategory.TileEntity).ToList();
    public static List<EntityInfo> AllEntities => All.Values.ToList();

    public static string GetDisplayName(string saveId) =>
        All.TryGetValue(saveId, out var info) ? info.DisplayName : saveId;
}
