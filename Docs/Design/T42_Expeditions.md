# T42 Expéditions

But (file de production, T42) : la première activité « méta », S-ACT-005 du doc « Mini-jeux & activités » et du doc 01
Contenu (section 7 et S-ACT-005). Un minuteur avec son propre écran, sans billet. Critère : minuteurs justes hors ligne,
cartes postales générées, rien de perdu en cas de retour tardif.

## Ce que le joueur vit

- **Ouverture.** Au niveau 5 du refuge et avec 8 animaux. La carte Expéditions du hub s'allume, marquée « Nouveau » ; le
  bouton Jouer porte un point.
- **L'écran.** Une carte dessinée avec chaque destination ouverte (aujourd'hui les Prés fleuris, avec sa propre image)
  et les biomes suivants en silhouettes douces avec un cadenas. En dessous, les 4 emplacements :
  - libre : « Choisis ton équipe » ;
  - en route : les portraits de l'équipe, la destination, un anneau doux, « De retour dans 3 h 12 », « Finir maintenant »
    avec son prix en Gemmes ;
  - de retour : l'emplacement brille, « Voir la carte postale » ;
  - fermé : « Nouvel emplacement » (800 Gemmes, deux touches pour confirmer) ou « Avec le Wild Club ».
- **Préparer un départ.** La destination, puis l'équipe : 1 à 3 animaux libres, avec leurs portraits. La note d'équipe
  s'affiche en direct (étoiles et raisons : animaux du biome, famille préférée, trois espèces), avec le bouton « Équipe
  conseillée ». Puis la durée (1 h, 4 h, 8 h, 12 h) avec l'aperçu du butin, et « Partir ». Les animaux quittent le refuge
  (ils continuent de produire).
- **Le retour** (doc 05 fiche 23). Toucher l'emplacement :
  - l'écran s'efface, l'équipe arrive à pied du côté de la vue et saute de joie dans un petit nuage de poussière ;
  - la carte postale pivote sur le monde assombri, avec un anneau doux et 6 pétales ronds ;
  - le butin se retourne objet par objet, une note plus haute à chaque objet ;
  - « Repartir » rouvre la préparation avec la même équipe ; « Tout réclamer » quand plusieurs équipes sont rentrées.
- **La carte postale** est une vraie image de l'équipe dans le décor de la destination (512 × 768) : aux Prés fleuris,
  un pique-nique sur une couverture vichy au pied d'une colline de pâquerettes, papillons blancs autour. Elle est
  dessinée une seule fois, au retour, et gardée pour le journal du refuge (T44).
- **Tutoriel A18 et A19.** Une patte sur la carte du hub, la destination, un animal, « Partir » ; la toute première
  expédition dure 3 min (« Première balade : 3 min ») et rapporte le butin d'1 h. À son retour, l'emplacement brille.

## Règles (simulation)

- **Minuteurs** : l'heure de départ et l'heure de retour sont des heures UTC absolues dans la sauvegarde. Ils avancent
  jeu fermé. Une équipe rentrée attend sans limite. Une horloge reculée ne rend jamais une attente plus longue que sa
  durée.
- **Butin tiré au départ** avec la graine de l'expédition, rangé dans la sauvegarde : autocollant, œuf Découverte,
  graines, ingrédient. Le retour ne fait que le montrer. Une accélération ne tire rien. La Lumière (minutes de
  production) est payée au retour avec la production du moment.
- **Note d'équipe** : 10 par animal, +10 par animal du biome de la destination, +10 avec la famille préférée, +10 pour
  trois espèces. 1 étoile sous 40, 2 étoiles dès 40, 3 étoiles dès 60, avec le bonus de butin de la section 7.2.
- **Butin commun** (section 7.3) plus la spécialité de la destination ; 5 points de lien par animal, 15 XP de maîtrise
  du biome, 2 XP de maîtrise par espèce de l'équipe (doc 08).
- **Garantie** : un œuf Découverte au plus tard à la 12e expédition de 8 h ou plus sans œuf.
- **Accélération** : 1 Gemme par tranche de 6 min commencée, 5 au moins, 3 par jour.
- **Emplacements** : 2, +1 contre 800 Gemmes, +1 avec le Wild Club.
- **Défis** : les 10 défis du doc ; la note d'équipe donne les étoiles, enregistrées au départ.
- **Destinations** : les 13 de biome et les 24 d'événement sont au catalogue. Seuls les Prés fleuris sont ouverts ; une
  destination de biome s'ouvre avec son biome, une destination d'événement avec son événement (T48 et après).
- **Souhaits et carnet** : WISH-041 à 046 (nouveau paramètre {destination}), JRN-024 à 026, les étoiles comptent pour
  JRN-009. Un animal parti n'exprime pas de souhait et ne joue à aucun mini-jeu.
- **Analytique** : événements 140 à 142 (départ, retour, accélération), 112 pour les défis.

## Décisions prises (le propriétaire peut les changer)

1. **Les chances d'autocollant suivent le doc 01** (10, 25, 45, 60 %) ; le doc 08 propose 15, 40, 70, 100 %.
2. **Une seule pioche Miel doux ou Crème vanille** pour tout le lot d'ingrédients.
3. **La carte postale est dessinée en jeu** par sa propre caméra, lue sans bloquer l'image et écrite en PNG hors du fil
   principal ; on garde la première de chaque destination pour toujours et les 24 plus récentes.
4. **Toutes les destinations sans décor propre utilisent celui des Prés fleuris** jusqu'à leur biome.
5. **Acheter l'emplacement à 800 Gemmes demande deux touches** en 3 s : un doigt qui glisse ne dépense rien.
6. **Pas encore** : notifications locales et carte de permission (T55), journal du refuge (T44), albums (T44 : les
  autocollants restent un compte), week-end des Expéditions (un réglage seulement).

## Fait main, sons et images

- Modèles Blender : couverture vichy, panier de pique-nique, colline de pâquerettes, papillons blancs, sol de prairie
  (`SourceArt/Blender/Activities/BL_Activity_Postcards.blend`, `pw_postcards.py`), revue
  `Docs/Captures/Review/T42_props_review.md`.
- Sons SFX_Expedition_* (ids 92 à 95) et SFX_Reward_ExpeditionReturn_01 (id 45), revue `T42_sounds_review.md`.
- Dessins par notre code (`pw_ui_sprites_t42.py`) : papier de carte, emplacements, anneau, durées, cadre de carte postale,
  timbre, cachet, pétale, raisons de la note ; revue `T42_sprites_review.md`.

## Performance

- 0 allocation par frame écran ouvert ; minuteurs relus de l'horloge, textes changés seulement quand ils changent.
- L'écran et son atlas (24 sprites) se chargent à l'ouverture et se libèrent à la fermeture ; le décor de carte postale
  (10 331 triangles) se charge au premier retour.
