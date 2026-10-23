using UnityEngine;

namespace SiegeDemo
{
    // Authoring identity survives manual transform edits; never infer a part from its display name.
    [DisallowMultipleComponent]
    public sealed class LevelObjectMarker : MonoBehaviour
    {
        public string objectId;
        public string partId;
    }
}
