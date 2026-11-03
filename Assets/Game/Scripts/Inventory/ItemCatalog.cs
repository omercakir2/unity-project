using UnityEngine;

/// <summary>
/// Item names, icons, and crafting recipes. Icons come from Resources/ItemIcons.
/// </summary>
public static class ItemCatalog
{
    private const string IconSetPath = "ItemIcons";

    private struct Recipe
    {
        public string a;
        public string b;
        public string result;
    }

    private static readonly Recipe[] Recipes =
    {
        new Recipe { a = PlayerInventory.WoodStickItemId, b = PlayerInventory.FabricItemId, result = PlayerInventory.TorchItemId }
    };

    private static ItemIconSet icons;

    public static string DisplayName(string itemId)
    {
        switch (itemId)
        {
            case PlayerInventory.TorchItemId: return "Fire Torch";
            case PlayerInventory.SearchlightItemId: return "Light Ball";
            case PlayerInventory.PistolItemId: return "Desert Eagle";
            case PlayerInventory.WoodStickItemId: return "Wood Stick";
            case PlayerInventory.FabricItemId: return "Fabric";
            default: return null;
        }
    }

    // Crafting materials never go on the hotbar.
    public static bool IsBagOnly(string itemId)
    {
        return itemId == PlayerInventory.WoodStickItemId || itemId == PlayerInventory.FabricItemId;
    }

    public static bool TryCombine(string first, string second, out string result)
    {
        result = null;
        if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second))
            return false;

        for (int i = 0; i < Recipes.Length; i++)
        {
            Recipe recipe = Recipes[i];
            if ((recipe.a == first && recipe.b == second) || (recipe.a == second && recipe.b == first))
            {
                result = recipe.result;
                return true;
            }
        }

        return false;
    }

    public static string RecipeHint()
    {
        if (Recipes.Length == 0)
            return null;

        Recipe recipe = Recipes[0];
        return DisplayName(recipe.a) + " + " + DisplayName(recipe.b) + " = " + DisplayName(recipe.result);
    }

    public static Sprite Icon(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return null;

        if (icons == null)
        {
            icons = Resources.Load<ItemIconSet>(IconSetPath);
            if (icons == null)
            {
                Debug.LogWarning("[Inventory] Item icon set is missing at Resources/" + IconSetPath + ".");
                return null;
            }
        }

        return icons.Find(itemId);
    }
}
