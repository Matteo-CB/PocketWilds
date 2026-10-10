# T43 : revue des sons du Jardin

10 octobre 2026. Les sons du Jardin (doc 01 Contenu, S-ACT-006 « Sons et effets » ; doc Audio, J-SFX-ACT-006-01 à 04)
et la récolte du doc 05 Sensations (fiche 24, section 6.1, J-SFX-FEEL-017). Sept fichiers, tous synthétisés par le code
du dépôt. Le niveau du Jardin rejoue J-SFX-REWARD-002 (`SFX_Reward_MasteryUp`, le motif de maîtrise) : aucun son nouveau.

## Comment c'est fait et mesuré

- Recettes : `SourceArt/Audio/Tools/pw_audio_sfx.py`, section « T43 » (`garden_plant`, `garden_water`, `garden_stage`,
  `world_harvest`, avec `_soil`, `_pat`, `_drip`), sur les instruments de la maison (`_moving_band`, `_leaf_grains`,
  `_flutter`, `_rise_fall`, `_pop`, `_tine_under`). Aucun échantillon, aucun générateur, rien de téléchargé.
- Rendu : `pw_audio_build.py sfx SFX_Garden_Plant SFX_Garden_Water SFX_Garden_Stage SFX_World_Harvest`. Masters 48 kHz
  24 bits mono dans `SourceArt/Audio/Masters/SFX/`, fichiers du jeu 48 kHz 16 bits mono dans `Assets/_Game/Art/Audio/SFX/`
  (les .meta viennent de Unity).
- Reproductible : un second rendu dans un dossier de travail donne les 7 fichiers et les 7 masters identiques à l'octet.
  Les sons existants ne changent pas (la section s'ajoute avant le catalogue, aucune recette existante n'est touchée).
- Mesures (comme en T37 à T42) : niveau sur les 200 ms les plus fortes (pondération K, échelle LUFS), crête et vraie
  crête, point sous -40 dB, part de 2 à 5 kHz et au-dessus de 6 kHz dans la trame de 20 ms la plus forte, premier et
  dernier échantillon à 0, niveau gardé par un haut-parleur de téléphone (filtre de 12 dB par octave sous 550 Hz).

## Identifiants

| Son | `AudioEventId` | Fichiers | Groupe, voix, délai |
| --- | --- | --- | --- |
| J-SFX-FEEL-017 et J-SFX-ACT-006-04 récolte | `GardenHarvest` = 46 | `SFX_World_Harvest_01` à `_03` | Monde, 2, 0,1 s (doc 05) |
| J-SFX-ACT-006-01 planter | `GardenPlant` = 96 | `SFX_Garden_Plant_01`, `_02` | Monde, 1, 0,2 s |
| J-SFX-ACT-006-02 arroser | `GardenWater` = 97 | `SFX_Garden_Water_01` | Monde, 1, 1 s |
| J-SFX-ACT-006-03 stade suivant | `GardenStage` = 98 | `SFX_Garden_Stage_01` | Monde, 2, 0,15 s |
| J-SFX-REWARD-002 niveau du Jardin | `GardenLevelUp` = 99 | `SFX_Reward_MasteryUp_01` (existant) | Récompenses, 1, 1 s |

`GardenHarvest` prend la place que le doc 05 (section 6.4) réserve à Harvest dans l'ordre de la liste (46, après
ExpeditionReturn), comme ExpeditionReturn (45) en T42. `GardenAudio` (dans `FeedbackEvents.cs`) dit où jouer chaque son
et donne la hauteur de la récolte : `HarvestPitch(chain)` monte la pentatonique de fa (0, 2, 4, 7, 9 demi-tons : fa,
sol, la, do, ré) pour les parcelles enchaînées (« Tout récolter » à 120 ms d'écart, ou une parcelle récoltée moins de
`ChainSeconds` = 1,5 s après la précédente), et reste sur ré au-delà de la cinquième (doc 05, section 6.4 : une série
bornée qui monte). Le pop de la récolte est sur fa dans les trois variantes, donc chaque marche reste dans le ton.

## Choix faits (les docs se taisent)

- Planter : terre douce. La patte enfonce la graine (un appui feutré et les miettes de terre qui se referment), puis
  tapote deux fois, le second tapotement plus léger. Brun et chaud, rien au-dessus de 3 kHz, aucune note. Deux variantes
  de tapotements et de miettes, pour qu'une parcelle plantée après une autre ne répète pas le même son. 0,4 s.
- Arroser : un filet d'eau d'une seconde. Un souffle d'eau doux dont la couleur ondule comme le jet qui tremble, et les
  petites gouttes qu'il fait sur la terre et les feuilles : de minuscules plics ronds qui montent vers les notes de la
  pentatonique de fa, une douzaine par seconde au début, plus rares quand l'arrosoir se relève, puis deux dernières
  gouttes. Mouillé, jamais un sifflement : sous 4,5 kHz.
