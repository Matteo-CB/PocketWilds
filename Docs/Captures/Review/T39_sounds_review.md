# T39 : revue des sons du Cache-cache

9 octobre 2026. Les sons du Cache-cache (doc 01 Contenu, S-ACT-002 « Sons et effets » ; doc Audio, J-SFX-ACT-002-01 à
06). Dix fichiers, tous synthétisés par le code du dépôt. Le « ip ? » du coucou (-01) est la voix heureuse de l'espèce,
un peu plus aiguë : aucun fichier de voix nouveau.

## Comment c'est fait et mesuré

- Recettes : `SourceArt/Audio/Tools/pw_audio_sfx.py`, section « T39 » (`hideseek_coucou`, `hideseek_empty`,
  `hideseek_hint`, `hideseek_magnifier`, `hideseek_whistle`, `hideseek_all_found`), avec les instruments de la maison
  (`_glass_note`, `_tine_under`, `_glass_bead`, `_leaf_flight`, `_moving_band`, `_rise_fall`, `_flutter`) et trois petits
  outils nouveaux : les feuilles qui se frôlent (`_leaf_grains`, grains de bruit de 3 à 8 ms aux fenêtres arrondies), la
  résonance étroite d'un verre (`_resonance`), la flûte de bois douce (`_flute`). Aucun échantillon, aucun générateur,
  rien de téléchargé.
- Rendu : `pw_audio_build.py sfx SFX_HideSeek_Coucou SFX_HideSeek_Empty SFX_HideSeek_Hint SFX_HideSeek_Magnifier
  SFX_HideSeek_Whistle SFX_HideSeek_AllFound`. Masters 48 kHz 24 bits dans `SourceArt/Audio/Masters/SFX/`, fichiers du
  jeu 48 kHz 16 bits mono dans `Assets/_Game/Art/Audio/SFX/` (queue silencieuse coupée à -54 dB, fondu de 8 ms). Le
  script de rendu ne change pas.
- Sons existants : les 108 sons, ambiances et musiques d'avant T39, rendus à nouveau à part avec les scripts modifiés,
  sont identiques à l'octet (216 fichiers, masters et fichiers du jeu). Aucun fichier existant n'a été réécrit. Les dix
  nouveaux fichiers, rendus deux fois, sont identiques à l'octet.
- Mesures (comme en T37 et T38) : niveau sur les 200 ms les plus fortes, pondération K, échelle LUFS ; crête et vraie
  crête ; point où le son passe sous -40 dB de son maximum ; part de 2 à 5 kHz et part au-dessus de 6 kHz dans la pire
  trame de 20 ms ; attaque (10 à 90 %) ; premier et dernier échantillon.
- Bords : chaque son commence sur 0 et finit sur 0 (fondu d'entrée de 1,5 à 3 ms, fondu de 8 ms de la coupe).

## Choix faits (les docs se taisent)

- Niveaux : froissement du toucher vide -25 et bruissement de l'indice -27 (sous le toucher vide : il invite à regarder,
  il n'appelle pas), loupe -23, pop du coucou et sifflet -21, motif « tous trouvés » -20 (sous l'étoile du résultat,
  -19).
- Coucou : le pop sous la voix, et la voix heureuse de l'espèce à 1,12 (`HideSeekAudio.CoucouPitch`) : sa banque et sa
  hauteur propres, un peu plus haut, comme une petite question. Le pop monte d'un ton à la fin pour porter ce « ? ». Deux
  variantes (fa vers sol, sol vers la) : les coucous d'une partie ne répètent pas un seul son. Trouvé avant ou après son
  indice (ou avec la loupe, le sifflet) : le même coucou, les points et l'anneau disent la différence.
