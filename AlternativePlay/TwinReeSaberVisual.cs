using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AlternativePlay
{
    /// <summary>
    /// Clones ReeSabers controllers from the live vanilla saber of the same type
    /// onto an extra Dual/Twin saber. ReeSabers only instantiates on SaberManager
    /// left/right sabers, so newly created cutting sabers would otherwise keep
    /// the default model.
    /// </summary>
    internal sealed class TwinReeSaberVisual : IDisposable
    {
        private static Type controllerType;
        private static MethodInfo instantiateMethod;
        private static PropertyInfo configProperty;
        private static PropertyInfo controllerRootProperty;
        private static bool resolved;
        private static bool resolveFailed;

        private readonly Saber twin;
        private readonly SaberManager manager;
        private readonly GameObject stock;
        private readonly Renderer[] stockRenderers;
        private readonly Behaviour[] stockTrails;
        private readonly List<Component> clones = new List<Component>();
        private SaberType attachedType;
        private int attachedKey;
        private float lengthScale = 1f;

        internal TwinReeSaberVisual(Saber twin, SaberManager manager, GameObject stock)
        {
            this.twin = twin;
            this.manager = manager;
            this.stock = stock;
            this.stockRenderers = stock != null ? stock.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
            this.stockTrails = stock != null
                ? stock.GetComponentsInChildren<Behaviour>(true).Where(b => b != null && b.GetType().FullName == "SaberTrail").ToArray()
                : Array.Empty<Behaviour>();
        }

        internal void ApplyLength(float scale)
        {
            this.lengthScale = scale < 0.01f ? 0.01f : scale;
            this.ApplyLengthToClones();
        }

        internal void Synchronize()
        {
            if (!Resolve() || this.twin == null || this.manager == null)
            {
                return;
            }

            Saber native = this.twin.saberType == SaberType.SaberA ? this.manager.leftSaber : this.manager.rightSaber;
            if (native == null || native == this.twin)
            {
                return;
            }

            Component[] sources = native.GetComponentsInChildren(controllerType, true);
            int key = NativeKey(sources);
            if (this.attachedType != this.twin.saberType || this.attachedKey != key)
            {
                this.Rebuild(sources);
                this.attachedType = this.twin.saberType;
                this.attachedKey = key;
            }

            this.SetStock(this.clones.Count == 0);
        }

        public void Dispose()
        {
            this.ClearClones();
            this.SetStock(true);
        }

        private void Rebuild(Component[] sources)
        {
            this.ClearClones();
            if (sources == null || sources.Length == 0)
            {
                return;
            }

            foreach (Component source in sources)
            {
                if (source == null)
                {
                    continue;
                }

                object config = configProperty.GetValue(source, null);
                if (config == null)
                {
                    continue;
                }

                var clone = instantiateMethod.Invoke(null, new object[] { this.twin.transform, config, this.twin.saberType }) as Component;
                if (clone != null)
                {
                    this.clones.Add(clone);
                    TwinReeSaberPoseLock.Attach(clone, controllerRootProperty);
                }
            }

            this.ApplyLengthToClones();
            if (this.clones.Count > 0)
            {
                AlternativePlay.Logger.Info("ReeSabers visual cloned onto " + this.twin.saberType + " extra saber (" + this.clones.Count + ").");
            }
        }

        private void ClearClones()
        {
            foreach (Component clone in this.clones)
            {
                if (clone != null)
                {
                    Object.Destroy(clone.gameObject);
                }
            }

            this.clones.Clear();
            this.attachedKey = 0;
        }

        private void ApplyLengthToClones()
        {
            foreach (Component clone in this.clones)
            {
                if (clone != null)
                {
                    clone.transform.localScale = new Vector3(1f, 1f, this.lengthScale);
                }
            }
        }

        private void SetStock(bool visible)
        {
            if (this.stock != null)
            {
                this.stock.SetActive(visible);
            }

            foreach (Renderer renderer in this.stockRenderers)
            {
                if (renderer != null)
                {
                    renderer.forceRenderingOff = !visible;
                }
            }

            foreach (Behaviour trail in this.stockTrails)
            {
                if (trail != null)
                {
                    trail.enabled = visible;
                }
            }
        }

        private static int NativeKey(Component[] sources)
        {
            int hash = 17;
            if (sources == null)
            {
                return hash;
            }

            foreach (Component source in sources)
            {
                hash = (hash * 31) + (source != null ? source.GetInstanceID() : 0);
            }

            return hash;
        }

        private static bool Resolve()
        {
            if (resolved)
            {
                return instantiateMethod != null;
            }

            if (resolveFailed)
            {
                return false;
            }

            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "ReeSabers");
            if (assembly == null)
            {
                resolveFailed = true;
                return false;
            }

            controllerType = assembly.GetType("ReeSabers.ReeSaberController");
            configProperty = controllerType?.GetProperty("Config", BindingFlags.Instance | BindingFlags.Public);
            controllerRootProperty = controllerType?.GetProperty("ControllerRoot", BindingFlags.Instance | BindingFlags.Public);
            if (controllerType != null)
            {
                foreach (MethodInfo method in controllerType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (method.Name != "Instantiate")
                    {
                        continue;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length == 3
                        && parameters[0].ParameterType == typeof(Transform)
                        && parameters[2].ParameterType == typeof(SaberType))
                    {
                        instantiateMethod = method;
                        break;
                    }
                }
            }

            resolved = true;
            if (instantiateMethod == null || configProperty == null)
            {
                AlternativePlay.Logger.Warn("ReeSabers is loaded but its saber instantiate API was not found.");
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// ReeSaber LateUpdate drives ControllerRoot from the VR controller of the
    /// clone's SaberType. Twin extras are the opposite color, so that pose is
    /// the other hand. Pin ControllerRoot to the extra saber after ReeSaber.
    /// </summary>
    [DefaultExecutionOrder(32767)]
    internal sealed class TwinReeSaberPoseLock : MonoBehaviour
    {
        private Transform controllerRoot;

        internal static void Attach(Component controller, PropertyInfo controllerRootProperty)
        {
            if (controller == null)
            {
                return;
            }

            TwinReeSaberPoseLock poseLock = controller.gameObject.GetComponent<TwinReeSaberPoseLock>();
            if (poseLock == null)
            {
                poseLock = controller.gameObject.AddComponent<TwinReeSaberPoseLock>();
            }

            poseLock.controllerRoot = controllerRootProperty?.GetValue(controller, null) as Transform;
            poseLock.enabled = poseLock.controllerRoot != null;
        }

        private void LateUpdate()
        {
            Transform saber = this.transform.parent;
            if (this.controllerRoot == null || saber == null)
            {
                return;
            }

            this.controllerRoot.SetPositionAndRotation(saber.position, saber.rotation);
        }
    }
}
