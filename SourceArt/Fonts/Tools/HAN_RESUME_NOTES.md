# Idéogrammes : notes de reprise

Le travail sur les langues et les polices est en pause depuis le 9 octobre 2026 (décision du propriétaire : le jeu
reste en anglais et en français jusqu'à la fin de la production). Ces notes complètent `HAN_BATCHES.md` pour la reprise.

## Règle ajoutée pendant la production des lots

Ne jamais relever de positions sur un rendu de référence (police du système), même arrondies ou réespacées : ce serait
dériver une forme d'une autre police, ce que le propriétaire interdit. Une référence sert seulement à reconnaître les
traits et les composants d'un caractère et sa forme régionale ; le dessin vient de notre code, puis le vérificateur
contrôle l'identité.

## Dessins à refaire de façon indépendante à la reprise

- Lot b02 : **fait le 9 octobre 2026**. 46 caractères et 39 pièces privées redessinés de mémoire, sans référence à
  l'écran (liste en tête de `pw_font_han_ids_b02.txt`), `check b02` sans faux. À la fusion, surveiller 諭 J contre
  輸 (lot b01), perdant de 0,015.
- Lot c06 : la pièce privée `{c06_zhan}` (詹, utilisée par 詹 et 赡).
- Lot c07 : proportions ajustées « à l'œil » d'après les références, à revoir et à redessiner si elles en dépendent :
  sous-boîte de 土 dans 埂 et 坷, boîte de 吕 dans 闾, 亥 dans 阂, et les dessins de 加, 员, 夯, 胡, 庚.
- Lot c05 : positions relevées « à l'œil » avant la règle, à revoir : la place de 尚 dans 趟 et la forme régionale de
  乑 (`{c05_ju_b}`). Structures regardées sur des références agrandies : 凿 临 鼎 黎 瘸 囱 丐 乑 叚 亵 裔 冘 袁 叠 聚 竣
  (identification seulement, à confirmer).
- Lot c08 : 15 caractères retirés parce que leurs premiers dessins venaient de positions relevées sur une référence
  (lignes supprimées, caractères mis en commentaire) : 捷 睫 韭 舅 撅 穆 羌 卿 黍 嚏 熙 焉 彝 叁 舆. À redessiner
  entièrement à la main. Restent aussi 5 caractères perdants (娟 SC, 嘻 SC, 咎 TC, 拈 TC, 橇 TC) et 3 contacts
  (谰, 窿, 廖).
- Règle générale à la reprise, **obligatoire avant toute fusion** : plusieurs lots ont relevé des positions sur des
  références alors que la consigne de départ l'interdisait déjà. Faire relire par un agent neuf tous les dessins privés
  et toutes les compositions des lots ; redessiner ou retirer tout ce qui dépend d'une position relevée, et ne garder
  que des tracés écrits de notre main.

## Caractères à ajouter à la liste tenue à l'écart

- 皈 : son seul sens courant relève de la croyance (signalé par le lot e03). L'ajouter à `han_lists/han_list_held.txt`.
- À examiner : 迦 et 伽 (surtout des transcriptions, dont des noms religieux) et 斋 (sens de jeûne rituel, mais aussi
  « atelier, bureau »), tous trois dessinés dans le lot c07.

## Conventions à unifier

- 声 : 7 traits (lot d01, compte du dictionnaire) contre 5 dans le lot e02. Choisir 7 et l'écrire dans les en-têtes.

## Pièces partagées perdantes signalées par c06

- 巩 (contre 机) et 曼 (contre 量, 夏, 晏) : propositions `{c06_gong}` et `{c06_man}` dans
  `pw_font_han_components_c06.txt`, qui gagnent tous leurs duels.
- Contact restant en Regular dans 瀚 : espacer davantage les deux 习 de `{c06_han_r}`.

## Corrections prêtes, non appliquées (lot c07)

- 铳 : contact en Regular. Faire démarrer plus bas le tournant de 厶 dans `{c07_chong}` (vers y 26).
- 钒 : la variante `{c07_fan3}` (dans `TestResults/Fonts/Han/lots/c07/try15.txt`) le met au rang 1 sans contact.

## Lot d02 (arrêté avec 250 caractères dessinés, 0 faux)

- Contacts à corriger : Regular 墮 黏 廣 龐 遞 ; SemiBold 鷗 鴻 鵬 鄭 龐 ; Bold et Plush, surtout les quatre points de 鳥
  à droite (le moteur raccourcit les points avant de les amincir), plus 墊 et 闡.
- 廣 reste `*` : réparer la tête de 黃 partagée sous 广 (`{d02_huang}` en candidat).
- Colonne des traits : réécrire 24 lignes en comptes dessinés.
- Mises en page inspirées d'une référence, à redessiner de mémoire : le long trait de 黽, le long haut de 扁, 犀, 虒,
  黍.
