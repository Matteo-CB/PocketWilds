# T37 Cadre des activités

But (file de production, T37) : le cadre commun des 18 activités du doc « Mini-jeux & activités » (sections 1, 2, 5
et 6) et du doc 01 Contenu (section 0, A15 et section 12.0). Il regroupe les billets, les étoiles, les défis,
l'entraînement, la difficulté qui s'adapte, le hub, le HUD de partie, la carte de résultat, le service de récompenses
testé et les analytics. Critère de la file : une activité factice parcourt tout le cycle, en test EditMode et en test
PlayMode dans le vrai jeu.

T37 ne livre encore aucune activité jouable. Dans le jeu réel, le bouton Jouer reste donc caché et un billet garde sa
valeur en Lumière (15 minutes). Tout s'allume avec T38 Toilettage, qui n'a qu'à enregistrer ses règles et sa scène.

## Ce que le joueur vivra

- **Bouton Jouer.** Il est sur le rail de droite, au-dessus d'Aujourd'hui, avec le nombre de billets dans une petite
  pastille. Il arrive au niveau 3 du refuge avec 5 animaux (A15), quand aucun écran n'est ouvert : un petit rebond,
  puis la patte le montre une fois. Un point apparaît quand une partie est offerte ou qu'une activité n'a jamais été
  jouée.
- **Hub (écran 029).**
  - C'est un panneau du bas : le refuge reste visible au-dessus, et un seul geste le ferme.
  - En haut, la puce des billets (« 4/5 », « Prochain billet dans 1 h 12 »). Jamais en rouge, jamais de compte à
    rebours qui défile. Le « + » ouvre les paquets.
  - Les cartes des activités défilent en carrousel : icône, nom, record, étoiles, défis réussis sur 10, « Nouveau »
    ou « Gratuit ».
  - Une carte fermée dit ce qui l'ouvre, en ne citant que les conditions qui manquent (« Niveau du refuge 10 et
    Jour 4 », « Ouvre le Lac »).
  - Sous les cartes : les pastilles de thème (un par biome ouvert, le dernier choix est gardé) et les 10 défis. Un défi
    s'ouvre avec une étoile sur le précédent.
  - L'interrupteur « Entraînement » rappelle « Gratuit, sans récompense ». Le gros bouton « Jouer » montre son coût :
    un billet, ou « Gratuit ».
- **Partie.**
  - Le HUD du refuge s'efface en 150 ms et le HUD de partie prend l'écran : le score qui roule sur une barre avec ses
    trois marques d'étoiles, le temps dans un anneau doux (jamais rouge, jamais de bip), la série dès ×2, la pause.
  - Une petite note de verre ouvre la partie, puis la musique de l'activité arrive, avec l'ambiance du biome 6 dB
    dessous. Il n'y a pas de compte à rebours.
  - La carte de pause propose « Reprendre » ou « Quitter ». Quitter dans les 3 premières secondes rend le billet.
  - Si l'application passe en arrière-plan, la partie se met en pause.
- **Résultat (doc 05, fiche 20).**
  - Un accord de kalimba clôt la partie et la musique baisse de 5 dB. La carte glisse en 200 ms, les étoiles sautent
    une à une (fa, la, do) pendant que le score roule, puis les récompenses se retournent une à une.
  - Le titre suit les étoiles (« Joli essai », « Bien joué », « Très bien », « Parfait ! »). Un nouveau record déroule
    son ruban et joue un motif de boîte à musique.
  - Dès la deuxième partie de la même activité, un seul toucher révèle tout.
  - Les boutons sont « Rejouer » et « Retour ».
- **Plus de billets.**
  - La carte, titrée « Billets en route », montre d'abord le temps avant le prochain billet gratuit, puis « Jouer en entraînement », puis « 3 billets »
    pour 30 Gemmes. Le paquet s'éteint doucement quand les 2 paquets du jour sont pris ou que les Gemmes manquent.
  - Elle ne bloque jamais : un toucher à côté la ferme. Acheter un paquet lance directement la partie demandée.
- **Entraînement.** Il est gratuit et illimité, ne donne aucune récompense, et compte pour la difficulté : le plaisir de
  jouer n'est jamais bloqué.

## Règles retenues (simulation)

- **Billets.**
  - La réserve est de 5 billets (7 avec le Club, à partir de T54). Il en revient un toutes les 2 heures, calculé sur
    l'horloge, absence comprise ; le reste de l'intervalle est gardé.
  - La première utilisation remplit la réserve (A15 montre 5) et reprend les billets gardés dans l'inventaire.
  - Les billets offerts dépassent la réserve jusqu'à 10 ; au-delà, ils attendent dans l'inventaire et rejoignent la
    réserve dès qu'il y a de la place. Aucun billet n'est jamais retiré.
  - Les billets achetés sont comptés à part et utilisés après les gratuits. Ils n'arrêtent pas la recharge.
