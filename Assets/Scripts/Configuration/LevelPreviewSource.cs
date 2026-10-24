using UnityEngine;

namespace SiegeDemo
{
    // Scene-serialized authoring baseline survives assembly reloads. This is not battle state.
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class LevelPreviewSource : MonoBehaviour
    {
        [HideInInspector] public string initialJson;
        [HideInInspector] public string sourcePath;
        [HideInInspector] public string workingJson;
        [HideInInspector] public string diskJson;

        private void Awake()
        {
            // The real controller builds its own gameplay world. Preview colliders must
            // never remain active alongside it when entering a play test.
            if (Application.isPlaying) gameObject.SetActive(false);
        }
    }
}
