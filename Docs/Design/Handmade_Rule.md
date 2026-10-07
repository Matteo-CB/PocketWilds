# Règle : des assets « fait main »

Demande du propriétaire (brief commun du 7 octobre 2026) : des assets qui ne ressemblent jamais à de l'IA, dans un jeu
magnifique et très optimisé. La règle complète est la charte « fait main » du Drive (dossier 05 - Art, UI,
UX & audio) : 25 signes du look « généré », langage de formes, palette, lumière, yeux, animation, interface, processus
et grille de revue de 44 points. Ce fichier résume ce que le dépôt contrôle.

## Ce qui est interdit

- Aucun modèle 3D, aucune image, aucune texture, aucun son, aucune musique ni voix sortis d'un générateur par IA, même
  pour un prototype qui pourrait rester.
- Aucun agrandissement d'image par un réseau de neurones.
- Aucun modèle importé d'une bibliothèque (Poly Haven, Sketchfab, Poly Pizza) dans le jeu final.
- Aucune police tierce : Pocket Round et Pocket Plush seulement.
- Les outils de génération des serveurs MCP ne sont jamais appelés : `generate_3d`, `import_asset` et
  `search_assets` de Blender MCP ; `generate_3d_model_from_text`, `generate_3d_model_from_image`,
  `generate_3d_model_texture`, `auto_rig_3d_model`, `generate_or_edit_images`, `generate_sfx`, `generate_music` et
  `generate_tts` de Coplay.

## Ce qui est permis

- Les scripts Blender écrits pour le projet (`SourceArt/Blender/Tools/pw_*.py`) : chaque forme y est une valeur
  choisie, lisible et versionnée.
- Les générateurs de sprites à distances signées (`SourceArt/UI/Tools/`), la planche de particules de
  `VfxPipeline.cs`, les polices construites par `SourceArt/Fonts/Tools/`.
- La synthèse sonore maison (`SourceArt/Audio/Tools/`), déterministe.
- Les captures rendues dans Unity depuis les vrais modèles (vignettes du Codex, captures des zones).
- La décimation contrôlée des LOD, revue à la distance d'affichage.

La frontière : un outil qui invente une forme, une image ou un son à partir d'une description est interdit. Un outil
qui exécute une construction décrite pas à pas par le projet est permis.

## D'où vient chaque asset

`SourceArt/Handmade_Sources.json` dit d'où vient chaque asset final de `Assets/_Game/Art/` :

| Asset final | Source exigée |
|---|---|
| Modèle FBX | Exporté par Blender (`pw_export.py`). L'en-tête du FBX nomme le `.blend` source, qui doit exister sous `SourceArt/Blender/` et figurer dans le fichier avec ses scripts de construction |
| Son | Master du même nom sous `SourceArt/Audio/Masters/<catégorie>/` et recette du même nom dans `SourceArt/Audio/Tools/` |
| Sprite d'interface | Master `<nom>@4x.png` sous `SourceArt/UI/Masters/` et dessin par un script de `SourceArt/UI/Tools/` |
| Police | Copie exacte de `SourceArt/Fonts/Build/`, construite par `pw_font_build.py` |
| Maillage cuit, capture, masque, planche de particules | Listé sous `generated` avec le script ou le pipeline qui le produit |
| Texture de plus de 512 px | Listée sous `largeTextures` avec sa raison |

## Contrôle automatique

`Assets/_Game/Tests/EditMode/HandmadeRuleTests.cs` doit passer avant chaque commit, comme les tests du vocabulaire,
de l'absence de collision et de l'absence d'étincelle. Il vérifie :

1. Chaque modèle est un FBX écrit par Blender depuis un `.blend` de `SourceArt/Blender/`.
2. Chaque `.blend` est listé avec ses scripts de construction, et ces scripts existent.
3. Chaque son a son master et sa recette.
4. Chaque image est dessinée par nos scripts ou rendue depuis nos modèles.
5. Chaque maillage cuit a son pipeline.
6. Chaque texture de plus de 512 px a une raison écrite.
7. Chaque police est notre propre build.
8. Aucun nom de fichier ni aucune métadonnée ne porte la trace d'un générateur ou d'une bibliothèque de modèles. Le
   test lit les noms de fichiers, les textes des FBX, les blocs de texte des PNG (dont les clés `parameters`, `prompt`
   et `workflow` des générateurs d'images), les blocs des WAV, la table des noms des polices et les `.meta`.
9. Aucun script du projet n'appelle un outil de génération.

Les `.blend` sont compressés : le test ne les ouvre pas. La revue « fait main » (T34E) les a ouverts une fois pour
les contrôler (voir `Docs/Captures/Review/`).

## Nouveau contenu

- Une nouvelle espèce ou un nouveau décor : construit par un script de `SourceArt/Blender/Tools/`, exporté par
  `pw_export.py`, son `.blend` ajouté à `SourceArt/Handmade_Sources.json`.
- Un nouveau son : recette dans `SourceArt/Audio/Tools/`, master dans `SourceArt/Audio/Masters/`.
- Un nouveau sprite : dessiné dans un script de `SourceArt/UI/Tools/`, master `@4x` dans `SourceArt/UI/Masters/`.
- Toute autre image ou tout maillage produit dans Unity : son pipeline ajouté sous `generated`.
- Puis la grille de revue de la charte, remplie dans `Docs/Captures/Review/`.
