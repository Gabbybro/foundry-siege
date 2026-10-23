using System;
using UnityEngine;

namespace SiegeDemo
{
    /// <summary>Mouse input and a short preview; the session owns ammunition and spawning.</summary>
    [DisallowMultipleComponent]
    public sealed class LauncherController : MonoBehaviour
    {
        private const float StartRadius = 1f;
        private const float PreviewSeconds = .65f;
        private const float PreviewDistance = 5.5f;
        private const int PreviewCapacity = 64;
        private IBattleSession session;
        private Camera battleCamera;
        private Vector2 origin;
        private LaunchSettings settings;
        private bool dragging, waitForRelease = true, focused = true, paused;
        private LineRenderer preview;
        private Material previewMaterial;
        private readonly Vector3[] previewPoints = new Vector3[PreviewCapacity];
        private readonly bool traceInput = Array.IndexOf(Environment.GetCommandLineArgs(), "-inputTrace") >= 0;

        public LaunchSolution CurrentAim { get; private set; }

        public void Configure(IBattleSession session, Camera camera, Vector2 origin, LaunchSettings settings)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (!camera) throw new ArgumentNullException(nameof(camera));
            if (!Finite(origin.x) || !Finite(origin.y)) throw new ArgumentException("Launcher origin must be finite.", nameof(origin));
            if (!ValidSettings(settings)) throw new ArgumentException("Invalid launch settings: check speed, drag, ratios, angles, mass and radius.", nameof(settings));
            CancelAim();
            this.session = session;
            battleCamera = camera;
            this.origin = origin;
            // Snapshot the configuration so a mutable config cannot change a gesture midway.
            this.settings = new LaunchSettings
            {
                maxSpeed = settings.maxSpeed, maxDrag = settings.maxDrag,
                cancelRatio = settings.cancelRatio, mediumRatio = settings.mediumRatio, heavyRatio = settings.heavyRatio,
                minAngle = settings.minAngle, maxAngle = settings.maxAngle,
                projectileMass = settings.projectileMass, projectileRadius = settings.projectileRadius
            };
            CreatePreview();
        }

        /// <param name="reverseDrag">Launcher origin minus pointer world position.</param>
        public static LaunchSolution Evaluate(Vector2 reverseDrag, LaunchSettings settings)
        {
            if (!ValidSettings(settings) || !Finite(reverseDrag.x) || !Finite(reverseDrag.y) || reverseDrag.x <= 0)
                return default;
            // Double precision also handles finite but very large external inputs without overflow.
            double length = Math.Sqrt((double)reverseDrag.x * reverseDrag.x + (double)reverseDrag.y * reverseDrag.y);
            float ratio = (float)Math.Min(1d, length / settings.maxDrag);
            if (ratio < settings.cancelRatio) return new LaunchSolution { pullRatio = ratio };
            int tier = ratio < settings.mediumRatio ? 1 : ratio < settings.heavyRatio ? 2 : 3;
            float angle = Mathf.Clamp(Mathf.Atan2(reverseDrag.y, reverseDrag.x) * Mathf.Rad2Deg, settings.minAngle, settings.maxAngle);
            float speed = settings.maxSpeed * (tier / 3f);
            float radians = angle * Mathf.Deg2Rad;
            return new LaunchSolution
            {
                valid = true, tier = tier, angle = angle, speed = speed, pullRatio = ratio,
                velocity = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * speed
            };
        }

        public void CancelAim()
        {
            dragging = false;
            waitForRelease = true;
            CurrentAim = default;
            if (preview) { preview.enabled = false; preview.positionCount = 0; }
        }

