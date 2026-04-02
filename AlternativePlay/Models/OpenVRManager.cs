using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Valve.VR;

namespace AlternativePlay.Models
{
    public class OpenVRManager
    {
        public CVRSystem System { get; private set; }

        public OpenVRManager()
        {
            TryLoadOpenVrNativeLibrary();

            try
            {
                var error = EVRInitError.None;
                this.System = OpenVR.Init(ref error, EVRApplicationType.VRApplication_Other);

                if (this.System == null || error != EVRInitError.None)
                {
                    if (error != EVRInitError.None)
                        AlternativePlay.Logger.Error($"Unable to initialize OpenVR with error: {error}");
                    this.System = null;
                }
            }
            catch (DllNotFoundException ex)
            {
                AlternativePlay.Logger.Error($"OpenVR native library (openvr_api) not found; tracker and controller polling are disabled. {ex.Message}");
                this.System = null;
            }
            catch (Exception ex)
            {
                AlternativePlay.Logger.Error($"OpenVR initialization failed: {ex.Message}");
                this.System = null;
            }
        }

        /// <summary>
        /// Loads {GameRoot}/Libs/OpenVR/openvr_api.dll before Valve.VR P/Invoke runs, so the loader can find the DLL.
        /// </summary>
        private static void TryLoadOpenVrNativeLibrary()
        {
            try
            {
                string path = ResolveOpenVrApiPath();
                if (string.IsNullOrEmpty(path))
                    return;

                IntPtr handle = NativeMethods.LoadLibrary(path);
                if (handle == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    AlternativePlay.Logger.Error($"LoadLibrary failed for OpenVR ({path}), Win32 error {err}");
                    return;
                }

                AlternativePlay.Logger.Info($"Preloaded OpenVR native library from {path}");
            }
            catch (Exception ex)
            {
                AlternativePlay.Logger.Error($"Could not preload OpenVR native library: {ex.Message}");
            }
        }

        /// <summary>
        /// AlternativePlay.dll is in Plugins/; game root is the parent folder (contains Libs/OpenVR/).
        /// </summary>
        private static string ResolveOpenVrApiPath()
        {
            string location = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrEmpty(location))
                return null;

            string pluginDir = Path.GetDirectoryName(location);
            string gameRoot = Path.GetDirectoryName(pluginDir);
            if (string.IsNullOrEmpty(gameRoot))
                return null;

            string candidate = Path.GetFullPath(Path.Combine(gameRoot, "Libs", "OpenVR", "openvr_api.dll"));
            return File.Exists(candidate) ? candidate : null;
        }

        private static class NativeMethods
        {
            [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern IntPtr LoadLibrary(string lpFileName);
        }
    }
}
