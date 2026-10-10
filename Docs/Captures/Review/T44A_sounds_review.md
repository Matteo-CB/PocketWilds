# T44A : revue des sons des albums

10 octobre 2026. Les deux emplacements que le doc 05 (section 6.4) réserve : 43 Sticker et 44 AlbumPage. Synthétisés par
le code du dépôt, aucun échantillon, aucun générateur.

## Comment c'est fait et mesuré

- Recettes : `SourceArt/Audio/Tools/pw_audio_sfx.py`, section « T44A » (`reward_sticker`, `reward_album_page`), sur les
  instruments de la maison (`_moving_band`, `_flutter`, `_rise_fall`, `_pop`, `_tine_under`, `mallet`, `damped_sine`).
- Rendu : `pw_audio_build.py sfx SFX_Reward_Sticker_01 SFX_Reward_AlbumPage_01`. Masters 48 kHz 24 bits dans
  `SourceArt/Audio/Masters/SFX/`, fichiers du jeu 48 kHz 16 bits mono dans `Assets/_Game/Art/Audio/SFX/` (les .meta viennent
  de Unity). Les sons existants ne changent pas (la section s'ajoute avant le catalogue).
- Mesures : premier et dernier échantillon à 0 ; moins de 1 % de l'énergie entre 2 et 5 kHz, rien au-dessus de 6 kHz ;
  crête à -5,8 dB (sticker) et -5,0 dB (page).

## Identifiants

| Son | `AudioEventId` | Fichier | Groupe, voix, délai | Niveau cible |
| --- | --- | --- | --- | --- |
| Autocollant gagné | `Sticker` = 43 | `SFX_Reward_Sticker_01` | Récompenses, 1, 0,4 s | -20 |
| Page d'album tournée | `AlbumPage` = 44 | `SFX_Reward_AlbumPage_01` | Récompenses, 1, 0,9 s | -21 |

## Choix faits

- Sticker, 0,56 s fichier (silence sous -40 dB à 0,48 s) : le décollage du papier (un souffle dont la bande monte pendant
  0,17 s), puis, quand il est pressé, un petit tap de papier et un pop rond qui monte vers la, une tine de bois une octave
  plus bas pour la chaleur d'un téléphone.
- Page, 0,96 s fichier (sous -40 dB à 0,93 s) : l'air d'une page épaisse (souffle qui monte puis se pose), la page posée
  (un coup feutré), puis deux notes de bois chaudes, fa puis la, la seconde un peu plus longue, sur deux tines.
  Pentatonique de fa comme le reste du jeu ; ni verre, ni cloche.
- Câblage : `AudioEventId.Sticker` et `AlbumPage` ajoutés dans `FeedbackEvents.cs` (à la place des deux lignes réservées),
  `Def(...)` dans `AudioPipeline.cs`. À jouer avec `audio.Play(AudioEventId.Sticker, Vector3.zero, ContentId.None)` ;
  plusieurs autocollants d'un tirage se jouent l'un après l'autre (0,4 s mini) ; au-delà, pas de son par autocollant.
