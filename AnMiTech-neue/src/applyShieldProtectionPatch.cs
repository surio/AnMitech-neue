using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.API.Datastructures;

namespace tonwexp
{
    [HarmonyPatch(typeof(ModSystemWearableStats), "applyShieldProtection")]
    public static class ShieldPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ref float __result, IPlayer player, float damage, DamageSource dmgSource)
        {
            double horizontalAngleProtectionRange = 1.0471975803375244;
            ItemSlot[] shieldSlots = new ItemSlot[]
            {
                player.Entity.LeftHandItemSlot,
                player.Entity.RightHandItemSlot
            };

            float initialDamage = damage;

            for (int i = 0; i < shieldSlots.Length; i++)
            {
                ItemSlot shieldSlot = shieldSlots[i];
                ItemStack itemstack = shieldSlot.Itemstack;
                if (itemstack == null)
                {
                    continue;
                }

                JsonObject jsonObject = itemstack.ItemAttributes?["shield"];
                float dmgabsorb = 0f;
                float chance = 0f;

                if (itemstack.Item is ItemShieldAnMiTech customShield)
                {
                    bool isActive = player.Entity.Controls.Sneak;
                    dmgabsorb = customShield.GetDamageAbsorptionAnmitech(itemstack, isActive);
                    chance = customShield.GetProtectionChanceAnmitech(itemstack, isActive);
                }
                else if (jsonObject != null && jsonObject.Exists)
                {
                    string usetype = player.Entity.Controls.Sneak ? "active" : "passive";
                    dmgabsorb = jsonObject["damageAbsorption"][usetype].AsFloat(0f);
                    chance = jsonObject["protectionChance"][usetype].AsFloat(0f);
                }
                else
                {
                    continue;
                }

                double dx, dy, dz;
                if (dmgSource.HitPosition != null)
                {
                    dx = dmgSource.HitPosition.X - player.Entity.Pos.X;
                    dy = dmgSource.HitPosition.Y - player.Entity.Pos.Y;
                    dz = dmgSource.HitPosition.Z - player.Entity.Pos.Z;
                }
                else if (dmgSource.SourceEntity != null)
                {
                    dx = dmgSource.SourceEntity.Pos.X - player.Entity.Pos.X;
                    dy = dmgSource.SourceEntity.Pos.Y - player.Entity.Pos.Y;
                    dz = dmgSource.SourceEntity.Pos.Z - player.Entity.Pos.Z;
                }
                else if (dmgSource.SourcePos != null)
                {
                    dx = dmgSource.SourcePos.X - player.Entity.Pos.X;
                    dy = dmgSource.SourcePos.Y - player.Entity.Pos.Y;
                    dz = dmgSource.SourcePos.Z - player.Entity.Pos.Z;
                }
                else
                {
                    continue;
                }

                double playerYaw = player.Entity.Pos.Yaw + Math.PI / 2;
                double playerPitch = player.Entity.Pos.Pitch;
                double attackYaw = Math.Atan2(dx, dz);
                float attackPitch = (float)Math.Atan2(dy, Math.Sqrt(dx * dx + dz * dz));

                bool inProtectionRange;
                if (Math.Abs(attackPitch) > 1.134464f)
                {
                    inProtectionRange = Math.Abs(GameMath.AngleRadDistance((float)playerPitch, attackPitch)) < 0.5235988f;
                }
                else
                {
                    inProtectionRange = Math.Abs(GameMath.AngleRadDistance((float)playerYaw, (float)attackYaw)) < horizontalAngleProtectionRange;
                }

                if (inProtectionRange && player.Entity.World.Rand.NextDouble() < chance)
                {
                    float blockedDamage = Math.Min(damage, dmgabsorb);
                    damage -= blockedDamage;

                    if (player is IServerPlayer serverPlayer && blockedDamage > 0)
                    {
                        serverPlayer.SendMessage(GlobalConstants.DamageLogChatGroup, Lang.Get("{0:0.#} of {1:0.#} damage blocked by shield", new object[]
                        {
                            blockedDamage,
                            initialDamage
                        }), EnumChatType.Notification);
                    }

                    string blockSound = itemstack.ItemAttributes?["blockSound"]?.AsString("held/shieldblock") ?? "held/shieldblock";
                    player.Entity.Api.World.PlaySoundAt(AssetLocation.Create(blockSound, itemstack.Collectible.Code.Domain).WithPathPrefixOnce("sounds/").WithPathAppendixOnce(".ogg"), player.Entity, null, true, 32f, 1f);

                    (player.Entity.Api as ICoreServerAPI)?.Network.BroadcastEntityPacket(player.Entity.EntityId, 200, SerializerUtil.Serialize<string>("shieldBlock" + ((i == 0) ? "L" : "R")));

                    if (player.Entity.Api.Side == EnumAppSide.Server)
                    {
                        shieldSlot.Itemstack.Collectible.DamageItem(player.Entity.Api.World, dmgSource.SourceEntity, shieldSlot, 1);
                        shieldSlot.MarkDirty();
                    }
                }
            }

            __result = damage;
            return false;
        }
    }
}
