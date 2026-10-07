# T29 Schéma analytics

Événements d'onboarding, d'économie, de monétisation et de performance, envoyés par lots. Critère : payload léger et
identifiants stables. Vérifié par `AnalyticsTests` (EditMode, 19 tests) et `AnalyticsPlayTests` (PlayMode).

## Architecture

```
HatchService.Hatched, EconomyService.Changed, StoreService.PurchaseFinished / RestoreFinished
                                   |
Presentation (IAnalytics) ----> AnalyticsService (Services) : file circulaire de 512 structs fixes
  écrans, offres vues et touchées,      |  lot toutes les 60 s de jeu, à 100 événements, et quand l'app part
  étapes du funnel                      v
                               AnalyticsBatchEncoder : JSON compact dans un buffer réutilisé (aucune allocation)
                                        |
                               AnalyticsOutbox : copie dans 4 emplacements, thread d'écriture, 64 fichiers max
                                        |
                               IAnalyticsTransport : NullAnalyticsTransport tant que le backend n'est pas choisi
```

- Les faits de jeu (éclosion, monnaies, achats) viennent des événements des services : la présentation ne peut pas
  oublier de les envoyer. La présentation ne signale que ce qu'elle seule voit (écrans, offres, collecte).
- Un événement est un struct de 64 octets copié dans la file : pas de chaîne, pas de boxing, pas d'allocation.
- Le thread principal ne touche jamais le disque : il copie le lot encodé (quelques Ko) et repart.
- Lumière et Essence sont additionnées par raison et envoyées une fois par session (`EconomyFlow`). Les Gemmes et
  les achats partent un par un. L'éclosion porte son coût : pas besoin d'un événement économique en plus.

## Format d'un lot (schéma 1)

```json
{"schema":1,"aid":"<id analytics>","app":"0.1.0","build":"development","platform":"Android","device":"<modèle>",
 "os":"<os>","ram":4096,"batch":0,"dropped":0,
 "events":[[1,1791219008329,1,0,0,0,1,0],[20,1791219009850,1,105022833,3252890455,4009452501,1,7,0,0,1]]}
```

