namespace PocketWilds.Presentation
{
    /// <summary>
    /// T39 « Cache-cache » stage numbers (doc 01 Contenu S-ACT-002; doc « Mini-jeux &amp; activités » S-ACT-002, sections 1
    /// and 6). The game plays in the refuge: the camera glides to one Prairie zone, the player's animals hide in its
    /// measured spots (DEF_HideSpots_Grassland), the refuge stays drawn. Every time below is in seconds, every length in
    /// meters; the T39 defaults where the docs are silent are marked so.
    /// </summary>
    public static class HideSeekLayout
    {
        /// <summary>Addressables group and address of the spots of the Prairie chunks (loaded at the first game).</summary>
        public const string Group = "Activity_HideAndSeek";

        public const string SpotsAddress = "activity.hide_seek.spots.grassland";

        /// <summary>
        /// Shared assets borrowed by address from the groups that own them (never copied into this one): the feedback VFX
        /// material (CommonAnimals: the discovery ring, the magnifier's glass ring) and the Prairie kit's leaf and material
        /// (Biome_Grassland_Kit: the leaves of an empty touch, H-VFX-ACT-002-02).
        /// </summary>
        public const string VfxMaterialAddress = "vfx.additive_lite";

        public const string LeafMeshAddress = "kit.grassland.gl_leaf_a";
        public const string LeafMaterialAddress = "kit.grassland.mat_environment_common";
        public const string LeafPalette = "palette.grassland.leaf";

        /// <summary>The feedback atlas' soft ring tile (VfxPipeline.TileRing of 8): H-VFX-ACT-002-01's ring.</summary>
        public const int RingTile = 4;

        public const int AtlasTiles = 8;

        /// <summary>Most hidden animals of a game (doc 01: 8 at d10, challenges 5 and 10) and spots of a zone (the contract's 32).</summary>
        public const int MaxHidden = 8;

        public const int MaxSpots = 32;

        /// <summary>Pooled effects: rings (discovery, magnifier, the paused touch's cue) and flying leaves (3 per empty touch).</summary>
        public const int RingCount = 4;

        public const int LeafCount = 9;

        public const int LeavesPerTouch = 3;

        /// <summary>The camera's glide to the zone and back (T39 default, about the refuge's own transitions).</summary>
        public const float CameraGlideSeconds = 0.6f;

        /// <summary>A zone's chunk that is still not drawn after this long never blocks the game (it starts anyway).</summary>
        public const float ChunkWaitSeconds = 4f;

        /// <summary>The narrowest portrait the zones' field of view is measured for (9:21).</summary>
        public const float NarrowestAspect = 9f / 21f;

        /// <summary>Screen shares the HUD keeps (the score and time at the top, the items at the bottom): spots stay between.</summary>
        public const float HudTopShare = 0.15f;

        public const float HudBottomShare = 0.12f;

        public const float SideShare = 0.04f;

        /// <summary>« Coucou »: the hop out of the spot to its landing point (brief: 0.45 s), its height at scale 1.</summary>
        public const float HopSeconds = 0.45f;

        public const float HopHeight = 0.16f;

        /// <summary>
        /// No free landing within this many rings of 8 cm around the measured one: the animal pops up where it hides instead
        /// (collision rule: never into anything).
        /// </summary>
        public const int LandingRings = 6;

        /// <summary>Kept between a body and anything else on its way or where it lands (collision rule: contact, never inside).</summary>
        public const float BodyGap = 0.02f;

        /// <summary>Challenge 9: an animal slips to its new spot unseen (down into the grass, up behind the new decor).</summary>
        public const float MovedSeconds = 0.4f;

        /// <summary>All found: the found animals gather in front of the camera (T39 default), shortly after the last hop.</summary>
        public const float GatherDelay = 0.35f;

        public const float GatherSeconds = 0.5f;

        /// <summary>H-VFX-ACT-002-03: a hint's ripple, repeated gently while the animal still waits.</summary>
        public const float HintRippleSeconds = 1.2f;

        public const float HintRepeatSeconds = 3f;

        /// <summary>A decoy's rustle and the whistle's single ripple per animal.</summary>
        public const float DecoyRippleSeconds = 1.2f;

        public const float WhistleRippleSeconds = 0.9f;

        /// <summary>The whistle's wiggles one animal after the other (T39 default).</summary>
        public const float WhistleStagger = 0.12f;

        /// <summary>H-VFX-ACT-002-01: the discovery ring grows and fades under the landing animal.</summary>
        public const float RingSeconds = 0.7f;

        /// <summary>H-VFX-ACT-002-02: three leaves fly off the decor and fade before they land.</summary>
        public const float LeafSeconds = 0.85f;

        public const float LeafSize = 0.075f;

        /// <summary>Doc Mini-jeux section 1 (understood without text in 3 s): the paw comes this soon in the first game.</summary>
        public const float TutorialDelay = 2f;

        public const float TipSeconds = 2.6f;

        /// <summary>A touch shorter and stiller than this is a tap.</summary>
        public const float TapPixels = 22f;

        public const float TapSeconds = 0.4f;

        /// <summary>The smallest tap target around a decor (share of the screen's height): always a fair finger.</summary>
        public const float MinTargetShare = 0.045f;

        /// <summary>A finger's margin around a hidden animal's own body (share of the screen's height): its ear tip counts.</summary>
        public const float AnimalTapMarginShare = 0.012f;

        /// <summary>
        /// A second tap this soon after a find and this close to it (share of the screen's height) is the same happy double
        /// tap: ignored, never an empty touch on a neighbour's decor (T39 default; challenge 6 counts empty touches).
        /// </summary>
        public const float FoundTapGuardSeconds = 0.5f;

        public const float FoundTapGuardShare = 0.03f;

        /// <summary>A hint's soft « React », its crossfade (played softly in place).</summary>
        public const float SoftReactFade = 0.22f;

        /// <summary>
        /// A hidden animal nibbles the grass, head down (the Eat loop: the pose the spots are measured in); after a peek (the
        /// hint, the whistle's wiggle) it goes back to nibbling after this long.
        /// </summary>
        public const float BackToNibbleSeconds = 0.9f;

        public const float NibbleFade = 0.25f;
    }
}
