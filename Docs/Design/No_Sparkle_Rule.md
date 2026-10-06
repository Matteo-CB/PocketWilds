# Règle : aucune étincelle ✨

Décision du propriétaire, 6 octobre 2026 : pas d'étincelle ✨ dans tout le projet. Est visée l'étoile scintillante à
quatre branches, seule ou en groupe, comme le symbole ✨.

La règle s'applique partout :

- icônes, badges, cadres ;
- particules et VFX ;
- textes du jeu, code, docs ;
- marketing, à venir.

## Formes à utiliser

- **Étoiles** : étoiles rondes à cinq branches (★), comme dans les étoiles de rareté.
- **Éclats de lumière** : points ronds lumineux et bulles douces. Ce sont les cases « glint » et « bubble » de la
  planche de particules.
- **Accents** : cœurs, pattes, pétales, anneaux, perles.

## Ce qui a changé (T34A)

| Élément | Avant | Après |
|---|---|---|
| Icône de la Lumière (`ICO_Currency_Light`) | Étoile à quatre branches avec un orbe | Orbe chaud entouré de huit rayons ronds |
| Badge de trait Brillant (`ICO_Trait_Shiny`) | Deux étoiles à quatre branches | Perle brillante |
| Cadres Épique, Légendaire, Fabuleux | Étoiles à quatre branches aux coins | Perles rondes |
| Badge Mélanique | Petite étoile à quatre branches | Deux points ronds |
| Planche de particules | Étoile à quatre branches et scintillement | Éclat rond et bulle douce |
| Effet des animaux rares | `VFX_RareSparkle` | `VFX_RareGlint`, avec des bulles de lumière |

## Contrôle automatique

`Assets/_Game/Tests/EditMode/NoSparkleRuleTests.cs` vérifie trois choses :

- aucun caractère ✨, ✦, ✧, ❇, ❈, ✴, ✵, ✶, 🌟 ou 💫 dans les textes, le code, les sources ou les docs ;
- aucune étoile à quatre branches dans les générateurs de sprites ;
- aucune case en étoile à quatre branches dans la planche de particules.

Le test doit passer avant chaque commit, comme ceux du vocabulaire et de l'absence de collision.