- Toucher vide : un froissement de feuilles, trois variantes, au plus un toutes les 0,3 s (trois touchers rapides ne
  s'empilent jamais). Jamais de buzzer, jamais de note qui descend. La pause d'une seconde après trois touchers vides
  est silencieuse, l'erreur du défi 6 aussi (son toucher a déjà froissé les feuilles).
- Indice : deux houles d'herbe, lisses, plus discrètes que le toucher vide. Plusieurs animaux qui donnent leur indice au
  même moment ne font qu'un bruissement (0,6 s, une voix). Les cachettes leurres bougent au vent sans bruit : le
  bruissement de l'indice veut toujours dire « un animal est là ». L'animal qui change de cachette (défi 9) bruisse comme
  un indice, doucement, pour qu'on sache qu'il faut regarder encore.
- Loupe : un souffle de verre, puis une petite perle de verre sur do quand l'anneau se pose autour de la cachette.
- Sifflet : deux notes d'une flûte à coulisse en bois, fa puis do au-dessus en glissant, comme un « où êtes-vous ? »
  qui monte. Sous 1,1 kHz, presque pur : un appel tendre, aucun cri d'oiseau, aucune alerte.
- Tous trouvés : do, ré, puis le fa au-dessus à la boîte à musique (court, court, long, qui grandit vers le fa), chacun
  sur une lame de kalimba une octave plus bas. Il monte depuis le do d'où l'accord de fin du cadre descend au même
  moment : les deux se rejoignent en un seul fa majeur, et son accent tombe sur le fa, quand le pop du dernier coucou et
  les premières notes de l'accord sont passés.

## Résultats

| Fichier | Durée du fichier | Sous -40 dB à | Niveau mesuré (cible) | Crête (vraie crête) | 2 à 5 kHz, pire trame | Au-dessus de 6 kHz | Attaque |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `SFX_HideSeek_Coucou_01` (fa vers sol) | 0,407 s | 0,29 s | -21,0 (-21) | -10,4 dBFS (-10,4) | -16,7 dB | -32,9 dB | 2,1 ms |
| `SFX_HideSeek_Coucou_02` (sol vers la) | 0,436 s | 0,28 s | -21,0 (-21) | -10,4 dBFS (-10,4) | -16,4 dB | -31,7 dB | 1,8 ms |
| `SFX_HideSeek_Empty_01` | 0,425 s | 0,31 s | -25,0 (-25) | -9,2 dBFS (-9,1) | -6,9 dB | -31,8 dB | 68 ms |
| `SFX_HideSeek_Empty_02` | 0,476 s | 0,36 s | -25,0 (-25) | -10,8 dBFS (-10,8) | -5,1 dB | -29,7 dB | 64 ms |
| `SFX_HideSeek_Empty_03` | 0,394 s | 0,30 s | -25,0 (-25) | -9,1 dBFS (-9,1) | -4,6 dB | -31,9 dB | 28 ms |
| `SFX_HideSeek_Hint_01` | 0,624 s | 0,50 s | -27,0 (-27) | -15,9 dBFS (-15,9) | -5,1 dB | -31,5 dB | 96 ms |
| `SFX_HideSeek_Hint_02` | 0,553 s | 0,42 s | -27,0 (-27) | -15,2 dBFS (-15,2) | -3,9 dB | -29,1 dB | 64 ms |
| `SFX_HideSeek_Magnifier_01` | 0,638 s | 0,57 s | -23,0 (-23) | -12,5 dBFS (-12,5) | -4,3 dB | -24,5 dB | 126 ms |
| `SFX_HideSeek_Whistle_01` | 0,860 s | 0,77 s | -21,0 (-21) | -15,8 dBFS (-15,8) | -13,0 dB | -43,2 dB | 28 ms |
| `SFX_HideSeek_AllFound_01` | 1,260 s | 1,17 s | -20,0 (-20) | -10,7 dBFS (-10,7) | -13,0 dB | -23,9 dB | 202 ms jusqu'au fa (2 ms par note) |

Tous à leur cible, crêtes sous -9 dBFS. Repères de la famille (pire trame de 2 à 5 kHz, puis au-dessus de 6 kHz) :
onglet (papier) -3,5 et -11,5, ouverture de panneau -2,2 et -18,1, brosse -7,9 à -9,3 et -31,8 à -34,1, nœud -13,3 et
-24,8. Les froissements et la loupe se placent entre la brosse et le papier de l'onglet, plus doux que lui au-dessus de
6 kHz ; leur pire trame tient à une feuille qui frôle (la moyenne des trames d'un froissement est vers -11 dB). Écrites
d'abord plus claires (feuilles jusqu'à 3,6 kHz, souffle de la loupe jusqu'à 4,5 kHz, résonance sur fa aigu), les
feuilles sont descendues vers 0,6 à 2,2 kHz et la loupe chante sur do : au-dessus de 6 kHz, -20 et -13 sont devenus
-30 et -24. Le pop du coucou, le sifflet et le motif restent dans le registre doux (centres de 740 à 1 150 Hz).

Partie de 10 s simulée (le service imité : délais, voix, variantes qui ne se répètent pas, variations de niveau et de
hauteur des définitions), sous « Pattes pressées » à son niveau par défaut et l'ambiance de la prairie à -6 dB, la
musique 5 dB plus bas à la fin :

| Moment | Effets | Beds |
| --- | --- | --- |
| Coucou (pop et voix ; lapin, écureuil, hérisson) | -18,2 à -18,7 | -21,8 à -22,1 |
| Toucher vide | -25,8 | -22,3 |
| Trois touchers vides rapides (le deuxième tombe dans le délai, le troisième déclenche la pause, muette) | -25,6 | -22,7 |
| Indice (deux animaux au même moment : un seul bruissement) | -27,2 | -22,2 |
| Loupe | -23,0 | -19,8 |
| Sifflet | -21,0 | -22,1 |
| Dernier coucou, « tous trouvés » et accord de fin ensemble | -14,7 | -22,8 |

Les froissements mesurent moins que les beds, mais ils sont ailleurs dans le spectre : par tiers d'octave, le toucher
vide passe de 10 à 21 dB au-dessus des beds entre 1 et 3,2 kHz, l'indice de 12 à 20 dB entre 1,2 et 3,2 kHz, la loupe de
20 à 27 dB entre 1,6 et 4 kHz (la musique et l'ambiance y sont pauvres). Ils s'entendent sans couvrir la musique. La fin
empile quatre sons (accord seul -18, motif et accord -15,1, coucou et accord -15,3, les quatre -14,1) : le sommet de la
partie, au niveau des révélations de rareté (-15), sans saturer (crête de la somme sous -3 dBFS, au pire des voix, des variantes et des variations).

## Les sons

### Coucou (`SFX_HideSeek_Coucou_01`, `_02`, J-SFX-ACT-002-01)

- Intention : l'animal sort de sa cachette en « coucou » ; le pop porte la question de son « ip ? ».
- Recette : un murmure de feuilles de 45 ms (il pousse les herbes), puis un pop rond qui monte dans sa note en 10 ms et
  continue de monter d'un ton sur 80 ms, sa hauteur tenue un instant avant de s'éteindre. Fa vers sol, ou sol vers la.
  La voix est jouée avec lui : la banque heureuse de l'espèce à 1,12, en gros plan.

### Toucher vide (`SFX_HideSeek_Empty_01` à `_03`, J-SFX-ACT-002-02)

- Intention : un froissement de feuilles doux, gentil et léger ; il n'y a personne ici, on cherche ailleurs.
- Recette : le buisson qui bouge un peu (un souffle de 0,6 à 2,2 kHz et son corps chaud, 220 à 700 Hz), les feuilles qui
  se frôlent (32 à 44 grains de 3 à 8 ms aux fenêtres arrondies, chacun dans la bande d'une feuille de sa taille, de 0,6
  à 2,2 kHz : un froissement, jamais un crépitement), puis quelques feuilles qui s'envolent en tournant (H-VFX-ACT-002-02).
  Trois secousses de longueur et de forme différentes.

### Indice (`SFX_HideSeek_Hint_01`, `_02`, J-SFX-ACT-002-03)

- Intention : l'herbe de la cachette ondule (H-VFX-ACT-002-03) ; un bruissement court qui fait tourner la tête.
- Recette : deux houles d'herbe qui se suivent, un souffle lisse dont la couleur monte un peu en haut de chaque houle
  (de 750 Hz à 1,5 kHz), quelques brins qui se touchent, très bas. Lisse, là où le toucher vide froisse.

### Loupe (`SFX_HideSeek_Magnifier_01`, J-SFX-ACT-002-04)

- Intention : un souffle de verre ; la loupe se lève et entoure doucement la cachette.
- Recette : un souffle dont la couleur monte quand la loupe se lève et redescend, le verre qui chante faiblement dedans
  (le même souffle passé par deux résonances étroites : do et le partiel d'un petit verre au-dessus, comme un souffle sur
  le bord d'un verre), puis une petite perle de verre sur do quand l'anneau se pose.

### Sifflet (`SFX_HideSeek_Whistle_01`, J-SFX-ACT-002-05)

- Intention : deux notes de flûte douce ; on appelle les animaux encore cachés, ils remuent.
- Recette : une flûte de bois jouée doucement (un son presque pur, un peu d'harmonique deux et trois, le souffle du
  joueur), fa puis un glissé de 50 ms jusqu'au do au-dessus, un léger creux entre les deux notes, un vibrato lent qui
  arrive sur la seconde, une petite attaque d'air. Sous 1,1 kHz.

### Tous trouvés (`SFX_HideSeek_AllFound_01`, J-SFX-ACT-002-06)

- Intention : un motif joyeux de trois notes, sous les étoiles du résultat, avec l'accord de fin du cadre.
- Recette : do, ré, fa à la boîte à musique (à 0, 0,1 et 0,2 s), chacun sur une lame de kalimba une octave plus bas, le
  fa laissé sonner sur une lame en fa grave. Petite salle.

## Grille de la charte « fait main »

Les points 1 à 37, 39 et 40 portent sur les formes, la lumière, l'animation et les icônes : sans objet pour un son.

| Point | Froissements (vide, indice) | Coucou, loupe | Sifflet, tous trouvés |
| --- | --- | --- | --- |
| 3. Chaque détail a une fonction écrite | vert : buisson, corps, feuilles, envol ; houles et brins | vert : murmure, pop, montée ; souffle, chant du verre, perle | vert : flûte, glissé, vibrato ; trois notes et leurs lames |
| 38. Aucune étoile à quatre branches | vert : noms de feuilles, d'herbe, de verre, de flûte | vert | vert |
| 41. Source éditable, export reproductible | vert : master WAV 24 bits et recette ; rendu deux fois, identique à l'octet | vert | vert |
| 42. Aucun générateur par IA (12.1) | vert : synthèse maison | vert | vert |
| 43. Vocabulaire | vert : noms et textes sans mot interdit, aucune cloche | vert | vert : boîte à musique, flûte de bois |
| 44. Fait par la même main (13.5) | vert : bruit filtré et feuille qui s'envole de la brosse et du débris | vert : pop rond du débris et de la bulle, perle de verre de la zone propre | vert : fa majeur, verre et lames du thème |

Critères du doc 08 et du doc Audio :

| Critère | Résultat |
| --- | --- |
| Attaques douces, aucun clic | attaques de 1,8 à 126 ms (10 à 90 % ; le motif : 2 ms par note), premier et dernier échantillon à 0 ; deux voix pour le toucher vide : aucune coupée |
| Pas d'aigu perçant, pas de pic de 2 à 5 kHz | froissements et loupe entre la brosse et le papier de l'onglet, descendus pour cela ; pop, sifflet et motif sous -13 dB dans cette bande |
| Pas de longue queue | petite salle ; tous sous 0,9 s sauf le motif (1,26 s, son fa qui sonne) |
| Pas de dépendance au grave | corps des sons entre 600 Hz et 2 kHz (centres de 740 à 1 590 Hz) |
| Ni pièces, ni casino, ni urgence | feuilles, herbe, verre, flûte de bois ; aucun son de « faux », aucun compte à rebours |
| Sons fréquents peu fatigants | toucher vide à -25 et indice à -27, trois et deux variantes, hauteur et niveau qui varient, délais |

## Intégration (côté Unity)

- `AudioEventId` reçoit `HideSeekCoucou = 78`, `HideSeekEmpty = 79`, `HideSeekHint = 80`, `HideSeekMagnifier = 81`,
  `HideSeekWhistle = 82`, `HideSeekAllFound = 83` ; `AudioPipeline.Events()` les définit (coucou : récompenses, 0,08 s,
  deux voix ; toucher vide : monde, 0,3 s, deux voix, volume 0,9 ; indice : monde, 0,6 s, une voix ; loupe et sifflet :
  interface, 1 s, une voix ; motif : récompenses, 1 s, une voix ; pas de variation de hauteur pour les sons mélodiques).
- `HideSeekAudio.Play(audio, in e, espèce, position)` donne le son de chaque événement du jeu ; la scène le lui passe
  pour chaque événement vidé, avec l'espèce de l'animal trouvé. `HideSeekAudio.Coucou` joue le pop et la voix.
  L'accord de fin reste au présentateur (`ActivityEnd`).
- Lancer « Pocket Wilds/Audio/Build Audio and Haptics (T34B) » : les nouveaux fichiers prennent les réglages d'import
  (mono, ADPCM jusqu'à 1,2 s, Vorbis pour le motif) et entrent dans `DEF_AudioEvents`. Les `.meta` sont créés par Unity à
  l'import.
- Écoute : `T39_ecoute.mp3` (chaque son, chaque coucou avec sa voix, la fin telle qu'elle tombe, puis une partie de 10 s
  simulée sous la musique).
