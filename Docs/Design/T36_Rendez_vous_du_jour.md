# T36 Rendez-vous du jour

But (file de production, T36) : le court rendez-vous quotidien du doc 01 « Contenu des rendez-vous, des activités et
de l'accueil » (sections 0 à 6 et 10.1), qui donne envie de revenir chaque jour sans jamais presser : cadeaux de
bienvenue, cadeau du jour, calendrier de 28 jours, souhaits des animaux, carnet de la semaine, cadeaux de retour, lien
des caresses et des friandises. Une visite complète se fait en moins de deux minutes.

## Ce que le joueur vit

- **Fin de l'accueil (A08 à A12).**
  - A08 : quand le panneau Améliorer se ferme (ou 30 secondes après l'apparition du bouton), une bulle apparaît
    au-dessus de Mochi : « Fais éclore 2 œufs ».
  - A10 : deux éclosions plus tard, la bulle devient une coche avec un cœur. Toucher Mochi ou la bulle récupère le
    souhait (3 Gemmes, un Trèfle croquant, 15 points de lien) et termine l'accueil. Le compteur de Gemmes apparaît avec
    ce premier gain.
  - A12 : au niveau 2 du refuge, quand aucun écran n'est ouvert, le bouton Aujourd'hui arrive sur le rail de droite avec
    un petit rebond et un point. La patte montre le bouton, puis le cadeau. Le premier cadeau est un œuf Découverte : il
    attend sur le bouton Éclore (« Gratuit ») et la patte montre Éclore une fois.
  - Une sauvegarde qui a déjà 5 animaux saute l'accueil.
- **Écran Aujourd'hui (031).** Un panneau du bas ; le refuge reste visible au-dessus. Il se ferme d'un geste (toucher
  au-dessus, Fermer, retour). Dans l'ordre :
  - le cadeau de bienvenue des jours 1 à 3 (100 Gemmes chacun ; 2 billets le jour 2, 5 graines le jour 3) ;
  - le cadeau du jour : une boîte ronde en bois avec une patte, sans nœud. Le couvercle se soulève et les objets
    apparaissent un par un. Le bandeau prend sa couleur (crème, bleu ciel, doré) seulement à l'ouverture : la boîte ne
    trahit jamais son contenu. Ensuite : « À demain » ;
  - les souhaits : portrait de l'animal, phrase, barre, « Récupérer », « Changer » (un échange gratuit par jour),
    « Tout récupérer » ;
  - le calendrier : 28 cases, la case du jour dorée qui respire doucement, un tampon de patte, « Demain : … », « Encore
    n jours » jusqu'à la grande case ;
  - la ligne du carnet (« Carnet de la semaine n/7 », avec son point).
  - Ouvrir Aujourd'hui récupère d'office les souhaits réalisés.
- **Bulles de souhait dans le monde (002).** Au-dessus des animaux, jamais dans le HUD. Anneau de progression, icône du
  système, nombre restant. La bulle ne touche jamais un animal, un décor, l'œuf ni une autre bulle (règle « rien
  n'entre dans rien ») ; les insectes et les oiseaux l'évitent aussi. Toucher une bulle ouvre la carte du souhait ;
  toucher une bulle réalisée la récupère : la bulle éclate (250 ms), des Gemmes volent vers le compteur, l'animal
  sautille.
- **Carte du souhait.** Petite carte posée au-dessus de la bulle, pointée vers elle : phrase, n/objectif, récompenses,
  « Récupérer » ou « Changer ». Un toucher à côté la ferme.
- **Carnet de la semaine (032).** Sept objectifs avec leur barre, ce que rapporte chaque objectif dit une seule fois en
  haut (« Chaque objectif : +5 Gemmes, 30 min »), le coffre du carnet complet (15 Gemmes et un œuf Découverte), les vrais
  jours restants (« Encore 4 jours », « Dernier jour »), jamais de compte à rebours. Le premier carnet commence avec deux
  objectifs déjà entamés (« Déjà bien commencé »). Ce qui n'a pas été récupéré avant lundi est gardé et payé à
  l'ouverture suivante.
- **Cadeau de retour.** Après 3 à 13 jours d'absence : 120 minutes de Lumière, 3 billets, un œuf Découverte,
  3 friandises. Après 14 jours ou plus : 240 minutes, 5 billets, un œuf, 5 friandises et un autocollant. Il s'affiche sous
  la Lumière de la carte de retour, avec ce qui attend dans le refuge ; un seul « Récolter » prend tout.
- **Friandises.** La fiche de l'animal a un bouton « Friandise » avec le nombre possédé et un petit cœur si sa famille la
  préfère (+50 %). Des cœurs montent vers la barre de lien.

## Règles retenues (simulation)

- Un jour va de l'heure de remise (minuit local par défaut) à la suivante ; une semaine de lundi à lundi. La clé du jour
  ne recule jamais : une horloge reculée ou un changement de fuseau vers l'ouest ne redonne rien.
- Cadeau du jour : 12 lots du doc ; le premier cadeau du compte est un œuf Découverte ; au moins un grand cadeau tous
  les 15 (`daily.gift_big_every`) ; un cadeau non ouvert n'est pas reporté.
- Calendrier : 100 Gemmes et 2 œufs Découverte par cycle de 28 cases, une décoration de cycle à la case 28 (puis un œuf à
  partir du cycle 27) ; un jour manqué ne remet rien à zéro.
- Souhaits : un tirage par jour de connexion, k = le plus petit de 3 et de 9 moins les souhaits en attente ; jamais deux
  souhaits du même système le même jour, au plus deux d'une même famille, au moins un rapide, au plus un à minuteur ;
  Gemmes plafonnées en cumul (9 × jours depuis l'installation). Systèmes livrés : éclosion, découvertes et maîtrise,
  câlins et friandises, améliorations, agrandissements.
- Avant l'existence des activités, du Jardin et des albums : un billet vaut 15 minutes de Lumière, les graines sont
  gardées pour le Jardin, un autocollant devient 3 friandises.
- Lien : une caresse vaut 1 point, au plus 5 par animal et 30 par jour ; friandises de 10 à 18 points (20 et 40 pour les
  rares) ; un souhait donne 15 points ; plafond de 1 000, jamais baissé ; l'Essence Confort et Meilleurs amis s'y
  ajoutent.
- Valeurs réglables par Remote Config (`daily.*`, `gems.*`, `econ.bond.*`, `econ.mastery_xp.wish`, `flags.*`).

## Décisions par défaut (à confirmer par le propriétaire)

1. Billets, graines et autocollants avant T37, T43 et T44 : Lumière 15 minutes par billet, graines gardées, autocollant
   du grand retour remplacé par 3 friandises.
2. Cadeau du jour non ouvert à minuit : non reporté (le point d'Aujourd'hui le rappelle).
3. Jours sans connexion : un tirage de souhaits par jour de connexion.
4. Service de lien minimal et friandises dans T36 : oui.
5. Numéros des sons : 34 à 37 réservés, `CollectGems = 67`.
6. Décorations de cycle : à modéliser dans Blender (13 et leurs versions crépuscule) avant le soft launch ; T36 montre
   l'icône et le nom.
7. Nouvelle clé `daily.gift_big_every` (5 à 30, 15 par défaut).

## Fait main et règles du projet

- 20 sprites dessinés par notre code (`SourceArt/UI/Tools/pw_ui_sprites_t36.py`), revue dans
  `Docs/Captures/Review/T36_sprites_review.md`. Étoiles rondes à cinq branches, pattes, cœurs, perles ; jamais
  d'étincelle à quatre branches.
- 6 sons synthétisés (`pw_audio_sfx.py` : souhait réalisé, ouverture du cadeau, tampon, Gemmes), revue dans
  `Docs/Captures/Review/T36_sounds_review.md`.
- Bulle : shader écrit à la main (`SH_WishBubble`), effet `VFX_WishDone` fait de bulles rondes, de cœurs et d'un anneau.
- Vocabulaire : « rendez-vous », jamais de mot religieux.

## Performance

- Aucune allocation par frame : la vérification de la journée tourne une fois par seconde, les crochets de progression
  et les tirages n'allouent rien, les textes ne changent que quand leur valeur change.
- Les écrans fermés ne coûtent rien (canevas désactivé, pas d'Update) ; tout est préchauffé à l'entrée de la scène.
- Les 9 bulles sont créées à l'entrée ; placement à 10 Hz, suivi à chaque frame, sans allocation.
- Les sprites propres à Aujourd'hui sont dans leur atlas (`Atlas_Today`), chargé avec l'écran.

## Analytics

Événements 45 à 55 (cadeau, souhaits tirés, réalisés et échangés, case du calendrier, objectif du carnet, cadeau de
retour, cadeau de bienvenue, visite terminée, friandise donnée) et écrans 7 (Aujourd'hui) et 8 (Carnet). La visite
terminée mesure le critère des deux minutes chez les vrais joueurs.

## Tests

- EditMode : cadeau, calendrier, bienvenue, souhaits (tirage et progression), carnet, lien, cadeau de retour, frontières
  de jour (heure d'été, fuseaux), façade, textes (en, fr, pluriels), placement des bulles, sauvegarde 10, analytics.
- PlayMode : HUD (bouton Aujourd'hui, Gemmes, carte du souhait), écrans Aujourd'hui et Carnet, bulles (30 animaux,
  aucune collision, aucune allocation), parcours complet (accueil jusqu'à Aujourd'hui, visite en moins de deux minutes,
  minuit pendant le jeu, cadeau de retour, friandise).
