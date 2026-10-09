# Plan des langues

Version du 9 octobre 2026. Auteur : agent LA (architecture de la localisation), pour le propriétaire.

> **En pause (décision du propriétaire, 9 octobre 2026) : « on peut continuer juste anglais et francais jusqu'a avoir
> fini le jeu ».** Le jeu reste en anglais et en français jusqu'à la fin de la production. Les autres langues, leur
> socle (T39B), les polices dans Unity (T39D) et les vagues (T39E à T39G) reprennent après la dernière tâche de la file.
> La règle « toute tâche qui ajoute des textes les traduit dans toutes les langues » ne s'applique qu'à l'anglais et
> au français d'ici là.
>
> **Ce qui est déjà fait et conservé** :
> - les polices de secours construites par notre code dans `SourceArt/Fonts/Build/` : cyrillique et grec, vietnamien,
>   thaï, kana, ponctuation chinoise, hangul complet, et les premiers idéogrammes JP, SC et TC ;
> - les idéogrammes en cours : lots a01 à a03, b01 à b03, c01 à c04, d01, e01 et e02 finis ; c05 à c08, d02 et e03
>   arrêtés en cours ; c09 et d03 non commencés. L'état précis est dans `SourceArt/Fonts/Tools/HAN_BATCHES.md` ;
> - les brouillons des 18 langues, avec leurs lexiques et leurs listes de mots interdits, dans `Localization/` (à
>   retraduire pour les textes ajoutés d'ici la reprise) ;
> - les prototypes non branchés `LanguageMatcher`, `DeviceLanguages` et `PluralRules`, avec leurs tests ;
> - la correction des accents du latin livré (Ã Õ Å ť…), déjà dans nos polices.

Ce plan remplace la section 2 « Langues » de « 00 : Décisions encore ouvertes, propositions » (Drive). Il suit les
souhaits du propriétaire du 9 octobre 2026 :

- le jeu sort dans beaucoup de langues, les plus rentables pour un jeu mignon et doux de collection ;
- chaque texte s'affiche bien dans chaque langue, et les traductions sont bonnes ;
- aucun choix de langue au premier lancement : la langue du téléphone, l'anglais si elle n'est pas prise en charge,
  et un réglage pour la changer ensuite ;
- les polices : « prend pas une autre police mais utilise notre police juste rajoute les caracteres necessaire ».
  Pocket Round et Pocket Plush sont étendues, par notre code, à toutes les écritures. Aucune police du système,
  aucune police libre, aucune donnée tirée d'une police, aucun téléchargement de police.

La première partie est écrite pour le propriétaire, en mots simples. La seconde est technique.

---

# Partie 1 : en mots simples

## 1.1 En bref

- **18 langues au total (19 avec le néerlandais en option)**, l'anglais et le français compris, ajoutées en trois vagues et classées par ce qu'elles rapportent à un jeu comme le nôtre. Le japonais
  arrive juste après l'anglais en valeur : les jeux mignons de collection y font souvent plus de la moitié de leurs
  revenus.
- **Le jeu choisit la langue tout seul**, à chaque lancement, en lisant la liste des langues du téléphone dans
  l'ordre. Exemple : un Suisse qui a « suisse allemand » puis « allemand » voit le jeu en allemand. Un Portugais voit
  le portugais du Brésil. Personne ne voit un écran de choix. Le joueur peut changer la langue dans les réglages, sans
  redémarrer le jeu.
- **Les polices restent les nôtres partout.** Cinq agents dessinent en ce moment le cyrillique, le grec, le
  vietnamien, le coréen, les kana japonais, le thaï et les caractères chinois et japonais, dans le style de Pocket
  Round et Pocket Plush. C'est le plus gros chantier du plan, surtout pour les caractères chinois et japonais.
- **Les traductions sont faites par IA, relues par une autre IA, puis vérifiées par des tests automatiques.** Pour les
  marchés les plus importants, une personne de langue maternelle relit la fiche du store, le tutoriel et les dix
  premières minutes avant la sortie : environ 200 à 350 dollars par langue.
- **Chaque texte est mesuré dans sa vraie case**, avec sa vraie police, dans chaque langue. Un texte qui déborde ou un
  caractère qui manque fait échouer les tests avant le commit, comme pour les règles du vocabulaire et de l'étincelle.
- **La règle du vocabulaire vaut dans toutes les langues.** Chaque langue reçoit sa liste de mots interdits et son
  glossaire (le refuge, les raretés, les monnaies, les espèces).
- **L'arabe et l'hindi ne sont pas prévus avant la V1.** L'arabe pourrait venir après, si les chiffres du soft launch
  le justifient. L'hindi, non.
- Le code de base du choix de langue est prêt (prototype non branché, testé). Le branchement est la tâche T39B,
  juste après T39.

## 1.2 Les langues, de la plus rentable à la moins rentable

Chiffres de 2025 (revenus des achats dans les jeux mobiles, par pays). Les sources sont en fin de document.

| Rang | Langue | Marchés principaux | Pourquoi | Vague |
|---|---|---|---|---|
| 1 | Anglais | États-Unis (24,2 Md$), Royaume-Uni, Canada, Australie | Le plus grand marché ouvert. Langue de secours pour tous | Déjà là |
| 2 | Japonais | Japon (12 Md$, 209 $ par joueur) | Le joueur qui dépense le plus au monde. Les jeux mignons de collection y font 67 à 73 % de leurs revenus (Pocket Camp, Pokémon Sleep) | B |
| 3 | Allemand | Allemagne (2,4 Md$), Autriche, Suisse | 5e marché, en hausse de 5 % en 2025. Les joueurs paient plus volontiers dans leur langue | A |
| 4 | Coréen | Corée du Sud (4,9 Md$, 179 $ par joueur) | Grand marché, joueurs fidèles. Les jeux doux de chats y marchent bien | B |
| 5 | Français | France (1,35 Md$), Canada, Belgique, Suisse, Afrique | Déjà là. Obligatoire au Québec | Déjà là |
| 6 | Chinois traditionnel | Taïwan (1,4 Md$, 93 $ par joueur), Hong Kong (0,4 Md$) | Joueurs qui dépensent beaucoup et aiment le mignon. 3e marché de Pokémon Sleep | B |
| 7 | Espagnol | Mexique (1,25 Md$), Espagne (0,57 Md$), Amérique latine, Hispaniques des États-Unis | Énorme public. Le Mexique seul rapporte deux fois plus que l'Espagne | A |
| 8 | Portugais du Brésil | Brésil (1,4 Md$, 93 millions de joueurs) | 3e pays au monde en téléchargements | A |
| 9 | Italien | Italie (1 Md$) | Marché européen solide | A |
| 10 | Indonésien | Indonésie (1,6 Md$, 144 millions de joueurs) | 4e pays en téléchargements, en hausse de 8 % | A |
| 11 | Turc | Turquie (0,9 Md$, 44 millions de joueurs) | Marché qui grandit vite, préfère sa langue | A |
| 12 | Thaï | Thaïlande (0,85 Md$, 37 millions de joueurs) | Bon marché pour le mignon. Écriture difficile, mais faisable avec nos polices | C |
| 13 | Chinois simplifié | Singapour, Malaisie, Chinois de l'étranger | La Chine continentale demande une licence de jeu et n'a pas Google Play : non visée. Réutilise le travail des caractères chinois | B (après le traditionnel) |
| 14 | Polonais | Pologne (0,55 Md$) | Déjà couvert par nos polices | A |
| 15 | Vietnamien | Vietnam (0,57 Md$, 53 millions de joueurs) | Beaucoup de joueurs, peu de dépense par joueur. Extension de notre latin | C |
| 16 | Russe | Russophones de nombreux pays | Beaucoup de téléchargements, mais paiements des stores très limités en Russie depuis 2022 | C |
| 17 | Ukrainien | Ukraine | Forte hausse des téléchargements, petit revenu. Fichier propre, jamais dérivé du russe | C |
| 18 | Néerlandais | Pays-Bas (0,52 Md$), Belgique | Les joueurs lisent très bien l'anglais. Peu cher (latin) : en option | A (option) |
| 19 | Grec | Grèce | Petit marché. Peu cher une fois le grec dessiné | C |
| 20 | Arabe | Arabie saoudite (0,87 Md$), Émirats (0,35 Md$) | Valeur réelle, mais écriture de droite à gauche et lettres qui changent de forme : gros travail. Voir 1.8 | Plus tard, peut-être |
| - | Hindi | Inde (1,4 Md$ mais 3,5 $ par joueur) | Les joueurs indiens jouent surtout en anglais. Voir 1.8 | Non |

**Espagnol : une seule langue écrite au départ**, un espagnol neutre d'Amérique latine (code es-419), montré à tous
les téléphones en espagnol, Espagne comprise. Deux fiches de store dès le départ : Espagne et Mexique. Si les
chiffres de l'Espagne le demandent plus tard, un petit fichier « espagnol d'Espagne » ne remplacera que les phrases
qui changent.

**Portugais : seulement celui du Brésil.** Les joueurs du Portugal le reçoivent, ce qui vaut mieux que l'anglais.

## 1.3 Les trois vagues

| Vague | Langues | Ce qu'il faut avant | Moment proposé |
|---|---|---|---|
| Socle | (aucune nouvelle langue) | T39B : choix automatique, fichiers de textes, glossaires, tests | Juste après T39 |
| A : latin | Allemand, espagnol, portugais du Brésil, italien, indonésien, turc, polonais (+ néerlandais en option) | Le socle. Nos polices couvrent déjà ces langues | Dès que le socle est fait, puis à chaque nouvelle tâche |
| B : Asie de l'Est | Japonais, coréen, chinois traditionnel, puis chinois simplifié | Les polices T39C (coréen, kana, caractères chinois) et leur intégration T39D | Dès que les polices de chaque langue sont prêtes, le japonais en premier |
| C : extension | Thaï, vietnamien, russe, ukrainien, grec | Les polices T39C (cyrillique, grec, vietnamien, thaï) | Après la vague B, ou en même temps si les polices sont prêtes |

Une fois une langue ajoutée, elle ne prend jamais de retard : chaque tâche qui ajoute des textes les traduit dans
toutes les langues déjà livrées avant d'être terminée.

La relecture humaine (1.7) a lieu avant le soft launch pour les langues des pays du soft launch, et avant la sortie
mondiale pour les autres.

## 1.4 Les polices : tout fait main

Le propriétaire a refusé toute autre police. Nos deux familles reçoivent donc les caractères de chaque langue,
dessinés par notre code, dans notre style.

| Écriture | Langues | Nombre de signes à dessiner par style (estimation) | Difficulté |
|---|---|---|---|
| Latin complété | Vietnamien, quelques lettres manquantes (ẞ, Ơ, Ư) | Environ 95 | Faible : les accents empilés du vietnamien demandent une règle de placement |
| Cyrillique | Russe, ukrainien (et serbe, bulgare, biélorusse) | Environ 115, dont une trentaine reprise du latin | Moyenne |
| Grec | Grec | Environ 75, dont une quinzaine reprise du latin | Moyenne |
| Thaï | Thaï | Environ 100 | Moyenne : les petits signes se posent au-dessus et en dessous des lettres |
| Kana et ponctuation | Japonais (et ponctuation chinoise) | Environ 350 | Moyenne |
| Hangul | Coréen | 11 172 syllabes, toutes composées par notre code à partir des 67 lettres de base (jamo), en plusieurs variantes selon leur place | Moyenne : beaucoup de signes, mais une seule méthode |
| Caractères chinois et japonais | Japonais, chinois traditionnel, chinois simplifié | 2 000 à 5 000 par région, avec des formes un peu différentes au Japon, en Chine et à Taïwan | Très forte : c'est le long chantier |

**Pour les caractères chinois et japonais**, on dessine d'abord ceux dont les textes du jeu ont besoin (un outil les
liste à partir des traductions), puis les caractères courants, pour les noms que les joueurs écrivent. Les premiers
500 sont en cours.

