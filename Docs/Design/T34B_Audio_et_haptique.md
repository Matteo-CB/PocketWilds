# T34B : Audio et haptique de base

Tâche de la file de production (Phase 2), fixée par le gate T30 : sons créés pour le projet (synthèse maison, sans
dépendance tierce), ambiance Prairie, thème du refuge, profils haptiques et implémentation Android.
Critère : gates 14 et 15, aucune avalanche de sons avec 20 animaux.

## 1. Les sons, faits pour le projet

Tous les sons sont synthétisés par du code du dépôt : aucun échantillon, aucune banque de sons, aucune bibliothèque
audio tierce. Seul numpy fait le calcul. Le rendu est déterministe : le même script redonne les mêmes fichiers.

| Dossier | Rôle |
| :-- | :-- |
| `SourceArt/Audio/Tools/pw_audio_dsp.py` | Oscillateurs, enveloppes, filtres, réverbération synthétique, mesure de sonie (BS.1770), écriture WAV |
| `SourceArt/Audio/Tools/pw_audio_sfx.py` | Les 19 sons courts et leurs variantes |
| `SourceArt/Audio/Tools/pw_audio_ambience.py` | L'ambiance de la Prairie |
| `SourceArt/Audio/Tools/pw_audio_music.py` | Le thème du refuge (composition et rendu) |
| `SourceArt/Audio/Tools/pw_audio_build.py` | Rendu, niveaux, rapport, planches de contrôle |
| `SourceArt/Audio/Masters/` | Masters WAV 48 kHz, 24 bits |
| `Assets/_Game/Art/Audio/` | Fichiers du jeu, 48 kHz, 16 bits, sans silence final |

Commande : `python SourceArt/Audio/Tools/pw_audio_build.py all`, puis dans Unity
`Pocket Wilds/Audio/Build Audio and Haptics (T34B)`.

### Identité sonore

Tout est en fa majeur et utilise les trois mêmes instruments que le thème :

- une lame de kalimba (fondamentale chaude, harmonique métallique brève) ;
- une lame de bois façon marimba, au toucher arrondi ;
- une cloche de verre façon boîte à musique.

Ainsi les paliers de rareté se ressemblent et le jeu a une seule voix. Les basses profondes sont évitées (les
téléphones ne les rendent pas) et la bande de 2 à 5 kHz est adoucie partout.

### Inventaire

| Manifest | Fichiers | Caractère |
| :-- | :-- | :-- |
| J-SFX-EGG-001 | SFX_Egg_Tap_01..02 | « toc » de céramique et de bois, chaud |
| J-SFX-EGG-002 | SFX_Egg_Crack_01..03 | fines craquelures de coquille, petit souffle |
| J-SFX-EGG-003 | SFX_Hatch_Pop_01 | pop rond, bouffée d'air, trois petites notes de verre |
| J-SFX-EGG-004 à 007 | SFX_Reveal_Rare/Epic/Legendary/Fabulous_01 | montée de lames, accord plus riche, grappe de cloches chaudes, accord aérien de neuvième |
| J-SFX-CURRENCY-001 | SFX_Collect_Light_01..03 | petit « pling » cristallin |
| J-SFX-REWARD-002 | SFX_Reward_MasteryUp_01 | motif joyeux de trois notes |
| J-UI-001 à 004 | SFX_UI_Tap_01..03, Confirm, Back, PanelOpen | clics de bois très doux, pop chaleureux, clic plus grave, petit souffle |
| J-UI-009, J-UI-010 | SFX_UI_ErrorSoft_01, SFX_UI_PurchaseSuccess_01 | deux notes feutrées qui descendent, deux cloches à la quinte |
| J-AN-RabbitHappy, RabbitSleep | SFX_Animal_Rabbit_Happy_01..03, Sleep_01..02 | petit « ip » soufflé (voix stylisée à formants), souffle du nez |
| J-AN-SmallQuadruped_CuteChirp, Sleep | SFX_Animal_Small_Chirp_01..04, Sleep_01..02 | banque de famille : pip, petit trille, « ip ? », souffle |

