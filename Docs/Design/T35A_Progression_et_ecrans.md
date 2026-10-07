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
- **Accès.** La fenêtre sera ouverte par la porte au bout de la Lisière (prefab monde à venir).

## Entrée des animaux dans une zone ouverte

- **Problème.** Les tests ont montré qu'un marcheur arrêté par un autre animal abandonnait sa marche. Un goulet
  entre deux décors suffisait alors à bloquer l'entrée.
- **Correctifs.**
  - Chaque marcheur va tout droit à sa propre profondeur. Les couloirs sont espacés de 0,9 m, en tenant compte des
    animaux déjà en route.
  - Les animaux dont la ligne droite ne touche aucun décor passent d'abord.
  - Une marche vers un but replanifie depuis là où elle s'est arrêtée, jusqu'à 6 essais.
  - Pendant 20 s, toutes les 1,5 s, si moins de trois animaux sont dans la zone ou en route, un autre part (6 au
    plus par ouverture).

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
- **Porte de la Forêt dans le monde.** `PF_World_BiomeGate_Forest`, une arche de branches de moins de 1 500
  triangles, au bout de la Lisière. Elle ouvre la fenêtre de porte.
- **Effets et sons dédiés.**
  - Effets H-VFX-100 à 105.
  - Sons J-SFX-UPGRADE-001, J-SFX-UPGRADE-002 et J-SFX-REWARD-005 à 007.
  - D'ici là, les événements sonores existants sont réutilisés : dépense de Lumière, maîtrise, récompense.
- **Zone fermée désaturée.** Elle doit être 35 % plus douce, réglée par un paramètre global du matériau.
