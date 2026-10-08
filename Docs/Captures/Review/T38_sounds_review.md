# T38 : revue des sons du Toilettage

8 octobre 2026. Les sons du Toilettage (doc 01 Contenu, S-ACT-001 « Sons et effets » ; doc Audio, J-SFX-ACT-001-01 à
07) et la bouffée de bulles du doc 05 (J-SFX-FEEL-020), donnée à la bulle de savon. Dix-huit fichiers, tous synthétisés
par le code du dépôt. Le gloussement (-04) et le soupir content (-07) sont les voix de l'espèce : aucun fichier nouveau.

## Comment c'est fait et mesuré

- Recettes : `SourceArt/Audio/Tools/pw_audio_sfx.py`, section « T38 » (`groom_brush`, `groom_debris`, `groom_burr`,
  `groom_clean`, `groom_bow`, `bubble_puff`), avec les instruments de la maison (`_glass_note`, `_tine_under`,
  `_bubble`, `_puff`) et quatre petits outils nouveaux : l'enveloppe d'un coup de brosse (`_rise_fall`), le bruit dont
  la couleur suit le geste (`_moving_band`), le frémissement lent des poils (`_flutter`), le pop rond (`_pop`), le vol
  de la feuille (`_leaf_flight`), les crochets de la bardane qui lâchent (`_tug`), la perle de verre (`_glass_bead`).
  Aucun échantillon, aucun générateur, rien de téléchargé.
- Rendu : `pw_audio_build.py sfx SFX_Groom_Brush SFX_Groom_Debris SFX_Groom_Burr SFX_Groom_Clean SFX_Groom_Bow
  SFX_World_BubblePuff`. Masters 48 kHz 24 bits dans `SourceArt/Audio/Masters/SFX/`, fichiers du jeu 48 kHz 16 bits
  mono dans `Assets/_Game/Art/Audio/SFX/` (queue silencieuse coupée à -54 dB, fondu de 8 ms). Seul changement du rendu :
  le niveau cible peut être donné par variante (la bardane), le rendu des autres sons ne change pas.
- Sons existants : les 90 sons, ambiances et musiques d'avant T38, rendus à nouveau à part avec les scripts modifiés,
  sont identiques à l'octet (180 fichiers, masters et fichiers du jeu). Aucun fichier existant n'a été réécrit. Les
  dix-huit nouveaux fichiers, rendus deux fois, sont identiques à l'octet.
- Mesures (comme en T37) : niveau sur les 200 ms les plus fortes, pondération K, échelle LUFS ; crête et vraie crête ;
  point où le son passe sous -40 dB de son maximum ; part de 2 à 5 kHz et part au-dessus de 6 kHz dans la pire trame de
  20 ms ; attaque (10 à 90 %) ; premier et dernier échantillon.
