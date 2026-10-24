using System.Collections.Generic;
using UnityEngine;

namespace SiegeDemo
{
    public sealed partial class CombatRuntime
    {
        private const int MaximumQueuedEffects = 512, EffectsPerFrame = 32;
        private readonly Queue<PendingEffect> effects = new Queue<PendingEffect>();
        private readonly HashSet<int> shockVictims = new HashSet<int>();
        private struct PendingEffect
        {
            internal CombatEventKind kind;
            internal ShotContext shot;
            internal Vector2 position, direction;
            internal int projectileId;
        }
        private sealed class RadialVictim
        {
            internal Collider2D collider;
            internal Rigidbody2D body;
            internal DamageableBody health;
            internal Vector2 point;
            internal float distance, factor;
        }

        internal void EnqueueExplosion(CombatProjectile projectile)
        {
            if (!Alive(projectile)) return;
            Enqueue(new PendingEffect { kind = CombatEventKind.RestExplosion, shot = CurrentShot,
                position = projectile.Body.position, projectileId = projectile.ProjectileId });
        }

        private void OnBodyDestroyed(DamageableBody body)
        {
            if (!IsCurrentShot || !body) return;
            DamageSource source = body.LastDamageSource;
            if (source.kind == DamageKind.Boundary || (source.generation != 0 && source.generation != context.generation)) return;
            Vector2 position = body.transform.position;
            if (body.IsTarget && Has(PassiveAbility.DeathShock) && shockVictims.Add(body.GetInstanceID()))
                Enqueue(new PendingEffect { kind = CombatEventKind.DeathShock, shot = CurrentShot, position = position, projectileId = source.projectileId });
            var surface = body.GetComponent<PartSurface>();
            if (surface && surface.Material == MaterialId.Glass && Has(PassiveAbility.GlassShards) && !CurrentShot.shardsUsed &&
                source.kind == DamageKind.Collision && source.directProjectile && source.projectileKind != ProjectileKind.Shard &&
                source.generation == context.generation && source.shotId == CurrentShot.shotId)
            {
                CurrentShot.shardsUsed = true;
                Enqueue(new PendingEffect { kind = CombatEventKind.GlassShards, shot = CurrentShot,
                    position = position, direction = source.incidentDirection, projectileId = source.projectileId });
            }
        }

        private void Enqueue(PendingEffect effect)
        {
            if (effects.Count >= MaximumQueuedEffects)
            {
                Debug.LogError("COMBAT_EFFECT_LIMIT: pending effect queue reached 512; check level content limits.", this);
                return;
            }
            effects.Enqueue(effect);
        }

        private void LateUpdate()
        {
            if (Time.timeScale <= 0 || !IsCurrentShot) return;
            int count = 0;
            // Recursion becomes a bounded FIFO. Newly killed enemies enqueue a later effect.
            while (effects.Count > 0 && count++ < EffectsPerFrame && IsCurrentShot)
            {
                PendingEffect effect = effects.Dequeue();
                if (effect.shot != CurrentShot || effect.shot.ended) continue;
                if (effect.kind == CombatEventKind.GlassShards) SpawnShards(effect);
                else ApplyRadial(effect, effect.kind == CombatEventKind.DeathShock ? Settings.deathShock : Settings.explosion);
            }
            projectiles.RemoveAll(p => !p || p.IsRetired || !p.gameObject.activeInHierarchy);
        }

        private void SpawnShards(PendingEffect effect)
        {
            Vector2 direction = effect.direction.sqrMagnitude > .000001f ? effect.direction.normalized : Vector2.right;
            var shards = new List<CombatProjectile>(2);
            var value = Settings.shards;
            for (int i = -1; i <= 1; i += 2)
            {
                if (!IsCurrentShot || CurrentShot != effect.shot) return;
                shards.Add(CreateProjectile(effect.position, CombatRules.Rotate(direction, i * value.spreadAngle * .5f) * value.speed,
                    value.mass, value.radius, ProjectileKind.Shard));
            }
            IgnoreSiblings(shards);
            Emit(CombatEventKind.GlassShards, effect.position, direction, 2, effect.projectileId);
        }

        private void ApplyRadial(PendingEffect effect, RadialEffectSettings settings)
        {
            // All range, attenuation and occlusion decisions are made before any wall can die.
            List<RadialVictim> victims = SnapshotRadial(effect.position, settings);
            Emit(effect.kind, effect.position, Vector2.zero, settings.radius, effect.projectileId);
            var source = new DamageSource
            {
                kind = effect.kind == CombatEventKind.DeathShock ? DamageKind.DeathShock : DamageKind.RestExplosion,
                generation = effect.shot.generation, shotId = effect.shot.shotId, projectileId = effect.projectileId
            };
            foreach (var victim in victims)
            {
                if (!IsCurrentShot || CurrentShot != effect.shot) return;
                if (victim.health && !victim.health.IsDestroyed)
                    victim.health.ApplyDirectDamage(settings.damage * victim.factor, source);
            }
            // Only survivors are pushed, after all direct HP changes for this effect.
            foreach (var victim in victims)
            {
                if (!IsCurrentShot || CurrentShot != effect.shot) return;
                if (!victim.body || !victim.body.simulated || victim.body.bodyType != RigidbodyType2D.Dynamic ||
                    (victim.health && victim.health.IsDestroyed)) continue;
                Vector2 direction = victim.body.worldCenterOfMass - effect.position;
                if (direction.sqrMagnitude < .000001f) direction = Vector2.up;
                victim.body.AddForce(direction.normalized * settings.impulse * victim.factor, ForceMode2D.Impulse);
            }
        }

        private List<RadialVictim> SnapshotRadial(Vector2 position, RadialEffectSettings settings)
        {
            var grouped = new Dictionary<int, RadialVictim>();
            foreach (var collider in context.world.GetComponentsInChildren<Collider2D>())
            {
                if (!collider.enabled || collider.isTrigger) continue;
                Rigidbody2D body = collider.attachedRigidbody;
                DamageableBody health = body ? body.GetComponent<DamageableBody>() : collider.GetComponent<DamageableBody>();
                if (!health && !CombatProjectile.FindSurface(collider)) continue; // Excludes drones and visual debris.
                if (health && health.IsDestroyed) continue;
                Vector2 point = collider.ClosestPoint(position);
                float distance = Vector2.Distance(position, point);
                if (distance >= settings.radius) continue;
                int key = body ? body.GetInstanceID() : health ? health.GetInstanceID() : collider.GetInstanceID();
                if (grouped.TryGetValue(key, out RadialVictim previous) && previous.distance <= distance) continue;
                grouped[key] = new RadialVictim { collider = collider, body = body, health = health, point = point,
                    distance = distance, factor = CombatRules.RadialFalloff(distance, settings) };
            }
            var result = new List<RadialVictim>();
            foreach (var victim in grouped.Values)
            {
                bool blocked = false;
                foreach (var hit in Physics2D.LinecastAll(position, victim.point))
                {
                    Collider2D collider = hit.collider;
                    if (!collider || !collider.enabled || collider.isTrigger || !collider.transform.IsChildOf(context.world)) continue;
                    if (collider == victim.collider || (victim.body && collider.attachedRigidbody == victim.body)) continue;
                    if (!CombatProjectile.FindSurface(collider) && !collider.GetComponent<DamageableBody>()) continue;
                    blocked = true; break;
                }
                if (!blocked) result.Add(victim);
            }
            return result;
        }
    }
}
