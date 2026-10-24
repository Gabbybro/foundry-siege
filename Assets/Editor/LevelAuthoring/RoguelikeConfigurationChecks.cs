using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SiegeDemo.Editor
{
    public static class RoguelikeConfigurationChecks
    {
        [MenuItem("Siege Demo/Run/Validate configuration and nine levels")]
        public static void Run()
        {
            var run = RunConfiguration.Parse(File.ReadAllText(RunConfiguration.AssetPath));
            Require(RunConfiguration.Serialize(run) == RunConfiguration.Serialize(RunConfiguration.Parse(RunConfiguration.Serialize(run))), "all ability parameters roundtrip");
            var levels = new Dictionary<string, LevelData>(StringComparer.Ordinal);
            int[] targetCounts = { 2, 3, 3, 2, 4, 3, 2, 3, 2 };
            string[] ids = { "C01", "C02", "C03", "C04", "C05", "P01", "P02", "P03", "P04" };
            for (int i = 0; i < ids.Length; i++)
            {
                string path = "Assets/Content/Levels/" + ids[i] + ".json";
                LevelData level;
                try { level = LevelConfiguration.Parse(File.ReadAllText(path)); }
                catch (Exception error) { throw new FormatException(path + ": " + error.Message, error); }
                Require(level.version == 2 && level.id == ids[i] && level.ammunition == 3, path + " identity/version/ammo");
                Require(LevelConfiguration.Serialize(level) == LevelConfiguration.Serialize(LevelConfiguration.Parse(LevelConfiguration.Serialize(level))), path + " complete roundtrip");
                var expanded = LevelConfiguration.Expand(level);
                Require(expanded.Count(p => Array.Find(level.parts, part => part.id == p.partId).kind == PartKind.Target) == targetCounts[i], path + " target count");
                levels.Add(level.id, level);
                level.objects = expanded.ToArray();
                level.structures = Array.Empty<StructurePlacement>();
                Require(LevelConfiguration.Expand(LevelConfiguration.Parse(LevelConfiguration.Serialize(level))).Count == expanded.Count, path + " flattened roundtrip");
            }
            var errors = RunConfiguration.ValidateReferences(run, levels);
            Require(errors.Count == 0, string.Join("\n", errors));
            var shape = new PartDefinition { id = "shape", kind = PartKind.Beam, size = new Vector2(2, .45f), isStatic = true, indestructible = false };
            MaterialDefaults.Apply(shape, MaterialId.Glass);
            Require(shape.id == "shape" && shape.size == new Vector2(2, .45f) && shape.isStatic && !shape.indestructible && shape.mass == .65f && shape.hitPoints == 3, "material preset preserves geometry and flags");
            shape.mass = 8;
            Require(shape.mass == 8 && shape.hitPoints == 3, "independent override");
            levels.Remove("C05");
            Require(RunConfiguration.ValidateReferences(run, levels).Any(error => error.Contains("campaignLevels[4]")), "missing reference identifies field");
            string c01 = File.ReadAllText("Assets/Content/Levels/C01.json");
            RejectLevel(c01.Replace("\"material\": 1,", ""), "material");
            RejectLevel(c01.Replace("\"isGround\": false,", ""), "isGround");
            RejectLevel(c01.Replace("\"indestructible\": false", "\"indestructibleTypo\": false"), "indestructibleTypo");
            var overlap = LevelConfiguration.Parse(c01);
            overlap.objects[2].position = overlap.objects[1].position;
            Require(LevelConfiguration.Validate(overlap).Any(error => error.Contains("穿插")), "initial overlap rejected");
            run.abilities.dash.angleWidth = 361;
            Require(RunConfiguration.Validate(run).Any(error => error.Contains("abilities.dash.angleWidth")), "ability range error path");
            Debug.Log("ROGUELIKE_CONFIGURATION_CHECKS_SUCCESS: nine layouts, targets, full roundtrip, catalog, material defaults, v2 required fields, overlaps and ability errors");
        }

        [MenuItem("Siege Demo/Run/Check v2 scene material roundtrip")]
        public static void RunAuthoring()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("场景读写验证只能在 Edit 模式运行。");
            if (Array.Exists(UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects(), go => go.name == LevelAuthoringTools.PreviewName))
                throw new InvalidOperationException("验证需要没有 Level Preview 的活动场景，避免替换作者正在编辑的预览。");
            string source = File.ReadAllText("Assets/Content/Levels/P01.json");
            string directory = Path.GetFullPath(".setup/validation");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "v2-material-roundtrip.json");
            File.WriteAllText(path, source);
            GameObject preview = null;
            try
            {
                preview = LevelAuthoringTools.LoadFile(path);
                var outer = Array.Find(preview.GetComponentsInChildren<LevelObjectMarker>(), marker => marker.objectId == "outer-wall");
                Selection.activeGameObject = outer.gameObject;
                LevelAuthoringTools.ChangeSelectedMaterial(MaterialId.Glass);
                preview = Selection.activeGameObject.transform.root.gameObject;
                var draft = LevelAuthoringTools.CapturePreview();
                var item = Array.Find(draft.objects, placement => placement.id == "outer-wall");
                var part = Array.Find(draft.parts, definition => definition.id == item.partId);
                Require(part.material == MaterialId.Glass && part.isStatic && !part.indestructible && part.size == new Vector2(.24f, 3.6f), "selected object shape/fixed/destructibility");
                Require(File.ReadAllText(path) == source, "material draft never writes source automatically");
                LevelAuthoringTools.SavePreview(path);
                preview = LevelAuthoringTools.LoadFile(path);
                var saved = LevelAuthoringTools.CapturePreview();
                Require(saved.version == 2 && saved.id == "P01" && saved.templates.Length == draft.templates.Length && saved.parts.Length == draft.parts.Length, "saved v2 identity and libraries");
                Require(File.ReadAllText("Assets/Content/Levels/P01.json") == source, "original asset untouched");
                Debug.Log("V2_LEVEL_AUTHORING_ROUNDTRIP_SUCCESS: material switch, independent library entry, no draft overwrite, v2 save/load and original preservation");
            }
            finally { if (preview) UnityEngine.Object.DestroyImmediate(preview); }
        }
        private static void RejectLevel(string json, string path)
        {
            try { LevelConfiguration.Parse(json); }
            catch (FormatException error) { Require(error.Message.Contains(path), "expected path " + path); return; }
            throw new InvalidOperationException("Expected invalid field: " + path);
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("ROGUELIKE_CONFIGURATION_CHECK_FAILED: " + message); }
    }
}
