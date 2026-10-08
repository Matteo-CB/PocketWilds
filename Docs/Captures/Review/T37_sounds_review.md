# T37 : revue des sons et des musiques des activités

8 octobre 2026. Les sons partagés par toutes les activités et les trois musiques d'activité (doc 01 Contenu, section
12.0 « Sons, musique et effets partagés » ; doc 05 Sensations, fiche 20 et section 6.1 ; doc Audio, « Activity sounds
and music »). Vingt et un sons et trois musiques, tous synthétisés par le code du dépôt.

## Comment c'est fait et mesuré

- Recettes : `SourceArt/Audio/Tools/pw_audio_sfx.py`, section « T37 » (instruments de la maison : `tine`, `glass`,
  `mallet`, `_wood_knock`), et `SourceArt/Audio/Tools/pw_audio_music.py`, section « T37 » (partition circulaire
  `_Score`, marimba, kalimba tenue, piano feutré, pad PADsynth, basse ronde, shaker du thème). Aucun échantillon, aucun
  générateur, rien de téléchargé.
- Rendu : `pw_audio_build.py sfx <noms>` et `pw_audio_build.py music <noms>` (nouveau filtre par nom, le rendu complet
  reste le même). Masters 48 kHz 24 bits dans `SourceArt/Audio/Masters/`, fichiers du jeu 48 kHz 16 bits dans
  `Assets/_Game/Art/Audio/` (sons mono, queue silencieuse coupée ; musiques stéréo).
- Sons existants : les 66 sons et ambiances et le thème du refuge, rendus à nouveau à part avec les scripts modifiés,
  sont identiques à l'octet (132 fichiers, masters et fichiers du jeu). Aucun fichier existant n'a été réécrit.
- Mesures : `pw_audio_build.py report` (niveau sur les 200 ms les plus fortes, pondération K, échelle LUFS ; crête et
  vraie crête). En plus : point où le son passe sous -40 dB de son maximum, part de 2 à 5 kHz et part au-dessus de
  6 kHz dans la pire trame de 20 ms, attaque (10 à 90 %). Pour les musiques : niveau intégré, niveau par 4 mesures,
  bandes, jonction de la boucle (le calcul de `AudioAssetTests`), longueur en échantillons.
- Bords : chaque son commence sur 0 (fondu d'entrée de 1,5 ms) et finit sur 0 (fondu de 8 ms de la coupe).

## Choix faits (les docs se contredisent ou se taisent)

- Étoile et carte : ce sont J-SFX-FEEL-013 et J-SFX-FEEL-012 du doc 05, partagés avec d'autres écrans (étoiles d'album
  de la fiche 21, cartes de choix, retour d'expédition). Ils gardent les noms du doc 05 et du doc Audio :
  `SFX_Reward_Star_01` à `_03` (`StarEarned`) et `SFX_UI_CardFlip_01` à `_05` (`CardFlip`), à leurs places dans l'ordre
  du doc 05 section 6.4 (41 et 42).
- Étoile : le doc 05 dit « une note de verre », le doc 01 « lame de kalimba ». Le son est une lame de kalimba avec sa
  note de verre à l'octave : fa, la, do.
- Carte : cinq hauteurs au lieu de trois variantes. La fiche 20 veut une hauteur qui monte à chaque carte et la section
  6.4 borne une série à cinq notes ; au-delà de la cinquième carte, plus de son par carte.
- Nouveau record : la fiche 20 renvoie au son de prise (J-SFX-REWARD-001), le doc 01 donne un motif de quatre notes de
  boîte à musique (J-SFX-ACT-000-04). Le motif est fait ; le son de prise reste celui des récompenses.
- Pause et reprise : deux variantes, la pause en ré, la reprise une tierce plus haut en fa.
- Fin de partie : « la musique baisse de 5 dB » est tenu par le présentateur jusqu'à la fermeture du résultat (le
  creux automatique du service ne dure que 1,3 s).
