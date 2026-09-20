using System;
using AlternativePlay.Models;
using BS_Utils.Gameplay;
using UnityEngine;
using UnityEngine.XR;
using Zenject;

namespace AlternativePlay
{
    public sealed class DualNunchakuBehavior : MonoBehaviour
    {
#pragma warning disable CS0649
        [Inject]
        private Configuration configuration;

        [Inject]
        private SaberDeviceManager devices;

        [Inject]
        private AssetLoaderBehavior assets;

        [Inject]
        private SaberManager sabers;

        [Inject]
        private DiContainer container;

        [Inject]
        private AudioTimeSyncController audioTime;

        [InjectOptional]
        private IGamePause pause;
#pragma warning restore CS0649

        private NunchakuInstance left;
        private NunchakuInstance right;
        private readonly NoteCutter cutter = new NoteCutter();
        private readonly DualNunchakuDiagnostics diagnostics = new DualNunchakuDiagnostics();
        private bool selected;
        private float length;
        private Vector3 leftOriginalScale;
        private Vector3 rightOriginalScale;
        private bool capturedSaberScale;

        public int InstanceCount => (this.left != null ? 1 : 0) + (this.right != null ? 1 : 0);

        private bool Playing
        {
            get
            {
                return this.selected
                    && this.sabers != null
                    && this.sabers.isActiveAndEnabled
                    && (this.pause == null || !this.pause.isPaused)
                    && this.audioTime.state == IAudioTimeSource.State.Playing;
            }
        }

        private void Start()
        {
            this.selected = this.configuration.Current.PlayMode == PlayMode.Nunchaku && this.configuration.Current.DualNunchaku;
            if (!this.selected)
            {
                this.enabled = false;
                return;
            }

            this.diagnostics.Initialize();
            this.gameObject.AddComponent<DualNunchakuCutDriver>().Owner = this;
            this.length = this.configuration.Current.NunchakuLength / 100f;
            Utilities.CheckAndDisableForTrackerTransforms(this.configuration.Current.LeftTracker);
            Utilities.CheckAndDisableForTrackerTransforms(this.configuration.Current.RightTracker);
            ScoreSubmission.DisableSubmission("AlternativePlay");
            AlternativePlay.Logger.Info("Dual Nunchaku: two independent chains; four physical cutting sabers; score submission disabled.");
        }

        private void Update()
        {
            if (!this.Playing)
            {
                this.Clear();
                return;
            }

            try
            {
                Pose leftSaberPose = this.devices.GetLeftSaberPose(this.configuration.Current.LeftTracker);
                Pose rightSaberPose = this.devices.GetRightSaberPose(this.configuration.Current.RightTracker);
                if (!ValidPose(leftSaberPose) || !ValidPose(rightSaberPose))
                {
                    return;
                }

                if (this.left != null && (this.left.Held != this.sabers.leftSaber || this.right.Held != this.sabers.rightSaber))
                {
                    this.Clear();
                }

                if (this.left == null)
                {
                    if (this.sabers.leftSaber == null || this.sabers.rightSaber == null)
                    {
                        return;
                    }

                    if (this.sabers.leftSaber.saberType != SaberType.SaberA || this.sabers.rightSaber.saberType != SaberType.SaberB)
                    {
                        throw new InvalidOperationException("Dual requires original left RED and right BLUE Saber types.");
                    }

                    this.right = new NunchakuInstance(XRNode.RightHand, this.sabers.rightSaber, this.sabers.leftSaber, rightSaberPose, this.length, this.transform, this.assets, this.container, this.sabers);
                    this.left = new NunchakuInstance(XRNode.LeftHand, this.sabers.leftSaber, this.sabers.rightSaber, leftSaberPose, this.length, this.transform, this.assets, this.container, this.sabers);
                }

                this.left.ApplyColors(this.configuration.Current.ReverseNunchaku, this.configuration.Current.DualNunchakuOneColorLeft);
                this.right.ApplyColors(this.configuration.Current.ReverseNunchaku, this.configuration.Current.DualNunchakuOneColorRight);
                this.devices.SetLeftSaberPose(leftSaberPose.Reverse());
                this.devices.SetRightSaberPose(rightSaberPose.Reverse());
                this.ApplyHeldSaberLength();
                float saberScale = this.configuration.Current.NunchakuSaberLength / 100f;
                this.left.ApplySaberLength(saberScale);
                this.right.ApplySaberLength(saberScale);
                this.left.UpdatePhysicsVisuals();
                this.right.UpdatePhysicsVisuals();
                TwinSaberManager manager = this.container.TryResolve<TwinSaberManager>();
                this.left.RegisterTwinSource(manager);
                this.right.RegisterTwinSource(manager);
            }
            catch (Exception error)
            {
                this.Fail(error);
            }
        }

