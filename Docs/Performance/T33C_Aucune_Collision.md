# T33C Règle « aucune collision »

Décision du propriétaire du 6 octobre 2026 : rien n'entre jamais dans rien. Cela concerne :

- les animaux ;
- les oiseaux et les insectes ;
- les décors ;
- l'herbe et les fleurs ;
- l'œuf et le nouveau-né.

Le résultat doit être réaliste, optimisé et performant. La règle complète (formes, comportements, contenu futur) est
dans `Docs/Design/Collision_Rule.md`.

Captures : `SourceArt/Blender/Environment/Grassland/Captures/Unity/T33C_refuge.png` et `T33C_rock_close.png` (20
animaux après 60 s de vie). Rapport brut du benchmark : `Baselines/T33C_player_windows_collision.json`. Ce sont des
mesures PC, pas des budgets mobiles : aucun téléphone de test.

## Ce qui a changé

| Partie | Fichiers | Détail |
|---|---|---|
| Formes | `Presentation/World/WorldCollision.cs` | `Obstacle` (gélule vue de dessus et bande de hauteur ; solide, eau ou pente raide), `BodyShape` (gélule de l'animal), `GroundHeightField` et `GroundSurface` (relief), `CollisionMath`. |
| Mesure des décors | `Editor/Environment/CollisionProxies.cs` | Surfaces LOD0 échantillonnées tous les 4 cm, cinq bandes de hauteur, contour convexe et gélule englobante par bande. Les pièces qui tournent sont balayées sur un tour complet. |
| Zones | `GrasslandPipeline.T33C.cs` | Les décors qui se chevauchent sont écartés, jonctions comprises. Chaque zone reçoit ses obstacles (décors, eau, pentes de plus de 32°) et une carte du relief à 0,25 m. Les points d'apparition sont dégagés. |
| Dispersion | `GrasslandPipeline.T33.cs` | Rien ne pousse dans le contour d'un décor : la plante entière reste dehors, son rayon compris. Fleurs, cailloux et champignons gardent leurs distances entre eux. |
| Animaux | `AnimalScheduler.cs`, `BehaviorProfile.cs`, `AnimalPipeline.cs` | Chaque animal a une silhouette mesurée sur son modèle : lapin 54 × 33 cm, écureuil 64 × 30 cm, hérisson 53 × 33 cm. Le corps est testé avant chaque pas, chaque demi-tour et chaque apparition. L'animal contourne un obstacle par un point de passage, fait un pas de côté devant un autre animal, glisse le long d'un bord, et abandonne un trajet bloqué. Il se tient sur le relief, incliné avec la pente. |
| Éclosion | `RefugeController.cs`, `AnimalView.cs` | Le nouveau-né sort de l'œuf d'un bond (2,5 fois plus vite, animation accélérée d'autant). L'œuf suivant attend que sa place soit libre, environ 1 s. L'offre « moment » se compte à partir du retour de l'œuf, comme avant. |
| Vie ambiante | `AmbientLife.cs` | Les insectes contournent les volumes et les animaux et ne se traversent pas. Les oiseaux ne se posent que sur un sol libre, s'envolent quand un animal approche, et leurs vols évitent les couronnes d'arbres et le moulin. |
| Moulin | `pw_grassland_t33b_props.py`, `GL_Props_T33B.fbx` | Les ailes traversaient la galerie en tournant. Le moyeu est maintenant à 1,40 m de l'axe de la tour (contre 0,98 m), relié au toit par un arbre en bois : les ailes passent devant la galerie (1,28 m). |

## Tests

Le fichier `CollisionRuleTests` compte 10 tests, tous verts :

| Test | Ce qu'il vérifie |
|---|---|
| Décors | Aucun décor n'entre dans un autre sur les 8 zones, jonctions comprises. |
| Dispersion | Aucune plante ni aucun détail dans un décor ; fleurs, cailloux et champignons ne se chevauchent pas. |
| Zones | Chaque zone a ses obstacles et son relief ; points d'apparition et place de l'œuf dégagés. |
| Corps | Silhouettes mesurées ; aux salutations, les museaux se touchent sans se chevaucher. |
| Pièces tournantes | Les ailes du moulin ne balaient jamais la tour ni la galerie. |
| Refuge | 30 animaux de 3 espèces pendant 2 min : aucun contact, toujours posés sur le relief, salutations et visites toujours là. |
| Rocher | Un trajet vers un point placé derrière un rocher le contourne. |
| Éclosion | Le nouveau-né libère la place de l'œuf en moins de 1,2 s, puis quitte toute la zone de l'œuf. |
| Coût | Aucune allocation, et une borne de temps dans l'éditeur avec 50 animaux. |
| Vie ambiante | 90 s : insectes et oiseaux n'entrent dans aucun volume, aucun animal, aucun de leurs semblables. |

Suites complètes : EditMode 360/360, PlayMode 35 tests passés et 1 ignoré volontairement (le benchmark, lancé à la
demande).

## Benchmark (exécutable Windows de développement)

Ryzen 7 5800X, RTX 5060 Ti, fenêtre 480 × 1040, Direct3D 12. Le contrôle des corps tourne dans le jeu compilé, une
image sur 60.

| Scénario | Thread principal, moyenne / p95 (T33B puis T33C) | Animaux (sim) | Monde | Écart minimal animaux / vie ambiante | Chevauchements |
|---|---|---|---|---|---|
| P2 : 20 lapins, Équilibrée | 2,47 / 4,85 → 1,76 / 2,18 ms | 0,01 → 0,04 ms | 0,08 → 0,11 ms | contact (−2 mm) / 0,5 mm | 0 |
| S50 : 50 lapins | 1,93 / 2,56 → 2,04 / 2,49 ms | 0,02 → 0,08 ms | 0,08 → 0,14 ms | contact / contact | 0 |
| S50-High : 50 lapins, Magnifique | 1,99 / 2,62 → 2,01 / 2,43 ms | 0,02 → 0,07 ms | 0,10 → 0,14 ms | contact / contact | 0 |
| S50-Mix3 : 50 animaux, 3 espèces | 1,90 / 2,38 → 2,23 / 2,74 ms | 0,02 → 0,06 ms | 0,08 → 0,14 ms | contact / contact | 0 |
| P5 : 10 éclosions à la suite | 1,79 / 2,21 → 1,92 / 2,52 ms | 0,02 → 0,06 ms | 0,08 → 0,11 ms | contact / contact | 0 |
| W2 : traversée des 8 zones | 1,57 / 2,13 → 1,69 / 2,21 ms | 0,01 → 0,04 ms | 0,11 → 0,15 ms | contact / contact | 0 |
| R1-High : pluie, Magnifique | 1,72 / 2,10 → 1,85 / 2,44 ms | 0,01 → 0,04 ms | 0,09 → 0,12 ms | contact / contact | 0 |

« Contact » signifie que deux corps se touchent, à 2 mm au plus (la tolérance d'arrondi). Ils ne se chevauchent jamais.

- **Chevauchements.** 0 relevé dans les 19 scénarios, sur les trois paliers, de 1 à 50 animaux, pendant les
  éclosions, la pluie, le streaming et la traversée de la bande.
- **Pics.** Aucune image au-dessus de 33 ms dans ce run (T33B en avait 4).
- **Coût de la règle.**
  - Animaux : +0,05 ms à 50 animaux (de 0,02 à 0,07 ms).
  - Insectes et oiseaux, qui tiennent compte des animaux : +0,04 à +0,06 ms dans « Monde ».
  - Aucune allocation, et le GPU est inchangé.
  - Les écarts de thread principal entre deux runs (±0,3 ms) sont du même ordre que le bruit habituel de la machine.
- **Mesure.** Le contrôle des corps tourne une image sur 60 et ignore ce qui est loin. Au premier passage, il tournait
  une image sur 15 sans tri préalable, et son propre coût montait le p95 de 50 animaux à 3,6 ms.
- **Dispersion.** −6 % d'instances (de 1 830 à 1 720 en Équilibrée), car plus rien ne pousse dans un décor. Le
  rendu n'en est pas changé à l'œil.
- **Rythme des éclosions.** L'œuf revient environ 1 s après l'éclosion, contre 0,45 s avant : le nouveau-né bondit
  d'abord hors de la place de l'œuf. Dans le scénario P5, 9 éclosions tiennent en 14 s, contre 10 avant.