- Musiques : boucles de 80, 120 et 100 s (aucune durée dans les docs ; les parties durent de 30 à 90 s et la musique
  continue d'une partie à l'autre). Niveau du thème du refuge (-20 LUFS intégrés), pour que le fondu entre le refuge et
  une activité garde le même volume. Noms tirés des titres anglais du doc Audio.

## Résultats : les sons

| Fichier | Durée du fichier | Sous -40 dB à | Niveau mesuré (cible) | Crête (vraie crête) | 2 à 5 kHz, pire trame | Au-dessus de 6 kHz |
| --- | --- | --- | --- | --- | --- | --- |
| `SFX_Activity_Start_01` | 1,052 s | 0,89 s | -20,0 (-20) | -11,5 dBFS (-11,5) | -11,1 dB | -22,8 dB |
| `SFX_Activity_End_01` | 1,857 s | 1,51 s | -18,0 (-18) | -7,4 dBFS (-7,4) | -24,2 dB | -25,1 dB |
| `SFX_Reward_Star_01` (fa) | 0,830 s | 0,73 s | -19,0 (-19) | -11,1 dBFS (-11,1) | -18,3 dB | -33,4 dB |
| `SFX_Reward_Star_02` (la) | 0,820 s | 0,73 s | -19,0 (-19) | -11,7 dBFS (-11,6) | -43,5 dB | -36,3 dB |
| `SFX_Reward_Star_03` (do) | 0,843 s | 0,73 s | -19,0 (-19) | -10,9 dBFS (-10,9) | -18,2 dB | -21,2 dB |
| `SFX_UI_CardFlip_01` à `_05` | 0,46 à 0,53 s | 0,33 à 0,42 s | -23,0 (-23) | -10,0 à -11,4 dBFS | -3,2 à -5,7 dB | -17,5 à -18,8 dB |
| `SFX_Activity_Record_01` | 1,522 s | 1,41 s | -17,0 (-17) | -9,0 dBFS (-9,0) | -13,1 dB | -37,6 dB |
| `SFX_Activity_Pause_01` (pause) | 0,308 s | 0,19 s | -25,0 (-25) | -10,2 dBFS (-10,2) | -32,0 dB | -73,7 dB |
| `SFX_Activity_Pause_02` (reprise) | 0,315 s | 0,19 s | -25,0 (-25) | -10,4 dBFS (-10,4) | -32,5 dB | -73,8 dB |
| `SFX_Activity_Streak_01` à `_08` | 0,51 à 0,59 s | 0,46 à 0,48 s | -22,0 (-22) | -11,2 à -13,4 dBFS | -48,8 à -19,6 dB | -38,9 à -23,4 dB |

Tous à leur cible, crêtes sous -7 dBFS. Repères de la famille, pire trame de 2 à 5 kHz puis au-dessus de 6 kHz :
onglet (papier) -3,5 et -11,5, ouverture de panneau -2,2 et -18,1, favori -12,0 et -23,9, Gemmes -13,9 et -26,1,
zone ouverte -8,9 et -18,4. La carte est du papier : sa part de 2 à 5 kHz est celle de l'onglet, à un niveau bas
(-23). Les sons musicaux restent dans la fourchette des sons de récompense. Attaques : de 1,1 à 1,6 ms pour les lames
(comme l'achat d'amélioration), 7 ms pour le départ, 93 ms pour le souffle de la carte, 160 ms pour l'accord roulé de
fin ; le toc de la pause est arrondi sur 1,5 ms (0,9 et 1,4 ms mesurées de 10 à 90 %).

Niveaux choisis (aucun dans les docs, sauf la carte et l'étoile au doc 05) : départ -20 (entre la confirmation, -22,
et les Gemmes, -19), fin -18 (comme la prise), record -17 (comme le cadeau du jour), pause -25 (comme l'interrupteur
éteint), série -22 (sous la collecte de Lumière, -21 : elle peut sonner souvent). À valider à l'écoute dans le jeu.

