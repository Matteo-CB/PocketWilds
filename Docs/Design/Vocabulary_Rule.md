# Règle de vocabulaire : rien de religieux, aucun lexique divin

Décision du propriétaire, 6 octobre 2026 : « rien de religieux ou de lexique divin », dans tout le projet. La règle
s'applique au texte du jeu, aux noms de contenu (espèces, variantes, raretés, événements, biomes, monuments, décors,
cosmétiques), aux identifiants et noms d'assets, au code, aux docs du Drive et du dépôt, aux rapports.

Le jeu reste neutre et universel : un refuge pour animaux, doux et naturel. Pas de thème, de symbole, de fête ni de
mot religieux ou divin. Le contrôle automatique est `Tests/EditMode/VocabularyRuleTests.cs` (textes localisés,
contenus, noms d'assets, code et docs du dépôt).

## Remplacements

| Avant | Après (FR) | Après (EN, code et ids) |
|---|---|---|
| sanctuaire / Sanctuary | refuge | Refuge (`RefugeController`, `RefugeCameraRig`, `RefugeHud`, `PF_CameraRig_Refuge`, `ScreenId.Refuge`) |
| Sanctuary Pass | Pass du Refuge | Refuge Pass |
| Bible (titres de docs) | Guide | Guide (Performance Guide, Asset Production Guide, Biome Guide, Writing & Localization Guide) |
| Mythique / Mythic (rareté) | Fabuleux | Fabulous (`RarityTier.Fabulous`, `FabulousReveal`, `VFX_HatchFabulous`) |
| Céleste / Celestial (biome D-BIO-009) | Îles des nuages | Cloudlands, préfixe CL_ (`CL_Landmark_CloudArch`) |
| Astral (biome D-BIO-011) | Ciel étoilé | Starlight, préfixe SL_ (`SL_Landmark_StarArch`, `SL_ConstellationStone`) |
| totem | cairn, pierre dressée | Cairn, Stone (`MO_Landmark_PeakCairn`) |
| rituel / ritual (« Rituel du jour ») | rendez-vous (« Rendez-vous du jour ») | Daily Visit |
| aura | lueur | Glow (`SH_RareGlow`) |
| halo | lueur | Glow |
| lanterne de pierre de temple | lanterne de jardin | GardenLantern (`SA_GardenLantern`) |
| lotus | fleur d'eau | WaterBloom |
| zen | calme, apaisant | Calm |
| esprit, âme (au sens surnaturel) | cœur, caractère, présence | Heart, Character |
| bleu céleste | bleu ciel | sky blue |
| sacré, divin, saint, ange, dieu, déesse, paradis, miracle, bénédiction, prière, temple, autel, oracle, moine, prêtre | à reformuler sans ce sens | à reformuler |

## Mots interdits (contrôle automatique)

Français : bible, sanctuaire, sacré, divin, divinité, céleste, saint, sainte, ange, dieu, déesse, paradis, miracle,
bénédiction, béni, prière, temple, autel, oracle, moine, prêtre, église, chapelle, relique, pèlerin, totem, rituel,
astral, mythique, karma, zen, nirvana, mantra, résurrection, réincarnation, démon, enfer.

Anglais : bible, sanctuary, sacred, holy, divine, deity, celestial, heaven, heavenly, angel, god, goddess, paradise,
miracle, blessing, blessed, prayer, pray, temple, shrine, altar, oracle, monk, priest, church, chapel, relic, pilgrim,
totem, ritual, aura, astral, mythic, karma, zen, nirvana, mantra, resurrection, reincarnation, demon, hell.

« aura » est contrôlé en anglais et dans les identifiants seulement : en français c'est aussi le verbe avoir
(« il aura »). Exceptions : aucune dans le contenu du jeu. Les mots peuvent apparaître uniquement dans ce document et dans le test
qui les liste, pour décrire la règle.

## Fêtes et événements

Aucun événement fondé sur une fête religieuse ou sur un symbole religieux, même renommé. Les événements restent ceux
du calendrier original (saisons, nature, animaux, jeux).
