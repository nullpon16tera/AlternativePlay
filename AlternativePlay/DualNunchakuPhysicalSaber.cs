using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Zenject;
using Object = UnityEngine.Object;

namespace AlternativePlay
{
    internal sealed class DualNunchakuPhysicalSaber : IDisposable
    {
        private static readonly FieldInfo Top = AccessTools.Field(typeof(Saber), "_saberBladeTopTransform");
        private static readonly FieldInfo Bottom = AccessTools.Field(typeof(Saber), "_saberBladeBottomTransform");
        private static readonly FieldInfo Handle = AccessTools.Field(typeof(Saber), "_handleTransform");
        private static readonly FieldInfo Type = AccessTools.Field(typeof(Saber), "_saberType");
        private static readonly FieldInfo TypeValue = AccessTools.Field(typeof(SaberTypeObject), "_saberType");
        private static readonly FieldInfo ModelPrefab = AccessTools.Field(typeof(SaberModelContainer), "_saberModelControllerPrefab");
        private static readonly FieldInfo TimeHelperField = AccessTools.Field(typeof(Saber), "_timeHelper");

        private readonly GameObject root;
        private readonly TwinNalulunaVisual visual;
        private readonly TwinReeSaberVisual reeVisual;
        private readonly SaberModelController model;
        private readonly Transform modelAnchor;
        private readonly Transform top;
        private readonly Transform bottom;
        private readonly Vector3 originalTopLocal;
        private readonly Vector3 originalBottomLocal;
        private readonly Vector3 originalModelScale;
        private readonly Color trailTint;
        private bool sampled;

        public Saber Saber { get; }

        internal DualNunchakuPhysicalSaber(Saber template, Transform owner, DiContainer container, SaberManager manager)
        {
            this.root = new GameObject("Dual Nunchaku Free " + template.saberType);
            this.root.SetActive(false);
            this.root.transform.SetParent(owner, false);
            try
            {
                SaberModelContainer modelContainer = template.GetComponentInChildren<SaberModelContainer>(true);
                if (modelContainer == null)
                {
                    throw new InvalidOperationException("Free Saber requires a model container.");
                }

                var prefab = (SaberModelController)ModelPrefab.GetValue(modelContainer);
                if (prefab == null
                    || prefab.GetComponentsInChildren<Rigidbody>(true).Length != 0
                    || prefab.GetComponentsInChildren<Joint>(true).Length != 0
                    || prefab.GetComponentsInChildren<Collider>(true).Length != 0)
                {
                    throw new InvalidOperationException("Free Saber requires a visual-only model prefab.");
                }

                var handle = (Transform)Handle.GetValue(template);
                Quaternion inverse = Quaternion.Inverse(template.transform.rotation);
                SaberTypeObject typeObject = this.root.AddComponent<SaberTypeObject>();
                TypeValue.SetValue(typeObject, template.saberType);
                this.Saber = this.root.AddComponent<Saber>();
                container.Inject(this.Saber);
                if (TimeHelperField.GetValue(this.Saber) == null)
                {
                    TimeHelperField.SetValue(this.Saber, TimeHelperField.GetValue(template));
                }
                Type.SetValue(this.Saber, typeObject);
                Transform hilt = Child(this.root.transform, "Hilt");
                Handle.SetValue(this.Saber, hilt);

                var sourceTop = (Transform)Top.GetValue(template);
                var sourceBottom = (Transform)Bottom.GetValue(template);
                this.top = Child(this.root.transform, "BladeTop");
                this.bottom = Child(this.root.transform, "BladeBottom");
                this.originalTopLocal = inverse * (sourceTop.position - handle.position);
                this.originalBottomLocal = inverse * (sourceBottom.position - handle.position);
                this.top.localPosition = this.originalTopLocal;
                this.bottom.localPosition = this.originalBottomLocal;
                Top.SetValue(this.Saber, this.top);
                Bottom.SetValue(this.Saber, this.bottom);

                Transform modelTransform = Child(this.root.transform, "Model");
                modelTransform.localPosition = inverse * (modelContainer.transform.position - handle.position);
                modelTransform.localRotation = inverse * modelContainer.transform.rotation;
                modelTransform.localScale = modelContainer.transform.lossyScale;
                this.originalModelScale = modelTransform.localScale;
                SaberModelContainer copyContainer = modelTransform.gameObject.AddComponent<SaberModelContainer>();
                copyContainer.enabled = false;
                ModelPrefab.SetValue(copyContainer, prefab);
                AccessTools.Field(typeof(SaberModelContainer), "_saber").SetValue(copyContainer, this.Saber);
                this.modelAnchor = modelTransform;
                this.model = container.InstantiatePrefabForComponent<SaberModelController>(prefab, modelTransform);
                this.trailTint = container.TryResolve<SaberModelContainer.InitData>()?.trailTintColor ?? Color.white;
                this.model.Init(modelTransform, this.Saber, this.trailTint);
                this.visual = new TwinNalulunaVisual(this.Saber, manager, this.model.gameObject);
                this.reeVisual = new TwinReeSaberVisual(this.Saber, manager, this.model.gameObject);
            }
            catch
            {
                this.Dispose();
                throw;
            }
        }

        internal void SetSaberType(SaberType type)
        {
            if (this.Saber.saberType != type)
            {
                TypeValue.SetValue(Type.GetValue(this.Saber), type);
                this.model.Init(this.modelAnchor, this.Saber, this.trailTint);
            }
        }

        internal void SetPhysicsPose(Pose pose)
        {
            this.Saber.transform.SetPositionAndRotation(pose.position, pose.rotation);
            this.root.SetActive(true);
        }

        internal void ApplySaberLength(float lengthScale)
        {
            float scale = lengthScale < 0.01f ? 0.01f : lengthScale;
            if (this.top != null)
            {
                this.top.localPosition = this.originalTopLocal * scale;
            }
            if (this.bottom != null)
            {
                this.bottom.localPosition = this.originalBottomLocal * scale;
            }
            if (this.modelAnchor != null)
            {
                this.modelAnchor.localScale = new Vector3(this.originalModelScale.x, this.originalModelScale.y, this.originalModelScale.z * scale);
            }

            this.reeVisual?.ApplyLength(scale);
        }

        internal void SampleAndCut(NoteCutter cutter)
        {
            this.visual.Synchronize();
            this.reeVisual.Synchronize();
            this.Saber.ManualUpdate();
            if (!this.sampled)
            {
                this.sampled = true;
            }
            else
            {
                cutter.Cut(this.Saber);
            }
        }

        public void Dispose()
        {
            this.visual?.Dispose();
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
    }
}
