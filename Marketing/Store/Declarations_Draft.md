# Store declarations: draft answers

Status: draft written on 9 October 2026. The answers are in English because the consoles show these questions in English by default. Nothing here is legal advice: every block marked **[LEGAL CHECK]** must be reviewed by a lawyer before it is submitted, and every block marked **[OWNER DECISION]** waits for the owner's choice.

## En bref pour le propriétaire (FR)

- **Aujourd'hui, rien ne quitte le téléphone.** Le jeu crée un identifiant de profil et un identifiant de statistiques au hasard, garde les statistiques dans une file sur le téléphone, mais n'envoie rien : aucun serveur n'est branché. Les achats passent par un faux magasin de test.
- **Les réponses changent dès qu'on branche quelque chose** : un service de statistiques, un SDK de publicité, la sauvegarde en ligne ou un compte. Il faudra alors refaire les formulaires.
- **Classification d'âge en Europe** : depuis juin 2026, le PEGI note plus haut certains mécanismes. Le rendez-vous du jour (récompenses pour revenir) donne au moins PEGI 7. Les offres à durée limitée (Carnet d'or et Coffret d'un événement, saisons du Pass) donnent au moins PEGI 12. Sans offres à durée limitée, le jeu resterait à PEGI 7.
- **Public cible** : c'est la décision la plus lourde. Viser aussi les moins de 13 ans fait entrer le jeu dans les règles « Familles » de Google : SDK de pub certifiés seulement, pas d'identifiant publicitaire pour les enfants, question d'âge, statistiques très limitées. Recommandation des docs : public de 13 ans et plus, sans catégorie Enfants chez Apple.
- **Pubs** : aucun fournisseur choisi, donc la réponse est « Non » tant que le build n'en contient pas.

---

## 1. What the game collects today (facts from the code, 9 October 2026)

| Data | Where it lives | Leaves the phone? | Source in the repo |
|---|---|---|---|
| Profile id (random GUID) | Save file on the phone | No | `Simulation/State/PlayerState.cs` (`Meta.ProfileId`) |
| Game progress, animals, names the player gives to animals, settings (Reduce Motion) | Save file on the phone | No | Save system (schema 11) |
| Analytics id (random GUID, not tied to the device or the profile) and consent state (unknown, granted, refused) | PlayerPrefs on the phone | No | `Services/Analytics/AnalyticsConsent.cs` |
| Gameplay events (session start and end, load time, hatches, Gem changes, screens, offers seen and touched, purchase results, upgrades, zones, activities, daily visit, frame-time samples) with a batch header (app version, build, platform, device model, OS, RAM) | Local outbox on the phone, 64 files at most | No. The transport is `NullAnalyticsTransport` until a backend is chosen. With consent "granted" the batches would be sent; with "refused" they are deleted | `Services/Analytics/*`, `Docs/Analytics/T29_Analytics_Schema.md` |
| Store purchases | Sandbox store provider (test only) | No real store yet. At launch: Google Play Billing and Apple StoreKit | `Services/Store/*`, `Services/StoreSandbox/*` |

Not present in the build: ad SDK, account or login, cloud save, chat, location, contacts, camera, microphone, push notifications, third-party analytics SDK. Event batches never carry content names, store SKUs, transaction ids or typed text.

**[LEGAL CHECK]** The analytics id is created on the phone before the player answers the consent question (the legal drafts already recommend creating it only after consent). Fix this or confirm it with the lawyer before any data is sent.

---

## 2. Content rating: IARC questionnaire (Google Play)

Play Console > Policy and programs > App content > Content rating. One questionnaire gives ratings for PEGI (Europe), ESRB (Americas), USK (Germany), ACB (Australia), ClassInd (Brazil), GRAC (Korea) and IARC Generic. A rating is required for every new app. The IARC questionnaire is moving to a new version (v11) during 2026, so read each question on the day and answer the version shown.