- Bords : chaque son commence sur 0 et finit sur 0 (fondu d'entrée de 1,5 à 3 ms, fondu de 8 ms de la coupe).

## Choix faits (les docs se taisent)

- Niveaux : brosse -25 et zone propre -24 (sons fréquents, du côté de l'interface, entre -22 et -26), pops et nœud
  de -19 à -20 (récompenses de geste), premier toucher de la bardane -22,5 (sous le double pop qui l'enlève), bouffée
  de bulles -20 (valeur du doc 05).
- Brosse : un son par passage, pas de boucle. Le délai de 0,12 s et trois voix font des passages d'un doigt qui glisse
  une seule texture douce ; rien ne sonne quand le doigt s'arrête. La hauteur suit un peu la vitesse du doigt (0,94 à
  1,08) : un coup rapide sonne un peu plus clair.
- Bardane : deux variantes, ses deux touchers. Le doc ne donne qu'un « double pop » ; le premier toucher (la bardane
  se décroche) reçoit un pop plus doux et plus grave, pour que chaque toucher réponde.
- Zone propre : une perle de verre de fa à do (fa, fa dièse, sol, sol dièse, la, si bémol, si, do), la montée finit sur
  la quinte, stable. Écrite d'abord une octave plus haut, la montée passait sa note au-dessus de 2 kHz aux deux
  derniers paliers (part de 2 à 5 kHz de -4 et -1 dB) : elle est descendue d'une octave (-17 à -18,5 dB). La série du
  cadre (`ActivityStreak`, lame de kalimba) ne joue pas pendant le Toilettage : la zone propre est sa série.
- Nœud : do puis fa, la quarte qui revient à la maison, juste avant l'accord de fin qui descend du même do.
- Gloussement : la voix heureuse de l'espèce à +10 % (doc). Soupir content : sa voix de sommeil un peu plus grave
  (0,95), au moment où l'animal ferme les yeux sous les cœurs, environ 0,6 s après le nœud, pour ne pas se perdre sous
  le nœud et l'accord de fin.
- Bulle de savon : la bouffée de bulles du doc 05 (J-SFX-FEEL-020), à sa place dans l'ordre du doc 05 (49) ; la
  bouffée pleine quand on la touche, la plus légère quand elle apparaît.
- Brosse dorée (aucun son dans le doc) : deux petites notes de verre qui montent quand elle apparaît (son existant
  ToggleOn), l'accord doux de prise quand on la prend (Claim).

## Résultats

| Fichier | Durée du fichier | Sous -40 dB à | Niveau mesuré (cible) | Crête (vraie crête) | 2 à 5 kHz, pire trame | Au-dessus de 6 kHz | Attaque |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `SFX_Groom_Brush_01` (vers le haut) | 0,461 s | 0,35 s | -25,0 (-25) | -12,3 dBFS (-12,2) | -9,3 dB | -34,1 dB | 68 ms |
| `SFX_Groom_Brush_02` (vers le bas) | 0,446 s | 0,32 s | -25,0 (-25) | -12,3 dBFS (-12,3) | -9,1 dB | -32,6 dB | 95 ms |
| `SFX_Groom_Brush_03` (en arc) | 0,459 s | 0,34 s | -25,0 (-25) | -12,2 dBFS (-12,2) | -7,9 dB | -31,8 dB | 89 ms |
| `SFX_Groom_Debris_01` (fa) | 0,338 s | 0,23 s | -20,0 (-20) | -5,2 dBFS (-5,2) | -34,6 dB | -47,2 dB | 5,5 ms |
| `SFX_Groom_Debris_02` (la) | 0,326 s | 0,21 s | -20,0 (-20) | -4,8 dBFS (-4,8) | -33,9 dB | -48,7 dB | 5,1 ms |
| `SFX_Groom_Burr_01` (enlevée) | 0,432 s | 0,29 s | -20,0 (-20) | -8,0 dBFS (-8,0) | -12,0 dB | -33,1 dB | 72 ms |
| `SFX_Groom_Burr_02` (premier toucher) | 0,320 s | 0,22 s | -22,5 (-22,5) | -7,4 dBFS (-7,4) | -39,6 dB | -62,5 dB | 2,1 ms |
| `SFX_Groom_Clean_01` à `_08` | 0,29 à 0,41 s | 0,24 à 0,28 s | -24,0 (-24) | -11,6 à -13,2 dBFS | -17,2 à -18,5 dB | -46,9 à -72,9 dB | 1,8 à 2,2 ms |
| `SFX_Groom_Bow_01` | 1,203 s | 1,04 s | -19,0 (-19) | -9,9 dBFS (-9,9) | -13,3 dB | -24,8 dB | 136 ms |
| `SFX_World_BubblePuff_01` | 0,461 s | 0,35 s | -20,0 (-20) | -7,8 dBFS (-7,8) | -16,1 dB | -29,9 dB | 9,5 ms |
| `SFX_World_BubblePuff_02` | 0,436 s | 0,32 s | -20,0 (-20) | -6,8 dBFS (-6,8) | -12,9 dB | -31,8 dB | 11,4 ms |

Tous à leur cible, crêtes sous -4,7 dBFS. Repères de la famille (pire trame de 2 à 5 kHz, puis au-dessus de 6 kHz) :
onglet (papier) -3,5 et -11,5, ouverture de panneau -2,2 et -18,1, favori -12,0 et -23,9, Gemmes -13,9 et -26,1. La
brosse est plus douce dans les aigus que le papier de l'onglet et des cartes ; les sons de verre restent dans la
fourchette du favori et des Gemmes. Attaques : de 68 à 95 ms pour la brosse (un geste, pas un choc), 5 ms pour les pops
de feuille, 2 ms pour la perle de verre (arrondie sur 3 ms), le nœud commence par le souffle du ruban (136 ms jusqu'à
sa note).

Partie de 10 s simulée (le service imité : délai, voix, variations de niveau et de hauteur), sous la musique « Petite
partie » à son niveau par défaut et l'ambiance de la prairie à -6 dB : pendant un coup de brosse, les effets (brosse et
zones propres) mesurent de -22,5 à -23,4 LUFS sur la durée du coup et les beds de -22,5 à -27,5 ; le nœud -20,2 contre
-23,5. La brosse s'entend sans couvrir la musique. Sur toute la partie, une quatrième brosse n'a coupé que des queues
sous -58 dBFS.

