using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;

namespace tonwexp
{
    public class ShieldModSystem : ModSystem
    {
        private Harmony harmony;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterItemClass("ItemShieldAnMiTech", typeof(ItemShieldAnMiTech));
            harmony = new Harmony("tonwexp.shieldpatch");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll("tonwexp.shieldpatch");
        }
    }
}