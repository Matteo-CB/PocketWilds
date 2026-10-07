# T35A Progression : écrans et HUD

But (file de production, T35A, étape 3) : l'interface des améliorations, des zones, du niveau du refuge et des portes
de biome, telle que la décrit le doc 02 Améliorations, section 11. La simulation (étape 1) et les zones dans le monde
(étape 2) étaient déjà là.

## HUD

- **Bouton Améliorer.** Rond, même taille que le Codex, juste au-dessus de lui (dans la zone du pouce ; la place à
  gauche du bouton Éclore est trop étroite à 1080 px de large). Icône `ICO_Upgrade` : une patte qui soulève un orbe
  chaud, sans flèche. Il apparaît après la troisième éclosion, avec la ligne « Rends ta Prairie plus lumineuse ».
- **Point du bouton** (section 3.6). Il s'allume quand une amélioration payable apporte au moins 5 % de la
  production totale, ou quand le palier × 2 suivant est payable. Pour Nids douillets, le gain estimé est l'économie
  sur les 10 prochains œufs, divisée par 600 secondes.
- **Patte du tutoriel.** Elle tape sur le bouton jusqu'à la première ouverture de l'écran, et plus jamais après le
  premier achat. Elle attend un moment calme et un premier niveau payable. La patte du premier œuf garde la priorité.
- **Compteur de Lumière.** Le toucher ouvre aussi Améliorer.
- **Insigne de niveau.** En haut à gauche : médaillon festonné, patte en haut, chiffre au milieu. À la montée de
  niveau (1,2 s), l'insigne grossit, puis un anneau et six pattes s'écartent. Un point s'allume tant qu'une carte de
  récompense attend.

## Écran Améliorer (I-SCR-039, `PF_UI_Upgrades`)

Panneau du bas sur les deux tiers de l'écran : le refuge reste visible au-dessus. Toucher le refuge ferme le panneau.

- **En-tête.** La production totale (elle compte vers sa nouvelle valeur en 0,4 s) et le choix × 1 / × 10 / Max. Le
  choix est mémorisé sur l'appareil, pas dans la sauvegarde.
- **Cartes.** Trois cartes réutilisées : Production, Nids douillets, Regard curieux. Chaque carte montre :
  - le niveau ;
  - ce que l'achat apporte (« +10 % de Lumière », « Œufs −4 % », « Variantes +1 % ») ;
  - la nouvelle production du biome, ou l'effet total ;
  - le prix et le nombre de niveaux achetés.
- **États d'une carte.**
  - Payable.
  - Pas assez : prix en couleur douce, jamais rouge, petite secousse au toucher.
  - « Au maximum ».
  - Verrouillée : « Maîtrise de la Prairie 2 », avec l'icône de maîtrise.
- **Ruban « Conseillé ».** Il va à la carte au plus court temps de retour, parmi Production et Nids douillets
  payables.
- **Bloc de maîtrise.** Niveau, barre, et ce que le prochain niveau apporte : une amélioration, une zone, ou +2 % de
  Lumière.
- **Prochaine zone.** Son nom sur sa propre ligne, son prix ou sa condition, et un bouton « Voir » : il ferme
  l'écran et fait glisser la caméra vers le panneau.
- **Achat.** La carte se tasse puis rebondit à 1,06, six éclats ronds montent. Un palier × 2 ajoute un anneau doré
  et des étoiles rondes à cinq branches. En série, un effet au plus toutes les 0,15 s.
- **Mise à jour.** Les cartes ne sont reconstruites que quand ce qu'elles montrent change : achat, prix franchi par
  la Lumière, maîtrise. Le contrôle tourne 4 fois par seconde et n'alloue rien en régime établi.
- **Œuf.** Un achat de Nids douillets baisse tout de suite le prix de l'œuf affiché.

## Fenêtre Ouvrir une zone (I-SCR-040, `PF_UI_ZoneOpen`)

- **Ouverture.** Toucher la haie ou le panneau l'ouvre. Le titre peut tenir sur deux lignes (« Ouvrir la Prairie
  fleurie »).
- **Capture.** Une vraie capture de la zone, rendue par `WorldCapture.CaptureZones` avec la lumière, le sol, les
  plantes et la vie du jeu : 740 × 356 px, coins arrondis, ASTC 6×6. Seule la capture de la prochaine zone est en
  mémoire : elle est chargée à part, quand sa haie se dresse.
