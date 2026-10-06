# Règle « aucune collision » : rien n'entre jamais dans rien

Décision du propriétaire, 6 octobre 2026 : les objets et les animaux ne se rentrent jamais dedans. Un animal ne
traverse pas un rocher, un autre animal ou l'eau, et un décor n'entre pas dans un autre décor. Le résultat doit être
réaliste, optimisé et performant.

La règle couvre tout ce qui bouge ou se pose dans le monde :

- les animaux du refuge ;
- les papillons, les abeilles et les oiseaux ;
- les décors : arbres, buissons, rochers, barrières, monuments, puits, souches ;
- le détail du sol : herbe, fleurs, cailloux, champignons ;
- l'œuf et le nouveau-né ;
- les points d'apparition, ainsi que tout ce qui viendra plus tard (décorations posées par le joueur, nouveaux biomes,
  nouvelles espèces).

Pas de physique ni de NavMesh : des formes simples mesurées sur les modèles et un contrôle avant chaque pas.

Contrôle automatique : `Assets/_Game/Tests/EditMode/CollisionRuleTests.cs` doit passer avant chaque commit. Le
validateur d'assets (`AssetValidator`) signale aussi les chevauchements de décors et les points d'apparition mal placés.
Le benchmark Windows relève l'écart minimal dans le jeu compilé : `overlapSamples` doit rester à 0.

## Formes de collision

| Quoi | Forme | D'où elle vient |
|---|---|---|
| Animal | Gélule orientée vue de dessus (museau, queue, flancs) et une hauteur | Mesurée sur le modèle LOD0 (`AnimalPipeline.MeasureBody`, menu *Measure Bodies*), à l'échelle de l'individu |
| Décor | Par tranche de hauteur (0–0,7 m, 0,7–1,5, 1,5–3, 3–6, 6–12) : contour convexe exact et gélule englobante | Échantillonnage des surfaces LOD0 tous les 4 cm (`CollisionProxies`) ; les ailes du moulin sont balayées sur un tour complet |
| Eau | Gélules couvrant la surface, rangée par rangée | Maillage de l'eau (`WaterObstacles`) |
| Pente trop raide (> 32°) | Gélules, rangée par rangée | Carte du relief (`SteepObstacles`) |
| Œuf | Disque réservé de 0,55 m ; l'œuf lui-même fait 0,36 m | `RefugeController.EggSpotRadius`, `EggBodyRadius` |
| Papillon, abeille, oiseau | Sphère de rayon égal à l'envergure | Maillage de l'espèce × son échelle |

Chaque zone (`WorldChunk`) stocke ses obstacles et une carte du relief (grille de 0,25 m). Les animaux et les oiseaux
posés au sol se tiennent sur ce relief, inclinés avec la pente (14° au plus). Sur le terrain praticable, l'erreur de
hauteur reste sous 2 cm (sous 5 mm dans le refuge).

## Comportements

- **Avant chaque pas, chaque demi-tour et chaque apparition**, le corps de l'animal est testé contre :
  - les obstacles qu'il peut heurter à sa hauteur (les couronnes d'arbres et les ailes du moulin passent au-dessus) ;
  - l'eau et les pentes raides ;
  - l'emplacement de l'œuf ;
  - tous les autres animaux.

  Un pas qui ferait entrer quoi que ce soit n'est jamais pris. Le contact est permis, l'entrée ne l'est pas
  (tolérance de 2 mm pour les arrondis de calcul).
- **Trajets.** Une destination n'est retenue que si le corps y tient et que le chemin est libre. Sinon l'animal
  contourne l'obstacle par un point de passage, en essayant les deux côtés, à trois largeurs.
- **Entre animaux.** Un animal croisé sur le chemin est évité par un pas de côté, toujours du même côté pour un
  trajet donné. Bloqué, l'animal glisse le long de l'obstacle (±35°, ±70°). Toujours bloqué après 0,8 s, il s'arrête
  sur place (ou fait son activité s'il est à moins de 30 cm du but).
- **Salutations.** Les museaux se touchent sans se chevaucher : l'écart vaut les deux avants du corps + 1 cm.
- **Points d'intérêt.** L'animal s'arrête au bord du rocher ou de l'arbre, museau vers lui, jamais dedans.
- **Éclosion.** Le nouveau-né apparaît dans l'œuf, puis en sort d'un bond vers l'avant, 2,5 fois plus vite qu'une
  marche, son animation accélérée d'autant. L'œuf suivant n'apparaît que lorsque la place de l'œuf est libre
  (environ 1 s).
- **Décors.** À la construction d'une zone, les décors qui se chevauchent sont écartés au plus court, y compris à la
  jonction des zones voisines. Le moins important bouge, dans cet ordre de priorité : monument, bordure, arbre,
  buisson, reste. Un écart de plus de 0,9 m est une erreur de mise en page à corriger à la main.
- **Herbe et fleurs.** Rien ne pousse dans le contour d'un décor : la plante entière, son rayon compris, reste
  dehors. Fleurs, cailloux et champignons ne se chevauchent pas entre eux. L'herbe peut se mêler à l'herbe.
- **Insectes.** Ils choisissent des points d'air libres, glissent le long des feuillages au lieu de les traverser,
  passent par-dessus quand ils sont coincés, s'écartent les uns des autres et contournent les animaux.
- **Oiseaux.** Ils ne sautillent et ne se posent que sur un sol libre : pas de décor, pas d'eau, pas d'animal, pas
  d'autre oiseau. Ils s'envolent quand un animal approche. Leur trajectoire de vol est choisie pour ne traverser
  aucune couronne d'arbre ni le moulin.

## Performance

Les mesures sont faites dans l'éditeur, sur PC.

- 50 animaux et 47 obstacles : 0,04 ms par image dans l'éditeur, sans allocation.
- Les obstacles sont copiés une fois à l'entrée dans une zone. Chaque test commence par un rejet rapide sur un
  disque englobant.
- Les insectes et les oiseaux ne testent que les obstacles proches de leur ancrage (liste construite à l'ajout de la
  zone).

## Pour tout nouveau contenu

- **Nouvelle espèce.** La mesure du corps est faite par le pipeline des animaux. Vérifier le test
  `Bodies_AreMeasured_AndGreetingNosesTouchWithoutOverlap`.
- **Nouveau décor.** Son volume est mesuré automatiquement. Une pièce qui tourne doit porter `_Sails_` dans son nom
  (ou une règle équivalente) pour être balayée.
- **Nouvelle zone.** Elle doit passer `Props_NeverEnterEachOther_AcrossTheWholeStrip`,
  `Scatter_NeverGrowsInsideAProp_AndSolidDetailsKeepApart` et
  `Chunks_CarryObstaclesAndGround_SpawnPointsAndEggSpotStandClear`.
- **Décorations posées par le joueur (plus tard).** Le placement est refusé si la forme entre dans un décor, un
  animal, l'œuf ou une autre décoration. Elles deviennent ensuite des obstacles pour les animaux.
