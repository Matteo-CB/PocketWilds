# T41 : revue des sons du Memory des empreintes

10 octobre 2026. Les sons du Memory des empreintes (doc 01 Contenu, S-ACT-004 « Sons et effets » ; doc Audio,
J-SFX-ACT-004-01 à 04). Treize fichiers, tous synthétisés par le code du dépôt.

## Comment c'est fait et mesuré

- Recettes : `SourceArt/Audio/Tools/pw_audio_sfx.py`, section « T41 » (`memory_flip`, `memory_match`, `memory_miss`,
  `memory_glow`), avec les instruments de la maison (`_moving_band`, `_leaf_grains`, `_flutter`, `_tine_under`,
  `_glass_note`, `_small_chime`, `_resonance`). Aucun échantillon, aucun générateur, rien de téléchargé.
- Rendu : `pw_audio_build.py sfx SFX_Memory_Flip SFX_Memory_Match SFX_Memory_Miss SFX_Memory_Glow`. Masters 48 kHz
  24 bits mono dans `SourceArt/Audio/Masters/SFX/`, fichiers du jeu 48 kHz 16 bits mono dans `Assets/_Game/Art/Audio/SFX/`.
- Sons existants : les 250 fichiers d'avant T41 (sons, ambiances, musiques, masters et fichiers du jeu), rendus à part
  avec le script modifié, sont identiques à l'octet ; aucun n'a été réécrit. Les 13 nouveaux, rendus trois fois, sont
  identiques à l'octet.
- Mesures (comme en T37 à T40) : niveau sur les 200 ms les plus fortes (pondération K, échelle LUFS), crête et vraie
  crête, point sous -40 dB, part de 2 à 5 kHz et au-dessus de 6 kHz dans la pire trame de 20 ms, attaque, premier et
  dernier échantillon à 0. En plus : le niveau gardé par un haut-parleur de téléphone (filtre de 12 dB par octave
  sous 550 Hz).

## Choix faits (les docs se taisent)

- Retourner : un froissement de carton souple, au plus fort 35 ms après le toucher, puis la carte posée sur la nappe
  (un toucher de tissu, ni coup ni clic). Aucune note : la tierce de la paire reste la seule mélodie du jeu. Trois
  variantes. Aussi pour l'aperçu (toutes les cartes se retournent puis reviennent), les deux cartes d'une erreur qui
  se retournent et l'échange de deux cartes du défi 6 : un froissement par événement.
- Paire : deux notes en tierce à la boîte à musique (lame de kalimba et verre), la basse puis la haute 75 ms après.
  La tierce monte la gamme de fa majeur avec les paires trouvées : huit marches, fa et la jusqu'à fa et la une octave
  plus haut (`SFX_Memory_Match_01` à `_08`). `MemoryAudio.MatchVariant` étale les paires d'une partie sur les marches :
  la dernière paire tombe toujours sur le fa du haut, sous l'accord de fin (6 paires : 0, 1, 3, 4, 6, 7 ; 8 paires : 0
  à 7 ; 10 paires : 0, 1, 2, 2, 3, 4, 5, 5, 6, 7). La paire double sonne pareil (ses 200 points font la différence).
- Pas de paire : une seule note feutrée, ré grave, une mailloche de feutre sur une petite boîte de bois. Elle se pose
  à peine dans sa hauteur, jamais un intervalle qui descend, jamais un buzzer, rien au-dessus de 2 kHz. Même son que
  l'erreur compte ou non. Son octave et sa douzième la portent sur un téléphone (6,1 dB perdus, comme l'erreur douce
  de l'interface, 6,3).
- Carte lueur : une petite tasse de verre chaude. La et do frappés ensemble (une tierce), le fa au-dessus juste après,
  une lame douce sur fa dessous, et le verre qui chante doucement pendant que le bord doré luit. Partiels courts, aucun
  partiel inharmonique : jamais une cloche.
- Niveaux : retourner -24 (interface, deux par essai, un peu au-dessus de la brosse), paire -20 (récompenses, sous
  l'étoile du résultat à -19), pas de paire -25 (le plus doux, sous le froissement qui l'accompagne), lueur -21.
