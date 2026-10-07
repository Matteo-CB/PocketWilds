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
    /// T35B, doc 02 Œufs section 5 and doc 05 Sensations section 30 (PlayMode): the × 10 staging on its own. Eggs pop
    /// 40 ms apart, burst in a wave from left to right (60 ms apart), at most three full hatch effects and five pop
    /// sounds, one crack sound per stage, the rarest result's vibration at its moment, the newborns handed over where
    /// they came out, reduced motion throws no shell. Ten × 10 hatches after one warmup: no Instantiate, no garbage.
    /// </summary>
    public class MultiHatchPlayerTests
    {
        private const float Dt = 1f / 60f;
        private const int Eggs = MultiHatchPlayer.MaxEggs;

        /// <summary>Records feedback and listener calls into fixed arrays (no allocation).</summary>
        private sealed class Recorder : IAudioService, IHapticService, IMultiHatchListener
        {
            public readonly AudioEventId[] Audio = new AudioEventId[512];
            public readonly HapticEventId[] Haptics = new HapticEventId[512];
            public readonly int[] Moments = new int[64];
            public int AudioCount, HapticCount, MomentCount, Revealed, Finished;

            public void Play(AudioEventId id, Vector3 position, ContentId context)
            {
                if (AudioCount < Audio.Length) Audio[AudioCount++] = id;
            }

            public void Play(HapticEventId id)
            {
                if (HapticCount < Haptics.Length) Haptics[HapticCount++] = id;
            }

            public void OnMultiRevealed() => Revealed++;

            public void OnMultiMoment(int index)
            {
                if (MomentCount < Moments.Length) Moments[MomentCount++] = index;
            }

            public void OnMultiFinished() => Finished++;

            public void Reset() => AudioCount = HapticCount = MomentCount = Revealed = Finished = 0;

            public int Count(AudioEventId id)
            {
                int n = 0;
                for (int i = 0; i < AudioCount; i++) if (Audio[i] == id) n++;
                return n;
            }

            public int Count(HapticEventId id)
            {
                int n = 0;
                for (int i = 0; i < HapticCount; i++) if (Haptics[i] == id) n++;
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
        private MultiHatchPlayer _player;
        private Recorder _recorder;
        private uint _eggVisual;
        private float _time;
        private uint _nextId = 1;
        private int _failures;

        private readonly EggView[] _eggViews = new EggView[Eggs];
        private readonly AnimalView[] _animalViews = new AnimalView[Eggs];
        private readonly ContentId[] _species = new ContentId[Eggs];
        private readonly RarityTier[] _rarity = new RarityTier[Eggs];
        private readonly HatchNews[] _news = new HatchNews[Eggs];

        /// <summary>Egg spots in a shuffled order: the wave must follow x, not the order of the results.</summary>
        private static readonly float[] SpotX = { 0.46f, -0.92f, 0.92f, 0f, -0.46f, -0.46f, 0.92f, 0f, -0.92f, 0.46f };

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
            _camera.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, -4f), Quaternion.Euler(20f, 0f, 0f));

            _root = new GameObject("MultiHatchTestRoot");
            _eggs = new GameObjectPool<EggView>(_config.eggPrefab, _root.transform, Eggs, Eggs);
            _eggs.Warmup(Eggs);
            _animals = new AnimalViewPool(_root.transform);
            Assert.IsTrue(catalog.TryGet("PF_Animal_Rabbit", out var rabbit));
            _rabbitHandle = _animals.Register(rabbit.visualKey, rabbit.prefab, Eggs, Eggs);
            _animals.Warmup(_rabbitHandle, Eggs);
            _vfx = new FeedbackVfx(_config.vfx, _root.transform, QualityTier.Medium);
            _recorder = new Recorder();
            _player = new MultiHatchPlayer(_config, _vfx, _recorder, _recorder);
            _time = 0f;
            yield return null;
#else
            Assert.Ignore("Editor only.");
            yield break;
#endif
        }

        private void Step()
        {
            _time += Dt;
            _player.Tick(_time, Dt);
            _vfx.Tick(_time);
        }

        /// <summary>Ten pooled eggs on their spots and ten newborns; the results given by <paramref name="seed"/>.</summary>
        private bool Prepare(int seed)
        {
            for (int i = 0; i < Eggs; i++)
            {
                if (!_eggs.TryAcquire(out var egg))
                {
                    _failures++;
                    return false;
                }
                egg.Configure(new Vector3(SpotX[i], 0f, i < 5 ? -0.22f : 0.42f), 180f, _eggVisual);
                _eggViews[i] = egg;
                var animal = new AnimalInstance
                {
                    InstanceId = _nextId,
                    Species = ContentId.FromSource("species.rabbit"),
                    Variant = ContentId.FromSource("variant.rabbit.white"),
                    Seed = _nextId++,
                };
                var reveal = egg.RevealPoint;
                if (!_resolver.TryResolve(animal, reveal.position, reveal.eulerAngles.y, out var snapshot)
                    || !_animals.TryAcquire(_rabbitHandle, snapshot, out var view))
                {
                    _failures++;
                    return false;
                }
                _animalViews[i] = view;
                _species[i] = animal.Species;
                // One Epic, one Rare, one new species, one new color among commons and uncommons.
                int k = (i + seed) % Eggs;
                _rarity[i] = k == 0 ? RarityTier.Epic : k == 1 ? RarityTier.Rare : k % 2 == 0 ? RarityTier.Uncommon : RarityTier.Common;
                _news[i] = k == 2 ? HatchNews.NewSpecies : k == 3 ? HatchNews.NewVariant : HatchNews.None;
            }
            return true;
        }

        private void ReleaseAll()
        {
            for (int i = 0; i < Eggs; i++)
            {
                if (_eggViews[i] != null)
                    _eggs.Release(_eggViews[i]);
                if (_animalViews[i] != null)
                    _animals.Release(_animalViews[i]);
                _eggViews[i] = null;
                _animalViews[i] = null;
            }
        }

        /// <summary>One whole × 10 staging; returns the number of ticks.</summary>
        private int RunOne(int seed)
        {
            if (!Prepare(seed) || !_player.Play(_eggViews, _animalViews, _species, _rarity, _news, Eggs, _recorder))
            {
                _failures++;
                return 0;
            }
            int ticks = 0;
            while (_player.IsPlaying && ticks < 1200)
            {
                Step();
                ticks++;
            }
            ReleaseAll();
            return ticks;
        }

        [UnityTest]
        public IEnumerator TenEggs_PopApart_BurstInAWave_FivePops_ThreeFullEffects()
        {
            yield return Setup();
            Assert.IsTrue(Prepare(0));
            Assert.IsTrue(_player.Play(_eggViews, _animalViews, _species, _rarity, _news, Eggs, _recorder));
            Assert.AreEqual(MultiHatchPlayer.FullEffects, _player.FullEffectCount, "At most three full hatch effects at once.");

            // The wave follows the eggs from left to right, 60 ms apart, whatever the order of the results.
            for (int a = 0; a < Eggs; a++)
            {
                for (int b = 0; b < Eggs; b++)
                {
                    float xa = _player.Egg(a).transform.position.x, xb = _player.Egg(b).transform.position.x;
                    if (xa < xb - 1e-4f)
                        Assert.Less(_player.BurstTime(a), _player.BurstTime(b), "Left eggs burst first.");
                }
            }
            float first = float.MaxValue, last = 0f;
            for (int i = 0; i < Eggs; i++)
            {
                first = Mathf.Min(first, _player.BurstTime(i));
                last = Mathf.Max(last, _player.BurstTime(i));
            }
            Assert.AreEqual((Eggs - 1) * MultiHatchPlayer.BurstWaveStep, last - first, 1e-4f, "Ten eggs, nine steps of 60 ms.");
            Assert.AreEqual(0.4f + MultiHatchPlayer.CrackTogetherSeconds, first, 1e-4f, "Crack together for 1.5 s after the pop.");

            // The fourth egg is still popping in while the first has settled (400 ms each, 40 ms apart).
            for (int i = 0; i < 18; i++) Step();
            Assert.AreEqual(1f, _player.Egg(0).Shell.localScale.x, 0.13f, "The first egg is in place.");
            Assert.Less(_player.Egg(9).Shell.localScale.x, 0.9f, "The last one is still popping in.");

            int ticks = 0;
            while (_player.IsPlaying && ticks < 1200)
            {
                Step();
                ticks++;
            }
            Assert.IsFalse(_player.IsPlaying);
            Assert.AreEqual(Eggs, _player.BurstCount);
            Assert.AreEqual(MultiHatchPlayer.MaxPops, _recorder.Count(AudioEventId.HatchPop), "Five pops at most for ten eggs.");
            Assert.AreEqual(1, _recorder.Count(AudioEventId.Crack1), "One crack sound for all.");
            Assert.AreEqual(1, _recorder.Count(AudioEventId.Crack2));
            Assert.AreEqual(1, _recorder.Count(AudioEventId.EpicReveal), "Then the sound of the rarest result, once.");
            Assert.AreEqual(1, _recorder.Count(HapticEventId.HatchCommon), "HatchCommon at the burst.");
            Assert.AreEqual(1, _recorder.Count(HapticEventId.Rare), "The rarest result's vibration at its moment.");
            Assert.AreEqual(4, _recorder.MomentCount, "Epic, Rare, the new species and the new color each get a moment.");
            Assert.AreEqual(1, _recorder.Finished);
            for (int i = 0; i < Eggs; i++)
            {
                var expected = _player.Egg(i).RevealPoint.position;
                var at = _player.Animal(i).transform.position;
                Assert.AreEqual(expected.x, at.x, 1e-3f, "Each newborn is handed over where it came out (the meadow walks it out).");
                Assert.AreEqual(expected.z, at.z, 1e-3f);
                Assert.AreEqual(1f, _player.Animal(i).transform.localScale.x, 1e-4f);
                Assert.IsFalse(_player.Egg(i).FragmentsVisible);
            }
            ReleaseAll();
        }

        [UnityTest]
        public IEnumerator ReducedMotion_ThrowsNoShell_AndSkipPassesTheMoments()
        {
            yield return Setup();
            _player.ReduceMotion = true;
            Assert.IsTrue(Prepare(3));
            Assert.IsTrue(_player.Play(_eggViews, _animalViews, _species, _rarity, _news, Eggs, _recorder));
            int ticks = 0;
            bool shells = false;
            while (_player.IsPlaying && ticks < 1200)
            {
                Step();
                for (int i = 0; i < Eggs; i++)
                    shells |= _player.Egg(i).FragmentsVisible;
                if (_player.CurrentMoment >= 0)
                    _player.Skip();
                ticks++;
            }
            Assert.IsFalse(shells, "Reduced motion: no flying shell.");
            Assert.AreEqual(1, _recorder.Finished);
            Assert.Less(ticks, (int)((0.4f + MultiHatchPlayer.CrackTogetherSeconds + 1.6f) / Dt), "A touch passes each moment.");
            ReleaseAll();
        }

        [UnityTest]
        public IEnumerator TenByTen_AfterWarmup_NoInstantiate_NoGC()
        {
            yield return Setup();
            RunOne(0); // warmup: pools, effects and code paths used once
            int eggs = _eggs.InstantiateCount;
            int animals = _animals.GetPool(_rabbitHandle).InstantiateCount;
            var effects = new int[(int)VfxId.Count];
            for (int i = 0; i < effects.Length; i++)
                effects[i] = _vfx.GetPool((VfxId)i)?.InstantiateCount ?? -1;
            _recorder.Reset();

            Assert.That(() =>
            {
                for (int h = 0; h < 10; h++)
                    RunOne(h);
            }, Is.Not.AllocatingGCMemory());

            Assert.AreEqual(0, _failures);
            Assert.AreEqual(10, _recorder.Finished);
            Assert.AreEqual(eggs, _eggs.InstantiateCount);
            Assert.AreEqual(animals, _animals.GetPool(_rabbitHandle).InstantiateCount);
            for (int i = 0; i < effects.Length; i++)
                Assert.AreEqual(effects[i], _vfx.GetPool((VfxId)i)?.InstantiateCount ?? -1, "No effect instantiates after warmup: " + (VfxId)i);
        }
    }
}
