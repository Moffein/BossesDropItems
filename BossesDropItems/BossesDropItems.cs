using BepInEx;
using BepInEx.Configuration;
using RoR2;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace R2API.Utils
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class ManualNetworkRegistrationAttribute : Attribute
    {
    }
}

namespace BossesDropItems
{
    [BepInPlugin("com.Moffein.BossesDropItems", "Bosses Drop Items", "1.2.3")]
    public class BossesDropItems : BaseUnityPlugin
    {
        public static float blankChance = 0f;
        public static float whiteChance = 60f;
        public static float greenChance = 30f;
        public static float redChance = 3f;
        public static float yellowChance = 7f;

        public static int minEliteBossDrops = 3;
        public static int maxExtraDrops = 7;
        public static bool guaranteeTeleGreen = true;
        public static bool teleBossDropsItems = true;
        public static bool hordeDropsItems = true;
        public static bool lunarChimerasDropItems = false;
        public static bool enableTeleDrops = true;
        public static bool sacrificeOnly = false;
        public static int overloadingWormBonus = 4;

        private static BodyIndex ElectricWormBodyIndex;

        private static GameModeIndex SimulacrumIndex;

        public void Awake()
        {
            sacrificeOnly = base.Config.Bind<bool>(new ConfigDefinition("Drop Settings", "Sacrifice Only"), false, new ConfigDescription("Only drop items if the Artifact of Sacrifice is enabled.")).Value;
            enableTeleDrops = base.Config.Bind<bool>(new ConfigDefinition("Drop Settings", "Enable Teleporter Drops"), true, new ConfigDescription("Teleporters drop items for all players when all bosses are killed.")).Value;
            teleBossDropsItems = base.Config.Bind<bool>(new ConfigDefinition("Drop Settings", "Teleporter Bosses Drop Items"), true, new ConfigDescription("Allows Teleporter Bosses to drop items when killed.")).Value;
            hordeDropsItems = base.Config.Bind<bool>(new ConfigDefinition("Drop Settings", "Horde of Many Drops Items"), true, new ConfigDescription("Allows Horde of Many Bosses to drop items when killed.")).Value;
            guaranteeTeleGreen = base.Config.Bind<bool>(new ConfigDefinition("Drop Settings", "Guaranteed Green from Tele Bosses"), false, new ConfigDescription("Guarantees that Teleporter Bosses will drop only Green-tier items or better. Does not apply to Horde of Many.")).Value;
            maxExtraDrops = base.Config.Bind<int>(new ConfigDefinition("Drop Settings", "Max Extra Drops"), 7, new ConfigDescription("Maximum amount of extra items Elite Bosses can drop. Regular Elite Bosses will attempt to drop 3 extra items, while Tier 2 Elite Bosses will attempt to drop 17.")).Value;
            minEliteBossDrops = base.Config.Bind<int>(new ConfigDefinition("Drop Settings", "Min Extra Elite Boss Drops"), 1, new ConfigDescription("Minimum amount of extra items Elite Bosses can drop.")).Value;
            overloadingWormBonus = base.Config.Bind<int>(new ConfigDefinition("Drop Settings", "Overloading Worm Extra Boss Drops"), 4, new ConfigDescription("Extra item drops from Overloading Worms.")).Value;
            lunarChimerasDropItems = base.Config.Bind<bool>(new ConfigDefinition("Drop Settings", "Lunar Chimeras Drop Items"), false, new ConfigDescription("Makes Lunar Chimeras able to drop items when they show up as bosses during Mitchell Phase 2.")).Value;

            blankChance = base.Config.Bind<float>(new ConfigDefinition("Item Tier Settings", "Blank Chance"), 0f, new ConfigDescription("Chance for bosses to drop no items.")).Value;
            whiteChance = base.Config.Bind<float>(new ConfigDefinition("Item Tier Settings", "White Chance"), 60f, new ConfigDescription("Chance for bosses to drop a white item.")).Value;
            greenChance = base.Config.Bind<float>(new ConfigDefinition("Item Tier Settings", "Green Chance"), 30f, new ConfigDescription("Chance for bosses to drop a green item.")).Value;
            redChance = base.Config.Bind<float>(new ConfigDefinition("Item Tier Settings", "Red Chance"), 3f, new ConfigDescription("Chance for bosses to drop a red item.")).Value;
            yellowChance = base.Config.Bind<float>(new ConfigDefinition("Item Tier Settings", "Yellow Chance"), 7f, new ConfigDescription("Chance for bosses to drop their corresponding boss item if they have one.")).Value;
            //GlobalEventManager.onCharacterDeathGlobal += BossesDropItems.OnServerCharacterDeath;

            On.RoR2.BodyCatalog.Init += (orig) =>
            {
                 orig();
                 ElectricWormBodyIndex = BodyCatalog.FindBodyIndex("ElectricWormBody");
            };

            On.RoR2.GameModeCatalog.LoadGameModes += (orig) =>
            {
                orig();
                SimulacrumIndex = GameModeCatalog.FindGameModeIndex("InfiniteTowerRun");
            };

            if (!enableTeleDrops)
            {
                On.RoR2.BossGroup.DropRewards += (orig, self) =>
                {
                    if (self.forceTier3Reward)
                    {
                        orig(self);
                    }
                };
            }

            //This is needed due to Aetherium's Witches Ring
            On.RoR2.HealthComponent.TakeDamage += (orig, self, damageInfo) =>
            {
                bool aliveBeforeHit = self.alive;
                bool bossEnemy = (self.body && self.body.isBoss);
                bool sacrificeCheck = !sacrificeOnly || RunArtifactManager.instance.IsArtifactEnabled(RoR2Content.Artifacts.Sacrifice.artifactIndex);

                orig(self, damageInfo);

                
                if (NetworkServer.active && sacrificeCheck && aliveBeforeHit && !self.alive)
                {
                    if (self.gameObject && self.body && (self.body.isChampion || (!self.body.isChampion && bossEnemy && hordeDropsItems && Run.instance.gameModeIndex != SimulacrumIndex)) && self.body.teamComponent && self.body.teamComponent.teamIndex == TeamIndex.Monster && (!bossEnemy || teleBossDropsItems) && (lunarChimerasDropItems || !(self.body.baseNameToken == "LUNARWISP_BODY_NAME" || self.body.baseNameToken == "LUNARGOLEM_BODY_NAME" || self.body.baseNameToken == "LUNAREXPLODER_BODY_NAME")))
                    {
                        DeathRewards reward = self.gameObject.GetComponent<DeathRewards>();
                        PickupIndex bossPickupIndex = reward ? PickupCatalog.FindPickupIndex(reward.bossPickup.pickupName) : PickupIndex.none;
                        BossesDropItems.DropItem(self.body, bossPickupIndex, (guaranteeTeleGreen && bossEnemy&& self.body.isChampion), 0f);

                        if ((self.body.isChampion && self.body.isElite) || self.body.bodyIndex == ElectricWormBodyIndex)
                        {
                            int extraDrops = 0;
                            if (self.body.master)
                            {
                                extraDrops += Mathf.RoundToInt((float)self.body.master.inventory.GetItemCount(RoR2Content.Items.BoostHp) / 10f);
                            }

                            if (self.body.bodyIndex == ElectricWormBodyIndex)
                            {
                                extraDrops += overloadingWormBonus;
                            }

                            if (self.body.isElite)
                            {
                                extraDrops = Math.Max(extraDrops, minEliteBossDrops);
                            }

                            extraDrops = Math.Min(maxExtraDrops, extraDrops);

                            for (int i = 0; i < extraDrops; i++)
                            {
                                BossesDropItems.DropItem(self.body, bossPickupIndex, (guaranteeTeleGreen && bossEnemy && self.body.isChampion), 15f);
                            }
                        }
                    }
                }
            };
        }

