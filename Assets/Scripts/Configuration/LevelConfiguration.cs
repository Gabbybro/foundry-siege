using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SiegeDemo
{
    public static class LevelConfiguration
    {
        private static readonly Regex Identifier = new Regex(@"\A[A-Za-z0-9_.-]+(?:/[A-Za-z0-9_.-]+)*\z", RegexOptions.CultureInvariant);

        public static LevelData Parse(string json)
        {
            // JsonUtility tolerates unknown fields and some malformed input. Check the full
            // document first so an AI typo cannot silently change the level's meaning.
            ConfigurationJson.Check(json);
            LevelData data;
            try { data = JsonUtility.FromJson<LevelData>(json.TrimStart('\uFEFF')); }
            catch (Exception ex) { throw new FormatException("$: 无法读取关卡 JSON: " + ex.Message, ex); }
            ThrowIfInvalid(data);
            return data;
        }

        public static string Serialize(LevelData data)
        {
            ThrowIfInvalid(data);
            return JsonUtility.ToJson(data, true) + "\n";
        }

        public static List<Placement> Expand(LevelData data)
        {
            ThrowIfInvalid(data);
            return ExpandUnchecked(data);
        }

        public static List<string> Validate(LevelData data)
        {
            var errors = new List<string>();
            if (data == null) { errors.Add("$: 关卡数据不能为空"); return errors; }
            if (data.version != 1 && data.version != 2) errors.Add("version: 仅支持版本 1 或 2");
            if (data.version == 2 && (string.IsNullOrEmpty(data.id) || !Identifier.IsMatch(data.id))) errors.Add("id: 版本 2 必须提供合法关卡 ID");
            if (string.IsNullOrWhiteSpace(data.name)) errors.Add("name: 关卡名称不能为空");
            if (data.ammunition < 1 || data.ammunition > 100) errors.Add("ammunition: 必须是 1～100 的整数");
            CheckVector(data.launcher, "launcher", errors);
            CheckFinite(data.killY, "killY", errors);
            CheckFinite(data.minX, "minX", errors);
            CheckFinite(data.maxX, "maxX", errors);
            if (data.minX >= data.maxX) errors.Add("minX/maxX: minX 必须小于 maxX");
            CheckPosition(data.launcher, "launcher", data, errors);
            CheckLaunch(data.launch, errors);
            CheckSettlement(data.settlement, errors);

            var partIds = new HashSet<string>(StringComparer.Ordinal);
            if (data.parts == null || data.parts.Length == 0) errors.Add("parts: 零件库不能为空");
            if (data.parts != null) for (int i = 0; i < data.parts.Length; i++)
            {
                string path = "parts[" + i + "]";
                var part = data.parts[i];
                if (part == null) { errors.Add(path + ": 零件不能为空"); continue; }
                CheckId(part.id, path + ".id", partIds, errors);
                if (!Enum.IsDefined(typeof(PartKind), part.kind)) errors.Add(path + ".kind: 未定义的零件种类");
                if (!Enum.IsDefined(typeof(MaterialId), part.material) || (data.version == 2 && part.material == MaterialId.Legacy)) errors.Add(path + ".material: 版本 2 只允许 Wood=1、Glass=2、Stone=3、Metal=4；Legacy=0 仅兼容旧关卡");
                Positive(part.size.x, path + ".size.x", errors);
                Positive(part.size.y, path + ".size.y", errors);
                Positive(part.mass, path + ".mass", errors);
                Positive(part.hitPoints, path + ".hitPoints", errors);
                Nonnegative(part.impulseThreshold, path + ".impulseThreshold", errors);
                Positive(part.damageMultiplier, path + ".damageMultiplier", errors);
                Range(part.friction, 0, 1, path + ".friction", errors);
                Range(part.bounciness, 0, 1, path + ".bounciness", errors);
                if (part.kind == PartKind.Target && part.isStatic) errors.Add(path + ".isStatic: 目标必须是动态刚体");
                if (data.version == 1 && part.kind == PartKind.Platform && !part.isStatic) errors.Add(path + ".isStatic: 旧版固定平台必须为 true");
                if (data.version == 2 && part.kind == PartKind.Target && part.indestructible) errors.Add(path + ".indestructible: 关卡目标不能不可破坏");
                if (data.version == 2 && part.kind == PartKind.Target && part.isGround) errors.Add(path + ".isGround: 目标不能标为地面");
                if (data.version == 2 && part.isGround && !part.isStatic) errors.Add(path + ".isGround: 地面必须固定，但 indestructible 独立设置");
            }

            var directIds = new HashSet<string>(StringComparer.Ordinal);
            CheckPlacements(data.objects, "objects", partIds, directIds, errors);
            var templateIds = new HashSet<string>(StringComparer.Ordinal);
            if (data.templates != null) for (int i = 0; i < data.templates.Length; i++)
            {
                string path = "templates[" + i + "]";
                var template = data.templates[i];
                if (template == null) { errors.Add(path + ": 模板不能为空"); continue; }
                CheckId(template.id, path + ".id", templateIds, errors);
                if (template.objects == null || template.objects.Length == 0) errors.Add(path + ".objects: 模板必须包含零件");
                CheckPlacements(template.objects, path + ".objects", partIds, new HashSet<string>(StringComparer.Ordinal), errors);
            }

            var structureIds = new HashSet<string>(StringComparer.Ordinal);
            if (data.structures != null) for (int i = 0; i < data.structures.Length; i++)
            {
                string path = "structures[" + i + "]";
                var structure = data.structures[i];
                if (structure == null) { errors.Add(path + ": 结构不能为空"); continue; }
                CheckId(structure.id, path + ".id", structureIds, errors);
                if (structure.templateId == null || !templateIds.Contains(structure.templateId)) errors.Add(path + ".templateId: 找不到模板 '" + structure.templateId + "'");
                CheckVector(structure.position, path + ".position", errors);
                CheckFinite(structure.rotation, path + ".rotation", errors);
            }

            // Only expand once all references and local values are valid. This also detects
            // clashes between a direct object ID and a generated structure/object ID.
            if (errors.Count == 0)
            {
                var expanded = ExpandUnchecked(data);
                var expandedIds = new HashSet<string>(StringComparer.Ordinal);
                bool hasTarget = false;
                var parts = new Dictionary<string, PartDefinition>(StringComparer.Ordinal);
                foreach (var part in data.parts) parts.Add(part.id, part);
                for (int i = 0; i < expanded.Count; i++)
                {
                    var item = expanded[i];
                    string path = "expanded['" + item.id + "']";
                    if (!expandedIds.Add(item.id)) errors.Add(path + ".id: 展开后 ID 重复");
                    CheckVector(item.position, path + ".position", errors);
                    CheckFinite(item.rotation, path + ".rotation", errors);
                    CheckPosition(item.position, path + ".position", data, errors);
                    if (parts[item.partId].kind == PartKind.Target) hasTarget = true;
                }
                if (!hasTarget) errors.Add("objects/structures: 关卡必须放置至少一个目标 (kind=1)");
                if (data.version == 2 && errors.Count == 0) CheckGeometry(expanded, parts, data, errors);
            }
            return errors;
        }

        private static void CheckGeometry(List<Placement> objects, Dictionary<string, PartDefinition> parts, LevelData data, List<string> errors)
        {
            bool hasGround = false;
            for (int i = 0; i < objects.Count; i++)
            {
                var a = objects[i];
                var partA = parts[a.partId];
                hasGround |= partA.isGround;
                double angle = (a.rotation % 360.0) * Math.PI / 180;
                double cosine = Math.Cos(angle), sine = Math.Sin(angle);
                double dx = data.launcher.x - a.position.x, dy = data.launcher.y - a.position.y;
                double localX = dx * cosine + dy * sine, localY = -dx * sine + dy * cosine;
                double distanceX = Math.Max(0, Math.Abs(localX) - partA.size.x / 2.0);
                double distanceY = Math.Max(0, Math.Abs(localY) - partA.size.y / 2.0);
                if (distanceX * distanceX + distanceY * distanceY < Math.Pow(Math.Max(0, data.launch.projectileRadius - .01), 2)) errors.Add("expanded['" + a.id + "']: 与 launcher 初始弹体重叠");
                for (int j = i + 1; j < objects.Count; j++)
                    if (Overlaps(a, partA, objects[j], parts[objects[j].partId])) errors.Add("expanded['" + a.id + "']/expanded['" + objects[j].id + "']: 初始构件明显穿插（容差 0.02m）");
            }
            if (!hasGround) errors.Add("parts[].isGround: 版本 2 必须放置至少一个显式地面");
        }
        private static bool Overlaps(Placement a, PartDefinition partA, Placement b, PartDefinition partB)
        {
            double aa = (a.rotation % 360.0) * Math.PI / 180, ab = (b.rotation % 360.0) * Math.PI / 180;
            double ax = Math.Cos(aa), ay = Math.Sin(aa), bx = Math.Cos(ab), by = Math.Sin(ab);
            double dx = (double)b.position.x - a.position.x, dy = (double)b.position.y - a.position.y;
            return Axis(ax, ay) && Axis(-ay, ax) && Axis(bx, by) && Axis(-by, bx);
            bool Axis(double x, double y)
            {
                double ra = partA.size.x * .5 * Math.Abs(ax * x + ay * y) + partA.size.y * .5 * Math.Abs(-ay * x + ax * y);
                double rb = partB.size.x * .5 * Math.Abs(bx * x + by * y) + partB.size.y * .5 * Math.Abs(-by * x + bx * y);
                return ra + rb - Math.Abs(dx * x + dy * y) > .02;
            }
        }

        private static List<Placement> ExpandUnchecked(LevelData data)
        {
            var result = new List<Placement>();
            if (data.objects != null) foreach (var item in data.objects)
                result.Add(new Placement { id = item.id, partId = item.partId, position = item.position, rotation = item.rotation });
            if (data.structures == null) return result;
            var templates = new Dictionary<string, StructureTemplate>(StringComparer.Ordinal);
            if (data.templates != null) foreach (var template in data.templates) templates.Add(template.id, template);
            foreach (var structure in data.structures)
            {
                // Reduce large finite angles before converting to radians; source data is not modified.
                double radians = (structure.rotation % 360.0) * Math.PI / 180.0;
                double cosine = Math.Cos(radians), sine = Math.Sin(radians);
                foreach (var item in templates[structure.templateId].objects)
                    result.Add(new Placement
                    {
                        id = structure.id + "/" + item.id,
                        partId = item.partId,
                        position = new Vector2((float)(structure.position.x + item.position.x * cosine - item.position.y * sine),
                            (float)(structure.position.y + item.position.x * sine + item.position.y * cosine)),
                        rotation = structure.rotation + item.rotation
                    });
            }
            return result;
        }

        private static void CheckPlacements(Placement[] values, string path, HashSet<string> parts, HashSet<string> ids, List<string> errors)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++)
            {
                string field = path + "[" + i + "]";
                var item = values[i];
                if (item == null) { errors.Add(field + ": 放置对象不能为空"); continue; }
                CheckId(item.id, field + ".id", ids, errors);
                if (item.partId == null || !parts.Contains(item.partId)) errors.Add(field + ".partId: 找不到零件 '" + item.partId + "'");
                CheckVector(item.position, field + ".position", errors);
                CheckFinite(item.rotation, field + ".rotation", errors);
            }
        }

        private static void CheckLaunch(LaunchSettings value, List<string> errors)
        {
            if (value == null) { errors.Add("launch: 发射参数不能为空"); return; }
            Positive(value.maxSpeed, "launch.maxSpeed", errors);
            Positive(value.maxDrag, "launch.maxDrag", errors);
            Positive(value.projectileMass, "launch.projectileMass", errors);
            Positive(value.projectileRadius, "launch.projectileRadius", errors);
            Range(value.cancelRatio, 0, 1, "launch.cancelRatio", errors);
            Range(value.mediumRatio, 0, 1, "launch.mediumRatio", errors);
            Range(value.heavyRatio, 0, 1, "launch.heavyRatio", errors);
            if (!(value.cancelRatio < value.mediumRatio && value.mediumRatio < value.heavyRatio && value.heavyRatio < 1))
                errors.Add("launch.cancelRatio/mediumRatio/heavyRatio: 必须满足 0 <= cancelRatio < mediumRatio < heavyRatio < 1");
            Range(value.minAngle, 0, 89.9f, "launch.minAngle", errors);
            Range(value.maxAngle, 0, 89.9f, "launch.maxAngle", errors);
            if (value.minAngle > value.maxAngle) errors.Add("launch.minAngle/maxAngle: 最小角度不能大于最大角度");
        }

        private static void CheckSettlement(SettlementSettings value, List<string> errors)
        {
            if (value == null) { errors.Add("settlement: 结算参数不能为空"); return; }
            Nonnegative(value.minSeconds, "settlement.minSeconds", errors);
            Positive(value.stableSeconds, "settlement.stableSeconds", errors);
            Positive(value.timeoutSeconds, "settlement.timeoutSeconds", errors);
            Nonnegative(value.linearThreshold, "settlement.linearThreshold", errors);
            Nonnegative(value.angularThreshold, "settlement.angularThreshold", errors);
            if (value.timeoutSeconds < 1 || value.timeoutSeconds < value.minSeconds || value.timeoutSeconds < value.stableSeconds)
                errors.Add("settlement.timeoutSeconds: 必须至少为 1 且不小于 minSeconds 和 stableSeconds");
        }

        private static void ThrowIfInvalid(LevelData data)
        {
            var errors = Validate(data);
            if (errors.Count > 0) throw new FormatException("关卡配置无效:\n" + string.Join("\n", errors));
        }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static void CheckFinite(float value, string path, List<string> errors) { if (!Finite(value)) errors.Add(path + ": 必须是有限数值"); }
        private static void Positive(float value, string path, List<string> errors) { if (!Finite(value) || value <= 0) errors.Add(path + ": 必须是大于 0 的有限数值"); }
        private static void Nonnegative(float value, string path, List<string> errors) { if (!Finite(value) || value < 0) errors.Add(path + ": 必须是非负有限数值"); }
        private static void Range(float value, float min, float max, string path, List<string> errors) { if (!Finite(value) || value < min || value > max) errors.Add(path + ": 必须介于 " + min.ToString(CultureInfo.InvariantCulture) + " 和 " + max.ToString(CultureInfo.InvariantCulture)); }
        private static void CheckVector(Vector2 value, string path, List<string> errors) { CheckFinite(value.x, path + ".x", errors); CheckFinite(value.y, path + ".y", errors); }
        private static void CheckPosition(Vector2 value, string path, LevelData data, List<string> errors)
        {
            if (value.x < data.minX || value.x > data.maxX || value.y <= data.killY) errors.Add(path + ": 初始位置必须位于 minX～maxX 且高于 killY");
        }
        private static void CheckId(string value, string path, HashSet<string> ids, List<string> errors)
        {
            if (string.IsNullOrEmpty(value) || !Identifier.IsMatch(value)) errors.Add(path + ": ID 必须由英文字母、数字、_、-、. 组成，可用 / 分段");
            else if (!ids.Add(value)) errors.Add(path + ": ID 重复 '" + value + "'");
        }
    }
}
