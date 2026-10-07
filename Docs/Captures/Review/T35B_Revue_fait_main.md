# T35B : revue « fait main » des nouveaux assets

8 octobre 2026. Grille de la charte « fait main » (dossier 05 du Drive) appliquée aux assets ajoutés par T35B. Captures
dans ce dossier (préfixe `T35B_`), rendues dans Unity avec les vrais shaders, la lumière et les polices du jeu.

## Assets

| Asset | Source | Master | Usage |
| --- | --- | --- | --- |
| `UI_Egg_Shell` (96 x 112) | `SourceArt/UI/Tools/pw_ui_sprites_t35b.py` | `SourceArt/UI/Masters/UI_Egg_Shell@4x.png` | coquille des pastilles et de l'œuf du bouton, teintée par œuf |
| `UI_Egg_Spots` (96 x 112) | idem | `SourceArt/UI/Masters/UI_Egg_Spots@4x.png` | taches, même cadre que la coquille, teintées par œuf |
| `PF_UI_RewardSummary` | `Assets/_Game/Editor/UI/EggUiPipeline.cs` | prefab construit par le pipeline | carte de résumé (écran 028) |
| HUD : pastilles, nom, × 1 / × 3 / × 10, cibles du Focus, « Gratuit » | idem | parties de `PF_UI_HUD_Main` | choix de l'œuf |

Aucun générateur, aucune bibliothèque : les deux sprites sont des distances signées dessinées en 4x puis réduites (même
méthode que tout le kit), les écrans sont construits par code à partir du kit existant (pilule, cercle, anneau doux,
point, étoile à cinq branches, cadres et badges de rareté T34A) et des polices maison.

## Grille

| Point de la charte | Résultat |
| --- | --- |
| Source éditable et master `@4x` | oui pour les deux sprites ; `HandmadeRuleTests` passe |
| Formes rondes, contour brun chaud du HUD | coquille à contour brun `#92684A`, taches rondes de trois tailles ; les coquilles sont pâles, le contour reste brun après teinte |
| Lisible à 24 et 48 dp | pastille de 96 px (≈ 37 dp) : coquille et taches se distinguent sur les quatre teintes d'œuf (aperçu teinté vérifié avant intégration) |
| Pas d'étincelle à quatre branches | aucune : taches rondes, étoiles ★ à cinq branches (glyphe des polices maison) |
| Rien de religieux | aucun motif ; textes « Bienvenue au refuge », « Nouveau », « Continuer », « Gratuit » |
| Rareté jamais par la couleur seule | carte de résumé : cadre à motif propre à chaque rareté, badge, étoiles et mot ; fenêtre des chances : étoiles et mot |
| Zones tactiles 48 dp | pastilles et sélecteur dans des zones de 124 px (`FeedbackScreenTests.EveryButton_IsAtLeast48dp`) |
| Pas de recalcul de mise en page sur le HUD | pastilles placées par code, aucun LayoutGroup (`HudTests`) |
| Animations réduites | cellules du résumé en fondu, pas de pop d'œuf ni d'éclats (`MultiHatchPlayerTests`) |

## Captures

- `T35B_choice_basic_*`, `T35B_choice_focus_*`, `T35B_choice_discovery_*` : le choix de l'œuf (français et anglais).
- `T35B_odds_focus_*` : la fenêtre des chances du Focus.
- `T35B_summary10_*`, `T35B_summary3_*` : la carte de résumé d'un × 10 et d'un × 3.
