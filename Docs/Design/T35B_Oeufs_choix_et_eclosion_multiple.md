# T35B Œufs : types, choix et éclosion multiple

But (file de production, T35B) : les types d'œufs, leur choix sous le bouton Éclore, l'éclosion par 3 et par 10, la
fenêtre des chances et la carte de résumé, tels que les décrivent le doc 02 « Œufs et éclosions, règles complètes »
et le doc 05 « Sensations et surprises », section 30. Trois étapes : la simulation (1/3), le HUD et la mise en scène
(2/3), la carte de résumé, la mise en scène finale et les tests (3/3).

## 1. Simulation (étape 1)

- **Basic de biome.** Table de base (72 / 20 / 6 / 1,6 / 0,36 / 0,04 %), les 6 espèces du biome. Sa suite aléatoire
  ne change pas : les sauvegardes et les tests existants tirent les mêmes animaux.
- **Focus de biome.** Débloqué à la maîtrise 3 du biome. Prix 4 × B × 1,18^n, son propre compteur. Table 50 / 30 /
  14 / 4,5 / 1,3 / 0,2 %. Les 3 espèces visées sont celles du biome qui ont le plus de variantes de base non
  découvertes (à égalité, l'ordre des identifiants) ; toutes trouvées, l'œuf contient les 6 espèces.
- **Découverte.** Gratuit, gagné (calendrier, carnet, expéditions, événements, cadeaux). Trois règles dans l'ordre :
  une espèce non découverte d'un biome ouvert (poids × 2 pour le biome de la caméra), sinon une couleur de base non
  découverte d'une espèce connue, sinon Rare ou mieux (70 / 22 / 7 / 1 %). Jamais de variante secrète. Le stock se
  garde sans limite (sauvegarde v8, section World v3).
- **Éclosion multiple.** Exactement n éclosions simples : les garanties s'appliquent œuf par œuf, dans l'ordre. Elle
  part seulement si tout le prix est là (ou assez d'œufs Découverte).
- **Chances affichées = chances utilisées.** `HatchService.GetOdds` remplit la fenêtre des chances à partir des tables
  et des bonus réellement tirés (testé sur 10 millions de tirages).
- **Analytics.** EggBuy (33), MultiHatch (34), DiscoveryEggGain (35), EggOddsOpen (36) ; l'événement Hatch gagne
  l'index dans le lot et la règle Découverte.

## 2. Choisir son œuf (HUD)

- **Pastilles.** Sous le prix, une pastille par œuf proposé : Basic et Focus de chaque biome ouvert, Découverte s'il y
  en a en stock (4 au plus). Chaque pastille montre la coquille et les taches de l'œuf (`UI_Egg_Shell` et
  `UI_Egg_Spots`, dessinés par `pw_ui_sprites_t35b.py`, teintés aux couleurs de l'œuf). L'œuf choisi grossit et prend
  un anneau. Le stock d'un œuf Découverte s'affiche sur une pastille dorée. Un seul œuf : pas de pastille.
- **Zones tactiles.** 124 px autour de chaque pastille de 96 px (48 dp), comme le sélecteur × 1 / × 3 / × 10.
- **Glisser.** Un glissement horizontal sur le bouton Éclore passe à l'œuf suivant ou précédent (en boucle) ; un
  glissement n'ouvre jamais d'œuf.
- **Nom.** Le nom de l'œuf choisi s'affiche 1,6 s au-dessus du bouton. Le toast de maîtrise garde la priorité sur
  cette place.
- **Bouton.** L'œuf du bouton prend les couleurs de l'œuf choisi (l'icône dessinée reste pour le Basic de la Prairie).
  Le prix d'un œuf Découverte devient « Gratuit ».
- **Focus.** Ses 3 espèces visées, en colonne à gauche du bouton : le portrait d'une couleur connue, sinon la
  silhouette de l'espèce à trouver.
- **Sélecteur × 1 / × 3 / × 10.** À droite du prix : × 3 au niveau 12 du refuge, × 10 au niveau 25 après le premier
  Renouveau. Le prix affiché est la somme des prochains prix. Choix et quantité sont mémorisés sur l'appareil.
- **Place.** Le bloc Éclore monte de 48 px pour loger les pastilles ; le toast de maîtrise monte avec lui.

## 3. Fenêtre des chances

Titre : le nom de l'œuf. Une ligne par rareté : ses étoiles (★, à cinq branches), son mot, son pourcentage. Puis les
espèces visées (Focus), les bonus en cours (« Nouvelle espèce : +9 % », « Nouvelle couleur : +6 % »), la garantie
(« Une nouvelle espèce arrive au plus tard au 20e œuf. ») et le stock (Découverte). Pour un œuf Découverte, ses règles
d'abord.

## 4. Éclosion simple

Les 20 premières éclosions jouent la séquence complète. Ensuite, le réglage « Éclosions rapides » (activé par défaut,
écran des réglages en T56) joue la séquence à vitesse × 2,05 (environ 1,2 s).

## 5. Éclosion multiple (mise en scène)

- **Formation.** Un arc pour 3, deux rangées de 5 pour 10. Les écarts viennent des corps mesurés des espèces : le
  plus long museau plus la plus longue queue en profondeur (80 cm entre les rangées), la plus grande largeur en
  travers (46 cm dans une rangée), avec le léger dépassement d'échelle de la révélation. Chaque place est vérifiée
  pour un corps entier : aucun décor, pas d'eau, aucun animal.
- **Attente.** Le groupe attend que tout son cercle (formation et corps) soit libre d'animaux, au repos ou en marche.
  La zone devant l'œuf s'agrandit à ce cercle, et les animaux posés là s'en vont, aucun animal ne s'y arrête. Après
  4 s, les places sont prises une par une, puis sur deux anneaux autour de l'œuf. Au départ, la zone de l'œuf couvre
  tout le groupe ; un animal encore dedans en sort tout droit, sans passer entre deux œufs.
- **Rythme.** Pop des œufs (400 ms, 40 ms d'écart), balancement commun, fissures ensemble (1,5 s, un seul son par
  fissure), puis éclatement en vague de gauche à droite (60 ms d'écart).
- **Effets.** Au plus 3 éclosions complètes (les meilleurs résultats : nouvelle espèce, rareté, nouvelle couleur) ;
  les autres œufs jouent la petite coquille H-VFX-002 et ne lancent pas d'éclats. 5 sons de pop au plus, puis le son
  de la plus forte rareté. Vibration HatchCommon à l'éclatement, puis celle de la plus forte rareté à son moment.
- **Moments.** Chaque résultat Rare ou mieux, chaque nouvelle espèce ou couleur a 0,8 s à lui : la caméra se
  rapproche doucement, la bannière de révélation s'affiche. Un toucher passe au moment suivant.
- **Caméra.** Cadrage d'éclosion pour 3 ; pour 10, une vue plus large sur le milieu des deux rangées, pour que tout
  le groupe soit à l'écran.
- **Sortie.** Les nouveau-nés rejoignent le pré, et le planificateur des animaux conduit chacun vers une place
  libre : au-delà du cercle du groupe, sur un couloir qui ne rencontre aucun décor, pas d'eau, aucun animal, et ne
  croise jamais le couloir ni le point de départ d'un autre nouveau-né. Chaque pas est vérifié comme toute marche du
  pré. L'œuf suivant revient dès que sa place est libre (moins d'une seconde après la carte).
- **Réduire les animations.** Pas de pop ni de balancement, aucun éclat qui vole, pas de rapprochement de caméra.
- **Performance.** Pools de 10 œufs et de leurs effets (capacité souple = dure, pas de création ni destruction en
  jeu), au plus 20 effets en même temps. 0 allocation après warmup sur 10 éclosions × 10 (testé).

## 6. Carte de résumé (écran 028, `PF_UI_RewardSummary`)

À la fin d'une éclosion multiple : « Bienvenue au refuge ». Une cellule par nouveau-né, en grille (une rangée pour 3,
deux rangées de 5 pour 10) : son portrait dans le cadre de sa rareté (chaque rareté a son motif), son nom, ses étoiles,
le mot de sa rareté, et le badge « Nouveau » pour une nouvelle espèce ou couleur. La rareté ne dépend jamais de la
seule couleur. Les cellules apparaissent l'une après l'autre (en fondu avec les animations réduites). Un bouton
« Continuer » ferme la carte ; rien n'éclot dessous et aucune offre ne s'ouvre tant qu'elle est là.

## 7. Tests

- `EggTypesTests` (EditMode) : tables sur 10 millions de tirages, Focus (déblocage, cibles, seulement les cibles),
  garantie au 20e œuf, plafond de 40 %, jamais de secrète, règles Découverte et poids de la caméra, stock, × 10 =
  10 éclosions simples, prix entier exigé, chances affichées = chances utilisées.
- `MultiHatchPlayerTests` (PlayMode) : pop décalé, vague de gauche à droite à 60 ms, 3 effets complets, 5 pops,
  un son de fissure, son et vibration de la plus forte rareté, animations réduites sans éclats, 10 × 10 sans création
  ni allocation.
- `EggChoicePlayTests` (PlayMode, vraie scène) : pastilles, glissement, cibles du Focus, « Gratuit », fenêtre des
  chances, × 3 au niveau 12 ; deux × 10 de suite sans aucun chevauchement (pendant la mise en scène et la sortie),
  carte de résumé avec un badge « Nouveau » par nouvelle couleur, œuf suivant de retour.

## 8. Points ouverts

- **Prix du Focus.** Chaque type d'œuf a son compteur (doc 02, section 1) : après une dizaine d'œufs Basic, le Focus
  coûte moins cher que le Basic. À regarder avec le simulateur d'économie (T35C).
- **Garantie de variante.** Le code la garde dans la rareté tirée (doc Formules) ; le doc 02 Œufs, section 2.1, dit
  « à n'importe quelle rareté ». Décision du propriétaire attendue.
- **Lumière de rareté à la deuxième fissure** (doc 02, section 4) : pas encore faite.
