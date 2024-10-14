using System;                        // Provides core functionalities such as basic data types and exception handling.
using System.Collections.Generic;    // Contains types for managing collections, such as List and Dictionary.
using System.Text;                   // Provides classes to handle text manipulation and string building.
using Newtonsoft.Json.Linq;          // External library for parsing and handling JSON data.
using Vintagestory.API.Client;       // Contains classes and interfaces for client-side operations in Vintage Story.
using Vintagestory.API.Common;       // Contains shared classes and interfaces used across both client and server sides.
using Vintagestory.API.Config;       // Provides access to game configuration settings.
using Vintagestory.API.Datastructures;// Provides advanced data structures to manage game data.
using Vintagestory.API.MathTools;    // Contains mathematical tools, such as vectors, matrices, and transformations.
using Vintagestory.API.Util;         // Provides various utility functions and helpers for the API.
using Vintagestory.GameContent;      // Provides core game content, allowing you to extend and modify base gameplay.

namespace tonwexp                      // Defines the namespace for grouping related classes.
{
    // Defines a custom shield item class that extends the base 'Item' class and implements 'IContainedMeshSource' for 3D mesh handling.
    public class ItemShieldAnMiTech : Item, IContainedMeshSource
    {
        // A dictionary that caches 3D mesh references, optimizing rendering by avoiding recalculating meshes.
        // This leverages the ObjectCacheUtil, which caches objects globally for performance reasons.
        private Dictionary<int, MultiTextureMeshRef> meshrefs => ObjectCacheUtil.GetOrCreate(this.api, "shieldmeshrefs",
            () => new Dictionary<int, MultiTextureMeshRef>());
        
        private float offY;     // Stores the initial vertical (Y-axis) offset of the shield when held.
        private float curOffY;  // Stores the current Y-offset of the shield, which may change based on player actions like sneaking.
        private Dictionary<string, Dictionary<string, int>> durabilityGains;  // A dictionary that stores the durability increases based on materials used (e.g., wood and metal types).

        private ICoreClientAPI capi;  // A reference to the client-side API, used for handling client-specific operations like rendering.

        // Called when the shield item is loaded into the game. Initializes various attributes.
        public override void OnLoaded(ICoreAPI api)
        {
            // Calls the base class method to perform any necessary base initialization.
            base.OnLoaded(api);

            // Sets the initial and current Y offset based on the shield's hand transformation.
            curOffY = offY = this.FpHandTransform.Translation.Y;

            // If the game is running on the client side (as opposed to the server), store the client API reference.
            if (api.Side == EnumAppSide.Client)
            {
                this.capi = api as ICoreClientAPI;  // Cast the API to the client-specific API.
            }

            // If there are no attributes (such as durability gains) associated with the item, exit early.
            if (this.Attributes == null)
            {
                return;
            }

            // Attempt to load durability gains from the item's JSON attributes. This maps material types to durability bonuses.
            this.durabilityGains = this.Attributes["durabilityGains"]?.AsObject<Dictionary<string, Dictionary<string, int>>>();
            if (this.durabilityGains == null) // If no durability gain data is found, exit the method.
            {
                return;
            }

            // Adds all material combinations (e.g., wood, metal, color variants) to the creative inventory.
            this.AddAllTypesToCreativeInventory();
        }

        // Summary:
        // This method is responsible for initializing the shield when it's loaded into the game. It sets up attributes like the current Y offset
        // for the shield (for rendering), initializes client-specific APIs, and loads configuration attributes like durability gains from JSON.
        // If successful, it populates the creative inventory with all shield variants (combinations of materials like wood and metal).

        // Calculates the maximum durability of the shield based on its materials.
        public override int GetMaxDurability(ItemStack itemstack)
        {
            int gain = 0;  // Initializes a gain variable that will accumulate the additional durability based on materials.

            // Retrieves the wood and metal material types from the item's attributes.
            string wood = itemstack.Attributes.GetString("wood", null);
            string metal = itemstack.Attributes.GetString("metal", null);

            // If the wood type is defined and its durability gain is found in the dictionary, add it to the gain.
            if (wood != null && this.durabilityGains["wood"].TryGetValue(wood, out int woodGain))
            {
                gain += woodGain;  // Add the wood durability gain to the total gain.
            }

            // If the metal type is defined and its durability gain is found in the dictionary, add it to the gain.
            if (metal != null && this.durabilityGains["metal"].TryGetValue(metal, out int metalGain))
            {
                gain += metalGain;  // Add the metal durability gain to the total gain.
            }

            // Returns the base durability of the shield (as defined by the base 'Item' class) plus any additional gains from materials.
            return base.GetMaxDurability(itemstack) + gain;
        }

