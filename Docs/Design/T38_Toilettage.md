# T38 Toilettage

But (file de production, T38) : la première vraie activité, S-ACT-001 du doc « Mini-jeux & activités » et du doc 01
Contenu (« Toilettage », lignes 1235 à 1328), posée sur le cadre T37. Critère : la validation d'activité du doc Mini-jeux,
section 6 :
- comprise sans texte en 3 secondes (un geste animé) ;
- 10 défis équilibrés ;
- des récompenses passées par le service testé ;
- des tests EditMode du score, des étoiles et des récompenses ;
- un test PlayMode qui joue une partie entière ;
- 60 FPS et 0 allocation par frame ;
- la mémoire rendue à la sortie ;
- les analytics envoyées.

C'est l'activité de l'attachement : on brosse SON animal, il penche la tête vers la brosse, ferme à moitié les yeux,
glousse quand on repasse sur une zone propre et soupire de contentement à la fin, un nœud sur la tête.

## Ce que le joueur vit

- **Ouverture.** Au niveau 3 du refuge avec 5 animaux, le bouton Jouer arrive sur le rail. La patte le montre, puis
  montre la carte Toilettage du hub. La première partie est offerte.
- **Choix de l'animal.**
  - Depuis sa fiche : bouton « Brosser », marqué « Gratuit » ou d'un billet.
  - Depuis le hub, dans l'ordre :
    - l'animal d'un souhait « Brosse {animal} » en attente ;
    - sinon l'animal visible au lien le plus bas ;
    - pour le défi « Grand gabarit », le plus gros animal possédé.
- **Le décor.** Un petit coin de Prairie rien que pour lui : un monticule d'herbe rond avec un coussin rose tricoté,
  des collines rondes en trois plans, des pâquerettes, un ciel chaud, une lumière un peu plus dorée que le refuge. Pendant
  la partie, le refuge ne se dessine plus (la partie coûte moins cher que le refuge lui-même). L'animal tient en
  trois quarts, bien cadré, sur le coussin.
- **La partie (45 s).**
  - **Poussière.** Le pelage porte des plaques de poussière douce, gris chaud et duveteuses, qui pâlissent à chaque
    passage.
  - **Brosser.** Glisser le doigt brosse ; la brosse en bois suit le doigt juste au-dessus du pelage, avec un souffle
    feutré et de la mousse ronde. Une zone propre jette trois petits éclats ronds et une note de verre qui monte d'un
    demi-ton par zone d'affilée.
  - **Débris.** Brins d'herbe, pétales de pâquerette et trèfles s'enlèvent d'un toucher et s'envolent en tournant.
  - **Bardanes.** Il faut deux petits coups : la bardane remue au premier, saute au second.
  - **Bulles de savon.** À 15 s et 30 s, une bulle se pose sur le pelage ; la toucher nettoie les 3 zones sales les plus
    proches.
  - **Brosse dorée.** À 20 s, si la série atteint × 1,5, elle attend sur le pelage ; la prendre fait compter double
    chaque passage pendant 5 s, avec une puce dorée sous le score.
  - **Repasser sur du propre.** L'animal glousse et la série repart à × 1,0, sans perte de points. Traverser du propre
    pour rejoindre une plaque sale ne compte pas.
- **Le nœud.**
  - Quand tout est propre, ou 5 s avant la fin, trois boutons ronds proposent un nœud rose, bleu ciel ou crème.
  - Le nœud choisi vole se poser sur la tête (deux notes de boîte à musique), +50 points, puis +5 par seconde restante.
  - L'animal ferme les yeux, des cœurs montent, il soupire. La carte de résultat s'ouvre sur lui, son nœud sur la tête.
- **Tutoriel (première partie).**
  - « Brosse le pelage » avec une patte qui glisse sur la poussière jusqu'au premier passage.
  - « Enlève les feuilles » avec une patte qui tape la première feuille.
  - « Deux petits coups pour les bardanes » avec une patte qui tape deux fois.
  - « Touche la bulle » à la première bulle, puis « Choisis un nœud ».
- **Récompenses.** Celles du cadre T37, plus un Trèfle croquant pour toute partie récompensée à 3 étoiles.

## Règles (simulation)

- Seuils du mode libre : 200, 380 et 520 points.
- Difficulté d de 1 à 10 :
  - zones sales 16 / 24 / 32 (d1 / d5 / d10) ;
  - débris 4 / 8 / 12 ;
  - bardanes 0 / 2 / 4 ;
  - passages par zone : 2 jusqu'à d7, 3 à partir de d8 ;
  - mouvement de l'animal : 0 % / 5 % / 12 % de l'écran.
- La poussière vient en plaques d'environ 5 zones réparties sur le corps, jamais en un seul tas ni en zones isolées.
  Une bulle n'est jamais sur un débris, et un débris par zone au plus.
- **Où se posent les objets.** Débris, bardanes, bulles et brosse dorée vont d'abord sur les zones larges du pelage. Les
  oreilles, les pattes, le bord de la silhouette et les épines fines du hérisson ne les reçoivent qu'une fois les autres
  zones prises. Elles sont toujours salies et brossées. Ainsi rien ne flotte au bord d'une oreille, et le nombre de
  débris ne change pas.