- **Ce que la zone apporte.** « × 1,25 Lumière » et « +3 animaux visibles ». « 6 places de décoration » attend le
  mode décoration : on ne promet pas ce que la version n'a pas.
- **Prix et bouton.** Si une condition manque, la condition et sa barre s'affichent, et le bouton est éteint.

## Niveau du refuge (I-SCR-041, `PF_UI_RefugeLevel`) et carte de récompense

- **Écran du niveau.**
  - Grand chiffre dans le médaillon, barre et score.
  - Le score par source en mots simples, seulement les sources déjà gagnées (Espèces toujours).
  - La piste : le dernier niveau atteint (coché), puis les suivants avec leurs Gemmes et les fonctions qui s'ouvrent.
    Seules les fonctions présentes dans la version sont listées.
- **Carte de récompense** (`PF_UI_LevelReward`).
  - Médaillon, « Niveau 3 », Gemmes, fonctions nouvelles, bouton « Récupérer ». Plusieurs niveaux d'un coup donnent
    une seule carte.
  - La carte attend un vrai moment calme : 4 s sans éclosion ni écran, l'œuf suivant prêt mais pas touché, aucune
    zone en train de s'ouvrir. Elle ne coupe jamais une série d'éclosions.
  - Toucher l'insigne la montre tout de suite. Le fond ne la ferme pas : on récupère d'un geste.

## Porte de biome (I-SCR-042, `PF_UI_BiomeGate`)

- **Contenu.** Conditions cochées : niveau du refuge, espèces, maîtrise, jour ou minutes de jeu. Les jours sont dits
  en mots (« Demain », « Dans 2 jours »), jamais en compte à rebours. Puis six silhouettes rondes, le prix et
  « Ouvrir ».
- **Biome sans contenu.** Il affiche « Bientôt », sans prix ni bouton. C'est le cas de la Forêt dans cette version.
- **Accès.** Toucher la porte de la Forêt dans le monde ouvre la fenêtre (voir plus bas). Quand toutes les zones
  sont ouvertes, le bas de l'écran Améliorer annonce « Prochain biome : la Forêt », et « Voir » y fait glisser la
  caméra.

## Porte de la Forêt dans le monde

- **Modèle.** `GL_BiomeGate_Forest`, 1 459 triangles. Deux vieux troncs de part et d'autre du chemin se courbent et
  se croisent en une arche feuillue. Des branches tressées, avec quelques feuilles, ferment le passage. Bois et
  feuillage sont ceux des arbres de la Prairie (même matériau, même palette).
- **Place.** Quand les six zones à acheter sont ouvertes, la frontière suivante est la couture vers la Lisière
  (x = 91). La porte s'y dresse à la place du panneau, et la haie continue de part et d'autre.
- **Orientation.** La caméra du jeu regarde le long de la couture. La porte est donc tournée de 15° seulement par
  rapport à l'écran : son arche se lit de face, ses deux troncs enjambent la couture.
- **Volumes.** Les deux pieds et le tressage ont des volumes mesurés : rien n'y entre, et la haie s'arrête à ses
  troncs.
- **Ouverture.** Quand le contenu de la Forêt arrivera, la porte s'enfoncera comme la haie. La séquence de 2,5 s de
  la section 5.2 viendra avec elle.

## Entrée des animaux dans une zone ouverte

- **Problème.** Les tests ont montré qu'un marcheur arrêté par un autre animal abandonnait sa marche. Un goulet
  entre deux décors suffisait alors à bloquer l'entrée.
- **Correctifs.**
  - Chaque marcheur va tout droit à sa propre profondeur. Les couloirs sont espacés de 0,9 m, en tenant compte des
    animaux déjà en route.
  - Les animaux dont la ligne droite ne touche aucun décor passent d'abord.
  - Une marche vers un but patiente 2,5 s quand un animal lui barre la route (0,8 s pour une marche ordinaire), puis
    replanifie depuis là où elle s'est arrêtée, jusqu'à 6 essais.
  - Pendant 28 s, toutes les 1,5 s, si moins de trois animaux sont dans la zone ou en route, un autre part (6 au
    plus par ouverture).
  - Le test d'ouverture vérifie qu'un animal entre dans la minute : en flânant, les animaux préfèrent les endroits
    vides et finissent de toute façon par investir la nouvelle zone. Il a réussi 15 fois de suite.

