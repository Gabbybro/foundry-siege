using System;
using System.IO;
using SiegeDemo;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DemoSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/FoundryDemo.unity";
    public const string ConfigPath = "Assets/Content/DemoLevel.json";
    public const string RunConfigPath = "Assets/Content/RunConfiguration.json";
    public const string PlayerDirectory = "Builds/Windows-v0.2";
    [MenuItem("Siege Demo/Build/Generate playable scene")]
    public static void Configure()
    {
        var source = AssetDatabase.LoadAssetAtPath<TextAsset>(ConfigPath);
        if (!source) throw new InvalidOperationException("Missing " + ConfigPath);
        LevelConfiguration.Parse(source.text);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        var camera = go.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 8.2f;
        camera.transform.position = new Vector3(.6f, 1.8f, -10);
        camera.backgroundColor = new Color(.055f,.075f,.095f);
        go.AddComponent<AudioListener>();
        var controller = new GameObject("Battle Controller").AddComponent<BattleController>();
        controller.Configure(source, camera);
        var runSource = AssetDatabase.LoadAssetAtPath<TextAsset>(RunConfigPath);
        if (!runSource) throw new InvalidOperationException("Missing " + RunConfigPath);
        RunConfiguration.Parse(runSource.text);
        var paths = Directory.GetFiles("Assets/Content/Levels", "*.json");
        Array.Sort(paths, StringComparer.Ordinal);
        var levels = Array.ConvertAll(paths, path => AssetDatabase.LoadAssetAtPath<TextAsset>(path.Replace('\\', '/')));
        controller.ConfigureRun(runSource, levels);
        PlayerSettings.productName = "Foundry Siege";
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.runInBackground = true;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        Physics2D.gravity = new Vector2(0,-9.81f);
        Time.fixedDeltaTime = .01f;
        Physics2D.velocityIterations = 12;
        Physics2D.positionIterations = 8;
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Validate();
        Debug.Log("DEMO_SETUP_SUCCESS");
    }
    [MenuItem("Siege Demo/Build/Validate")]
    public static void Validate()
    {
        if (Application.unityVersion != "6000.3.27f1") throw new InvalidOperationException("Pinned editor required.");
        if (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline) throw new InvalidOperationException("Built-In pipeline required.");
        var data = LevelConfiguration.Parse(File.ReadAllText(ConfigPath));
        var roundTrip = LevelConfiguration.Parse(LevelConfiguration.Serialize(data));
        if (LevelConfiguration.Expand(data).Count != LevelConfiguration.Expand(roundTrip).Count) throw new InvalidOperationException("Config roundtrip mismatch");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var controller = UnityEngine.Object.FindFirstObjectByType<BattleController>();
        if (!controller) throw new InvalidOperationException("Missing BattleController");
        var serialized = new SerializedObject(controller);
        if (!serialized.FindProperty("levelSource").objectReferenceValue || !serialized.FindProperty("battleCamera").objectReferenceValue) throw new InvalidOperationException("Unbound battle references");
        foreach (var root in scene.GetRootGameObjects()) foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0) throw new InvalidOperationException("Missing script " + transform.name);
        DemoValidation.Run(data);
        SiegeDemo.Editor.LevelConfigurationChecks.Run();
        LevelRoundTripValidation.Run(data);
        RoguelikeValidation.Run();
        Debug.Log("PROJECT_VALIDATION_SUCCESS");
    }
    [MenuItem("Siege Demo/Build/Windows")]
    public static void BuildWindows()
    {
        Validate();
        Directory.CreateDirectory(PlayerDirectory);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] {ScenePath}, locationPathName=PlayerDirectory + "/FoundrySiege.exe", target=BuildTarget.StandaloneWindows64, options=BuildOptions.Development | BuildOptions.StrictMode });
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Build failed " + report.summary.result);
        Debug.Log("WINDOWS_BUILD_SUCCESS: " + report.summary.totalSize);
    }
}
