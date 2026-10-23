using System;
using UnityEngine;

namespace SiegeDemo
{
    public sealed class BattleController : MonoBehaviour, IBattleSession
    {
        [SerializeField] private TextAsset levelSource;
        [SerializeField] private Camera battleCamera;
        private LevelData data;
        private Transform world, launcherVisual;
        private LauncherController launcher;
        private Rigidbody2D loaded;
        private float stateStarted, quietTime;
        private int generation;
        public BattleState State { get; private set; }
        public int Ammunition { get; private set; }
        public int TargetsRemaining { get; private set; }
        public LaunchSolution Aim => launcher ? launcher.CurrentAim : default;
        public float ResolveProgress => data == null ? 0 : Mathf.Clamp01((Time.time - stateStarted) / data.settlement.timeoutSeconds);
        public string LevelName => data == null ? "LOADING" : data.name;
        public LevelData Data => data;
        public int ShotsFired { get; private set; }
        public int ResultCount { get; private set; }
        public int DynamicHits { get; private set; }
        public int DamageEvents { get; private set; }
        public string LastSettlement { get; private set; }
        public Transform World => world;

        public void Configure(TextAsset source, Camera camera) { levelSource = source; battleCamera = camera; }
        private void Start()
        {
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.targetFrameRate = 60;
            if (!battleCamera) battleCamera = Camera.main;
            try { data = LevelConfiguration.Parse(levelSource.text); }
            catch (Exception ex) { Debug.LogError("DEMO_CONFIG_FAILED: " + ex); enabled = false; return; }
            world = new GameObject("Battle World").transform;
            launcherVisual = new GameObject("Launch Platform").transform;
            DemoPresentation.CreateLauncher(data.launcher, launcherVisual);
            launcher = gameObject.AddComponent<LauncherController>();
            launcher.Configure(this, battleCamera, data.launcher, data.launch);
            gameObject.AddComponent<DemoPresentation>().Configure(this, battleCamera);
            Restart();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-smokeTest") >= 0) gameObject.AddComponent<DemoSmokeRunner>();
            else if (Array.IndexOf(Environment.GetCommandLineArgs(), "-solveTest") >= 0) gameObject.AddComponent<DemoAimSearch>();
            Debug.Log("DEMO_READY: " + data.name + " / Unity " + Application.unityVersion);
        }
        public void Restart()
        {
            if (data == null || !world) return;
            launcher.CancelAim();
            generation++;
            int currentGeneration = generation;
            DamageableBody.ResetSession();
            DemoLevelFactory.Rebuild(data, world, true);
            loaded = null;
            Ammunition = data.ammunition;
            ShotsFired = ResultCount = DynamicHits = DamageEvents = 0;
            TargetsRemaining = 0;
            foreach (var damageable in world.GetComponentsInChildren<DamageableBody>())
            {
                if (damageable.IsTarget) TargetsRemaining++;
                damageable.Destroyed += body => { if (currentGeneration == generation) OnDestroyed(body); };
                damageable.Damaged += (body, damage) => { if (currentGeneration == generation) OnDamaged(body, damage); };
            }
            LastSettlement = "Preparing";
            ChangeState(BattleState.Preparing);
            Debug.Log("DEMO_RESET: ammo=" + Ammunition + " targets=" + TargetsRemaining);
        }
        private void OnDamaged(DamageableBody body, float damage)
        {
            DamageEvents++;
            DemoPresentation.PlayImpact(body.transform.position, damage, false);
        }
        private void OnDestroyed(DamageableBody body)
        {
            DemoPresentation.PlayImpact(body.transform.position, 0, true);
            if (body.IsTarget) TargetsRemaining = Mathf.Max(0, TargetsRemaining - 1);
        }
        private void ChangeState(BattleState state) { State = state; stateStarted = Time.time; quietTime = 0; }
        private void LoadDrone()
        {
            var go = new GameObject("Drone " + (ShotsFired + 1));
            go.transform.SetParent(world, false);
            go.transform.position = data.launcher;
            loaded = go.AddComponent<Rigidbody2D>();
            loaded.mass = data.launch.projectileMass;
            loaded.bodyType = RigidbodyType2D.Kinematic;
            loaded.interpolation = RigidbodyInterpolation2D.Interpolate;
            loaded.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            go.AddComponent<CircleCollider2D>().radius = data.launch.projectileRadius;
            go.AddComponent<ProjectileProbe>();
            DemoPresentation.DecorateProjectile(go);
        }
        public bool TryLaunch(LaunchSolution solution)
        {
            if (State != BattleState.Ready || Ammunition <= 0 || !loaded || !solution.valid) return false;
            if (solution.tier < 1 || solution.tier > 3 || !float.IsFinite(solution.angle) || !float.IsFinite(solution.velocity.x) || !float.IsFinite(solution.velocity.y)) return false;
            // Reconstruct authoritative speed; callers cannot introduce continuous power or invalid angles.
            float angle = Mathf.Clamp(solution.angle, data.launch.minAngle, data.launch.maxAngle) * Mathf.Deg2Rad;
            Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * data.launch.maxSpeed * solution.tier / 3f;
            loaded.bodyType = RigidbodyType2D.Dynamic;
            loaded.linearVelocity = velocity;
            loaded = null;
            Ammunition--; ShotsFired++;
            ChangeState(BattleState.Resolving);
            Debug.Log("DEMO_SHOT: " + ShotsFired + " velocity=" + velocity);
            return true;
        }
        private void FixedUpdate()
        {
            if (data == null || !world) return;
            foreach (var body in world.GetComponentsInChildren<Rigidbody2D>())
            {
                Vector2 p = body.position;
                if (p.y >= data.killY && p.x >= data.minX && p.x <= data.maxX) continue;
                var damageable = body.GetComponent<DamageableBody>();
                if (damageable) damageable.Eliminate(); else { body.gameObject.SetActive(false); Destroy(body.gameObject); }
            }
            if (State == BattleState.Won || State == BattleState.Lost) return;
            if (TargetsRemaining == 0) { Finish(true); return; }
            if (State != BattleState.Preparing && State != BattleState.Resolving) return;
            bool stable = true;
            foreach (var body in world.GetComponentsInChildren<Rigidbody2D>())
                if (body.bodyType == RigidbodyType2D.Dynamic && !body.IsSleeping() &&
                    (body.linearVelocity.magnitude > data.settlement.linearThreshold || Mathf.Abs(body.angularVelocity) > data.settlement.angularThreshold)) { stable = false; break; }
            quietTime = stable ? quietTime + Time.fixedDeltaTime : 0;
            float elapsed = Time.time - stateStarted;
            float minimum = State == BattleState.Preparing ? 1 : data.settlement.minSeconds;
            if (elapsed < minimum) return;
            bool timeout = elapsed >= data.settlement.timeoutSeconds;
            if (quietTime < data.settlement.stableSeconds && !timeout) return;
            LastSettlement = timeout ? "Timeout" : "Stable";
            DynamicHits = 0;
            foreach (var probe in world.GetComponentsInChildren<ProjectileProbe>()) DynamicHits += probe.DynamicHits;
            if (State == BattleState.Resolving && Ammunition <= 0) { Finish(false); return; }
            LoadDrone();
            ChangeState(BattleState.Ready);
        }
        private void Finish(bool won)
        {
            if (State == BattleState.Won || State == BattleState.Lost) return;
            launcher.CancelAim();
            ResultCount++;
            ChangeState(won ? BattleState.Won : BattleState.Lost);
            Debug.Log("DEMO_RESULT: " + State + " shots=" + ShotsFired);
        }
    }
}
