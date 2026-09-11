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

        // TWO→ONE is a gameplay-only state. It must never be stored in Configuration.
        private bool temporaryOneFromTwo;
        private bool temporaryOneUseLeft;

        private void Start()
        {
            // Do nothing if we aren't playing Darth Maul
            if (this.configuration.Current.PlayMode != PlayMode.DarthMaul) { return; }

            this.temporaryOneFromTwo = false;
            this.temporaryOneUseLeft = this.configuration.Current.UseLeft;

            Utilities.CheckAndDisableForTrackerTransforms(this.configuration.Current.LeftTracker);
            Utilities.CheckAndDisableForTrackerTransforms(this.configuration.Current.RightTracker);
        }

        private void Update()
        {
            if (this.configuration.Current.PlayMode != PlayMode.DarthMaul)
            {
                return;
            }

            if (this.Split)
            {
                // Split: either trigger rejoins to the state that existed before Split.
                if (this.inputManager.GetLeftTriggerClicked() || this.inputManager.GetRightTriggerClicked())
                {
                    this.Split = false;
                }
            }
            else if (this.temporaryOneFromTwo)
            {
                // Temporary ONE created from configured TWO. Both triggers return to TWO.
                if (this.inputManager.GetBothTriggerClicked())
                {
                    this.inputManager.GetLeftTriggerClicked();
                    this.inputManager.GetRightTriggerClicked();
                    this.temporaryOneFromTwo = false;
                }
                else if (this.temporaryOneUseLeft)
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
                        this.temporaryOneUseLeft = false;
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
                    this.temporaryOneUseLeft = true;
                }
            }
            else if (this.configuration.Current.ControllerCount == ControllerCountEnum.Two)
            {
                // RemoveOtherSaber is kept as the persisted UI option for TWO / ONE Switch
                // for compatibility with existing Pre-Twin configurations.
                if (this.configuration.Current.RemoveOtherSaber)
                {
                    if (this.inputManager.GetBothTriggerClicked())
                    {
                        this.inputManager.GetLeftTriggerClicked();
                        this.inputManager.GetRightTriggerClicked();
                        if (this.configuration.Current.UseTriggerToSeparate)
                        {
                            this.Split = true;
                        }
                    }
                    else if (this.inputManager.GetLeftTriggerClicked())
                    {
                        this.temporaryOneFromTwo = true;
                        this.temporaryOneUseLeft = true;
                    }
                    else if (this.inputManager.GetRightTriggerClicked())
                    {
                        this.temporaryOneFromTwo = true;
                        this.temporaryOneUseLeft = false;
                    }
                }
                else if (this.configuration.Current.UseTriggerToSeparate &&
                    (this.inputManager.GetLeftTriggerClicked() || this.inputManager.GetRightTriggerClicked()))
                {
                    this.Split = true;
                }
            }
            else if (this.configuration.Current.UseLeft)
            {
                // Formal configured ONE mode.
                if (this.inputManager.GetLeftTriggerClicked())
                {
                    if (this.configuration.Current.UseTriggerToSeparate)
                    {
                        this.Split = true;
                    }
                }
                else if (this.inputManager.GetRightTriggerClicked() && this.configuration.Current.UseTriggerToSwitchHands)
                {
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

            if (this.temporaryOneFromTwo)
            {
                this.TransformOneControllerMaul(this.temporaryOneUseLeft, true);
                return;
            }

            switch (this.configuration.Current.ControllerCount)
            {
                case ControllerCountEnum.One:
                    this.TransformOneControllerMaul(this.configuration.Current.UseLeft, false);
                    break;

                case ControllerCountEnum.Two:
                    this.TransformTwoControllerMaul();
                    break;

                default:
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
        private void TransformOneControllerMaul(bool useLeft, bool temporaryFromTwo)
        {
            float sep = 1.0f * this.configuration.Current.MaulDistance / 100.0f;

            // Get the Pose of the base saber and calculate the rotated pose from it
            Pose basePose = useLeft
                ? this.saberDeviceManager.GetLeftSaberPose(this.configuration.Current.LeftTracker)
                : this.saberDeviceManager.GetRightSaberPose(this.configuration.Current.RightTracker);

            Pose rotatedPose = basePose.Reverse();
            Vector3 separation = new Vector3(0.0f, 0.0f, sep * 2.0f);
            rotatedPose.position += (rotatedPose.rotation * separation);

            // TWO-derived ONE uses a fixed hand-based color mapping:
            // Right hand = front RED / rear BLUE; Left hand = front BLUE / rear RED.
            // Formal ONE keeps the configured Reverse Maul Direction behavior.
            Pose leftSaberPose;
            Pose rightSaberPose;
            bool useRotatedAsLeft = temporaryFromTwo
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