| Topic | Answer | Why |
|---|---|---|
| Category | Game | |
| Violence of any kind (cartoon, fantasy, realistic), blood | No | Animals play, sleep, get brushed. Nothing fights |
| Fear, horror | No | Rain and evening light only |
| Sexual content, nudity, suggestive themes | No | |
| Crude language, crude humor | No | |
| Drugs, alcohol, tobacco | No | |
| Discrimination | No | |
| Simulated gambling (casino, slots, cards) | No | Eggs are a game reward bought with earned Light, no casino theme |
| Real-money gambling | No | |
| Digital purchases | Yes | Optional in-app purchases |
| Random items purchasable with real money or a purchasable currency ("paid random items") | No **[LEGAL CHECK]** | Eggs are bought with Light, which is never sold. Money and Gems only buy fixed items. Indirect paths (a biome pack gives +5 % Light on one biome) are listed in the legal drafts, section 10.4 |
| Time-limited or quantity-limited offers | **[OWNER DECISION]** Yes if event offers (Golden Book, Chest), Pass seasons or dated offer windows ship; No otherwise | PEGI "Time-Limited Offers" descriptor |
| Rewards for coming back (daily gift, calendar, wishes, weekly journal) | Yes | PEGI "Pressure to Play" descriptor |
| Penalties for not coming back (losing rewards, progress or status) | No **[LEGAL CHECK]** | A missed day resets nothing and the weekly journal keeps unclaimed rewards. One point to review: an unopened daily gift is not carried over to the next day |
| Users interact or exchange content (chat, voice, images) | No | No social features in the build |
| Shares the user's location with other users | No | |
| Unrestricted internet access (in-app browser) | No | Links open the system browser |
| User-generated content shared with others | No | Animal names stay on the phone |

Expected result (only the questionnaire decides):

| System | With time-limited offers | Without time-limited offers |
|---|---|---|
| PEGI | PEGI 12, with "In-Game Purchases", "Time-Limited Offers" and "Pressure to Play" | PEGI 7, with "In-Game Purchases" and "Pressure to Play" |
| ESRB | Everyone, "In-Game Purchases" | Everyone, "In-Game Purchases" |
| Others | Lowest or near-lowest band, with an in-game purchases notice | Same |

Why this changed: since June 2026 PEGI rates "rewards for returning" (daily quests, login rewards) at PEGI 7 by default, PEGI 12 when players are punished for not returning, "time-limited or quantity-limited offers" at PEGI 12 (PEGI 7 possible only with built-in parental controls that turn spending off by default), and paid random items at PEGI 16 or 18. The older project drafts expected PEGI 3; that estimate is now out of date.

**[OWNER DECISION]** Keep the event offers and Pass seasons (PEGI 12 in Europe) or sell them without a time limit (PEGI 7). PEGI 12 matches the project's original "12 and over" idea, but it narrows the store audience in Europe.

---

## 3. Apple age rating

App Store Connect > App information > Age rating. Apple does not use IARC. Its bands are 4+, 9+, 13+, 16+ and 18+.

| Section | Question | Answer |
|---|---|---|
| In-app controls | Parental controls | No |
| In-app controls | Age assurance | No (a self-declared age question is not verification) |
| Capabilities | Unrestricted web access | No |
| Capabilities | User-generated content | No |
| Capabilities | Social media | No |
| Capabilities | Messaging and chat | No |
| Capabilities | Advertising | No today. Yes if rewarded ads ship |
| Mature themes | Profanity or crude humor; horror or fear; alcohol, tobacco or drugs | None |
| Medical or wellness | Medical information; health or wellness topics | None |
| Sexuality or nudity | All three questions | None |
| Violence | Cartoon or fantasy; realistic; prolonged graphic; guns or weapons | None |
| Chance-based activities | Gambling; simulated gambling | None |
| Chance-based activities | Contests | None (no contest in the build) |
| Chance-based activities | Loot boxes ("randomized virtual items for purchase") | No **[LEGAL CHECK]**, same reasoning as section 2 |
| Kids Category | Made for Kids | No (see section 7) |

Expected rating: **4+**. Advertising and in-app purchases do not raise Apple's band; loot boxes would (9+).

---

## 4. Google Play Data safety

Required for every app on a closed, open or production track, even when nothing is collected, and it needs a privacy policy URL first. "Collected" means sent off the phone. Data processed only on the phone is not declared.

### 4.1 Scenario A: the build as it is today (closed test, nothing sent)

| Question | Answer |
|---|---|
| Does your app collect or share any of the required user data types? | No |
| Privacy policy URL | [PRIVACY POLICY URL] (required anyway) |

