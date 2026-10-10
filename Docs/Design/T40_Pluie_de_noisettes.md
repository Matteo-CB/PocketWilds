# T40 Pluie de noisettes

But (file de production, T40) : la troisième activité, S-ACT-003 du doc « Mini-jeux & activités » et du doc 01 Contenu
(« Pluie de noisettes », lignes 1411 à 1497), sur le cadre T37. Critère : la validation d'activité du doc Mini-jeux,
section 6.

## Ce que le joueur vit

- **Ouverture.** Au niveau 10 du refuge, le jour 4 ou avec 15 animaux. La carte du hub s'allume, marquée « Nouveau ».
- **Le décor.** Un coin de Prairie rien que pour le jeu, comme le Toilettage : un grand noisetier rond dont la couronne
  remplit le haut de l'écran, ses grappes de noisettes, les collines douces derrière, l'herbe et les pâquerettes en bas.
  Le refuge ne se dessine pas pendant la partie.
- **L'animal.** L'Écureuil si le joueur en a un, sinon un petit quadrupède, sinon le favori (doc 01). Il porte un panier
  d'osier sur la tête (nos modèles n'ont pas de point d'accroche aux pattes) : le panier glisse vers l'avant sur le front
  jusqu'à la place la plus basse sans toucher les oreilles. Sur le lapin, il tient en équilibre sur ses oreilles dressées.
  L'animal fait face à la caméra, sautille de gauche à droite sous le doigt, se tourne un peu vers son mouvement.
- **La partie (60 s).**
  - Glisser le doigt n'importe où déplace le panier.
  - Noisettes et glands 10 points, baies 15, noisette dorée 50 avec sa traînée de points doux, pomme de pin 0 : elle
    rebondit sur le bord avec un « bonk » feutré, deux feuilles tombent, l'animal sursaute et la série repart à zéro.
  - Série : chaque prise d'affilée sans pomme de pin. Multiplicateur × 1,0, +0,1 toutes les 5 prises, × 3,0 au plus ; à
    chaque palier, un cœur et l'animal est content. Un objet manqué ne casse pas la série : il rebondit dans l'herbe et
    s'efface.
  - Les prises se posent dans le panier (une petite pile de 3 au plus, plus petite que les objets qui tombent).
  - Objets spéciaux, chacun une fois au plus par partie entre 15 et 45 s : panier large (+50 % pendant 8 s), plume (chute
    × 0,6 pendant 5 s), averse dorée (seulement des noisettes dorées pendant 5 s). Un anneau doux entoure le panier et la
    puce du HUD montre le temps qui reste.
- **Tutoriel (première partie).** Après 1 s, une patte glisse sous le panier avec « Glisse pour attraper » jusqu'à la
  première prise ; à la première pomme de pin, « Évite les pommes de pin ».
- **Récompenses.** Celles du cadre T37, plus : 1 Noisette (ingrédient) par 10 noisettes attrapées (3 au plus), 1 Fraise
  par 10 baies (2 au plus) quand la Pâtisserie sera ouverte (T63), 1 Noisette grillée à 3 étoiles. La carte de résultat
  montre les ingrédients et les friandises avec leurs propres icônes.

## Règles (simulation)

| d | 1 | 5 | 10 |
|---|---|---|---|
| Objets par seconde | 1,0 | 1,6 | 2,4 |
| Vitesse de chute | 2,5 | 3,5 | 5,0 |
| Part de pommes de pin | 10 % | 18 % | 25 % |
| Largeur du panier | 1,20 | 1,00 | 0,85 |
| Part de noisettes dorées | 3 % | 4 % | 5 % |

- Seuils du mode libre : 500, 700 et 900 (le doc prévoyait 500, 1 000 et 1 600 : avec ses propres cadences, une partie de
  niveau 1 fait environ 900 à 1 000 points au plus, sans l'averse dorée).
- Équité : deux noisettes d'affilée tombent toujours assez près l'une de l'autre pour qu'un joueur rapide attrape les
  deux ; une pomme de pin qui tombe près d'une noisette reste évitable.
- Vent du défi 8 : une sinusoïde douce de 0,6 au plus, période 8 s ; tous les objets dérivent pareil.
- Mode plus lent : la partie dure 75 s, chute, cadence et vent × 0,8.

### Défis

| # | Nom | Contrainte | Étoiles |
|---|---|---|---|
| 1 | Premières noisettes | paramètres d1 | 400, 800, 900 |
| 2 | Sans pomme de pin | aucune pomme de pin, chute 4,0 | 600, 1 100, 1 600 |
| 3 | Gare aux pommes de pin | 30 % de pommes de pin | 450, 700, 950 |
| 4 | Petit panier | panier 0,7 | 400, 850, 1 300 |
| 5 | Averse | 2,5 objets par seconde | 800, 1 500, 2 050 |
| 6 | Série d'or | plus longue série, paramètres d5 | 10, 35, 50 prises |
| 7 | Baies seulement | les autres objets valent 0 et ne cassent pas la série | 140, 300, 450 |
| 8 | Vent de côté | les objets dérivent de côté | 500, 1 000, 1 400 |
| 9 | Noisettes dorées | 10 % de noisettes dorées, chute 4,5 | 750, 1 100, 1 500 |
| 10 | Grande récolte | 90 s, paramètres d10 | 1 150, 2 800, 3 000 |

Équilibre (pire de 6 graines) : un joueur fort a 3 étoiles partout, un joueur tranquille 1 à 2 étoiles en mode libre au
niveau 3 et au moins 1 étoile à chaque défi. Les marges sont minces à cause de l'averse dorée, qui fait varier les scores.

## Décisions prises (le propriétaire peut les changer)

1. Un décor à part sous un noisetier, comme le Toilettage (le doc ne demande pas le refuge).
2. Le panier sur la tête, glissé vers l'avant sur le front ; sur le lapin, en équilibre sur les oreilles.
3. Les seuils d'étoiles abaissés là où les cadences du doc les rendaient impossibles.
4. Les vœux de score (WISH-055 et 056) utilisent les paliers du doc (30 à 60 noisettes, séries de 15 à 30) : aucun record
   par partie n'est encore enregistré pour appliquer la règle des 80 % du record.
5. Le son de série ne joue qu'à chaque palier du multiplicateur, pas à chaque prise.

## Fait main, sons et images

- Modèles Blender : panier, noisette, gland, baies, noisette dorée, pomme de pin, plume, noisetier
  (`SourceArt/Blender/Activities/BL_Activity_NutDrop.blend`, `pw_nutdrop.py`), revue `Docs/Captures/Review/T40_props_review.md`.
- Sons SFX_NutDrop_* (ids 84 à 87), revue `T40_sounds_review.md`.
- Icônes des bonus et des récompenses (`pw_ui_sprites_t40.py`), revue `T40_sprites_review.md`.

## Performance

- 0 allocation par frame (tests EditMode et PlayMode) ; 40 objets en pool, rien n'est créé pendant une partie.
- Le décor : 61 rendus, environ 8 000 triangles, chargé à la première partie et rendu en quittant les activités.