**Ce que cela coûte au téléphone.** Les caractères chinois et japonais sont nombreux : un téléphone ne charge que les
polices de la langue choisie, jamais les autres. Un joueur japonais ne télécharge ni ne charge le coréen. La taille
et la mémoire seront mesurées pendant l'intégration (T39D) ; si elles dépassent le budget, on garde moins de graisses
pour ces écritures (par exemple sans la SemiBold).

**Une question pour le propriétaire** (voir 1.9) : les polices d'Asie de l'Est peuvent être dans l'application dès
l'installation (plus simple, application plus lourde de 10 à 25 Mo environ), ou livrées par le store juste après
l'installation, seulement pour la langue du joueur (application plus légère, ce sont nos propres fichiers, mais cela
fait un petit téléchargement depuis le store).

## 1.5 La langue au premier lancement

Le jeu lit la liste des langues du téléphone, dans l'ordre choisi par le joueur, et prend la première qu'il sait
afficher. Si aucune ne va, il prend l'anglais. Quelques exemples :

| Langues du téléphone, dans l'ordre | Langue du jeu |
|---|---|
| Français (Canada) | Français |
| Portugais (Portugal), anglais | Portugais du Brésil |
| Espagnol (Mexique) | Espagnol d'Amérique latine |
| Chinois (Hong Kong) | Chinois traditionnel |
| Suisse allemand, anglais | Allemand |
| Catalan, espagnol (Espagne) | Espagnol |
| Swahili | Anglais |

- À chaque lancement, le jeu suit le téléphone, tant que le joueur n'a pas choisi une langue dans les réglages.
- Dans les réglages : « Langue de l'appareil » en premier, puis chaque langue écrite dans sa propre langue
  (« Deutsch », « Español », « 日本語 », « 한국어 », « 繁體中文 »).
- Changer de langue ne redémarre pas le jeu : l'écran se redessine dans la nouvelle langue en moins d'une seconde.
- Android 13 et plus, iOS 13 et plus : le joueur peut aussi choisir la langue du jeu dans les réglages du téléphone.
  Le jeu la lit en premier.

## 1.6 Des textes beaux dans chaque langue

- **Mode pseudo-langue** : un faux langage, 40 % plus long, plein d'accents, entre crochets. On joue avec pour voir
  tout de suite ce qui déborde, avant même d'avoir des traductions.
