# Pocket Wilds — règles du projet

## Vocabulaire (décision du propriétaire, 6 octobre 2026)

Rien de religieux ni de lexique divin dans tout le projet : texte du jeu, noms de contenu (espèces, variantes, raretés,
événements, biomes, monuments, décors, cosmétiques), identifiants, noms d'assets et de fichiers, code, commentaires,
docs du dépôt et du Drive, rapports. Pas de thème, symbole ou fête religieuse, même renommé.

- Glossaire et remplacements : `Docs/Design/Vocabulary_Rule.md` (sanctuaire → refuge, Mythique → Fabuleux,
  Céleste → Îles des nuages, Astral → Ciel étoilé, totem → cairn, rituel → rendez-vous, aura et halo → lueur,
  « Bible » → « Guide » dans les titres de docs).
- Contrôle automatique : `Assets/_Game/Tests/EditMode/VocabularyRuleTests.cs` doit passer avant chaque commit.
- Avant de nommer quoi que ce soit, vérifier le glossaire.

## Aucune collision (décision du propriétaire, 6 octobre 2026)

Rien n'entre jamais dans rien : animaux, oiseaux, insectes, décors, herbe et fleurs, œuf et nouveau-né. Un animal ne
traverse ni un rocher, ni un autre animal, ni l'eau ; un décor n'entre pas dans un autre ; rien ne pousse dans un décor.
Réaliste, optimisé et performant : pas de physique ni de NavMesh, des formes mesurées sur les modèles et un contrôle
avant chaque pas.

- Règle détaillée : `Docs/Design/Collision_Rule.md` (formes, comportements, performance, nouveau contenu).
- Contrôle automatique : `Assets/_Game/Tests/EditMode/CollisionRuleTests.cs` doit passer avant chaque commit ;
  l'`AssetValidator` signale les chevauchements ; le benchmark relève `overlapSamples` (doit rester à 0).
- Tout nouveau contenu (espèce, décor, zone, décoration du joueur) passe par ces mesures et ces tests.

## Aucune étincelle ✨ (décision du propriétaire, 6 octobre 2026)

Pas d'étincelle ✨ dans tout le projet : ni l'étoile scintillante à quatre branches, seule ou en groupe, ni le
caractère. Cela vaut pour les icônes, badges, cadres, particules, textes, code, docs et marketing. À la place :
étoiles rondes à cinq branches, éclats ronds, bulles douces, cœurs, pattes, perles.

- Règle détaillée : `Docs/Design/No_Sparkle_Rule.md`.
- Contrôle automatique : `Assets/_Game/Tests/EditMode/NoSparkleRuleTests.cs` doit passer avant chaque commit.

## Fait main (demande du propriétaire, 7 octobre 2026)

Des assets qui ne ressemblent jamais à de l'IA. Aucun générateur d'image, de 3D, de texture, de son ou de voix dans la
chaîne, aucun modèle de bibliothèque, aucune police tierce. Chaque asset final a sa source éditable : `.blend` et
script Blender, master WAV et recette, master de sprite et script de dessin, build de police.

- Règle détaillée : `Docs/Design/Handmade_Rule.md` ; charte complète : la charte « fait main » du Drive (dossier 05).
- Sources : `SourceArt/Handmade_Sources.json` (chaque `.blend` avec ses scripts, ce que produit Unity).
- Contrôle automatique : `Assets/_Game/Tests/EditMode/HandmadeRuleTests.cs` doit passer avant chaque commit.
- Les outils `generate_3d`, `import_asset`, `search_assets` (Blender MCP) et les outils de génération de Coplay ne
  sont jamais appelés.
- Tout nouvel asset passe la grille de revue de la charte, remplie dans `Docs/Captures/Review/`.
