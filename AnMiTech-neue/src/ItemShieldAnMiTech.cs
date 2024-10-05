using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace tonwexp
{
    public class ItemShieldAnMiTech : Item, IContainedMeshSource
    {
        private Dictionary<int, MultiTextureMeshRef> meshrefs => ObjectCacheUtil.GetOrCreate(this.api, "shieldmeshrefs",
            () => new Dictionary<int, MultiTextureMeshRef>());
        private float offY;
        private float curOffY;
        private Dictionary<string, Dictionary<string, int>> durabilityGains;

        private ICoreClientAPI capi;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            curOffY = offY = this.FpHandTransform.Translation.Y;
            if (api.Side == EnumAppSide.Client)
            {
                this.capi = api as ICoreClientAPI;
            }

            if (this.Attributes == null)
            {
                return;
            }

            this.durabilityGains = this.Attributes["durabilityGains"]?.AsObject<Dictionary<string, Dictionary<string, int>>>();
            if (this.durabilityGains == null)
            {
                return;
            }

            this.AddAllTypesToCreativeInventory();
        }

        public override int GetMaxDurability(ItemStack itemstack)
        {
            int gain = 0;

            string wood = itemstack.Attributes.GetString("wood", null);
            string metal = itemstack.Attributes.GetString("metal", null);

            if (wood != null && this.durabilityGains["wood"].TryGetValue(wood, out int woodGain))
            {
                gain += woodGain;
            }

            if (metal != null && this.durabilityGains["metal"].TryGetValue(metal, out int metalGain))
            {
                gain += metalGain;
            }

            return base.GetMaxDurability(itemstack) + gain;
        }

        public void AddAllTypesToCreativeInventory()
        {
            List<JsonItemStack> stacks = new List<JsonItemStack>();

            Dictionary<string, string[]> variantGroups = this.Attributes["variantGroups"]?.AsObject<Dictionary<string, string[]>>();
            if (variantGroups == null)
            {
                return;
            }

            foreach (string metal in variantGroups["metal"])
            {
                foreach (string wood in variantGroups["wood"])
                {
                    foreach (string color in variantGroups["color"])
                    {
                        JsonItemStack stack = this.genJstack($"{{ wood: \"{wood}\", metal: \"{metal}\", color: \"{color}\" }}");

                        if (stack != null && stack.Attributes != null)
                        {
                            stacks.Add(stack);
                        }
                    }
                }
            }

            if (stacks.Count > 0)
            {
                this.CreativeInventoryStacks = new CreativeTabAndStackList[]
                {
                    new CreativeTabAndStackList
                    {
                        Stacks = stacks.ToArray(),
                        Tabs = new string[] { "tonwexp", "general", "items", "tools" }
                    }
                };
            }
        }

        private JsonItemStack genJstack(string json)
        {
            JsonObject attributes = new JsonObject(JToken.Parse(json));

            AssetLocation code = this.Code;

            JsonItemStack jsonItemStack = new JsonItemStack
            {
                Code = code,
                Type = EnumItemClass.Item,
                Attributes = attributes
            };

            if (!jsonItemStack.Resolve(this.api.World, "shield generation", false))
            {
                return null;
            }

            return jsonItemStack;
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (capi == null)
            {
                return;
            }
            if (target == EnumItemRenderTarget.HandFp)
            {
                bool sneak = capi.World.Player.Entity.Controls.Sneak;
                this.curOffY += ((sneak ? 0.4f : this.offY) - this.curOffY) * renderinfo.dt * 8f;
                renderinfo.Transform.Translation.X = this.curOffY;
                renderinfo.Transform.Translation.Y = this.curOffY * 1.2f;
                renderinfo.Transform.Translation.Z = this.curOffY * 1.2f;
            }
            int meshrefid = itemstack.TempAttributes.GetInt("meshRefId", 0);
            if (meshrefid == 0 || !this.meshrefs.TryGetValue(meshrefid, out renderinfo.ModelRef))
            {
                int id = this.meshrefs.Count + 1;
                MultiTextureMeshRef modelref = capi.Render.UploadMultiTextureMesh(this.GenMesh(itemstack, capi.ItemTextureAtlas));
                renderinfo.ModelRef = (this.meshrefs[id] = modelref);
                itemstack.TempAttributes.SetInt("meshRefId", id);
            }

            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }
        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            string onhand = (byEntity.LeftHandItemSlot == slot) ? "left" : "right";
            string notonhand = (byEntity.LeftHandItemSlot == slot) ? "right" : "left";
            if (byEntity.Controls.Sneak)
            {
                if (!byEntity.AnimManager.IsAnimationActive("raiseshield-" + onhand))
                {
                    byEntity.AnimManager.StartAnimation("raiseshield-" + onhand);
                }
            }
            else if (byEntity.AnimManager.IsAnimationActive("raiseshield-" + onhand))
            {
                byEntity.AnimManager.StopAnimation("raiseshield-" + onhand);
            }
            if (byEntity.AnimManager.IsAnimationActive("raiseshield-" + notonhand))
            {
                byEntity.AnimManager.StopAnimation("raiseshield-" + notonhand);
            }

            base.OnHeldIdle(slot, byEntity);
        }
        public MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas)
        {
            if (this.capi == null)
            {
                throw new Exception("Client API (capi) is not initialized.");
            }

            var cnts = new ContainedTextureSource(this.capi, targetAtlas, new Dictionary<string, AssetLocation>(), $"For render in shield {this.Code}");
            cnts.Textures.Clear();

            var texturePaths = this.Attributes["texturePaths"].AsObject<Dictionary<string, string>>();
            foreach (var textureType in new[] { "wood", "metal", "color" })
            {
                string material = itemstack.Attributes.GetString(textureType, null);
                if (!string.IsNullOrEmpty(material) && texturePaths.TryGetValue(textureType, out var path))
                {
                    cnts.Textures[textureType] = new AssetLocation($"{path}/{material}.png");
                }
                else
                {
                    cnts.Textures[textureType] = new AssetLocation($"{texturePaths[textureType]}/default.png");
                }
            }

            MeshData mesh;
            try
            {
                this.capi.Tesselator.TesselateItem(this, out mesh, cnts);
            }
            catch (Exception)
            {
                throw;
            }

            return mesh;
        }

        public MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos atBlockPos) => this.GenMesh(itemstack, targetAtlas);

        public override string GetHeldItemName(ItemStack itemStack)
        {
            string metal = itemStack.Attributes.GetString("metal", null);
            string wood = itemStack.Attributes.GetString("wood", null);

            string metalName = metal != null ? Lang.Get("material-" + metal) : Lang.Get("Unknown Material");
            string woodName = wood != null ? Lang.Get("material-" + wood) : Lang.Get("Unknown Material");

            string itemType = this.Code.Path;

            if (!string.IsNullOrEmpty(itemType))
            {
                itemType = char.ToUpper(itemType[0]) + itemType.Substring(1);
            }

            return Lang.Get("{0} {1} {2}", metalName, woodName, itemType);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            ItemStack itemstack = inSlot.Itemstack;

            float acdmgabsorb = GetDamageAbsorptionAnmitech(itemstack, true);
            float acchance = GetProtectionChanceAnmitech(itemstack, true);
            float padmgabsorb = GetDamageAbsorptionAnmitech(itemstack, false);
            float pachance = GetProtectionChanceAnmitech(itemstack, false);

            dsc.AppendLine(Lang.Get("shield-stats", (int)(100f * acchance), (int)(100f * pachance), acdmgabsorb, padmgabsorb));

            string wood = itemstack.Attributes.GetString("wood", null);
            string metal = itemstack.Attributes.GetString("metal", null);

            if (!string.IsNullOrEmpty(wood))
            {
                dsc.AppendLine(Lang.Get("shield-woodtype", Lang.Get("material-" + wood)));
            }

            if (!string.IsNullOrEmpty(metal))
            {
                dsc.AppendLine(Lang.Get("shield-metaltype", Lang.Get("material-" + metal)));
            }
        }

        public string GetMeshCacheKey(ItemStack itemstack)
        {
            string wood = itemstack.Attributes.GetString("wood", null);
            string metal = itemstack.Attributes.GetString("metal", null);
            string color = itemstack.Attributes.GetString("color", null);
            return $"{this.Code.ToShortString()}-{wood}-{metal}-{color}";
        }

        public float GetProtectionChanceAnmitech(ItemStack itemstack, bool isActive)
        {
            string wood = itemstack.Attributes.GetString("wood", null);
            string metal = itemstack.Attributes.GetString("metal", null);

            float chance = 0f;
            JsonObject shieldStats = this.Attributes["shield"];

            if (shieldStats != null)
            {
                if (!string.IsNullOrEmpty(wood))
                {
                    JsonObject woodStats = shieldStats["shieldStatsByMaterial"]["wood"][wood];
                    if (woodStats != null)
                    {
                        chance += woodStats["protectionChance"][isActive ? "active" : "passive"].AsFloat(0f);
                    }
                }

                if (!string.IsNullOrEmpty(metal))
                {
                    JsonObject metalStats = shieldStats["shieldStatsByMaterial"]["metal"][metal];
                    if (metalStats != null)
                    {
                        chance += metalStats["protectionChance"][isActive ? "active" : "passive"].AsFloat(0f);
                    }
                }

                chance = Math.Min(1f, chance);
            }

            return chance;
        }

        public float GetDamageAbsorptionAnmitech(ItemStack itemstack, bool isActive)
        {
            string wood = itemstack.Attributes.GetString("wood", null);
            string metal = itemstack.Attributes.GetString("metal", null);

            float absorption = 0f;
            JsonObject shieldStats = this.Attributes["shield"];

            if (shieldStats != null)
            {
                if (!string.IsNullOrEmpty(wood))
                {
                    JsonObject woodStats = shieldStats["shieldStatsByMaterial"]["wood"][wood];
                    if (woodStats != null)
                    {
                        absorption += woodStats["damageAbsorption"][isActive ? "active" : "passive"].AsFloat(0f);
                    }
                }

                if (!string.IsNullOrEmpty(metal))
                {
                    JsonObject metalStats = shieldStats["shieldStatsByMaterial"]["metal"][metal];
                    if (metalStats != null)
                    {
                        absorption += metalStats["damageAbsorption"][isActive ? "active" : "passive"].AsFloat(0f);
                    }
                }
            }

            return absorption;
        }
        public override void OnCreatedByCrafting(ItemSlot[] allInputSlots, ItemSlot outputSlot, GridRecipe byRecipe)
        {
            base.OnCreatedByCrafting(allInputSlots, outputSlot, byRecipe);
            AssignCustomShieldAttributes(outputSlot.Itemstack);
        }

        private void AssignCustomShieldAttributes(ItemStack itemstack)
        {
            string wood = itemstack.Attributes.GetString("wood");
            string metal = itemstack.Attributes.GetString("metal");


            float acdmgabsorb = GetDamageAbsorptionAnmitech(itemstack, true);
            float acchance = GetProtectionChanceAnmitech(itemstack, true);
            float padmgabsorb = GetDamageAbsorptionAnmitech(itemstack, false);
            float pachance = GetProtectionChanceAnmitech(itemstack, false);
            
            itemstack.Attributes.SetFloat("customDamageAbsorptionActive", acdmgabsorb);
            itemstack.Attributes.SetFloat("customProtectionChanceActive", acchance);
            itemstack.Attributes.SetFloat("customDamageAbsorptionPassive", padmgabsorb);
            itemstack.Attributes.SetFloat("customProtectionChancePassive", pachance);
        }
    }
}
