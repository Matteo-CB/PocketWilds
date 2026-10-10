# T44A Albums

T44 (file de production) est découpée en trois : T44A albums (ce document), T44B succès, titres, cadres et poses,
T44C journal du refuge et visiteurs rares. Spec : « 01 Albums et autocollants : liste complète ». Critère : garanties
testées, 0 GC en jeu, les 720 autocollants sont des données validées.

## Ce que le joueur vit

- **Ouverture** au niveau 12 du refuge et au jour 5 : un bouton Albums apparaît au-dessus de la Boutique (point rouge
  quand un autocollant n'a pas été vu). La page 1 de la Prairie commence avec le Lapin curieux offert.
- **L'étagère** : 16 albums aux couvertures dessinées, leur progression, les étoiles d'album, « Choisir un autocollant »
  dès 10 étoiles.
- **Une page** : 12 emplacements ; un autocollant gagné montre sa capture et sa rareté (Commun, Rare, Brillant avec sa
  perle et un reflet nacré lent, jamais d'étincelle pointue) ; une case vide montre sa silhouette et l'icône de sa
  source ; « Bientôt » quand le sujet n'existe pas encore dans le jeu. On glisse d'une page à l'autre.
- **La révélation** sur le refuge, jamais pendant une partie : la pochette s'ouvre, la rareté apparaît, l'autocollant se
  colle ; un doublon donne des étoiles d'album.

## Règles

- Les 720 autocollants, 60 pages et 16 albums viennent des tableaux du doc, lus par un script (`Tools/Data/pw_album_data.py`)
  qui refuse toute ligne illisible et vérifie 7 Communs, 4 Rares et 1 Brillant par page.
- Tirages : sources et chances de la section 1.4, choix de la page avec exclusions et prorata, poids des raretés,
  source privilégiée, garanties (nouveauté, Brillant, record, Cadeau du jour sans doublon, autocollant de zone, moment
  signature une fois), parties payées en ordre fixe sans tirage, étoiles d'album, récompenses une seule fois.
- **Disponibilité** : un autocollant n'est tiré que si sa capture existe et que son sujet est dans le jeu.
- Sauvegarde section 20 (schéma 14), analytique 160 à 165.

## Art

- 36 autocollants capturés depuis nos vrais modèles (Prairie : Lapin, Écureuil, les 8 paysages de zones, les papillons ;
  les pages des activités livrées). 54 attendent leurs espèces ou leurs objets ; 6 ont été refusés pour un léger
  chevauchement et seront repris.
- Icônes, 16 couvertures et deux sons dessinés par nos scripts.

## Décisions prises

1. T44 découpée en trois parties.
2. Les autocollants gardés avant l'ouverture des albums deviennent 10 étoiles d'album chacun.
3. Un autocollant fixe peut être donné même sans capture (sa case montre la silhouette jusqu'à sa capture).
4. L'icône des Améliorations est redessinée (patte et badge vert « + ») à la demande du propriétaire.
