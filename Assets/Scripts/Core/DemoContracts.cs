using System;
using UnityEngine;

namespace SiegeDemo
{
    public enum BattleState { Preparing, Ready, Resolving, Won, Lost }
    public enum PartKind { Beam, Target, Platform }
    [Serializable] public class LaunchSettings
    {
        public float maxSpeed = 20, maxDrag = 2.3f, cancelRatio = .15f, mediumRatio = .4f, heavyRatio = .7f;
        public float minAngle = 5, maxAngle = 75, projectileMass = 3, projectileRadius = .32f;
    }
    [Serializable] public class SettlementSettings
    {
        public float minSeconds = 2, stableSeconds = 1, timeoutSeconds = 12, linearThreshold = .16f, angularThreshold = 8;
    }
    [Serializable] public class PartDefinition
    {
        public string id;
        public PartKind kind;
        public Vector2 size = Vector2.one;
        public float mass = 1, hitPoints = 12, impulseThreshold = 2, damageMultiplier = 1, friction = .65f, bounciness = .05f;
        public bool isStatic;
        // v1 omits these fields; Legacy retains the original platform/static behaviour.
        public MaterialId material;
        public bool isGround, indestructible;
    }
    [Serializable] public class Placement
    {
        public string id, partId;
        public Vector2 position;
        public float rotation;
    }
    [Serializable] public class StructureTemplate { public string id; public Placement[] objects; }
    [Serializable] public class StructurePlacement { public string id, templateId; public Vector2 position; public float rotation; }
    [Serializable] public class LevelData
    {
        public int version = 1;
        public string id;
        public string name = "FOUNDRY / 01";
        public int ammunition = 3;
        public Vector2 launcher = new Vector2(-9, -2.4f);
        public float killY = -8, minX = -20, maxX = 30;
        public LaunchSettings launch = new LaunchSettings();
        public SettlementSettings settlement = new SettlementSettings();
        public PartDefinition[] parts;
        public Placement[] objects;
        public StructureTemplate[] templates;
        public StructurePlacement[] structures;
    }
    public struct LaunchSolution
    {
        public bool valid;
        public int tier;
        public float angle, speed, pullRatio;
        public Vector2 velocity;
    }
    public interface IBattleSession
    {
        BattleState State { get; }
        int Ammunition { get; }
        int TargetsRemaining { get; }
        LaunchSolution Aim { get; }
        float ResolveProgress { get; }
        string LevelName { get; }
        bool TryLaunch(LaunchSolution solution);
        void Restart();
    }
}
