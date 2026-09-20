using HarmonyLib;
using UnityEngine.XR;

namespace AlternativePlay.HarmonyPatches
{
    [HarmonyPatch(typeof(NoteCutter), "Cut")]
    internal static class DualNunchakuCutPatch
    {
        private static void Prefix(Saber saber, out XRNode? __state)
        {
            __state = DualNunchakuCutContext.Hand;
            DualNunchakuCutContext.Hand = DualNunchakuCutContext.Owner(saber);
        }

        private static void Finalizer(XRNode? __state)
        {
            DualNunchakuCutContext.Hand = __state;
        }
    }
}
