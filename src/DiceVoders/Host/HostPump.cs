using System;
using DiceVoders.Dev;
using DiceVoders.Modularity;
using UnityEngine;

namespace DiceVoders.Host
{
    /// <summary>
    /// The one injected MonoBehaviour, registered once for the process (IL2CPP type registration is
    /// permanent, which is why this lives in the host and never reloads). Each frame it pumps the dev
    /// server's job queue and drives the current feature module's per-frame work. The module is read
    /// fresh from the loader each frame so a reload swaps it in with no further wiring.
    /// </summary>
    public sealed class HostPump : MonoBehaviour
    {
        // Required for an IL2CPP-injected MonoBehaviour.
        public HostPump(IntPtr ptr) : base(ptr) { }

#if DEBUG
        internal static DevServer DevServer;
#endif
        internal static ModuleLoader Loader;

        private void Update()
        {
#if DEBUG
            try
            {
                DevServer?.Pump();
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogWarning("DevServer.Pump: " + ex);
            }
#endif

            try
            {
                Loader?.Module?.Tick();
            }
            catch (Exception ex)
            {
                // Full exception (with stack); a per-frame Tick failure is otherwise unlocatable.
                Plugin.Logger?.LogWarning("Module.Tick: " + ex);
            }
        }
    }
}
