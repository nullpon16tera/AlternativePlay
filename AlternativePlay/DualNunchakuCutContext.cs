using System.Collections.Generic;
using UnityEngine.XR;

namespace AlternativePlay
{
    internal static class DualNunchakuCutContext
    {
        private static readonly Dictionary<Saber, XRNode> owners = new Dictionary<Saber, XRNode>();

        [System.ThreadStatic]
        internal static XRNode? Hand;

        internal static void Register(Saber saber, XRNode hand)
        {
            owners[saber] = hand;
        }

        internal static void Unregister(Saber saber)
        {
            if (saber != null)
            {
                owners.Remove(saber);
            }
        }

        internal static XRNode? Owner(Saber saber)
        {
            if (saber == null)
            {
                return null;
            }

            TwinSaberMarker marker = saber.GetComponent<TwinSaberMarker>();
            Saber key = marker != null ? marker.Source : saber;
            if (key == null || !owners.TryGetValue(key, out XRNode value))
            {
                return null;
            }

            return value;
        }
    }
}
