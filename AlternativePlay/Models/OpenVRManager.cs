using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Valve.VR;

namespace AlternativePlay.Models
{
    public class OpenVRManager
    {
        private static class NativeMethods
        {
            [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern IntPtr LoadLibrary(string lpFileName);
        }

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
                    {
                        AlternativePlay.Logger.Error($"Unable to initialize OpenVR with error: {error}");
                    }
                    this.System = null;
                }
            }
            catch (DllNotFoundException ex)
            {
                AlternativePlay.Logger.Error(
                    "OpenVR native library (openvr_api) not found; tracker and controller polling are disabled. " + ex.Message);
                this.System = null;
            }
            catch (Exception ex)
            {
                AlternativePlay.Logger.Error("OpenVR initialization failed: " + ex.Message);
                this.System = null;
            }
        }

        private static void TryLoadOpenVrNativeLibrary()
        {
            try
            {
                string path = ResolveOpenVrApiPath();
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                IntPtr handle = NativeMethods.LoadLibrary(path);
                if (handle == IntPtr.Zero)
                {
                    int lastWin32Error = Marshal.GetLastWin32Error();
                    AlternativePlay.Logger.Error($"LoadLibrary failed for OpenVR ({path}), Win32 error {lastWin32Error}");
                }
                else
                {
                    AlternativePlay.Logger.Info("Preloaded OpenVR native library from " + path);
                }
            }
            catch (Exception ex)
            {
                AlternativePlay.Logger.Error("Could not preload OpenVR native library: " + ex.Message);
            }
        }

        /// <summary>
        /// Looks for openvr_api.dll under Beat Saber/Libs/OpenVR relative to Plugins.
        /// </summary>
        private static string ResolveOpenVrApiPath()
        {
            string location = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrEmpty(location))
            {
                return null;
            }

            string pluginsDir = Path.GetDirectoryName(location);
            string beatSaberRoot = Path.GetDirectoryName(pluginsDir);
            if (string.IsNullOrEmpty(beatSaberRoot))
            {
                return null;
            }

            string fullPath = Path.GetFullPath(Path.Combine(beatSaberRoot, "Libs", "OpenVR", "openvr_api.dll"));
            return File.Exists(fullPath) ? fullPath : null;
        }
    }
}