        // Summary:
        // This method calculates the maximum durability of the shield based on its constituent materials (wood and metal).
        // The durability gains for each material type are looked up in a dictionary, and the final durability is the sum of
        // the base durability and the additional material-specific gains.

        // Populates the creative inventory with all possible combinations of the shield's material variants.
        public void AddAllTypesToCreativeInventory()
        {
            List<JsonItemStack> stacks = new List<JsonItemStack>();  // Creates a list to hold all possible item stacks (different variants of the shield).

            // Retrieves the variant groups from the item's attributes, which contain different combinations of materials (wood, metal, colors).
            Dictionary<string, string[]> variantGroups = this.Attributes["variantGroups"]?.AsObject<Dictionary<string, string[]>>();
            if (variantGroups == null)  // If no variant groups are defined, exit the method.
            {
                return;
            }

            // Nested loops iterate through all combinations of metals, woods, and colors to generate each shield variant.
            foreach (string metal in variantGroups["metal"])
            {
                foreach (string wood in variantGroups["wood"])
                {
                    foreach (string color in variantGroups["color"])
                    {
                        // Generates a JSON item stack for each combination of wood, metal, and color.
                        JsonItemStack stack = this.genJstack($"{{ wood: \"{wood}\", metal: \"{metal}\", color: \"{color}\" }}");

                        // If the stack is successfully created and has attributes, add it to the list.
                        if (stack != null && stack.Attributes != null)
                        {
                            stacks.Add(stack);
                        }
                    }
                }
            }

            // If any valid stacks (shield variants) were created, add them to the creative inventory.
            if (stacks.Count > 0)
            {
                this.CreativeInventoryStacks = new CreativeTabAndStackList[]
                {
                    new CreativeTabAndStackList
                    {
                        Stacks = stacks.ToArray(),  // Convert the list of stacks to an array.
                        Tabs = new string[] { "tonwexp", "general", "items", "tools" }  // Specify which creative tabs these items belong to.
                    }
                };
            }
        }

        // Summary:
        // This method dynamically generates all possible shield variants based on material combinations (wood, metal, and color).
        // Each variant is added to the creative inventory, making it available for testing or creative gameplay. It ensures that
        // all combinations are properly defined as separate item stacks.

        // Helper method that generates a JSON item stack from a provided JSON string.
        private JsonItemStack genJstack(string json)
        {
            // Parses the provided JSON string into a JsonObject, representing the item stack's attributes.
            JsonObject attributes = new JsonObject(JToken.Parse(json));

            // Gets the asset location (or item code) for this shield, which uniquely identifies it.
            AssetLocation code = this.Code;

            // Creates a new JsonItemStack object with the parsed attributes and the shield's code.
            JsonItemStack jsonItemStack = new JsonItemStack
            {
                Code = code,  // Set the shield's code.
                Type = EnumItemClass.Item,  // Defines the type as an item (not a block).
                Attributes = attributes  // Assign the parsed JSON attributes.
            };

            // Tries to resolve the item stack within the current world, returning null if the resolution fails.
            if (!jsonItemStack.Resolve(this.api.World, "shield generation", false))
            {
                return null;
            }

            return jsonItemStack;  // Returns the successfully generated item stack.
        }

        // Summary:
        // This method creates a new JsonItemStack from a JSON string, which represents the shield's materials (wood, metal, color).
        // The JsonItemStack object is necessary to store all relevant data about the item, such as its attributes, and ensures the stack
        // is valid in the game world. If the resolution fails, the method returns null.

        // Handles rendering transformations for the shield when it is held by the player in first-person view.
        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (capi == null)  // If the client-side API is not initialized, do nothing.
            {
                return;
            }

            // If the shield is being rendered in the player's first-person view, adjust its position dynamically.
            if (target == EnumItemRenderTarget.HandFp)
            {
                // Check if the player is sneaking, which affects the shield's position.
                bool sneak = capi.World.Player.Entity.Controls.Sneak;

                // Adjust the shield's current Y offset (curOffY) based on whether the player is sneaking.
                this.curOffY += ((sneak ? 0.4f : this.offY) - this.curOffY) * renderinfo.dt * 8f;

                // Apply the offset values to the shield's transformation (position in 3D space).
                renderinfo.Transform.Translation.X = this.curOffY;  // X translation.
                renderinfo.Transform.Translation.Y = this.curOffY * 1.2f;  // Y translation.
                renderinfo.Transform.Translation.Z = this.curOffY * 1.2f;  // Z translation.
            }

