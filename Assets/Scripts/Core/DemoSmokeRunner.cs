using System;
using System.Collections;
using UnityEngine;

namespace SiegeDemo
{
    public sealed class DemoSmokeRunner : MonoBehaviour
    {
        private BattleController battle;
        private bool done;
        private float started;
        private void Awake() { started = Time.realtimeSinceStartup; }
        private IEnumerator Start()
        {
            battle = GetComponent<BattleController>();
            Time.timeScale = 4;
            yield return PhysicsRegression.Run(Check);
            if (done) yield break;
            yield return Ready();
            if (done) yield break;
            int targets = battle.TargetsRemaining;
            int bodies = battle.World.GetComponentsInChildren<DamageableBody>().Length;
            float health = Health();
            yield return new WaitForSeconds(6);
            CheckOpeningGeometry();
            Check(Mathf.Abs(health - Health()) < .001f, "resting support does not damage");
            Check(battle.TargetsRemaining == targets, "opening targets stable");
            Check(!battle.TryLaunch(default) && battle.Ammunition == battle.Data.ammunition, "cancel preserves ammo");
            var invalidShot = Shot(3,15);
            invalidShot.angle = float.NaN;
            Check(!battle.TryLaunch(invalidShot), "non-finite launch rejected");
            Debug.Log("DEMO_SMOKE_PHASE: resting and cancel passed");

            // Exercise real misses, timeout/stability settlement and delayed final failure.
            for (int i=0; i<battle.Data.ammunition && !done; i++)
            {
                Check(battle.TryLaunch(Shot(1,75)), "miss launch accepted");
                Check(battle.State == BattleState.Resolving, "last shot resolves before failure");
                Check(!battle.TryLaunch(Shot(1,75)), "double fire blocked");
                yield return Settled();
            }
            Check(battle.State == BattleState.Lost && battle.ResultCount == 1, "three misses lose exactly once");
            Check(!battle.TryLaunch(Shot(3,15)), "no launch after result");
            battle.Restart();
            yield return Ready();
            yield return new WaitForSeconds(6);
            CheckOpeningGeometry();
            Check(battle.ShotsFired == 0 && battle.Ammunition == battle.Data.ammunition && battle.TargetsRemaining == targets, "restart restores counters");
            Check(battle.World.GetComponentsInChildren<DamageableBody>().Length == bodies, "restart restores all bodies");
            Check(Mathf.Abs(health-Health())<.001f, "restart restores health");
            Check(battle.TryLaunch(Shot(1,75)), "launch before interrupted reset");
            battle.Restart();
            yield return Ready();
            Check(battle.ShotsFired == 0 && battle.DamageEvents == 0, "in-flight reset discards old simulation");
            // Shorten only the watchdog for an explicit timeout-path regression, then restore it.
            var settle = battle.Data.settlement;
            float savedMin = settle.minSeconds, savedStable = settle.stableSeconds, savedTimeout = settle.timeoutSeconds;
            settle.minSeconds = .05f; settle.stableSeconds = 20; settle.timeoutSeconds = .15f;
            Check(battle.TryLaunch(Shot(1,75)), "timeout fixture launch");
            yield return Settled();
            Check(battle.State == BattleState.Ready && battle.LastSettlement == "Timeout", "timeout reloads without hanging");
            settle.minSeconds = savedMin; settle.stableSeconds = savedStable; settle.timeoutSeconds = savedTimeout;
            battle.Restart();
            yield return Ready();
            Debug.Log("DEMO_SMOKE_PHASE: failure and repeated reset passed");

            // Real, unmodified level. Aim sequence is intentionally recorded for reproducible play.
            foreach (float angle in new[] {10f,12.5f,5f})
            {
                if (battle.State == BattleState.Won) break;
                Check(battle.TryLaunch(Shot(3,angle)), "attack launch accepted");
                yield return Settled();
            }
            int hits=0;
            foreach(var probe in battle.World.GetComponentsInChildren<ProjectileProbe>()) hits += probe.DynamicHits;
            Debug.Log("DEMO_SMOKE_COMBAT: targets="+battle.TargetsRemaining+" damageEvents="+battle.DamageEvents+" dynamicHits="+hits);
            Check(battle.DamageEvents > 0, "real collision caused damage");
            Check(battle.State == BattleState.Won && battle.ResultCount == 1, "normal level physical victory");
            if(done) yield break;
            // A one-bay configuration exercises victory after the final ammunition is spent.
            var structures = battle.Data.structures;
            int ammunition = battle.Data.ammunition;
            battle.Data.structures = new[] { structures[0] };
            battle.Data.ammunition = 1;
            battle.Restart();
            yield return Ready();
            Check(battle.TryLaunch(Shot(3,5)), "last-ammo fixture launched");
            Check(battle.Ammunition == 0 && battle.State == BattleState.Resolving, "final ammo permits delayed victory");
            yield return Settled();
            Check(battle.State == BattleState.Won && battle.ResultCount == 1, "final-ammo collision/collapse can win");
            battle.Data.structures = structures;
            battle.Data.ammunition = ammunition;
            battle.Restart();
            yield return Ready();
            yield return new WaitForSeconds(6);
            CheckOpeningGeometry();
            Check(battle.TargetsRemaining == targets && battle.ResultCount == 0, "restart after victory");
            if(done) yield break;
            done=true;
            Debug.Log("DEMO_SMOKE_SUCCESS: actual physics, stable support, cancel, double fire, delayed failure, repeated reset, collision victory");
            Application.Quit(0);
        }
        private LaunchSolution Shot(int tier,float angle)
        {
            float speed=battle.Data.launch.maxSpeed*tier/3f;
            return new LaunchSolution {valid=true,tier=tier,angle=angle,speed=speed,velocity=new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad))*speed};
        }
        private float Health() { float n=0; foreach(var b in battle.World.GetComponentsInChildren<DamageableBody>()) n+=b.HitPoints; return n; }
        private void CheckOpeningGeometry()
        {
            var original=LevelConfiguration.Expand(battle.Data);
            foreach(var marker in battle.World.GetComponentsInChildren<LevelObjectMarker>())
            {
                var placement=original.Find(p=>p.id==marker.objectId);
                Check(Vector2.Distance(marker.transform.position,placement.position)<.15f && Mathf.Abs(Mathf.DeltaAngle(marker.transform.eulerAngles.z,placement.rotation))<4,"opening stability: "+marker.objectId);
            }
        }
        private IEnumerator Ready() { while (!done && battle.State == BattleState.Preparing) yield return null; Check(battle.State == BattleState.Ready,"ready"); }
        private IEnumerator Settled() { while(!done && battle.State==BattleState.Resolving) yield return null; }
        private void Check(bool pass,string reason) { if(!pass && !done) { done=true; Debug.LogError("DEMO_SMOKE_FAILED: "+reason); Application.Quit(2); } }
        private void Update() { if(!done && Time.realtimeSinceStartup-started>80) Check(false,"watchdog timeout"); }
    }
}
