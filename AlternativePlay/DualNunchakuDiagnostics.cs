using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AlternativePlay
{
    internal sealed class DualNunchakuDiagnostics
    {
        private bool enabled;
        private float nextCheck;
        private string previous;

        internal void Initialize()
        {
            try
            {
                this.enabled = File.Exists(Path.Combine(Application.dataPath, "..", "UserData", "AlternativePlay.DualDiagnostics.enabled"));
            }
            catch
            {
                this.enabled = false;
            }
        }

        internal void Observe(NunchakuInstance left, NunchakuInstance right, bool twin, bool reverse, bool releasing = false)
        {
            if (!this.enabled || (!releasing && Time.unscaledTime < this.nextCheck))
            {
                return;
            }

            this.nextCheck = Time.unscaledTime + 1f;
            try
            {
                string text = "Twin=" + twin + ", Reverse=" + reverse + "\n" + Pair(left) + "\n" + Pair(right);
                if (releasing || text != this.previous)
                {
                    AlternativePlay.Logger.Info("[DualDiagnostic] " + (releasing ? "release" : "created/state change") + "\n" + text);
                }

                this.previous = releasing ? null : text;
                if (releasing)
                {
                    this.nextCheck = 0f;
                }
            }
            catch (Exception ex)
            {
                AlternativePlay.Logger.Warn("[DualDiagnostic] snapshot unavailable: " + ex.Message);
            }
        }

        private static object Field(object owner, string name)
        {
            return owner?.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(owner);
        }

        private static string State(GameObject go)
        {
            if (go == null)
            {
                return "missing/destroyed";
            }

            return go.name + "#" + go.GetInstanceID() + " self=" + go.activeSelf + " hierarchy=" + go.activeInHierarchy;
        }

        private static string Parent(Transform transform)
        {
            string text = "";
            Transform current = transform != null ? transform.parent : null;
            while (current != null)
            {
                text = current.name + "/" + text;
                current = current.parent;
            }

            return text;
        }

        private static string Rendering(GameObject go)
        {
            if (go == null)
            {
                return "missing";
            }

            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            return "renderers=" + renderers.Length
                + ", enabled=" + renderers.Count(r => r.enabled)
                + ", forcedOff=" + renderers.Count(r => r.forceRenderingOff)
                + ", drawable=" + renderers.Count(r => r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy)
                + ", layers=" + string.Join(",", renderers.Select(r => r.gameObject.layer).Distinct());
        }

        private static string SaberState(Saber saber)
        {
            if (saber == null)
            {
                return "Saber missing/destroyed";
            }

            SaberMovementData movementData = saber.movementDataForLogic;
            return State(saber.gameObject)
                + " parent=" + Parent(saber.transform)
                + " componentEnabled=" + saber.enabled
                + " type=" + saber.saberType
                + " movementData=" + (movementData == null ? "missing" : RuntimeHelpers.GetHashCode(movementData).ToString())
                + " twinMarker=" + (saber.GetComponent<TwinSaberMarker>() != null)
                + " hapticOwner=" + DualNunchakuCutContext.Owner(saber)
                + " " + Rendering(saber.gameObject)
                + " (actual NoteCut requires note-hit confirmation)";
        }

        private static string Pair(NunchakuInstance pair)
        {
            if (pair == null)
            {
                return "no instance";
            }

            GameObject root = pair.PhysicsChain.Count > 0 ? pair.PhysicsChain[0] : null;
            return "Instance#" + RuntimeHelpers.GetHashCode(pair)
                + " hand=" + pair.Hand
                + " chain=" + pair.PhysicsChain.Count
                + " root=" + State(root)
                + " rootJoint=" + (root != null && root.GetComponent<ConfigurableJoint>() != null)
                + "\nHeld " + SaberState(pair.Held)
                + "\nFree " + SaberState(pair.Free);
        }
    }
}
