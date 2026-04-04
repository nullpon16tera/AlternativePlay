using AlternativePlay.Models;
using HarmonyLib;
using UnityEngine.XR;

namespace AlternativePlay.HarmonyPatches
{
    [HarmonyPatch(nameof(HapticFeedbackManager.PlayHapticFeedback))]
    [HarmonyPatch(typeof(HapticFeedbackManager))]
    [HarmonyPriority(Priority.VeryHigh)]
    public class DarthMaulHapticPatch
    {
        public static Configuration Configuration { get; set; }

        public static DarthMaulBehavior DarthMaulBehavior { get; set; }

        private static void Prefix(HapticFeedbackManager __instance, ref XRNode node)
        {
            if (Configuration.Current.PlayMode != PlayMode.DarthMaul || DarthMaulBehavior == null || DarthMaulBehavior.Split)
            {
                // Let the original function handle the haptic feedback
                return;
            }

            if (Configuration.Current.ControllerCount == ControllerCountEnum.One)
            {
                if (!Configuration.Current.UseLeft && node == XRNode.LeftHand)
                {
                    // Using right controller, move left hits to right hand
                    node = XRNode.RightHand;
                }

                if (Configuration.Current.UseLeft && node == XRNode.RightHand)
                {
                    // Using left controller, move right hits to left hand
                    node = XRNode.LeftHand;
                }

                return;
            }

            // Two-controller Darth Maul: do not remap haptics. ReverseMaulDirection only changes saber poses in
            // DarthMaulBehavior; hits still correspond to left/right saber → left/right controller as in vanilla.
        }
    }
}