- **Les 10 défis :**
  1. Premier coup de brosse
  2. Tout doux (−20 par rebrossage)
  3. Pluie de feuilles
  4. Vite fait, bien fait (30 s)
  5. Petit agité (mouvement 15 %)
  6. Pelage épais (3 passages)
  7. Grand gabarit (le plus gros animal)
  8. Finition rapide (nœud avant 35 s)
  9. Bardanes têtues
  10. Grand toilettage
- **Équilibre mesuré** par des joueurs scriptés sur 6 graines, sur un corps de 40 zones :

  | Joueur | Mode libre (tout d) | Défis |
  |---|---|---|
  | Fort | 3 étoiles | 3 étoiles aux 10 |
  | Occasionnel | 2 étoiles | 1 à 3 étoiles selon le défi |

- **Souhaits et carnet.**
  - WISH-019 : une partie finie avec cet animal, entraînement compris.
  - WISH-020 : les étoiles des parties récompensées.
  - WISH-021 : les débris et bardanes enlevés.
  - WISH-022 : un nœud posé.
  - JRN-033 : chaque partie finie.

## Décisions prises (le propriétaire peut les changer)

1. Un décor dédié plutôt que le refuge : toujours bien cadré, moins coûteux, prêt pour les thèmes par biome.
2. La poussière est un masque par zone lu par le shader des animaux (mot-clé `_PW_DUST`), pas les couleurs de sommets :
   celles-ci portent déjà les régions de l'animal.
3. Les nœuds se choisissent par trois boutons ronds en bas de l'écran.
4. Repasser sur du propre en chemin vers une plaque sale ne fait ni glousser ni retomber la série.
5. La phase du nœud commence 5 s avant la fin (40 s sur 45, 25 s sur 30 au défi 4).
6. Les débris ne bloquent pas le nœud et restent à enlever pendant la phase du nœud.
7. Une fois tout propre, brosser fait glousser l'animal sans rien compter : on peut le câliner.
8. Le Trèfle croquant de 3 étoiles n'est jamais doublé par le week-end des activités.
9. La graine de pissenlit est retirée des débris du thème Prairie : plantée sur sa tige, elle ressemblait à une épingle.
10. Seul le thème Prairie existe dans ce build ; les autres viendront avec leurs biomes (données seulement).

## Fait main, sons et images

- **Modèles.** Décor, brosse, 6 débris, bardane, nœud et bulle sont construits par script dans Blender
  (`SourceArt/Blender/Tools/pw_grooming.py`, fichier `SourceArt/Blender/Activities/BL_Activity_Grooming.blend`) et
  peints en couleurs de sommets. Revue : `Docs/Captures/Review/T38_props_review.md`.
- **Sons.** 18 sons synthétisés : brossage (3), feuille (2), bardane (2), zone propre (8 paliers), nœud et bulle (2).
  Le gloussement et le soupir viennent de la voix de l'espèce. Revue : `Docs/Captures/Review/T38_sounds_review.md`.
- **Images.** 5 sprites dessinés par notre code : patte qui glisse, trois nœuds, brosse dorée. Revue :
  `Docs/Captures/Review/T38_sprites_review.md`.
- **Règles du projet.**
  - Les éclats sont des points ronds, jamais d'étincelle à quatre branches ; rien de religieux.
  - Rien n'entre dans rien : les pattes restent sur le plat du coussin, avec la marge du balancement. Les débris sont
    posés sur le pelage, le nœud sur la tête au-dessus des épines, et la brosse glisse à 9 mm du pelage.

## Performance

- **Décor.** Environ 5 350 triangles en 2 appels de dessin. Le pire cas, avec l'animal, les débris, la mousse, les éclats
  et les bulles, est d'environ 40 appels et 14 000 triangles.
- **Calcul par frame.** Au plus 64 zones suivies sur leurs os. Le tableau de poussière n'est envoyé au shader que quand
  il change.
- **Mémoire.** Le décor et ses objets sont dans le groupe Addressables `Activity_Grooming`. Ils sont chargés à la première
  partie, préchauffés pendant le chargement (une image hors écran compile les shaders), et libérés en quittant les
  activités.
- **Aucune allocation** par frame pendant une partie (test PlayMode).

## Tests

- **EditMode.**
  - Points, séries, rebrossage et ses tolérances, débris, bardanes, bulles, brosse dorée, nœud et bonus de temps.
  - Chaque défi, la disposition déterministe et répartie, les zones fines et l'équilibre des joueurs scriptés.
  - Le Trèfle croquant, les souhaits et le carnet, le cycle complet par les services, 0 allocation.
- **PlayMode.**
  - Une partie entière dans le vrai jeu : décor chargé, refuge éteint puis rallumé, zones mesurées, tutoriel, poussière
    à zéro, nœud sur la tête, résultat et récompenses, 0 allocation, décor libéré.
  - Le HUD : conseils, patte, choix du nœud, puce dorée, bouton « Brosser » de la fiche.