Les voix d'animaux ne sont jamais des enregistrements et ne ressemblent jamais à un animal en détresse. L'écureuil
utilise la banque de famille à la hauteur 1,12 et le hérisson à 0,84 ; le lapin a sa voix propre.

### Niveaux

Les effets sont calés sur leur passage le plus fort (200 ms, pondération K) : l'oreille intègre un son court sur
environ ce temps, donc un clic de 90 ms et une révélation de 2 s se comparent justement.

| Catégorie | Niveau | Crête |
| :-- | :-: | :-: |
| Révélations Rare à Fabuleux | -15 | -5 à -6 dB |
| Pop d'éclosion, maîtrise, achat | -16 | -3,5 à -9,6 dB |
| Toc de l'œuf | -19 | -4 dB |
| Craquelures, collecte, voix d'animaux | -21 | -3 à -13 dB |
| Confirmer, erreur douce | -22, -23 | -10 à -15 dB |
| Ouvrir un panneau, retour | -25, -26 | -11 à -15 dB |
| Clic | -29 | -10 dB |
| Souffles de sommeil | -27 | -15 à -18 dB |
| Thème (intégré) | -20 LUFS | -6,4 dB |
| Ambiance (intégrée) | -27 LUFS | -13,5 dB |

La hiérarchie du doc 08 est respectée : la musique et l'ambiance restent sous le retour d'action, le clic est
discret, la collecte présente sans percer, la révélation est le son court le plus fort, toujours confortable.

## 2. L'ambiance de la Prairie (J-AMB-001)

- **Durée :** boucle de 75 s, en stéréo.
- **Contenu :**
    - une brise chaude (bruit rose filtré, rafales lentes) ;
    - le bruissement de l'herbe, qui monte avec les rafales ;
    - quelques froissements de feuilles ;
    - huit oiseaux lointains à des moments irréguliers, jamais à moins de 5 s l'un de l'autre, parfois avec une réponse
      plus loin. Pas de mélodie qui revient.
- **Raccord :** tout est rendu dans un tampon circulaire, réverbération comprise, donc la boucle n'a aucun raccord
  (saut au raccord : 0,27 d'un pas typique).
- **Spectre :** surtout entre 150 Hz et 1 kHz, presque rien au-dessus de 9 kHz.
- **Import :** Vorbis en flux, à 32 kHz.

## 3. Le thème du refuge (J-MUS-001)

- **Forme :** 64 mesures à 80 BPM en fa majeur, soit 192 s exactement, un nombre entier d'échantillons à 48 et à
  44,1 kHz : le point de boucle tombe pile.
- **Quatre parties :**
    - A : introduction, puis le thème au piano feutré ;
    - B : la mélodie passe à la lame de kalimba, avec une petite ligne de boîte à musique ;
    - C : une respiration très calme ;
    - A' : retour du thème. Il finit sur un silence de mélodie, et l'introduction repart naturellement.
- **Instruments :** piano feutré, lames de kalimba en arpèges, nappe PADsynth, basse ronde sans infrabasse et un
  souffle de shaker. Pas de batterie ni de voix.
- **Niveaux :** -20 LUFS, plage de sonie 9,7 LU. Par partie : A -21, B -18, C -25, A' -19.
- **Rendu et import :** rendu circulaire, réverbération comprise. Vorbis en flux à 44,1 kHz.

## 4. Le système

- **AudioService** (`Services/Feedback`) :
    - Le jeu n'émet que des AudioEventId, jamais des clips. 6 identifiants sont ajoutés à la fin de la liste : ErrorSoft,
      UiTap, UiConfirm, UiBack, AnimalSleep, AnimalChatter.
    - Il y a 12 voix créées une fois. Chaque son a son délai minimal, son nombre de voix maximal et sa priorité :
      1 achat et interface, 2 éclosion et récompenses, 3 animal touché, 4 animaux, 5 ambiance.
    - Quand le réservoir est plein, le son le moins important est remplacé ou ignoré. Rien n'est créé ni détruit par son.
- **Voix des animaux :**
    - Un animal touché répond tout de suite.
    - Un animal proche qui salue, se réjouit ou s'endort (prévenu par l'ordonnanceur) peut parler. Mais une seule voix de
      ce type existe pour tout le refuge, au plus toutes les 3 s (6 s pour le sommeil), jamais pendant une éclosion ou
      un écran. La batterie économe les coupe.
    - Les voix baissent avec la distance au point regardé et se placent à gauche ou à droite. Un animal hors de la vue
      ne prend pas de voix.
