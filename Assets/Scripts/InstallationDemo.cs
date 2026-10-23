using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BirdReplica
{
    public sealed class InstallationDemo : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D bird;
        [SerializeField] private BirdImpactProbe impactProbe;
        private bool launched;
        private bool smoke;
        private bool finished;
        private float launchTime;
        private Vector2 launchPosition;

        public void Configure(Rigidbody2D body, BirdImpactProbe probe)
        {
            bird = body;
            impactProbe = probe;
        }

        private void Start()
        {
            Application.targetFrameRate = 60;
            smoke = Array.IndexOf(Environment.GetCommandLineArgs(), "-smokeTest") >= 0;
            Debug.Log("INSTALLATION_SCENE_READY Unity " + Application.unityVersion);
        }

        private void Update()
        {
            if (!smoke || finished) return;
            if (!launched && Time.timeSinceLevelLoad > 0.5f) Launch();
            if (!launched) return;

            if (impactProbe.DynamicHits > 0 && bird.position.x > launchPosition.x + 2f)
            {
                finished = true;
                Debug.Log("PHYSICS_SMOKE_SUCCESS: bird launched and collided with a dynamic target.");
                Application.Quit(0);
            }
            else if (Time.unscaledTime - launchTime > 10f)
            {
                finished = true;
                Debug.LogError("PHYSICS_SMOKE_FAILED: no dynamic target collision within 10 seconds.");
                Application.Quit(2);
            }
        }

        public void Launch()
        {
            if (launched) return;
            launched = true;
            launchTime = Time.unscaledTime;
            launchPosition = bird.position;
            bird.bodyType = RigidbodyType2D.Dynamic;
            bird.linearVelocity = new Vector2(10f, 5f);
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(20, 20, 390, 150), "BIRD REPLICA / SETUP CHECK");
            GUI.Label(new Rect(36, 54, 360, 24), "Unity 6.3 LTS - 2D physics starter scene");
            GUI.Label(new Rect(36, 80, 360, 24), "Launch the bird to check physics and collisions.");
            GUI.enabled = !launched;
            if (GUI.Button(new Rect(36, 116, 160, 34), "Launch bird")) Launch();
            GUI.enabled = true;
            if (GUI.Button(new Rect(214, 116, 160, 34), "Reset scene"))
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
