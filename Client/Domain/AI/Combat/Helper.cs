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

        public static List<Drop> GetDropByConfig(WorldHandler worldHandler, Config config, Hero hero, Vector3? deathPosition = null)
        {
            if (!config.Combat.PickupIfPossible)
            {
                return new List<Drop>();
            }

            var allDrops = worldHandler.GetDropsSortedByDistanceToHero(config.Combat.PickupMaxDeltaZ).ToList();
            var result = allDrops.AsEnumerable()
                .Where(x => !config.Combat.ExcludedItemIdsToPickup.ContainsKey(x.ItemId));

            if (config.Combat.IncludedItemIdsToPickup.Count > 0)
            {
                result = result.Where(x => config.Combat.IncludedItemIdsToPickup.ContainsKey(x.ItemId));
            }

            // Two-stage pickup filter:
            // 1. Try strict: drops within PickupRadius of deathPos (avoids foreign drops)
            // 2. Fallback: drops within max(PickupRadius, attackDistance) of hero position
            //    Server scatters drops far from deathPos, so strict often misses.
            var radius = (short)config.Combat.PickupRadius;
            var attackDist = config.Combat.AttackDistanceOverride > 0
                ? config.Combat.AttackDistanceOverride
                : (uint)GetAttackDistanceByConfigSimple(worldHandler, config);
            var fallbackRadius = Math.Max(radius, attackDist);

            List<Drop> filtered;

            if (deathPosition != null)
            {
                // Stage 1: strict from deathPos
                var strict = result.Where(x => x.Transform.Position.HorizontalDistance(deathPosition) <= radius).ToList();
                if (strict.Count > 0)
                {
                    filtered = strict;
                }
                else
                {
                    // Stage 2: fallback to hero position — server scatters drops
                    filtered = result.Where(x => x.Transform.Position.HorizontalDistance(hero.Transform.Position) <= fallbackRadius).ToList();
                    if (allDrops.Count > 0 && filtered.Count == 0)
                    {
                        var heroDists = allDrops.Select(d => $"{d.Name}={d.Transform.Position.HorizontalDistance(hero.Transform.Position):F0}").ToList();
                        DebugLogger.Log($"GetDropByConfig: allDrops={allDrops.Count}, strict=0 (radius={radius} from deathPos), fallback=0 (radius={fallbackRadius} from hero), fromHero=[{string.Join(", ", heroDists)}]");
                    }
                    else if (filtered.Count > 0)
                    {
                        DebugLogger.Log($"GetDropByConfig: strict=0, fallback={filtered.Count} drops within {fallbackRadius} of hero (deathPos too far)");
                    }
                }
            }
            else
            {
                // No death position — use hero pos with attackDistance
                filtered = result.Where(x => x.Transform.Position.HorizontalDistance(hero.Transform.Position) <= fallbackRadius).ToList();
            }

            return filtered;
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

        /// <summary>
        /// Simplified attack distance without hero/target — just checks skill ranges from config.
        /// Used for pickup radius calculation where we don't have a specific target.
        /// </summary>
        public static uint GetAttackDistanceByConfigSimple(WorldHandler worldHandler, Config config)
        {
            if (config.Combat.AttackDistanceOverride != 0)
                return config.Combat.AttackDistanceOverride;

            if (config.Combat.PrimaryAttackSkillId != 0)
            {
                var skill = worldHandler.GetSkillById(config.Combat.PrimaryAttackSkillId);
                if (skill != null && skill.Range > 0)
                    return (uint)skill.Range;
            }

            var enabledConditions = config.Combat.SkillConditions.Where(x => x.Enabled).ToList();
            uint maxRange = 0;
            foreach (var cond in enabledConditions)
            {
                var condSkill = worldHandler.GetSkillById(cond.Id);
                if (condSkill != null && condSkill.Range > maxRange)
                    maxRange = (uint)condSkill.Range;
            }
            if (maxRange > 0)
                return maxRange;

            return config.Combat.AttackDistanceBow;
        }
    }
}
