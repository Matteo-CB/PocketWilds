using System.Collections.Generic;
using PocketWilds.Editor.Activities;
using UnityEngine;
using S = PocketWilds.Editor.Albums.StickerSubject;

namespace PocketWilds.Editor.Albums
{
    /// <summary>
    /// T44A: the stickers capturable today (doc « 01 Albums et autocollants », sections 2.1 and 5.1 to 5.6): only those whose
    /// subjects exist in this build (Rabbit, Squirrel, Hedgehog, the Prairie zones and kit, the props of the 6 shipped
    /// activities). Every other sticker of those pages is listed in <see cref="Unavailable"/> with the reason; its slot shows
    /// the soft « bientôt » silhouette until its species or prop exists. Positions are in meters in the sticker frame (x right,
    /// z away from the camera at azimuth 0); the pipeline refuses any overlap and finds the nearest clear spot in a zone.
    /// </summary>
    public static class StickerCaptureDefinitions
    {
        public const string PageGrassland1 = "page.grassland.1";
        public const string PageGrassland2 = "page.grassland.2";
        public const string PageGrooming = "page.activity.001";
        public const string PageHideSeek = "page.activity.002";
        public const string PageNutDrop = "page.activity.003";
        public const string PageMemory = "page.activity.004";
        public const string PageExpeditions = "page.activity.005";
        public const string PageGarden = "page.activity.006";

        private const string Gl = "Assets/_Game/Prefabs/Environment/Grassland/PF_GL_";
        private const string Kit = NutDropPipeline.KitFolder;
        private const string EnvMaterial = GroomingPipeline.EnvironmentCommonPath;
        private const string Brush = GroomingPipeline.ModelsFolder + "/" + GroomingPipeline.BrushModel + ".fbx";
        private const string Bow = GroomingPipeline.ModelsFolder + "/" + GroomingPipeline.BowModel + ".fbx";
        private const string Debris = GroomingPipeline.ModelsFolder + "/" + GroomingPipeline.DebrisModel + ".fbx";
        private const string NutProps = NutDropPipeline.ModelsFolder + "/" + NutDropPipeline.PropsModel + ".fbx";
        private const string GardenModel = GardenSetPipeline.ModelsFolder + "/" + GardenSetPipeline.SetModel + ".fbx";
        private const string Plant = GardenSetPipeline.PlantPrefabFolder + "/PF_GardenPlant_";

        private const string Grass = "palette.grassland.grass";
        private const string DaisyWhite = "palette.grassland.flower_white";
        private const string Clover = "palette.grassland.clover";
        private const string Poppy = "palette.grassland.flower_red";

        private static readonly List<StickerCaptureDefinition> _all = Build();

        /// <summary>Every capturable sticker, in page order.</summary>
        public static IReadOnlyList<StickerCaptureDefinition> All => _all;

        /// <summary>The page keys with at least one capturable sticker, in order.</summary>
        public static readonly string[] Pages = { PageGrassland1, PageGrassland2, PageGrooming, PageHideSeek, PageNutDrop, PageExpeditions, PageGarden };

