using System;
using System.Reflection;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Editor.Marketing
{
    /// <summary>
    /// The marketing capture's view of the Hide and Seek stage (T39). Bound by reflection on purpose: the activity is the
    /// newest code of the project and may still move; a renamed member must make the capture fall back to another shot,
    /// never break the editor assembly. Members mirror what HideSeekPlayTests uses (PollInput, Game, IsHidden, View,
    /// AnimalPosition, SpotScreen, TapAt, MostVisible, LandingPoint, IsOut) and the frozen IHideSeekGame contract.
    /// </summary>
    internal sealed class MarketingHideSeekProbe
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public;
        private const string StageTypeName = "PocketWilds.Presentation.HideSeekStage";
        private const string GameTypeName = "PocketWilds.Simulation.IHideSeekGame";

        private readonly object _stage;
        private readonly object _game;
        private readonly PropertyInfo _pollInput;
        private readonly MethodInfo _isHidden, _view, _animalPosition, _spotScreen, _tapAt, _mostVisible, _landingPoint, _isOut;
        private readonly PropertyInfo _hiddenCount, _hintDelay;
        private readonly MethodInfo _animalSpot, _animalFound, _animalHinting;

        private MarketingHideSeekProbe(object stage, object game, Type stageType, Type gameType)
        {
            _stage = stage;
            _game = game;
            _pollInput = stageType.GetProperty("PollInput", Instance);
            _isHidden = Method(stageType, "IsHidden", typeof(int));
            _view = Method(stageType, "View", typeof(int));
            _animalPosition = Method(stageType, "AnimalPosition", typeof(int));
            _spotScreen = Method(stageType, "SpotScreen", typeof(int));
            _tapAt = Method(stageType, "TapAt", typeof(Vector2));
            _mostVisible = Method(stageType, "MostVisible");
            _landingPoint = Method(stageType, "LandingPoint", typeof(int));
            _isOut = Method(stageType, "IsOut", typeof(int));
            _hiddenCount = gameType.GetProperty("HiddenCount", Instance);
            _hintDelay = gameType.GetProperty("HintDelay", Instance);
            _animalSpot = Method(gameType, "AnimalSpot", typeof(int));
            _animalFound = Method(gameType, "AnimalFound", typeof(int));
            _animalHinting = Method(gameType, "AnimalHinting", typeof(int));
        }

        private bool Complete(out string missing)
        {
            missing = null;
            if (_isHidden == null) missing = "IsHidden";
            else if (_view == null) missing = "View";
            else if (_animalPosition == null) missing = "AnimalPosition";
            else if (_spotScreen == null) missing = "SpotScreen";
            else if (_tapAt == null) missing = "TapAt";
            else if (_hiddenCount == null) missing = "IHideSeekGame.HiddenCount";
            else if (_animalSpot == null) missing = "IHideSeekGame.AnimalSpot";
            else if (_animalFound == null) missing = "IHideSeekGame.AnimalFound";
            return missing == null;
        }

        /// <summary>The stage is registered with the presenter (the shipped registry may not list it yet, as in the tests).</summary>
        public static bool EnsureStage(ActivityPresenter presenter, out string reason)
        {
            reason = null;
            if (presenter == null || presenter.Stages == null)
            {
                reason = "no activity presenter";
                return false;
            }
            if (presenter.Stages.Has(ActivityId.HideAndSeek))
                return true;
            var type = FindType(StageTypeName);
            var register = type?.GetMethod("Register", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(ActivityStageRegistry) }, null);
            if (register == null || !(presenter.Stages is ActivityStageRegistry registry))
            {
                reason = type == null ? "no HideSeekStage in this build" : "HideSeekStage.Register(ActivityStageRegistry) not found";
                return false;
            }
            register.Invoke(null, new object[] { registry });
            if (!presenter.Stages.Has(ActivityId.HideAndSeek))
            {
                reason = "the stage did not register";
                return false;
            }
            return true;
        }

        public static MarketingHideSeekProbe Bind(object stage, out string reason)
        {
            reason = null;
            var stageType = stage?.GetType();
            if (stageType == null || stageType.FullName != StageTypeName)
            {
                reason = "the acquired stage is " + (stageType != null ? stageType.FullName : "null");
                return null;
            }
            var gameType = FindType(GameTypeName);
            var game = stageType.GetProperty("Game", Instance)?.GetValue(stage);
            if (gameType == null || game == null)
            {
                reason = "the stage has no IHideSeekGame";
                return null;
            }
            var probe = new MarketingHideSeekProbe(stage, game, stageType, gameType);
            if (!probe.Complete(out string missing))
            {
                reason = "member not found: " + missing;
                return null;
            }
            return probe;
        }

        public bool PollInput
        {
            set => _pollInput?.SetValue(_stage, value);
        }

        public int HiddenCount => (int)_hiddenCount.GetValue(_game);

        /// <summary>Seconds before a hidden animal gives its hint (6 s at the first difficulty).</summary>
        public float HintDelay => _hintDelay != null ? (float)_hintDelay.GetValue(_game) : 6f;

        public int AnimalSpot(int animal) => (int)_animalSpot.Invoke(_game, new object[] { animal });

        public bool AnimalFound(int animal) => (bool)_animalFound.Invoke(_game, new object[] { animal });

        public bool AnimalHinting(int animal) => _animalHinting != null && (bool)_animalHinting.Invoke(_game, new object[] { animal });

        public bool IsHidden(int animal) => (bool)_isHidden.Invoke(_stage, new object[] { animal });

        public bool IsOut(int animal) => _isOut != null && (bool)_isOut.Invoke(_stage, new object[] { animal });

        public AnimalView View(int animal) => _view.Invoke(_stage, new object[] { animal }) as AnimalView;

        public Vector3 AnimalPosition(int animal) => (Vector3)_animalPosition.Invoke(_stage, new object[] { animal });

        /// <summary>Where the animal lands once found (its position when the stage does not say).</summary>
        public Vector3 LandingPoint(int animal) =>
            _landingPoint != null ? (Vector3)_landingPoint.Invoke(_stage, new object[] { animal }) : AnimalPosition(animal);

        public Vector2 SpotScreen(int spot) => (Vector2)_spotScreen.Invoke(_stage, new object[] { spot });

        public void TapAt(Vector2 screen) => _tapAt.Invoke(_stage, new object[] { screen });

        public int MostVisible() => _mostVisible != null ? (int)_mostVisible.Invoke(_stage, null) : -1;

        /// <summary>The decor of a spot ("TallGrass", "FlowerBush", "Bush"...), empty when the stage does not say.</summary>
        public string SpotDecor(int spot)
        {
            try
            {
                var set = _stage.GetType().GetProperty("Spots", Instance)?.GetValue(_stage);
                var data = set?.GetType().GetMethod("GetSpot", Instance, null, new[] { typeof(int) }, null)?.Invoke(set, new object[] { spot });
                return data?.GetType().GetField("decor", Instance)?.GetValue(data)?.ToString() ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static MethodInfo Method(Type type, string name, params Type[] parameters) =>
            type.GetMethod(name, Instance, null, parameters, null);

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null)
                    return type;
            }
            return null;
        }
    }
}
