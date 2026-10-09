# T39 Cache-cache

But (file de production, T39) : la deuxième activité, S-ACT-002 du doc « Mini-jeux & activités » et du doc 01 Contenu
(« Cache-cache », lignes 1326 à 1410), posée sur le cadre T37 et rangée comme le Toilettage T38. Critère : la validation
d'activité du doc Mini-jeux, section 6, avec en plus « cachettes dans tous les chunks Prairie » :
- comprise sans texte en 3 secondes (une patte qui tape l'animal le plus visible) ;
- 10 défis équilibrés ;
- des récompenses passées par le service testé ;
- des tests EditMode du score, des étoiles et des récompenses ;
- un test PlayMode qui joue une partie entière ;
- 60 FPS et 0 allocation par frame ;
- la mémoire rendue à la sortie ;
- les analytics envoyées.

C'est l'activité qui fait regarder son refuge et ses animaux : la joie de repérer une oreille derrière un buisson, le
« coucou » quand l'animal saute dehors, le petit cœur.

## Ce que le joueur vit

- **Ouverture.** Au niveau 4 du refuge avec 8 animaux, la carte Cache-cache du hub s'allume, marquée « Nouveau »
  (A18). La première partie montre ses gestes.
- **Le lieu : son propre refuge.** Pas de décor à part. La caméra du refuge glisse en 0,6 s vers un coin d'un chunk
  Prairie ouvert, cadré en trois quarts un peu plus près que d'habitude. Chaque partie prend le coin suivant : jamais
  deux fois de suite le même quand deux zones ou plus sont ouvertes. Les animaux du refuge, les bulles de souhait et
  l'œuf s'effacent le temps de la partie ; les oiseaux et les papillons continuent, mais s'écartent des cachettes.
- **Les cachettes.** Chaque chunk Prairie a sa zone de 3 à 7 cachettes, mesurées sur le chunk lui-même :
  - buissons fleuris (deux nouveaux, pâquerettes rondes, un peu plus grands que les buissons du kit) : les meilleures
    cachettes ;
  - hautes herbes (trois touffes nouvelles, faites main dans Blender, groupées en bosquets d'environ 1 m dans tous les
    chunks) ;
  - petits arbres, buissons, rochers, rondins, souches, bottes de foin, clôtures du kit.
- **Les animaux cachés.** De 3 à 5 animaux du joueur, tous différents quand c'est possible, les favoris visibles
  d'abord. Chacun se tient à côté de son décor, jamais dedans, et grignote l'herbe tête baissée. Pour chaque espèce, la
  part visible de sa silhouette (une oreille, la queue, le museau) est celle de la partie : 30 % au début, 8 % aux plus
  hauts niveaux.
- **La partie (60 s).**
  - **Toucher un décor** où un animal se cache : il saute dehors en arc jusqu'à une place libre devant (0,45 s, le saut
    contourne le décor), « coucou » à sa voix avec un petit pop, un cœur, un anneau doux au sol. +100 points et +2 par
    seconde restante.
  - **L'indice.** Après le délai de la partie, l'animal relève la tête (son geste de surprise, en place : les oreilles
    dépassent), puis se remet à grignoter ; l'herbe de sa cachette ondule par un vent local du shader, sans particule. L'ondulation revient toutes les 3 s. Trouvé après son
    indice : +50 et +1 par seconde restante. Un animal timide attend 2 s de plus.
  - **Un décor vide.** Trois feuilles s'envolent du côté de la caméra, un froissement léger, aucun point retiré. Trois
    touchers vides en 2 s : les touchers se reposent 1 s, un anneau pâle et « Doucement… ».
  - **Les leurres.** Des cachettes vides qui bougent au vent, en silence (le bruissement d'indice veut toujours dire
    qu'un animal est là).
  - **La loupe** (bouton rond en bas à droite, une fois par partie) : un anneau de verre pulse 2 s autour de la cachette
    occupée la plus proche du dernier toucher.
  - **Le sifflet** (une fois) : deux notes de flûte qui montent, chaque animal encore caché remue à son tour.
  - **Tous trouvés.** +5 par seconde restante ; les animaux se rassemblent en rang au point de rendez-vous de la zone,
    joyeux, avec un motif de boîte à musique. Si le temps finit avant, les animaux encore cachés sortent la tête à leur
    place d'arrivée : on apprend où ils étaient.
- **Tutoriel (première partie de l'activité).**
  - Après 2 s : « Touche les animaux cachés » avec une patte qui tape l'animal le plus visible, jusqu'à la première
    trouvaille.
  - Au premier indice : « Une oreille qui bouge ! ».
  - Après la première trouvaille : « La loupe et le sifflet t'aident », sous les boutons.
- **Récompenses.** Celles du cadre T37, plus un Biscuit champignon pour toute partie récompensée à 3 étoiles.

## Règles (simulation)

- Seuils du mode libre : 300, 600 et 750 points.
- **Écart au doc 01, assumé.** Le doc prévoyait 4 à 8 animaux et jusqu'à 4 leurres, pensés pour un décor à part. Dans le
  vrai refuge, un coin cadré en portrait offre 3 à 7 bonnes cachettes : un décor bas ne cache bien qu'un animal qui
  grignote juste derrière lui, vu par une caméra assez basse. La partie cache donc moins d'animaux, mieux cachés, et
  choisit un coin qui les contient tous.
- Difficulté d de 1 à 10 :

| d | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| Animaux cachés | 3 | 3 | 4 | 4 | 4 | 4 | 4 | 5 | 5 | 5 |
| Part visible | 30 % | 26 % | 22 % | 19 % | 15 % | 14 % | 12 % | 11 % | 9 % | 8 % |
| Délai d'indice | 6 s | 7 s | 8 s | 9 s | 10 s | 11 s | 12 s | 13 s | 14 s | 15 s |
| Leurres | 0 | 0 | 1 | 1 | 1 | 1 | 1 | 2 | 2 | 2 |

- Les cachettes d'un coin arrivent rangées de la meilleure à la moins bonne : les animaux prennent les meilleures (les
  N + 2 premières, jamais deux côte à côte quand c'est possible), les leurres les suivantes.
- Le coin de la partie est le suivant parmi les zones Prairie ouvertes qui contiennent tous ses animaux et leurres ; si
  aucune ne suffit, la plus grande, et la partie cache alors moins d'animaux.
- Les indices sont décalés de 0 à 1,5 s d'un animal à l'autre : jamais deux en même temps.
- Un toucher sur un animal déjà trouvé ne fait rien. Les points comptent les secondes entières restantes.
- Les objets marchent même pendant le repos des touchers. Le mode plus lent allonge la partie, la loupe et le rythme des
  déplacements, pas les indices : il ne fait que donner du temps.

### Défis

| # | Nom | Objectif | Contrainte | Étoiles |
|---|---|---|---|---|
| 1 | Premiers cachés | trouve 3 animaux | indice à 6 s | 200, 450, 650 |
| 2 | Sans indice | 4 animaux | aucun indice, ni loupe ni sifflet | 300, 600, 850 |
| 3 | Bien cachés | 4 animaux | visibilité 10 % | 300, 600, 800 |
| 4 | Course contre la montre | 4 animaux | 40 s | 250, 500, 700 |
| 5 | Grande famille | 5 animaux | visibilité 20 % | 450, 900, 1 000 |
| 6 | Un seul regard | 4 animaux | 3 essais à côté permis, le 4e finit la partie | 300, 600, 900 |
| 7 | Dans le noir | 4 animaux | nuit, cercle éclairé autour du doigt | 250, 550, 800 |
| 8 | Tous pareils | 5 animaux de la même espèce | visibilité 15 % | 350, 700, 900 |
| 9 | Cachettes mouvantes | 4 animaux | un animal non trouvé change de cachette toutes les 15 s | 350, 700, 850 |
| 10 | Le grand cache-cache | 5 animaux | 45 s, visibilité 8 %, aucun indice | 350, 600, 750 |

- **Nuit (défi 7).** Un voile bleu nuit (#211F47, 76 % près du cercle, 86 % au loin) avec un cercle doux qui suit le
  doigt et reste là où il s'est levé ; un masque d'interface, une seule image, aucune lumière temps réel. Le refuge passe
  en ambiance de nuit (`LP_Grassland_Night`) le temps du défi.
- **Tous pareils (défi 8).** L'espèce que le joueur possède le plus ; ses individus se répètent s'il en a moins de 5.
- **Cachettes mouvantes (défi 9).** Une cachette reste toujours libre. L'animal choisi rapetisse dans l'herbe et repousse
  derrière sa nouvelle cachette en 0,4 s, sans jamais traverser le coin à découvert ; jamais vers un leurre, une cachette
  occupée ou celle que la loupe entoure. Un bruissement doux dit qu'il faut regarder à nouveau.

### Équilibre (pire de 6 graines, coins de 7, 6, 5 et 4 cachettes)

Modèle :
- un joueur fort trouve un animal en 2 s à 30 % visible (4 s à 8 %) et ne touche jamais à côté ;
- un joueur tranquille met 5 s (10 s à 8 %) et touche une cachette vide toutes les 2 trouvailles.

- Mode libre : le joueur fort a 3 étoiles à tous les niveaux (792 points au niveau 1, 1 100 à 1 128 aux niveaux 8 à
  10) ; le joueur tranquille a 2 étoiles (709 à 711 au niveau 3).
- Défis : 3 étoiles possibles pour un joueur fort dans les 10, au moins 1 étoile pour un joueur tranquille dans chacun.

## Les coins du refuge (mesurés)

| Zone | Caméra | Cachettes |
|---|---|---|
| Départ | vers le nord, à l'ouest de l'œuf | 3 |
| Pré | vers l'ouest, le long de la bande | 5 |
| Ruisseau | vers l'ouest | 4 |
| Verger | vers l'ouest | 7 |
| Colline | vers l'ouest | 5 |
| Moulin | vers l'ouest | 5 |
| Mare | vers l'ouest | 4 |
| Lisière | vers l'ouest | 3 |

- **Règles de cadrage**, vérifiées sur tous les téléphones (9:21 à 9:16) : la zone de jeu ne montre jamais le bord de
  l'île ni le vide ; aucune cachette à moins de 1,5 m du bord ; seul son propre décor cache un animal, aucun autre décor
  ne passe devant ; l'atterrissage du « coucou » est à 0,7 m au moins et l'animal y est visible à 80 % au moins. Sur
  tablette, la caméra garde la largeur d'un téléphone 9:16 et zoome un peu.
- **Seuils** : 3 cachettes au moins par coin (une partie de niveau 1, le tutoriel compris), 4 coins de 5 ou plus (une
  partie complète, tous les défis), un coin de 7 (niveau 10 avec ses leurres). Le simulateur d'économie ouvre le Pré
  (5 cachettes) dans la première heure, avant le niveau 4 qui débloque le cache-cache.

## Décisions prises (le propriétaire peut les changer)

1. **La partie se joue dans le refuge**, pas dans un décor à part (file : « cachettes dans tous les chunks Prairie » ;
   doc Mini-jeux : « chaque chunk a ses cachettes »). Le refuge reste dessiné, ses animaux s'effacent et les animaux
   joués se cachent dans ses vrais buissons.
2. **Les hautes herbes et les buissons fleuris du thème Prairie existent désormais dans le monde** : trois touffes et
   deux buissons faits main, posés dans les huit chunks.
3. **Moins d'animaux, mieux cachés** : 3 à 5 animaux selon le niveau et dans les défis, 0 à 2 leurres (le doc prévoyait
   4 à 8 et jusqu'à 4 leurres). Un vrai coin du refuge n'offre que 3 à 7 bonnes cachettes ; les seuils
   d'étoiles sont rééquilibrés en conséquence.
4. **Les animaux cachés grignotent**, tête baissée derrière leur décor (la pose dans laquelle les cachettes sont
   mesurées) ; leur indice est de relever la tête.
5. **La part visible est mesurée, pas devinée** : pour chaque cachette et chaque espèce, des points de la silhouette
   testés contre la géométrie du chunk depuis la caméra de la zone ; le jeu interpole entre 8, 15, 20 et 30 %.
6. **Le défi 10 garde la loupe et le sifflet** : le doc ne retire que les indices (seul le défi 2 dit « ni loupe ni
   sifflet »).
7. **Le « coucou »** est le même son, trouvé avant ou après l'indice ; la voix de l'espèce monte un peu (× 1,12).
8. **Le vœu WISH-051** compte une partie menée à son terme, entraînement compris ; WISH-052 et JRN-016 comptent chaque
   animal trouvé, même dans une partie quittée ; WISH-053 demande tous les animaux d'une même partie.
9. **Les objets se dépensent au toucher** ; le bouton grisé porte une coche ronde.
10. **Le cercle de nuit (défi 7)** : « 25 % de l'écran » est lu comme un cercle d'un quart de la hauteur de l'écran
   (rayon d'un quart de sa largeur), une vraie lampe de poche ; un cercle d'un quart de la surface couvrirait presque
   toute la largeur en portrait.
11. **Récompenses** : le lien et l'expérience d'espèce vont à trois des animaux qui ont joué, pas à un favori absent de la
   partie.
12. **Noms anglais des défis** : First Finds, No Hints, Well Hidden, Race the Clock, Big Family, Sharp Eyes, In the Dark,
   All Alike, Shifting Spots, Grand Hide and Seek.
