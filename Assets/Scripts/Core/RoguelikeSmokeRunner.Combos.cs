using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SiegeDemo
{
    public sealed partial class RoguelikeSmokeRunner
    {
        private IEnumerator PracticeCombo(string id)
        {
            var events = new List<CombatEvent>();
            Action<CombatEvent> handler = value => { events.Add(value); Debug.Log("PRACTICE_EVENT: " + id + " " + value.kind + " p=" + value.projectileId + " at=" + value.position); };
            bool shardKill = false;
            bool woodHitAfterDive = false;
            battle.CombatFeedback += handler;
            foreach (var target in battle.World.GetComponentsInChildren<DamageableBody>())
            {
                if (target.IsTarget) target.Destroyed += body => { if (body.LastDamageSource.directProjectile && body.LastDamageSource.projectileKind == ProjectileKind.Shard) shardKill = true; };
                target.Damaged += (body, amount) =>
                {
                    if (id == "P03") Debug.Log("PRACTICE_DAMAGE: P03/" + body.name + " hp=" + body.HitPoints + " source=" + body.LastDamageSource.kind + " projectile=" + body.LastDamageSource.projectileKind);
                    var surface = body.GetComponent<PartSurface>();
                    if (surface && surface.Material == MaterialId.Wood && events.Exists(value => value.kind == CombatEventKind.ApexDive))
                    { woodHitAfterDive = true; Debug.Log("PRACTICE_WOOD_AFTER_DIVE: " + id + "/" + body.name); }
                };
            }
            try
            {
                Time.timeScale = 4;
                if (id == "P01")
                {
                    Check(battle.TryLaunch(Shot(3, 12)), "P01 launch");
                    float limit = Time.time + 2;
                    var mother = Projectile().GetComponent<CombatProjectile>();
                    while (mother && !mother.WoodPiercingUsed && Time.time < limit) yield return new WaitForFixedUpdate();
                    Check(mother && mother.WoodPiercingUsed, "P01 original crosses outer wood");
                    Check(battle.TryUseActive(mother.Body.position), "P01 split after original pierce");
                    yield return new WaitForSeconds(.6f);
                    var childPierces = new HashSet<int>();
                    foreach (var value in events) if (value.kind == CombatEventKind.WoodPiercing && value.projectileId != mother.ProjectileId) childPierces.Add(value.projectileId);
                    Check(childPierces.Count >= 2, "P01 refreshed children pierce distinct inner routes");
                }
                else if (id == "P02")
                {
                    Check(battle.TryLaunch(Shot(3, 40)), "P02 launch over cover");
                    var original = Projectile();
                    float limit = Time.time + 2;
                    while (original && original.position.x < .5f && Time.time < limit) yield return new WaitForFixedUpdate();
                    Check(original && original.position.x >= .5f, "P02 clears stone wall");
                    Check(battle.TryUseActive(new Vector2(2.8f, -3.6f)), "P02 aims down into landing zone");
                    yield return new WaitForSeconds(1.2f);
                    Check(Ordered(events, CombatEventKind.Dash, CombatEventKind.GroundBounce), "P02 dash then ground bounce");
                    Check(battle.DamageEvents > 0, "P02 bounce reaches structure underside");
                }
                else if (id == "P03")
                {
                    Check(battle.TryLaunch(Shot(3, 5)), "P03 launch at glass trigger");
                    yield return Settled();
                    Check(Ordered(events, CombatEventKind.GlassShards, CombatEventKind.DeathShock), "P03 glass shards precede death shock");
                    Check(shardKill, "P03 shard physically kills first target");
                    Check(events.FindAll(value => value.kind == CombatEventKind.DeathShock).Count >= 2 && battle.TargetsRemaining < 2, "P03 first death shock causes a later target death");
                    int shardBursts = 0;
                    foreach (var value in events) if (value.kind == CombatEventKind.GlassShards) shardBursts++;
                    Check(shardBursts == 1, "P03 shared shard quota");
                }
                else if (id == "P04")
                {
                    Check(battle.TryLaunch(Shot(3, 5)), "P04 low angle into ramp");
                    yield return new WaitForSeconds(4);
                    Check(Ordered(events, CombatEventKind.StoneRicochet, CombatEventKind.ApexDive), "P04 stone before natural apex dive");
                    Check(woodHitAfterDive, "P04 dive reaches wooden roof after apex");
                }
                Debug.Log("PRACTICE_COMBO_SUCCESS: " + id + " damageEvents=" + battle.DamageEvents + " remaining=" + battle.TargetsRemaining);
            }
            finally { battle.CombatFeedback -= handler; }
        }
        private static bool Ordered(List<CombatEvent> events, CombatEventKind first, CombatEventKind second)
        {
            int index = events.FindIndex(value => value.kind == first);
            return index >= 0 && events.FindIndex(index + 1, value => value.kind == second) > index;
        }
    }
}
