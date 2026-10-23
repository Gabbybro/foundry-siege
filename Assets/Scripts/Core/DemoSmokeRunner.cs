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
            yield return Ready();
            if (done) yield break;
            int targets = battle.TargetsRemaining;
            int bodies = battle.World.GetComponentsInChildren<DamageableBody>().Length;
            float health = Health();
            yield return new WaitForSeconds(2);
            Check(Mathf.Abs(health - Health()) < .001f, "resting support does not damage");
            Check(battle.TargetsRemaining == targets, "opening targets stable");
            Check(!battle.TryLaunch(default) && battle.Ammunition == battle.Data.ammunition, "cancel preserves ammo");
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
            Check(battle.ShotsFired == 0 && battle.Ammunition == battle.Data.ammunition && battle.TargetsRemaining == targets, "restart restores counters");
            Check(battle.World.GetComponentsInChildren<DamageableBody>().Length == bodies, "restart restores all bodies");
            Check(Mathf.Abs(health-Health())<.001f, "restart restores health");
            battle.Restart();
            yield return Ready();
            Debug.Log("DEMO_SMOKE_PHASE: failure and repeated reset passed");

            // Real, unmodified level. Aim sequence is intentionally recorded for reproducible play.
            foreach (float angle in new[] {5f,12f,22f})
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
            battle.Restart();
            yield return Ready();
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
        private IEnumerator Ready() { while (!done && battle.State == BattleState.Preparing) yield return null; Check(battle.State == BattleState.Ready,"ready"); }
        private IEnumerator Settled() { while(!done && battle.State==BattleState.Resolving) yield return null; }
        private void Check(bool pass,string reason) { if(!pass && !done) { done=true; Debug.LogError("DEMO_SMOKE_FAILED: "+reason); Application.Quit(2); } }
        private void Update() { if(!done && Time.realtimeSinceStartup-started>80) Check(false,"watchdog timeout"); }
    }
}
