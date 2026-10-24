using System;
using System.Collections.Generic;
using System.IO;
using SiegeDemo;
using UnityEditor;
using UnityEngine;

public static class RoguelikeValidation
{
    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("ROGUELIKE_VALIDATION: " + message);
    }
    public static void Run()
    {
        var run = RunConfiguration.Parse(File.ReadAllText(DemoSceneBuilder.RunConfigPath));
        Require(run.campaignLevels.Length == 5 && run.practices.Length == 4, "five campaign and four practice entries");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in run.campaignLevels) Require(ids.Add(id), "duplicate route id " + id);
        foreach (var practice in run.practices) Require(ids.Add(practice.levelId), "duplicate practice id " + practice.levelId);
        foreach (string id in ids)
        {
            string path = "Assets/Content/Levels/" + id + ".json";
            var data = LevelConfiguration.Parse(File.ReadAllText(path));
            Require(data.id == id && data.ammunition == 3, path + " ID/ammunition");
            Require(LevelConfiguration.Expand(data).Exists(p => Array.Find(data.parts, part => part.id == p.partId).kind == PartKind.Target), path + " targets");
        }
        var controller = UnityEngine.Object.FindFirstObjectByType<BattleController>();
        var serialized = new SerializedObject(controller);
        Require(serialized.FindProperty("runSource").objectReferenceValue, "run config reference");
        var sources = serialized.FindProperty("levelSources");
        Require(sources.arraySize == 9, "nine scene level references");
        for (int i = 0; i < sources.arraySize; i++) Require(sources.GetArrayElementAtIndex(i).objectReferenceValue, "null scene level reference " + i);

        // Exercise pool depletion and snapshot restoration across all four reward draws.
        for (uint seed = 1; seed <= 40; seed++)
        {
            var random = new RewardRandom(seed);
            var owned = new List<PassiveAbility>();
            for (int stage = 0; stage < 4; stage++)
            {
                uint snapshot = random.State;
                var options = random.DrawThree(owned.ToArray());
                var restored = new RewardRandom(snapshot);
                var repeated = restored.DrawThree(owned.ToArray());
                Require(new HashSet<PassiveAbility>(options).Count == 3, "distinct reward candidates");
                for (int i = 0; i < 3; i++) Require(!owned.Contains(options[i]) && options[i] == repeated[i], "owned filtering and deterministic retry");
                Require(random.State == restored.State, "reward state restored after draw");
                owned.Add(options[(int)(seed % 3)]);
            }
            Require(owned.Count == 4, "four distinct rewards");
        }
        Debug.Log("ROGUELIKE_CONFIG_SUCCESS: nine referenced levels, route, strict configs, deterministic reward snapshots");
    }
}
