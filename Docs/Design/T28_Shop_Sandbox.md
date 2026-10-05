# T28 Boutique sandbox

Gemmes factices, catalogue piloté par les données, interface du Starter Pack, emplacements d'offres. Critère : aucun
lien entre l'interface et le fournisseur de paiement. Vérifié par le test `Ui_HasNoLinkToTheStoreProvider`.

## Architecture

```
UI (ShopScreen, OfferModalView, ShopCardView)
  -> IShopModel / IShopActions (Presentation : ids d'offre seulement)
ShopPresenter (Presentation)
  -> StoreService (Services, indépendant du fournisseur)
       -> IStoreProvider : SandboxStoreProvider (éditeur et builds de dev) / UnavailableStoreProvider (release)
       -> OfferService, GrantService (Simulation, purs et testables)
```

- L'interface ne voit que des cartes déjà prêtes et n'envoie que des identifiants d'offre. Aucun type du namespace
  Store n'apparaît dans l'assembly UI ; l'asmdef UI ne référence pas le bac à sable.
- `SandboxStoreProvider` vit dans `Game.Services.StoreSandbox`, compilé seulement si
  `UNITY_EDITOR || DEVELOPMENT_BUILD`. Un build release ne peut donc jamais donner de Gemmes gratuites (compilation
  des scripts player Android release vérifiée : assembly absente, jeu compilé). Le vrai fournisseur (Google Play,
  App Store) se branche dans `GameBootstrap.CreateStoreProvider`.
- Achat : le GrantService attribue une transaction une seule fois (ids traités, déjà sauvegardés depuis T03). La
  transaction n'est confirmée au store qu'une fois qu'un instantané de sauvegarde pris après l'achat est écrit
  (thread d'écriture, jamais d'écriture synchrone pendant le jeu). Un crash entre les deux : le store renvoie la
  transaction, elle est attribuée une seule fois.
- Restauration (nouvel appareil) : les objets permanents reviennent, pas les Gemmes déjà dépensées.
- Les prix affichés viennent toujours du store, localisés (`0,99 €`, `€0.99`). Le catalogue ne contient qu'un prix
  de référence, utilisé par le bac à sable et l'analytics.

## Catalogue (Content/Shop/DEF_ShopCatalog, généré par Pocket Wilds/Shop/Build Sandbox Catalog)

| Offre | Emplacement | Prix (doc 09) | Contenu | Règles |
|---|---|---|---|---|
| Gem pack 1 à 6 | Gemmes | 0,99 / 4,99 / 9,99 / 19,99 / 49,99 / 99,99 € | 100 / 550 / 1150 / 2400 / 6250 / 13000 Gemmes | Bonus +10 % à +30 %, Meilleure offre sur le plus gros |
| Starter Pack | À la une | 1,99 € | 500 Gemmes + Couronne de fleurs | Une fois, 5 animaux et 2 espèces, nouveaux joueurs (non payeurs) |
| Starter Pack (moment) | Après une éclosion | même produit | même contenu | Une fois, revient au plus tous les 3 jours s'il est fermé, jamais pendant l'éclosion |

**À valider par toi :** les quantités de Gemmes et le prix du Starter Pack sont des valeurs de départ. Le doc 09
donne les tranches de prix, pas les quantités. Le « petit bonus permanent de confort » du Starter Pack n'est pas
défini dans les docs : il n'est pas inclus.

La boutique apparaît après l'onboarding (3 animaux). Pas de faux timer : une offre n'affiche une durée que si elle a
une vraie date de fin. Le bonus d'un pack est calculé par rapport au plus petit pack, à l'euro commencé, arrondi
vers le bas : il n'est jamais surestimé.

## Sauvegarde

Schéma 5 : section Shop (achats et affichages par offre, dernière apparition, nombre d'achats réels). Migration
0004, sauvegarde de référence `GoldenSave_Shop_v5`.

## Interface

`PF_UI_Shop`, `PF_UI_OfferModal`, `PF_UI_ProductCard`, `PF_UI_ProductCard_Wide`, raccourci boutique et puce Gemmes
du HUD. Atlas séparé `Atlas_Shop`. Icônes générées (`pw_ui_sprites.py`) : gemme, sac, piles de gemmes par palier,
couronne de fleurs, croix. Textes en anglais et en français, selon la Writing Bible : ce que l'on obtient, le prix,
« Offre unique », aucun mot de pression. Captures : `TestResults/Captures/T28_*.png`.

## Mesures (player Windows de développement, 480x1040, PC et non téléphone)

| | P2-Mix3 | U1 : ouvrir, acheter, fermer, rouvrir |
|---|---|---|
| Frame CPU max | 17,62 ms | 17,25 ms (vsync) |
| Thread principal max | 2,85 ms | 7,13 ms |
| Frames au-dessus de 33 ms | 0 | 0 |
| Frames avec allocation / max | 3 / 9,5 Ko | 14 / 20,8 Ko (frames d'achat) |

- Sans préchauffage, la première ouverture coûtait 36 à 82 ms (texte, mise en page, glyphes de prix). Les écrans
  sont maintenant préparés à l'entrée en scène, derrière l'écran de chargement.
- Avec l'ancienne écriture synchrone de la sauvegarde à l'achat : 13,7 ms. Avec l'écriture en arrière-plan : 7,1 ms.
- Boutique fermée : aucune allocation par frame (test `SteadyState_ShopTicksAndQueries_DoNotAllocate`).

## Tests

EditMode `ShopTests` (16) : catalogue doc 09, textes et art présents, données commerciales invalides refusées,
attribution idempotente, restauration, éligibilité et segments, délai de l'offre moment, fenêtres réelles, prix
localisés, confirmation après sauvegarde, annulation, échec, achat en attente, un achat à la fois, offre en Gemmes,
cartes honnêtes, absence d'allocation, absence de lien UI vers le fournisseur, sauvegarde. PlayMode `ShopPlayTests`
(2) : HUD, boutique, achat au bouton, conservé après redémarrage ; Starter Pack après une éclosion, acheté une fois.

## Reste à faire

- Vrai fournisseur Google Play et App Store : demande l'accès aux consoles et les identifiants produits (ta décision).
- Couronne de fleurs en 3D et fonction pour l'équiper ; contenu cosmétique à vendre en Gemmes.
- Événements analytics de la boutique (T29), pilotage des offres par Remote Config.
- Sons de la boutique (GainGems, OfferOpen) avec l'audio.
