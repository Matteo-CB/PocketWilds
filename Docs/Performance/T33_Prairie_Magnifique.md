# T33 Prairie Magnifique (partie 1)

But (doc 07 « Beauté maximale sans lag », doc 04 D-BIO-001) : une prairie magnifique et très détaillée, sans lag,
adaptée aux trois paliers. Cette partie livre les systèmes (dispersion instanciée, vent, vie ambiante, ciel,
ambiances) et le kit de végétation et de décors. L'eau, les monuments, les chemins et bords, la météo, la nuit et les
6 autres chunks forment la partie 2 (tâche T33B, ajoutée juste après dans la file).

Captures : `SourceArt/Blender/Environment/Grassland/Captures/Unity/T33_*.png`. Rapport brut du benchmark :
`Baselines/T33_player_windows_prairie.json`. Ce sont des mesures PC, pas des budgets mobiles : aucun téléphone de test.

## Systèmes

| Système | Fichiers | Ce qu'il fait |
|---|---|---|
| ScatterRenderer | `Presentation/World/ScatterRenderer.cs`, `ScatterKit.cs`, `ScatterData.cs` | Détails au sol sans GameObject : une liste compacte par chunk (position, lacet, échelle, sorte), dessinée par `Graphics.RenderMeshInstanced`, un lot par sorte et par chunk, chunks hors champ ignorés. Le palier garde un préfixe stable de chaque sorte (rang par hachage) : 30 / 65 / 100 %. Fleurs, cailloux et champignons gardent au moins 55, 30 et 50 % (le charme ne part jamais en Fluide). Aucune allocation par frame, aucune ombre portée. |
| FoliageWind (G-SH-009) | `Art/Shaders/SH_FoliageWind.shader`, `Include/PW_World.hlsl` | Vent global (direction, rafales qui traversent la prairie, frémissement), pondéré par la hauteur peinte dans Blender. Teinte et luminosité varient par instance (hachage de position) : deux brins ne sont jamais identiques. Même éclairage stylisé que le décor. |
| AmbientLife (G-SH-011) | `Presentation/World/AmbientLife.cs`, `AmbientLifeKit.cs`, `Art/Shaders/SH_AmbientLife.shader` | Papillons (jaune et crème, bleu et lilas, doré rare sur un tiers des points), abeilles, rouges-gorges. Un seul système, sans script par créature : trajectoires calculées en quelques lignes, battements d'ailes dans le shader (charnière à l'épaule, ailes repliées au sol pour les oiseaux). Un toucher fait fuir les insectes et s'envoler les oiseaux, qui reviennent se poser plus loin. Densité 25 / 60 / 100 % (au moins un individu par point). |
| Ciel et nuages (G-SH-007) | `SH_SkyGradient.shader` | Dégradé, lueur du soleil et nuages peints (bruit procédural, bords nets, cœur et côté à l'ombre teintés). Pas de nuages en Fluide. |
| Brouillard de hauteur | `PW_World.hlsl` (`PW_FogFactor`) | Une brume douce dans les creux lointains, pliée dans le brouillard linéaire (aucun coût de passe). Absente en Fluide. |
| Ambiances | `WorldAtmosphere.cs`, `LP_Grassland_Morning/Day/Evening` | Matin (soleil doré bas, horizon pêche, brume), jour, soir (lumière chaude rasante, nuages rosés). Le mélange suit l'heure locale de l'appareil, adoucie : pas de nuit noire, les heures tardives restent sur le soir. Vérifié chaque seconde, réécrit seulement quand l'heure a bougé d'une minute. |
| Profils de qualité | `WorldQualityProfile.cs`, `Content/Quality/DEF_WorldQuality.asset` | Valeurs du tableau du doc 07 : détails 30 / 65 / 100 %, vie 25 / 60 / 100 %, nuages et brouillard de hauteur à partir d'Équilibrée, météo 40 / 70 / 100 % (pour T33B). |

## Kit Prairie

Source : `SourceArt/Blender/Environment/Grassland/BL_Biome_Grassland_Magnifique.blend`, construit par
`SourceArt/Blender/Tools/pw_grassland_t33.py` (régions de palette et occlusion peintes par sommet). Export :
`GL_Scatter.fbx`, `GL_Props_T33.fbx`, `GL_Life.fbx`. Unity : `GrasslandPipeline.BuildMagnifique()`.

| Famille | Contenu | Cible doc 04 |
|---|---|---|
| Herbes | touffes S/M/L + épis S/M/L (40 à 56 triangles) | 3 tailles x 2 formes : OK |
| Fleurs | marguerite blanche et jaune, campanule bleue et lilas, trèfle rose, coquelicot, lavande, bouton-d'or (6 maillages, 8 sortes) | 6 à 8 : OK |
| Détails dispersés | cailloux A/B/C, feuille, pétales, trèfle, mini-champignon | 6 à 10 : OK (7) |
| Arbres | A (rond), B (couronne asymétrique), C (bouleau), D (fruitier aux petites pommes), LOD0/1/2 | 4 : OK. Arbres monuments et imposteurs : T33B |
| Buissons | A, B (en fleurs), C | 3 à 4 : OK |
| Rochers | S/M/L + héros A/B | OK |
| Décors signature | tronc couché, souche, barrière A, botte de foin, bain d'oiseaux | 4 à 6 : OK. Barrière B, petit puits : T33B |
| Vie ambiante | papillons, abeilles, oiseaux | 3 : OK |
| Ambiances | matin, jour, soir | 3 : OK. Nuit pour les événements : T33B |

Les deux chunks sont recomposés : le décor de cadrage reste en renderers simples (46 et 38 renderers, 2 matériaux),
tout le reste passe par la dispersion (2 847 et 2 941 instances). Des massifs d'une seule espèce encadrent la zone de
jeu, les épis et grandes touffes s'appuient contre les rochers et les arbres, cailloux au pied des rochers, feuilles et
champignons sous les arbres, gazon court là où jouent les animaux. La dispersion rejoint la couture des deux chunks.

## Revue de beauté côte à côte

Captures : `T33_world_Low/Medium/High.png` (même cadre, 6 lapins), planche `T33_tiers_sheet.png`. Mesures
`T33_beauty_metrics.json` (script `Tools/Capture/beauty_review.py`) par rapport au Magnifique :

| | Similarité de structure | Écart de couleur (dE Lab) | Énergie de détail |
|---|---|---|---|
| Équilibrée | 0,86 | 2,2 (invisible) | 74 % |
| Fluide | 0,76 | 7,5 (pas d'ombres temps réel) | 43 % |

| Critère (doc 07, section 7) | Équilibrée par rapport au Magnifique |
|---|---|
| Silhouette | identique : mêmes arbres, buissons, rochers et décors |
| Composition | identique : les massifs qui encadrent la zone de jeu sont tous là (minimum de fleurs) |
| Couleur | identique (dE 2,2) |
| Lumière | très proche : 1 cascade d'ombre au lieu de 2, les ombres lointaines s'adoucissent |
| Vie | 10 individus visibles au lieu de 11 (au moins un par point) |
| Détail | 65 % des brins, l'œil le voit surtout au premier plan ; les fleurs restent (74 % de l'énergie de détail) |

Verdict : l'Équilibrée garde environ 90 % du ressenti du Magnifique, au-dessus du seuil de 85 %. Le Fluide garde
l'identité : silhouettes, palette, lumière, massifs de fleurs, vie, animaux nets. Il retire des brins et les ombres
du décor. Les trois ambiances sont dans `T33_moods_sheet.png`, la faune dans `T33_life_*.png`, le ciel dans
`T33_sky_clouds.png`.

## Mesures (player Windows de développement, 480x1040, Ryzen 7 5800X + RTX 5060 Ti)

| Scénario | Palier | Thread principal moy / max | GPU moy | Monde moy / max | Lots | Triangles | Détails / vie dessinés | Mémoire |
|---|---|---|---|---|---|---|---|---|
| P0 vide | Équilibrée | 1,29 / 2,57 ms | 0,27 ms | 0,08 / 0,48 ms | 70 | 105 k | 1 862 / 10 | 236 Mo |
| P2 20 animaux | Équilibrée | 1,58 / 2,87 ms | 0,68 ms | 0,08 / 0,31 ms | 109 | 181 k | 1 862 / 10 | 238 Mo |
| S50 | Équilibrée | 1,78 / 2,82 ms | 1,28 ms | 0,08 / 0,18 ms | 158 | 284 k | 1 862 / 10 | 239 Mo |
| P2-Low | Fluide | 1,43 / 2,32 ms | 0,42 ms | 0,07 / 0,24 ms | 83 | 68 k | 934 / 6 | 226 Mo |
| P2-High | Magnifique | 1,90 / 3,48 ms | 0,83 ms | 0,11 / 0,51 ms | 112 | 237 k | 2 847 / 11 | 254 Mo |
| S50-High | Magnifique | 1,81 / 2,81 ms | 0,78 ms | 0,09 / 0,16 ms | 161 | 334 k | 2 847 / 11 | 253 Mo |
| W1 streaming | Équilibrée | 1,55 / 2,99 ms | 0,52 ms | 0,08 / 0,31 ms | 94 | 151 k | 2 110 / 13 | 241 Mo |

« Monde » = dispersion + vie ambiante + atmosphère (CPU). Les 16 scénarios tiennent la synchronisation verticale
(p99 sous 17,1 ms), aucune frame au-dessus de 33 ms. Allocations : identiques aux références T30 et T32 (2 frames
par scénario hors burst, déjà présentes avant T33) ; les tests EditMode prouvent 0 allocation pour le rendu de la
dispersion, la vie ambiante et l'atmosphère.

Comparaison avec les cibles du doc 07 (sur PC) :

| | Fluide | Équilibrée | Magnifique |
|---|---|---|---|
| Lots de rendu (cible) | 83 (90) | 109 à 158 (150 : dépassé de 8 à 50 animaux) | 112 à 161 (250) |
| Triangles (cible) | 68 k (200 k) | 181 k à 284 k (400 k) | 237 k à 334 k (700 k) |
| Mémoire en jeu (cible) | 226 Mo (350) | 238 Mo (450) | 254 Mo (600) |
| Dispersion CPU (cible 0,3 ms) | 0,07 ms | 0,08 ms | 0,11 ms |

Les 158 lots de S50 en Équilibrée viennent des 50 animaux (+78 lots par rapport à P0), pas du décor ; le doc 07 prévoit
30 animaux interactifs en Équilibrée (129 lots en S30). Bundles Addressables : kit 178 Ko, chunk de départ 90 Ko,
prairie 88 Ko (les maillages d'herbe fusionnés de T22 ont disparu au profit des données de dispersion).

Un premier passage a montré des pics de 40 à 85 ms dans des scénarios différents à chaque fois, jamais liés au
palier ni au coût du monde (0,9 ms au pire). Ils ne se sont pas reproduits avec le profileur, puis plus du tout sur
le passage final. Entre les deux, l'atmosphère a cessé de réécrire les réglages de rendu chaque seconde ; rien ne
prouve que c'était la cause (activité de la machine possible : certains scénarios du premier passage ont perdu des
frames de façon irrégulière).

## Écarts assumés et suite

- Culling par chunk (14 m) plutôt que par cellule : le cadre de jeu voit l'essentiel du chunk de départ ; à revoir
  avec le LOD par distance quand les chunks seront plus nombreux (T33B).
- Nuages peints dans le shader du ciel plutôt qu'en maillages : même rendu pour moins de surdessin. Rayons de soleil du
  Magnifique : T33B.
- Vie ambiante mise à jour par individu (au plus 40) plutôt que par paramètres de groupe : son coût est compris dans
  les 0,08 ms du monde, et les comportements sont plus vivants.
- Fluide garde 33 % des détails au lieu de 30 % : minimum de fleurs, cailloux et champignons (charme).
- T33B : ruisseau WaterLite et pierres, monuments Vieil arbre et Moulin, imposteurs, barrière B, petit puits, chemins et
  bords, sol C, 6 chunks (StreamBend, Orchard, Hill, WindmillRise, PondEdge, ForestEdge), météo (pluie fine,
  arc-en-ciel), ambiance de nuit.
- Mesures téléphone : aucune, faute d'appareil (statuts de performance laissés en revue).