- L'en-tête (appareil, version) est écrit une fois par lot, jamais dans chaque événement.
- Chaque événement est un tableau positionnel : `[id, temps unix ms, session, champs...]`.
- Les contenus voyagent par leur hash stable (FNV-1a 32 bits de l'id source, le même que dans la sauvegarde),
  jamais par leur nom ni par un SKU de store. Table de correspondance : `T29_Analytics_Ids.csv`
  (menu `Pocket Wilds/Analytics/Export Id Table (T29)`).
- Les grands nombres (`BigValue`) voyagent en mantisse + exposant de 10.
- `dropped` : événements perdus parce que la file était pleine (jamais en pratique : 512 places, un lot par minute).
- Mesuré : première session complète (boot, éclosion, Codex, départ) = 11 événements, environ 900 octets en-tête
  compris. Un événement fait 15 à 60 octets.

## Événements

Les valeurs sont figées : on n'en change jamais une, on ajoute seulement (test `WireIds_AreStable`). Un champ nouveau
s'ajoute à la fin d'un événement.

| Id | Événement | Champs après [id, temps, session] |
|---|---|---|
| 1 | SessionStart | secondes hors ligne, animaux, espèces, palier qualité, économie batterie |
| 2 | SessionEnd | durée (s), animaux |
| 3 | LoadTime | boot (ms depuis le lancement), chargement de scène (ms) |
| 10 | Milestone | étape du funnel, secondes depuis l'installation |
| 20 | Hatch | œuf, espèce, variante, rareté, drapeaux (1 nouvelle espèce, 2 nouvelle variante, 4 gratuit, 8 pitié, 16 garantie d'espèce, 32 garantie de variante, 64 œuf Découverte), coût (mantisse, exposant), niveau de maîtrise, rang dans une éclosion multiple (0 : simple), règle de l'œuf Découverte (1 espèce, 2 variante, 3 Rare ou mieux). T35B ajoute les deux derniers champs |
| 21 | UpgradeBuy | biome, sorte (0 Production, 1 Nids douillets, 2 Regard curieux), niveau de départ, niveau d'arrivée, log10 du coût, quantité (0 × 1, 1 × 10, 2 Max). T35A |
| 22 | UpgradeMilestone | biome, sorte, niveau (palier × 2 franchi). T35A |
| 23 | ZoneOpen | zone, biome, rang de l'agrandissement, log10 du coût, minutes depuis l'installation. T35A |
| 24 | BiomeUnlock | biome, niveau du refuge, jours depuis l'installation, minutes de jeu. T35A |
| 25 | BiomeGateSeen | biome, conditions remplies (bits : 1 niveau, 2 espèces, 4 maîtrise, 8 jour, 16 Lumière). T35A |
| 26 | RefugeLevelUp | niveau, score, jours depuis l'installation. T35A |
| 27 | BiomeMasteryUp | biome, niveau. T35A |
| 28 | DecorationBuyLight | décoration, exemplaire, log10 du coût. T35A |
| 29 | TreatBuy | friandise, log10 du coût. T35A |
| 30 | Economy | monnaie, raison, variation (mantisse, exposant), solde (mantisse, exposant). Gemmes seulement |
| 31 | EconomySnapshot | Lumière (mantisse, exposant), production/s (mantisse, exposant), Gemmes, animaux, espèces. Fin de session |
| 32 | EconomyFlow | monnaie, raison, nombre de changements, somme (mantisse, exposant). Fin de session |
| 33 | EggBuy | œuf, quantité (1, 3, 10), œufs achetés avant dans le cycle, log10 du coût, monnaie (0 Lumière, 1 stock Découverte, 2 événement). T35B |
| 34 | MultiHatch | œuf, quantité, résultats Rare ou mieux, nouveautés (espèces ou variantes). T35B |
| 35 | DiscoveryEggGain | source (0 test, 1 calendrier, 2 carnet, 3 expédition, 4 parcours d'événement, 5 cadeau du jour, 6 ouverture de biome, 7 Pass, 8 cadeau de retour), œufs gagnés, stock après. T35B |
| 36 | EggOddsOpen | œuf. T35B |
| 40 | ScreenView | écran |
| 41 | OfferImpression | offre, emplacement. Une fois par offre et par session |
| 42 | OfferClick | offre, emplacement |
| 43 | PurchaseResult | offre, produit, statut, prix de référence (centimes), Gemmes (+ reçues, - dépensées), achats de cette offre, achats réels, drapeaux (1 restauré, 2 nouvel octroi) |
| 44 | Restore | achats rendus |
| 50 | PerfSample | frames, moyenne (ms), p95 (ms), frames lentes (> 33,4 ms), max (ms), mémoire (Mo), palier qualité, économie batterie. Une par minute de jeu, après 3 s de chauffe |
| 60 | OfflineReturn | secondes d'absence, Lumière gagnée (mantisse, exposant). Seulement si un gain réel |

Funnel (doc 09, une fois par profil, enregistré dans la sauvegarde) : 0 TutorialStart, 1 FirstHatch, 2 FirstCollect
(bouton Récupérer du retour hors ligne), 3 FirstUpgrade, 4 FirstNewSpecies (deuxième espèce), 5 FirstRare,
6 BiomeUnlock, 7 CodexOpen, 8 FirstShopExposure, 9 FirstRewardedAd, 10 FirstOfferImpression, 11 FirstPurchase,
12 SecondPurchase. BiomeUnlock, FirstUpgrade et FirstRewardedAd sont prêts mais n'ont pas encore de fonction de jeu
qui les déclenche (pas de deuxième biome, d'amélioration ni de pub récompensée dans la tranche).

Écrans : 1 Refuge, 2 Codex, 3 Fiche Codex, 4 Boutique, 5 Offre, 6 Retour hors ligne.

## Sauvegarde

Schéma 6 : section Analytics (étapes atteintes, nombre de sessions). Migration 0005. Une sauvegarde plus ancienne
déduit les étapes déjà faites (collection, espèces, rareté, Codex vu, boutique, achats) : un joueur existant n'envoie
jamais « première éclosion » après une mise à jour. Sauvegarde de référence `GoldenSave_Analytics_v6`.

## Consentement et vie privée

- Identifiant analytics : aléatoire, créé sur le téléphone (PlayerPrefs), sans lien avec l'appareil ni le profil.
  Remis à zéro si le joueur refuse.
- Inconnu (pas encore demandé) : les lots restent sur le téléphone (64 fichiers au plus), rien ne part.
- Accordé : les lots partent, du plus ancien au plus récent ; un lot envoyé est supprimé, un échec est retenté une
  minute plus tard.
- Refusé : plus rien n'est collecté, la file et les fichiers sont supprimés.
- Aucun nom de contenu, SKU, identifiant de transaction ou texte saisi n'est envoyé.

**À décider par toi :** l'écran de consentement (texte, moment où il apparaît) et le backend (Firebase,
GameAnalytics ou serveur à toi) dépendent de choix juridiques et commerciaux. Tant qu'ils ne sont pas faits, rien
n'est envoyé.

## Performance

- Test `SteadyState_TicksEventsAndBatches_DoNotAllocate` : 5000 frames avec événements, échantillonnage de
  performance et plus de 100 lots encodés : 0 octet alloué.
- Player Windows de développement, 480x1040 (PC, pas un téléphone), comparé à T28 :

| | P2-Mix3 T28 | P2-Mix3 T29 | U1 T28 | U1 T29 |
|---|---|---|---|---|
| Thread principal moyen / max | 1,44 / 2,85 ms | 1,48 / 2,58 ms | 1,26 / 7,13 ms | 1,24 / 7,30 ms |
| Frames au-dessus de 33 ms | 0 | 0 | 0 | 0 |
| Frames avec allocation | 3 | 3 | 14 | 14 |

  Rapport : Docs/Performance/Baselines/T29_player_windows_analytics.json. L'écart est dans le bruit de mesure.

## Branchements

- Bootstrap : étape `Analytics` avant le store (les achats livrés au lancement sont comptés). Session ouverte après
  le calcul hors ligne, fermée à la mise en arrière-plan et à la fermeture (lots écrits avant de rendre la main,
  100 ms au plus).
- `GameLoop` donne la durée brute de chaque frame (non bornée) à l'échantillonneur.
- Présentation : `GameContext.Analytics` (jamais nul). Codex, boutique, offre moment, retour hors ligne, Récupérer.
- Tests : les lots sont écrits à côté de la sauvegarde de test, jamais dans le dossier réel de l'app.
