using UnityEngine;

namespace SiegeDemo
{
    // Animates only the decoration child; the rigidbody and collider remain authoritative.
    public sealed class DemoDroneVisual : MonoBehaviour
    {
        private Rigidbody2D body;
        private Transform exhaust;
        public void Configure(Rigidbody2D source, Transform thrust) { body = source; exhaust = thrust; }
        private void LateUpdate()
        {
            if (!body) return;
            bool moving = body.bodyType == RigidbodyType2D.Dynamic && body.linearVelocity.sqrMagnitude > .3f;
            if (moving)
            {
                Vector2 velocity = body.linearVelocity;
                float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, angle);
            }
            else if (body.bodyType == RigidbodyType2D.Kinematic)
            {
                var session = DemoPresentation.CurrentSession;
                float angle = session != null && session.Aim.valid ? session.Aim.angle : 30;
                transform.rotation = Quaternion.Euler(0, 0, angle);
            }
            if (exhaust)
            {
                exhaust.gameObject.SetActive(moving);
                exhaust.localScale = new Vector3(.2f + .08f * Mathf.Sin(Time.time * 48), .1f, 1);
            }
        }
    }
}
