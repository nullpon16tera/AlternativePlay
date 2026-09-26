using System;
using HarmonyLib;
using UnityEngine;
using Zenject;
using Object = UnityEngine.Object;

namespace AlternativePlay
{
    internal sealed class DualNunchakuAppearance : IDisposable
    {
        private static readonly System.Reflection.FieldInfo Type = AccessTools.Field(typeof(Saber), "_saberType");
        private static readonly System.Reflection.FieldInfo TypeValue = AccessTools.Field(typeof(SaberTypeObject), "_saberType");

        private readonly Saber saber;
        private readonly SaberTypeObject originalType;
        private readonly DiContainer container;
        private readonly SaberManager manager;
        private GameObject root;
        private Transform modelTransform;
        private Vector3 originalModelScale;
        private TwinNalulunaVisual visual;
        private TwinNalulunaVisual.NativeVisibility visibility;

        internal DualNunchakuAppearance(Saber saber, DiContainer container, SaberManager manager)
        {
            this.saber = saber;
            this.container = container;
            this.manager = manager;
            this.originalType = (SaberTypeObject)Type.GetValue(saber);
        }

        internal void SetReversed(bool reversed)
        {
            if (!reversed)
            {
                if (this.root != null)
                {
                    this.Dispose();
                }

                return;
            }

            if (this.root != null)
            {
                return;
            }

            this.root = new GameObject("Dual Reverse Appearance");
            this.root.SetActive(false);
            this.root.transform.SetParent(this.saber.transform, false);
            try
            {
                var handle = (Transform)AccessTools.Field(typeof(Saber), "_handleTransform").GetValue(this.saber);
                Quaternion inverse = Quaternion.Inverse(this.saber.transform.rotation);
                this.root.transform.localPosition = inverse * (handle.position - this.saber.transform.position);
                SaberTypeObject typeObject = this.root.AddComponent<SaberTypeObject>();
                TypeValue.SetValue(typeObject, this.originalType.saberType == SaberType.SaberA ? SaberType.SaberB : SaberType.SaberA);
                Type.SetValue(this.saber, typeObject);
                SaberModelContainer modelContainer = this.saber.GetComponentInChildren<SaberModelContainer>(true);
                var prefab = (SaberModelController)AccessTools.Field(typeof(SaberModelContainer), "_saberModelControllerPrefab").GetValue(modelContainer);
                this.modelTransform = new GameObject("Model").transform;
                this.modelTransform.SetParent(this.root.transform, false);
                this.modelTransform.localPosition = inverse * (modelContainer.transform.position - handle.position);
                this.modelTransform.localRotation = inverse * modelContainer.transform.rotation;
                this.modelTransform.localScale = modelContainer.transform.lossyScale;
                this.originalModelScale = this.modelTransform.localScale;
                SaberModelController model = this.container.InstantiatePrefabForComponent<SaberModelController>(prefab, this.modelTransform);
                SaberModelContainer.InitData initData = this.container.TryResolve<SaberModelContainer.InitData>();
                model.Init(this.modelTransform, this.saber, initData?.trailTintColor ?? Color.white);
                this.visual = new TwinNalulunaVisual(this.saber, this.manager, model.gameObject, this.root.transform);
                this.visibility = new TwinNalulunaVisual.NativeVisibility(this.saber, modelContainer.gameObject, this.root.transform);
                this.root.SetActive(true);
            }
            catch
            {
                this.Dispose();
                throw;
            }
        }

        internal void ApplyLengthCompensation(float lengthScale)
        {
            if (this.root == null) return;
            float scale = lengthScale < 0.01f ? 0.01f : lengthScale;
            this.root.transform.localScale = new Vector3(1f, 1f, 1f / scale);
            if (this.modelTransform != null)
            {
                this.modelTransform.localScale = new Vector3(this.originalModelScale.x, this.originalModelScale.y, this.originalModelScale.z * scale);
            }
        }

        internal void Synchronize()
        {
            this.visual?.Synchronize();
            this.visibility?.Synchronize();
        }

        public void Dispose()
        {
            this.visibility?.Dispose();
            this.visibility = null;
            this.visual?.Dispose();
            this.visual = null;
            if (this.saber != null && this.originalType != null)
            {
                Type.SetValue(this.saber, this.originalType);
            }

            if (this.root != null)
            {
                this.root.SetActive(false);
                Object.Destroy(this.root);
            }

            this.root = null;
            this.modelTransform = null;
        }
    }
}
