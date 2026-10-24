using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SiegeDemo.Editor
{
    // Read-only checks for the real Unity serializer. The manager can call Run() from
    // integration validation without creating a scene or entering Play mode.
    public static class LevelConfigurationChecks
    {
        [MenuItem("Siege Demo/Level/Run configuration checks")]
        public static void Run()
        {
            string source = File.ReadAllText(LevelAuthoringTools.DefaultPath);
            var original = LevelConfiguration.Parse(source);
            var roundtrip = LevelConfiguration.Parse(LevelConfiguration.Serialize(original));
            Require(LevelConfiguration.Expand(original).Select(p => p.id).SequenceEqual(LevelConfiguration.Expand(roundtrip).Select(p => p.id)), "roundtrip IDs");
            Require(LevelConfiguration.Serialize(original) == LevelConfiguration.Serialize(roundtrip), "roundtrip all serialized parameters");
            Require(LevelConfiguration.Parse("\uFEFF" + source).ammunition == original.ammunition, "UTF-8 BOM");
            int count = LevelConfiguration.Expand(original).Count;
            Require(count == LevelConfiguration.Expand(original).Count, "repeated expansion count");
            var expanded = LevelConfiguration.Expand(original);
            expanded[0].id = "changed";
            Require(original.objects[0].id != "changed", "independent expansion objects");

            var rotated = new LevelData
            {
                parts = new[] { new PartDefinition { id = "target", kind = PartKind.Target } },
                objects = Array.Empty<Placement>(),
                templates = new[] { new StructureTemplate { id = "pair", objects = new[] { new Placement { id = "local", partId = "target", position = new Vector2(2, 1), rotation = 15 } } } },
                structures = new[] { new StructurePlacement { id = "group", templateId = "pair", position = new Vector2(5, 0), rotation = 90 } }
            };
            var transformed = LevelConfiguration.Expand(rotated)[0];
            Require(transformed.id == "group/local" && Vector2.Distance(transformed.position, new Vector2(4, 2)) < .0001f && transformed.rotation == 105, "rotation, translation and composed ID");
            rotated.objects = new[] { new Placement { id = "group/local", partId = "target", position = Vector2.zero } };
            Require(LevelConfiguration.Validate(rotated).Any(error => error.Contains("展开后 ID 重复")), "composed ID collision");

            var flattened = LevelConfiguration.Parse(LevelConfiguration.Serialize(original));
            flattened.objects = LevelConfiguration.Expand(flattened).ToArray();
            flattened.structures = Array.Empty<StructurePlacement>();
            var saved = LevelConfiguration.Parse(LevelConfiguration.Serialize(flattened));
            Require(LevelConfiguration.Expand(saved).Count == count && saved.parts.Length == original.parts.Length && saved.templates.Length == original.templates.Length && saved.launch.maxSpeed == original.launch.maxSpeed && saved.ammunition == original.ammunition, "flatten preserves libraries and settings");
            Reject(original, data => data.ammunition = 0, "ammunition");
            Reject(original, data => data.objects[0].partId = "missing", "objects[0].partId");
            Reject(original, data => data.parts[1].id = data.parts[0].id, "parts[1].id");
            Reject(original, data => data.parts[0].mass = float.NaN, "parts[0].mass");
            Reject(original, data => data.launch.maxSpeed = float.PositiveInfinity, "launch.maxSpeed");
            Reject(original, data => data.structures[0].templateId = "missing", "structures[0].templateId");
            Reject(original, data => data.structures[1].id = data.structures[0].id, "structures[1].id");
            RejectJson(source.Replace("\"ammunition\"", "\"amunition\""), "amunition");
            RejectJson(source + " trailing", "$");
            RejectJson(source.Replace("\"version\": 1,", "\"version\": 1, \"version\": 1,"), "version");
            RejectJson("null", "$");
            Require(LevelConfiguration.Parse(source).ammunition == original.ammunition, "reloading restores baseline");
            Debug.Log("LEVEL_CONFIGURATION_CHECKS_SUCCESS: roundtrip, rotation, immutable expansion, duplicate IDs, reset, invalid JSON and references (real JsonUtility)");
            if (File.Exists(RunConfiguration.AssetPath)) RoguelikeConfigurationChecks.Run();
        }
        private static void Reject(LevelData baseline, Action<LevelData> mutation, string path)
        {
            var data = LevelConfiguration.Parse(LevelConfiguration.Serialize(baseline));
            mutation(data);
            Require(LevelConfiguration.Validate(data).Any(error => error.Contains(path)), "expected invalid field: " + path);
        }
        private static void RejectJson(string json, string path)
        {
            try { LevelConfiguration.Parse(json); }
            catch (FormatException error) { Require(error.Message.Contains(path), "error should identify " + path); return; }
            throw new InvalidOperationException("CONFIGURATION_CHECK_FAILED: malformed JSON was accepted");
        }
        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("CONFIGURATION_CHECK_FAILED: " + message);
        }
    }
}
