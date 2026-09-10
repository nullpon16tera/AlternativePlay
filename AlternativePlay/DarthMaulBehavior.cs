using AlternativePlay.Models;
using UnityEngine;
using Zenject;

namespace AlternativePlay
{
    public class DarthMaulBehavior : MonoBehaviour
    {
#pragma warning disable CS0649
        [Inject]
        private Configuration configuration;
        [Inject]
        private SaberDeviceManager saberDeviceManager;
        [Inject]
        private InputManager inputManager;
#pragma warning restore CS0649

        public bool Split { get; private set; }

        private void Start()
        {
            // Do nothing if we aren't playing Darth Maul
            if (this.configuration.Current.PlayMode != PlayMode.DarthMaul) { return; }

            // ReverseLeftSaber is reused as a temporary "switched from TWO to ONE" flag.
            // Clear it on start so a previous play session cannot leave ONE stuck.
            if (this.configuration.Current.ControllerCount >= ControllerCountEnum.Two)
            {
                this.configuration.Current.ControllerCount = ControllerCountEnum.Two;
            }
            if (this.configuration.Current.ReverseLeftSaber)
            {
                this.configuration.Current.ControllerCount = ControllerCountEnum.Two;
            }
            this.configuration.Current.ReverseLeftSaber = false;

            Utilities.CheckAndDisableForTrackerTransforms(this.configuration.Current.LeftTracker);
            Utilities.CheckAndDisableForTrackerTransforms(this.configuration.Current.RightTracker);
        }

        private void Update()
        {
            if (this.configuration.Current.PlayMode != PlayMode.DarthMaul)
            {
                // Do nothing if we aren't playing Darth Maul
                return;
            }

            if (this.Split)
            {
                // Split: either trigger rejoins the Maul
                if (this.inputManager.GetLeftTriggerClicked() || this.inputManager.GetRightTriggerClicked())
                {
                    this.Split = false;
                }
            }
            else if (this.configuration.Current.ControllerCount == ControllerCountEnum.Two)
            {
                // RemoveOtherSaber is reused as "TWO / ONE Switch" for Darth Maul
                if (this.configuration.Current.RemoveOtherSaber)
                {
                    if (this.inputManager.GetBothTriggerClicked())
                    {
                        // Consume individual trigger clicks so they don't also fire below
                        this.inputManager.GetLeftTriggerClicked();
                        this.inputManager.GetRightTriggerClicked();
                        if (this.configuration.Current.UseTriggerToSeparate)
                        {
                            this.Split = true;
                        }
                    }
                    else if (this.inputManager.GetLeftTriggerClicked())
                    {
                        this.configuration.Current.ControllerCount = ControllerCountEnum.One;
                        this.configuration.Current.UseLeft = true;
                        this.configuration.Current.ReverseLeftSaber = true; // temporary ONE from TWO
                    }
                    else if (this.inputManager.GetRightTriggerClicked())
                    {
                        this.configuration.Current.ControllerCount = ControllerCountEnum.One;
                        this.configuration.Current.UseLeft = false;
                        this.configuration.Current.ReverseLeftSaber = true; // temporary ONE from TWO
                    }
                }
                else if (this.configuration.Current.UseTriggerToSeparate &&
                    (this.inputManager.GetLeftTriggerClicked() || this.inputManager.GetRightTriggerClicked()))
                {
                    this.Split = true;
                }
            }
            else if (this.configuration.Current.ReverseLeftSaber && this.inputManager.GetBothTriggerClicked())
            {
                // Temporary ONE: both triggers return to TWO
                this.inputManager.GetLeftTriggerClicked();
                this.inputManager.GetRightTriggerClicked();
                this.configuration.Current.ControllerCount = ControllerCountEnum.Two;
                this.configuration.Current.ReverseLeftSaber = false;
            }
            else if (this.configuration.Current.UseLeft)
            {
                if (this.inputManager.GetLeftTriggerClicked())
                {
                    if (this.configuration.Current.UseTriggerToSeparate)
                    {
                        this.Split = true;
                    }
                }
                else if (this.inputManager.GetRightTriggerClicked() && this.configuration.Current.UseTriggerToSwitchHands)
                {
                    // Empty-hand trigger transfers the Maul to that hand
                    this.configuration.Current.UseLeft = false;
                }
            }
            else if (this.inputManager.GetRightTriggerClicked())
            {
                if (this.configuration.Current.UseTriggerToSeparate)
                {
                    this.Split = true;
                }
            }
            else if (this.inputManager.GetLeftTriggerClicked() && this.configuration.Current.UseTriggerToSwitchHands)
            {
                this.configuration.Current.UseLeft = true;
            }

            this.TransformSabers();
        }