            // Retrieve the mesh reference ID from the item's temporary attributes.
            int meshrefid = itemstack.TempAttributes.GetInt("meshRefId", 0);
            
            // If no valid mesh reference ID exists, or the mesh cannot be found in the cache, upload a new mesh.
            if (meshrefid == 0 || !this.meshrefs.TryGetValue(meshrefid, out renderinfo.ModelRef))
            {
                int id = this.meshrefs.Count + 1;  // Generate a new ID for the mesh reference.
                
                // Upload a new multi-textured mesh for the shield based on its materials.
                MultiTextureMeshRef modelref = capi.Render.UploadMultiTextureMesh(this.GenMesh(itemstack, capi.ItemTextureAtlas));
                
                // Cache the new mesh reference and store its ID in the item's attributes.
                renderinfo.ModelRef = (this.meshrefs[id] = modelref);
                itemstack.TempAttributes.SetInt("meshRefId", id);
            }

            // Call the base class method to handle any additional rendering logic.
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }

        // Summary:
        // This method handles the rendering of the shield in first-person view, adjusting its position dynamically based on player actions
        // (such as sneaking). It also manages the loading and caching of the 3D mesh for the shield, ensuring that the correct visual
        // representation is rendered efficiently.

        // Handles animations while the shield is being held, such as raising or lowering the shield when the player sneaks.
        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            // Determines whether the shield is held in the left or right hand.
            string onhand = (byEntity.LeftHandItemSlot == slot) ? "left" : "right";
            string notonhand = (byEntity.LeftHandItemSlot == slot) ? "right" : "left";

            // If the player is sneaking, start the shield raise animation for the relevant hand.
            if (byEntity.Controls.Sneak)
            {
                if (!byEntity.AnimManager.IsAnimationActive("raiseshield-" + onhand))
                {
                    byEntity.AnimManager.StartAnimation("raiseshield-" + onhand);
                }
            }
            // If the player stops sneaking, stop the shield raise animation.
            else if (byEntity.AnimManager.IsAnimationActive("raiseshield-" + onhand))
            {
                byEntity.AnimManager.StopAnimation("raiseshield-" + onhand);
            }

            // Ensure the shield raise animation is not active on the opposite hand.
            if (byEntity.AnimManager.IsAnimationActive("raiseshield-" + notonhand))
            {
                byEntity.AnimManager.StopAnimation("raiseshield-" + notonhand);
            }

