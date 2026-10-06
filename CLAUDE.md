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
