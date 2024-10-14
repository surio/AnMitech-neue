using System;                              // Provides core functionality such as basic data types and mathematical functions.
using HarmonyLib;                          // Importing the Harmony library for modifying methods at runtime via patching.
using Vintagestory.API.Common;             // Common API components for interacting with the Vintage Story game world (e.g., players, entities).
using Vintagestory.API.Config;             // API components related to game configurations (e.g., localization, global settings).
using Vintagestory.API.Server;             // Provides server-side API for handling server-specific logic (e.g., messaging, entity management).
using Vintagestory.API.MathTools;          // Provides mathematical tools such as vectors and angle calculations.
using Vintagestory.API.Util;               // Utility functions and helpers for common game tasks (e.g., random number generation).
using Vintagestory.GameContent;            // Core game content, allowing mods to interact with built-in game items, entities, and mechanics.
using Vintagestory.API.Datastructures;     // Provides advanced data structures for working with game data.

namespace tonwexp                         // Defines the mod's namespace, grouping related classes under a common structure.
{
    // Applies a Harmony patch to the 'applyShieldProtection' method in the 'ModSystemWearableStats' class.
    [HarmonyPatch(typeof(ModSystemWearableStats), "applyShieldProtection")]
    public static class ShieldPatch
    {
        // The prefix method for the Harmony patch. It intercepts calls to 'applyShieldProtection' and modifies its behavior before it executes.
        [HarmonyPrefix]
        public static bool Prefix(ref float __result, IPlayer player, float damage, DamageSource dmgSource)
        {
            // Defines the range of protection for horizontal shield blocks (measured in radians, approximately 60 degrees).
            double horizontalAngleProtectionRange = 1.0471975803375244;

            // Defines the possible item slots where the player could be holding a shield (left or right hand).
            ItemSlot[] shieldSlots = new ItemSlot[]
            {
                player.Entity.LeftHandItemSlot,
                player.Entity.RightHandItemSlot
            };

            float initialDamage = damage;  // Stores the original damage value for later use, such as displaying blocked damage messages.

            // Loop over both item slots (left and right hand) to check for shields.
            for (int i = 0; i < shieldSlots.Length; i++)
            {
                ItemSlot shieldSlot = shieldSlots[i];  // Get the current item slot (left or right hand).
                ItemStack itemstack = shieldSlot.Itemstack;  // Get the item stack from the current slot.

                if (itemstack == null)  // If the current slot is empty (no shield), continue to the next slot.
                {
                    continue;
                }

                // Try to retrieve shield attributes from the item, if available.
                JsonObject jsonObject = itemstack.ItemAttributes?["shield"];
                float dmgabsorb = 0f;  // Initialize the damage absorption value.
                float chance = 0f;     // Initialize the protection chance.

                // If the shield is an instance of the custom shield class 'ItemShieldAnMiTech', retrieve its custom stats.
                if (itemstack.Item is ItemShieldAnMiTech customShield)
                {
                    bool isActive = player.Entity.Controls.Sneak;  // Check if the player is sneaking to determine if the shield is actively blocking.
                    dmgabsorb = customShield.GetDamageAbsorptionAnmitech(itemstack, isActive);  // Get damage absorption value based on active or passive state.
                    chance = customShield.GetProtectionChanceAnmitech(itemstack, isActive);     // Get protection chance value based on active or passive state.
                }
                // Otherwise, if the item has general shield attributes, extract them (fallback for non-custom shields).
                else if (jsonObject != null && jsonObject.Exists)
                {
                    string usetype = player.Entity.Controls.Sneak ? "active" : "passive";  // Determine if the shield is active (sneak) or passive (not sneaking).
                    dmgabsorb = jsonObject["damageAbsorption"][usetype].AsFloat(0f);       // Get damage absorption from the item attributes.
                    chance = jsonObject["protectionChance"][usetype].AsFloat(0f);          // Get protection chance from the item attributes.
                }
                else
                {
                    continue;  // If no shield attributes are found, skip this slot and continue to the next one.
                }

                // Calculate the direction of the incoming damage (dx, dy, dz) based on the damage source's position relative to the player.
                double dx, dy, dz;
                if (dmgSource.HitPosition != null)  // If the hit position is specified, calculate based on that.
                {
                    dx = dmgSource.HitPosition.X - player.Entity.Pos.X;
                    dy = dmgSource.HitPosition.Y - player.Entity.Pos.Y;
                    dz = dmgSource.HitPosition.Z - player.Entity.Pos.Z;
                }
                else if (dmgSource.SourceEntity != null)  // If the damage comes from another entity, calculate based on that entity's position.
                {
                    dx = dmgSource.SourceEntity.Pos.X - player.Entity.Pos.X;
                    dy = dmgSource.SourceEntity.Pos.Y - player.Entity.Pos.Y;
                    dz = dmgSource.SourceEntity.Pos.Z - player.Entity.Pos.Z;
                }
                else if (dmgSource.SourcePos != null)  // If the damage source has a position (e.g., projectile), calculate based on that.
                {
                    dx = dmgSource.SourcePos.X - player.Entity.Pos.X;
                    dy = dmgSource.SourcePos.Y - player.Entity.Pos.Y;
                    dz = dmgSource.SourcePos.Z - player.Entity.Pos.Z;
                }
                else  // If the damage source is undefined, skip this check.
                {
                    continue;
                }

                // Calculate the player's yaw and pitch (rotation) to determine the player's orientation.
                double playerYaw = player.Entity.Pos.Yaw + Math.PI / 2;  // Calculate player's yaw (rotation around Y-axis).
                double playerPitch = player.Entity.Pos.Pitch;            // Retrieve player's pitch (rotation around X-axis).
                double attackYaw = Math.Atan2(dx, dz);                   // Calculate the yaw of the attack (angle relative to the player).
                float attackPitch = (float)Math.Atan2(dy, Math.Sqrt(dx * dx + dz * dz));  // Calculate the pitch of the attack (vertical angle).

                // Determine if the attack is within the shield's protection range.
                bool inProtectionRange;
                if (Math.Abs(attackPitch) > 1.134464f)  // If the attack is very steep (pitch greater than ~65 degrees).
                {
                    inProtectionRange = Math.Abs(GameMath.AngleRadDistance((float)playerPitch, attackPitch)) < 0.5235988f;  // Check vertical protection range (~30 degrees).
                }
                else  // For less steep angles, check if the attack falls within the horizontal protection range (~60 degrees).
                {
                    inProtectionRange = Math.Abs(GameMath.AngleRadDistance((float)playerYaw, (float)attackYaw)) < horizontalAngleProtectionRange;
                }

                // If the attack is within the protection range and passes the shield's protection chance, block the damage.
                if (inProtectionRange && player.Entity.World.Rand.NextDouble() < chance)
                {
                    float blockedDamage = Math.Min(damage, dmgabsorb);  // Calculate how much damage the shield blocks (up to the shield's absorption limit).
                    damage -= blockedDamage;  // Subtract the blocked damage from the total damage.

                    // If this is a server player and some damage was blocked, send a message to the player indicating how much damage was blocked.
                    if (player is IServerPlayer serverPlayer && blockedDamage > 0)
                    {
                        serverPlayer.SendMessage(GlobalConstants.DamageLogChatGroup, Lang.Get("{0:0.#} of {1:0.#} damage blocked by shield", new object[]
                        {
                            blockedDamage,  // The amount of damage blocked.
                            initialDamage  // The total damage before blocking.
                        }), EnumChatType.Notification);  // Sends the message as a notification.
                    }

                    // Play a shield block sound effect at the player's location.
                    string blockSound = itemstack.ItemAttributes?["blockSound"]?.AsString("held/shieldblock") ?? "held/shieldblock";
                    player.Entity.Api.World.PlaySoundAt(AssetLocation.Create(blockSound, itemstack.Collectible.Code.Domain).WithPathPrefixOnce("sounds/").WithPathAppendixOnce(".ogg"), player.Entity, null, true, 32f, 1f);

                    // Broadcast a network packet to indicate a shield block animation (left or right hand, based on the shield slot).
                    (player.Entity.Api as ICoreServerAPI)?.Network.BroadcastEntityPacket(player.Entity.EntityId, 200, SerializerUtil.Serialize<string>("shieldBlock" + ((i == 0) ? "L" : "R")));

                    // If this is a server, apply durability damage to the shield and mark the item slot as dirty (modified).
                    if (player.Entity.Api.Side == EnumAppSide.Server)
                    {
                        shieldSlot.Itemstack.Collectible.DamageItem(player.Entity.Api.World, dmgSource.SourceEntity, shieldSlot, 1);  // Damages the shield.
                        shieldSlot.MarkDirty();  // Mark the shield slot as changed, so the game updates the item state.
                    }
                }
            }

            // After processing all possible shields, set the final damage value (after blocking).
            __result = damage;

            // Return 'false' to prevent the original method from executing, as we have handled the shield protection logic here.
            return false;
        }
    }
}