## Les sons

### Départ (`SFX_Activity_Start_01`, J-SFX-ACT-000-01)

- Intention : la partie commence. Une petite cloche de verre, puis la musique entre. Rien qui presse, pas de compte à
  rebours.
- Recette : une seule frappe, un petit verre en fa (note de verre de la maison et le partiel inharmonique d'une petite
  coupe de verre, x2,76, qui s'éteint vite), une lame de kalimba une octave plus bas pour la chaleur. Petite salle.

### Fin de partie (`SFX_Activity_End_01`, J-SFX-ACT-000-02)

- Intention : la partie est finie, les mains se posent. Un accord doux de kalimba.
- Recette : cinq lames roulées vers le bas, do, la, fa, puis do et fa graves, à 40 ms d'écart, sur un souffle d'accord
  très léger. Le son de prise roule vers le haut : les deux ne se confondent pas.

### Étoiles du résultat (`SFX_Reward_Star_01` à `_03`, J-SFX-FEEL-013 et J-SFX-ACT-000-03)

- Intention : chaque étoile se pose (Pop à 1,20, une toutes les 350 ms). Les trois étoiles dessinent un accord de fa
  majeur.
- Recette : une lame de kalimba (fa, la, do) et sa note de verre à l'octave, plus douce sur les notes hautes ; la
  troisième s'appuie sur un fa grave très léger, l'accord est complet.

### Cartes de récompense (`SFX_UI_CardFlip_01` à `_05`, J-SFX-FEEL-012)

- Intention : une carte de papier pivote (250 ms) et se pose ; la hauteur monte à chaque carte.
- Recette : le souffle du papier qui tourne (bruit filtré dont la bande monte, 130 ms), un tapotis léger du carton posé
  à 110 ms (le moment où la face apparaît), et avec lui une petite note de bois étouffée qui monte d'une carte à
  l'autre : fa, sol, la, do, ré (pentatonique de fa). Le présentateur lance le son au début du pivot, pour les cinq
  premières cartes.

### Nouveau record (`SFX_Activity_Record_01`, J-SFX-ACT-000-04)

- Intention : fier et léger, jamais une fanfare.
- Recette : quatre notes de boîte à musique, fa, la, sol, puis do laissé sonner (à 0, 0,1, 0,2 et 0,33 s), sur une
  quinte de lames (fa et do) et l'octave du do à peine audible. Écrit d'abord une octave plus haut, le motif plaçait
  ses notes dans la bande de 2 à 5 kHz (-0,7 dB dans la pire trame) : il a été descendu d'une octave (-13,1 dB).

### Pause et reprise (`SFX_Activity_Pause_01`, `_02`, J-SFX-ACT-000-05)

- Intention : un clic de bois doux, le bout du doigt sur une petite boîte de bois clair.
- Recette : le toc de bois de la maison (note creuse qui se pose un peu plus bas, deux partiels de bois courts),
  attaque arrondie, filtré sous 3,6 kHz. Pause en ré, reprise en fa.

### Série (`SFX_Activity_Streak_01` à `_08`, J-SFX-ACT-000-06)

- Intention : la série monte pendant le jeu, un demi-ton par palier, huit paliers, puis elle reste en haut.
- Recette : la lame de l'étoile, plus légère et plus courte, de fa à do (fa, fa dièse, sol, sol dièse, la, si bémol,
  si, do) : la montée finit sur la quinte de fa, stable. Le présentateur rejoue le huitième palier au-delà.

## Résultats : les musiques

