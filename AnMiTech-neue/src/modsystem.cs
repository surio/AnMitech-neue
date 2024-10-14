using System.Reflection;          // Importing the 'Reflection' namespace to allow inspection and modification of types and assemblies at runtime.
using HarmonyLib;                 // Importing the 'Harmony' library, which enables patching and modifying methods at runtime in a non-destructive way.
using Vintagestory.API.Common;    // Importing common Vintage Story API elements that are used both on the client and server sides.

namespace tonwexp                      // Declaring a namespace to encapsulate the mod's classes and logic, keeping them organized and distinct from other mods or core game code.
{
    // This class represents a mod system for adding and modifying shields in the game. It extends the base 'ModSystem' class.
    public class ShieldModSystem : ModSystem
    {
        private Harmony harmony;  // A field that holds an instance of Harmony, used for patching the game's methods at runtime.

        // This method is called when the mod starts. It registers the custom shield item and applies Harmony patches.
        public override void Start(ICoreAPI api)
        {
            // Calls the base 'ModSystem' start method, which ensures any core startup logic is executed.
            base.Start(api);

            // Registers the custom item class 'ItemShieldAnMiTech' with the game. This allows the game to recognize this custom item as a new shield type.
            api.RegisterItemClass("ItemShieldAnMiTech", typeof(ItemShieldAnMiTech));

            // Initializes a new Harmony instance with a unique ID ("tonwexp.shieldpatch") for this mod's patches.
            harmony = new Harmony("tonwexp.shieldpatch");

            // Applies all Harmony patches found in the current assembly (i.e., the mod's code). This will modify or extend the behavior of the game.
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        // Summary:
        // This method is the entry point for the mod system. It registers a new custom shield item with the game using `api.RegisterItemClass` 
        // and applies all patches using Harmony. Harmony is used to modify existing methods in the game to customize the behavior of the shields 
        // without directly editing game source code, making it flexible and maintainable. The Start method is crucial for initializing the mod and 
        // injecting its logic into the game.

        // This method is called when the mod system is being disposed of or unloaded from the game. It removes any applied Harmony patches.
        public override void Dispose()
        {
            // If the Harmony instance exists, it unpatches all methods that were patched by this mod's Harmony ID ("tonwexp.shieldpatch").
            harmony?.UnpatchAll("tonwexp.shieldpatch");
        }

        // Summary:
        // This method is responsible for cleaning up when the mod system is disposed of, such as when the game shuts down or the mod is unloaded.
        // It uses Harmony's `UnpatchAll` method to remove all patches applied by this mod, identified by the "tonwexp.shieldpatch" ID.
        // Properly unpatching ensures that no changes persist after the mod is removed, preventing potential conflicts or unwanted behavior in the game.
    }
}