        /// <summary>
        /// Move the sabers that have been disconnected from the VRControllers ourselves
        /// </summary>
        private void TransformSabers()
        {
            if (this.Split)
            {
                this.TransformForSplitDarthMaul();
                return;
            }

            switch (this.configuration.Current.ControllerCount)
            {
                case ControllerCountEnum.One:
                    this.TransformOneControllerMaul();
                    break;

                case ControllerCountEnum.Two:
                    this.TransformTwoControllerMaul();
                    break;

                default:
                    // Do nothing
                    break;
            }
        }

        /// <summary>
        /// Tracks the sabers for when Darth Maul mode is split into two swords
        /// </summary>
        private void TransformForSplitDarthMaul()
        {
            this.saberDeviceManager.SetLeftSaber(this.configuration.Current.LeftTracker);
            this.saberDeviceManager.SetRightSaber(this.configuration.Current.RightTracker);
        }

        /// <summary>
        /// Moves the maul sabers based on a one controller scheme
        /// </summary>
        private void TransformOneControllerMaul()
        {
            bool useLeft = this.configuration.Current.UseLeft;
            float sep = 1.0f * this.configuration.Current.MaulDistance / 100.0f;

            // Get the Pose of the base saber and calculate the rotated pose from it
            Pose basePose = useLeft
                ? this.saberDeviceManager.GetLeftSaberPose(this.configuration.Current.LeftTracker)
                : this.saberDeviceManager.GetRightSaberPose(this.configuration.Current.RightTracker);

            Pose rotatedPose = basePose.Reverse();
            Vector3 separation = new Vector3(0.0f, 0.0f, sep * 2.0f);
            rotatedPose.position += (rotatedPose.rotation * separation);

            // When ReverseLeftSaber is set (temporary ONE from TWO), use hand-based mapping.
            // Otherwise use the normal ReverseMaulDirection / UseLeft mapping.
            Pose leftSaberPose;
            Pose rightSaberPose;
            bool useRotatedAsLeft = this.configuration.Current.ReverseLeftSaber
                ? useLeft
                : (useLeft == this.configuration.Current.ReverseMaulDirection);

            if (useRotatedAsLeft)
            {
                leftSaberPose = rotatedPose;
                rightSaberPose = basePose;
            }
            else
            {
                leftSaberPose = basePose;
                rightSaberPose = rotatedPose;
            }

            this.saberDeviceManager.SetLeftSaberPose(leftSaberPose);
            this.saberDeviceManager.SetRightSaberPose(rightSaberPose);
        }

        /// <summary>
        /// Moves the maul sabers based on a two controller scheme
        /// </summary>
        private void TransformTwoControllerMaul()
        {
            float sep = 1.0f * this.configuration.Current.MaulDistance / 100.0f;

            // Determine Hand positions
            Pose leftHand = this.saberDeviceManager.GetLeftSaberPose(this.configuration.Current.LeftTracker);
            Pose rightHand = this.saberDeviceManager.GetRightSaberPose(this.configuration.Current.RightTracker);

            // Determine final saber positions and rotations
            Vector3 middlePos = (rightHand.position + leftHand.position) * 0.5f;
            Vector3 forward = (rightHand.position - leftHand.position).normalized;
            Vector3 rightHandUp = rightHand.rotation * Vector3.up;

            Pose forwardSaberPose = new Pose(middlePos + (forward * sep), Quaternion.LookRotation(forward, rightHandUp));
            Pose backwardSaberPose = new Pose(middlePos + (-forward * sep), Quaternion.LookRotation(-forward, -rightHandUp));

            Pose leftSaberPose = this.configuration.Current.ReverseMaulDirection ? forwardSaberPose : backwardSaberPose;
            Pose rightSaberPose = this.configuration.Current.ReverseMaulDirection ? backwardSaberPose : forwardSaberPose;

            this.saberDeviceManager.SetLeftSaberPose(leftSaberPose);
            this.saberDeviceManager.SetRightSaberPose(rightSaberPose);
        }
    }
}
