# Guide de publication : Google Play et App Store

Pour le propriétaire de Pocket Wilds (nom provisoire). Rédigé le 9 octobre 2026. Chaque règle a été vérifiée sur les pages officielles de Google et d'Apple, listées à la fin. Les règles des stores changent souvent : relis la page officielle le jour où tu fais l'étape.

Les textes des fiches, les déclarations et la politique de confidentialité sont prêts en brouillon dans `Marketing/Store/` (en anglais, comme les fiches). Ce guide te dit dans quel ordre tout faire.

---

## 1. Le chemin en 8 étapes

1. Choisir le nom définitif du jeu (avant le tout premier envoi sur Google Play).
2. Décider : compte personnel ou compte d'entreprise (section 3).
3. Ouvrir les comptes développeur Google et Apple, et passer les vérifications.
4. Mettre la politique de confidentialité en ligne (section 9).
5. Google : test interne, puis test fermé avec au moins 12 testeurs pendant 14 jours (compte personnel), puis demande d'accès à la production.
6. Préparer les fiches (textes, icône, captures, vidéo) et remplir les déclarations.
7. Ouvrir la pré-inscription Google Play et la précommande App Store, 2 à 3 mois avant la sortie.
8. Lancer le jeu en production à la date prévue.

Pendant tout ce temps : une vidéo par jour sur TikTok et Instagram (plan dans `Marketing/Social/`).

---

## 2. Les comptes et ce qu'ils coûtent