| Fichier | Durée | Niveau intégré | Crête | Jonction (gauche, droite) | 2 à 5 kHz par seconde (médiane, pire) | Niveau par 4 mesures |
| --- | --- | --- | --- | --- | --- | --- |
| `MUS_Activity_LittleGame` | 80,000 s, 28 mesures à 84 BPM | -20,0 LUFS | -8,3 dBFS | 0,44 et 0,89 | -31,8 et -19,4 dB | intro -23,0, puis -18,9 à -20,4 |
| `MUS_Activity_HurriedPaws` | 120,000 s, 56 mesures à 112 BPM | -20,0 LUFS | -8,2 dBFS | 0,25 et 0,17 | -30,3 et -17,8 dB | intro -23,0, respiration -25,6, puis -18,5 à -22,0 |
| `MUS_Activity_HugStreak` | 100,000 s, 40 mesures à 96 BPM | -20,0 LUFS | -7,8 dBFS | 0,22 et 0,08 | -26,9 et -9,2 dB | intro -25,6, puis -17,8 à -21,7 |
| `MUS_RefugeTheme` (repère) | 192,000 s | -20,0 LUFS | -6,4 dBFS | 0,21 et 0,55 | -24,1 et -7,4 dB | de -26,9 à -17,5 |

- Jonction : écart entre le dernier et le premier échantillon rapporté au pas moyen ; `AudioAssetTests` demande moins
  de 4. Chaque longueur est un nombre entier d'échantillons à 48 kHz et à 44,1 kHz (multiple de 160 échantillons) :
  le point de boucle reste exact après le rééchantillonnage de Unity (musique importée à 44,1 kHz).
- Équilibre : la part de 500 Hz à 2 kHz porte la musique (lames et marimba), comme les sons. Les trois musiques sont
  plus douces dans les aigus que le thème (bande de 2 à 5 kHz entre -28 et -21 dB du total, contre -18,7) : on les
  écoute pendant qu'on se concentre. Une petite présence (+2 dB vers 2,2 kHz) évite un son étouffé sur un téléphone.
- Harmonie vérifiée mesure par mesure (chroma du mélange) : les notes les plus fortes de chaque mesure sont celles de
  l'accord écrit.

### « Petite partie » (`MUS_Activity_LittleGame`, J-MUS-ACT-01)

- Intention : calme, pour les activités où l'on prend son temps (Toilettage, Memory, Pâtisserie, Constellations,
  Taquin, Safari photo). 84 BPM, fa majeur, kalimba, marimba, cloche de verre.
- Forme : intro (le marimba berce sur fa sous le pad, la kalimba appelle doucement), A (mélodie de kalimba sur le
  marimba), B (le marimba chante, roulé sur ses notes longues, la kalimba et le verre répondent au-dessus), A' (la
  kalimba revient, une fin nouvelle qui repart vers l'intro).

### « Pattes pressées » (`MUS_Activity_HurriedPaws`, J-MUS-ACT-02)

- Intention : vive sans presser, pour les activités d'adresse (Cache-cache, Pluie de noisettes, Pêche aux trésors,
  Saut de nénuphars, Glissade, Bulles, Labyrinthe). 112 BPM, fa majeur, mêmes instruments, shaker doux, aucune
  batterie.
- Forme : intro, A, A2 (une autre réponse), B (le marimba mène, la kalimba pince les contretemps), C (respiration :
  verre et pad, le shaker sort puis revient), B2 (la kalimba reprend B), A3 (A avec sa seconde réponse, doublée au
  verre), quatre mesures de retour vers l'intro. La basse sautille (fondamentale, quinte, octave).

### « Câlins en série » (`MUS_Activity_HugStreak`, J-MUS-ACT-03)

- Intention : la musique de réflexion (Match des câlins, Grand Concours). 96 BPM, ré mineur doux vers fa majeur.
- Forme : intro en ré mineur (pulsation de kalimba, ligne lente de verre), A (piano feutré sur un marimba grave), A2
  (la kalimba, vers fa), B et B2 en fa majeur (la kalimba puis le marimba, dans la lumière), retour par sol mineur,
  do et si bémol vers un la suspendu qui rouvre sur ré mineur. Aucun do dièse : le mineur reste doux, jamais triste.

## Dans le jeu

