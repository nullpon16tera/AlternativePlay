using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Zenject;
using Object = UnityEngine.Object;

namespace AlternativePlay
{
    internal sealed class TwinSaber : IDisposable
    {
        private static readonly FieldInfo TopField = RequireField(typeof(Saber), "_saberBladeTopTransform");
        private static readonly FieldInfo BottomField = RequireField(typeof(Saber), "_saberBladeBottomTransform");
        private static readonly FieldInfo HandleField = RequireField(typeof(Saber), "_handleTransform");
        private static readonly FieldInfo TypeField = RequireField(typeof(Saber), "_saberType");
        private static readonly FieldInfo TypeValueField = RequireField(typeof(SaberTypeObject), "_saberType");
        private static readonly FieldInfo ModelPrefabField = RequireField(typeof(SaberModelContainer), "_saberModelControllerPrefab");

        private readonly Transform sourceTop;
        private readonly Transform sourceBottom;
        private readonly Transform sourceHandle;
        private readonly Transform sourceModel;
        private readonly Transform top;
        private readonly Transform bottom;
        private readonly Transform modelAnchor;
        private readonly GameObject root;
        private readonly TwinNalulunaVisual customVisual;
        private readonly TwinReeSaberVisual reeVisual;
        private bool sampled;

        public Saber Source { get; }
        public Saber Saber { get; }

        public TwinSaber(Saber source, Transform owner, DiContainer container)
        {
            this.Source = source;
            this.sourceTop = (Transform)TopField.GetValue(source);
            this.sourceBottom = (Transform)BottomField.GetValue(source);
            this.sourceHandle = (Transform)HandleField.GetValue(source);
            SaberModelContainer modelContainer = source.GetComponentInChildren<SaberModelContainer>(true);
            if (this.sourceTop == null || this.sourceBottom == null || this.sourceHandle == null || modelContainer == null)
            {
                throw new InvalidOperationException("Twin requires the Saber endpoints and model container.");
            }

            this.sourceModel = modelContainer.transform;
            var prefab = (SaberModelController)ModelPrefabField.GetValue(modelContainer);
            if (prefab == null
                || prefab.GetComponentsInChildren<Rigidbody>(true).Length != 0
                || prefab.GetComponentsInChildren<Joint>(true).Length != 0
                || prefab.GetComponentsInChildren<Collider>(true).Length != 0)
            {
                throw new InvalidOperationException("Twin requires a visual-only SaberModelController prefab.");
            }

            this.root = new GameObject("AlternativePlay Twin " + source.name);
            this.root.SetActive(false);
            this.root.transform.SetParent(owner, false);
            try
            {
                this.root.AddComponent<TwinSaberMarker>().Source = source;
                SaberTypeObject typeObject = this.root.AddComponent<SaberTypeObject>();
                TypeValueField.SetValue(typeObject, Opposite(source.saberType));
                this.Saber = this.root.AddComponent<Saber>();
                container.Inject(this.Saber);
                this.top = Child(this.root.transform, "BladeTop");
                this.bottom = Child(this.root.transform, "BladeBottom");
                Transform handle = Child(this.root.transform, "Hilt");
                TopField.SetValue(this.Saber, this.top);
                BottomField.SetValue(this.Saber, this.bottom);
                HandleField.SetValue(this.Saber, handle);
                TypeField.SetValue(this.Saber, typeObject);
                this.modelAnchor = Child(this.root.transform, "Model");
                this.SynchronizeTransform();
                SaberModelController model = container.InstantiatePrefabForComponent<SaberModelController>(prefab, this.modelAnchor);
                SaberModelContainer.InitData initData = container.TryResolve<SaberModelContainer.InitData>();
                model.Init(this.modelAnchor, this.Saber, initData?.trailTintColor ?? Color.white);
                this.root.SetActive(true);
                SaberManager saberManager = container.TryResolve<SaberManager>();
                this.customVisual = new TwinNalulunaVisual(this.Saber, saberManager, model.gameObject);
                this.reeVisual = new TwinReeSaberVisual(this.Saber, saberManager, model.gameObject);
            }
            catch
            {
                this.root.SetActive(false);
                Object.Destroy(this.root);
                throw;
            }
        }

        public static SaberType Opposite(SaberType type)
        {
            if (type == SaberType.SaberA)
            {
                return SaberType.SaberB;
            }

            if (type == SaberType.SaberB)
            {
                return SaberType.SaberA;
            }

            throw new ArgumentOutOfRangeException(nameof(type));
        }

        internal static Quaternion ReverseBlade(Vector3 localBladeDirection)
        {
            if (localBladeDirection.sqrMagnitude < 1E-06f)
            {
                throw new InvalidOperationException("Source Saber blade has zero length.");
            }

            Vector3 axis = Vector3.Cross(localBladeDirection.normalized, Vector3.up);
            if (axis.sqrMagnitude < 1E-06f)
            {
                axis = Vector3.Cross(localBladeDirection.normalized, Vector3.right);
            }

            return Quaternion.AngleAxis(180f, axis.normalized);
        }

        private void SynchronizeTransform()
        {
            Quaternion rotation = this.Source.transform.rotation;
            Quaternion inverse = Quaternion.Inverse(rotation);
            Vector3 position = this.sourceHandle.position;
            Vector3 localTop = inverse * (this.sourceTop.position - position);
            Vector3 localBottom = inverse * (this.sourceBottom.position - position);
            Quaternion reverse = ReverseBlade(localTop - localBottom);
            this.Saber.transform.SetPositionAndRotation(position, rotation * reverse);
            this.top.localPosition = localTop;
            this.bottom.localPosition = localBottom;
            this.modelAnchor.localPosition = inverse * (this.sourceModel.position - position);
            this.modelAnchor.localRotation = inverse * this.sourceModel.rotation;
            this.modelAnchor.localScale = this.sourceModel.lossyScale;
        }

        public void SampleAndCut(NoteCutter cutter)
        {
            this.SynchronizeTransform();
            this.customVisual.Synchronize();
            this.reeVisual.Synchronize();
            this.Saber.ManualUpdate();
            if (!this.sampled)
            {
                this.sampled = true;
                return;
            }

            Saber previous = TwinCutContext.Source;
            TwinCutContext.Source = this.Source;
            try
            {
                cutter.Cut(this.Saber);
            }
            finally
            {
                TwinCutContext.Source = previous;
            }
        }

        public void Dispose()
        {
            this.customVisual?.Dispose();
            this.reeVisual?.Dispose();
            if (this.root != null)
            {
                this.root.SetActive(false);
                Object.Destroy(this.root);
            }
        }

        private static Transform Child(Transform parent, string name)
        {
            Transform transform = new GameObject(name).transform;
            transform.SetParent(parent, false);
            return transform;
        }

        private static FieldInfo RequireField(Type type, string name)
        {
            return AccessTools.Field(type, name) ?? throw new MissingFieldException(type.FullName, name);
        }
    }
}
