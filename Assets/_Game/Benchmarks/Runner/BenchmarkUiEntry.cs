using System;
using System.Collections;
using System.Diagnostics;
using PocketWilds.Content;
using PocketWilds.Core;
using PocketWilds.Presentation;
using PocketWilds.Simulation;
using PocketWilds.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PocketWilds.Benchmarks
{
    /// <summary>
    /// SC_Benchmark_UI driver (L-SC-007, R-BENCH-5K, gate 13; editor and development builds only). A synthetic
    /// collection of 5000 animals over 5000 species in 6 biomes (generated at start, no texture per entry) in the real
    /// PF_UI_Codex: open, 30 s of fast scrolling up and down the whole list, 20 close and reopen cycles, while the
    /// real HUD counts a fast-changing Light value. The runner samples every frame (GC, UI, memory).
    /// </summary>
    public sealed class BenchmarkUiEntry : MonoBehaviour, ICodexActions, IRefugeActions
    {
        private sealed class Keys : ILocalization
        {
            public string Language => "en";
            public string Get(string key) => key;
        }

        [SerializeField] private CodexScreen _codexPrefab;
        [SerializeField] private RefugeHud _hudPrefab;
        [SerializeField, Min(1)] private int _species = 5000;
        [SerializeField, Min(1)] private int _biomes = 6;
        [Tooltip("Animals per found species (every 3rd species is found): 3 gives a 5000-animal collection.")]
        [SerializeField, Min(1)] private int _animalsPerFound = 3;
        [SerializeField] private bool _runOnStart = true;

        /// <summary>Set by the benchmark runner before loading the scene: it drives the run and samples the frames.</summary>
        public static bool DrivenByRunner;

        private CodexScreen _codex;
        private RefugeHud _hud;
        private CodexService _service;
        private readonly Keys _keys = new Keys();

        public int Species => _species;
        public int Animals { get; private set; }
        public float FirstOpenMs { get; private set; }
        public float ReopenMaxMs { get; private set; }
        public int OpenCloseCycles { get; private set; }
        public int RowViews => _codex != null ? _codex.CreatedRowViews : 0;
        public int Rows => _codex != null ? _codex.RowCount : 0;
        public bool Ready => _service != null;

        private void Start()
        {
            Build();
            if (_runOnStart && !DrivenByRunner)
                StartCoroutine(Run(30f, 20));
        }

        private void Build()
        {
            if (_service != null)
                return;
            var builder = new ContentRegistryBuilder();
            for (int b = 0; b < _biomes; b++)
                builder.AddBiome(new BiomeDefinition(ContentId.FromSource("biome.b" + b), "biome.b" + b + ".name", b), "biome.b" + b);
            var state = PlayerState.CreateNew(DateTimeOffset.UtcNow);
            for (int i = 0; i < _species; i++)
            {
                string id = "species.s" + i;
                var speciesId = ContentId.FromSource(id);
                builder.AddSpecies(new SpeciesDefinition(speciesId, id + ".name", RigFamily.SmallQuadruped,
                    ContentId.FromSource("biome.b" + (i % _biomes)), 1.0, "PF_Animal_Test"), id);
                string variant = "variant.s" + i + ".a";
                var variantId = ContentId.FromSource(variant);
                builder.AddVariant(new VariantDefinition(variantId, speciesId, variant + ".name", RarityTier.Common,
                    new Color32[VariantDefinition.PaletteSize], PatternChannel.None, 0), variant);
                if (i % 3 != 0)
                    continue;
                for (int k = 0; k < _animalsPerFound; k++)
                {
                    state.Collection.Add(new AnimalInstance { Species = speciesId, Variant = variantId, Seed = (uint)(i * 7 + k) });
                    Animals++;
                }
                state.Collection.DiscoveredSpecies.Add(speciesId);
                state.Collection.DiscoveredVariants.Add(variantId);
            }
            _service = new CodexService(state, builder.Build(), null);
            _codex = Instantiate(_codexPrefab);
            _hud = Instantiate(_hudPrefab);
            _hud.Bind(this);
        }

        /// <summary>Open, fast scroll for <paramref name="scrollSeconds"/>, then close and reopen <paramref name="cycles"/> times.</summary>
        public IEnumerator Run(float scrollSeconds, int cycles)
        {
            Build();
            var watch = Stopwatch.StartNew();
            _codex.Open(_service, _keys, this);
            FirstOpenMs = (float)watch.Elapsed.TotalMilliseconds;
            yield return null;

            // Fast scroll: the whole list down and up every 4 s (about 30 000 px per second), the HUD counting fast.
            float start = Time.realtimeSinceStartup;
            double light = 1e6;
            while (Time.realtimeSinceStartup - start < scrollSeconds)
            {
                float t = Time.realtimeSinceStartup - start;
                float phase = Mathf.PingPong(t / 2f, 1f);
                _codex.ScrollTo(phase * _codex.ContentHeight);
                light *= 1.013;
                _hud.SetLight(BigValue.FromDouble(light), BigValue.FromDouble(light * 0.01));
                yield return null;
            }

            for (int i = 0; i < cycles; i++)
            {
                _codex.Close();
                for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
                    yield return null;
                watch.Restart();
                _codex.Open(_service, _keys, this);
                ReopenMaxMs = Mathf.Max(ReopenMaxMs, (float)watch.Elapsed.TotalMilliseconds);
                OpenCloseCycles++;
                for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
                    yield return null;
            }
        }

        public void CodexClosed()
        {
        }

        public void RequestHatch()
        {
        }

        public void OpenCodex()
        {
        }

        public void OpenShop()
        {
        }

        public void OpenEggInfo()
        {
        }

        private void OnDestroy()
        {
            if (_codex != null)
                Object.Destroy(_codex.gameObject);
            if (_hud != null)
                Object.Destroy(_hud.gameObject);
        }
    }
}