        private static void DropItem(CharacterBody victimBody, PickupIndex bossPickup, bool greenMinimum, float randomOffset)
        {
            List<PickupIndex> list;
            float total = whiteChance + greenChance + redChance + yellowChance + blankChance;
            if (greenMinimum || !Util.CheckRoll(100f * blankChance / total, 0))
            {
                total -= blankChance;
                if (!greenMinimum && Util.CheckRoll(100f * whiteChance / total, 0))//drop white
                {
                    list = Run.instance.availableTier1DropList;
                }
                else
                {
                    total -= whiteChance;
                    if (Util.CheckRoll(100f * greenChance / total, 0))//drop green
                    {
                        list = Run.instance.availableTier2DropList;
                    }
                    else
                    {
                        total -= greenChance;
                        if (Util.CheckRoll(100f * redChance / total, 0))//drop red
                        {
                            list = Run.instance.availableTier3DropList;
                        }
                        else
                        {
                            if (bossPickup != PickupIndex.none)//drop yellow
                            {
                                list = new List<PickupIndex>
                                {
                                    bossPickup
                                };
                            }
                            else//drop green if no boss pickup available
                            {
                                list = Run.instance.availableTier2DropList;
                            }
                        }
                    }
                }
                int index = Run.instance.treasureRng.RangeInt(0, list.Count);
                PickupDropletController.CreatePickupDroplet(list[index], victimBody.transform.position, new Vector3(UnityEngine.Random.Range(0f, randomOffset), 20f, UnityEngine.Random.Range(0f, randomOffset)));
            }
        }
    }
}
