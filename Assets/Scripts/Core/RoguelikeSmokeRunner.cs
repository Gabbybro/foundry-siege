using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SiegeDemo
{
    // Explicit development-player verification, using the same game commands as the HUD.
    public sealed partial class RoguelikeSmokeRunner : MonoBehaviour
    {
        private BattleController battle;
        private float began;
        private bool done;
        private IEnumerator Start()
        {
            began = Time.realtimeSinceStartup;
            battle = GetComponent<BattleController>();
            var stack = new Stack<IEnumerator>();
            stack.Push(Run());
            while (stack.Count > 0)
            {
                object current = null;
                bool moved = false;
                try { moved = stack.Peek().MoveNext(); if (moved) current = stack.Peek().Current; }
                catch (Exception ex) { Debug.LogError("ROGUELIKE_SMOKE_FAILED: " + ex); done = true; Application.Quit(2); yield break; }
                if (!moved) { stack.Pop(); continue; }
                if (current is IEnumerator nested) { stack.Push(nested); continue; }
                yield return current;
            }
            done = true;
            Debug.Log("ROGUELIKE_SMOKE_SUCCESS: physics, two actives, retries, pause, five-stage flow, nine openings, independent practice");
            Application.Quit(0);
        }
        private IEnumerator Run()
        {
            Check(battle.Phase == RunPhase.MainMenu, "player starts at menu");
            Time.timeScale = 8;
            yield return PhysicsRegression.Run(Check);
            battle.StartCampaign();
            Check(battle.Phase == RunPhase.ActiveSelection && battle.Build.passives.Length == 0, "campaign selects active first");
            Check(battle.SelectActive(ActiveAbility.Split), "select split");
            yield return Ready();
            yield return CheckOpening();
            Check(!battle.TryLaunch(default) && battle.Ammunition == 3, "invalid shot preserves ammo");
            Check(battle.TryLaunch(Shot(3, 45)), "split launch");
            Check(!battle.TryLaunch(Shot(3, 45)), "double launch rejected");
            yield return new WaitForSeconds(.16f);
            var original = Projectile();
            float mass = original.mass;
            Vector2 direction = original.linearVelocity.normalized;
            Check(battle.TryUseActive(original.position + Vector2.left * 5), "split in flight");
            Check(battle.ActiveProjectiles == 3 && battle.Combat.CurrentShot.activeUsed, "three children and one shared active charge");
            float totalMass = 0;
            int count = 0;
            foreach (var body in battle.World.GetComponentsInChildren<Rigidbody2D>())
            {
                if (body.bodyType != RigidbodyType2D.Dynamic || body.GetComponent<DamageableBody>()) continue;
                totalMass += body.mass; count++;
                Check(Mathf.Abs(body.mass - mass / 3) < .001f, "child mass fraction");
                Check(Mathf.Abs(body.linearVelocity.magnitude - battle.RunData.abilities.split.speed) < .01f, "fixed split speed");
                Check(Vector2.Angle(body.linearVelocity, direction) <= battle.RunData.abilities.split.spreadAngle + .1f, "split follows incident direction");
            }
            Check(count == 3 && Mathf.Abs(totalMass - mass) < .001f, "total split mass conserved");
            Check(!battle.TryUseActive(Vector2.right * 10), "children cannot reuse active");
            var oldShot = battle.Combat.CurrentShot;
            int previousGeneration = battle.Generation;
            uint entryRng = battle.RewardRandomState;
            battle.RetryLevel();
            yield return Ready();
            Check(battle.Generation > previousGeneration && oldShot.ended && battle.ActiveProjectiles == 0, "retry invalidates previous split generation");
            Check(battle.Ammunition == 3 && battle.Build.active == ActiveAbility.Split && battle.RewardRandomState == entryRng, "retry restores entry and rng");

            battle.RestartRun();
            Check(battle.Phase == RunPhase.ActiveSelection && battle.Build.active == ActiveAbility.None && battle.Build.passives.Length == 0, "restart run clears build");
            Check(battle.SelectActive(ActiveAbility.Dash), "select dash");
            yield return Ready();
            Check(battle.TryLaunch(Shot(3, 45)), "dash launch");
            yield return new WaitForSeconds(.16f);
            original = Projectile();
            var dash = battle.RunData.abilities.dash;
            float savedWidth = dash.angleWidth;
            float savedCenter = dash.centerAngle;
            dash.angleWidth = 90; dash.centerAngle = 0;
            Check(!battle.TryUseActive(original.position + Vector2.left * 10) && !battle.Combat.CurrentShot.activeUsed, "invalid dash direction preserves charge");
            dash.angleWidth = savedWidth; dash.centerAngle = savedCenter;
            float speed = original.linearVelocity.magnitude;
            Check(battle.TryUseActive(original.position + Vector2.down * 10), "downward dash");
            Check(Mathf.Abs(original.linearVelocity.x) < .01f && Mathf.Abs(original.linearVelocity.y + speed + dash.speedIncrement) < .01f, "dash redirects current speed plus increment");
            float downward = original.linearVelocity.y;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Check(original && original.linearVelocity.y < downward, "gravity continues after dash");
            battle.RetryLevel();
            yield return Ready();
            Check(battle.TryLaunch(Shot(3, 45)), "pause fixture");
            yield return new WaitForSeconds(.12f);
            original = Projectile();
            battle.SetPaused(true);
            Vector2 pausedPosition = original.position;
            float pausedTime = Time.time;
            yield return new WaitForSecondsRealtime(.15f);
            Check(Time.timeScale == 0 && Time.time == pausedTime && Vector2.Distance(original.position, pausedPosition) < .00001f, "pause freezes physics and timers");
            Check(!battle.TryUseActive(original.position + Vector2.right * 5), "paused active rejected");
            battle.SetPaused(false);
            battle.RetryLevel();
            yield return Ready();
            for (int i = 0; i < 3; i++)
            {
                Check(battle.TryLaunch(Shot(1, 75)), "deliberate short miss");
                Check(battle.State == BattleState.Resolving, "final shot delays failure");
                yield return Settled();
            }
            Check(battle.State == BattleState.Lost && battle.ResultCount == 1, "failure only after last shot settles");
            var failureBuild = battle.Build;
            uint failureRng = battle.RewardRandomState;
            battle.RetryLevel();
            yield return Ready();
            Check(battle.Ammunition == 3 && battle.Build.active == failureBuild.active && battle.RewardRandomState == failureRng, "failure retry keeps snapshot");
            battle.RestartRun();
            Check(battle.Phase == RunPhase.ActiveSelection, "failure can restart whole run");
            Check(battle.SelectActive(ActiveAbility.Dash), "new campaign starts");
            var owned = new HashSet<PassiveAbility>();
            for (int level = 0; level < 5; level++)
            {
                yield return Ready();
                yield return CheckOpening();
                Check(battle.LevelIndex == level && battle.Ammunition == 3 && battle.Build.passives.Length == level, "entry progression " + level);
                uint rng = battle.RewardRandomState;
                var expectedRandom = new RewardRandom(rng);
                var expected = level < 4 ? expectedRandom.DrawThree(battle.Build.passives) : Array.Empty<PassiveAbility>();
                // Boundary elimination isolates flow checks from physical level solutions.
                foreach (var target in battle.World.GetComponentsInChildren<DamageableBody>()) if (target.IsTarget) target.Eliminate();
                yield return new WaitForSeconds(.5f);
                if (level == 4) break;
                Check(battle.Phase == RunPhase.RewardSelection && battle.RewardChoices.Length == 3, "one reward after stage " + level);
                var choices = battle.RewardChoices;
                for (int i = 0; i < 3; i++) Check(choices[i] == expected[i] && !owned.Contains(choices[i]), "reward is reproducible and unowned");
                Check(new HashSet<PassiveAbility>(choices).Count == 3, "unique reward candidates");
                PassiveAbility selected = choices[level % 3];
                owned.Add(selected);
                Check(battle.SelectReward(selected) && !battle.SelectReward(selected), "reward grants once");
                if (level == 1)
                {
                    yield return Ready();
                    uint nextRng = battle.RewardRandomState;
                    Check(battle.TryLaunch(Shot(1, 75)), "built-run retry fixture");
                    battle.RetryLevel();
                    yield return Ready();
                    Check(battle.Build.passives.Length == 2 && battle.RewardRandomState == nextRng && battle.Ammunition == 3, "retry preserves earned build and reward rng");
                }
            }
            Check(battle.Phase == RunPhase.RunComplete && battle.Build.passives.Length == 4 && battle.RewardChoices.Length == 0, "five stages and no fifth reward");
            Debug.Log("ROGUELIKE_FLOW_SUCCESS: synthetic boundary events exercise five stages, four rewards, entry snapshots; not a physical level-solution claim");
            foreach (var practice in battle.Practices)
            {
                battle.ReturnToMenu();
                Check(battle.StartPractice(practice.levelId, practice.build.active, practice.build.passives), "practice entry " + practice.levelId);
                yield return Ready();
                yield return CheckOpening();
                Check(battle.IsPractice && battle.LevelCount == 1, "practice is isolated");
                yield return PracticeCombo(practice.levelId);
                foreach (var target in battle.World.GetComponentsInChildren<DamageableBody>()) if (target.IsTarget) target.Eliminate();
                yield return new WaitForSeconds(.5f);
                Check(battle.Phase == RunPhase.PracticeComplete && battle.RewardChoices.Length == 0, "practice cannot grant campaign reward");
                battle.RetryLevel();
                yield return Ready();
                Check(battle.Build.passives.Length == practice.build.passives.Length && battle.Ammunition == 3, "practice retry keeps selected build");
            }
            battle.ReturnToMenu();
            battle.StartCampaign();
            Check(battle.Build.passives.Length == 0, "practice build never leaks into campaign");
            Debug.Log("ROGUELIKE_ROOT_CHECKS_SUCCESS");
            yield return CombatRegression.Run(battle);
        }
        private IEnumerator Ready()
        {
            Time.timeScale = 8;
            float limit = Time.time + 30;
            while (battle.State == BattleState.Preparing && Time.time < limit) yield return null;
            Check(battle.State == BattleState.Ready, "ready: " + battle.LevelId);
        }
        private IEnumerator Settled()
        {
            float limit = Time.time + 30;
            while (battle.State == BattleState.Resolving && Time.time < limit) yield return null;
            Check(battle.State != BattleState.Resolving, "settlement watchdog");
        }
        private IEnumerator CheckOpening()
        {
            float hp = Health();
            int targets = battle.TargetsRemaining;
            yield return new WaitForSeconds(4);
            var placements = LevelConfiguration.Expand(battle.Data);
            foreach (var marker in battle.World.GetComponentsInChildren<LevelObjectMarker>())
            {
                var placement = placements.Find(p => p.id == marker.objectId);
                Check(Vector2.Distance(marker.transform.position, placement.position) < .15f && Mathf.Abs(Mathf.DeltaAngle(marker.transform.eulerAngles.z, placement.rotation)) < 4, "opening position stable: " + battle.LevelId + "/" + marker.objectId);
            }
            Check(battle.TargetsRemaining == targets && Mathf.Abs(hp - Health()) < .001f, "opening HP stable: " + battle.LevelId);
            Debug.Log("ROGUELIKE_OPENING_STABLE: " + battle.LevelId);
        }
        private float Health() { float sum = 0; foreach (var body in battle.World.GetComponentsInChildren<DamageableBody>()) sum += body.HitPoints; return sum; }
        private Rigidbody2D Projectile()
        {
            foreach (var body in battle.World.GetComponentsInChildren<Rigidbody2D>())
                if (body.bodyType == RigidbodyType2D.Dynamic && !body.GetComponent<DamageableBody>()) return body;
            throw new InvalidOperationException("No live projectile");
        }
        internal LaunchSolution Shot(int tier, float angle)
        {
            float speed = battle.Data.launch.maxSpeed * tier / 3f;
            return new LaunchSolution { valid = true, tier = tier, angle = angle, speed = speed, velocity = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * speed };
        }
        private static void Check(bool valid, string reason) { if (!valid) throw new InvalidOperationException(reason); }
        private void Update()
        {
            if (!done && Time.realtimeSinceStartup - began > 220) { done = true; Debug.LogError("ROGUELIKE_SMOKE_FAILED: realtime watchdog"); Application.Quit(2); }
        }
    }
}
