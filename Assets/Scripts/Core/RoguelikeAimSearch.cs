using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SiegeDemo
{
    // Only attached for -solveRun. Replays real authored geometry without any abilities.
    public sealed class RoguelikeAimSearch : MonoBehaviour
    {
        private BattleController battle;
        private bool finished;
        private float began;
        private static readonly float[] Angles = { 5, 7.5f, 10, 12.5f, 15, 17.5f, 20, 25, 30, 35, 40, 50, 60, 65, 70, 75 };
        private IEnumerator Start()
        {
            began = Time.realtimeSinceStartup;
            battle = GetComponent<BattleController>();
            var args = Environment.GetCommandLineArgs();
            int selected = Array.IndexOf(args, "-level");
            var ids = selected >= 0 && selected + 1 < args.Length ? new[] { args[selected + 1] } : battle.RunData.campaignLevels;
            int solved = 0;
            foreach (string id in ids)
            {
                var authored = battle.ConfiguredLevel(id);
                var chosen = new List<float>();
                bool won = false;
                for (int shot = 0; shot < 3 && !won; shot++)
                {
                    float bestScore = float.NegativeInfinity, bestAngle = 5;
                    foreach (float angle in Angles)
                    {
                        JsonUtility.FromJsonOverwrite(LevelConfiguration.Serialize(authored), battle.Data);
                        battle.Restart();
                        Time.timeScale = 30;
                        while (battle.State == BattleState.Preparing) yield return null;
                        foreach (float previous in chosen)
                        {
                            if (battle.State != BattleState.Ready) break;
                            battle.TryLaunch(Shot(previous));
                            while (battle.State == BattleState.Resolving) yield return null;
                        }
                        if (battle.State == BattleState.Ready) battle.TryLaunch(Shot(angle));
                        while (battle.State == BattleState.Resolving) yield return null;
                        float score = -battle.TargetsRemaining * 10000;
                        string remaining = "";
                        foreach (var body in battle.World.GetComponentsInChildren<DamageableBody>())
                        {
                            if (body.IsIndestructible) continue;
                            score -= body.HitPoints * (body.IsTarget ? 100 : 1);
                            if (body.IsTarget) remaining += body.name + ":" + body.HitPoints.ToString("F2") + ",";
                        }
                        Debug.Log("RUN_AIM: level=" + id + " prefix=" + string.Join(",", chosen) + " angle=" + angle + " score=" + score + " remaining=" + remaining);
                        if (battle.State == BattleState.Won)
                        {
                            chosen.Add(angle);
                            Debug.Log("RUN_AIM_SOLUTION: " + id + " heavy=" + string.Join(",", chosen) + " abilities=none");
                            won = true; solved++; break;
                        }
                        if (score > bestScore) { bestScore = score; bestAngle = angle; }
                    }
                    if (!won) { chosen.Add(bestAngle); Debug.Log("RUN_AIM_PREFIX: " + id + " " + string.Join(",", chosen)); }
                }
                if (!won) Debug.LogError("RUN_AIM_NO_SOLUTION: " + id + " greedy search exhausted; not proof the level is impossible");
            }
            finished = true;
            Debug.Log("RUN_AIM_COMPLETE: solved=" + solved + "/" + ids.Length);
            Application.Quit(solved == ids.Length ? 0 : 3);
        }
        private LaunchSolution Shot(float angle)
        {
            float speed = battle.Data.launch.maxSpeed;
            return new LaunchSolution { valid = true, tier = 3, angle = angle, speed = speed,
                velocity = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * speed };
        }
        private void Update()
        {
            if (!finished && Time.realtimeSinceStartup - began > 900) { finished = true; Debug.LogError("RUN_AIM_TIMEOUT"); Application.Quit(4); }
        }
    }
}