## Les sons

### Brossage (`SFX_Groom_Brush_01` à `_03`, J-SFX-ACT-001-01)

- Intention : une brosse douce sur le pelage, feutrée, câline, jamais un grattement. Un passage par zone traversée ;
  tant que le doigt glisse, les passages font une seule texture.
- Recette : un souffle de bruit dont la couleur suit le geste (bandes d'une demi-octave pondérées autour d'un centre
  qui glisse : vers le haut, vers le bas, en arc), un frémissement lent et lisse de ce souffle (les mèches sous les
  poils, filtré sous 22 Hz : une texture, jamais un crépitement), le corps doux du coussinet de feutre qui appuie (200 à
  650 Hz), un peu d'air très bas. Tout est filtré sous 3,2 kHz, avec un creux léger vers 2,6 kHz. Une salle à peine.

### Débris enlevé (`SFX_Groom_Debris_01`, `_02`, J-SFX-ACT-001-02)

- Intention : un pop de feuille doux, pincé et mignon, puis la feuille qui s'envole en tournant (H-VFX-ACT-001-02).
- Recette : le petit claquement de la tige (bruit de 12 ms, très bas), un pop rond dont la hauteur monte dans sa note en
  12 ms (fa pour la première variante, la pour la seconde : une série de feuilles ne répète pas un seul son), puis des
  bouffées d'air douces au rythme de la feuille qui tourne (15 et 18 tours par seconde), de plus en plus loin.

### Bardane (`SFX_Groom_Burr_01`, `_02`, J-SFX-ACT-001-03)

- Intention : une bardane s'enlève en deux touchers. Le second fait le double pop ; le premier dit « elle bouge ».
- Recette : `_01`, les crochets lâchent le poil dans un murmure (six grains de 2 ms), deux pops ronds à la quarte, do puis
  fa, à 70 ms d'écart, et un petit envol. `_02`, un seul pop plus grave sur la, étouffé sous 2,6 kHz, avec le début du
  murmure des crochets.

### Zone propre (`SFX_Groom_Clean_01` à `_08`, J-SFX-ACT-001-05)

- Intention : un petit son de verre qui monte d'un demi-ton par zone propre d'affilée, huit paliers, très court, sous
  l'étoile du résultat (qui reste le grand son).
- Recette : une perle de verre presque pure (verre de la maison avec une modulation légère, arrondie sur 3 ms, et le
  partiel clair d'un petit verre à x2,76), son octave murmurée au-dessus, qui s'éteint vite (constante de temps de
  60 ms) ; de fa à do. Chaque palier est réglé au même niveau mesuré (-24) : la montée garde un volume égal.

### Nœud posé (`SFX_Groom_Bow_01`, J-SFX-ACT-001-06)

- Intention : deux notes de boîte à musique, le nœud est posé, c'est fini et c'est joli.
- Recette : le ruban de satin qui se pose (un souffle de 70 ms, très bas), do puis fa à la boîte à musique (à 0,05 et
  0,16 s), le fa laissé sonner sur une lame de kalimba douce en fa grave. Petite salle.

### Bouffée de bulles (`SFX_World_BubblePuff_01`, `_02`, J-SFX-FEEL-020)

- Intention : la bulle de savon touchée éclate en mousse douce ; une bulle qui apparaît sur le pelage.
- Recette : un souffle d'air et une petite grappe de bulles rondes dont la hauteur monte en se fermant : quatre bulles
  autour de fa (fa, la, sol, do) pour la bouffée pleine, trois plus légères (la, do, fa) pour l'apparition.

### Gloussement et soupir content (J-SFX-ACT-001-04 et 07)

- Les voix de l'espèce : sa banque heureuse à +10 % quand on rebrosse une zone propre (le jeu espace déjà les
  gloussements : un toutes les 0,6 s au plus), sa banque de sommeil à 0,95 à la fin. `IAudioService.PlayPitched` joue
  la voix de l'animal en gros plan : à plein niveau, au centre, quel que soit le regard posé sur le refuge, et même en
  économie de batterie.