Purchases made through Google Play Billing are handled by Google. The app does not need to declare payment information it never sees.

### 4.2 Scenario B: launch with an analytics backend and real purchases **[OWNER DECISION]** on the backend

Fill this only when the backend is chosen (the design docs propose Google Analytics for Firebase, or the in-house batches sent to an EU server). Each SDK's own Data safety notes must be added.

| Category | Data type | Collected | Shared | Required or optional | Purposes |
|---|---|---|---|---|---|
| App activity | App interactions (screens, hatches, activities, offers seen) | Yes | No (a processor working for us is not "sharing") | Optional (consent) | Analytics |
| App info and performance | Diagnostics (frame times, memory, load times, device model, OS) | Yes | No | Optional (consent) | Analytics |
| App info and performance | Crash logs | Only if a crash reporter is added | No | Optional | Analytics |
| Device or other IDs | Random analytics id (and the SDK's installation id if a third-party SDK is used) | Yes | No | Optional (consent) | Analytics |
| Financial info | Purchase history (purchase results in analytics, receipts if verified on a server) | Yes | No | Optional | App functionality, analytics, fraud prevention |
| Personal info, location, messages, photos, audio, files, contacts, calendar, health, web history | | No | No | | |

Other answers: data encrypted in transit: Yes (HTTPS). Deletion: Yes, refusing consent deletes the queue on the phone; deletion of data already sent by email request to [PRIVACY EMAIL] **[LEGAL CHECK]**.

---

## 5. Apple App Privacy (nutrition label)

Apple's definition: data is "collected" when it is sent off the device and kept longer than needed to answer the request in real time. Third-party SDKs count.

| Scenario | Answer |
|---|---|
| A, today | "Data Not Collected" |
| B, with analytics and real purchases **[OWNER DECISION]** | Usage Data > Product Interaction; Diagnostics > Performance Data (and Crash Data if a crash reporter is added); Identifiers > Device ID or User ID for the random analytics id **[LEGAL CHECK]**; Purchases > Purchase History. Purpose: Analytics (and App Functionality for purchases). Tracking: No. Linked to the user: to confirm with the lawyer, since Apple treats personal data under privacy laws as linked |

If cloud saves arrive later, add User Content > Gameplay Content (Apple: "Your app includes game saves ... Declare Gameplay Content").

Also needed on iOS: a privacy manifest (`PrivacyInfo.xcprivacy`) declaring the "required reason" APIs used by Unity and the game.

---

## 6. Ads declaration

| Store | Question | Answer today |
|---|---|---|
| Google Play | Does your app contain ads? | No |
| Google Play | Does your app use the advertising ID? | No. Make sure the Android manifest does not contain the AD_ID permission |
| Apple | Age rating > Advertising | No |

**[OWNER DECISION]** No ad provider is chosen. The design docs propose optional rewarded ads (Google AdMob). If ads are added, all of these change at once:
- Play "Contains ads": Yes, and an `app-ads.txt` file on the developer website;
- Data safety and App Privacy: device or advertising IDs, approximate location, ad interactions, often "shared" with the ad network;
- a consent tool in the EEA, the UK and Switzerland (certified consent platform);
- the Families rules if children are in the target audience (section 7);
- the privacy policy.

---

## 7. Target audience and the Families policy **[OWNER DECISION]**

Play Console > App content > Target audience and content. Age groups: 5 and under, 6 to 8, 9 to 12, 13 to 15, 16 to 17, 18 and over. Choose only the groups the game is designed for.

| Option | What it means in practice |
|---|---|
| A. 13 and over (recommended by the design docs) | Normal Play rules. Store art and texts must not look aimed at young children. Because the art is cute, Google may still decide the listing appeals to children and ask for changes. In the game: a neutral age question at first launch and a "youth mode" (no analytics, no personalized ads) for players under the local age limit **[LEGAL CHECK]** |
| B. Mixed audience, children included (for example 9 to 12 plus older groups) | The Families policy applies to the child groups: ads only through Families Self-Certified Ads SDKs, no interest-based ads, no advertising ID or other device identifiers sent for children or users of unknown age, a neutral age screen, child-safe SDKs and analytics, privacy policy linked in the store and in the game. Google notes the self-certified ads SDK program is not taking new SDKs at the moment, which limits ad choices |
| C. Children only | All ads from self-certified SDKs, no advertising ID, no location. On Apple's Kids Category: no third-party analytics or advertising, and purchases or links behind a parental gate. Not recommended for this game |

In plain words: the more the game targets children, the fewer ad and analytics tools it can use, and the more checks it needs (age question, parental gates). Option A keeps the most freedom, but only if the store page and social posts speak to teens and adults (calm, cozy tone, no "for kids" wording). Apple: do not enroll in the Kids Category.

Related declarations: "Could the store listing unintentionally appeal to children?" Answer honestly. The soft art can appeal to children; Google may review the listing (up to 7 days or more) and ask to remove child-oriented marketing or to change the target audience.

---

## 8. Other declarations

| Declaration | Draft answer |
|---|---|
| App access (Play) | All functionality is available without special access or login |
| News app, government app, financial features, health app | No |
| Account deletion (Play and Apple) | Not required today: the game has no account. Apps whose accounts are only "created and operated offline" are outside Google's policy. Required as soon as an online account or cloud save account exists (in-app path plus a web link) |
| EU trader status (Apple Digital Services Act; Google contact details) | A developer who earns money from the app is very likely a "trader": name, address, phone and email are shown publicly in the EU **[LEGAL CHECK]** |
| Target API level (Play) | New apps must target Android 16 (API level 36) since 31 August 2026 (extension possible to 1 November 2026). For the build team |
| Privacy policy | Required by both stores, also linked inside the game (Play User Data policy; Apple guideline 5.1.1(i)) |

---

## 9. Points for the lawyer **[LEGAL CHECK]**

1. Consent flow: analytics id created before consent; consent text and timing; age question and youth mode by country (France: 15 for consent to online services).
2. Whether the random analytics id and gameplay events are personal data, and the "linked to the user" answer on Apple.
3. "Paid random items": indirect paths (biome pack +5 % Light, Gem-bought tickets) and the PEGI and Apple definitions.
4. "Pressure to Play": the daily gift that is not carried over.
5. Time-limited offers and the PEGI 12 outcome.
6. Trader status, public contact details, legal mentions.
7. The privacy policy text (`Privacy_Policy_Draft_en.md`) and its French version.

## Sources

- Play Console Help, content ratings (IARC): https://support.google.com/googleplay/android-developer/answer/9859655
- PEGI, new interactive risk categories (June 2026): https://pegi.info/news/pegi-expands-age-rating-criteria-interactive-risk-categories
- PEGI, what the labels mean (Time-Limited Offers, Pressure to Play, Paid Random Items): https://pegi.info/what-do-the-labels-mean
- App Store Connect Help, age ratings values and definitions: https://developer.apple.com/help/app-store-connect/reference/app-information/age-ratings-values-and-definitions
- Play Console Help, Data safety section: https://support.google.com/googleplay/android-developer/answer/10787469
- Apple, App Privacy details: https://developer.apple.com/app-store/app-privacy-details/
- Play Console Help, target audience and app content: https://support.google.com/googleplay/android-developer/answer/9867159
- Play Families policy: https://support.google.com/googleplay/android-developer/answer/9893335
- Play Families Self-Certified Ads SDK Program: https://support.google.com/googleplay/android-developer/answer/9900633
- Play Console Help, ads declaration: https://support.google.com/googleplay/android-developer/answer/9857753
- Play account deletion requirements: https://support.google.com/googleplay/android-developer/answer/13327111
- Play User Data policy (privacy policy): https://support.google.com/googleplay/android-developer/answer/10144311
- Play target API level requirements: https://support.google.com/googleplay/android-developer/answer/11926878
- App Review Guidelines (1.3, 5.1.1, 5.1.4, 3.1.1 loot boxes): https://developer.apple.com/app-store/review/guidelines/
- Apple EU Digital Services Act trader requirements: https://developer.apple.com/help/app-store-connect/manage-compliance-information/manage-european-union-digital-services-act-trader-requirements/
- Project legal drafts (French, 7 October 2026): Drive « 00 - Textes légaux et conformité : brouillons », sections 6 to 10