- Stade suivant : un petit pop végétal. Les feuilles se déplient (un minuscule froissement qui monte), puis le bouton
  s'ouvre : un pop rond qui monte vers la, un second plus léger une quarte au-dessus, une lame douce une octave sous le
  premier pour la chaleur qu'un téléphone garde. Sous la récolte : des parcelles qui rattrapent leur pousse à
  l'ouverture du Jardin font un ou deux pops (délai 0,15 s, 2 voix).
- Récolte : un plop de terre et un froissement de feuilles (doc 05). Les racines lâchent la terre avec un plop rond (un
  corps grave qui remonte quand la terre cède, le souffle de la terre remuée), les feuilles frémissent pendant que la
  plante saute (petits grains arrondis, un souffle court, sous 3,2 kHz), puis un pop chaud sur fa avec une lame une
  octave dessous. Trois variantes de plop et de froissement. 0,3 s.
- Niveaux : récolte -20 (doc 05) ; planter -22, en dessous ; stade suivant -23 ; le filet d'eau, une seconde de long, le
  plus doux à -24. Tous dans le groupe Monde, joués du centre (le décor du Jardin est un gros plan), sans variation de
  hauteur sur la récolte (la chaîne choisit sa hauteur).

## Résultats

| Fichier | Durée | Sous -40 dB à | Niveau (cible) | Crête | 2 à 5 kHz, trame forte | Au-dessus de 6 kHz | Téléphone |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `SFX_Garden_Plant_01` | 0,50 s | 0,38 s | -22,0 (-22) | -7,3 dBFS | -20,2 dB | -45,2 dB | -2,9 dB |
| `SFX_Garden_Plant_02` | 0,48 s | 0,38 s | -22,0 (-22) | -6,3 dBFS | -17,3 dB | -42,3 dB | -2,6 dB |
| `SFX_Garden_Water_01` | 1,33 s | 1,22 s | -24,0 (-24) | -12,0 dBFS | -11,1 dB | -37,0 dB | -0,7 dB |
| `SFX_Garden_Stage_01` | 0,45 s | 0,34 s | -23,0 (-23) | -9,7 dBFS | -37,8 dB | -60,2 dB | -1,2 dB |
| `SFX_World_Harvest_01` | 0,41 s | 0,32 s | -20,0 (-20) | -5,6 dBFS | -9,0 dB | -35,4 dB | -1,8 dB |
| `SFX_World_Harvest_02` | 0,41 s | 0,32 s | -20,0 (-20) | -5,4 dBFS | -10,8 dB | -34,4 dB | -2,2 dB |
| `SFX_World_Harvest_03` | 0,41 s | 0,32 s | -20,0 (-20) | -4,3 dBFS | -13,6 dB | -39,0 dB | -2,1 dB |

Tous à leur cible, vraie crête égale à la crête (à 0,1 dB près), premier et dernier échantillon à 0. Le filet d'eau
dépasse 1,2 s : Unity le garde en Vorbis en mémoire (règle de l'`AudioPipeline`), les autres en ADPCM. Repères de la
famille (au-dessus de 6 kHz) : carte postale -34,9, départ -37,7. Deux premiers essais trop clairs ont été adoucis : la
récolte (-19,5 à -24 dB au-dessus de 6 kHz, froissement ramené sous 3,2 kHz, filtre d'ordre 3) et le filet d'eau (-32,1,
filtre à 4,5 kHz).

## Grille de la charte « fait main »

Les points 1 à 37, 39 et 40 portent sur les formes, la lumière, l'animation et les icônes : sans objet pour un son.

| Point | Planter | Arroser | Stade suivant | Récolte |
| --- | --- | --- | --- | --- |
| 38. Aucune étoile à quatre branches | sans objet | sans objet | sans objet | sans objet |
| 41. Source éditable, export reproductible | vert : recette et master ; rendu identique | vert | vert | vert |
| 42. Aucun générateur par IA (12.1) | vert : synthèse du dépôt | vert | vert | vert |
| 43. Vocabulaire | vert | vert | vert | vert |
| 44. Fait par la même main (13.5) | vert : feutre des pas de T42 | vert : gouttes en fa majeur | vert : pop de T38 à T42, lame de la maison | vert : feuilles de T39, pop de T40 |

## À écouter dans Unity

Jouer un « Tout récolter » de cinq parcelles (120 ms d'écart) pour entendre la montée fa, sol, la, do, ré sous la
vibration unique ; vérifier que le filet d'eau (1 s) couvre les six gouttes de H-VFX-ACT-006-01 (trois paires à 0,1 s).