        /// <summary>The stickers of the same pages that cannot be captured today, and why.</summary>
        public static readonly (string id, string reason)[] Unavailable =
        {
            ("STK-GL-07", "Hamster: species not in the game yet"),
            ("STK-GL-22", "big leaf prop missing, and the light rain is not frozen by the capture tool yet"),
            ("STK-GL-23", "the rainbow weather is not frozen by the capture tool yet"),
            ("STK-GL-24", "golden butterfly (signature moment): effect not in the game yet"),
            ("STK-ACT-001-01", "cushion prop missing (the brush exists)"),
            ("STK-ACT-001-05", "Renard: species not in the game yet"),
            ("STK-ACT-001-06", "Faon: species not in the game yet"),
            ("STK-ACT-001-07", "Cochon d'Inde: species not in the game yet"),
            ("STK-ACT-001-11", "Panda roux: species not in the game yet"),
            ("STK-ACT-001-12", "Faon: species not in the game yet"),
            ("STK-ACT-002-03", "big mushroom prop missing (the Prairie has only the tiny scatter mushroom)"),
            ("STK-ACT-002-04", "Renard: species not in the game yet"),
            ("STK-ACT-002-05", "Raton laveur: species not in the game yet"),
            ("STK-ACT-002-08", "Blaireau and ferns: species and prop not in the game yet"),
            ("STK-ACT-002-09", "wooden magnifier prop missing"),
            ("STK-ACT-002-10", "Mulot and hollow trunk: species and prop not in the game yet"),
            ("STK-ACT-002-11", "Chouette: species not in the game yet"),
            ("STK-ACT-003-10", "the Forest biome is not in the game yet"),
            ("STK-ACT-003-11", "Marcassin: species not in the game yet"),
            ("STK-ACT-004-01", "Memory cards are 2D UI sprites: no 3D card model to capture yet"),
            ("STK-ACT-004-02", "no 3D card model; Renard print"),
            ("STK-ACT-004-03", "no 3D card model; duck print"),
            ("STK-ACT-004-04", "no 3D card model"),
            ("STK-ACT-004-05", "no 3D card model"),
            ("STK-ACT-004-06", "no 3D card model"),
            ("STK-ACT-004-07", "no 3D card model"),
            ("STK-ACT-004-08", "no 3D card model; Chouette not in the game yet"),
            ("STK-ACT-004-09", "no 3D card model"),
            ("STK-ACT-004-10", "no 3D card model; Lynx and Castor not in the game yet"),
            ("STK-ACT-004-11", "no 3D card model, ribbon prop missing"),
            ("STK-ACT-004-12", "no 3D card model, most print species missing"),
            ("STK-ACT-005-01", "small canvas backpack prop missing"),
            ("STK-ACT-005-02", "the Lake (its postcard) is not in the game yet"),
            ("STK-ACT-005-05", "Mulot: species not in the game yet"),
            ("STK-ACT-005-06", "full bags prop missing"),
            ("STK-ACT-005-07", "the postcard is a 2D card art: no dog-eared card model"),
            ("STK-ACT-005-08", "the Discovery egg exists but the capture tool does not spawn eggs yet (next round)"),
            ("STK-ACT-005-09", "wooden compass prop missing"),
            ("STK-ACT-005-10", "six biomes' postcards: only the Prairie exists"),
            ("STK-ACT-005-12", "world map with pins prop missing"),
            ("STK-ACT-006-01", "seed and soil hole props missing"),
            ("STK-ACT-006-07", "harvested crop prop missing (plants exist rooted only)"),
            ("STK-ACT-006-08", "dye pot prop missing"),
            ("STK-ACT-006-09", "loose fruits, flowers and round vegetables props missing"),
            ("STK-ACT-006-10", "herbarium prop missing"),
            ("STK-ACT-006-11", "dye pots prop missing"),
        };

        private static StickerCaptureDefinition D(string id, string page, StickerFraming framing, StickerLight light, string note, params S[] subjects)
        {
            var d = new StickerCaptureDefinition
            {
                id = id, page = page, framing = framing, light = light, note = note,
                shape = StickerCaptureDefinition.DefaultShape(framing),
                elevation = framing == StickerFraming.Landscape ? 8f : 10f,
            };
            d.subjects.AddRange(subjects);
            return d;
        }

        private static StickerCaptureDefinition InZone(this StickerCaptureDefinition d, string zone, float x = 0f, float z = 0f)
        {
            d.backdrop = StickerBackdrop.Zone;
            d.zone = zone;
            d.anchor = new Vector3(x, 0f, z);
            return d;
        }

        private static StickerCaptureDefinition InGarden(this StickerCaptureDefinition d)
        {
            d.backdrop = StickerBackdrop.GardenSet;
            return d;
        }

        private static StickerCaptureDefinition View(this StickerCaptureDefinition d, float azimuth, float elevation, float zoom = 1f)
        {
            d.azimuth = azimuth;
            d.elevation = elevation;
            d.zoom = zoom;
            return d;
        }

        private static StickerCaptureDefinition Far(this StickerCaptureDefinition d, float distance, float aim = 0.3f, bool life = false)
        {
            d.distance = distance;
            d.aimHeight = aim;
            d.life = life;
            return d;
        }

        private static S Nut(string mesh, string palette, float x = 0f, float z = 0f, bool gold = false) =>
            S.Mesh(NutProps, mesh, gold ? NutDropPipeline.GoldMaterialPath : NutDropPipeline.PropsMaterialPath, palette, x, z);

