# T33B Prairie Magnifique (partie 2)

But (doc 04 D-BIO-001, doc 07) : finir la prairie. Cette partie ajoute :

- l'eau (ruisseau, cascade, mare) ;
- les monuments (vieil arbre à balançoire, moulin dont les ailes tournent) ;
- les derniers décors ;
- six nouvelles zones, soit huit zones en tout le long d'une bande de 112 m ;
- la pluie légère, l'arc-en-ciel, les rayons de soleil ;
- la nuit des événements ;
- le fondu des détails au sol avec la distance.

Captures : `SourceArt/Blender/Environment/Grassland/Captures/Unity/T33B_*.png`. Rapport brut du benchmark :
`Baselines/T33B_player_windows_strip.json`. Ce sont des mesures PC (Ryzen 7 5800X, RTX 5060 Ti, fenêtre 480 × 1040,
Direct3D 12), pas des budgets mobiles : aucun téléphone de test.

## Contenu

| Élément | Fichiers | Détail |
|---|---|---|
| Sols de la bande | `pw_grassland_t33b.py`, `GL_Grounds_T33B.fbx`, `GL_Ground_Core_A/B.fbx` | StreamBend, Orchard, Hill, WindmillRise, PondEdge, ForestEdge (3 648 à 6 912 triangles). Mêmes taches d'herbe en coordonnées monde et même chemin usé sur toute la bande : les jonctions ne se voient pas. |
| Eau | `GL_Water.fbx`, `SH_WaterLite.shader`, `MAT_Water_Grassland` | Ruisseau (1 212 triangles, cascade par-dessus le bord de l'île), mare (440). Profondeur, écume et chute peintes par sommet. Reflet du ciel, rides, scintillement. Trois niveaux de qualité : Fluide sans rides, Magnifique avec les reflets. |
| Monuments | `GL_Props_T33B.fbx` | Vieil arbre avec balançoire (LOD 1 682 / 760 / 240). Moulin (tour 740, ailes 230) : les ailes tournent dans le shader (drapeau `rotor`), sans script. |
| Décors | idem | Barrière blanche, petit puits, bords d'île A/B, pierres de gué au croisement du chemin et du ruisseau. |
| Météo | `WeatherSystem.cs`, `WeatherKit.cs`, `SH_RainLite`, `SH_Rainbow`, `SH_SunRays` | Averse légère tous les 18 à 35 min (70 à 110 s), arc-en-ciel 50 s après, de jour seulement. 900 gouttes au plus, animées dans le shader, dans une boîte qui suit la caméra. Rayons de soleil en Magnifique seulement. Gouttes par palier : 40 / 70 / 100 %. |
| Nuit (événements) | `LP_Grassland_Night`, `WorldAtmosphere.SetNight` | Bleu doux, étoiles, jamais noire. Réservée aux événements, l'heure locale ne la déclenche pas. |
| Fondu au loin | `ScatterRenderer.SetDistanceFade` | Les détails au sol disparaissent en fondu entre 16 et 26 m (Fluide), 22 et 34 m (Équilibrée), 30 et 46 m (Magnifique). |

## Benchmark (exécutable Windows de développement)

| Scénario | CPU principal (moy. / p95) | GPU | Monde | Détails / vie | Pics > 33 ms |
|---|---|---|---|---|---|
| P2 : 20 lapins, Équilibrée | 2,47 / 4,85 ms | 0,43 ms | 0,08 ms | 1 830 / 10 | 2 (bruit de démarrage, absent des autres runs) |
| P2-High : 20 animaux, Magnifique | 1,69 / 2,09 ms | 0,39 ms | 0,09 ms | 2 802 / 11 | 0 |
| S50-High : 50 lapins, Magnifique | 1,99 / 2,62 ms | 0,70 ms | 0,10 ms | 2 802 / 11 | 0 |
| W2 : traversée des 8 zones | 1,57 / 2,13 ms | 0,19 ms | 0,11 ms (max 1,51) | 2 590 / 22 | 1 (46 ms en chargeant une zone) |
| R1 : pluie, Équilibrée | 1,81 / 2,38 ms | 0,33 ms | 0,09 ms | 1 830 / 10 | 0 |
| R1-High : pluie, Magnifique | 1,72 / 2,10 ms | 0,39 ms | 0,09 ms | 2 802 / 11 | 0 |

Ce que montrent les chiffres :

- La pluie coûte environ 0,1 ms de CPU, et rien de mesurable côté GPU.
- La traversée des 8 zones reste sous 2,2 ms au p95.
- Un seul pic de 46 ms est apparu, pendant le chargement d'une zone (66 images avec une allocation, au plus 220 Ko). Il ne s'est pas reproduit dans les deux runs suivants (T33C) : 0 image au-dessus de 33 ms.

## Écarts par rapport au doc 04

- **Chemins.** Ils sont peints dans le sol, comme une usure qui court sur toute la bande, et non posés en bandes de
  maillage (`GL_Path_A/B`). C'est plus léger et la jonction entre zones est invisible. Les deux entrées restent
  « reportées ».
- **Troisième sol.** `GL_Ground_Core_C` n'existe pas en tant que tel : les six sols de la bande le remplacent.
- **Imposteurs d'arbres lointains.** Reportés : la bande n'a pas encore de vue lointaine qui en profite. Le fondu des
  détails et les LOD suffisent aux chiffres ci-dessus.
- **Vieil arbre.** Sa couronne est faite de 13 masses arrondies (le style des arbres du kit). Ce n'est pas un feuillage
  détaillé.