- Chaque musique d'activité porte la couche d'ambiance du biome du thème à -6 dB (doc 01, section 12.0 ; doc 04
  Biomes tardifs) : le présentateur garde l'ambiance du biome dans `SetBeds` et demande `SetBedLevels(0, -6)`.
- Départ : le son de départ, puis la musique en fondu de 1 s. Une partie rejouée ne relance pas la musique : elle
  continue là où elle était, on n'entend pas l'intro à chaque partie.
- Fin : le son de fin et la musique 5 dB plus bas jusqu'à la fermeture du résultat ; retour au refuge : beds du biome,
  niveaux à 0.
- Étoiles, cartes, série : la variante est choisie par le présentateur (`PlayVariant`), la hauteur ne varie pas au
  hasard sur ces trois sons.

## Grille de la charte « fait main »

Les points 1 à 37, 39 et 40 portent sur les formes, la lumière, l'animation et les icônes : sans objet pour un son.

| Point | Sons de partie (départ, fin, pause, série) | Résultat (étoiles, cartes, record) | Musiques |
| --- | --- | --- | --- |
| 3. Chaque détail a une fonction écrite | vert : chaque couche nommée dans la recette | vert | vert : chaque section et chaque voix décrites dans la recette |
| 38. Aucune étoile à quatre branches | vert : noms de verre, de lame, de bois | vert | vert |
| 41. Source éditable, export reproductible | vert : master WAV et recette ; rendu déterministe, le même script redonne les mêmes octets | vert | vert : rendu deux fois, identique à l'octet |
| 42. Aucun générateur par IA (12.1) | vert : synthèse maison | vert | vert : notes écrites à la main dans la recette |
| 43. Vocabulaire | vert : noms et textes sans mot interdit, ni cloche grave ni chœur | vert | vert |
| 44. Fait par la même main (13.5) | vert : fa majeur, kalimba, verre et bois de la maison | vert : idem, papier de la famille de l'onglet | vert : instruments, tonalité et réverbération du thème du refuge |

Critères du doc 08 et du doc Audio :

| Critère | Résultat |
| --- | --- |
| Attaques douces, aucun clic | attaques de 0,9 à 160 ms (10 à 90 %), premier et dernier échantillon à 0 ; musiques jointes sans saut |
| Pas d'aigu perçant, pas de pic de 2 à 5 kHz | parts mesurées dans la fourchette de la famille ; record descendu d'une octave pour cela |
| Pas de longue queue | petite salle, sauf la fin (grande salle légère) ; pause, série et cartes sous 0,6 s, tous les autres sous 1,9 s |
| Pas de dépendance au grave | corps des sons entre 300 Hz et 2 kHz ; basse des musiques portée par ses harmoniques, rien sous 45 Hz |
| Ni pièces, ni casino, ni urgence | verre, bois, papier ; aucun compte à rebours ; séries bornées (cinq cartes, huit paliers) |
| Musique peu fatigante | boucles de 80 à 120 s en sections, mélodie qui passe d'un instrument à l'autre, aucune batterie, aucun motif répété toutes les deux mesures |

## Intégration (côté Unity)

`AudioEventId` reçoit `CardFlip = 41`, `StarEarned = 42` et `ActivityStart = 68` à `ActivityStreak = 72` ;
`AudioPipeline.Events()` les définit ; `AudioPipeline.EnsureActivityMusics()` met les trois musiques dans le groupe
Addressables `Audio_Activities` (adresses `audio.activity.little_game`, `audio.activity.hurried_paws`,
`audio.activity.hug_streak`). Lancer « Pocket Wilds/Audio/Build Audio and Haptics (T34B) » : les nouveaux fichiers
prennent les réglages d'import (sons mono ADPCM sous 1,2 s, Vorbis au-dessus ; musiques stéréo en streaming, Vorbis
44,1 kHz) et entrent dans `DEF_AudioEvents`. Les `.meta` sont créés par Unity à l'import.
