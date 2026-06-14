using Client.Domain.Entities;
using Client.Domain.Service;
using Client.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Domain.AI.Combat
{
    public static class Helper
    {
        public static Skill? GetSkillByConfig(WorldHandler worldHandler, Config config, Hero hero, CreatureInterface target)
        {
            var allConditions = config.Combat.SkillConditions;
            var enabledConditions = allConditions.Where(x => x.Enabled).OrderBy(x => x.Priority).ToList();
            var targetHp = target.VitalStats.HpPercent;
            var heroMp = hero.VitalStats.MpPercent;
            var heroHp = hero.VitalStats.HpPercent;

            if (allConditions.Count > 0)
            {
                DebugLogger.Log($"GetSkillByConfig: total conditions={allConditions.Count}, enabled={enabledConditions.Count}, allSkills={worldHandler.GetAllSkills().Count}, heroMP={heroMp}% heroHP={heroHp}% targetHP={targetHp}%");
            }

            foreach (var condition in enabledConditions)
            {
                var skill = worldHandler.GetSkillById(condition.Id);
                if (skill == null)
                {
                    DebugLogger.Log($"GetSkillByConfig: condition id={condition.Id} → skill NOT FOUND in world/skillInfo");
                    continue;
                }

                DebugLogger.Log($"GetSkillByConfig: checking id={condition.Id} name={skill.Name} enabled_chks=[HP≤={condition.MaxTargetPercentHpEnabled}({condition.MaxTargetPercentHp}), MP≥={condition.MinPlayerPercentMpEnabled}({condition.MinPlayerPercentMp}), MyHP≤={condition.MaxPlayerPercentHpEnabled}({condition.MaxPlayerPercentHp})]");

                if (condition.MaxTargetPercentHpEnabled && condition.MaxTargetPercentHp < targetHp)
                {
                    DebugLogger.Log($"GetSkillByConfig: SKIP id={condition.Id} — MaxTargetHp {condition.MaxTargetPercentHp}% < target {targetHp}%");
                    continue;
                }
                if (condition.MinPlayerPercentMpEnabled && condition.MinPlayerPercentMp > heroMp)
                {
                    DebugLogger.Log($"GetSkillByConfig: SKIP id={condition.Id} — MinPlayerMp {condition.MinPlayerPercentMp}% > hero {heroMp}%");
                    continue;
                }
                if (condition.MaxPlayerPercentHpEnabled && condition.MaxPlayerPercentHp < heroHp)
                {
                    DebugLogger.Log($"GetSkillByConfig: SKIP id={condition.Id} — MaxPlayerHp {condition.MaxPlayerPercentHp}% < hero {heroHp}%");
                    continue;
                }

                DebugLogger.Log($"GetSkillByConfig: RETURNING skill id={condition.Id} name={skill.Name}");
                return skill;
            }

            return null;
        }

        public static List<Drop> GetDropByConfig(WorldHandler worldHandler, Config config, Hero hero)
        {
            if (!config.Combat.PickupIfPossible)
            {
                return new List<Drop>();
            }

            var result = worldHandler.GetDropsSortedByDistanceToHero(config.Combat.PickupMaxDeltaZ)
                .Where(x => !config.Combat.ExcludedItemIdsToPickup.ContainsKey(x.ItemId));

            if (config.Combat.IncludedItemIdsToPickup.Count > 0)
            {
                result = result.Where(x => config.Combat.IncludedItemIdsToPickup.ContainsKey(x.ItemId));
            }

            result = result.Where(x => x.Transform.Position.HorizontalDistance(hero.Transform.Position) <= config.Combat.PickupRadius);

            return result.ToList();
        }

        public static List<NPC> GetMobsToAttackByConfig(WorldHandler worldHandler, Config config, Hero hero)
        {
            var result = worldHandler.GetAliveMobsSortedByDistanceToHero(config.Combat.Zone.MaxZDelta)
                .Where(x => !config.Combat.ExcludedMobIds.ContainsKey(x.NpcId));

            if (config.Combat.IncludedMobIds.Count > 0)
            {
                result = result.Where(x => config.Combat.IncludedMobIds.ContainsKey(x.NpcId));
            }

            if (config.Combat.MobLevelLowerLimit != null)
            {
                result = result.Where(x => (int) (hero.ExperienceInfo.Level - x.Level) <= config.Combat.MobLevelLowerLimit);
            }

            if (config.Combat.MobLevelUpperLimit != null)
            {
                result = result.Where(x => (int) (x.Level - hero.ExperienceInfo.Level) <= config.Combat.MobLevelUpperLimit);
            }

            if (config.Combat.Zone != null)
            {
                result = result.Where(x => config.Combat.Zone.IsInside(x.Transform.Position, hero.Transform.Position));
            }

            return result.ToList();
        }

        public static bool IsOnSpot(WorldHandler worldHandler, Config config, Hero hero)
        {
            if (config.Combat.Zone == null || config.Combat.Zone.Type == ZoneType.Free)
            {
                return true;
            }

            return config.Combat.Zone.IsInside(hero.Transform.Position, hero.Transform.Position);
        }

        public static uint GetAttackDistanceByConfig(WorldHandler worldHandler, Config config, Hero hero, CreatureInterface target)
        {
            // 1. PrimaryAttackSkillId — its Range is the approach distance even on cooldown
            if (config.Combat.PrimaryAttackSkillId != 0)
            {
                var primarySkill = worldHandler.GetSkillById(config.Combat.PrimaryAttackSkillId);
                if (primarySkill != null)
                {
                    return (uint)primarySkill.Range;
                }
            }

            // 2. SkillCondition skills — use the max range among enabled conditions
            var enabledConditions = config.Combat.SkillConditions.Where(x => x.Enabled).OrderBy(x => x.Priority).ToList();
            if (enabledConditions.Count > 0)
            {
                uint maxRange = 0;
                foreach (var cond in enabledConditions)
                {
                    var condSkill = worldHandler.GetSkillById(cond.Id);
                    if (condSkill != null && condSkill.Range > maxRange)
                    {
                        maxRange = (uint)condSkill.Range;
                    }
                }
                if (maxRange > 0)
                {
                    return maxRange;
                }
            }

            // 3. AttackDistanceOverride — manual override
            if (config.Combat.AttackDistanceOverride != 0)
            {
                return config.Combat.AttackDistanceOverride;
            }

            // 4. Fallback: weapon-based
            var equippedWeapon = worldHandler.GetEquippedWeapon();
            return equippedWeapon != null && equippedWeapon.WeaponType == Enums.WeaponTypeEnum.Bow
                ? config.Combat.AttackDistanceBow
                : config.Combat.AttackDistanceMili;
        }
    }
}