        private static S Hazelnut(float x = 0f, float z = 0f) => Nut("ND_Hazelnut", NutDropPipeline.PaletteHazelnut, x, z);
        private static S Berry(float x = 0f, float z = 0f) => Nut("ND_Berry", NutDropPipeline.PaletteBerry, x, z);
        private static S Basket(float x = 0f, float z = 0f) => Nut(NutDropPipeline.Basket, NutDropPipeline.PaletteBasket, x, z);
        private static S KitMesh(string mesh, string palette, float x, float z, float yaw = 0f) => S.Mesh(Kit, mesh, EnvMaterial, palette, x, z, yaw);
        private static S GlProp(string name, float x, float z, float yaw = 0f) => S.Prefab(Gl + name + ".prefab", x, z, yaw);
        private static S GardenPlant(string name) => S.Prefab(Plant + name + ".prefab", 0f, 0f);

        private static List<StickerCaptureDefinition> Build()
        {
            var list = new List<StickerCaptureDefinition>
            {
                // ---------------------------------------------------------------- Prairie page 1: inhabitants
                D("STK-GL-01", PageGrassland1, StickerFraming.Full, StickerLight.Morning,
                    "Rabbit three-quarter, looking around (IdleLook), white daisies and a tuft of the Start at its paws",
                    S.Animal("rabbit.brown", 0f, 0f, 205f, "IdleLook", 0.35f),
                    KitMesh("GL_Flower_A", DaisyWhite, -0.34f, -0.14f, 20f),
                    KitMesh("GL_Flower_A", DaisyWhite, 0.32f, -0.2f, 140f),
                    KitMesh("GL_Flower_A", DaisyWhite, 0.2f, 0.34f, 260f),
                    GlProp("GrassTuft_S", -0.3f, 0.3f, 60f).Palette(Grass)).View(0f, 8f),
                D("STK-GL-02", PageGrassland1, StickerFraming.Portrait, StickerLight.Day,
                    "Squirrel eating (Eat), a hazelnut on the stump GL_Stump beside it (held nut: no paw socket yet)",
                    S.Animal("squirrel.red", 0f, 0f, 200f, "Eat", 0.4f).Focus(),
                    GlProp("Stump", 0.6f, 0.6f, 30f),
                    Hazelnut(0.6f, 0.6f).On(1)).View(0f, 6f),
                D("STK-GL-03", PageGrassland1, StickerFraming.Detail, StickerLight.Day,
                    "Hedgehog curled asleep (Sleep), clover GL_Clover_A around it",
                    S.Animal("hedgehog.brown", 0f, 0f, 200f, "Sleep", 0.5f),
                    KitMesh("GL_Clover_A", Clover, -0.24f, 0.04f, 0f),
                    KitMesh("GL_Clover_A", Clover, 0.22f, -0.12f, 120f),
                    KitMesh("GL_Clover_A", Clover, 0.06f, 0.27f, 240f)).View(0f, 14f),
                // T45: the field mouse and the guinea pig are in the game.
                D("STK-GL-04", PageGrassland1, StickerFraming.Full, StickerLight.Evening,
                    "Field mouse up on a hay bale GL_HayBale, looking around (IdleLook), golden evening",
                    GlProp("HayBale", 0f, 0f, 20f),
                    S.Animal("fieldmouse.warmbrown", 0f, 0f, 205f, "IdleLook", 0.4f).On(0).Focus()).View(0f, 10f),
                D("STK-GL-05", PageGrassland1, StickerFraming.Full, StickerLight.Day,
                    "Guinea pig in profile, a little hop of joy (Happy) between tufts of tall grass",
                    S.Animal("guineapig.caramel", 0f, 0f, 260f, "Happy", 0.3f).Focus(),
                    GlProp("TallGrass_A", -0.5f, 0.34f, 30f).Palette(Grass),
                    GlProp("TallGrass_B", 0.52f, 0.32f, 200f).Palette(Grass)).View(0f, 6f),
                D("STK-GL-08", PageGrassland1, StickerFraming.Scene, StickerLight.Day,
                    "Rabbit and guinea pig asleep side by side (Sleep), a white butterfly resting on a daisy near them",
                    S.Animal("rabbit.white", -0.24f, 0f, 200f, "Sleep", 0.5f),
                    S.Animal("guineapig.tricolor", 0.24f, 0.02f, 165f, "Sleep", 0.5f),
                    KitMesh("GL_Flower_A", DaisyWhite, 0.02f, -0.34f, 40f),
                    S.Mesh(PostcardSetPipeline.ModelsFolder + "/" + PostcardSetPipeline.Model + ".fbx", PostcardSetPipeline.ButterflyOpen,
                        PostcardSetPipeline.PropsMaterialPath, PostcardSetPipeline.PaletteButterfly, 0.02f, -0.34f, 150f).On(2)).View(0f, 12f),
                D("STK-GL-09", PageGrassland1, StickerFraming.Scene, StickerLight.Day,
                    "Squirrel and field mouse face to face by the stump, a hazelnut on the stump between them (no paw socket yet)",
                    S.Animal("squirrel.red", -0.36f, -0.04f, 125f, "Eat", 0.4f),
                    S.Animal("fieldmouse.sand", 0.32f, 0.0f, 235f, "IdleLook", 0.5f),
                    GlProp("Stump", 0f, 0.5f, 30f),
                    Hazelnut(0f, 0.5f).On(2)).View(0f, 8f),
                D("STK-GL-11", PageGrassland1, StickerFraming.Scene, StickerLight.Evening,
                    "Rabbit, squirrel, field mouse and hedgehog sitting in a ring at the Stream Bend, golden evening",
                    S.Animal("rabbit.brown", -0.5f, 0f, 160f, "Idle", 0.2f),
                    S.Animal("squirrel.red", -0.18f, 0.32f, 175f, "Idle", 0.5f),
                    S.Animal("fieldmouse.warmbrown", 0.16f, 0.34f, 190f, "Idle", 0.7f),
                    S.Animal("hedgehog.brown", 0.48f, 0.02f, 205f, "Idle", 0.4f)).InZone("streambend", 0f, 1.6f).Far(9f, 0.8f),
                // T46: the fawn is in the game, the Prairie has its six species.
                D("STK-GL-06", PageGrassland1, StickerFraming.Full, StickerLight.Morning,
                    "Fawn standing among the Prairie flowers, a surprised look (React), morning",
                    S.Animal("fawn.chestnutspots", 0f, 0f, 205f, "React", 0.25f).Focus(),
                    KitMesh("GL_Flower_A", DaisyWhite, -0.36f, -0.2f, 20f),
                    KitMesh("GL_Flower_D", Poppy, 0.38f, -0.16f, 140f),
                    KitMesh("GL_Flower_A", DaisyWhite, 0.3f, 0.36f, 260f)).View(0f, 6f),
                D("STK-GL-10", PageGrassland1, StickerFraming.Scene, StickerLight.Day,
                    "Hedgehog and fawn peeking out on each side of GL_Tree_D at the edge of its crown (nothing enters the tree), Orchard light",
                    GlProp("Tree_D", 0f, 0.2f, 0f),
                    S.Animal("hedgehog.brown", -1.45f, 0.05f, 140f, "Inspect", 0.4f),
                    S.Animal("fawn.lightbrown", 1.5f, 0.1f, 225f, "IdleLook", 0.35f)).View(0f, 8f),
                D("STK-GL-12", PageGrassland1, StickerFraming.Scene, StickerLight.Evening,
                    "The six meadow species close together, the fawn in the middle, all looking at the camera, the Old Tree behind",
                    S.Animal("fawn.chestnutspots", 0f, 0.16f, 185f, "Idle", 0.3f),
                    S.Animal("rabbit.brown", -0.62f, 0.06f, 170f, "Idle", 0.2f),
                    S.Animal("squirrel.red", 0.62f, 0.06f, 195f, "Idle", 0.5f),
                    S.Animal("hedgehog.brown", -0.36f, -0.56f, 175f, "Idle", 0.4f),
                    S.Animal("fieldmouse.warmbrown", 0.02f, -0.62f, 185f, "Idle", 0.7f),
                    S.Animal("guineapig.caramel", 0.4f, -0.56f, 195f, "Idle", 0.6f)).InZone("hill", 0f, 2.2f).Far(10f, 0.8f),

                // ---------------------------------------------------------------- Prairie page 2: the biome (zone landscapes)
                D("STK-GL-13", PageGrassland2, StickerFraming.Landscape, StickerLight.Morning, "The Start: egg pedestal, path, grass and daisies")
                    .InZone("start", 0f, 0.8f).Far(8f, 0.5f, true),
                D("STK-GL-14", PageGrassland2, StickerFraming.Landscape, StickerLight.Day, "The Flower Meadow").InZone("meadow", 0f, 0.8f).Far(8f, 0.5f, true),
                D("STK-GL-15", PageGrassland2, StickerFraming.Landscape, StickerLight.Day, "The Stream Bend, its stepping stones").InZone("streambend", 0f, 0.8f).Far(8f, 0.5f, true),
                D("STK-GL-16", PageGrassland2, StickerFraming.Landscape, StickerLight.Day, "The Orchard, fruit trees GL_Tree_D").InZone("orchard", 0f, 0.8f).Far(8f, 0.5f, true),
                D("STK-GL-17", PageGrassland2, StickerFraming.Landscape, StickerLight.Evening,
                    "The Hill and the Old Tree whole, a Rabbit and a Hedgehog resting near it",
                    S.Animal("rabbit.brown", -0.35f, 0f, 170f, "Idle", 0.2f),
                    S.Animal("hedgehog.brown", 0.35f, 0.1f, 200f, "Idle", 0.6f)).InZone("hill", 0f, 2.2f).Far(10f, 0.8f),
                D("STK-GL-18", PageGrassland2, StickerFraming.Landscape, StickerLight.Evening, "The Windmill Rise, sails turning (frozen)")
                    .InZone("windmillrise", 0f, 3.2f).Far(12f, 1.2f, true),
                D("STK-GL-19", PageGrassland2, StickerFraming.Landscape, StickerLight.Morning, "The Pond Edge").InZone("pondedge", 0f, 0.8f).Far(8f, 0.5f, true),
                D("STK-GL-20", PageGrassland2, StickerFraming.Landscape, StickerLight.Evening, "The Forest Edge, the first tall trees").InZone("forestedge", 0f, 0.8f).Far(8f, 0.6f, true),
                D("STK-GL-21", PageGrassland2, StickerFraming.Detail, StickerLight.Day,
                    "Three white butterflies (PC_Butterfly_Open) resting on daisies and a poppy of the Prairie kit",
                    KitMesh("GL_Flower_A", DaisyWhite, -0.17f, 0f, 0f),
                    KitMesh("GL_Flower_D", Poppy, 0f, 0.1f, 90f),
                    KitMesh("GL_Flower_A", DaisyWhite, 0.17f, -0.03f, 200f),
                    S.Mesh(PostcardSetPipeline.ModelsFolder + "/" + PostcardSetPipeline.Model + ".fbx", PostcardSetPipeline.ButterflyOpen,
                        PostcardSetPipeline.PropsMaterialPath, PostcardSetPipeline.PaletteButterfly, -0.17f, 0f, 160f).On(0),
                    S.Mesh(PostcardSetPipeline.ModelsFolder + "/" + PostcardSetPipeline.Model + ".fbx", PostcardSetPipeline.ButterflyOpen,
                        PostcardSetPipeline.PropsMaterialPath, PostcardSetPipeline.PaletteButterfly, 0f, 0.1f, 210f).On(1),
                    S.Mesh(PostcardSetPipeline.ModelsFolder + "/" + PostcardSetPipeline.Model + ".fbx", PostcardSetPipeline.ButterflyOpen,
                        PostcardSetPipeline.PropsMaterialPath, PostcardSetPipeline.PaletteButterfly, 0.17f, -0.03f, 130f).On(2)).View(0f, 22f),

                // ---------------------------------------------------------------- S-ACT-001 Grooming
                D("STK-ACT-001-02", PageGrooming, StickerFraming.Detail, StickerLight.Day, "A twig (GR_Debris_Twig) flying off a happy Rabbit's back",
                    S.Animal("rabbit.brown", 0f, 0f, 240f, "Happy", 0.3f),
                    S.Mesh(Debris, "GR_Debris_Twig", GroomingPipeline.PropsMaterialPath, GroomingPipeline.PaletteDebris, 0f, 0f, 30f)
                        .At(0, AnimalSocket.Back, -0.03f, 0.1f, 0f).Tilted(25f, 40f).Scaled(1.6f)).View(0f, 12f),
                D("STK-ACT-001-03", PageGrooming, StickerFraming.Detail, StickerLight.Day, "A leaf (GR_Debris_Leaf) caught on a Squirrel's back",
                    S.Animal("squirrel.red", 0f, 0f, 235f, "Idle", 0.4f),
                    S.Mesh(Debris, "GR_Debris_Leaf", GroomingPipeline.PropsMaterialPath, GroomingPipeline.PaletteDebris, 0f, 0f, 15f)
                        .At(0, AnimalSocket.Back, 0f, 0.006f, 0f).Tilted(10f, 15f).Scaled(1.4f)).View(0f, 16f),
                D("STK-ACT-001-04", PageGrooming, StickerFraming.Portrait, StickerLight.Day, "Hedgehog with its eyes closed (Sleep)",
                    S.Animal("hedgehog.brown", 0f, 0f, 195f, "Sleep", 0.6f).Focus()).View(0f, 8f),
                D("STK-ACT-001-08", PageGrooming, StickerFraming.Scene, StickerLight.Day, "Rabbit giggling (Happy) as the brush passes over its back",
                    S.Animal("rabbit.brown", 0f, 0f, 210f, "Happy", 0.45f),
                    S.Mesh(Brush, null, GroomingPipeline.PropsMaterialPath, GroomingPipeline.PaletteWood, 0f, 0f, 90f)
                        .At(0, AnimalSocket.Back, 0f, 0.09f, 0f).Tilted(0f, 25f)).InZone("meadow", 0f, 0.5f).View(0f, 14f),
                D("STK-ACT-001-09", PageGrooming, StickerFraming.Detail, StickerLight.Day, "The pro brush: golden handle, cream bristles",
                    S.Mesh(Brush, null, GroomingPipeline.GoldMaterialPath, GroomingPipeline.PaletteGold, 0f, 0f, 120f).Tilted(-15f, 10f)).View(0f, 24f),
                D("STK-ACT-001-10", PageGrooming, StickerFraming.Scene, StickerLight.Day, "Three clean animals in a row, each with a bow",
                    S.Animal("rabbit.brown", -0.55f, 0f, 190f, "Happy", 0.3f),
                    S.Animal("squirrel.red", 0f, 0f, 180f, "Idle", 0.5f),
                    S.Animal("hedgehog.brown", 0.55f, 0f, 170f, "Happy", 0.6f),
                    S.Mesh(Bow, null, GroomingPipeline.PropsMaterialPath, GroomingPipeline.BowPalettes[0], 0f, 0f).At(0, AnimalSocket.HeadTop, 0f, 0.004f),
                    S.Mesh(Bow, null, GroomingPipeline.PropsMaterialPath, GroomingPipeline.BowPalettes[1], 0f, 0f).At(1, AnimalSocket.HeadTop, 0f, 0.004f),
                    S.Mesh(Bow, null, GroomingPipeline.PropsMaterialPath, GroomingPipeline.BowPalettes[2], 0f, 0f).At(2, AnimalSocket.HeadTop, 0f, 0.004f))
                    .InZone("meadow", 0f, 0.5f).View(0f, 12f),

                // ---------------------------------------------------------------- S-ACT-002 Hide and Seek
                D("STK-ACT-002-01", PageHideSeek, StickerFraming.Detail, StickerLight.Day, "A Rabbit's ear peeking over a grass tuft",
                    GlProp("GrassTuft_L", 0f, 0f, 0f).Palette(Grass),
                    S.Animal("rabbit.brown", 0.04f, 0.34f, 250f, "IdleLook", 0.5f)).View(0f, 3f),
                D("STK-ACT-002-02", PageHideSeek, StickerFraming.Detail, StickerLight.Day, "Tall grass leaning, a Squirrel's tail showing beside it",
                    GlProp("TallGrass_A", 0f, 0f, 0f).Palette(Grass),
                    S.Animal("squirrel.red", 0.05f, 0.36f, 80f, "Idle", 0.2f)).View(0f, 6f),
                D("STK-ACT-002-06", PageHideSeek, StickerFraming.Portrait, StickerLight.Day, "Squirrel popping out in front of a bush (Happy)",
                    GlProp("Bush_A", 0f, 0.72f, 0f),
                    S.Animal("squirrel.red", 0f, 0f, 185f, "Happy", 0.35f).Focus()).View(0f, 6f),
                D("STK-ACT-002-07", PageHideSeek, StickerFraming.Scene, StickerLight.Day, "Five animals out of their hiding spots, proud (Happy)",
                    S.Animal("rabbit.brown", -1f, 0.15f, 160f, "Happy", 0.2f),
                    S.Animal("squirrel.red", -0.5f, -0.1f, 170f, "Happy", 0.4f),
                    S.Animal("hedgehog.brown", 0f, -0.2f, 180f, "Happy", 0.6f),
                    S.Animal("rabbit.spotted", 0.5f, -0.1f, 190f, "Happy", 0.8f),
                    S.Animal("squirrel.autumn", 1f, 0.15f, 200f, "Happy", 0.1f)).InZone("orchard", 0f, 0.5f).View(0f, 12f),
                D("STK-ACT-002-12", PageHideSeek, StickerFraming.Scene, StickerLight.Day, "Everyone comes out of their hiding spots at once: bush, tall grass, stump",
                    GlProp("Bush_A", -1f, 0.55f, 0f),
                    GlProp("TallGrass_B", 0f, 0.65f, 40f).Palette(Grass),
                    GlProp("Stump", 1f, 0.55f, 0f),
                    S.Animal("rabbit.brown", -0.95f, -0.15f, 195f, "Happy", 0.3f),
                    S.Animal("hedgehog.brown", 0f, -0.1f, 180f, "Happy", 0.5f),
                    S.Animal("squirrel.red", 0.95f, -0.15f, 165f, "Happy", 0.7f)).InZone("meadow", 0f, 0.6f).View(0f, 12f),

                // ---------------------------------------------------------------- S-ACT-003 Nut Drop
                D("STK-ACT-003-01", PageNutDrop, StickerFraming.Detail, StickerLight.Day, "A falling hazelnut (no motion trail: none in the game)",
                    Hazelnut().Tilted(20f, -30f)).View(0f, 10f),
                D("STK-ACT-003-02", PageNutDrop, StickerFraming.Detail, StickerLight.Day, "A falling acorn with its cap",
                    Nut("ND_Acorn", NutDropPipeline.PaletteAcorn).Tilted(-15f, 25f)).View(0f, 10f),
                D("STK-ACT-003-03", PageNutDrop, StickerFraming.Detail, StickerLight.Day, "A round red berry in the air", Berry().Tilted(0f, 15f)).View(0f, 10f),
                D("STK-ACT-003-04", PageNutDrop, StickerFraming.Detail, StickerLight.Day, "The woven basket full of hazelnuts and berries",
                    Basket(),
                    Hazelnut(0f, 0f).In(0, 0.62f),
                    Berry(0.45f, 0f).In(0, 0.55f),
                    Hazelnut(-0.45f, 0f).In(0, 0.55f),
                    Berry(0f, 0.45f).In(0, 0.55f),
                    Hazelnut(0f, -0.45f).In(0, 0.55f),
                    Hazelnut(0.2f, 0.2f).In(0, 0.82f)).View(0f, 24f),
                D("STK-ACT-003-05", PageNutDrop, StickerFraming.Scene, StickerLight.Day, "Squirrel leaning away (React) from a falling pine cone",
                    S.Animal("squirrel.red", 0f, 0f, 200f, "React", 0.4f),
                    Nut("ND_Pinecone", NutDropPipeline.PalettePinecone, 0.24f, -0.05f).Air(0.34f).Tilted(30f, 10f)).InZone("orchard", 0f, 0.5f).View(0f, 10f),
                D("STK-ACT-003-06", PageNutDrop, StickerFraming.Scene, StickerLight.Day, "Five hazelnuts in a row dropping into the basket",
                    Basket(),
                    Hazelnut(-0.36f, 0f).Air(0.95f),
                    Hazelnut(-0.27f, 0f).Air(0.8f),
                    Hazelnut(-0.18f, 0f).Air(0.65f),
                    Hazelnut(-0.09f, 0f).Air(0.5f),
                    Hazelnut(0f, 0f).Air(0.36f)).InZone("orchard", 0f, 0.5f).View(0f, 10f),
                D("STK-ACT-003-07", PageNutDrop, StickerFraming.Scene, StickerLight.Day, "Squirrel with its full basket on the head (as in the game), proud",
                    S.Animal("squirrel.red", 0f, 0f, 195f, "Happy", 0.3f),
                    Basket().At(0, AnimalSocket.HeadTop, 0f, 0.004f),
                    Hazelnut(0f, 0f).In(1, 0.6f),
                    Berry(0.4f, 0.1f).In(1, 0.6f),
                    Hazelnut(-0.4f, -0.1f).In(1, 0.6f)).InZone("orchard", 0f, 0.5f).View(0f, 10f),
                D("STK-ACT-003-08", PageNutDrop, StickerFraming.Detail, StickerLight.Day, "A falling golden nut",
                    Nut("ND_GoldenNut", NutDropPipeline.PaletteGolden, 0f, 0f, true).Tilted(15f, -20f)).View(0f, 10f),
                D("STK-ACT-003-09", PageNutDrop, StickerFraming.Detail, StickerLight.Day, "The woven basket, new, round handles", Basket()).View(0f, 22f),
                D("STK-ACT-003-12", PageNutDrop, StickerFraming.Landscape, StickerLight.Day, "Nut shower over the Orchard, a delighted Squirrel below",
                    S.Animal("squirrel.red", 0f, 0f, 190f, "Happy", 0.5f),
                    Hazelnut(0f, 0.6f).Air(1.7f).Cloud(90, new Vector3(2.4f, 1.1f, 1.4f)),
                    Nut("ND_Acorn", NutDropPipeline.PaletteAcorn, 0f, 0.6f).Air(1.7f).Cloud(40, new Vector3(2.4f, 1.1f, 1.4f)))
                    .InZone("orchard", 0f, 0.5f).Far(6f, 0.9f),

                // ---------------------------------------------------------------- S-ACT-005 Expeditions
                D("STK-ACT-005-03", PageExpeditions, StickerFraming.Scene, StickerLight.Morning, "Three animals in a file setting out at sunrise (Move)",
                    S.Animal("rabbit.brown", -0.6f, 0.1f, 120f, "Move", 0.2f),
                    S.Animal("squirrel.red", 0f, 0f, 120f, "Move", 0.5f),
                    S.Animal("hedgehog.brown", 0.6f, -0.1f, 120f, "Move", 0.8f)).InZone("start", 0f, 0.4f).View(0f, 10f),
                D("STK-ACT-005-04", PageExpeditions, StickerFraming.Landscape, StickerLight.Morning, "A little trail climbing toward the hill")
                    .InZone("hill", 0f, 1.5f).View(30f, 8f).Far(10f, 0.6f, true),
                D("STK-ACT-005-11", PageExpeditions, StickerFraming.Landscape, StickerLight.Evening, "A tiny team crossing the wide meadow in the evening",
                    S.Animal("rabbit.brown", -0.5f, 0f, 100f, "Move", 0.2f),
                    S.Animal("squirrel.red", 0f, 0f, 100f, "Move", 0.5f),
                    S.Animal("hedgehog.brown", 0.5f, 0f, 100f, "Move", 0.8f)).InZone("meadow", 0f, 0.5f).View(0f, 12f).Far(9f, 0.3f),

                // ---------------------------------------------------------------- S-ACT-006 Garden
                D("STK-ACT-006-02", PageGarden, StickerFraming.Detail, StickerLight.Day, "A sprout at the first stage (PF_GardenPlant_Sunflower_1)",
                    GardenPlant("Sunflower_1")).View(0f, 20f),
                D("STK-ACT-006-03", PageGarden, StickerFraming.Scene, StickerLight.Day, "The watering can tilted over a growing plot (no drops: none in the game)",
                    GardenPlant("Poppy_2").InPlot(1),
                    S.Mesh(GardenModel, GardenSetPipeline.Can, GardenSetPipeline.PropsMaterialPath, "palette.garden.watering_can", -0.42f, -0.36f, 60f)
                        .Air(0.58f).Near(0).Tilted(0f, 32f)).InGarden().View(0f, 28f),
                D("STK-ACT-006-04", PageGarden, StickerFraming.Detail, StickerLight.Day, "An open flower at the last stage (PF_GardenPlant_Daisy_3)",
                    GardenPlant("Daisy_3")).View(0f, 18f),
                D("STK-ACT-006-05", PageGarden, StickerFraming.Detail, StickerLight.Day, "A round smooth fruit ready to pick (PF_GardenPlant_RoundMelon_3)",
                    GardenPlant("RoundMelon_3")).View(0f, 24f),
                D("STK-ACT-006-06", PageGarden, StickerFraming.Landscape, StickerLight.Day, "A whole plot in full growth (wild strawberry, stage 3)",
                    GardenPlant("WildStrawberry_3").InPlot(2)).InGarden().View(0f, 38f, 0.55f),
                D("STK-ACT-006-12", PageGarden, StickerFraming.Landscape, StickerLight.Day,
                    "Every plot in bloom, the sunflower at the back, a Rabbit and a Squirrel gardening on the path",
                    GardenPlant("Poppy_3").InPlot(0),
                    GardenPlant("WildStrawberry_3").InPlot(1),
                    GardenPlant("DwarfApple_3").InPlot(2),
                    GardenPlant("Daisy_3").InPlot(3),
                    GardenPlant("Sunflower_3").InPlot(4),
                    S.Animal("rabbit.brown", -0.45f, -1.3f, 170f, "Eat", 0.3f),
                    S.Animal("squirrel.red", 0.45f, -1.3f, 190f, "Happy", 0.4f)).InGarden().View(0f, 40f),
            };
            return list;
        }
    }
}
