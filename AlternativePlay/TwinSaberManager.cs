using System;
using System.Collections.Generic;
using AlternativePlay.Models;
using BS_Utils.Gameplay;
using UnityEngine;
using Zenject;
using Object = UnityEngine.Object;

namespace AlternativePlay
{
    [DefaultExecutionOrder(-10000)]
    public sealed class TwinSaberManager : MonoBehaviour
    {
#pragma warning disable CS0649
        [Inject]
        private Configuration configuration;

        [Inject]
        private SaberManager saberManager;

        [Inject]
        private DiContainer container;

        [Inject]
        private AudioTimeSyncController audioTime;

        [InjectOptional]
        private IGamePause gamePause;
#pragma warning restore CS0649

        private readonly HashSet<Saber> sources = new HashSet<Saber>();
        private readonly Dictionary<Saber, TwinSaber> twins = new Dictionary<Saber, TwinSaber>();
        private readonly List<Saber> snapshot = new List<Saber>();
        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly NoteCutter cutter = new NoteCutter();
        private Saber leftSource;
        private Saber rightSource;

        public int TwinCount => this.twins.Count;

        private void Start()
        {
            if (BS_Utils.Plugin.LevelData.Mode == Mode.Multiplayer)
            {
                AlternativePlay.Logger.Info("Twin Darth Maul: game modifiers are solo-only.");
                this.enabled = false;
            }
            else if (this.configuration.Current.TwinDarthMaul)
            {
                ScoreSubmission.DisableSubmission("AlternativePlay");
                AlternativePlay.Logger.Info("Twin Darth Maul enabled; score submission disabled.");
            }
        }

        public void RegisterSource(Saber source)
        {
            if (source != null && source.GetComponent<TwinSaberMarker>() == null)
            {
                this.sources.Add(source);
            }
        }

        public void UnregisterSource(Saber source)
        {
            if (source != null)
            {
                this.sources.Remove(source);
                this.RemoveTwin(source);
            }
        }

        private void LateUpdate()
        {
            if (this.configuration.Current.TwinDarthMaul
                && this.saberManager != null
                && this.saberManager.isActiveAndEnabled
                && (this.gamePause == null || !this.gamePause.isPaused)
                && this.audioTime.state == IAudioTimeSource.State.Playing)
            {
                try
                {
                    this.RefreshDefaultSources();
                    this.snapshot.Clear();
                    this.snapshot.AddRange(this.sources);
                    foreach (Saber item in this.snapshot)
                    {
                        if (item == null)
                        {
                            this.UnregisterSource(item);
                            continue;
                        }

                        if (!this.IsActiveSource(item))
                        {
                            this.RemoveTwin(item);
                            continue;
                        }

                        if (this.twins.TryGetValue(item, out TwinSaber twin)
                            && (twin.Saber == null || twin.Saber.saberType != TwinSaber.Opposite(item.saberType)))
                        {
                            this.RemoveTwin(item);
                            twin = null;
                        }

                        if (twin == null)
                        {
                            twin = new TwinSaber(item, this.transform, this.container);
                            this.twins.Add(item, twin);
                        }

                        twin.SampleAndCut(this.cutter);
                    }

                    return;
                }
                catch (Exception ex)
                {
                    AlternativePlay.Logger.Error("Twin Darth Maul stopped for this scene: " + ex);
                    this.enabled = false;
                    return;
                }
            }

            this.ClearTwins();
        }

        private void RefreshDefaultSources()
        {
            if (this.leftSource != this.saberManager.leftSaber)
            {
                this.UnregisterSource(this.leftSource);
                this.leftSource = this.saberManager.leftSaber;
            }

            if (this.rightSource != this.saberManager.rightSaber)
            {
                this.UnregisterSource(this.rightSource);
                this.rightSource = this.saberManager.rightSaber;
            }

            this.RegisterSource(this.leftSource);
            this.RegisterSource(this.rightSource);
        }

        private bool IsActiveSource(Saber source)
        {
            if (!source.isActiveAndEnabled || source.GetComponent<TwinSaberMarker>() != null)
            {
                return false;
            }

            this.renderers.Clear();
            source.GetComponentsInChildren(true, this.renderers);
            foreach (Renderer renderer in this.renderers)
            {
                if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }

            return false;
        }

        private void RemoveTwin(Saber source)
        {
            if (this.twins.TryGetValue(source, out TwinSaber twin))
            {
                twin.Dispose();
                this.twins.Remove(source);
            }
        }

        private void ClearTwins()
        {
            foreach (TwinSaber twin in this.twins.Values)
            {
                twin.Dispose();
            }

            this.twins.Clear();
        }

        private void OnDisable()
        {
            this.ClearTwins();
        }

        private void OnDestroy()
        {
            this.ClearTwins();
            this.sources.Clear();
            this.snapshot.Clear();
        }
    }
}