- **Test de chaque texte dans sa case** : chaque phrase est posée dans sa vraie case, avec sa vraie police, à la plus
  petite taille permise. Si elle déborde ou si un caractère manque, le test échoue.
- **Tailles et interlignes par écriture** : le chinois, le japonais et le coréen sont un peu plus grands dans les
  petits textes ; le thaï et le vietnamien, dont les accents s'empilent, ont plus d'interligne.
- **Captures de tous les écrans dans chaque langue**, sur le plus petit téléphone visé, pour la relecture.

## 1.7 Des traductions de qualité

1. Les textes sortent du code et vont dans un fichier par langue. Chaque phrase a son contexte : où elle s'affiche,
   sa longueur maximale, le ton.
2. Chaque langue a un glossaire (le refuge, les raretés, les monnaies, les espèces, les activités) et sa liste de
   mots interdits (règle du vocabulaire). Exemple : en espagnol, en italien et en portugais, le mot le plus naturel
   pour un refuge d'animaux a une racine religieuse ; le glossaire impose « refugio », « rifugio » et « refúgio ».
3. Une IA traduit, avec tout le contexte, des captures d'écran et le glossaire.
4. Une autre passe d'IA relit chaque langue : retraduction vers l'anglais, glossaire, ton, longueur.
5. Des tests automatiques vérifient tout ce qui se vérifie : les {n} et autres variables, les pluriels, la longueur,
   les mots interdits, l'absence d'étincelle, les caractères présents dans nos polices.
6. Pour les marchés principaux, une personne de langue maternelle relit la fiche du store, le tutoriel et les dix
   premières minutes : environ 2 000 mots par langue, soit 200 à 350 dollars par langue avec un test en jeu. Pour le
   japonais, le marché le plus exigeant, une relecture complète du jeu est conseillée avant la sortie mondiale (2 000
   à 3 000 dollars).

## 1.8 Arabe et hindi : notre avis

- **Arabe : plus tard, peut-être.** Il faudrait dessiner environ 150 signes par style (les lettres changent de forme
  selon leur place dans le mot), écrire notre propre moteur de formes et de sens d'écriture (TextMesh Pro ne le fait
  pas), et surtout retourner toute l'interface de droite à gauche. Environ 6 à 9 semaines de travail en tout. À
  revoir après le soft launch, si des joueurs du Golfe arrivent. Les joueurs du Golfe lisent souvent l'anglais.
- **Hindi : non.** L'écriture demande des centaines de ligatures et un moteur de mise en forme complet. Les joueurs
  indiens dépensent peu (3,5 dollars par an en moyenne) et jouent surtout en anglais.

## 1.9 Ce que le propriétaire doit décider

1. **L'ordre des vagues** proposé en 1.3 (japonais avant les autres langues d'Asie, vague A dès le socle).
2. **La livraison des polices d'Asie de l'Est** : dans l'application dès l'installation, ou livrées par le store
   après l'installation, seulement pour la langue du joueur (1.4).
3. **Les listes de caractères courants** pour les noms que les joueurs écrivent (japonais, chinois) : ce sont des
   listes officielles de caractères, du texte seulement, sans aucun dessin. Les importer est-il permis, alors que la
   règle dit « aucun téléchargement » ? Sinon, on ne dessine que les caractères des textes du jeu et on limite les
   noms à ces caractères.
4. **Le budget de relecture humaine** : 200 à 350 dollars par langue pour les marchés principaux, plus la relecture
   complète du japonais.
5. **L'espagnol** : un seul espagnol neutre d'Amérique latine au départ, comme proposé.

---

# Partie 2 : technique

## 2.1 L'état actuel

| Élément | Où | État |
|---|---|---|
| Service | `Services/Localization/LocalizationService.cs` | Langue sauvegardée, sinon `Application.systemLanguage` mappé sur un code à deux lettres si une table existe, sinon « en ». Construit une fois au boot, ne change pas ensuite |
| Interface | `Core/Services/ILocalization.cs` | `Get`, `Has`, `GetPlural` avec deux formes « .one » et « .other » (`PluralForms`, en et fr justes) |
| Tables | `Content/Localization/LOC_en.asset`, `LOC_fr.asset` | Remplies par les pipelines depuis des tables C# (`UiPipeline.Texts`, `ActivityTexts`, `DailyVisitTexts`...) |
| Réglage | `PlayerState.Settings.LanguageCode` | Sauvegardé, aucun écran ne le règle encore |
| Polices | `SourceArt/Fonts`, `Editor/UI/FontPipeline.cs` | Pocket Round (3 graisses) et Pocket Plush (Black), 368 caractères, un atlas SDF statique de 1 024 x 1 024 par graisse, aucune police de repli |
| Rôles | `UI/Scripts/Common/TextStyleSheet.cs` | Tailles minimales par rôle, minimum lisible 36 unités (14 dp) |
| Garde-fou | `FeedbackScreenTests.Labels_SurviveThirtyPercentLongerText` | Liste écrite à la main de libellés, texte le plus long + 30 % |
| Lecture des textes | Les écrans | `Bind` à l'ouverture (`text.Get(...)`), jamais par image. `DailyVisitTextFormatter` vide ses caches quand la langue change (`EnsureLanguage`) |

**Ce que l'étude a trouvé** (à corriger dans T39B) :

1. **Codes « fr » écrits en dur** pour les formats : `RefugeController` (deux fois), `ProgressionPresenter`,
   `ShopText.GroupSeparator`, `SandboxStoreProvider`, `DailyVisitTextFormatter._french`. À remplacer par un format par
   langue (`LocaleFormat`, 2.6).
2. **Articles dans les variables** : `{species_the}` et `{biome_the}` (« le Lapin », « la Prairie »), avec les
   contractions françaises écrites dans le code (« de le » donne « du »). Cela ne tiendra ni en allemand (cas), ni en
   polonais, russe, ukrainien, grec ou turc. Remplacement proposé : des formes de noms par langue (2.6).
