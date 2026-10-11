# T49 art review: shop icons of L'Éveil des Terriers (33 icons)

Rendered in Unity from the real assets (no drawing, no generator), same light and framing rules as the animal thumbnails
(Prairie day light, three-quarter front view from slightly above, cropped on the alpha), written to `Assets/_Game/Art/UI/Shop/`:
- `ICO_Accessory_<Piece>` (8): each piece alone in the palette the shop shows first (Golden Miner Helmet and Golden Shovel in gold),
  `AccessoryVisualPipeline.RenderIcons`.
- `ICO_Outfit_Burrows_01..07`: the outfit worn on the mole (else the field hamster) with its measured fits, same pipeline, run after
  `AccessoryFitPipeline.BuildAll`.
- `ICO_Decor_Burrows_<Name>` (13), `<Name>Gold` (4, gold row on the same model), `ICO_Decor_Burrows_RootBurrow`:
  `BurrowsDecorPipeline.RenderIcons`.
Sources: `SourceArt/Handmade_Sources.json` (generated entries), models reviewed in the T49 grids of each piece.

| Check | Result |
|---|---|
| Rendered from the shipped prefab and palette row, nothing painted over | yes (pipeline) |
| Icon keys equal the event catalog's (`BurrowsDecorTests.IconKeys_MatchTheEventCatalog`) | yes (test) |
| Outfit icons show the pieces resting on the animal, nothing entering it (2 mm rule, `AccessoryFitTests`) | yes (test) |
| Reads at shop size on Basse / Équilibrée / Magnifique, soft rounded, warm, never AI-looking | to check on the rendered sheet |
| No four-point star, nothing religious | yes |
