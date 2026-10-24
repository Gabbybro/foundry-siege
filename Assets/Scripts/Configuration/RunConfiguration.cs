using System;
using System.Collections.Generic;
using UnityEngine;

namespace SiegeDemo
{
    public static class RunConfiguration
    {
        public const string AssetPath = "Assets/Content/RunConfiguration.json";
        public static RunConfigurationData Parse(string json)
        {
            ConfigurationJson.Check(json, typeof(RunConfigurationData));
            RunConfigurationData data;
            try { data = JsonUtility.FromJson<RunConfigurationData>(json.TrimStart('\uFEFF')); }
            catch (Exception error) { throw new FormatException("$: 无法读取整轮配置: " + error.Message, error); }
            ThrowIfInvalid(data);
            return data;
        }
        public static string Serialize(RunConfigurationData data)
        {
            ThrowIfInvalid(data);
            return JsonUtility.ToJson(data, true) + "\n";
        }
        public static List<string> Validate(RunConfigurationData data)
        {
            var errors = new List<string>();
            if (data == null) { errors.Add("$: 整轮配置不能为空"); return errors; }
            if (data.version != 1) errors.Add("version: 整轮配置仅支持版本 1（关卡配置使用版本 2）");
            if (data.campaignLevels == null || data.campaignLevels.Length != 5) errors.Add("campaignLevels: 正式路线必须包含 5 关 C01～C05");
            if (data.campaignLevels != null)
            {
                var ids = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < data.campaignLevels.Length; i++)
                {
                    string path = "campaignLevels[" + i + "]";
                    if (data.campaignLevels[i] != "C" + (i + 1).ToString("00")) errors.Add(path + ": 本版正式路线依次为 C01→C02→C03→C04→C05");
                    if (!ids.Add(data.campaignLevels[i] ?? "")) errors.Add(path + ": 关卡 ID 重复");
                }
            }
            if (data.practices == null || data.practices.Length != 4) errors.Add("practices: 必须包含四个独立练习关 P01～P04");
            if (data.practices != null)
            {
                var ids = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < data.practices.Length; i++)
                {
                    string path = "practices[" + i + "]";
                    var practice = data.practices[i];
                    if (practice == null) { errors.Add(path + ": 练习定义不能为空"); continue; }
                    if (practice.levelId != "P01" && practice.levelId != "P02" && practice.levelId != "P03" && practice.levelId != "P04") errors.Add(path + ".levelId: 未知练习 ID，仅支持 P01～P04");
                    if (!ids.Add(practice.levelId ?? "")) errors.Add(path + ".levelId: 练习 ID 重复");
                    if (string.IsNullOrWhiteSpace(practice.name)) errors.Add(path + ".name: 练习名称不能为空");
                    if (string.IsNullOrWhiteSpace(practice.instructions)) errors.Add(path + ".instructions: 必须提供操作说明");
                    CheckBuild(practice.build, path + ".build", errors);
                }
            }
            CheckAbilities(data.abilities, errors);
            return errors;
        }

        // Runtime callers already holding the loaded catalog can validate references without
        // any Editor/File dependency. No fallback to an empty or unrelated level is allowed.
        public static List<string> ValidateReferences(RunConfigurationData data, IDictionary<string, LevelData> levels)
        {
            var errors = Validate(data);
            if (levels == null) { errors.Add("levels: 缺少已载入的关卡目录"); return errors; }
            if (data == null) return errors;
            if (data.campaignLevels != null) for (int i = 0; i < data.campaignLevels.Length; i++)
                CheckLevel(data.campaignLevels[i], "campaignLevels[" + i + "]", levels, errors);
            if (data.practices != null) for (int i = 0; i < data.practices.Length; i++)
                if (data.practices[i] != null) CheckLevel(data.practices[i].levelId, "practices[" + i + "].levelId", levels, errors);
            return errors;
        }
        private static void CheckLevel(string id, string path, IDictionary<string, LevelData> levels, List<string> errors)
        {
            if (id == null || !levels.TryGetValue(id, out var level) || level == null) { errors.Add(path + ": 找不到关卡 '" + id + "'"); return; }
            if (level.id != id) errors.Add(path + ": 目录键与 LevelData.id 不一致 '" + id + "'");
            if (level.ammunition != 3) errors.Add(path + ".ammunition: 本版正式及练习关均为 3 发");
            foreach (string error in LevelConfiguration.Validate(level)) errors.Add("levels['" + id + "']." + error);
        }
        private static void CheckBuild(BuildLoadout build, string path, List<string> errors)
        {
            if (build == null) { errors.Add(path + ": 默认构筑不能为空"); return; }
            if (build.active != ActiveAbility.Split && build.active != ActiveAbility.Dash) errors.Add(path + ".active: 必须为 Split=1 或 Dash=2");
            if (build.passives == null) { errors.Add(path + ".passives: 被动列表不能为空，可用 []"); return; }
            var passives = new HashSet<PassiveAbility>();
            for (int i = 0; i < build.passives.Length; i++)
            {
                if (!Enum.IsDefined(typeof(PassiveAbility), build.passives[i])) errors.Add(path + ".passives[" + i + "]: 未知被动 ID");
                if (!passives.Add(build.passives[i])) errors.Add(path + ".passives[" + i + "]: 被动重复");
            }
        }
        private static void CheckAbilities(AbilitySettings value, List<string> errors)
        {
            if (value == null) { errors.Add("abilities: 能力参数不能为空"); return; }
            if (value.split == null) errors.Add("abilities.split: 参数不能为空");
            else
            {
                Positive(value.split.speed, "abilities.split.speed", errors);
                Range(value.split.spreadAngle, 0, 89.9f, "abilities.split.spreadAngle", errors);
                Positive(value.split.radiusScale, "abilities.split.radiusScale", errors);
                if (value.split.radiusScale >= 1) errors.Add("abilities.split.radiusScale: 子弹应小于母体，必须小于 1");
            }
            if (value.dash == null) errors.Add("abilities.dash: 参数不能为空");
            else
            {
                Positive(value.dash.speedIncrement, "abilities.dash.speedIncrement", errors);
                Finite(value.dash.centerAngle, "abilities.dash.centerAngle", errors);
                Positive(value.dash.angleWidth, "abilities.dash.angleWidth", errors);
                if (value.dash.angleWidth > 360) errors.Add("abilities.dash.angleWidth: 不可大于 360 度");
            }
            if (value.piercing == null) errors.Add("abilities.piercing: 参数不能为空");
            else
            {
                Nonnegative(value.piercing.minimumSpeed, "abilities.piercing.minimumSpeed", errors);
                Positive(value.piercing.maximumThickness, "abilities.piercing.maximumThickness", errors);
                Range(value.piercing.speedRetention, 0, 1, "abilities.piercing.speedRetention", errors);
            }
            if (value.groundBounce == null) errors.Add("abilities.groundBounce: 参数不能为空");
            else
            {
                Nonnegative(value.groundBounce.verticalMultiplier, "abilities.groundBounce.verticalMultiplier", errors);
                Positive(value.groundBounce.minimumUpSpeed, "abilities.groundBounce.minimumUpSpeed", errors);
                Range(value.groundBounce.horizontalRetention, 0, 1, "abilities.groundBounce.horizontalRetention", errors);
            }
            if (value.stoneRicochet == null) errors.Add("abilities.stoneRicochet: 参数不能为空");
            else
            {
                Range(value.stoneRicochet.maximumGlancingAngle, 0, 89.9f, "abilities.stoneRicochet.maximumGlancingAngle", errors);
                Range(value.stoneRicochet.speedRetention, 0, 1, "abilities.stoneRicochet.speedRetention", errors);
                Positive(value.stoneRicochet.minimumUpSpeed, "abilities.stoneRicochet.minimumUpSpeed", errors);
            }
            if (value.apexDive == null) errors.Add("abilities.apexDive: 参数不能为空");
            else
            {
                Positive(value.apexDive.downSpeed, "abilities.apexDive.downSpeed", errors);
                Range(value.apexDive.horizontalRetention, 0, 1, "abilities.apexDive.horizontalRetention", errors);
            }
            if (value.rest == null) errors.Add("abilities.rest: 参数不能为空");
            else
            {
                Positive(value.rest.stableSeconds, "abilities.rest.stableSeconds", errors);
                Positive(value.rest.countdownSeconds, "abilities.rest.countdownSeconds", errors);
                Nonnegative(value.rest.maximumSpeed, "abilities.rest.maximumSpeed", errors);
                Nonnegative(value.rest.maximumAngularSpeed, "abilities.rest.maximumAngularSpeed", errors);
            }
            Radial(value.explosion, "abilities.explosion", errors);
            Radial(value.deathShock, "abilities.deathShock", errors);
            if (value.shards == null) errors.Add("abilities.shards: 参数不能为空");
            else
            {
                Positive(value.shards.speed, "abilities.shards.speed", errors);
                Range(value.shards.spreadAngle, 0, 180, "abilities.shards.spreadAngle", errors);
                Positive(value.shards.mass, "abilities.shards.mass", errors);
                Positive(value.shards.radius, "abilities.shards.radius", errors);
                Positive(value.shards.lifetime, "abilities.shards.lifetime", errors);
            }
        }
        private static void Radial(RadialEffectSettings value, string path, List<string> errors)
        {
            if (value == null) { errors.Add(path + ": 参数不能为空"); return; }
            Positive(value.radius, path + ".radius", errors);
            Nonnegative(value.damage, path + ".damage", errors);
            Nonnegative(value.impulse, path + ".impulse", errors);
            Positive(value.falloffPower, path + ".falloffPower", errors);
        }
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Finite(float value, string path, List<string> errors) { if (!IsFinite(value)) errors.Add(path + ": 必须是有限数值"); }
        private static void Positive(float value, string path, List<string> errors) { if (!IsFinite(value) || value <= 0) errors.Add(path + ": 必须是大于 0 的有限数值"); }
        private static void Nonnegative(float value, string path, List<string> errors) { if (!IsFinite(value) || value < 0) errors.Add(path + ": 必须是非负有限数值"); }
        private static void Range(float value, float min, float max, string path, List<string> errors) { if (!IsFinite(value) || value < min || value > max) errors.Add(path + ": 数值超出允许范围 " + min + "～" + max); }
        private static void ThrowIfInvalid(RunConfigurationData data)
        {
            var errors = Validate(data);
            if (errors.Count > 0) throw new FormatException("整轮配置无效:\n" + string.Join("\n", errors));
        }
    }
}
