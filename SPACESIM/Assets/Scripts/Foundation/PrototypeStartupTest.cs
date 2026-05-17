using UnityEngine;

namespace SpaceSim.Foundation
{
    /// <summary>
    /// Phase 0 prototype startup smoke test.
    ///
    /// Attached to a GameObject in TestFoundation.unity. When the scene runs (Play in the
    /// editor or a built executable), this logs a marker line confirming the prototype's
    /// foundation scripting layer is wired up correctly. If the user opens Unity, presses
    /// Play, and sees "Phase 0 prototype foundation ready" in the Console, the file-level
    /// project setup from commit 027 is verified at the runtime layer.
    ///
    /// This script does no real work — it is intentionally minimal. The netcode contract
    /// implementation (sim-tick boundary, authoritative state, mode transitions, etc.) lands
    /// in subsequent commits under Scripts/Foundation/SimTick/, Scripts/Foundation/Coordinates/,
    /// and Scripts/Foundation/Physics/.
    /// </summary>
    public sealed class PrototypeStartupTest : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("Phase 0 prototype foundation ready");
            Debug.Log($"Unity version: {Application.unityVersion}");
            Debug.Log($"Platform: {Application.platform}");
            Debug.Log($"Persistent data path: {Application.persistentDataPath}");
        }
    }
}
