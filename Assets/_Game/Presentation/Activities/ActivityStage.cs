using System;
using System.Collections.Generic;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// The scene side of one activity (doc « Mini-jeux &amp; activités », sections 1 and 2): its props, its camera framing,
    /// its touches and effects. Its <see cref="Controller"/> holds the game logic and is ticked by the runner; the stage only
    /// shows it and turns touches into calls on it. One stage per activity, built once (pooled props, no Instantiate during
    /// a game) and reused game after game; its content is an Addressables group Activity_&lt;Name&gt; loaded on the first
    /// game and released when the player leaves the activities.
    /// </summary>
    public interface IActivityStage
    {
        ActivityId Activity { get; }

        /// <summary>The game logic the runner ticks (pure C#, no allocation per tick).</summary>
        IActivityController Controller { get; }

        /// <summary>Its content is loaded and its props are built: <see cref="Enter"/> can show the first frame at once.</summary>
        bool Ready { get; }

        /// <summary>A game starts: the runner already called <see cref="IActivityController.Begin"/> with the same setup.</summary>
        void Enter(in ActivitySetup setup, ActivityStageHost host);

        /// <summary>Per frame while the game shows (paused included): visuals and touches. No allocation.</summary>
        void Tick(float deltaTime, float unscaledDeltaTime);

        void SetPaused(bool paused);

        /// <summary>The game is over (finished or quit): the stage stays visible under the result card until <see cref="Exit"/>.</summary>
        void End();

        /// <summary>Back to the hub or the refuge: props hidden and returned to their pools, the refuge camera back.</summary>
        void Exit();
    }

    /// <summary>Where the presenter finds an activity's stage: the shipped activities register theirs; tests add theirs.</summary>
    public interface IActivityStageProvider
    {
        bool Has(ActivityId activity);

        /// <summary>The activity's stage, built on first use (it may load: the presenter waits for <see cref="IActivityStage.Ready"/>).</summary>
        IActivityStage Acquire(ActivityId activity, ActivityStageHost host);

        /// <summary>The player left the activities: stages release their loaded content.</summary>
        void ReleaseAll();

        /// <summary>The stage has a set for this biome theme (doc 01 section 12.0 « Thème par biome »); all by default.</summary>
        bool SupportsTheme(ActivityId activity, PocketWilds.Core.ContentId theme) => true;
    }

    /// <summary>
    /// What a stage may use in the scene: the services, the refuge (its animals), the camera rig, a root for its props, the
    /// activity HUD (tips, tutorial paws, picker, boost chip) and the texts.
    /// </summary>
    public sealed class ActivityStageHost
    {
        public ActivityStageHost(GameContext context, RefugeController refuge, RefugeCameraRig cameraRig, Transform root)
        {
            Context = context;
            Refuge = refuge;
            CameraRig = cameraRig;
            Root = root;
        }

        public GameContext Context { get; }
        public RefugeController Refuge { get; }
        public RefugeCameraRig CameraRig { get; }
        public Transform Root { get; }

        /// <summary>The activity HUD (set by the presenter; null in headless tests).</summary>
        public IActivityHudView Hud { get; set; }

        /// <summary>Player-facing texts (never null once the presenter runs).</summary>
        public PocketWilds.Core.ILocalization Text { get; set; }

        /// <summary>The game is the free first one with its tutorial (doc 01 A15): the stage shows its gestures and tips.</summary>
        public bool Tutorial { get; set; }

        /// <summary>The refuge's world rendering is off while a stage shows its own set (the presenter turns it back on).</summary>
        public System.Action<bool> SetWorldVisible { get; set; }
    }

    /// <summary>
    /// The stages of this build. Each activity task registers its factory once (<see cref="Register"/>); a stage is built
    /// on its first game and kept for the next ones. T37 registers none: the framework's tests add the test activity.
    /// </summary>
    public sealed class ActivityStageRegistry : IActivityStageProvider
    {
        private readonly Dictionary<ActivityId, Func<ActivityStageHost, IActivityStage>> _factories =
            new Dictionary<ActivityId, Func<ActivityStageHost, IActivityStage>>();

        private readonly Dictionary<ActivityId, IActivityStage> _built = new Dictionary<ActivityId, IActivityStage>();

        private readonly Dictionary<ActivityId, PocketWilds.Core.ContentId[]> _themes = new Dictionary<ActivityId, PocketWilds.Core.ContentId[]>();

        public void Register(ActivityId activity, Func<ActivityStageHost, IActivityStage> factory)
        {
            _factories[activity] = factory ?? throw new ArgumentNullException(nameof(factory));
            _built.Remove(activity);
        }

        public bool Has(ActivityId activity) => _factories.ContainsKey(activity);

        /// <summary>The biome themes an activity's stage ships (the others open with their sets); none set: every theme.</summary>
        public void SetThemes(ActivityId activity, params PocketWilds.Core.ContentId[] themes) => _themes[activity] = themes;

        public bool SupportsTheme(ActivityId activity, PocketWilds.Core.ContentId theme)
        {
            if (!_themes.TryGetValue(activity, out var themes) || themes == null || themes.Length == 0)
                return true;
            return System.Array.IndexOf(themes, theme) >= 0;
        }

        public IActivityStage Acquire(ActivityId activity, ActivityStageHost host)
        {
            if (_built.TryGetValue(activity, out var stage))
                return stage;
            if (!_factories.TryGetValue(activity, out var factory))
                return null;
            stage = factory(host);
            if (stage != null)
                _built[activity] = stage;
            return stage;
        }

        public void ReleaseAll()
        {
            foreach (var pair in _built)
                (pair.Value as IDisposable)?.Dispose();
            _built.Clear();
        }
    }
}
