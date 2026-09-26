using HarmonyLib;

namespace AlternativePlay.HarmonyPatches
{
    [HarmonyPatch(typeof(NoteCutHapticEffect), "HitNote")]
    internal static class TwinSaberHapticPatch
    {
        private static void Prefix(ref SaberType saberType)
        {
            Saber source = TwinCutContext.Source;
            if (source != null)
            {
                saberType = source.saberType;
            }
        }
    }
}
