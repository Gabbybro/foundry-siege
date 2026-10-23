using System;
using System.IO;
using BirdReplica;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BirdReplica.EditorTools
{
    public static class ProjectSetup
    {
        private const string ScenePath = "Assets/Scenes/InstallationCheck.unity";

        [MenuItem("Bird Replica/Setup/Create installation check scene")]
        public static void Configure()
        {
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Art/Generated");
            EditorSettings.serializationMode = SerializationMode.ForceText;
            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
            PlayerSettings.companyName = "Gabby";
            PlayerSettings.productName = "Bird Replica";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.gabby.birdreplica");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            Time.fixedDeltaTime = 0.02f;
            Physics2D.gravity = new Vector2(0f, -9.81f);
            AssetDatabase.Refresh();

            // Do not replace an existing scene when this command is run again.
            if (!File.Exists(ScenePath)) CreateScene();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("PROJECT_SETUP_SUCCESS");
        }

        private static Sprite MakeSprite(string name, bool circle)
        {
            string path = "Assets/Art/Generated/" + name + ".png";
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                bool visible = !circle || new Vector2(x - 31.5f, y - 31.5f).sqrMagnitude < 31.5f * 31.5f;
                texture.SetPixel(x, y, visible ? Color.white : Color.clear);
            }
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 64;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static GameObject Body(string name, Sprite sprite, Vector2 position, Vector2 size,
            Color color, bool dynamic, bool circle = false)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            if (circle) go.AddComponent<CircleCollider2D>();
            else go.AddComponent<BoxCollider2D>();
            if (dynamic)
            {
                var rigidbody = go.AddComponent<Rigidbody2D>();
                rigidbody.mass = 1f;
                rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
                rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }
            return go;
        }

        private static void CreateScene()
        {
            var square = MakeSprite("Square", false);
            var circle = MakeSprite("Circle", true);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.79f, 0.94f);
            cameraObject.AddComponent<AudioListener>();

            Body("Ground", square, new Vector2(0, -3.5f), new Vector2(22, 1), new Color(0.32f, 0.5f, 0.22f), false);
            var wood = new Color(0.66f, 0.38f, 0.18f);
            Body("Wood_Left", square, new Vector2(2.2f, -2.2f), new Vector2(0.45f, 1.6f), wood, true);
            Body("Wood_Right", square, new Vector2(3.8f, -2.2f), new Vector2(0.45f, 1.6f), wood, true);
            Body("Wood_Top", square, new Vector2(3, -1.2f), new Vector2(2.2f, 0.35f), wood, true);
            Body("Target", circle, new Vector2(3, -2.6f), new Vector2(0.8f, 0.8f), new Color(0.4f, 0.8f, 0.2f), true, true);
            var bird = Body("Bird", circle, new Vector2(-6, -2.3f), new Vector2(0.7f, 0.7f), new Color(0.85f, 0.15f, 0.12f), true, true);
            var body = bird.GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            var probe = bird.AddComponent<BirdImpactProbe>();
            new GameObject("Installation Demo").AddComponent<InstallationDemo>().Configure(body, probe);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        [MenuItem("Bird Replica/Setup/Validate project")]
        public static void Validate()
        {
            if (Application.unityVersion != "6000.3.27f1")
                throw new InvalidOperationException("Use the pinned editor: 6000.3.27f1.");
            if (!File.Exists(ScenePath)) throw new InvalidOperationException("Run project setup first.");
            if (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null)
                throw new InvalidOperationException("This starter project uses the Built-In Render Pipeline.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            if (UnityEngine.Object.FindFirstObjectByType<InstallationDemo>() == null)
                throw new InvalidOperationException("Missing installation demo component.");
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0)
                    throw new InvalidOperationException("Missing script on " + child.name);
            Debug.Log("PROJECT_VALIDATION_SUCCESS");
        }

        [MenuItem("Bird Replica/Build/Windows development build")]
        public static void BuildWindows()
        {
            Validate();
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Windows/BirdReplica.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development | BuildOptions.StrictMode
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Build failed: " + report.summary.result);
            Debug.Log("WINDOWS_BUILD_SUCCESS: " + report.summary.totalSize + " bytes");
        }
    }
}
