using UnityEngine;

namespace SiegeDemo
{
    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class CombatProjectile : MonoBehaviour
    {
        private CombatRuntime runtime;
        private Vector2 incidentVelocity, effectiveDirection = Vector2.right;
        private float previousY, bornAt, stableTime, countdownTime, piercingUntil;
        private bool apexArmed, countdown;
        private readonly ContactPoint2D[] contacts = new ContactPoint2D[16];
        public Rigidbody2D Body { get; private set; }
        public CircleCollider2D Collider { get; private set; }
        public ShotContext Shot { get; private set; }
        public int ProjectileId { get; private set; }
        public ProjectileKind Kind { get; private set; }
        public bool IsRetired { get; private set; }
        public bool WoodPiercingUsed { get; private set; }
        public bool GroundBounceUsed { get; private set; }
        public bool StoneRicochetUsed { get; private set; }
        public bool DiveUsed { get; private set; }
        public bool IsCountingDown => countdown && !IsRetired;
        public bool IsPiercing => !IsRetired && Time.time < piercingUntil;
        public float RestStableTime => stableTime;
        public float RestCountdownRemaining => IsCountingDown ? Mathf.Max(0, runtime.Settings.rest.countdownSeconds - countdownTime) : 0;
        public float Radius => Collider ? Collider.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y)) : .1f;
        public Vector2 EffectiveDirection => effectiveDirection;
        public Vector2 LastIncidentVelocity => incidentVelocity;
        public bool IsAirborne => !IsRetired && Body && !Supported();

        internal void Initialize(CombatRuntime owner, ShotContext shot, int id, ProjectileKind kind)
        {
            runtime = owner; Shot = shot; ProjectileId = id; Kind = kind;
            Body = GetComponent<Rigidbody2D>();
            Collider = GetComponent<CircleCollider2D>();
            IsRetired = WoodPiercingUsed = GroundBounceUsed = StoneRicochetUsed = DiveUsed = false;
            bornAt = Time.time;
            stableTime = countdownTime = 0;
            countdown = false;
            RebaseFlight();
            // This is the only downward-birth exception. Dash/collision do not take this path.
            if (kind == ProjectileKind.Split && Body.linearVelocity.y < 0 && Has(PassiveAbility.ApexDive)) Dive();
        }

        private bool Has(PassiveAbility ability) => Kind != ProjectileKind.Shard && runtime && runtime.Has(ability);

        private void FixedUpdate()
        {
            if (IsRetired || !runtime || !runtime.Alive(this)) return;
            if (Kind == ProjectileKind.Shard && Time.time - bornAt >= runtime.Settings.shards.lifetime) { Retire(); return; }
            Vector2 velocity = Body.linearVelocity;
            bool support = Supported();
            if (Has(PassiveAbility.ApexDive) && !DiveUsed && !support && apexArmed && previousY > 0 && velocity.y <= 0)
            {
                Dive();
                velocity = Body.linearVelocity;
            }
            if (velocity.y > .0001f) apexArmed = true;
            previousY = velocity.y;
            RememberDirection(velocity);
            // FixedUpdate precedes the solver. Include this step's free-flight gravity and
            // damping, rather than reporting the preceding rendered frame as incident motion.
            incidentVelocity = (velocity + Physics2D.gravity * (Body.gravityScale * Time.fixedDeltaTime)) /
                (1 + Body.linearDamping * Time.fixedDeltaTime);

            var rest = runtime.Settings.rest;
            bool resting = support && !IsPiercing && velocity.magnitude <= rest.maximumSpeed && Mathf.Abs(Body.angularVelocity) <= rest.maximumAngularSpeed;
            if (!resting)
            {
                stableTime = countdownTime = 0;
                countdown = false;
                return;
            }
            stableTime += Time.fixedDeltaTime;
            if (stableTime < rest.stableSeconds) return;
            if (!Has(PassiveAbility.RestExplosion)) { Retire(); return; }
            if (!countdown)
            {
                countdown = true;
                countdownTime = 0;
                runtime.Emit(CombatEventKind.RestCountdown, Body.position, Vector2.zero, rest.countdownSeconds, ProjectileId);
            }
            else countdownTime += Time.fixedDeltaTime;
            if (countdownTime >= rest.countdownSeconds)
            {
                runtime.EnqueueExplosion(this);
                Retire();
            }
        }

        private bool Supported()
        {
            if (!Body || !Collider || !Body.simulated) return false;
            int count = Body.GetContacts(contacts);
            for (int i = 0; i < count; i++)
            {
                ContactPoint2D point = contacts[i];
                if (!point.enabled) continue;
                Vector2 normal = OutwardNormal(point.normal, Body.position, point.point, incidentVelocity);
                if (normal.y <= .2f) continue;
                Collider2D other = point.collider && point.collider.attachedRigidbody == Body ? point.otherCollider : point.collider;
                Vector2 otherVelocity = other && other.attachedRigidbody ? other.attachedRigidbody.GetPointVelocity(point.point) : Vector2.zero;
                if (Vector2.Dot(Body.GetPointVelocity(point.point) - otherVelocity, normal) > .1f) continue;
                return true;
            }
            return false;
        }

        internal DamageSource Source => new DamageSource
        {
            kind = DamageKind.Collision, generation = Shot.generation, shotId = Shot.shotId,
            projectileId = ProjectileId, projectileKind = Kind,
            incidentDirection = incidentVelocity.sqrMagnitude > .000001f ? incidentVelocity.normalized : effectiveDirection, directProjectile = true
        };

        internal struct ContactPlan
        {
            internal Collider2D collider;
            internal PartSurface surface;
            internal Vector2 velocity, normal, point, exit;
            internal bool pierce;
        }

        internal ContactPlan PrepareContact(Collider2D other, Vector2 contactNormal, Vector2 contactPoint)
        {
            Vector2 velocity = incidentVelocity;
            if (velocity.sqrMagnitude < .000001f) velocity = Body.linearVelocity;
            var plan = new ContactPlan { collider = other, surface = FindSurface(other), velocity = velocity, point = contactPoint,
                normal = OutwardNormal(contactNormal, Body.position, contactPoint, velocity) };
            if (plan.surface && !plan.surface.IsGround && plan.surface.Material == MaterialId.Wood && !WoodPiercingUsed && Has(PassiveAbility.WoodPiercing) &&
                velocity.magnitude >= runtime.Settings.piercing.minimumSpeed)
                plan.pierce = PiercingGeometry.TryExit(this, other, velocity.normalized, runtime.Settings.piercing.maximumThickness, out plan.exit);
            return plan;
        }

        internal void ResolveContact(ContactPlan plan)
        {
            if (IsRetired || !runtime || !runtime.Alive(this)) return;
            if (plan.surface && Kind != ProjectileKind.Shard)
            {
                if (plan.surface.IsGround)
                {
                    // Ground wins even when the bounce is already spent or not in the build.
                    if (!GroundBounceUsed && Has(PassiveAbility.GroundBounce) && plan.normal.y > .5f && plan.velocity.y < 0 && Vector2.Dot(plan.velocity, plan.normal) < 0)
                    {
                        var value = runtime.Settings.groundBounce;
                        GroundBounceUsed = true;
                        SetForcedVelocity(new Vector2(plan.velocity.x * value.horizontalRetention, Mathf.Max(-plan.velocity.y * value.verticalMultiplier, value.minimumUpSpeed)));
                        runtime.Emit(CombatEventKind.GroundBounce, Body.position, Body.linearVelocity.normalized, Body.linearVelocity.magnitude, ProjectileId);
                    }
                }
                else if (plan.pierce && !WoodPiercingUsed)
                {
                    WoodPiercingUsed = true;
                    piercingUntil = Time.time + Time.fixedDeltaTime * 2;
                    Body.position = plan.exit;
                    SetForcedVelocity(plan.velocity * runtime.Settings.piercing.speedRetention);
                    runtime.Emit(CombatEventKind.WoodPiercing, Body.position, plan.velocity.normalized, Body.linearVelocity.magnitude, ProjectileId);
                }
                else if (plan.surface.Material == MaterialId.Stone && !StoneRicochetUsed && Has(PassiveAbility.StoneRicochet) && plan.velocity.sqrMagnitude > .0001f)
                {
                    Vector2 direction = plan.velocity.normalized;
                    float incidence = Mathf.Asin(Mathf.Clamp01(Mathf.Abs(Vector2.Dot(direction, plan.normal)))) * Mathf.Rad2Deg;
                    if (Vector2.Dot(plan.velocity, plan.normal) < 0 && incidence <= runtime.Settings.stoneRicochet.maximumGlancingAngle)
                    {
                        var value = runtime.Settings.stoneRicochet;
                        Vector2 reflected = Vector2.Reflect(plan.velocity, plan.normal) * value.speedRetention;
                        reflected.x = Mathf.Sign(plan.velocity.x) * Mathf.Abs(reflected.x);
                        reflected.y = Mathf.Max(Mathf.Abs(reflected.y), value.minimumUpSpeed);
                        StoneRicochetUsed = true;
                        SetForcedVelocity(reflected);
                        runtime.Emit(CombatEventKind.StoneRicochet, Body.position, reflected.normalized, reflected.magnitude, ProjectileId);
                    }
                }
            }
            // Any collision can force a downward velocity. Rebase after the solver and ability
            // rewrite so it is never mistaken for the natural positive-to-negative apex crossing.
            RebaseFlight();
        }

        internal void SetForcedVelocity(Vector2 velocity)
        {
            if (IsRetired || !Body) return;
            Body.linearVelocity = velocity;
            stableTime = countdownTime = 0;
            countdown = false;
            RebaseFlight();
        }

        private void RebaseFlight()
        {
            incidentVelocity = Body.linearVelocity;
            previousY = incidentVelocity.y;
            apexArmed = previousY > .0001f;
            RememberDirection(incidentVelocity);
        }

        private void RememberDirection(Vector2 velocity)
        {
            if (velocity.sqrMagnitude > .000001f) effectiveDirection = velocity.normalized;
        }

        private void Dive()
        {
            DiveUsed = true;
            var value = runtime.Settings.apexDive;
            SetForcedVelocity(new Vector2(Body.linearVelocity.x * value.horizontalRetention, -value.downSpeed));
            runtime.Emit(CombatEventKind.ApexDive, Body.position, Body.linearVelocity.normalized, value.downSpeed, ProjectileId);
        }

        internal void Retire()
        {
            if (IsRetired) return;
            IsRetired = true;
            countdown = false;
            if (Collider) Collider.enabled = false;
            if (Body) Body.simulated = false;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        internal static PartSurface FindSurface(Collider2D collider) => !collider ? null :
            collider.attachedRigidbody ? collider.attachedRigidbody.GetComponent<PartSurface>() : collider.GetComponent<PartSurface>();
        internal static Vector2 OutwardNormal(Vector2 normal, Vector2 center, Vector2 point, Vector2 velocity)
        {
            float alignment = Vector2.Dot(normal, center - point);
            if (alignment < -.00001f || (Mathf.Abs(alignment) <= .00001f && Vector2.Dot(normal, velocity) > 0)) normal = -normal;
            return normal;
        }
        private void OnCollisionEnter2D(Collision2D collision) => ImpactCollision.Process(collision);
        private void OnCollisionStay2D(Collision2D collision) => ImpactCollision.Process(collision);
    }
}