- **Étoiles et records.**
  - Les seuils du mode libre sont fixes. Seule une partie récompensée du mode libre fait un record.
  - Le premier score n'est pas un « nouveau record ».
- **Défis.** Chaque activité a 10 défis aux paramètres fixes, qui ne changent pas la difficulté. Ils s'ouvrent dans
  l'ordre : une étoile sur le précédent ouvre le suivant.
- **Difficulté qui s'adapte.**
  - d va de 1 à 10 et commence à 3. Deux parties de suite à 0 étoile la baissent d'un cran, deux parties à 3 étoiles la
    montent d'un cran. Les parties d'entraînement comptent.
  - Chaque paramètre est donné à d = 1, 5 et 10, avec des droites entre ces points.
- **Récompenses d'une partie récompensée (doc Mini-jeux, section 2).**
  - 0 étoile ne donne rien.
  - Lumière : 5, 10 ou 20 minutes de la production du moment.
  - Lien : +2, +4 ou +6 à chaque animal joué.
  - Maîtrise : XP d'espèce 1, 2 ou 3, plus de l'XP de maîtrise du biome du thème.
  - Gemmes : 2 pour les premières 3 étoiles du jour dans chaque activité, 6 par jour au plus ; 5 pour les premières
    3 étoiles d'un défi, une seule fois ; une décoration de défi tous les 5 défis réussis.
  - Autocollants : un garanti pour un nouveau record, sinon une chance de 10, 20 ou 35 %.
  - Monnaie d'événement 20, 30 ou 40 pendant un événement (crochet prêt). Le week-end des activités double tout, sauf
    les Gemmes et les autocollants.
- **Pas de hasard payant (décision du 7 octobre).** Une partie jouée avec un billet acheté en Gemmes ne tire rien au sort.
  Son autocollant suit un compteur fixe (10, 20 ou 35 points par partie, un autocollant à 100), noté « Prochain
  autocollant de la page ».
- **Avant les albums (T44).** Un autocollant devient 3 friandises du biome le plus récent, comme en T36.
- **Ouvertures.**
  - La table du doc 01 (section 0) s'applique. Pour les activités qui en ont une, la décision vient du service du niveau
    du refuge, si bien que le hub et l'écran du niveau ne se contredisent jamais.
  - Une activité ouverte le reste.
- **Remote Config.** Toutes les valeurs sont réglables par les clés du doc 08 : `activities.*`,
  `econ.light_minutes.activity_*`, `econ.mastery_xp.activity_*`, `econ.bond.activity_*`, `gems.activity_*`,
  `gems.price.tickets_pack`, `gems.tickets_pack_*` et les drapeaux `flags.activities` / `flags.activity.*`. Ce sont
  30 clés et 16 drapeaux, avec leurs bornes.

## Ajouter une activité (T38 et suivantes)

1. Les règles : `ActivityRules` (seuils du mode libre, paramètres à d = 1, 5 et 10, 10 défis), enregistrées dans
   `ActivityRegistry.RegisterShipped`.
2. Le jeu : une classe `IActivityController` en C# pur, tickée par l'`ActivityRunner`, sans allocation par tick.
3. La scène : une `IActivityStage` (accessoires poolés, cadrage, touchers) enregistrée dans l'`ActivityStageRegistry` du
   présentateur. Son groupe Addressables `Activity_<Nom>` se charge à la première partie et se libère en quittant les
   activités.
4. Dans `ActivityCatalog`, `ShippedInThisBuild` passe à vrai ; ses souhaits et ses objectifs du carnet sont branchés.
5. La validation suit le doc Mini-jeux, section 6.

## Décisions prises (le propriétaire peut les changer)

1. Pas de bouton Jouer tant qu'aucune activité n'existe dans le build. Un billet reste à 15 minutes de Lumière jusqu'à
   T38.
2. Quitter une partie dans les 3 premières secondes rend le billet (valeur du code, pas encore de clé Remote Config).
3. Billets offerts : jusqu'à 10 dans la réserve, le reste attend dans l'inventaire.
4. Billets achetés utilisés après les gratuits.
5. Une partie à 0 étoile ne donne rien, pas même de l'XP de biome.
6. Les Gemmes des premières 3 étoiles du jour comptent aussi les parties de défi ; les 5 Gemmes d'un défi sont hors du
   plafond de 6 par jour.