- Silences : le bonus de fin et la fin de partie sont ceux du présentateur (l'accord de fin). Le pas de série partagé
  se tait pour cette activité (`ActivityAudio.HasOwnStreakSound` compte le Memory) : la tierce monte déjà.

## Résultats

| Fichier | Durée | Sous -40 dB à | Niveau (cible) | Crête | 2 à 5 kHz, pire trame | Au-dessus de 6 kHz | Attaque |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `SFX_Memory_Flip_01` à `_03` | 0,33 à 0,35 s | 0,21 à 0,22 s | -24,0 (-24) | -8,2 à -9,4 dBFS | -7,0 à -10,4 dB | -30,4 à -34,5 dB | 18 à 74 ms |
| `SFX_Memory_Match_01` à `_08` | 0,83 à 0,93 s | 0,79 à 0,81 s | -20,0 (-20) | -10,4 à -12,7 dBFS | -18,2 à -28,9 dB | -24,1 à -36,0 dB | 76 à 80 ms (la note haute) |
| `SFX_Memory_Miss_01` | 0,38 s | 0,35 s | -25,0 (-25) | -11,0 dBFS | -28,9 dB | -60,5 dB | 4,6 ms |
| `SFX_Memory_Glow_01` | 1,00 s | 0,85 s | -21,0 (-21) | -10,9 dBFS | -13,8 dB | -26,8 dB | 61 ms |

Tous à leur cible, vraie crête égale à la crête, premier et dernier échantillon à 0. Repères de la famille (pire
trame de 2 à 5 kHz, puis au-dessus de 6 kHz) : brosse -9,3 et -34,5, toucher vide du cache-cache -7,9 et -32,1, carte
de récompense -5,7 et -21,3. Le froissement se place entre la brosse et le toucher vide ; un premier essai, plus clair
(-5,0 et -19,6), a été adouci (grains de papier plus bas et plus discrets, filtre à 3,8 kHz).

Partie de 4 × 3 simulée (6 paires, aperçu de 2 s ; délais, voix, variantes, variations de niveau et de hauteur des
définitions ; le choix des sons de `MemoryAudio`), sous « Petite partie » à son niveau par défaut et l'ambiance de la
prairie à -6 dB, la musique 5 dB plus bas à la fin :

| Moment | Effets | Beds |
| --- | --- | --- |
| Première carte retournée | -23,9 | -21,5 |
| Deuxième carte et tierce (paire 1, fa la) | -18,7 | -22,0 |
| Deuxième carte et note feutrée | -21,2 | -24,5 |
| Les deux cartes se retournent | -24,4 | -25,9 |
| Carte lueur (froissement et verre) | -19,2 | -23,3 |
| Paire 4 (do mi), paire 6 (fa la du haut) | -18,5, -18,7 | -22,5, -27,2 |
| Accord de fin | -17,9 | -28,7 |

Par tiers d'octave, le froissement passe 13 à 27 dB au-dessus des beds de 1 à 3,2 kHz : il s'entend sous la musique
sans la couvrir. La crête de tous les effets de la partie reste à -6 dBFS.

## Grille de la charte « fait main »

Les points 1 à 37, 39 et 40 portent sur les formes, la lumière, l'animation et les icônes : sans objet pour un son.

| Point | Retourner | Paire | Pas de paire | Lueur |
| --- | --- | --- | --- | --- |
| 3. Chaque détail a une fonction écrite | vert : froissement, grains, toucher du tissu | vert : deux notes, lame, verre | vert : feutre, octave pour le téléphone | vert : tierce, fa, verre qui chante |
| 38. Aucune étoile à quatre branches | vert | vert | vert | vert |
| 41. Source éditable, export reproductible | vert : master 24 bits et recette ; rendu trois fois, identique à l'octet | vert | vert | vert |
| 42. Aucun générateur par IA (12.1) | vert : synthèse maison | vert | vert | vert |
| 43. Vocabulaire | vert | vert | vert | vert : boîte à musique, verre ; aucune cloche |
| 44. Fait par la même main (13.5) | vert : le souffle de la brosse, les grains des feuilles | vert : lames et verre des étoiles, fa majeur | vert : feutre de la pomme de pin | vert : verre de la noisette dorée |

## Intégration (côté Unity)

- `AudioEventId` reçoit `MemoryFlip = 88`, `MemoryMatch = 89`, `MemoryMiss = 90`, `MemoryGlow = 91` ;
  `AudioPipeline.Events()` les définit (retourner : interface, 0,05 s, deux voix, 4 % de hauteur ; paire : récompenses,
  0,1 s, deux voix, hauteur fixe ; pas de paire : interface, 0,3 s, une voix ; lueur : récompenses, 0,5 s, une voix).
- `MemoryAudio.Play(audio, in e, pairsFound, pairCount)` donne le son de chaque événement ; la scène le lui passe pour
  chaque événement vidé, avec `PairsFound` et `PairCount` du jeu.
- Lancer « Pocket Wilds/Audio/Build Audio and Haptics (T34B) » : les nouveaux fichiers prennent les réglages d'import
  (mono, ADPCM, tous sous 1,2 s) et entrent dans `DEF_AudioEvents`.
- Écoute : `T41_ecoute.mp3` (chaque son, les huit tierces qui montent, un essai réussi et un essai raté, puis une
  partie de 13 s simulée sous la musique).