## Grille de la charte « fait main »

Les points 1 à 37, 39 et 40 portent sur les formes, la lumière, l'animation et les icônes : sans objet pour un son.

| Point | Brosse, zone propre | Pops (débris, bardane, bulles) | Nœud |
| --- | --- | --- | --- |
| 3. Chaque détail a une fonction écrite | vert : chaque couche nommée dans la recette (souffle, mèches, coussinet, air ; perle et octave) | vert | vert : ruban, deux notes, lame |
| 38. Aucune étoile à quatre branches | vert : noms de brosse, de perle, de feuille, de bulle | vert | vert |
| 41. Source éditable, export reproductible | vert : master WAV 24 bits et recette ; rendu deux fois, identique à l'octet | vert | vert |
| 42. Aucun générateur par IA (12.1) | vert : synthèse maison | vert | vert |
| 43. Vocabulaire | vert : noms et textes sans mot interdit, aucune cloche, ni grave ni de chœur | vert | vert : boîte à musique |
| 44. Fait par la même main (13.5) | vert : fa majeur, verre de la maison, bruit filtré comme le panneau et la carte | vert : pop rond de la bulle du vœu, souffle du tampon | vert : boîte à musique et lame du thème |

Critères du doc 08 et du doc Audio :

| Critère | Résultat |
| --- | --- |
| Attaques douces, aucun clic | attaques de 1,8 à 136 ms (10 à 90 %), premier et dernier échantillon à 0, voix volées seulement dans leur queue (sous -58 dBFS) |
| Pas d'aigu perçant, pas de pic de 2 à 5 kHz | brosse sous le papier de la famille ; perle descendue d'une octave pour cela ; verre dans la fourchette du favori |
| Pas de longue queue | petite salle ; tous sous 0,5 s sauf le nœud (1,2 s, sa note qui sonne) |
| Pas de dépendance au grave | corps des sons entre 400 Hz et 2 kHz (centres de 425 à 1 211 Hz) |
| Ni pièces, ni casino, ni urgence | feutre, feuilles, verre, bulles ; séries bornées (huit paliers) |
| Sons fréquents peu fatigants | brosse à -25 et zone propre à -24, trois coups de brosse et deux variantes par pop, hauteur et niveau qui varient |

## Intégration (côté Unity)

- `AudioEventId` reçoit `BubblePuff = 49` (sa place dans l'ordre du doc 05) et `GroomBrush = 73`, `GroomDebris = 74`,
  `GroomBurr = 75`, `GroomZoneClean = 76`, `GroomBow = 77` ; `AudioPipeline.Events()` les définit (brosse : interface,
  0,12 s, trois voix ; débris et bardane : récompenses, 0,06 s, deux voix ; zone propre : 0,05 s, deux voix, sans
  variation de hauteur ; nœud : 0,5 s, une voix ; bulles : monde, 0,3 s, une voix, au centre).
- `IAudioService.PlayPitched` (par défaut : `Play`) et son code dans `AudioService` ; `GroomingAudio` donne les
  réglages et le son de chaque événement du jeu ; `ActivityAudio.HasOwnStreakSound` dit au présentateur de ne pas
  jouer la série du cadre pendant le Toilettage.
- Lancer « Pocket Wilds/Audio/Build Audio and Haptics (T34B) » : les nouveaux fichiers prennent les réglages d'import
  (mono, ADPCM sous 1,2 s, Vorbis pour le nœud) et entrent dans `DEF_AudioEvents`. Les `.meta` sont créés par Unity à
  l'import.
- Écoute : `T38_ecoute.mp3` (chaque son, puis une partie de 10 s simulée sous la musique).