3. **Ordinal anglais** : `egg.odds.guarantee` dit « by the {0}th egg » (faux pour 1, 2, 3). Reformuler sans ordinal.
4. **Le test du guide d'écriture** (`LocalizationTests.ShippedText_FollowsTheWritingGuide`) interdit le point-virgule
   partout (or le point d'interrogation grec est « ; », U+037E, que la normalisation Unicode change en U+003B) et
   compte les mots des boutons avec les espaces (sans effet en chinois, japonais et thaï). À rendre dépendant de la
   langue.
5. **TextMesh Pro** : `m_UseModernHangulLineBreakingRules: 0` (le coréen moderne coupe aux espaces, il faut 1) ;
   seul le crénage est actif (`m_ActiveFontFeatures` ne contient que `kern`) : le thaï a besoin de `mark` et
   `mkmk`.
6. **Prix des stores** : le prix formaté par Google Play ou l'App Store suit la langue du téléphone, pas celle du jeu.
   Un joueur saoudien avec un téléphone en arabe et le jeu en anglais recevrait des chiffres et une monnaie en
   lettres arabes, absentes de nos polices. Les prix doivent être formatés par le jeu à partir du montant et du code
   ISO de la monnaie, dans la langue du jeu, avec le symbole si nos polices l'ont, sinon le code (« SAR 3.99 »).

## 2.2 Données de marché

| Pays | Revenus 2025 | Joueurs | Revenu par joueur |
|---|---|---|---|
| Chine | 37,6 Md$ | 652 M | 57,65 $ |
| États-Unis | 24,2 Md$ | 161 M | 150,84 $ |
| Japon | 12 Md$ | 57,1 M | 209,26 $ |
| Corée du Sud | 4,94 Md$ | 27,7 M | 178,74 $ |
| Allemagne | 2,43 Md$ | 35,9 M | 67,86 $ |
| Royaume-Uni | 2,42 Md$ | 30,8 M | 78,78 $ |
| Indonésie | 1,61 Md$ | 144 M | 11,20 $ |
| Inde | 1,42 Md$ | 408 M | 3,47 $ |
| Brésil | 1,41 Md$ | 93,3 M | 15,11 $ |
| Taïwan | 1,37 Md$ | 14,7 M | 92,89 $ |
| France | 1,35 Md$ | 26,6 M | 50,71 $ |
| Mexique | 1,25 Md$ | 64,7 M | 19,27 $ |
| Italie | 996 M$ | 25 M | 39,87 $ |
| Turquie | 899 M$ | 43,9 M | 20,51 $ |
| Arabie saoudite | 866 M$ | 20,1 M | 43,11 $ |
| Thaïlande | 853 M$ | 37 M | 23,04 $ |
| Espagne | 570 M$ | 25,5 M | 22,40 $ |
| Vietnam | 565 M$ | 53,2 M | 10,63 $ |
| Pologne | 554 M$ | 15,5 M | 35,68 $ |
| Pays-Bas | 524 M$ | 7,92 M | 66,24 $ |
| Hong Kong | 394 M$ | 4,12 M | 95,65 $ |

Source : Allcorrect, Mobile Game Market Index 2025 (achats intégrés App Store et Google Play ; la Russie est exclue).
Compilation d'un prestataire, méthode non publiée : un ordre de grandeur, pas une vérité.

Autres données :

- **Téléchargements 2025** (AppMagic) : Inde, États-Unis, Brésil, Indonésie (+8 %), Russie. Revenus : États-Unis,
  Chine, Japon, Corée du Sud (-12 %), Allemagne (+5 %). Turquie, Argentine et Colombie parmi les plus fortes
  croissances de revenus.
- **Japon** (Sensor Tower, rapport 2026) : plus de 10 Md$ d'achats sur les 12 mois finis en juillet 2026, presque
  quatre fois Taïwan, plus du double de la Corée ; puzzle en tête des téléchargements.
- **Jeux mignons et doux** (pas de catégorie « cosy » dans les outils d'analyse : on regarde des jeux proches) :
  - Pokémon Sleep (AppMagic, février 2025) : Japon 73 % des dépenses, États-Unis 14 %, Taïwan 3 % ; 44 % des
    joueurs au Japon, 7 % à Taïwan.
  - Animal Crossing: Pocket Camp (Sensor Tower, novembre 2021) : Japon 67,3 % des revenus, États-Unis environ 21 %,
    Corée du Sud 1,8 % ; téléchargements : États-Unis 20,4 %, Japon 7,8 %, France 6,3 %.
  - Ces deux jeux portent une licence japonaise très forte, ce qui grossit la part du Japon. La tendance reste nette :
    pour un jeu mignon de collection, le japonais vaut plus que toute autre langue après l'anglais.
- **Stores** : l'App Store propose 50 langues de fiche, dont Espagnol (Mexique) et Espagnol (Espagne), Portugais
  (Brésil) et Portugais (Portugal), Chinois simplifié et traditionnel. Google Play propose es-419, es-ES, es-US,
  zh-CN, zh-TW, zh-HK.

## 2.3 Choisir la langue sans demander

### Lire les langues du téléphone, dans l'ordre

`Application.systemLanguage` ne donne qu'une langue et ne distingue ni pt-BR de pt-PT, ni es-ES de es-419, ni la
deuxième langue d'un Suisse. Le prototype `Services/Localization/DeviceLanguages.cs` lit la liste ordonnée :

| Plateforme | Source | Détail |
|---|---|---|
| Android 13+ | `LocaleManager.getApplicationLocales()` puis `getSystemLocales()` (JNI, `getSystemService("locale")`) | La langue choisie pour l'app dans les réglages du téléphone vient en premier |
| Android 8 à 12 | `LocaleList.getDefault().toLanguageTags()` | Le minimum du projet est l'API 26 : `LocaleList` (API 24) est toujours là |
| iOS 15+ | `[NSLocale preferredLanguages]` par `Plugins/iOS/PWLanguages.mm` (`DllImport("__Internal")`) | Depuis iOS 13, commence par la langue choisie pour l'app |
| Éditeur, PC | `CultureInfo.CurrentUICulture`, puis `Application.systemLanguage` | |
| Secours | `Application.systemLanguage` mappé en étiquette (zh-Hans, zh-Hant, pt...) | Si l'OS ne donne rien |

Lue une fois, mise en cache : aucune allocation après le premier appel. `Refresh()` relit (retour au premier plan),
`SetOverride()` sert aux tests, au mode pseudo-langue et à un menu de test.

**Langue par app** :

- Android 13+ : déclarer `res/xml/locales_config.xml` et `android:localeConfig` dans le manifeste (une
  `.androidlib` comme `PocketWildsHaptics.androidlib`). Sans cette déclaration, le téléphone ne propose pas de langue
  par app. Le choix du joueur dans le jeu peut être recopié avec `LocaleManager.setApplicationLocales` pour que les
  réglages du téléphone l'affichent. L'activité Unity déclare `locale|layoutDirection` dans `configChanges` (à
  vérifier dans le manifeste généré) : pas de recréation.
- iOS : un script de post-build écrit `CFBundleLocalizations` dans `Info.plist` (liste des langues livrées). Cela
  affiche aussi les langues sur la page de l'App Store.

### Les règles de correspondance

Le prototype `Services/Localization/LanguageMatcher.cs` (C# pur) prend la liste du téléphone et les tables livrées.
Pour chaque langue du téléphone, dans l'ordre, la première qu'une table sert gagne :

1. même langue, écriture et région (`zh-Hant-TW` pour une table `zh-TW`) : score 100 ;
2. groupe régional : l'espagnol d'Amérique latine et des États-Unis va vers `es-419` (es-MX, es-AR, es-US) : 90 ;
3. la table de la langue seule (`de` pour de-CH, `fr` pour fr-CA, `zh-Hant` pour zh-HK, zh-TW et zh-MO,
   `zh-Hans` pour zh-CN et zh-SG) : 80 ;
4. la région probable (CLDR) d'une langue sans région (`es` va vers `es-ES`, `pt` vers `pt-BR`) : 70 ;
5. une autre région de la même langue et écriture (`pt-PT` va vers `pt-BR`) : 50 ;
6. une langue proche qui lit la même écriture (suisse allemand vers allemand, nynorsk vers bokmål) : 30.

Seulement si aucune langue du téléphone ne passe ainsi, un lecteur de chinois reçoit l'autre écriture chinoise
plutôt que l'anglais (score 10, deuxième passe). Puis l'anglais, sinon la première table.

Les étiquettes sont lues en minuscules ou majuscules, avec « - » ou « _ », sans les extensions (`-u-...`) ; les
anciens codes Android sont convertis (`in` vers `id`, `iw` vers `he`, `no` vers `nb`, `tl` vers `fil`) ; le
cantonais (`yue`) lit le chinois traditionnel.

`LanguageMatcher.Resolve(saved, device, tables)` : le choix sauvegardé gagne s'il est servi (une ancienne sauvegarde
« pt » ouvre pt-BR, « zh » ouvre zh-Hans) ; un choix vide veut dire « langue de l'appareil ».

### Changer de langue sans redémarrer

Faisable avec la lecture actuelle des textes (les écrans lisent leurs libellés à l'ouverture) :

1. `LocalizationService.SetLanguage(code)` recharge les deux dictionnaires (allocation au changement seulement) et
   lève `ILocalization.Changed`.
2. Les polices de la nouvelle langue sont chargées (Addressables) avant l'affichage, celles de l'ancienne libérées.
3. Les vues persistantes (HUD, bulles de souhaits, cartes) se réabonnent et refont leur `Bind` ; les écrans ouverts
   se ferment et se rouvrent derrière un court fondu ; l'écran des réglages se rouvre dans la nouvelle langue.
4. Test PlayMode : après le changement, aucun texte visible n'appartient encore à l'ancienne langue.

### L'écran des réglages

- « Langue de l'appareil » en premier, avec la langue retenue entre parenthèses.
- Chaque langue s'écrit dans sa propre langue. Comme seules les polices de la langue active sont chargées, les noms
  des langues viennent d'un petit atlas statique « noms des langues », construit depuis nos polices par
  `FontPipeline` (environ 60 signes de toutes les écritures), toujours chargé.

## 2.4 Les polices dans Unity

### Architecture

- **Noyau** (chaque graisse, toujours chargé, comme aujourd'hui) : latin et symboles, atlas statique.
- **Une police de repli par écriture et par région**, chargée seulement quand la langue active en a besoin :
  cyrillique, grec, latin vietnamien, thaï, kana et ponctuation JP / SC / TC, hangul, caractères chinois JP / SC / TC.
- La construction (`pw_font_build.py`, avec le crochet de fusion des modules `pw_font_<écriture>.py`) produit, par
  style, la police noyau et un fichier TTF par écriture et par région, de la même famille, plus une police complète
  pour les épreuves.
- **Petites écritures** (cyrillique, grec, vietnamien, thaï, kana) : atlas statiques construits dans l'éditeur,
  comme aujourd'hui ; aucun coût pendant le jeu.
- **Grandes écritures** (hangul, caractères chinois) : assets TMP dynamiques construits depuis nos TTF
  (`AtlasPopulationMode.Dynamic`, multi-atlas, `clearDynamicDataOnBuild`), dans un groupe Addressables par langue.
  Au chargement de la langue : ajout dans `fallbackFontAssetTable` des polices noyau, puis préchauffage des
  caractères des textes de la langue (liste de l'extracteur, 2.5) par `TryAddCharacters`, réparti sur les images du
  boot. Pendant le jeu, seuls les noms écrits par les joueurs peuvent ajouter des glyphes.
- **Pas de `locl` dans TextMesh Pro.** TMP (com.unity.ugui 2.0, TextCore 1.5 et 1.6) lit le crénage, `mark`,
  `mkmk` et les ligatures, mais pas les substitutions simples ni contextuelles. Les formes régionales des caractères
  chinois et de la ponctuation (centrée à Taïwan, en bas à gauche au Japon et en Chine) viennent donc de fichiers
  séparés par région, chacun avec ses propres formes sous les mêmes codes. Il faut le dire aux agents des polices.
- **Thaï** : positions des signes par GPOS `mark` et `mkmk` (lus par TMP), activés dans les réglages TMP et sur les
  textes. Ce que TMP ne fait pas (ญ et ฐ sans leur jambage sous une voyelle basse, décomposition de ำ devant un signe
  de ton) est fait à l'import des traductions : glyphes de remplacement en zone privée de notre police. Les coupures
  de mots sont des U+200B insérés dans les traductions (TMP coupe sur U+200B sans l'afficher).

### Garde-fous mesurés dans T39D

| Mesure | Budget proposé | Si dépassé |
|---|---|---|
| Mémoire des polices d'une langue d'Asie de l'Est (TTF chargés + atlas, toutes graisses) | 16 Mo sur le palier bas | Moins de graisses pour ces écritures (la Caption en SemiBold prend la Regular), échantillonnage plus petit pour les textes courants |
| Préchauffage des caractères des textes | 300 ms sur le palier bas (estimé depuis le PC, mesuré en T66) | Passer la partie « textes du jeu » en atlas statique cuit dans l'éditeur (toujours depuis nos TTF), garder le dynamique pour les noms |
| Taille ajoutée à l'installation | 10 à 25 Mo pour toute l'Asie de l'Est | Décision 1.9 : livraison par le store après installation (Play Asset Delivery, On-Demand Resources) |
| Allocation pendant le jeu due aux textes | 0 hors ouverture d'écran | Préchauffage plus large |

Estimations de taille par style, à vérifier : Latin actuel environ 550 octets par glyphe (201 Ko pour 368 signes).
Hangul en glyphes composites (chaque syllabe référence ses pièces) : moins de 1 Mo ; en contours pleins : 4 à 13 Mo.
Les glyphes composites sont fortement conseillés à l'agent du coréen. Caractères chinois : 1,5 à 2,5 Ko par glyphe,
donc 5 à 12 Mo par région et par style ; à réduire (points simplifiés, trois graisses) et à compresser dans les
bundles.

### Mise en page par écriture

| Écriture | Taille minimale des petits rôles (Body, Label, Caption) | Interligne | Coupure de ligne |
|---|---|---|---|
| Latin, cyrillique, grec | 36 (aujourd'hui) | 1,0 | Espaces |
| Vietnamien | 36 | +15 % (accents empilés, Ớ) | Espaces |
| Coréen | 38 | +15 % | Espaces (`useModernHangulLineBreakingRules = true`) |
| Japonais, chinois | 40 (un cran au-dessus, comme Material et Flutter le font pour les écritures denses) | +15 à 20 % (Spectrum : 1,7 contre 1,5) | Tout caractère, avec les règles de début et de fin de ligne de TMP (kinsoku, listes à relire) |
| Thaï | 40 | +20 % et marge haute (signes au-dessus) | U+200B entre les mots |

Ces valeurs vont dans `TextStyleSheet` sous forme de réglages par écriture, appliqués au chargement de la langue.

### Signes absents et noms écrits par les joueurs

- Les textes livrés sont couverts à 100 % : c'est un test (2.7).
- Les noms d'animaux (succès « Noms doux ») et, plus tard, les noms affichés des Cercles (T70) : le champ de saisie
  refuse un caractère absent de nos polices (`onValidateInput`), avec un message doux. Le serveur fait le même
  contrôle pour les noms affichés aux autres joueurs.
- `missingGlyphCharacter` reste le carré : il ne doit jamais apparaître, les tests le garantissent.

## 2.5 Jeux de caractères et extracteur

- **Extracteur** (`Tools/loc_charset.py`, ou un outil d'éditeur) : lit les fichiers de traduction et écrit, par
  langue, `SourceArt/Fonts/Charsets/<langue>_game.txt` : chaque caractère, sa fréquence et les premières clés où il
  apparaît. Il compare avec la table des caractères de nos polices et sort la liste « à dessiner d'abord ».
- **Démarrage** : une première traduction IA des 617 textes actuels dans toutes les langues prévues, faite tout de
  suite (avant toute relecture), sert à nourrir l'extracteur et les tests de place.
- **Jeux livrés** :
  - hangul : les 11 172 syllabes et les 51 jamo modernes de compatibilité (noms) ;
  - kana : complets (hiragana, katakana, petits kana, marques de son), ponctuation CJK et formes pleine chasse
    utilisées ;
  - caractères chinois : d'abord ceux des textes du jeu (environ 800 à 1 200 par langue aujourd'hui, 2 000 à 2 500
    pour le jeu complet), puis les listes officielles courantes si le propriétaire l'accepte : jōyō (2 136) et jinmeiyō
    (863) au Japon, niveau 1 de la table générale (3 500) en chinois simplifié, liste courante du ministère taïwanais
    (4 808) en traditionnel ;
  - devises : déjà presque toutes ; à ajouter si l'on affiche ces monnaies : ₼ (U+20BC), ₾ (U+20BE).
- Un test échoue si un texte livré utilise un caractère absent de la police de sa langue.

## 2.6 Pluriels, genres et formats

### Pluriels (CLDR)

Le prototype `Core/Services/PluralRules.cs` donne la catégorie CLDR (version 46) d'un nombre entier :

| Langues | Formes pour les nombres entiers |
|---|---|
| ja, ko, zh, th, vi, id, ms | other |
| en, de, nl, el, tr, sv, da, nb, fi, hu | one (1), other |
| fr, pt (Brésil) | one (0 et 1), many (millions exacts), other |
| es, it, ca, pt-PT | one (1), many (millions exacts), other |
| ru, uk, be | one (1, 21, 101), few (2 à 4, 22 à 24), many (0, 5 à 20, 11 à 14...), other |
| pl | one (1 seulement), few (2 à 4, 22 à 24), many (le reste), other |
| cs, sk, ro, hr, sr | one, few, other |
| ar | zero, one, two, few, many, other |
| he | one, two, other |

- Les clés gardent le schéma actuel : `clé.one`, `clé.few`, `clé.many`, `clé.other`... ; la clé seule reste la forme
  habituelle pour `Get`. `PluralRules.Categories(langue)` dit quelles formes chaque langue doit fournir : c'est le
  contrôle automatique des traductions. Repli à l'exécution : forme exacte, puis `.other`, puis la clé seule.
- `PluralForms.IsOne` (T36) donne les mêmes réponses que CLDR pour en et fr (testé) : T39B branche `GetPlural` sur
  `PluralRules` sans changer les tables actuelles.

### Genres, cas et articles

Les variables `{species_the}` et `{biome_the}` deviennent des formes de noms : `{biome:in}`, `{species:the}`. Le
glossaire de chaque langue donne, pour chaque nom d'espèce et de biome, les formes dont ses phrases ont besoin
(français : le, de, à ; allemand : nominatif, accusatif, datif avec l'article ; polonais, russe, ukrainien : les
cas). L'extracteur vérifie que chaque forme demandée par une phrase existe pour chaque nom. Le code des contractions
françaises disparaît au profit des données.

### Nombres, dates, durées

Un `LocaleFormat` par langue, dans les données de langue : séparateur décimal, séparateur des milliers (fr : espace
fine insécable U+202F ; de, es, it, pt, id, tr : point ; ru, uk, pl : espace insécable ; en, ja, ko, zh, th :
virgule), pourcentage (« 12 % » en fr et de, « %12 » en tr, « 12% » ailleurs), durée (« 1 h 05 »), date courte. Il
alimente `NumberFormatSettings` de `BigValueFormatter` (déjà sans allocation). Les suffixes K, M, B restent
universels.

### Prix

Formatés par le jeu à partir du montant et du code ISO de la monnaie renvoyés par le store, dans la langue du jeu
(voir 2.1, point 6). Le format de chaque store reste la référence pour le montant.

## 2.7 Contrôles « chaque texte s'affiche bien »

1. **Pseudo-langue** (`qps-ploc`, code de pseudo-langue standard) : chaque lettre remplacée par une variante
   accentuée de notre police, +40 % de longueur, crochets `[ ]` autour de chaque texte, quelques caractères
   cyrilliques, grecs et CJK dès que leurs polices existent. Choisie dans le menu de debug, jouable partout.
2. **Test de place par langue** (EditMode) : les pipelines d'interface écrivent un registre des libellés (chemin dans
   le prefab, clés affichées, rôle, case). Le test pose chaque texte de chaque langue dans sa case, avec sa police et
   la taille minimale de son rôle, et échoue sur un débordement (`isTextOverflowing` ou taille préférée plus grande
   que la case) ou un glyphe absent (recherche dans les replis de la langue).
3. **Le garde-fou des 30 % généralisé** : pour une langue pas encore traduite, le même test utilise l'anglais + 30 %
   et la pseudo-langue + 40 %. Le registre remplace la liste écrite à la main de `FeedbackScreenTests`.
4. **Couverture des glyphes** : chaque caractère de chaque table est dans le noyau ou dans les replis de sa langue,
   tous tirés de nos polices.
5. **Contrôles des traductions** (EditMode, sur les fichiers de langue) : variables identiques à l'anglais, formes de
   pluriel exactes (`PluralRules.Categories`), longueur maximale, mots interdits de la langue, aucun caractère
   d'étincelle (liste de `NoSparkleRuleTests`), pas de tiret cadratin, point-virgule permis en grec seulement, U+200B
   présent dans les longs textes thaïs, guillemets de la langue (« » en fr, „ “ en de, 「」 en ja).
6. **Captures** de chaque écran dans chaque langue, sur 720 x 1520, par l'outil de captures de l'éditeur.

## 2.8 Le flux de traduction

### Fichiers

Les textes sortent des tables C# (`UiPipeline.Texts`, `ActivityTexts`, `DailyVisitTexts` et les autres) vers un
dossier `Localization/` à la racine du dépôt, comme `SourceArt/` :

- `Localization/en.json` : la source. Par entrée : `key`, `text`, `forms` (pluriels), `context` (où, qui parle, ton),
  `maxChars`, `screen` (libellé du registre), `capture` (image de l'écran).
- `Localization/<langue>.json` : `key`, `text` ou `forms`, `source` (empreinte du texte anglais traduit, pour
  repérer les traductions périmées), `status` (`machine`, `reviewed`, `native`).
- `Localization/Glossary/<langue>.json` : termes fixés (refuge, raretés jusqu'à « Fabuleux », Lumière, Gemmes,
  espèces, biomes, activités, noms des écrans), formes des noms (2.6), ton (tutoiement : du, tú, tu, você, ты,
  ty, sen ; japonais poli et chaleureux ; coréen 해요체 ; chinois 你).
- `Localization/Forbidden/<langue>.txt` : les mots interdits de la règle du vocabulaire, traduits, avec leurs formes.
  Comme `Vocabulary_Rule.md`, ces fichiers sont les seuls endroits permis pour ces mots : `VocabularyRuleTests` les
  ajoute à sa liste d'exceptions, les lit, et contrôle chaque table de leur langue. Le test parcourt aussi le
  nouveau dossier `Localization/`.
- Les pipelines construisent `LOC_<langue>.asset` depuis ces fichiers. Les tables C# disparaissent.

### Étapes

1. **Traduction IA** par lot d'écran, avec le contexte, les captures, le glossaire, la liste interdite, les règles de
   la langue (pluriels, guillemets, espaces).
2. **Relecture IA séparée** par langue : retraduction vers l'anglais et comparaison du sens, glossaire, ton, mots
   interdits, longueur. Les écarts reviennent à l'étape 1.
3. **Contrôles automatiques** (2.7). Rien n'entre sans eux.
4. **Relecture native** (marchés principaux, avant la sortie) : fiche du store (600 à 900 mots), tutoriel et dix
   premières minutes (1 000 à 1 200 mots), dans le jeu, sur captures.

### Relecture humaine : volume, coût, recrutement

| Poste | Volume par langue | Tarif du marché | Coût par langue |
|---|---|---|---|
| Relecture fiche + tutoriel + 10 minutes | Environ 2 000 mots | 0,06 à 0,09 $ par mot | 120 à 180 $ |
| Test dans le jeu (LQA) | 2 à 3 heures | 23 à 50 $ de l'heure | 50 à 150 $ |
| Relecture complète du japonais (conseillée) | 25 000 à 30 000 mots | 0,06 à 0,09 $ par mot | 1 500 à 2 700 $ |

Pour huit marchés principaux (ja, de, ko, zh-Hant, es, pt-BR, it, id) : environ 1 600 à 2 800 $, plus le japonais
complet. Une traduction humaine complète coûterait 0,10 à 0,15 $ par mot, soit 2 500 à 4 500 $ par langue pour le jeu
complet : l'IA rend ce poste inutile, la relecture native reste.

Trouver les relecteurs : prestataires de localisation de jeux qui font de la relecture et du LQA à la langue ; annuaires
de traducteurs (ProZ.com) et plateformes de freelances ; demander un essai de 300 mots sur nos textes, et de
l'expérience en jeux mignons ou casual ; plus tard, les joueurs du soft launch (Discord) pour des suggestions, jamais
comme seul contrôle.

Les textes légaux restent hors de ce flux : traduction professionnelle là où la loi demande la langue locale, à
valider par le juriste (T66B).

## 2.9 Volume de texte

| Mesure | Aujourd'hui (LOC_en) |
|---|---|
| Clés | 719 |
| Textes à traduire (sans les doublons des pluriels) | 617, dont 103 textes comptés |
| Mots anglais | 1 913 (2 326 avec les doublons) |
| Caractères anglais | 10 163 |
| Français | 2 466 mots, 14 626 caractères (+18 % de caractères) |
| Variables | 29 sortes, dans 299 textes |
| Caractères distincts (en + fr) | 90 |

Estimation du jeu complet (V1 et biomes 7 à 13), d'après les docs de contenu : autocollants (720 noms), succès (200,
nom et description), titres, cadres, poses, souvenirs du journal, souhaits, décorations, 78 espèces et leurs
variantes, 18 activités (règles, astuces, défis), événements de l'année, boutique, Pass, tutoriel, notifications :
**25 000 à 30 000 mots anglais en jeu**, soit 13 à 15 fois aujourd'hui, plus la fiche du store (environ 800 mots par
langue) et les textes légaux (traités à part).

## 2.10 Arabe et hindi : évaluation technique

| | Arabe | Hindi (devanagari) |
|---|---|---|
| Glyphes par style | Environ 150 : 28 lettres en 2 à 4 formes, ligatures lam-alif obligatoires, chiffres | 50 lettres de base, signes de voyelles, demi-formes et 200 à 400 conjointes |
| Mise en forme | Choix de la forme selon les voisines : petit algorithme (types de jonction Unicode), faisable dans notre import, vers nos glyphes en formes de présentation | Réordonnancement des voyelles, reph, demi-formes, conjointes : un moteur indic complet à écrire |
| Sens d'écriture | Droite à gauche, avec chiffres et noms latins de gauche à droite dedans : algorithme bidi à écrire ; le mode `isRightToLeftText` de TMP inverse mal les textes sur plusieurs lignes | Gauche à droite |
| Interface | Tout l'écran en miroir (barres, retour, carrousels) dans chaque pipeline | Rien de spécial |
| Coût estimé | 6 à 9 semaines (polices 2 à 3, forme et bidi 1 à 2, miroir et tests 3 à 4) | Plus de 10 semaines |
| Valeur | Arabie saoudite 0,87 Md$, Émirats 0,35 Md$ ; joueurs du Golfe souvent à l'aise en anglais | Inde 1,42 Md$ mais 3,47 $ par joueur ; l'anglais domine le jeu mobile indien |
| Avis | Après la V1, seulement si le soft launch montre une demande du Golfe. Pseudo-langue miroir `qps-plocm` d'abord | Non prévu |

## 2.11 Tâches à insérer dans la file

T74 (polices cyrillique et grecque), T83 et T84 (langues des vagues 2 et 3) sont remplacées par les tâches
ci-dessous.

**Règle ajoutée à la règle d'exécution** : toute tâche qui ajoute des textes les écrit dans `Localization/en.json`
avec leur contexte, et les traduit (IA, relecture IA, contrôles) dans toutes les langues déjà livrées avant d'être
terminée.

| Tâche | Contenu | Critère |
|---|---|---|
| **T39B : Langues, socle** (juste après T39) | `DeviceLanguages` et `LanguageMatcher` branchés dans le boot ; tables par code BCP 47 avec héritage (es-ES vers es-419 vers en) ; `PluralRules` dans `GetPlural` ; `LocaleFormat` à la place des `== "fr"` ; formes des noms à la place de `{species_the}` ; `SetLanguage` et `Changed`, changement sans redémarrage ; textes déplacés vers `Localization/*.json` avec contexte, glossaires et listes interdites en et fr ; extracteur de caractères ; pseudo-langue ; registre des libellés et test de place ; tests du vocabulaire et du guide d'écriture par langue ; écran de choix de langue dans les réglages ; `locales_config.xml` Android et `CFBundleLocalizations` iOS ; prix formatés par le jeu | Tous les cas de `LanguageMatcherTests` et `PluralRulesTests` passent dans l'éditeur ; dans le build PC, un changement de langue met à jour tous les libellés visibles en moins d'une seconde sans redémarrage (test PlayMode : aucun texte de l'ancienne langue) ; pseudo-langue jouable sans débordement sur tous les écrans actuels ; 0 GC dû aux textes hors ouverture d'écran ; tous les tests verts, dont les quatre règles du propriétaire |
| **T39C : Polices, toutes les écritures** (en parties, agents en cours) | C-1 cyrillique, grec, vietnamien et latin manquant ; C-2 hangul (11 172 syllabes composées) ; C-3 kana, ponctuation CJK, pleine chasse, par région ; C-4 thaï (GPOS `mark` et `mkmk`, glyphes de pré-mise en forme, règle U+200B) ; C-5 caractères chinois : 500 premiers, puis ceux des textes du jeu (extracteur), puis les listes courantes, formes JP, SC et TC ; C-6 crochet de fusion dans `pw_font_build.py` : TTF noyau et TTF par écriture et région, par style, builds identiques deux fois, `Handmade_Sources.json` et `HandmadeRuleTests` mis à jour | Pour chaque écriture : épreuves revues avec la grille de la charte, lisible à 18 px, aucun caractère faux (contrôle de structure), couverture égale à la liste, build déterministe, revue du propriétaire ; les TTF actuels restent identiques |
| **T39D : Polices dans Unity** | `FontPipeline` : replis par écriture (statiques pour les petites, dynamiques depuis nos TTF pour hangul et caractères chinois), groupes Addressables par langue, chargement au boot et au changement de langue, préchauffage, atlas des noms de langues ; réglages TMP (hangul moderne, `mark` et `mkmk`, listes kinsoku relues) ; tailles et interlignes par écriture ; contrôle de saisie des noms | Test de couverture : chaque glyphe de chaque table vient de nos polices ; mémoire et préchauffage dans les budgets de 2.4 (PC, puis téléphone en T66) ; dix changements de langue sans fuite (mémoire stable) ; 0 GC en jeu |
| **T39E : Vague A** (latin) | de, es-419, pt-BR, it, id, tr, pl (+ nl si choisi) : traduction IA, relecture IA, contrôles, captures ; fiches es-ES et es-419 | Aucune clé manquante, tests de place et de vocabulaire verts dans chaque langue, feuille de revue par langue dans `Docs/Captures/Review/` |
| **T39F : Vague B** (Asie de l'Est) | ja, puis ko, zh-Hant, zh-Hans, dès que leurs polices sont intégrées | Idem, plus couverture des glyphes et tailles minimales par écriture |
| **T39G : Vague C** | th, vi, ru, uk, el | Idem, plus U+200B en thaï, accents empilés vietnamiens sans coupure en haut des cases |
| **T66C : Relecture native** (avec T66B) | Fiches de store, tutoriel, dix premières minutes pour les marchés du soft launch puis de la sortie mondiale ; relecture complète du japonais | Retours appliqués, feuilles de relecture signées |
| **T85 : Arabe (étude)** (après la V1, si les chiffres le demandent) | Pseudo-langue miroir, mise en forme, bidi, interface en miroir, polices | Décision du propriétaire après le soft launch |

## 2.12 Le prototype livré

Fichiers nouveaux, non branchés dans le jeu (le branchement est T39B) :

| Fichier | Assembly | Rôle |
|---|---|---|
| `Assets/_Game/Core/Services/PluralRules.cs` | Game.Core | Catégories CLDR des nombres entiers, formes requises par langue, suffixes ; sans allocation |
| `Assets/_Game/Services/Localization/LanguageMatcher.cs` | Game.Services | `LanguageTag` (lecture des étiquettes BCP 47) et règles de correspondance, C# pur |
| `Assets/_Game/Services/Localization/DeviceLanguages.cs` | Game.Services | Langues ordonnées du téléphone : JNI Android, plugin iOS, éditeur ; cache sans allocation |
| `Assets/_Game/Plugins/iOS/PWLanguages.mm` | (plugin iOS) | `PWLanguages_Preferred()` : `preferredLanguages` en UTF-8. Unity créera son `.meta` à l'import : vérifier en T39B que le plugin n'est actif que pour iOS |
| `Assets/_Game/Tests/EditMode/LanguageMatcherTests.cs` | Game.Tests.EditMode | Lecture des étiquettes, chinois, portugais, espagnol, variantes régionales, ordre, repli, choix sauvegardé, scores, lecture de l'OS |
| `Assets/_Game/Tests/EditMode/PluralRulesTests.cs` | Game.Tests.EditMode | Tables CLDR par langue, formes requises, suffixes, accord avec `PluralForms`, aucune allocation |

Compilation hors Unity (`csc_check.py`) : Game.Core, Game.Services et Game.Tests.EditMode sans erreur ; Game.Services
compilé aussi comme lecteur Android et comme lecteur iOS (sans `UNITY_EDITOR`), sans erreur ni avertissement dans les
nouveaux fichiers. Tests exécutés hors Unity dans un banc de test : 26 réussis, aucun échec ; les deux tests
d'allocation demandent Unity et passeront dans l'éditeur en T39B.

---

## Sources

- Allcorrect Games, « Mobile Game Market Index 2025: Top 30 Markets » :
  https://allcorrectgames.com/insights/mobile-game-market-index-2025-top-30-markets-growth-trends-opportunities/
- AppMagic, « Mobile Market Landscape in 2026 » (résumé GameDev Reports) :
  https://gamedevreports.substack.com/p/appmagic-mobile-market-landscape
- Sensor Tower, « State of Gaming in Japan 2026 » : https://sensortower.com/report/state-of-gaming-in-japan-2026 ;
  résumé PocketGamer.biz : https://www.pocketgamer.biz/japan-generates-10bn-in-mobile-game-revenue-despite-declining-downloads/
- PocketGamer.biz, Pokémon Sleep au-delà de 150 M$ (AppMagic, 25 février 2025) :
  https://www.pocketgamer.biz/pokmon-sleep-slumbers-over-150m-mark-despite-slowdown-since-first-anniversary/
- PocketGamer.biz, Animal Crossing: Pocket Camp au-delà de 250 M$ (Sensor Tower, 9 novembre 2021) :
  https://www.pocketgamer.biz/animal-crossing-pocket-camp-surpasses-250-million/
- Apple, langues des fiches de l'App Store :
  https://developer.apple.com/help/app-store-connect/reference/app-store-localizations/
- Google Play, langues prises en charge : https://support.google.com/googleplay/android-developer/table/4419860?hl=en
- Android, langue par app : https://developer.android.com/guide/topics/resources/app-languages
- Unicode CLDR, règles de pluriel : https://www.unicode.org/cldr/charts/latest/supplemental/language_plural_rules.html
- Material Design 2, écritures denses et hautes : https://m2.material.io/design/typography/language-support.html ;
  Flutter, `ScriptCategory` : https://api.flutter.dev/flutter/material/ScriptCategory.html ;
  Adobe Spectrum, typographie : https://spectrum.adobe.com/page/typography
- Tarifs de localisation : https://allcorrectgames.com/localization-pricing/ ; https://localizedirect.com/platform ;
  https://www.steampageanalyzer.com/blog/steam-localization-languages
- TextMesh Pro de Unity 6 : code du paquet `com.unity.ugui` 2.0.0 dans `Library/PackageCache` (`TMP_FontAsset.cs`,
  `TMP_Text.cs`, `TMP_Settings.cs`, `TMP_TextParsingUtilities.cs`), et `Assets/TextMesh Pro/Resources/TMP Settings.asset`.
