using UnityEngine;

namespace SiegeDemo
{
    [DisallowMultipleComponent]
    public sealed class DemoPresentation : MonoBehaviour
    {
        private static DemoPresentation active;
        private IBattleSession session;
        private Camera battleCamera;
        private Transform environment;
        private DemoImpactEffects effects;
        private DemoHud hud;
        private BattleState previousState = BattleState.Preparing;
        internal static IBattleSession CurrentSession => active && active.isActiveAndEnabled ? active.session : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { active = null; }

        public void Configure(IBattleSession battleSession, Camera camera)
        {
            session = battleSession;
            battleCamera = camera ? camera : Camera.main;
            active = this;
            if (!environment) environment = DemoEnvironment.Create(transform);
            if (effects == null) effects = new DemoImpactEffects(transform);
            if (hud == null) hud = new DemoHud();
            previousState = session != null ? session.State : BattleState.Preparing;
            effects.Clear();
        }

        public static void DecoratePart(GameObject go, PartDefinition part) => DemoVisualFactory.Part(go, part);
        public static void DecorateProjectile(GameObject go) => DemoVisualFactory.Projectile(go);
        public static void CreateLauncher(Vector2 origin, Transform parent) => DemoVisualFactory.Launcher(origin, parent);
        public static void PlayImpact(Vector2 position, float damage, bool destroyed)
        {
            if (active && active.isActiveAndEnabled) active.effects?.Play(position, damage, destroyed);
        }

        // Optional immediate cleanup for code that restarts twice within one frame.
        public static void ClearFeedback() { if (active) active.effects?.Clear(); }

        private void OnEnable() { active = this; }
        private void Update()
        {
            if (session == null) return;
            // Preparing is also the reset signal defined by IBattleSession.
            if (session.State == BattleState.Preparing) effects?.Clear();
            if (previousState != session.State) { hud?.StateChanged(); previousState = session.State; }
            effects?.Tick(Time.unscaledDeltaTime);
        }

        private void OnGUI()
        {
            if (hud == null) return;
            effects?.DrawLabels(battleCamera, hud.DamageStyle);
            if (hud.Draw(session))
            {
                effects?.Clear();
                session?.Restart();
            }
        }

        private void OnDisable() { if (active == this) active = null; effects?.Clear(); }
        private void OnDestroy()
        {
            if (active == this) active = null;
            effects?.Dispose();
            hud?.Dispose();
            DemoPrimitives.Dispose(environment ? environment.gameObject : null);
        }
    }
}
