# T41 Memory des empreintes

But (file de production, T41) : la quatrième activité, S-ACT-004 du doc « Mini-jeux & activités » et du doc 01 Contenu
(« Memory des empreintes », lignes 1498 à 1560), sur le cadre T37. C'est la première activité entièrement en interface.
Critère : la validation d'activité du doc Mini-jeux, section 6.

## Ce que le joueur vit

- **Ouverture.** Au niveau 11 du refuge et le jour 5. La carte du hub s'allume, marquée « Nouveau ».
- **L'écran.** Le refuge ne se dessine plus. Une nappe crème à carreaux vichy très doux, et dessus la grille de cartes.
  Les dos montrent un trèfle à quatre feuilles rondes. Sur un écran portrait, une grille large se couche : 4 × 3 devient
  3 colonnes de 4, 5 × 4 devient 4 colonnes de 5, pour des cartes plus grandes.
- **Les cartes.** Les portraits sont les vignettes de capture des animaux du joueur (ses variantes d'abord, puis celles
  que le codex connaît, puis les autres). Aux niveaux 6 à 10, une carte de chaque paire montre l'empreinte de la
  variante : la forme de la patte dit l'espèce (lapin, écureuil, hérisson), sa couleur dit le pelage.
- **La partie.**
  - Toucher une carte la retourne (un petit écrasement et une légère montée, 0,22 s, froissement de carte).
  - Deux cartes qui vont ensemble restent visibles avec une coche, deux éclats ronds dorés, un petit cœur et deux notes
    en tierce qui montent de paire en paire. Sinon elles tremblent doucement et se retournent après 0,8 s, sur une note
    feutrée très douce.
  - Aperçu de toutes les cartes au début : 2 s aux niveaux 1 et 2, 1 s aux niveaux 3 et 4, puis aucun.
  - La carte lueur (bord doré) : la retourner fait luire 1 s la carte de sa paire.
  - La paire double (à partir du niveau 4) porte un badge « ×2 » visible face retournée seulement, et vaut 200.
- **Tutoriel (première partie).** Après l'aperçu, une patte touche une carte puis sa paire, avec « Trouve les paires »,
  jusqu'à la première paire.
- **Récompenses.** Celles du cadre T37, plus 1 Biscuit aux graines à 3 étoiles, et 1 graine de Trèfle doux quand le
  Jardin sera ouvert (T43).

## Règles (simulation)

| Niveau | Grille | Durée | Paires | Aperçu |
|---|---|---|---|---|
| 1 à 3 | 4 × 3 | 45 s | portraits identiques | 2 s (1 et 2), 1 s (3) |
| 4 à 7 | 4 × 4 | 60 s | portraits identiques (4 et 5), empreinte et portrait (6 et 7) | 1 s (4), aucun ensuite |
| 8 à 10 | 5 × 4 | 90 s | empreinte et portrait | aucun |

- Paire : +100 (200 pour la paire double). Bonus de fin : 50 × paires − 20 × erreurs (au moins 0), plus 3 points par
  seconde entière restante. À la fin du temps, seules les paires trouvées comptent.
- Erreur : rater alors que la première carte et une carte de sa bonne paire avaient déjà été vues face retournée ;
  l'aperçu ne compte pas comme vu, et un premier essai n'est jamais une erreur.
- Seuils du mode libre par grille : 4 × 3 : 500, 700, 980 (le doc disait 850 : finir les 6 paires donne déjà 900 avant
  le bonus de temps) ; 4 × 4 : 700, 950, 1 150 ; 5 × 4 : 900, 1 250, 1 500. Le cadre des activités accepte désormais des
  seuils et une durée qui varient avec le niveau.

### Défis

| # | Nom | Contrainte | Étoiles |
|---|---|---|---|
| 1 | Petite grille | 4 × 3, portraits identiques | 500, 700, 850 |
| 2 | Bonne mémoire | 4 × 3, aucun aperçu | 450, 650, 800 |
| 3 | Grille moyenne | 4 × 4 | 700, 950, 1 150 |
| 4 | Empreintes | 4 × 4, empreinte et portrait | 650, 900, 1 100 |
| 5 | Sans faute | 4 × 3, chaque erreur retire 50 au lieu de 20 | 500, 700, 850 |
| 6 | Cartes qui bougent | 4 × 4, deux cartes cachées échangent leur place toutes les 4 paires | 650, 900, 1 100 |
| 7 | Chrono serré | 4 × 4 en 45 s | 650, 900, 1 100 |
| 8 | Grande grille | 5 × 4 | 900, 1 250, 1 500 |
| 9 | Même biome | 5 × 4, toutes les cartes d'un même biome (aujourd'hui la Prairie) | 850, 1 200, 1 450 |
| 10 | Grand Memory | 5 × 4, empreinte et portrait, 75 s | 900, 1 250, 1 500 |

Équilibre (pire de 6 graines) : un joueur fort a 3 étoiles partout. Un joueur tranquille a 2 étoiles en mode libre au
niveau 3, mais atteint souvent 3 étoiles sur les grilles 4 × 4 et 5 × 4 et dans les défis : le Memory est un jeu calme et
un vrai joueur sera plus lent que le joueur simulé. À vérifier au premier test avec de vrais joueurs.

## Décisions prises (le propriétaire peut les changer)

1. **Une empreinte par variante**, pas par espèce : avec 3 espèces, une empreinte d'espèce irait avec plusieurs
   portraits. Forme = espèce, couleur = pelage, chaque paire reste unique.
2. **Les grilles se couchent en portrait** quand les cartes y sont plus grandes.
3. **Les portraits sont empruntés** au catalogue du codex, jamais copiés dans le paquet de l'activité.
4. **Le seuil 3 étoiles de la grille 4 × 3 passe à 980**, les autres seuils restent ceux du doc.
5. **Le carnet JRN-052** (« Trouve {n} paires au Memory ») est livré avec l'activité.

## Fait main, sons et images

- Sons SFX_Memory_* (ids 88 à 91), revue `Docs/Captures/Review/T41_sounds_review.md`.
- Dessins par notre code (`pw_ui_sprites_t41.py`) : dos de carte, face, bord doré, badge, nappe, éclat, empreintes des
  trois espèces, Biscuit aux graines ; revue `T41_sprites_review.md`.

## Performance

- 0 allocation par frame ; 20 cartes en pool, rien n'est créé pendant une partie ; l'écran et son atlas sont chargés à
  la première partie et rendus en quittant les activités.