            // Call the base method for any additional idle behavior (from the base 'Item' class).
            base.OnHeldIdle(slot, byEntity);
        }

        // Summary:
        // This method handles player animations when the shield is idle (i.e., when the player is not attacking or blocking).
        // Specifically, it starts or stops the "raise shield" animation depending on whether the player is sneaking, and ensures
        // the correct hand animation is applied. This helps make the shield behave realistically in the game.

        // Generates a 3D mesh (model) for the shield based on the materials (wood, metal, color).
        public MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas)
        {
            if (this.capi == null)  // If the client API is not initialized, throw an exception.
            {
                throw new Exception("Client API (capi) is not initialized.");
            }

            // Creates a new texture source for the shield, used when generating its 3D mesh.
            var cnts = new ContainedTextureSource(this.capi, targetAtlas, new Dictionary<string, AssetLocation>(), $"For render in shield {this.Code}");
            cnts.Textures.Clear();  // Clear any existing textures in the source.

            // Retrieves the texture paths for different materials (wood, metal, and color) from the item's attributes.
            var texturePaths = this.Attributes["texturePaths"].AsObject<Dictionary<string, string>>();
            
            // Loop through each texture type and assign the appropriate texture path based on the item's attributes.
            foreach (var textureType in new[] { "wood", "metal", "color" })
            {
                // Retrieve the material type (e.g., "oak", "iron") and apply the corresponding texture.
                string material = itemstack.Attributes.GetString(textureType, null);
                
                // If the material is defined and a texture path exists for it, apply the texture.
                if (!string.IsNullOrEmpty(material) && texturePaths.TryGetValue(textureType, out var path))
                {
                    cnts.Textures[textureType] = new AssetLocation($"{path}/{material}.png");
                }
                // Otherwise, apply the default texture for that type.
                else
                {
                    cnts.Textures[textureType] = new AssetLocation($"{texturePaths[textureType]}/default.png");
                }
            }

            MeshData mesh;  // Variable to hold the generated 3D mesh.
            try
            {
                // Uses the game's tesselator to convert the item's data into a 3D mesh with the correct textures.
                this.capi.Tesselator.TesselateItem(this, out mesh, cnts);
            }
            catch (Exception)
            {
                throw;  // Rethrow any exceptions that occur during mesh generation.
            }

            return mesh;  // Return the generated mesh.
        }

        // Summary:
        // This method generates the 3D mesh (model) for the shield based on its materials. It first sets up the appropriate textures
        // for wood, metal, and color, then uses the game's tesselator to convert this data into a 3D mesh. This mesh is used for
        // rendering the shield in the game, ensuring that it visually reflects the materials chosen by the player.

        // Overloaded method to generate a 3D mesh at a specific block position (used for display purposes).
        public MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos atBlockPos) => this.GenMesh(itemstack, targetAtlas);

        // Returns the localized name of the shield, including its material components (e.g., "Iron Oak Shield").
        public override string GetHeldItemName(ItemStack itemStack)
        {
            // Retrieve the metal and wood types from the item's attributes.
            string metal = itemStack.Attributes.GetString("metal", null);
            string wood = itemStack.Attributes.GetString("wood", null);

            // Get the localized name of the metal, or "Unknown Material" if undefined.
            string metalName = metal != null ? Lang.Get("material-" + metal) : Lang.Get("Unknown Material");
            // Get the localized name of the wood, or "Unknown Material" if undefined.
            string woodName = wood != null ? Lang.Get("material-" + wood) : Lang.Get("Unknown Material");

            // Get the item type from its code (e.g., "shield") and capitalize the first letter.
            string itemType = this.Code.Path;
            if (!string.IsNullOrEmpty(itemType))
            {
                itemType = char.ToUpper(itemType[0]) + itemType.Substring(1);
            }

            // Return the full item name in the format "Metal Wood ItemType" (e.g., "Iron Oak Shield").
            return Lang.Get("{0} {1} {2}", metalName, woodName, itemType);
        }

        // Summary:
        // This method dynamically generates the name of the shield based on its materials (metal and wood). It retrieves the
        // localized names of these materials and constructs the full name of the shield (e.g., "Iron Oak Shield"). This name
        // is displayed to the player when they view the item in their inventory or in the game world.

        // Adds additional information about the shield's attributes, such as protection and absorption values.
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            // Call the base method to gather any default item information.
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            // Get the current item stack from the slot.
            ItemStack itemstack = inSlot.Itemstack;

            // Get the active and passive damage absorption and protection chance values.
            float acdmgabsorb = GetDamageAbsorptionAnmitech(itemstack, true);
            float acchance = GetProtectionChanceAnmitech(itemstack, true);
            float padmgabsorb = GetDamageAbsorptionAnmitech(itemstack, false);
            float pachance = GetProtectionChanceAnmitech(itemstack, false);

            // Append the shield's stats to the description string builder.
            dsc.AppendLine(Lang.Get("shield-stats", (int)(100f * acchance), (int)(100f * pachance), acdmgabsorb, padmgabsorb));

            // Retrieve the wood and metal types and append their names to the description.
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

        // Summary:
        // This method appends detailed information about the shield's attributes (such as damage absorption and protection chance)
        // to the item's description. This information is displayed to the player when they inspect the item, providing useful
        // details about the shield's effectiveness based on the materials used (wood, metal).

        // Generates a unique key for caching the shield's mesh based on its material attributes.
        public string GetMeshCacheKey(ItemStack itemstack)
        {
            // Retrieve the wood, metal, and color types from the item's attributes.
            string wood = itemstack.Attributes.GetString("wood", null);
            string metal = itemstack.Attributes.GetString("metal", null);
            string color = itemstack.Attributes.GetString("color", null);
            
            // Return a string that uniquely identifies the shield's mesh based on its materials.
            return $"{this.Code.ToShortString()}-{wood}-{metal}-{color}";
        }

        // Summary:
        // This method generates a unique cache key based on the shield's materials (wood, metal, color). The key is used to
        // efficiently cache and retrieve the shield's mesh, avoiding the need to regenerate the mesh every time the shield
        // is rendered. This improves performance by reducing unnecessary computations.

        // Calculates the shield's protection chance (active or passive) based on its materials.
        public float GetProtectionChanceAnmitech(ItemStack itemstack, bool isActive)
        {
            // Retrieve the wood and metal types from the item's attributes.
            string wood = itemstack.Attributes.GetString("wood", null);
            string metal = itemstack.Attributes.GetString("metal", null);

            float chance = 0f;  // Initialize the protection chance to zero.
            JsonObject shieldStats = this.Attributes["shield"];  // Retrieve the shield stats from the item's attributes.

            // If shield stats are defined, add the protection chance based on the wood material.
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

                // Add the protection chance based on the metal material, if defined.
                if (!string.IsNullOrEmpty(metal))
                {
                    JsonObject metalStats = shieldStats["shieldStatsByMaterial"]["metal"][metal];
                    if (metalStats != null)
                    {
                        chance += metalStats["protectionChance"][isActive ? "active" : "passive"].AsFloat(0f);
                    }
                }

                // Ensure the protection chance does not exceed 100% (1.0).
                chance = Math.Min(1f, chance);
            }

            return chance;  // Return the calculated protection chance.
        }

        // Summary:
        // This method calculates the shield's protection chance, which determines the likelihood of the shield blocking an attack.
        // The protection chance is based on the materials used (wood and metal), and separate values are calculated for active
        // (when the player is actively blocking) and passive (when not blocking) states.

        // Calculates the damage absorption of the shield (active or passive) based on its materials.
        public float GetDamageAbsorptionAnmitech(ItemStack itemstack, bool isActive)
        {
            // Retrieve the wood and metal types from the item's attributes.
            string wood = itemstack.Attributes.GetString("wood", null);
            string metal = itemstack.Attributes.GetString("metal", null);

            float absorption = 0f;  // Initialize the damage absorption value to zero.
            JsonObject shieldStats = this.Attributes["shield"];  // Retrieve the shield stats from the item's attributes.

            // If shield stats are defined, add the damage absorption value based on the wood material.
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

                // Add the damage absorption value based on the metal material, if defined.
                if (!string.IsNullOrEmpty(metal))
                {
                    JsonObject metalStats = shieldStats["shieldStatsByMaterial"]["metal"][metal];
                    if (metalStats != null)
                    {
                        absorption += metalStats["damageAbsorption"][isActive ? "active" : "passive"].AsFloat(0f);
                    }
                }
            }

            return absorption;  // Return the calculated damage absorption value.
        }

        // Summary:
        // This method calculates how much damage the shield can absorb based on the materials used (wood and metal). Separate
        // absorption values are calculated for active (blocking) and passive states. The final absorption value is determined
        // by summing the contribution of both the wood and metal components.

        // Called when the shield is created via crafting, allowing for custom attributes to be assigned.
        public override void OnCreatedByCrafting(ItemSlot[] allInputSlots, ItemSlot outputSlot, GridRecipe byRecipe)
        {
            // Call the base method to handle any default crafting behavior.
            base.OnCreatedByCrafting(allInputSlots, outputSlot, byRecipe);

            // Assign custom attributes (such as durability or protection values) to the crafted shield.
            AssignCustomShieldAttributes(outputSlot.Itemstack);
        }

        // Assigns custom attributes to the shield based on its materials when it is crafted.
        private void AssignCustomShieldAttributes(ItemStack itemstack)
        {
            // Retrieve the wood and metal types from the item's attributes.
            string wood = itemstack.Attributes.GetString("wood");
            string metal = itemstack.Attributes.GetString("metal");

            // Calculate active and passive damage absorption and protection chance values.
            float acdmgabsorb = GetDamageAbsorptionAnmitech(itemstack, true);
            float acchance = GetProtectionChanceAnmitech(itemstack, true);
            float padmgabsorb = GetDamageAbsorptionAnmitech(itemstack, false);
            float pachance = GetProtectionChanceAnmitech(itemstack, false);
            
            // Assign the calculated values as custom attributes for the shield.
            itemstack.Attributes.SetFloat("customDamageAbsorptionActive", acdmgabsorb);
            itemstack.Attributes.SetFloat("customProtectionChanceActive", acchance);
            itemstack.Attributes.SetFloat("customDamageAbsorptionPassive", padmgabsorb);
            itemstack.Attributes.SetFloat("customProtectionChancePassive", pachance);
        }

        // Summary:
        // This method is called when the shield is crafted by the player. It assigns custom attributes to the shield based
        // on the materials used (wood and metal), such as custom protection and damage absorption values. These custom attributes
        // are stored in the item's attributes and affect how the shield performs in gameplay.
    }
}