## Zone fermée plus douce

- **Réglage.** La zone fermée est 35 % plus douce (section 4.6) : moins saturée, un peu plus claire, comme une brume
  de ce qui reste à découvrir. C'est un seul paramètre global, `_PW_ClosedZone` dans `PW_World.hlsl` : où commence
  la douceur, la largeur du fondu, l'intensité.
- **Shaders concernés.** Décors, feuillage et herbe, sol : calcul par sommet sur la couleur de base, sans
  interpolation en plus ni coût par pixel. Eau : par pixel.
- **Position.** La douceur commence 0,8 m après la haie et se fond sur 1,6 m : la haie et le panneau gardent leurs
  couleurs.
- **Ouverture.** À l'ouverture d'une zone, le début de la douceur glisse en 1,6 s jusqu'à la haie suivante (0,4 s
  avec Réduire les animations) : la couleur revient en vague.
- **Valeur par défaut.** À zéro, rien ne change. La scène le remet à zéro en sortant, et les captures aussi.

## Sons et effet dédiés

- **Sons.** Synthétisés comme ceux de la tranche, en fa majeur avec tine, cloche de verre et maillet, dans
  `SourceArt/Audio/Tools/pw_audio_sfx.py` :
  - J-SFX-UPGRADE-001 `SFX_Upgrade_Buy` : petit pincement qui monte, 3 variantes un ton plus haut chacune ;
  - J-SFX-UPGRADE-002 `SFX_Upgrade_Milestone` : accord doux de deux notes ;
  - J-SFX-REWARD-005 `SFX_Reward_ZoneOpen` : feuilles et arpège montant ;
  - J-SFX-REWARD-006 `SFX_Reward_RefugeLevel` : fanfare douce de quatre notes ;
  - J-SFX-REWARD-007 `SFX_Reward_BiomeMastery` : deux notes de bois et un bruissement.
- **Durées.** De 0,6 à 1,4 s, réverbération comprise.
- **Maîtrise de biome.** Elle joue son son et affiche une ligne calme : « Maîtrise de la Prairie : niveau 2 ».
- **Effet.** H-VFX-102 `VFX_ZoneOpen` : pétales et feuilles claires qui s'envolent le long de la haie qui s'enfonce,
  et quelques éclats ronds de lumière. 24 particules, matériau partagé, comptes selon le palier.

## Fichiers

- **Icônes.** `SourceArt/UI/Tools/pw_ui_sprites_t35a.py` génère `ICO_Upgrade`, `ICO_Upgrade_Production`,
  `ICO_Upgrade_Nests`, `ICO_Upgrade_Curiosity`, `ICO_ZoneSign`, `ICO_RefugeLevel`, `ICO_BiomeMastery` et
  `ICO_Check`.
- **Présentation.** Dans `Presentation/Scenes/` :
  - `ProgressionViews.cs` : contrats ;
  - `ProgressionPresenter.cs` : logique ;
  - `ProgressionText.cs` : clés de texte.
- **UI.** Dans `UI/Scripts/Progression/` : les cinq écrans et `UpgradeCardView`. `UI/Scripts/Screens/WindowMotion.cs`
  gère les ouvertures et fermetures, `UI/Scripts/Common/UiRow.cs` centre une icône et son texte.
- **Éditeur.**
  - `Editor/UI/ProgressionUiPipeline.cs` (menu « Pocket Wilds/UI/Build Progression Screens (T35A) ») : prefabs, HUD,
    câblage.
  - `Editor/Capture/WorldCapture.T35A.cs` (menu « Pocket Wilds/Capture/Zone Captures (T35A) ») : captures des zones.
- **Tests.** `Tests/PlayMode/ProgressionUiPlayTests.cs`.

## Reste à faire

- **Épicerie de friandises.** Elle attend que les friandises servent à quelque chose (souhaits, lien) : vendre un
  objet sans usage serait trompeur.
- **Effets restants.** H-VFX-100 (achat) et H-VFX-101 (palier) sont faits dans l'interface (éclats ronds, anneau doré
  et étoiles rondes de la carte). H-VFX-104 (niveau) est fait dans le HUD (anneau de pattes). H-VFX-103 (ouverture de
  biome) viendra avec le contenu de la Forêt. H-VFX-105 (maîtrise de biome) passe pour l'instant par le son et la
  ligne du HUD.
