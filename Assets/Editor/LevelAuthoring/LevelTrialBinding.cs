using UnityEditor;
using UnityEngine;

namespace SiegeDemo.Editor
{
    // A one-level authoring test uses the controller's supported legacy entry, with no
    // run or reward mutation. Restore scene bindings after returning from Play.
    [InitializeOnLoad]
    internal static class LevelTrialBinding
    {
        private const string Prefix = "SiegeDemo.LevelTrial.";
        static LevelTrialBinding() { EditorApplication.playModeStateChanged += Restore; }

        internal static void Arm(BattleController controller)
        {
            var serialized = new SerializedObject(controller);
            SessionState.SetString(Prefix + "controller", GlobalObjectId.GetGlobalObjectIdSlow(controller).ToString());
            SessionState.SetString(Prefix + "level", AssetDatabase.GetAssetPath(serialized.FindProperty("levelSource").objectReferenceValue));
            var run = serialized.FindProperty("runSource");
            SessionState.SetString(Prefix + "run", run == null ? "" : AssetDatabase.GetAssetPath(run.objectReferenceValue));
        }
        private static void Restore(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            string id = SessionState.GetString(Prefix + "controller", "");
            if (string.IsNullOrEmpty(id)) return;
            SessionState.EraseString(Prefix + "controller");
            if (!GlobalObjectId.TryParse(id, out var globalId)) return;
            var controller = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) as BattleController;
            if (!controller) { Debug.LogWarning("LEVEL_TRIAL_BINDINGS: 原控制器已移除，无法恢复场景引用。"); return; }
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("levelSource").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(SessionState.GetString(Prefix + "level", ""));
            var run = serialized.FindProperty("runSource");
            if (run != null) run.objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(SessionState.GetString(Prefix + "run", ""));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("LEVEL_TRIAL_BINDINGS_RESTORED");
        }
    }
}
