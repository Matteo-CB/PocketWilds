# T40 : revue des sons de la Pluie de noisettes

10 octobre 2026. Les sons de la Pluie de noisettes (doc 01 Contenu, S-ACT-003 « Sons et effets » ; doc Audio,
J-SFX-ACT-003-01 à 05). Sept fichiers, tous synthétisés par le code du dépôt. Le pas de série (-04) reprend
J-SFX-ACT-000-06 (`SFX_Activity_Streak`, `AudioEventId.ActivityStreak = 72`) : aucun fichier nouveau pour lui.

## Comment c'est fait et mesuré

- Recettes : `SourceArt/Audio/Tools/pw_audio_sfx.py`, section « T40 » (`nutdrop_catch`, `nutdrop_golden`,
  `nutdrop_pinecone`, `nutdrop_special`), avec les instruments de la maison (`_pop`, `_wood_knock`, `_small_chime`,
  `_tine_under`, `_glass_note`, `_leaf_grains`, `_leaf_flight`, `_moving_band`, `swell`) et un petit outil nouveau :
  l'osier qui reçoit l'objet (`_wicker`, quelques grains de roseau sec de 3 à 8 ms aux fenêtres arrondies). Aucun
  échantillon, aucun générateur, rien de téléchargé.
- Rendu : `pw_audio_build.py sfx SFX_NutDrop_Catch SFX_NutDrop_Golden SFX_NutDrop_Pinecone SFX_NutDrop_Special`. Masters
  48 kHz 24 bits mono dans `SourceArt/Audio/Masters/SFX/`, fichiers du jeu 48 kHz 16 bits mono dans
  `Assets/_Game/Art/Audio/SFX/`. Le script de rendu ne change pas.
- Sons existants : les 118 sons, ambiances et musiques d'avant T40, rendus à nouveau à part avec les scripts modifiés,
  sont identiques à l'octet (236 fichiers, masters et fichiers du jeu) ; aucun fichier existant n'a été réécrit. Les
  sept nouveaux, rendus trois fois, sont identiques à l'octet.
- Mesures (comme en T37 à T39) : niveau sur les 200 ms les plus fortes, pondération K, échelle LUFS ; crête et vraie
  crête ; point où le son passe sous -40 dB de son maximum ; part de 2 à 5 kHz et part au-dessus de 6 kHz dans la pire
  trame de 20 ms ; attaque (10 à 90 %) ; premier et dernier échantillon à 0.

## Choix faits (les docs se taisent)

- Hauteur de la prise : trois variantes, une par objet, sur les trois notes de l'accord de fa majeur comme les étoiles
  du résultat : noisette fa, gland la, baie do. La scène choisit la variante (`NutDropAudio.CatchVariant`), le
  service ajoute 1,5 % de variation de hauteur pour qu'une suite de noisettes reste vivante.
