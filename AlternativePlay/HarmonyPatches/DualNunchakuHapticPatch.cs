using HarmonyLib;
using UnityEngine.XR;

namespace AlternativePlay.HarmonyPatches
{
    [HarmonyPatch(typeof(HapticFeedbackManager), "PlayHapticFeedback")]
    [HarmonyPriority(200)]
    internal static class DualNunchakuHapticPatch
    {
        private static void Prefix(ref XRNode node)
        {
            if (DualNunchakuCutContext.Hand.HasValue)
            {
                node = DualNunchakuCutContext.Hand.Value;
            }
        }
    }
}
