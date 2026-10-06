using System.Collections;
using NUnit.Framework;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Services.Feedback;
using PocketWilds.Simulation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace PocketWilds.Tests.PlayMode
{
    /// <summary>
    /// T14 criterion: complete hatch, short, skippable, 0 recurring allocations after warmup
    /// (also Gate 22 hatch spam: 10 hatches, pools and fragments reset, no Instantiate).
    /// </summary>
    public class HatchSequenceTests
    {
        private const float Dt = 1f / 60f;

        /// <summary>Records feedback events into fixed arrays (no allocation).</summary>
        private sealed class Recorder : IAudioService, IHapticService, IHatchListener
        {
            public readonly AudioEventId[] Audio = new AudioEventId[256];
            public readonly HapticEventId[] Haptics = new HapticEventId[256];
            public int AudioCount, HapticCount, Revealed, Finished;

            public void Play(AudioEventId id, Vector3 position, ContentId context)
            {
                if (AudioCount < Audio.Length) Audio[AudioCount++] = id;
            }

            public void Play(HapticEventId id)
            {
                if (HapticCount < Haptics.Length) Haptics[HapticCount++] = id;
            }

            public void OnHatchRevealed() => Revealed++;
            public void OnHatchFinished() => Finished++;

            public void Reset() => AudioCount = HapticCount = Revealed = Finished = 0;

            public int Count(AudioEventId id)
            {
                int n = 0;
                for (int i = 0; i < AudioCount; i++) if (Audio[i] == id) n++;
                return n;
            }
        }

        private GameObject _root;
        private GameObject _camera;
        private PaletteTable _palettes;
        private AnimalVisualResolver _resolver;
        private HatchPresentationConfig _config;
        private GameObjectPool<EggView> _eggs;
        private AnimalViewPool _animals;
        private int _rabbitHandle;
        private FeedbackVfx _vfx;
        private HatchSequencePlayer _player;
        private Recorder _recorder;
        private uint _eggVisual;
        private float _time;
        private uint _nextId = 1;

        /// <summary>Pool or resolve failures inside measured loops (NUnit asserts allocate, so count instead).</summary>
        private int _failures;

        [TearDown]
        public void TearDown()
        {
            _vfx?.Dispose();
            _animals?.Dispose();
            _eggs?.Clear();
            _palettes?.Dispose();
            if (_root != null) Object.Destroy(_root);
            if (_camera != null) Object.Destroy(_camera);
        }

        private IEnumerator Setup()
        {
#if UNITY_EDITOR
            _config = UnityEditor.AssetDatabase.LoadAssetAtPath<HatchPresentationConfig>("Assets/_Game/Content/Visuals/DEF_HatchPresentation.asset");
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimalVisualCatalog>("Assets/_Game/Content/Visuals/DEF_AnimalVisualCatalog.asset");
            var database = UnityEditor.AssetDatabase.LoadAssetAtPath<ContentDatabaseAsset>("Assets/_Game/Content/DEF_ContentDatabase.asset");
            Assert.IsNotNull(_config);
            var registry = database.CreateBuilder().Build();
            _palettes = new PaletteTable(registry);
            _resolver = new AnimalVisualResolver(registry, _palettes);
            var basic = registry.Eggs.Get(ContentId.FromSource("egg.grassland.basic"));
            Assert.IsTrue(_palettes.TryGetRow(basic.Id, out int row));
            _eggVisual = EggVisualPacking.FromDefinition(basic, row);

            _camera = new GameObject("TestCamera");
            _camera.AddComponent<Camera>();
            _camera.transform.SetPositionAndRotation(new Vector3(0f, 1.2f, -3f), Quaternion.Euler(18f, 0f, 0f));

            _root = new GameObject("HatchTestRoot");
            _eggs = new GameObjectPool<EggView>(_config.eggPrefab, _root.transform, 2, 2);
            _eggs.Warmup(2);
            _animals = new AnimalViewPool(_root.transform);
            Assert.IsTrue(catalog.TryGet("PF_Animal_Rabbit", out var rabbit));
            _rabbitHandle = _animals.Register(rabbit.visualKey, rabbit.prefab, 4, 4);
            _animals.Warmup(_rabbitHandle, 4);
            Assert.IsNotNull(_config.vfx, "DEF_HatchPresentation lists the feedback effects.");
            _vfx = new FeedbackVfx(_config.vfx, _root.transform, QualityTier.Medium);

            _recorder = new Recorder();
            _player = new HatchSequencePlayer(_config, _vfx, _recorder, _recorder);
            _time = 0f;
            yield return null;
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }

        private EggView PresentEgg()
        {
            if (!_eggs.TryAcquire(out var egg))
            {
                _failures++;
                return null;
            }
            egg.Configure(Vector3.zero, 180f, _eggVisual);
            _player.Present(egg);
            return egg;
        }

        private AnimalView NewRabbit()
        {
            var animal = new AnimalInstance
            {
                InstanceId = _nextId,
                Species = ContentId.FromSource("species.rabbit"),
                Variant = ContentId.FromSource("variant.rabbit.white"),
                Seed = _nextId++,
            };
            if (!_resolver.TryResolve(animal, Vector3.zero, 180f, out var snapshot) || !_animals.TryAcquire(_rabbitHandle, snapshot, out var view))
            {
                _failures++;
                return null;
            }
            return view;
        }

        private void Step()
        {
            _time += Dt;
            _player.Tick(_time, Dt);
            _vfx.Tick(_time);
        }

        /// <summary>One full hatch with pooled egg and animal; returns the number of ticks.</summary>
        private int RunOneHatch(RarityTier rarity, bool repeat, HatchNews news = HatchNews.None)
        {
            var egg = PresentEgg();
            for (int i = 0; i < 10; i++) Step();
            var rabbit = NewRabbit();
            if (egg == null || rabbit == null || !_player.Hatch(rabbit, ContentId.FromSource("species.rabbit"), rarity, repeat, _recorder, news))
            {
                _failures++;
                return 0;
            }
            int ticks = 0;
            while (_player.Phase != HatchPhase.Done && ticks < 600)
            {
                Step();
                ticks++;
            }
            _eggs.Release(egg);
            _animals.Release(rabbit);
            return ticks;
        }

        [UnityTest]
        public IEnumerator CommonHatch_PlaysThreePhases_WithNamedEvents()
        {
            yield return Setup();
            var egg = PresentEgg();
            var rabbit = NewRabbit();
            Assert.IsTrue(_player.Hatch(rabbit, ContentId.FromSource("species.rabbit"), RarityTier.Common, false, _recorder));

            var seen = new bool[8];
            bool shellHiddenBeforeBurst = false;
            int frames = 0;
            while (_player.Phase != HatchPhase.Done && frames < 600)
            {
                Step();
                seen[(int)_player.Phase] = true;
                if (_player.Phase < HatchPhase.Revealing && !egg.ShellVisible) shellHiddenBeforeBurst = true;
                frames++;
                yield return null;
            }

            Assert.AreEqual(HatchPhase.Done, _player.Phase);
            Assert.IsTrue(seen[(int)HatchPhase.Anticipation] && seen[(int)HatchPhase.Cracked1] && seen[(int)HatchPhase.Cracked2]
                          && seen[(int)HatchPhase.Revealing] && seen[(int)HatchPhase.Celebrating]);
            Assert.IsFalse(shellHiddenBeforeBurst);
            Assert.LessOrEqual(frames * Dt, 2.6f, "A common hatch stays short.");
            Assert.IsFalse(egg.ShellVisible);
            Assert.IsFalse(egg.FragmentsVisible);
            Assert.AreEqual(1f, rabbit.transform.localScale.x, 1e-4f);

            AudioEventId[] expected = { AudioEventId.EggTap, AudioEventId.Crack1, AudioEventId.Crack2, AudioEventId.HatchPop, AudioEventId.AnimalHappy };
            Assert.AreEqual(expected.Length, _recorder.AudioCount);
            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], _recorder.Audio[i]);
            Assert.AreEqual(HapticEventId.HatchCommon, _recorder.Haptics[_recorder.HapticCount - 1]);
            Assert.AreEqual(1, _recorder.Revealed);
            Assert.AreEqual(1, _recorder.Finished);
        }

        [UnityTest]
        public IEnumerator RareHatch_GainsAFewHundredMilliseconds_AndARevealCue()
        {
            yield return Setup();
            int common = RunOneHatch(RarityTier.Common, false);
            _recorder.Reset();
            int rare = RunOneHatch(RarityTier.Rare, false);
            float extra = (rare - common) * Dt;
            Assert.AreEqual(_config.RarityExtra(RarityTier.Rare), extra, 2.5f * Dt);
            Assert.Less(extra, 1f, "Rarer hatches gain staging, not a cutscene.");
            Assert.AreEqual(1, _recorder.Count(AudioEventId.RareReveal));
            Assert.AreEqual(HapticEventId.Rare, _recorder.Haptics[_recorder.HapticCount - 1]);
        }

        [UnityTest]
        public IEnumerator RepeatedHatch_PlaysFaster()
        {
            yield return Setup();
            int first = RunOneHatch(RarityTier.Common, false);
            int repeat = RunOneHatch(RarityTier.Common, true);
            Assert.AreEqual(first / _config.repeatSpeed, repeat, 3f);
        }

        [UnityTest]
        public IEnumerator Skip_RevealsAtOnce_WithoutDoubleEvents()
        {
            yield return Setup();
            var egg = PresentEgg();
            var rabbit = NewRabbit();
            _player.Hatch(rabbit, ContentId.FromSource("species.rabbit"), RarityTier.Epic, false, _recorder);
            for (int i = 0; i < 20; i++) Step();
            Assert.AreEqual(HatchPhase.Anticipation, _player.Phase);

            _player.Skip();
            Assert.AreEqual(HatchPhase.Done, _player.Phase);
            Assert.IsFalse(egg.ShellVisible);
            Assert.IsFalse(egg.FragmentsVisible);
            Assert.AreEqual(1f, rabbit.transform.localScale.x, 1e-4f);
            Assert.AreEqual(1, _recorder.Count(AudioEventId.HatchPop));
            Assert.AreEqual(1, _recorder.Count(AudioEventId.EpicReveal));
            Assert.AreEqual(1, _recorder.Finished);

            int audio = _recorder.AudioCount;
            for (int i = 0; i < 120; i++) Step();
            Assert.AreEqual(audio, _recorder.AudioCount, "Nothing plays after a skip.");
            _player.Skip();
            Assert.AreEqual(1, _recorder.Finished);
        }

        [UnityTest]
        public IEnumerator TenHatches_AfterWarmup_NoInstantiate_NoGC_AndCleanReset()
        {
            yield return Setup();
            RunOneHatch(RarityTier.Rare, false); // warmup: every pool and code path used once
            int eggs = _eggs.InstantiateCount;
            int animals = _animals.GetPool(_rabbitHandle).InstantiateCount;
            var effects = new int[(int)VfxId.Count];
            for (int i = 0; i < effects.Length; i++)
                effects[i] = _vfx.GetPool((VfxId)i)?.InstantiateCount ?? -1;
            _recorder.Reset();

            // Every rarity accent and both discovery rings play inside the measured loop (T34A, Gate 10 and 22).
            Assert.That(() =>
            {
                for (int h = 0; h < 10; h++)
                    RunOneHatch((RarityTier)(h % 6), h > 0, (HatchNews)(h % 3));
            }, Is.Not.AllocatingGCMemory());

            Assert.AreEqual(0, _failures);
            Assert.AreEqual(eggs, _eggs.InstantiateCount);
            Assert.AreEqual(animals, _animals.GetPool(_rabbitHandle).InstantiateCount);
            for (int i = 0; i < effects.Length; i++)
                Assert.AreEqual(effects[i], _vfx.GetPool((VfxId)i)?.InstantiateCount ?? -1, "No effect instantiates after warmup: " + (VfxId)i);
            Assert.AreEqual(10, _recorder.Finished);
            Assert.AreEqual(10, _recorder.Count(AudioEventId.HatchPop));

            // A recycled egg comes back intact: shell shown, no crack, fragments hidden at rest.
            Assert.IsTrue(_eggs.TryAcquire(out var egg));
            Assert.IsTrue(egg.ShellVisible);
            Assert.IsFalse(egg.FragmentsVisible);
            Assert.AreEqual(0, EggVisualPacking.CrackStage(egg.VisualData));
            _eggs.Release(egg);
        }
    }
}