- **Collecte :** une seule série bornée de 5 notes qui montent (0, 2, 4, 7, 9 demi-tons), au rythme des particules qui
  arrivent dans le compteur. Jamais un son par particule.
- **Lits sonores :**
    - La musique et l'ambiance sont chargées avec la Prairie par Addressables (groupe Audio_Grassland, adresse
      audio.grassland) et passent en fondu enchaîné de 2,5 s.
    - Une révélation ou un achat baisse la musique de 5 dB pendant environ 1,3 s.
    - Musique coupée : le flux s'arrête et rien n'est décodé. L'ambiance suit le réglage des sons.
- **Mélangeur AM_Game :**
    - Master > Musique, Ambiance, Effets > Animaux, Interface, Récompenses, Monde.
    - Les réglages du joueur pilotent MusicVolume, AmbienceVolume et SfxVolume (déjà dans la sauvegarde :
      SettingsState).
- **Écoute :** une seule oreille (AudioListener), sur la racine audio permanente. La caméra du refuge n'en a plus, donc
  l'écoute existe même pendant le démarrage et les changements de scène.
- **Import :**
    - effets courts : mono, ADPCM en mémoire, préchargés ;
    - effets de plus de 1,2 s : Vorbis en mémoire ;
    - musique et ambiance : en flux.

## 5. L'haptique

- **Profils (DEF_HapticProfiles) :** un par HapticEventId, tous sous 300 ms, aucun ne se répète.

| Profil | Forme composée (Android 11+) | Forme de secours |
| :-- | :-- | :-- |
| TapLight | Tick 0,5 | 12 ms à 70 |
| Confirm | Click 0,6 | 18 ms à 130 |
| HatchCommon | Click 0,55 puis Tick 0,7 | deux impulsions légères |
| Rare | Click 0,85 puis Tick 0,6 | moyenne puis légère |
| Legendary | Click, Click, Tick | trois impulsions, 200 ms |
| Fabulous | QuickRise, Click 1,0, deux Tick | quatre temps, moins de 300 ms |
| ErrorSoft | LowTick 0,7 | une impulsion sourde de 35 ms à 70 |
| PurchaseSuccess | Click 0,7 puis Tick 0,5 | deux impulsions |
| AnimalTouch | Tick 0,3 | 8 ms à 45, coupé en économie de batterie |

- **Android :** la bibliothèque `Platform/Android/PocketWildsHaptics.androidlib` (Java, PwHaptics) déclare la
  permission VIBRATE et prépare chaque profil au démarrage, dans la meilleure forme que le téléphone sait jouer :
    - des primitives composées quand toutes sont gérées ;
    - sinon une forme d'onde avec amplitudes ;
    - sinon la même forme en marche et arrêt.
- **Comportement Android :**
    - Sur Android 13 et plus, ce sont des vibrations de toucher : le réglage du système s'applique aussi.
    - Le jeu joue un profil par un seul appel JNI préparé, sans allocation.
- **HapticService :**
    - réglage du joueur (HapticsEnabled) ;
    - délai par profil ;
    - 45 ms au moins entre deux vibrations, sauf si la suivante est plus forte (une éclosion passe après un toucher) ;
    - économie de batterie.
- **Autres plateformes :** un appareil sans vibreur et le PC ne jouent rien, sans erreur. Aucune information ne passe
  par la vibration seule : chaque profil double un son ou une image.

## 6. Validation

- **Tests côté éditeur :**
    - **AudioAssetTests (8 tests) :**
        - chaque son défini et routé vers son groupe ;
        - groupes et réglages du mélangeur ;
        - import par catégorie ;
        - boucles exactes et sans raccord ;
        - masters présents ;
        - lits dans leur groupe Addressables ;
        - une voix pour chaque espèce ;
        - aucun son d'ambiance capable de faire un chœur.
    - **HapticTests (6 tests) :**
        - profils courts et distincts ;
        - toucher léger ;
        - réglage qui coupe tout ;
        - appareil sans vibreur muet ;
        - le spam ne fait jamais une vibration continue ;
        - économie de batterie.