7. Défis ouverts dans l'ordre, une étoile sur le précédent suffit.
8. La première partie de Toilettage est offerte, en mode libre ou en défi.
9. L'entraînement ne fait ni record ni étoile de défi ; il compte pour la difficulté.
10. Le week-end des activités double la Lumière, le lien, les maîtrises et la monnaie d'événement, jamais les Gemmes
    ni les autocollants.
11. Le hub ne montre que les activités du build, les ouvertes d'abord, dans leur ordre d'ouverture. Il s'ouvre sur la
    dernière activité jouée, sinon sur la première nouvelle.
12. L'animal mis en scène est un favori quand il y en a un, sinon il change à chaque partie.
13. Le carnet de la semaine compte les étoiles des parties récompensées. Un défi compte comme réussi à sa première
    étoile.
14. Acheter un paquet depuis la carte « Plus de billets » lance la partie demandée.
15. Le Grand Concours suit le doc 01 (niveau 24, jour 14 et une tenue), pas le doc 08 (niveau 20, jour 7) ; c'est à
    harmoniser dans le doc 08.
16. Les cartes des activités à minuteur (Expéditions, Jardin) ne montrent ni étoiles ni défis : elles sont marquées
    « Gratuit ».
17. Si l'application est tuée en pleine partie, le billet est perdu. Une mise en arrière-plan, elle, met la partie en
    pause.

## Sauvegarde, analytics, sons et images

- **Sauvegarde.** Le schéma 11 ajoute la section Activities (billets, ancrage de recharge, paquets et Gemmes du jour,
  records, difficulté, défis avec étoiles et essais). La migration 10 → 11 n'a rien à convertir. La sauvegarde de
  référence v11 est dans les tests.
- **Analytics.**
  - Les numéros des événements, des écrans et des raisons d'économie suivent maintenant le catalogue des docs 08 et 09.
    Ils ont été réalignés avant tout envoi de données et sont figés à partir du premier build qui en envoie.
  - Événements des activités : 110 à 114.
  - Événements du rendez-vous : 120 Aujourd'hui ouvert, 131 carnet complet.
  - Étapes 13 premier souhait et 14 première activité.
  - La recharge des billets est sommée dans le flux d'économie (monnaie 5, raison 52).
  - Détails : `Docs/Analytics/T29_Analytics_Schema.md`.
- **Sons.** 24 sons synthétisés : début, fin, record, pause, 8 pas de série, 3 étoiles (fa, la, do) et 5 cartes. Trois
  musiques en boucle : « Petite partie » (84 BPM, 80 s), « Pattes pressées » (112 BPM, 120 s) et « Câlins en série »
  (96 BPM, 100 s), dans le groupe Addressables `Audio_Activities`. Revue : `Docs/Captures/Review/T37_sounds_review.md`.
- **Images.** 27 sprites dessinés par notre code (`SourceArt/UI/Tools/pw_ui_sprites_t37.py`) : le ballon Jouer,
  entraînement, défi, record, minuteur, cadenas, pause, les étoiles du résultat et une icône par activité. Ils sont dans
  `Atlas_Activities`. Revue : `Docs/Captures/Review/T37_sprites_review.md`.
- **Règles du projet.** Pas d'étincelle (étoiles rondes à cinq branches, points, bulles), rien de religieux, tout fait
  main.

## Performance

- **Rien n'alloue par frame.** Le runner, le tick des services (une fois par seconde : recharge, jour, ouvertures) et le
  calcul des récompenses n'allouent rien, tests à l'appui. Le HUD de partie ne change ses textes que quand la valeur
  change.
- **Écrans fermés.** Ils ne coûtent rien (canevas éteint). Tout est préchauffé à l'entrée de la scène, et le carrousel
  recycle 8 cartes pour 18 activités.
- **Musiques.** Elles sont lues en flux et chargées au choix d'une partie. Elles sont libérées une fois la musique du
  refuge revenue.

## Tests

- **EditMode.**
  - Billets : recharge, absences, paquets, plafonds.
  - Difficulté : séquences et interpolation.
  - Récompenses de 0 à 3 étoiles, plafonds de Gemmes, défis et décorations, absence de hasard payant.
  - Table des ouvertures face au doc 01 et au service du niveau.
  - Cycle complet avec un contrôleur factice et absence d'allocation.
  - Sauvegarde 11 et analytics des activités.
- **PlayMode.**
  - Écrans avec des modèles factices : hub, carrousel, entraînement, défis, thèmes, résultat normal, réduit et révélé
    d'un coup, HUD de partie, pause, carte sans billet, bouton Jouer, effacement du HUD.
  - Le cycle complet de l'activité factice dans le vrai jeu.