| | Google Play | Apple App Store |
|---|---|---|
| Prix | 25 $ US, une seule fois | 99 $ US par an (prix affiché en euros à l'inscription) |
| Paiement | Carte Visa, Mastercard ou American Express. Les cartes prépayées sont refusées | Carte bancaire |
| Identité | Pièce d'identité et carte bancaire à ton nom légal. Si Google juge les informations fausses, les 25 $ ne sont pas remboursés | Compte Apple avec la double authentification. Nom légal exact (pas de pseudo) |
| Appareil | Les nouveaux comptes personnels doivent prouver qu'ils ont un téléphone Android, avec l'application Play Console | Un Mac pour fabriquer la version iPhone (voir ci-dessous) |
| Site | Console : Play Console | Console : App Store Connect |

### Le problème du Mac

Pour fabriquer la version iPhone, il faut Xcode, et Xcode ne marche que sur Mac. Trois solutions :

- **Acheter un Mac** (un Mac mini suffit). La plus simple pour la suite.
- **Unity Build Automation** : un service de Unity qui fabrique la version iPhone dans le nuage, sans Mac. Unity annonce 100 minutes de fabrication sur Mac offertes chaque mois depuis le 1er mars 2026, puis un paiement à la minute (à vérifier sur la page des prix de Unity avant de compter dessus). Il faut quand même le compte Apple payant et ses certificats. Une fabrication iPhone peut prendre longtemps (plusieurs dizaines de minutes).
- **Louer un Mac dans le nuage** (services de Mac à distance à l'heure ou au mois).

Conseil : commence par Android (moins cher, pas de Mac). Prépare iPhone en parallèle avec Unity Build Automation ou un Mac.

---

## 3. Compte personnel ou compte d'entreprise ?

C'est une décision importante, à prendre avec ton expert-comptable.

### Chez Google

| | Compte personnel | Compte organisation |
|---|---|---|
| Pour qui | Une personne | Une entreprise |
| Numéro D-U-N-S | Non | Obligatoire. Gratuit chez Dun & Bradstreet, peut prendre jusqu'à 30 jours. Attention aux sites qui le font payer |
| Test obligatoire | Oui : pour les comptes personnels créés après le 13 novembre 2023, un test fermé avec au moins 12 testeurs inscrits sans interruption pendant au moins 14 jours. Avant ça, la Production et la Pré-inscription sont bloquées | Les règles de test de Google ne visent que les comptes personnels |
| Ce qui est public sur Google Play | Ton nom légal, ton pays, ton e-mail de développeur. Si le jeu gagne de l'argent : ton adresse complète | Le nom légal, l'adresse, l'e-mail et le téléphone de l'entreprise |
| À fournir en plus | | Site web, téléphone, e-mail de contact non générique et non personnel |

### Chez Apple

| | Particulier (individual) | Organisation |
|---|---|---|
| Pour qui | Une personne, ou une entreprise individuelle d'une seule personne | Une entité juridique (société) |
| Numéro D-U-N-S | Non | Obligatoire. Environ 5 jours ouvrés chez D&B, puis 2 jours pour qu'Apple le reçoive |
| Nom affiché comme vendeur | Ton nom légal | Le nom légal de la société. Apple refuse les noms commerciaux |
| À fournir en plus | | Site web public au nom de domaine de l'entreprise, e-mail sur ce domaine, pouvoir de signer pour l'entreprise |

### Et une micro-entreprise ?

- En France, une micro-entreprise est une **entreprise individuelle** : ce n'est pas une société séparée de toi.
- **Apple** le dit clairement : une entreprise individuelle d'une seule personne s'inscrit comme **particulier**. Ton nom légal sera affiché comme vendeur.
- **Google** ne dit rien de précis sur ce cas. Une micro-entreprise peut obtenir un numéro D-U-N-S, mais Google vérifie l'organisation avec des documents. Avant de payer, pose la question au support Google Play. Sinon, le compte personnel marche très bien : il faut juste faire le test fermé.
- **Avec une société** (SASU, EURL ou autre) : comptes organisation chez les deux, nom de la société affiché, pas de test fermé obligatoire chez Google. Mais création, frais et comptabilité à prévoir. Les docs du projet proposent une société « HiddenLab ».

### Dans tous les cas : tes coordonnées seront publiques en Europe

La loi européenne sur les services numériques demande aux vendeurs (« commerçants ») d'afficher nom, adresse, téléphone et e-mail sur les stores dans l'Union européenne. Si tu vends des achats intégrés, tu es très probablement concerné. Chez Apple, un particulier peut donner une boîte postale. Prévois une adresse e-mail dédiée au jeu.

---

## 4. Le test fermé Google (compte personnel)

### Les règles

- Au moins **12 testeurs**, inscrits **sans interruption** pendant au moins **14 jours**.
- Un testeur qui quitte le test puis revient recommence ses 14 jours.
- Ensuite, dans le tableau de bord de la Play Console : « Demander l'accès à la production ». Google pose des questions sur le test (recrutement, retours, ce que tu as changé), sur le jeu (public, ce qui le rend unique, nombre d'installations prévu la première année) et sur ta préparation.
- L'examen prend en général 7 jours ou moins. Google peut demander de tester plus longtemps, par exemple si trop peu de testeurs ont vraiment joué.

### Trouver 12 à 20 vrais testeurs

Vise **20 personnes** pour être sûr d'en garder 12 jusqu'au bout.

1. **Tes proches** qui ont un téléphone Android : famille, amis, collègues. C'est la base.
2. **Tes abonnés** TikTok et Instagram : une vidéo « Rejoins le test du jeu » avec un lien vers un formulaire où ils donnent leur adresse Gmail.
3. **Un groupe Google** (Google Groups) : tu ajoutes le groupe comme liste de testeurs dans la Play Console. Les gens rejoignent le groupe, tu n'as pas à saisir chaque adresse.
4. **Des communautés de développeurs indépendants** (serveurs Discord, forums) où les développeurs testent les jeux les uns des autres.
5. **Demande-leur d'ouvrir le jeu chaque jour**, même 2 minutes, pendant les 14 jours. Envoie un petit rappel.
6. **Note leurs retours** dans un formulaire : ces notes serviront pour répondre aux questions de Google.

À éviter : les services payants qui vendent « 12 testeurs ». Ce ne sont pas de vrais joueurs, et Google peut refuser l'accès à la production.

---

## 5. La pré-inscription Google Play

Les joueurs appuient sur « Pré-inscription » au lieu de « Installer ». Le jour de la sortie, ils reçoivent une notification et, s'ils l'ont accepté, le jeu s'installe tout seul.

### Avant de l'ouvrir

1. Compte personnel : avoir rempli les conditions du test fermé. Avant, la pré-inscription est bloquée.
2. Remplir les déclarations du jeu : publicités, classification, public cible, sécurité des données (voir `Store/Declarations_Draft.md`). Google en a besoin pour son examen.
3. Faire une fiche complète : textes, icône, captures, vidéo (voir `Store/Google_Play_Listing.md`). Google le recommande fortement.
4. Envoyer des fichiers **.aab** sur la piste de pré-inscription. Ils ne sont pas donnés aux joueurs : ils servent à savoir quels téléphones sont compatibles. Si le jeu vend des achats intégrés, au moins un fichier doit contenir la bibliothèque Google Play Billing.
5. Choisir les pays. On peut en ajouter plus tard.
6. Récompense de pré-inscription (facultative) : un seul cadeau par campagne. C'est un produit unique (« one-time product ») créé exprès, actif avant le début. Une fois créé, il ne se modifie plus et ne se supprime plus. Le jeu doit vraiment le donner, sinon l'application peut être suspendue. Idée : un petit cosmétique, jamais un objet de puissance.

### La règle des 90 jours (très importante)

- Une campagne de pré-inscription dure **90 jours au plus** dans chaque pays, à partir de son ouverture dans ce pays.
- Une pause (« inactive ») compte quand même dans les 90 jours.
- Si le jeu n'est pas lancé en production à temps, la campagne s'arrête **dans tous les pays**, son statut devient « Expirée », et **tu ne pourras plus jamais lancer de nouvelle campagne** pour ce jeu.

Donc : choisis d'abord la date de sortie, puis ouvre la pré-inscription **2 à 3 mois avant**, pas plus tôt. Garde 2 à 4 semaines de marge pour un retard.

### Autres points

- Taille du jeu pour l'installation automatique : jusqu'à 200 Mo en Wi-Fi ou en données mobiles, de 200 Mo à 2 Go en Wi-Fi seulement, au-delà pas d'installation automatique.
- Les comptes d'enfants de moins de 13 ans n'ont pas l'installation automatique.
- Un testeur ne peut pas se pré-inscrire tant qu'il est dans un test.
- On peut avoir au plus 2 jeux en pré-inscription en même temps.
- On peut ouvrir la pré-inscription dans certains pays et faire un test dans d'autres.

---

## 6. La précommande sur l'App Store

- Un nouveau jeu peut être mis en précommande avec une date de sortie **de 2 à 180 jours** après la publication de la précommande.
- Le jeu doit d'abord être **envoyé et validé par l'examen d'Apple** : on prépare la précommande dans « Prix et disponibilité », on envoie la version à l'examen, puis, une fois validée, on clique sur « Publier cette version » et la précommande apparaît sur l'App Store.
- Les achats intégrés ne se précommandent pas, mais on peut les préparer pendant la précommande.
- Le jour de la sortie, le jeu se télécharge tout seul chez les personnes qui l'ont précommandé, avec une notification.
- On peut sortir le jeu plus tôt que la date prévue.

---

## 7. Les frais des stores

| Store | Ce que le store garde sur tes ventes |
|---|---|
| Apple | **15 %** avec le **Small Business Program** (au lieu de 30 %), si toi et tes comptes associés gagnez au plus 1 million de dollars par an. **Il faut s'inscrire** : être titulaire du compte, accepter le contrat des apps payantes dans App Store Connect, déclarer les comptes associés. Le taux s'applique 15 jours après la fin du mois fiscal où l'inscription est acceptée |
| Google, joueurs de l'Union européenne, du Royaume-Uni et des États-Unis | Depuis le **30 juin 2026**, nouveau calcul : frais de service de **10 %** sur le premier million de dollars gagné dans l'année, plus des frais de facturation de **5 %** quand l'achat passe par Google Play Billing. Total : **15 %** pour un petit studio. Le tableau de la page d'aide Google est complexe : vérifie ta situation dans la Play Console |
| Google, autres pays | Encore l'ancien palier : **15 %** sur le premier million de dollars par an (30 % au-delà). Ce palier demandait de s'inscrire dans la Play Console (groupe de comptes, profil de paiement) |
| Google, programme Play Games Level Up | Facultatif. Des taux plus bas à partir du 30 septembre 2026 si le jeu respecte toutes les règles du programme (beaucoup d'intégrations techniques). À regarder plus tard |

Les abonnements ont leurs propres taux (plus bas). Les prix affichés aux joueurs incluent les taxes, fixées par le store dans chaque pays.

---

## 8. Les déclarations à remplir

Tout est préparé dans `Store/Declarations_Draft.md`. En résumé :

- **Classification d'âge** : questionnaire IARC chez Google, questionnaire d'Apple. Résultat probable : PEGI 12 en Europe si le jeu vend des offres à durée limitée (Carnet d'or et Coffret des événements, saisons du Pass), sinon PEGI 7 (à cause des récompenses du rendez-vous du jour). 4+ chez Apple.
- **Sécurité des données** (Google) et **Confidentialité de l'app** (Apple) : aujourd'hui, rien ne quitte le téléphone. À refaire dès qu'un service de statistiques est branché.
- **Publicités** : « Non » tant qu'aucun fournisseur n'est intégré.
- **Public cible** : 13 ans et plus recommandé (voir la section 7 du fichier pour les règles « Familles » si tu vises les enfants).
- **Statut de commerçant** en Europe (Apple et Google).
- **Niveau d'API Android** : depuis le 31 août 2026, Google demande que les nouveaux jeux visent Android 16 (API 36). C'est pour la personne qui fait le build.

Tout ce qui est marqué « LEGAL CHECK » doit être relu par un juriste.

---

## 9. Mettre la politique de confidentialité en ligne (gratuit)

Les deux stores l'exigent, même si le jeu ne collecte rien. Google veut une page web **active, publique, accessible depuis tous les pays, pas en PDF et pas modifiable par les visiteurs**. Le jeu doit aussi avoir un lien vers cette page (Google et Apple le demandent).

Le brouillon est dans `Store/Privacy_Policy_Draft_en.md`. Fais-le relire, remplis les champs entre crochets, puis publie-le.

### Option A : GitHub Pages (gratuit)

1. Crée un compte GitHub gratuit.
2. Crée un dépôt **public** nommé `ton-nom.github.io` (avec un compte gratuit, le dépôt doit être public).
3. Ajoute un fichier `index.md` (ou `index.html`) avec la politique, et un dossier `privacy` si tu veux plusieurs pages.
4. Dans le dépôt : Settings, puis Pages, puis choisis la branche à publier.
5. Ta page est en ligne à l'adresse `https://ton-nom.github.io`.

Note : GitHub Pages n'est pas fait pour faire tourner une boutique en ligne. Une page d'information (confidentialité, aide, contact) va très bien.

### Option B : Google Sites (gratuit)

1. Va sur sites.google.com avec ton compte Google, crée un site.
2. Colle le texte de la politique sur une page.
3. Clique sur « Publier », choisis l'adresse, puis dans « Partager », mets le site publié en **Public**.

### Le mieux à terme

Un vrai nom de domaine (par exemple au nom du jeu) relié à GitHub Pages. Il servira aussi pour la page d'aide, la page de suppression de compte si un jour le jeu a des comptes, et le fichier `app-ads.txt` si un jour il y a des publicités. Apple exige un site au nom de domaine de l'entreprise pour un compte organisation.

---

## 10. Calendrier proposé

Les dates sont des exemples, à partir d'aujourd'hui (9 octobre 2026), pour une sortie au printemps 2027. Le plan des docs prévoit aussi un lancement d'essai (« soft launch ») au Canada et en Australie pendant 10 semaines avant le lancement mondial : dans ce cas, décale tout d'autant.

| Quand | Quoi |
|---|---|
| Maintenant (octobre 2026) | Créer les comptes TikTok, Instagram et YouTube, publier chaque jour. Choisir le nom définitif. Décider personnel ou société. Demander le D-U-N-S si besoin |
| Octobre à novembre 2026 | Ouvrir les comptes Google et Apple (les vérifications prennent de quelques jours à quelques semaines). Mettre la politique de confidentialité en ligne. Créer l'icône à partir du vrai modèle de Mochi |
| Novembre 2026 | Test interne sur Google Play (toi et quelques proches). TestFlight chez Apple |
| Décembre 2026 à janvier 2027 | Test fermé Google : 20 testeurs invités, au moins 14 jours d'affilée (prévois 3 à 4 semaines). Puis demande d'accès à la production (environ 7 jours) |
| Février 2027 | Fiches finales, captures, vidéo, déclarations. Choisir la date de sortie |
| Environ 2 à 3 mois avant la sortie (par exemple mi-février à début mars 2027) | Ouvrir la pré-inscription Google Play. Envoyer le build à Apple et ouvrir la précommande après validation |
| Sortie (par exemple mai 2027) | Lancer en production sur Google Play (avant la fin des 90 jours) et publier la version App Store |

---

## 11. Petit glossaire

| Mot | Ce que ça veut dire |
|---|---|
| .aab (Android App Bundle) | Le fichier du jeu Android qu'on envoie à Google Play |
| App Store Connect | Le site d'Apple pour gérer le jeu sur l'App Store |
| ASO | « App Store Optimization » : bien choisir les mots et les images de la fiche pour être trouvé |
| D-U-N-S | Un numéro gratuit qui identifie une entreprise, donné par Dun & Bradstreet |
| Fiche (store listing) | La page du jeu dans le store : nom, textes, icône, captures, vidéo |
| IARC | Le questionnaire unique qui donne les classifications d'âge (PEGI, ESRB...) sur Google Play |
| Nom du paquet (package name) | L'identifiant Android du jeu, par exemple `com.studio.jeu`. Définitif après le premier envoi |
| PEGI | La classification d'âge en Europe (3, 7, 12, 16, 18) |
| Play Console | Le site de Google pour gérer le jeu sur Google Play |
| Pré-inscription | Google Play : les joueurs s'inscrivent avant la sortie |
| Précommande | App Store : les joueurs commandent avant la sortie |
| Production | La version publique du jeu, que tout le monde peut installer |
| Programme Familles | Les règles de Google pour les jeux qui visent les enfants |
| SDK | Un morceau de code d'une autre entreprise ajouté au jeu (pub, statistiques) |
| Sécurité des données (Data safety) | Le formulaire Google qui dit quelles données le jeu collecte |
| Small Business Program | Le programme d'Apple qui baisse la commission à 15 % |
| Test interne, fermé, ouvert | Trois niveaux de test sur Google Play, du plus petit au plus large |
| TestFlight | L'outil d'Apple pour faire tester le jeu sur iPhone avant la sortie |

---

## Sources officielles

- Google, inscription et frais (25 $) : https://support.google.com/googleplay/android-developer/answer/6112435
- Google, choisir le type de compte (D-U-N-S) : https://support.google.com/googleplay/android-developer/answer/13634885
- Google, informations demandées et affichées : https://support.google.com/googleplay/android-developer/answer/13628312
- Google, test obligatoire des nouveaux comptes personnels (12 testeurs, 14 jours) : https://support.google.com/googleplay/android-developer/answer/14151465
- Google, pré-inscription (90 jours, récompense, installation automatique) : https://support.google.com/googleplay/android-developer/answer/9084187
- Google, frais de service (dont le changement du 30 juin 2026) : https://support.google.com/googleplay/android-developer/answer/112622
- Blog Android Developers, annonce des nouveaux frais (juin 2026) : https://android-developers.googleblog.com/2026/06/play-expanded-billing.html
- Google, palier de 15 % (inscription) : https://support.google.com/googleplay/android-developer/answer/10632485
- Google, niveau d'API demandé : https://support.google.com/googleplay/android-developer/answer/11926878
- Google, règles sur les données et la politique de confidentialité : https://support.google.com/googleplay/android-developer/answer/10144311
- Apple, inscription au programme développeur (99 $ par an, particulier ou organisation) : https://developer.apple.com/programs/enroll/
- Apple, D-U-N-S et entreprise individuelle : https://developer.apple.com/support/D-U-N-S/
- Apple, Small Business Program : https://developer.apple.com/app-store/small-business-program/
- Apple, précommandes : https://developer.apple.com/app-store/pre-orders/ et https://developer.apple.com/help/app-store-connect/manage-your-apps-availability/publish-for-pre-order/
- Apple, statut de commerçant (DSA) : https://developer.apple.com/help/app-store-connect/manage-compliance-information/manage-european-union-digital-services-act-trader-requirements/
- Unity, environnement iOS (Xcode seulement sur Mac, Build Automation) : https://docs.unity3d.com/Manual/ios-environment-setup.html
- Unity, changements de prix (minutes Mac offertes) : https://unity.com/en/products/pricing-updates
- GitHub Pages, créer un site : https://docs.github.com/en/pages/getting-started-with-github-pages/creating-a-github-pages-site
- GitHub Pages, limites d'usage : https://docs.github.com/en/pages/getting-started-with-github-pages/github-pages-limits
- Google Sites, publier un site : https://support.google.com/sites/answer/6372880