- **Tests en jeu :**
    - **AudioServicePlayTests (7 tests) :**
        - 30 s avec 20 animaux qui demandent leur voix 3 fois par image : au plus 4 voix d'animaux à la fois et une seule
          voix d'ambiance ;
        - 50 réactions d'un coup : 2 voix ;
        - spam de clics : au plus un son toutes les 40 ms ;
        - remplacement par priorité ;
        - collecte bornée ;
        - animal lointain ignoré ;
        - réglages ;
        - fondus et baisse de la musique ;
        - zéro allocation.
    - **AudioBootPlayTests (2 tests) :**
        - le jeu complet charge ses lits, l'éclosion sonne et vibre ;
        - avec les vibrations coupées dans la sauvegarde, rien ne vibre.
    - Les tests en jeu ne font plus aucun bruit sur le poste (son général coupé, l'audio tourne quand même).
- **Gate 14 :**
    - Source, master, fichier du jeu, définition, routage, voix maximales, délais, chargement : vérifiés.
    - Boucles sans raccord : vérifiées.
    - Pas de crête ni de dureté : mesuré (crêtes réelles sous -1 dB, bande 2 à 5 kHz adoucie).
    - En attente : l'écoute sur haut-parleur de téléphone et écouteurs (pas de téléphone de test) et l'avis du
      propriétaire (pack d'écoute envoyé).
- **Gate 15 :**
    - Vérifiés : profils courts, paliers distincts, réglage global, appareil sans vibreur, pas de vibration continue.
    - En attente : la sensation sur un vrai téléphone (T66).

## 7. Performance

Mesures faites sur le lecteur Windows de développement (PC, 480 x 1040), avec l'audio actif mais le son de sortie
coupé. Ce ne sont pas des mesures téléphone : il n'y a pas de téléphone de test.

| Mesure | Valeur |
| :-- | :-- |
| 22 scénarios, image la plus longue | 19,1 ms (Q1, échelle de qualité) ; 17,4 à 18,3 ms ailleurs |
| Images au-dessus de 33 ms | 0 |
| Chevauchements (règle « aucune collision ») | 0 |
| Thread principal, p95 | 1,9 à 2,8 ms (T34A : 1,7 à 2,5 ms) |
| Coût audio du thread principal (AudioManager.Update) | 0,042 à 0,055 ms par image, au plus 0,28 ms |
| A1 tempête audio : voix à la fois au plus | 7 sur 12 (au plus 4 pour les animaux) |
| A1 : sons joués, demandes ignorées | 94 à 104 joués, environ 3 200 ignorées (délais, plafonds, hors vue) |
| A1 : image la plus longue | 17,4 à 18,0 ms |
| GC par image | inchangé, sauf P5 (64 à 101 o) : le profileur attribue l'écart à l'interface des éclosions (bannière, textes), selon les raretés tirées, pas à l'audio (0 octet mesuré pour la lecture et le mélangeur) |
| Préparation des shaders au démarrage | 82 états en 108 ms, image la plus longue 31,9 ms |
| Première seconde du refuge (P0) | 27 à 29 ms (T34A : 23,5 ms), sous le seuil de 50 ms de T34 |
| Taille du lecteur | +4,3 Mo (170,9 à 175,2 Mo) |

Rapports : `Docs/Performance/Baselines/T34B_player_windows_audio.json` (22 scénarios) et
`T34B_player_windows_audio_rerun.json` (P0, P2, P5, U1, F1, A1, coût audio corrigé).

## 8. Écarts et suite

- **Groupe Audio_Common reporté :** les 33 effets (environ 1,3 Mo compressés) sont nécessaires dès la première image et
  partent avec SC_Bootstrap via DEF_AudioEvents. À revoir avec le groupe Bootstrap en T34C.
- **Réglages :** l'écran (musique, sons, vibrations) vient avec T56. Les valeurs de la sauvegarde sont déjà appliquées
  au démarrage.
- **Sons SoftLaunch faits en avance :** les révélations Épique à Fabuleux, l'ouverture de panneau, la maîtrise,
  l'erreur douce et l'achat existaient déjà comme événements dans le code ; ils ont maintenant leur son.
- **Pluie et nuit :** l'ambiance de pluie (J-WEATHER-RainLight) et une ambiance de nuit restent pour plus tard
  (SoftLaunch).

## 9. T34B2 : complément de la tranche

Ajouté le 6 octobre au soir, après la question du propriétaire sur les sons manquants. Le principe : tout ce que le
jeu actuel peut déjà jouer a maintenant son son ; le reste arrive avec sa fonction ou en T65.

### Ambiance : le soir et la pluie

- **Soirée (AMB_Grassland_Evening).**
    - Une boucle de 60 s : brise légère et six grillons lointains à des distances différentes, sans chant d'oiseau, à
      -29 LUFS.
    - Le jeu suit l'heure réelle du téléphone :
        - les grillons arrivent entre 19 h et 20 h 30 ;
        - ils repartent entre 5 h 30 et 7 h ;
        - la nuit des événements compte comme le soir.
- **Pluie fine (AMB_Rain_Light).**
    - Une boucle de 30 s : un lit feutré, des milliers de petites gouttes et quelques gouttes rondes qui tombent des
      feuilles, à -27 LUFS.
    - Elle se mêle selon la force de l'averse (WeatherSystem). La prairie du jour baisse de moitié et les grillons
      s'adoucissent.
- **Mélange.** Les couches bougent d'au plus 0,4 par seconde : jamais de coupure nette. Une couche silencieuse
  s'arrête et n'est plus décodée.

### Nouveaux sons

| Événement | Fichiers | Où |
| :-- | :-- | :-- |
| PanelClose | SFX_UI_PanelClose_01 | fermeture du Codex et de la boutique |
| UiTab | SFX_UI_Tab_01..02 | page d'une espèce dans le Codex (le retour joue UiBack) |
| ToggleOn, ToggleOff | SFX_UI_ToggleOn_01, SFX_UI_ToggleOff_01 | cœur favori |
| PurchasePending | SFX_UI_PurchasePending_01 | achat en attente |
| SpendLight | SFX_Currency_SpendLight_01 | œuf payé, sous le toc de l'œuf |
| Claim | SFX_Reward_Claim_01 | message de déblocage (boutique ouverte) ; plus tard les cadeaux |
| AnimalEat | SFX_Animal_Rabbit_Eat_01..02, SFX_Animal_Small_Eat_01..03 | un animal proche qui commence à manger |
| BirdTakeOff | SFX_World_BirdTakeOff_01..03 | un petit oiseau qui s'envole près de la vue |

- **Repas et envols :** au plus une voix pour tout le refuge, un repas toutes les 4 s et un envol toutes les 2,5 s au
  plus. Ils sont coupés en économie de batterie.
- **Offre :** son ouverture garde PanelOpen, comme le doc 08 le permet.

### Pas encore

- **Pas des animaux (J-MOVE).** Ils doivent tomber sur le contact des pattes. Cela demande de mesurer ce contact sur
  chaque clip d'animation et de stocker ces repères, plutôt que des événements d'animation qui allouent. Ce sera fait
  en T65.
- **Gemmes, Essence, biome débloqué, Renouveau.** Ces sons arrivent avec leur fonction.

### Validation et mesures

- **Tests :**
    - côté éditeur, 399 sur 399 : couches soir et pluie exactes et sans raccord, voix de repas pour chaque espèce, poids
      du soir selon l'heure ;
    - en jeu, 72 réussis, plus 1 benchmark qui ne se lance qu'à la demande : mélange des couches, repas et envols
      plafonnés.
- **Lecteur Windows (8 scénarios, dont la pluie R1) :**
    - image la plus longue 17,5 ms, aucune au-dessus de 33 ms, 0 chevauchement ;
    - audio 0,04 à 0,06 ms par image ;
    - tempête audio : 8 voix au plus en même temps.
- **Rapport :** `Docs/Performance/Baselines/T34B2_player_windows_layers.json`.
