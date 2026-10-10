# T42 : revue des sons des Expéditions

10 octobre 2026. Les sons des Expéditions (doc 01 Contenu, S-ACT-005 « Sons et effets » ; doc Audio, J-SFX-ACT-005-01
à 04) et le retour d'expédition du doc 05 Sensations (fiche 23, section 6.1, J-SFX-FEEL-016). Douze fichiers, tous
synthétisés par le code du dépôt.

## Comment c'est fait et mesuré

- Recettes : `SourceArt/Audio/Tools/pw_audio_sfx.py`, section « T42 » (`expedition_depart`, `expedition_back`,
  `expedition_postcard`, `expedition_loot`, `expedition_return`, avec `_paw_step`, `_trot`, `_paper_slide`,
  `_shop_bell`), sur les instruments de la maison (`_moving_band`, `_leaf_grains`, `_flutter`, `_wood_knock`, `_pop`,
  `_tine_under`, `glass`, `mallet`). Aucun échantillon, aucun générateur, rien de téléchargé.
- Rendu : `pw_audio_build.py sfx SFX_Expedition_Depart SFX_Expedition_Back SFX_Expedition_Postcard SFX_Expedition_Loot
  SFX_Reward_ExpeditionReturn`. Masters 48 kHz 24 bits mono dans `SourceArt/Audio/Masters/SFX/`, fichiers du jeu 48 kHz
  16 bits mono dans `Assets/_Game/Art/Audio/SFX/` (les .meta viennent de Unity).
- Reproductible : un second rendu dans un dossier de travail donne les 12 fichiers identiques à l'octet. Les sons
  existants ne changent pas (la section s'ajoute avant le catalogue ; quatre sons d'avant T42 rendus à part avec le
  script modifié sont identiques à l'octet).
- Mesures (comme en T37 à T41) : niveau sur les 200 ms les plus fortes (pondération K, échelle LUFS), crête et vraie
  crête, point sous -40 dB, part de 2 à 5 kHz et au-dessus de 6 kHz dans la pire trame de 20 ms, premier et dernier
  échantillon à 0, niveau gardé par un haut-parleur de téléphone (filtre de 12 dB par octave sous 550 Hz).

## Identifiants

| Son | `AudioEventId` | Fichiers | Groupe, voix, délai |
| --- | --- | --- | --- |
| J-SFX-ACT-005-01 départ | `ExpeditionDepart` = 92 | `SFX_Expedition_Depart_01` | Interface, 1, 1 s |
| J-SFX-ACT-005-02 retour | `ExpeditionBack` = 93 | `SFX_Expedition_Back_01` | Récompenses, 1, 1 s |
| J-SFX-ACT-005-03 carte postale | `ExpeditionPostcard` = 94 | `SFX_Expedition_Postcard_01` | Interface, 2, 0,25 s |
| J-SFX-ACT-005-04 objet du butin | `ExpeditionLoot` = 95 | `SFX_Expedition_Loot_01` à `_08` | Récompenses, 2, 0,1 s |
| J-SFX-FEEL-016 retour d'expédition | `ExpeditionReturn` = 45 | `SFX_Reward_ExpeditionReturn_01` | Récompenses, 1, 1 s (doc 05) |

`ExpeditionReturn` prend la place que le doc 05 (section 6.4) lui réservait dans l'ordre de la liste (45, entre
AlbumPage et Harvest), comme CardFlip (41), StarEarned (42) et BubblePuff (49) avant lui ; il n'existait pas encore.
`ExpeditionAudio` (dans `FeedbackEvents.cs`) dit où jouer chaque son et choisit la marche du pop (`LootVariant`).

## Choix faits (les docs se taisent)

