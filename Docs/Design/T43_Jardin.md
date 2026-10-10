# T43 Jardin

But (file de production, T43) : la deuxième activité « méta », S-ACT-006 du doc « Mini-jeux & activités » et du doc 01
Contenu (sections 8 et 9). Critère : pousse hors ligne exacte, récoltes qui alimentent Souhaits et teintures.

## Ce que le joueur vit

- **Ouverture.** Niveau 8 du refuge et jour 3 (le cadeau du jour 3 donne 5 graines de Trèfle doux). La carte Jardin du
  hub affiche « Nouveau », puis la prochaine récolte ou « {n} prêts ».
- **Le potager du refuge**, un décor à part comme le Toilettage : 5 carrés en bois le long d'une allée de pas japonais,
  une barrière basse, la Prairie derrière, un arrosoir et un panier. Les deux carrés fermés portent une bâche et un
  panneau (400 Gemmes en deux touches, ou le Wild Club).
- **Planter, arroser, récolter.** Toucher un carré libre ouvre les sachets de graines ; l'arrosoir retire 10 % du temps
  (une fois) ; une plante mûre se balance, saute et vole dans le panier ; « Tout récolter » enchaîne les carrés.
- **Étoiles de récolte** : 1 simple, 2 arrosée, 3 arrosée avec une voisine d'une autre espèce la moitié du temps (deux
  petits cœurs verts). Une récolte à 3 étoiles donne un produit de plus.
- **Niveaux du Jardin** 1 à 10 avec ce qu'ils ouvrent (plantes, teintures au niveau 4, graine rendue 75 puis 90 %,
  « Pousse heureuse » au niveau 7) et un bandeau « Jardin niveau {n} ».
- **Tutoriel** à la première visite : la patte sur un carré, le Trèfle doux, l'arrosoir, la pousse.

## Règles (simulation)

- Minuteurs en heures UTC absolues dans la sauvegarde (section 19, schéma 13) : la pousse avance jeu fermé, une plante
  mûre attend sans limite, une horloge reculée n'allonge jamais une pousse.
- Tirages à la plantation (graine rendue, « Pousse heureuse ») ; une accélération (1 Gemme par 10 min, 3 au moins, 3 par
  jour) ne tire rien.
- L'entraide se calcule exactement, jeu fermé compris : chaque pousse garde le temps passé à côté d'une voisine d'une
  autre espèce.
- Produits dans l'inventaire (ingrédients `ingredient.*`, autres `crop.*`), teintures dans le Jardin (dès le niveau 4),
  palettes de teinture achetables (5 teintures, duo 3 + 3) ; l'écran des teintures vient avec les tenues (T47).
- Souhaits WISH-031 à 036 (nouveau paramètre {crop}, qui porte son nombre : « Récolte 3 fraises »), carnet JRN-027 à 030
  et 041, 10 défis, analytique 150 à 152.

## Décisions prises (le propriétaire peut les changer)

1. **Un décor à part** pour le Jardin, comme le Toilettage et la Pluie de noisettes.
2. **13 plantes modélisées** (les 12 de départ et le Pois de senteur de la Prairie) ; les 12 plantes des autres biomes
   sont au catalogue et seront modélisées avec leurs biomes (leurs graines attendent dans l'inventaire).
3. **WISH-035 donne les produits tout seul** après chaque récolte.
4. **« Pousse heureuse » double les produits**, pas les teintures ni les graines.
5. **Les décorations des niveaux** (pancarte, arche, cloche, grand tournesol) vont dans l'inventaire.
6. **Floraison et week-end du Jardin** : réglages seulement (pousse × 2, graines rendues à 100 % le week-end).

## Fait main

- Blender : carré, bâche et panneau, pas japonais, barrière, arrosoir, panier, et 39 stades de plantes faits par un seul
  générateur paramétrique (`SourceArt/Blender/Tools/pw_garden.py`), revue `Docs/Captures/Review/T43_props_review.md`.
- Sons SFX_Garden_* et SFX_World_Harvest_* (ids 96 à 99 et 46), 58 dessins par notre code (`pw_ui_sprites_t43.py`),
  4 effets (gouttes, anneau de pousse, récolte, cœurs d'entraide).

## Performance

- 0 allocation par frame au Jardin ; vues de plantes en pool ; décor, plantes et interface chargés à la visite et rendus
  en partant. Décor : 12 109 triangles ; plantes de 157 à 885 triangles par stade.
