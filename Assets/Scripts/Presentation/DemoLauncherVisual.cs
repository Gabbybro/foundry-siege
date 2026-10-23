using UnityEngine;

namespace SiegeDemo
{
    public sealed class DemoLauncherVisual : MonoBehaviour
    {
        private Transform rail;
        private float angle = 30;
        public void Configure(Transform aimRail) { rail = aimRail; }
        private void LateUpdate()
        {
            if (!rail) return;
            var session = DemoPresentation.CurrentSession;
            if (session == null || session.State == BattleState.Preparing) angle = 30;
            else if (session.State == BattleState.Ready) angle = session.Aim.valid ? session.Aim.angle : 30;
            rail.localRotation = Quaternion.Euler(0, 0, angle);
        }
    }
}