        private void Update()
        {
            if (traceInput && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonUp(0)))
                Debug.Log("DEMO_INPUT: down=" + Input.GetMouseButtonDown(0) + " up=" + Input.GetMouseButtonUp(0) + " pointer=" + Input.mousePosition + " focused=" + focused + " wait=" + waitForRelease + " drag=" + dragging + " hot=" + GUIUtility.hotControl + " state=" + session?.State);
            if (session == null || !battleCamera || settings == null || !focused || paused || Time.timeScale <= 0)
            {
                CancelAim();
                return;
            }
            if (session.State != BattleState.Ready || session.Ammunition <= 0)
            {
                CancelAim();
                return;
            }
            bool held = Input.GetMouseButton(0);
            if (waitForRelease)
            {
                if (!held) waitForRelease = false;
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                CancelAim();
                return;
            }
            // Allow the MouseUp event its frame; a lost release must never leave an armed drag.
            if (dragging && !held && !Input.GetMouseButtonUp()) { CancelAim(); return; }
            Vector2 pointer = Input.mousePosition;
            if (PointerBlocked(pointer) || !TryPointerWorld(pointer, out Vector2 pointerWorld))
            {
                // Crossing a HUD or window boundary cancels the whole gesture, including release.
                if (dragging || held) CancelAim();
                return;
            }
            if (dragging)
            {
                CurrentAim = Evaluate(origin - pointerWorld, settings);
                DrawPreview();
            }
        }

        private void OnGUI()
        {
            // Preserve the position attached to each OS mouse event. Polling Input.mousePosition
            // on the down frame loses the start point when a fast drag crosses several pixels
            // between rendered frames (including automated native pointer gestures).
            Event input = Event.current;
            if (input == null || input.button != 0 ||
                (input.type != EventType.MouseDown && input.type != EventType.MouseDrag && input.type != EventType.MouseUp)) return;
            if (session == null || session.State != BattleState.Ready || session.Ammunition <= 0 ||
                !battleCamera || settings == null || !focused || paused || waitForRelease || Time.timeScale <= 0) return;
            Vector2 pointer = new Vector2(input.mousePosition.x, Screen.height - input.mousePosition.y);
            if (traceInput) Debug.Log("DEMO_INPUT_EVENT: " + input.type + " pointer=" + pointer);
            if (PointerBlocked(pointer) || !TryPointerWorld(pointer, out Vector2 pointerWorld))
            {
                if (dragging) CancelAim();
                return;
            }
            if (input.type == EventType.MouseDown)
            {
                if (dragging || (pointerWorld - origin).sqrMagnitude > StartRadius * StartRadius) return;
                dragging = true;
            }
            if (!dragging) return;
            CurrentAim = Evaluate(origin - pointerWorld, settings);
            DrawPreview();
            if (input.type == EventType.MouseUp)
            {
                LaunchSolution solution = CurrentAim;
                CancelAim();
                if (solution.valid) session.TryLaunch(solution);
            }
            input.Use();
        }

        private bool PointerBlocked(Vector2 pointer)
        {
            if (pointer.x < 0 || pointer.x >= Screen.width || pointer.y < 100 || pointer.y >= Screen.height - 110) return true;
            // Reserve the top-right restart area too, if a HUD extends below its top band.
            if (pointer.x >= Screen.width - 220 && pointer.y >= Screen.height - 160) return true;
            return !battleCamera.pixelRect.Contains(pointer) || GUIUtility.hotControl != 0;
        }

        private bool TryPointerWorld(Vector2 pointer, out Vector2 world)
        {
            Ray ray = battleCamera.ScreenPointToRay(pointer);
            if (Mathf.Abs(ray.direction.z) < .00001f) { world = default; return false; }
            float distance = -ray.origin.z / ray.direction.z;
            Vector3 point = ray.GetPoint(distance);
            world = point;
            return distance >= 0 && Finite(world.x) && Finite(world.y);
        }

        private void CreatePreview()
        {
            if (preview) return;
            Shader shader = Shader.Find("Sprites/Default");
            if (!shader)
            {
                Debug.LogWarning("Combat trajectory shader Sprites/Default is unavailable.", this);
                return;
            }
            var go = new GameObject("Combat Trajectory");
            go.transform.SetParent(transform, false);
            preview = go.AddComponent<LineRenderer>();
            previewMaterial = new Material(shader) { name = "Combat Trajectory (Runtime)" };
            preview.sharedMaterial = previewMaterial;
            preview.useWorldSpace = true;
            preview.startWidth = .045f;
            preview.endWidth = .018f;
            preview.numCapVertices = 2;
            preview.sortingOrder = 25;
            preview.enabled = false;
        }

        private void DrawPreview()
        {
            if (!preview) return;
            preview.enabled = CurrentAim.valid;
            if (!CurrentAim.valid) { preview.positionCount = 0; return; }
            float step = Time.fixedDeltaTime;
            if (!Finite(step) || step <= 0) { preview.enabled = false; return; }
            Vector2 position = origin, velocity = CurrentAim.velocity;
            float distance = 0, elapsed = 0;
            int count = 1;
            previewPoints[0] = new Vector3(position.x, position.y, -.5f);
            // Matches the factory's gravityScale=1 / linearDamping=0 projectile and the 2D
            // solver's gravity-then-position integration, through the next collision.
            while (count < PreviewCapacity && elapsed + step <= PreviewSeconds)
            {
                velocity += Physics2D.gravity * step;
                Vector2 delta = velocity * step;
                if (distance + delta.magnitude > PreviewDistance) break;
                distance += delta.magnitude;
                position += delta;
                elapsed += step;
                previewPoints[count++] = new Vector3(position.x, position.y, -.5f);
            }
            Color color = CurrentAim.tier == 1 ? new Color(.35f, .9f, 1) :
                CurrentAim.tier == 2 ? new Color(1, .75f, .25f) : new Color(1, .35f, .2f);
            preview.startColor = color;
            color.a = .15f;
            preview.endColor = color;
            preview.positionCount = count;
            for (int i = 0; i < count; i++) preview.SetPosition(i, previewPoints[i]);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool ValidSettings(LaunchSettings s)
        {
            return s != null && Finite(s.maxSpeed) && s.maxSpeed > 0 && Finite(s.maxDrag) && s.maxDrag > 0 &&
                Finite(s.cancelRatio) && Finite(s.mediumRatio) && Finite(s.heavyRatio) &&
                s.cancelRatio >= 0 && s.cancelRatio < s.mediumRatio && s.mediumRatio < s.heavyRatio && s.heavyRatio <= 1 &&
                Finite(s.minAngle) && Finite(s.maxAngle) && s.minAngle >= 0 && s.minAngle <= s.maxAngle && s.maxAngle < 90 &&
                Finite(s.projectileMass) && s.projectileMass > 0 && Finite(s.projectileRadius) && s.projectileRadius > 0;
        }

        private void OnApplicationFocus(bool hasFocus) { focused = hasFocus; CancelAim(); if(traceInput) Debug.Log("DEMO_FOCUS: " + hasFocus); }
        private void OnApplicationPause(bool isPaused) { paused = isPaused; CancelAim(); }
        private void OnDisable() => CancelAim();
        private void OnDestroy()
        {
            if (preview) Destroy(preview.gameObject);
            if (previewMaterial) Destroy(previewMaterial);
        }
    }
}
