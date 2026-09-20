using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Zenject;
using Object = UnityEngine.Object;

namespace AlternativePlay
{
    internal sealed class NunchakuInstance : IDisposable
    {
        private readonly GameObject owner;
        private readonly List<GameObject> chain = new List<GameObject>();
        private List<GameObject> meshes = new List<GameObject>();
        private readonly DualNunchakuPhysicalSaber free;
        private readonly DualNunchakuAppearance appearance;
        private readonly float length;
        private readonly SaberType nativeType;
        private TwinSaberManager twinManager;

        internal Saber Held { get; }
        internal Saber Free => this.free.Saber;
        internal XRNode Hand { get; }
        internal IReadOnlyList<GameObject> PhysicsChain => this.chain;

        internal NunchakuInstance(XRNode hand, Saber held, Saber freeTemplate, Pose initialPose, float length, Transform parent, AssetLoaderBehavior assets, DiContainer container, SaberManager manager)
        {
            this.Hand = hand;
            this.Held = held;
            this.nativeType = held.saberType;
            this.length = length;
            this.owner = new GameObject("Dual Nunchaku " + hand);
            this.owner.transform.SetParent(parent, false);
            try
            {
                this.chain.Add(Utilities.CreateLink(hand + " Root", 3f, 1.0f, true));
                for (int i = 0; i < 3; i++)
                {
                    this.chain.Add(Utilities.CreateLink(hand + " Link " + i, 1f, 1.0f));
                }

                this.chain.Add(Utilities.CreateLink(hand + " Free Body", 3f, 1.0f));
                foreach (GameObject item in this.chain)
                {
                    item.transform.SetParent(this.owner.transform, true);
                }

                Utilities.ConnectChain(this.chain, length);
                Vector3 origin = this.chain[0].transform.position;
                Quaternion rotation = initialPose.rotation * Quaternion.Euler(0f, 90f, 0f);
                foreach (GameObject item in this.chain)
                {
                    item.transform.SetPositionAndRotation(initialPose.position * 10f + rotation * (item.transform.position - origin), rotation);
                }

                this.meshes = Utilities.CreateLinkMeshes(assets, this.chain.Count, length);
                foreach (GameObject mesh in this.meshes)
                {
                    mesh.transform.SetParent(this.owner.transform, true);
                }

                this.free = new DualNunchakuPhysicalSaber(freeTemplate, this.owner.transform, container, manager);
                this.appearance = new DualNunchakuAppearance(held, container, manager);
                DualNunchakuCutContext.Register(this.Held, hand);
                DualNunchakuCutContext.Register(this.Free, hand);
                this.UpdatePhysicsVisuals();
            }
            catch
            {
                this.Dispose();
                throw;
            }
        }

        internal void FixedStep(Pose controller, float gravity)
        {
            foreach (GameObject item in this.chain)
            {
                Rigidbody rigidbody = item.GetComponent<Rigidbody>();
                rigidbody.AddForce(new Vector3(0f, gravity * -9.81f, 0f) * rigidbody.mass);
            }

            this.chain[0].transform.SetPositionAndRotation(controller.position * 10f, controller.rotation * Quaternion.Euler(0f, 90f, 0f));
        }

        internal void UpdatePhysicsVisuals()
        {
            Utilities.MoveLinkMeshes(this.meshes, this.chain, this.length);
            Transform transform = this.chain[this.chain.Count - 1].transform;
            this.free.SetPhysicsPose(new Pose(transform.position / 10f, transform.rotation * Quaternion.Euler(0f, -90f, 0f)));
        }

        internal void ApplyColors(bool reversed, bool oneColor)
        {
            if (oneColor)
            {
                this.appearance.SetReversed(false);
                this.free.SetSaberType(this.nativeType);
                return;
            }

            this.appearance.SetReversed(reversed);
            this.free.SetSaberType(this.Held.saberType == SaberType.SaberA ? SaberType.SaberB : SaberType.SaberA);
        }

        internal void ApplySaberLength(float lengthScale)
        {
            this.free.ApplySaberLength(lengthScale);
            this.appearance.ApplyLengthCompensation(lengthScale);
        }

        internal void SampleAndCut(NoteCutter cutter)
        {
            this.appearance.Synchronize();
            this.free.SampleAndCut(cutter);
        }

        internal void RegisterTwinSource(TwinSaberManager manager)
        {
            if (this.twinManager != manager)
            {
                if (this.twinManager != null)
                {
                    this.twinManager.UnregisterSource(this.Free);
                }

                this.twinManager = manager;
            }

            if (this.twinManager != null)
            {
                this.twinManager.RegisterSource(this.Free);
            }
        }

        public void Dispose()
        {
            this.appearance?.Dispose();
            if (this.free != null)
            {
                if (this.twinManager != null)
                {
                    this.twinManager.UnregisterSource(this.Free);
                }

                DualNunchakuCutContext.Unregister(this.Free);
                this.free.Dispose();
            }

            DualNunchakuCutContext.Unregister(this.Held);
            if (this.owner != null)
            {
                this.owner.SetActive(false);
                Object.Destroy(this.owner);
            }

            foreach (GameObject item in this.chain)
            {
                if (item != null)
                {
                    item.SetActive(false);
                    Object.Destroy(item);
                }
            }

            foreach (GameObject mesh in this.meshes)
            {
                if (mesh != null)
                {
                    mesh.SetActive(false);
                    Object.Destroy(mesh);
                }
            }

            this.chain.Clear();
            this.meshes.Clear();
        }
    }
}
