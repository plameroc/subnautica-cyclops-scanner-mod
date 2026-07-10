using Nautilus.Assets;
using Nautilus.Assets.Gadgets;
using Nautilus.Assets.PrefabTemplates;
using Nautilus.Crafting;
using Nautilus.Handlers;

namespace CyclopsScannerModule.Items;

public static class ScannerModuleItem
{
    public static PrefabInfo Info { get; private set; }

    public static void Register()
    {
        Info = PrefabInfo.WithTechType("CyclopsScannerModule",
                "Cyclops Scanner Module",
                "Long-range resource scanner for the Cyclops. Select a resource to track; blips appear on your HUD while wearing a Scanner Room HUD Chip. Consumes 12 energy per minute while scanning.",
                unlockAtStart: false)
            .WithIcon(SpriteManager.Get(TechType.MapRoomHUDChip))
            .WithSizeInInventory(new Vector2int(1, 1));

        var prefab = new CustomPrefab(Info);
        prefab.SetGameObject(new CloneTemplate(Info, TechType.CyclopsThermalReactorModule));
        prefab.SetRecipe(new RecipeData(
            new Ingredient(TechType.ComputerChip, 1),
            new Ingredient(TechType.Magnetite, 2),
            new Ingredient(TechType.CopperWire, 1)));
        prefab.SetEquipment(EquipmentType.CyclopsModule);
        prefab.SetPdaGroupCategory(TechGroup.Cyclops, TechCategory.CyclopsUpgrades);
        prefab.Register();

        CraftTreeHandler.AddCraftingNode(CraftTree.Type.CyclopsFabricator, Info.TechType);
        KnownTechHandler.AddRequirementForUnlock(Info.TechType, TechType.BaseMapRoom);
    }
}