- Niveaux : prise -21 (le retour principal, le plus fréquent, jusqu'à 2,5 par seconde : un peu au-dessus de la
  musique « Pattes pressées », sous la noisette dorée et l'étoile du résultat), noisette dorée -20, objet spécial -20,
  pomme de pin -23 (sous la prise : jamais une punition).
- Série : le pas de J-SFX-ACT-000-06 sonne à chaque +0,1 du multiplicateur (toutes les 5 prises d'affilée), fa à la
  première marche, un demi-ton plus haut à chaque marche, do à × 1,8 puis il reste en haut. Le pas du cadre, joué à
  chaque prise par le présentateur, se tait pour cette activité (`ActivityAudio.HasOwnStreakSound`).
- Silences : un objet qui apparaît, un objet manqué (il roule dans l'herbe, rien n'est perdu), la fin d'un effet
  spécial et la fin de la partie (l'accord du présentateur) ne font pas de son.
- Une pomme de pin ou un objet spécial annoncé à la fois comme prise et comme événement propre ne sonne qu'une fois :
  le délai de la définition garde le premier.

## Résultats

| Fichier | Durée | Sous -40 dB à | Niveau (cible) | Crête (vraie crête) | 2 à 5 kHz, pire trame | Au-dessus de 6 kHz | Attaque |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `SFX_NutDrop_Catch_01` (noisette, fa) | 0,310 s | 0,20 s | -21,0 (-21) | -4,5 dBFS (-4,5) | -27,4 dB | -43,8 dB | 3,5 ms |
| `SFX_NutDrop_Catch_02` (gland, la) | 0,296 s | 0,20 s | -21,0 (-21) | -4,9 dBFS (-4,9) | -23,1 dB | -42,3 dB | 3,6 ms |
| `SFX_NutDrop_Catch_03` (baie, do) | 0,342 s | 0,21 s | -21,0 (-21) | -7,3 dBFS (-7,3) | -16,7 dB | -31,6 dB | 12 ms |
| `SFX_NutDrop_Golden_01` | 0,881 s | 0,76 s | -20,0 (-20) | -8,4 dBFS (-8,4) | -13,5 dB | -26,5 dB | 3,5 ms |
| `SFX_NutDrop_Pinecone_01` (do) | 0,290 s | 0,22 s | -23,0 (-23) | -6,2 dBFS (-6,2) | -13,2 dB | -39,7 dB | 4,3 ms |
| `SFX_NutDrop_Pinecone_02` (si bémol) | 0,288 s | 0,20 s | -23,0 (-23) | -5,0 dBFS (-5,0) | -13,9 dB | -39,6 dB | 5,4 ms |
| `SFX_NutDrop_Special_01` | 1,244 s | 1,14 s | -20,0 (-20) | -8,8 dBFS (-8,8) | -8,4 dB | -24,9 dB | 195 ms (le souffle) |

Tous à leur cible. Les prises et la pomme de pin sont courtes et percutantes : leur crête est plus haute que celle des
sons tenus de T39 pour le même niveau, sous le plafond de -1 dBFS. Repères de la famille (pire trame de 2 à 5 kHz,
puis au-dessus de 6 kHz) : brosse -7,9 à -9,3 et -31,8 à -34,1, nœud -13,3 et -24,8, loupe -4,3 et -24,5. Les prises
sont les plus douces de la famille dans ces bandes ; la noisette dorée se place comme le nœud ; le souffle du spécial,
d'abord monté jusqu'à 1,8 kHz (-5,6 dB), est descendu vers 1,5 kHz (-8,4 dB).

Partie de 12 s simulée (le service imité : délais, voix, variantes, variations de niveau et de hauteur des
définitions ; le choix des sons de `NutDropAudio`), sous « Pattes pressées » à son niveau par défaut et l'ambiance de
la prairie à -6 dB, la musique 5 dB plus bas à la fin :

| Moment | Effets | Beds |
| --- | --- | --- |
| Prise d'une noisette, d'un gland, d'une baie | -21,4, -20,7, -20,8 | -24,7, -24,7, -22,2 |
| Cinquième prise et premier pas de série | -18,8 | -25,4 |
| Pomme de pin | -23,3 | -23,0 |
| Noisette dorée | -20,2 | -22,1 |
| Objet spécial (avec un pas de série) | -19,7 | -22,5 |
| Averse dorée (cinq noisettes dorées en 1,7 s) | -17,8 | -21,2 |
| Accord de fin | -18,0 | -24,3 |

Par tiers d'octave, la noisette passe 13 dB au-dessus des beds vers 500 Hz (le bois de sa coque), la baie 2 à 3 dB
vers 1 et 3,2 kHz, la pomme de pin 3 à 7 dB entre 2 et 3,2 kHz et vers 500 Hz, la noisette dorée 19 et 22 dB vers
1,25 et 4 kHz, le spécial 12 à 17 dB de 500 Hz à 4 kHz. Ils s'entendent sans couvrir la musique. Deux objets qui
tombent à 20 ms l'un de l'autre font une seule prise (délai de 0,04 s). La crête de tous les effets de la partie reste
à -4 dBFS.

## Les sons

### Prise (`SFX_NutDrop_Catch_01` à `_03`, J-SFX-ACT-003-01)

- Intention : un pop boisé quand l'objet tombe dans le panier, sa hauteur selon l'objet.
- Recette : un pop rond qui monte dans sa note en 12 ms, sur le coup de sa coque une quarte plus bas (un petit bloc de
  bois), un murmure d'osier dessous. La noisette sonne creux (l'air de sa coque ronde une octave sous le pop), le gland
  un peu plus sec avec le petit coup de sa cupule juste après, la baie plus ronde et plus douce (plus de pop, moins de
  bois). Attaque arrondie sur 2 ms.

### Noisette dorée (`SFX_NutDrop_Golden_01`, J-SFX-ACT-003-02)

- Intention : la « cloche de verre » du doc, faite comme une petite tasse de verre de la boîte à musique, chaude et
  courte : jamais une cloche.
- Recette : le pop boisé de la noisette sur fa, puis la, do et fa roulés vite vers le haut (32 ms entre les notes), chacun
  sur une lame de kalimba une octave plus bas, le fa laissé sonner un peu. Une averse dorée (une toutes les 0,4 s)
  reste légère : deux voix, chaque tintement sous -40 dB en 0,76 s.

### Pomme de pin (`SFX_NutDrop_Pinecone_01`, `_02`, J-SFX-ACT-003-03)

- Intention : un « bonk » feutré, doux, jamais une punition.
- Recette : une mailloche de feutre sur une petite boîte de bois (une note ronde qui descend un peu dans sa hauteur en
  12 ms : le bonk), le souffle du feutre, les écailles sèches de la pomme qui frôlent l'osier en un murmure, puis deux
  petites feuilles qui tombent (H-VFX-ACT-003-03). Do ou si bémol. Aucun buzzer, aucune mélodie qui descend ; sous
  3 kHz.

### Objet spécial (`SFX_NutDrop_Special_01`, J-SFX-ACT-003-05)

- Intention : un souffle et un accord quand le panier large, la plume ou l'averse dorée commence.
- Recette : un souffle d'air dont la couleur monte pendant que l'anneau doux s'ouvre autour du panier
  (H-VFX-ACT-003-04), puis l'accord de fa majeur qui en sort : une nappe douce (fa, la, do) et trois lames roulées vers
  le haut, la, do, fa, le verre du fa murmuré au-dessus. Aérien là où l'accord de récompense est gratté.

## Grille de la charte « fait main »

Les points 1 à 37, 39 et 40 portent sur les formes, la lumière, l'animation et les icônes : sans objet pour un son.

| Point | Prises, pomme de pin | Noisette dorée | Objet spécial |
| --- | --- | --- | --- |
| 3. Chaque détail a une fonction écrite | vert : pop, coque, cupule, osier ; feutre, écailles, feuilles | vert : pop, trois notes de verre, lames | vert : souffle, nappe, lames, verre |
| 38. Aucune étoile à quatre branches | vert | vert | vert |
| 41. Source éditable, export reproductible | vert : master 24 bits et recette ; rendu trois fois, identique à l'octet | vert | vert |
| 42. Aucun générateur par IA (12.1) | vert : synthèse maison | vert | vert |
| 43. Vocabulaire | vert : noms et textes sans mot interdit | vert : boîte à musique, verre ; aucune cloche | vert |
| 44. Fait par la même main (13.5) | vert : pop du débris, bois de la pause, feuilles du cache-cache | vert : verre et lames du thème, fa majeur | vert : souffle de la loupe, lames de l'accord de fin |

## Intégration (côté Unity)

- `AudioEventId` reçoit `NutDropCatch = 84`, `NutDropGolden = 85`, `NutDropPinecone = 86`, `NutDropSpecial = 87` ;
  `AudioPipeline.Events()` les définit (prise : récompenses, 0,04 s, trois voix, 1,5 % de hauteur ; noisette dorée :
  récompenses, 0,1 s, deux voix ; pomme de pin : monde, 0,15 s, deux voix ; spécial : récompenses, 0,5 s, une voix).
- `NutDropAudio.Play(audio, in e, position)` donne le son de chaque événement du jeu ; la scène le lui passe pour
  chaque événement vidé. `ActivityAudio.HasOwnStreakSound` compte maintenant la Pluie de noisettes.
- Lancer « Pocket Wilds/Audio/Build Audio and Haptics (T34B) » : les nouveaux fichiers prennent les réglages d'import
  (mono, ADPCM jusqu'à 1,2 s, Vorbis pour le spécial) et entrent dans `DEF_AudioEvents`. Les `.meta` sont créés par
  Unity à l'import.
- Écoute : `T40_ecoute.mp3` (chaque son, cinq prises qui finissent sur le premier pas de série, puis une partie de
  12 s simulée sous la musique).