        private void FixedUpdate()
        {
            if (!this.Playing || this.left == null || this.right == null)
            {
                return;
            }

            try
            {
                Pose leftSaberPose = this.devices.GetLeftSaberPose(this.configuration.Current.LeftTracker);
                Pose rightSaberPose = this.devices.GetRightSaberPose(this.configuration.Current.RightTracker);
                if (ValidPose(leftSaberPose) && ValidPose(rightSaberPose))
                {
                    this.left.FixedStep(leftSaberPose, this.configuration.Current.Gravity);
                    this.right.FixedStep(rightSaberPose, this.configuration.Current.Gravity);
                }
            }
            catch (Exception error)
            {
                this.Fail(error);
            }
        }

        internal void SampleFreeSabers()
        {
            if (!this.isActiveAndEnabled || !this.Playing || this.left == null || this.right == null)
            {
                return;
            }

            try
            {
                this.left.SampleAndCut(this.cutter);
                this.right.SampleAndCut(this.cutter);
                this.diagnostics.Observe(this.left, this.right, this.configuration.Current.TwinDarthMaul, this.configuration.Current.ReverseNunchaku);
            }
            catch (Exception error)
            {
                this.Fail(error);
            }
        }

        private static bool ValidPose(Pose pose)
        {
            Vector3 position = pose.position;
            Quaternion rotation = pose.rotation;
            float magnitude = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
            if (!float.IsNaN(position.x + position.y + position.z + magnitude) && !float.IsInfinity(position.x + position.y + position.z + magnitude))
            {
                return magnitude > 0.5f;
            }

            return false;
        }

        private void Fail(Exception error)
        {
            AlternativePlay.Logger.Error("Dual Nunchaku stopped for this scene: " + error);
            this.enabled = false;
        }

        private void CaptureSaberScales()
        {
            if (this.capturedSaberScale || this.sabers?.leftSaber == null || this.sabers.rightSaber == null) return;
            this.leftOriginalScale = this.sabers.leftSaber.transform.localScale;
            this.rightOriginalScale = this.sabers.rightSaber.transform.localScale;
            this.capturedSaberScale = true;
        }

        private void ApplyHeldSaberLength()
        {
            this.CaptureSaberScales();
            if (!this.capturedSaberScale) return;
            float scale = this.configuration.Current.NunchakuSaberLength / 100f;
            Utilities.ApplySaberLength(this.sabers.leftSaber.transform, this.leftOriginalScale, scale);
            Utilities.ApplySaberLength(this.sabers.rightSaber.transform, this.rightOriginalScale, scale);
        }

        private void RestoreSaberScales()
        {
            if (!this.capturedSaberScale) return;
            if (this.sabers?.leftSaber != null)
            {
                this.sabers.leftSaber.transform.localScale = this.leftOriginalScale;
            }
            if (this.sabers?.rightSaber != null)
            {
                this.sabers.rightSaber.transform.localScale = this.rightOriginalScale;
            }
        }

        private void Clear()
        {
            if (this.left != null || this.right != null)
            {
                this.diagnostics.Observe(this.left, this.right, this.configuration.Current.TwinDarthMaul, this.configuration.Current.ReverseNunchaku, true);
            }

            this.RestoreSaberScales();
            this.left?.Dispose();
            this.right?.Dispose();
            this.left = null;
            this.right = null;
        }

        private void OnDisable()
        {
            this.Clear();
        }

        private void OnDestroy()
        {
            this.Clear();
        }
    }
}
