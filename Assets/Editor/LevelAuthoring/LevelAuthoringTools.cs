using System;
using System.Collections.Generic;
using System.IO;
using SiegeDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SiegeDemo.Editor
{
    public static class LevelAuthoringTools
    {
        public const string DefaultPath = "Assets/Content/DemoLevel.json";
        public const string PreviewName = "Level Preview";

        [MenuItem("Siege Demo/Level/Load Demo JSON")]
        private static void LoadDefaultMenu() { Report(() => LoadFile(DefaultPath)); }

        [MenuItem("Siege Demo/Level/Load JSON...")]
        private static void LoadMenu()
        {
            Report(() =>
            {
                RequireEditMode();
                string path = EditorUtility.OpenFilePanel("载入关卡 JSON", ContentDirectory(), "json");
                if (!string.IsNullOrEmpty(path)) LoadFile(path);
            });
        }

        [MenuItem("Siege Demo/Level/Save preview JSON")]
        private static void SaveMenu() { Report(() => SavePreview(GetPreview().sourcePath)); }

        [MenuItem("Siege Demo/Level/Save preview JSON as...")]
        private static void SaveAsMenu()
        {
            Report(() =>
            {
                RequireEditMode();
                GetPreview();
                string path = EditorUtility.SaveFilePanel("保存关卡 JSON", ContentDirectory(), "DemoLevel", "json");
                if (!string.IsNullOrEmpty(path)) SavePreview(path);
            });
        }

        [MenuItem("Siege Demo/Level/Validate JSON and preview")]
        private static void ValidateMenu()
        {
            Report(() =>
            {
                RequireEditMode();
                var selected = Selection.activeObject as TextAsset;
                if (selected) LevelConfiguration.Parse(selected.text);
                else if (FindPreview()) CapturePreview();
                else LevelConfiguration.Parse(File.ReadAllText(DefaultPath));
                Debug.Log("LEVEL_CONFIGURATION_VALID: JSON 字段、引用、展开 ID 与位置检查通过");
            });
        }

        [MenuItem("Siege Demo/Level/Reset preview to loaded snapshot")]
        public static void ResetPreview()
        {
            Report(() =>
            {
                RequireEditMode();
                var preview = GetPreview();
                LoadPreview(preview.initialJson, preview.sourcePath);
            });
        }

        [MenuItem("Siege Demo/Level/Reset and test saved JSON")]
        public static void ResetAndTestFire()
        {
            Report(() =>
            {
                RequireEditMode();
                var preview = GetPreview();
                string path = FullPath(preview.sourcePath);
                string assetPath = AssetPath(path);
                if (assetPath == null) throw new InvalidOperationException("试射需要 Assets 下的 JSON TextAsset；请先另存到 Assets/Content。");
                // Parse and check the session first. A failure must leave the preview intact.
                string json = File.ReadAllText(path);
                LevelConfiguration.Parse(json);
                var controllers = new List<BattleController>();
                foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                    controllers.AddRange(root.GetComponentsInChildren<BattleController>(true));
                if (controllers.Count != 1 || !controllers[0].gameObject.activeInHierarchy || !controllers[0].enabled)
                    throw new InvalidOperationException("试射需要当前正式场景内唯一且启用的 BattleController；请先打开 FoundryDemo。");
                var controller = new SerializedObject(controllers[0]);
                if (!controller.FindProperty("battleCamera").objectReferenceValue)
                    throw new InvalidOperationException("BattleController.battleCamera 未绑定，请先由场景生成工具配置正式场景。");
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                var source = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                if (!source) throw new InvalidOperationException("无法导入 JSON TextAsset: " + assetPath);
                LoadPreview(json, path);
                controller.FindProperty("levelSource").objectReferenceValue = source;
                controller.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(controllers[0].gameObject.scene);
                EditorApplication.isPlaying = true;
            });
        }

        public static GameObject LoadFile(string path)
        {
            RequireEditMode();
            string absolute = FullPath(path);
            return LoadPreview(File.ReadAllText(absolute), absolute);
        }

        public static GameObject LoadPreview(string json, string sourcePath)
        {
            RequireEditMode();
            LevelData data = LevelConfiguration.Parse(json);
            var previous = FindPreview();
            var scene = SceneManager.GetActiveScene();
            // Build a replacement first. Syntax, reference or factory errors preserve the
            // previously edited preview instead of leaving a half-built scene behind.
            var staging = new GameObject(PreviewName + " (loading)");
            SceneManager.MoveGameObjectToScene(staging, scene);
            try
            {
                DemoLevelFactory.Rebuild(data, staging.transform, false);
                var snapshot = staging.AddComponent<LevelPreviewSource>();
                snapshot.initialJson = json;
                snapshot.sourcePath = FullPath(sourcePath);
            }
            catch { UnityEngine.Object.DestroyImmediate(staging); throw; }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("载入关卡预览");
            if (previous) Undo.DestroyObjectImmediate(previous.gameObject);
            staging.name = PreviewName;
            Undo.RegisterCreatedObjectUndo(staging, "载入关卡预览");
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = staging;
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("LEVEL_PREVIEW_LOADED: " + sourcePath + ", ammo=" + data.ammunition + ", parts=" + staging.transform.childCount);
            return staging;
        }

        public static LevelData CapturePreview()
        {
            RequireEditMode();
            var preview = GetPreview();
            var data = LevelConfiguration.Parse(preview.initialJson);
            var parts = new Dictionary<string, PartDefinition>(StringComparer.Ordinal);
            foreach (var part in data.parts) parts.Add(part.id, part);
            var placements = new List<Placement>();
            foreach (var marker in preview.GetComponentsInChildren<LevelObjectMarker>(true))
            {
                string path = "preview['" + marker.objectId + "']";
                if (!marker.enabled || !marker.gameObject.activeInHierarchy) throw new FormatException(path + ": 不支持保存禁用的对象；请删除对象或重新启用。");
                if (marker.partId == null || !parts.TryGetValue(marker.partId, out var part)) throw new FormatException(path + ".partId: 找不到零件 '" + marker.partId + "'");
                var t = marker.transform;
                if (!Near(t.lossyScale.x, 1) || !Near(t.lossyScale.y, 1) || !Near(t.lossyScale.z, 1)) throw new FormatException(path + ".scale: 仅支持单位缩放；尺寸请修改 parts[].size 后重新载入。");
                if (!Near(t.position.z, 0) || Mathf.Abs(Vector3.Dot(t.forward, Vector3.forward) - 1) > .0001f) throw new FormatException(path + ".transform: 只支持 XY 平面内移动和 Z 轴旋转。");
                var collider = marker.GetComponent<BoxCollider2D>();
                if (marker.GetComponents<Collider2D>().Length != 1 || !collider || !collider.enabled || collider.isTrigger || !Near(collider.size.x, part.size.x) || !Near(collider.size.y, part.size.y) || !Near(collider.offset.x, 0) || !Near(collider.offset.y, 0))
                    throw new FormatException(path + ".collider: 碰撞尺寸/偏移必须与零件库一致；物理参数请在 JSON 中编辑。");
                if (!collider.sharedMaterial || !Near(collider.sharedMaterial.friction, part.friction) || !Near(collider.sharedMaterial.bounciness, part.bounciness))
                    throw new FormatException(path + ".material: 物理材质必须与零件库一致，请在 JSON 中编辑。");
                var body = marker.GetComponent<Rigidbody2D>();
                if (part.isStatic ? body != null : !body || body.bodyType != RigidbodyType2D.Dynamic || !Near(body.mass, part.mass) || body.simulated)
                    throw new FormatException(path + ".rigidbody: 请保留工具生成的预览刚体，质量/静态属性请在 JSON 中编辑。");
                placements.Add(new Placement { id = marker.objectId, partId = marker.partId, position = new Vector2(t.position.x, t.position.y), rotation = t.eulerAngles.z });
            }
            foreach (var collider in preview.GetComponentsInChildren<Collider2D>(true))
                if (!(collider is BoxCollider2D) || !collider.GetComponent<LevelObjectMarker>()) throw new FormatException("preview: 存在没有 LevelObjectMarker 的碰撞体，无法无损保存。");
            placements.Sort((left, right) => string.CompareOrdinal(left.id, right.id));
            data.objects = placements.ToArray();
            data.structures = Array.Empty<StructurePlacement>();
            // Validate before touching disk; this rejects duplicate copied markers and
            // a preview from which the last target was removed.
            var errors = LevelConfiguration.Validate(data);
            if (errors.Count > 0) throw new FormatException(string.Join("\n", errors));
            return data;
        }

        public static void SavePreview(string path)
        {
            RequireEditMode();
            var preview = GetPreview();
            string absolute = FullPath(path);
            if (!string.Equals(Path.GetExtension(absolute), ".json", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("保存路径必须使用 .json 扩展名", nameof(path));
            if (string.Equals(absolute, FullPath(preview.sourcePath), StringComparison.OrdinalIgnoreCase) && File.Exists(absolute) && File.ReadAllText(absolute) != preview.initialJson)
                throw new IOException("源 JSON 已被其他工具修改；请先重新载入，或另存新文件，避免覆盖外部改动: " + absolute);
            string json = LevelConfiguration.Serialize(CapturePreview());
            string directory = Path.GetDirectoryName(absolute);
            if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
            string temporary = absolute + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, json, new System.Text.UTF8Encoding(false));
                if (File.Exists(absolute)) File.Replace(temporary, absolute, null); else File.Move(temporary, absolute);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            Undo.RecordObject(preview, "更新关卡预览基线");
            preview.initialJson = json;
            preview.sourcePath = absolute;
            EditorUtility.SetDirty(preview);
            EditorSceneManager.MarkSceneDirty(preview.gameObject.scene);
            string assetPath = AssetPath(absolute);
            if (assetPath != null) AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("LEVEL_PREVIEW_SAVED: " + absolute);
        }

        private static LevelPreviewSource FindPreview()
        {
            LevelPreviewSource found = null;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name != PreviewName) continue;
                var candidate = root.GetComponent<LevelPreviewSource>();
                if (!candidate) throw new InvalidOperationException("场景中已有非工具创建的 '" + PreviewName + "'，请重命名后再载入。");
                if (found) throw new InvalidOperationException("当前场景存在多个 '" + PreviewName + "'，请先移除重复根节点。");
                found = candidate;
            }
            return found;
        }
        private static LevelPreviewSource GetPreview()
        {
            RequireEditMode();
            var value = FindPreview();
            if (!value) throw new InvalidOperationException("请先用 Siege Demo/Level/Load Demo JSON 载入预览。");
            return value;
        }
        private static bool Near(float left, float right) { return !float.IsNaN(left) && !float.IsInfinity(left) && Mathf.Abs(left - right) <= .0001f; }
        private static string FullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("JSON 路径不能为空", nameof(path));
            return Path.GetFullPath(path);
        }
        private static string AssetPath(string absolute)
        {
            string prefix = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
            return absolute.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? "Assets/" + absolute.Substring(prefix.Length).Replace('\\', '/') : null;
        }
        private static string ContentDirectory() { return Path.Combine(Application.dataPath, "Content"); }
        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("运行或切换 Play 模式期间禁止载入/保存关卡；请退出 Play 后操作，倒塌状态不能写回初始配置。");
        }
        private static void Report(Action action)
        {
            try { action(); }
            catch (Exception ex) { Debug.LogError("LEVEL_AUTHORING_FAILED: " + ex.Message); }
        }
    }
}
