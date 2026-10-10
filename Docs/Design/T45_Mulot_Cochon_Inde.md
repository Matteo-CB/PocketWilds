# T45 Mulot et Cochon d'Inde

Quatrième et cinquième espèces de la Prairie (spec : doc 03 « Roster complet », A-SP-004 et A-SP-005). Critère : le
pipeline des petits quadrupèdes réutilisé, 6 couleurs et 3 couleurs de maîtrise pour chacun.

## Ce que le joueur vit

- **Le Mulot** : le plus petit du pré, grosse tête ronde, grandes oreilles rondes, longue queue fine enroulée vers le haut
  en point d'interrogation.
  Il trottine vite. Six couleurs dans les œufs de la Prairie : Brun chaud, Sable (Communes), Gris, Blanc (Peu communes),
  Noir (Rare), Pie (Épique, grandes taches brunes).
- **Le Cochon d'Inde** : un corps en pomme de terre, sans cou ni queue, une bonne bouille ronde, de toutes petites
  oreilles. Il marche doucement. Six couleurs : Caramel, Crème (Communes), Noir, Tricolore (Peu communes), Gris et blanc
  (Rare, ceinture et liste blanches), Doré (Épique).
- **Couleurs de maîtrise** (données prêtes, données au joueur par T46 aux niveaux 10, 20 et 30) : Noisette, Lilas, Blé
  doré pour le Mulot ; Cannelle, Guimauve, Abricot pour le Cochon d'Inde.
- **Couleurs secrètes** (données prêtes, leurs conditions viendront avec les saisons et la reproduction) : Bouton d'or et
  Nuage de lait.
- **Leurs autocollants** de la page 1 de la Prairie : Mulot des foins, Cochon d'Inde qui saute, Sieste dans l'herbe,
  Partage de noisettes, Ronde du soir.

## Règles

- Même squelette SK_SmallQuadruped, mêmes clips : le Mulot saute comme le Lapin, le Cochon d'Inde marche comme le
  Hérisson. Les oreilles des deux espèces tournent avec la tête.
- Les couleurs de maîtrise et secrètes ne sortent jamais des œufs, des souhaits ni de la génétique, et les couleurs de
  maîtrise ne comptent pas comme couleurs secrètes trouvées.
- Les identifiants d'espèces des albums, des titres et des visiteurs suivent maintenant le roster (species.fieldmouse,
  species.guineapig, species.fawn, species.ermine, species.chickadee...).

## Décisions prises

1. Les couleurs de maîtrise sont des données dès T45, données au joueur par T46 (maîtrise pour toutes les espèces).
2. Les lueurs « émissives » du doc (bout de la queue, oreilles) attendent un masque émissif dans le shader des animaux.
3. Les oreilles du Cochon d'Inde prennent un ton plus foncé du pelage plutôt que le rose.
4. Le Mulot est un peu plus grand que l'estimation du doc (53 cm avec la queue, 29 cm avec les oreilles) pour garder les
   os du squelette commun dans le corps ; sa queue s'enroule vers le haut au lieu de traîner au sol.
5. Cache-cache : avec ces deux petites espèces, la plus grande zone de la Prairie tient 6 cachettes au lieu de 7. Au
   niveau 10, 5 animaux se cachent toujours, avec 1 leurre au lieu de 2 (le jeu limitait déjà les leurres aux cachettes
   libres). Un décor de plus dans le Verger rendra le second leurre.
