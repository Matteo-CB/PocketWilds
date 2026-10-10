# T44C Journal du refuge et visiteurs rares

Troisième et dernière partie de T44 (spec : « 01 Succès, titres, cadres, poses et journal », section 6 ; « Rétention »,
visiteurs rares ; « Événements originaux », section 5 ; doc 05, SUR-06). Critère : garanties testées, 0 GC en jeu.

## Ce que le joueur vit

- **Le journal** s'ouvre depuis la carte du profil (pastille « Journal », point quand une page n'a pas été vue) :
  - **Souvenirs** : la frise des 63 moments du doc, groupés par mois, les plus récents en haut. Chaque page a sa date, son
    texte (le nom de l'animal, l'espèce, le biome… jamais une phrase assemblée), l'icône de son système et « Nouveau » ;
  - **Moments** : les surprises du monde déjà vues (les 50 du doc 05 ont leur nom ; seule SUR-06 « Un visiteur arrive »
    existe dans le jeu aujourd'hui, les autres s'ajouteront avec leurs surprises).
- **Les images** : le portrait de l'animal ou la vue du refuge (interface cachée) sont pris une seule fois, à un moment
  calme (jamais pendant une éclosion, une activité, un achat ou un passage de niveau), au plus un par seconde ; sinon
  l'icône du moment, la carte postale de l'expédition ou le portrait du Codex.
- **Les visiteurs rares** à partir du niveau 8 du refuge et du jour 3 : une ou deux visites par jour, 2 heures chacune. Le
  visiteur arrive par un chemin libre avec sa traînée de points ronds, s'assoit sur une place libre et attend. Un toucher
  donne un cadeau (5 minutes de Lumière, une friandise ou un ticket d'activité), son autocollant de saison au premier
  toucher, et 30 % de chances de rejoindre la collection (garanti au 4e contact, « Nouveau visiteur ! »).

## Règles

- Les 63 moments viennent du tableau du doc, lus par un script (`Tools/Data/pw_journal_data.py`) qui refuse les lignes
  illisibles, les variables qui diffèrent entre anglais et français et les doublons. Le même script lit les 50 surprises
  du doc 05.
- 300 images au plus : les plus anciennes des moments répétables reprennent leur icône, les premières fois gardent la leur.
- Le planning des visiteurs est tiré par jour depuis une graine : une absence ne fait perdre aucune visite déjà passée,
  et le calendrier ne dépend que du jour.
- **Visiteurs disponibles** : un visiteur ne vient que si son espèce et sa couleur existent. Tant qu'aucun visiteur de la
  saison n'existe, ceux qui existent viennent à leur place : Lapin des prés et Hérisson fleuri (printemps), Lapin de neige
  (hiver).
- Les couleurs de visiteur ne sortent jamais des œufs et ne comptent pas dans les succès des couleurs secrètes.
- Sauvegarde section 22 (schéma 16), analytique 180 à 182 et 344.

## Art et son

- Trois couleurs de visiteur peintes sur nos modèles, l'icône du journal, la pastille du visiteur, la carte de page et
  l'onglet de mois, dessinés par nos scripts. Deux sons de recette (l'arrivée du visiteur, la page qu'on tourne) et deux
  effets en points ronds. Grille de revue : `Docs/Captures/Review/T44C_assets_review.md`.

## Décisions prises

1. Le journal s'ouvre depuis le profil (pas de nouveau bouton dans le HUD, déjà chargé).
2. Les 50 surprises ont leur nom dès maintenant ; chacune s'ajoute à la page « Moments » quand elle existera.
3. Le premier toucher d'un visiteur inscrit SUR-06 dans les Moments.
4. Les identifiants des 9 visiteurs pas encore jouables sont provisoires, ils seront confirmés avec leurs espèces.
