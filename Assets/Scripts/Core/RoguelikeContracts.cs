using System;
using UnityEngine;

namespace SiegeDemo
{
    // Serialized numeric values are stable. Never reorder them.
    public enum MaterialId { Legacy = 0, Wood = 1, Glass = 2, Stone = 3, Metal = 4 }
    public enum ActiveAbility { None = 0, Split = 1, Dash = 2 }
    public enum PassiveAbility { WoodPiercing = 0, GroundBounce = 1, RestExplosion = 2, DeathShock = 3, GlassShards = 4, StoneRicochet = 5, ApexDive = 6 }
    public enum RunPhase { MainMenu, ActiveSelection, Battle, RewardSelection, RunComplete, PracticeComplete }
    public enum ProjectileKind { Original, Split, Shard }
    public enum DamageKind { Collision, RestExplosion, DeathShock, Boundary }
    public enum CombatEventKind { Split, Dash, WoodPiercing, GroundBounce, RestCountdown, RestExplosion, DeathShock, GlassShards, StoneRicochet, ApexDive }

    [Serializable] public sealed class BuildLoadout
    {
        public ActiveAbility active = ActiveAbility.None;
        public PassiveAbility[] passives = Array.Empty<PassiveAbility>();
        public bool Has(PassiveAbility value) => passives != null && Array.IndexOf(passives, value) >= 0;
        public BuildLoadout Copy() => new BuildLoadout { active = active, passives = passives == null ? Array.Empty<PassiveAbility>() : (PassiveAbility[])passives.Clone() };
    }
    [Serializable] public sealed class SplitSettings
    {
        public float speed = 18, spreadAngle = 18, radiusScale = .58f;
    }
    [Serializable] public sealed class DashSettings
    {
        public float speedIncrement = 10, centerAngle = 0, angleWidth = 360;
    }
    [Serializable] public sealed class PiercingSettings
    {
        public float minimumSpeed = 6, maximumThickness = 1.2f, speedRetention = .8f;
    }
    [Serializable] public sealed class GroundBounceSettings
    {
        public float verticalMultiplier = 1, minimumUpSpeed = 8, horizontalRetention = .85f;
    }
    [Serializable] public sealed class StoneRicochetSettings
    {
        // Angle measured from the surface tangent: 0 = glancing, 90 = head on.
        public float maximumGlancingAngle = 30, speedRetention = .85f, minimumUpSpeed = 8;
    }
    [Serializable] public sealed class ApexDiveSettings
    {
        public float downSpeed = 18, horizontalRetention = .9f;
    }
    [Serializable] public sealed class RadialEffectSettings
    {
        // Multiplier = pow(clamp01(1 - distance/radius), falloffPower).
        public float radius = 4, damage = 10, impulse = 8, falloffPower = 1;
    }
    [Serializable] public sealed class RestSettings
    {
        public float stableSeconds = .6f, countdownSeconds = 1, maximumSpeed = .3f, maximumAngularSpeed = 20;
    }
    [Serializable] public sealed class ShardSettings
    {
        // spreadAngle is the full angle between the two fragments.
        public float speed = 18, spreadAngle = 30, mass = .18f, radius = .1f, lifetime = 6;
    }
    [Serializable] public sealed class AbilitySettings
    {
        public SplitSettings split = new SplitSettings();
        public DashSettings dash = new DashSettings();
        public PiercingSettings piercing = new PiercingSettings();
        public GroundBounceSettings groundBounce = new GroundBounceSettings();
        public StoneRicochetSettings stoneRicochet = new StoneRicochetSettings();
        public ApexDiveSettings apexDive = new ApexDiveSettings();
        public RestSettings rest = new RestSettings();
        public RadialEffectSettings explosion = new RadialEffectSettings();
        public RadialEffectSettings deathShock = new RadialEffectSettings { radius = 3.4f, damage = 6, impulse = 6 };
        public ShardSettings shards = new ShardSettings();
    }
    [Serializable] public sealed class PracticeDefinition
    {
        public string levelId, name, instructions;
        public BuildLoadout build = new BuildLoadout();
    }
    [Serializable] public sealed class RunConfigurationData
    {
        public int version = 1;
        public string[] campaignLevels = { "C01", "C02", "C03", "C04", "C05" };
        public PracticeDefinition[] practices = Array.Empty<PracticeDefinition>();
        public AbilitySettings abilities = new AbilitySettings();
    }
    public sealed class LevelEntrySnapshot
    {
        public string levelJson;
        public int levelIndex;
        public bool practice;
        public uint rewardRandomState;
        public BuildLoadout build;
    }
    public sealed class ShotContext
    {
        public int generation, shotId;
        public bool activeUsed, shardsUsed, ended;
    }
    public struct DamageSource
    {
        public DamageKind kind;
        public int generation, shotId, projectileId;
        public ProjectileKind projectileKind;
        public Vector2 incidentDirection;
        public bool directProjectile;
    }
    public struct CombatEvent
    {
        public CombatEventKind kind;
        public int generation, shotId, projectileId;
        public Vector2 position, direction;
        public float magnitude;
    }
    public sealed class CombatContext
    {
        public Transform world;
        public int generation;
        public LevelData level;
        public BuildLoadout build;
        public AbilitySettings settings;
        public Action<CombatEvent> onEvent;
        public Action<GameObject, ProjectileKind> onProjectileCreated;
    }
    // Combat supplies CombatRuntime : MonoBehaviour, ICombatRuntime.
    // Configure invalidates previous generations; EndShot removes shot entities/effects only.
    public interface ICombatRuntime
    {
        void Configure(CombatContext context);
        void BeginShot(Rigidbody2D original, int shotId);
        bool TryUseActive(Vector2 worldTarget, out string reason);
        bool ActiveAvailable { get; }
        int ActiveProjectiles { get; }
        int PendingEffects { get; }
        bool HasUnsettledEffects { get; }
        ShotContext CurrentShot { get; }
        void EndShot();
    }
    public interface IRunSession : IBattleSession
    {
        RunPhase Phase { get; }
        bool IsPaused { get; }
        bool IsPractice { get; }
        int LevelIndex { get; }
        int LevelCount { get; }
        string LevelId { get; }
        string Feedback { get; }
        BuildLoadout Build { get; }
        PassiveAbility[] RewardChoices { get; }
        PracticeDefinition[] Practices { get; }
        bool CanUseActive { get; }
        int ActiveProjectiles { get; }
        event Action<CombatEvent> CombatFeedback;
        void StartCampaign();
        bool SelectActive(ActiveAbility active);
        bool SelectReward(PassiveAbility passive);
        bool StartPractice(string levelId, ActiveAbility active, PassiveAbility[] passives);
        bool TryUseActive(Vector2 worldTarget);
        void RetryLevel();
        void RestartRun();
        void SetPaused(bool paused);
        void ReturnToMenu();
        // GUI rectangles use top-left coordinates; pointer uses Unity bottom-left screen coordinates.
        void SetUiBlockingRects(Rect[] guiRects);
        bool IsPointerBlocked(Vector2 screenPoint);
    }
}