- Départ : le petit sac de toile se ferme (le cordon tiré dans la toile, un froissement qui monte ; le rabat posé, un
  pouf feutré et un froissement de toile ; un toucher très léger de son bouton de bois), puis six petits pas de patte
  sur l'herbe qui s'éloignent, chaque pas plus doux et plus sourd (l'herbe les cache). 1 s.
- Retour : une petite clochette de porte de boutique. Elle se balance sur son ressort et sonne trois fois (do, un
  cheveu plus bas au retour du battant, do plus doux), une seconde petite clochette sur le fa dessous sonne avec elle,
  et le ressort berce la fin. Partiels courts et chauds. Joué quand un emplacement passe à « De retour » écran ouvert.
- Carte postale : une carte de papier épais glissée sur du papier (la couleur du souffle monte pendant qu'elle glisse,
  le grain du papier très discret), puis posée à plat (un toucher sourd de carte sur carte). Sous le pivot de 400 ms.
- Objet du butin : un pop chaud, une marche plus haut par objet, sur la pentatonique de fa : fa, sol, la, do, ré (les
  cinq notes de la carte de récompense de T37, pour que la carte et le pop sonnent ensemble), puis fa, sol, la une
  octave plus haut. Une lame douce une octave dessous donne le corps qu'un téléphone garde. Au-delà du huitième
  objet, la marche du haut.
- Retour d'expédition (J-SFX-FEEL-016) : sept petits pas feutrés pressés qui s'approchent (les écarts raccourcissent,
  plus forts et plus clairs à mesure), la carte postale tirée du sac (un court glissement de papier), puis deux notes
  de bois, do puis le fa au-dessus (la quarte qui rentre à la maison). Lancé au début de l'accueil, pendant que
  l'équipe arrive : les deux notes tombent à 0,6 et 0,7 s, juste avant le pivot de la carte à 1,2 s.
- Niveaux : départ -21 (interface, au-dessus de la confirmation à -22 : le sac et les pas sont plus doux), clochette
  -20, glissement de la carte -22 (un peu au-dessus de la carte de récompense, -23), pop -21, retour d'expédition -18
  (doc 05).

## Résultats

| Fichier | Durée | Sous -40 dB à | Niveau (cible) | Crête | 2 à 5 kHz, pire trame | Au-dessus de 6 kHz | Téléphone |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `SFX_Expedition_Depart_01` | 1,10 s | 1,00 s | -21,0 (-21) | -3,8 dBFS | -12,1 dB | -37,7 dB | -2,5 dB |
| `SFX_Expedition_Back_01` | 1,13 s | 1,03 s | -20,0 (-20) | -14,4 dBFS | -10,9 dB | -40,7 dB | -0,5 dB |
| `SFX_Expedition_Postcard_01` | 0,60 s | 0,47 s | -22,0 (-22) | -10,3 dBFS | -6,1 dB | -34,9 dB | -0,5 dB |
| `SFX_Expedition_Loot_01` à `_08` | 0,44 s | 0,33 à 0,37 s | -21,0 (-21) | -6,8 à -8,4 dBFS | -19,9 à -26,3 dB | -43,4 à -45,3 dB | -2,7 à -0,3 dB |
| `SFX_Reward_ExpeditionReturn_01` | 1,38 s | 1,27 s | -18,0 (-18) | -5,4 dBFS | -15,1 dB | -46,0 dB | -1,8 dB |

Tous à leur cible, vraie crête égale à la crête, premier et dernier échantillon à 0. Repères de la famille (pire
trame de 2 à 5 kHz) : brosse -9,3, froissement du Memory -7,0 à -10,4, carte de récompense -5,7. Deux premiers essais
trop clairs ont été adoucis : le cordon du départ (-3,3, bande ramenée sous 2,4 kHz) et le glissement de la carte
(-3,1, souffle plus bas, filtre à 3,2 kHz).

## Grille de la charte « fait main »

Les points 1 à 37, 39 et 40 portent sur les formes, la lumière, l'animation et les icônes : sans objet pour un son.

| Point | Départ | Clochette | Carte postale | Pop du butin | Retour d'expédition |
| --- | --- | --- | --- | --- | --- |
| 38. Aucune étoile à quatre branches | sans objet | sans objet | sans objet | sans objet | sans objet |
| 41. Source éditable, export reproductible | vert : recette et master ; rendu identique | vert | vert | vert | vert |
| 42. Aucun générateur par IA (12.1) | vert : synthèse du dépôt | vert | vert | vert | vert |
| 43. Vocabulaire | vert | vert : clochette de boutique | vert | vert | vert |
| 44. Fait par la même main (13.5) | vert : pas et toile comme la brosse et le Memory | vert : verre de la maison | vert : comme le froissement du Memory | vert : pop de T38 à T40, notes de la carte de T37 | vert : marimba de la maison, fa majeur |

## À écouter dans Unity

Jouer `SFX_Reward_ExpeditionReturn_01` avec l'arrivée de l'équipe (1,2 s) pour caler son départ ; vérifier que le pop
du butin et la carte de récompense (si les deux sont gardés) sonnent comme une seule note.
