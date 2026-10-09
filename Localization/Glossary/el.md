# Pocket Wilds glossary: Greek (el)

Draft of 9 October 2026, agent TR3. Fixed terms for every Greek text of the game, so later texts stay consistent.
Source: `Localization/Drafts/el.json`. Forbidden words: `el_forbidden.txt`. Questions for the native reviewer:
`Drafts/NOTES_TR3.md`.

## Tone

- Informal second person singular (« εσύ »), as the French « tu »: warm, playful, never pushy.
- Goals (wishes, weekly journal, challenges) are short imperatives: « Εκκόλαψε 3 αυγά », « Βρες 5 ζωάκια ».
- Buttons: an imperative for actions (Μάζεψε, Παίξε, Άνοιξε, Δες, Σφράγισε) and the usual nouns for system buttons
  (Παραλαβή, Συνέχεια, Κλείσιμο, Άκυρο, Παύση, Έξοδος, Πίσω).
- Diminutives give the cozy tone: ζωάκι, Κουνελάκι, Σκιουράκι, Σκαντζοχοιράκι, φωλίτσες, φιογκάκι, αγκαλίτσα, μωράκι.
- Monotonic spelling with tonos. If a screen ever shows text in capitals, the accents are dropped (Greek rule).
- Greek quotes « » around names inside a sentence; the question mark is « ; » (none in the current texts).
- Numbers: decimal comma (× 1,25); « 50% »; units λεπ., ώρ., δευτ., επ.
- No em dash: use a colon or a comma.
- Brand: Pocket Wilds stays in Latin letters.

## Names in sentences (articles and cases)

Greek puts an article before names in running text, and the article changes with gender and case. The drafts:

- make the three species neuter diminutives (το Κουνελάκι, το Σκιουράκι, το Σκαντζοχοιράκι), and the two biomes are
  neuter too (το Λιβάδι, το Δάσος): neuter nominative and accusative are the same, so {species_the} and {biome_the},
  given with their article, fit both subject and object slots;
- use « για » (for) before {biome_the}, which takes the accusative without merging with the article (« για το
  Λιβάδι »), instead of « σε », which would need « στο »;
- put {animal}, {visitor}, {destination} and {cake} after a colon (« Μια λιχουδιά για: {animal} »), because a pet's own
  name has no known gender for the article;
- keep zone names bare (Αφετηρία, Περιβόλι...) with « Άνοιγμα: {zone} ».

T39B can give each name its article and case forms; future biomes should prefer neuter names when natural.

## Plurals

CLDR one / other (one = exactly 1). The « one » form may say « ένα » instead of {n}: « Εκκόλαψε ένα αυγό ». The key
without a suffix carries the other form, or the one form for things that come one at a time.

## Species and animals

| English | Greek | Notes |
|---|---|---|
| Rabbit | Κουνελάκι | neuter; variants Καφέ, Λευκό, Γκρι, Μαύρο, Πιτσιλωτό, Σακούρα |
| Squirrel | Σκιουράκι | neuter; variants Κόκκινο, Καφέ, Γκρι, Κρεμ, Μαύρο, Φθινοπωρινό |
| Hedgehog | Σκαντζοχοιράκι | neuter; variants Καφέ, Κρεμ, Γκρι, Μαύρο, Λευκό, Μανιτάρι |
| Axolotl | αξολότλ | neuter |
| species (the word) | είδος | « Νέο είδος! » |
| animal | ζωάκι | |
| newborn, baby | μωράκι | |
| visitor | επισκέπτης | |
| variant | παραλλαγή | « Νέα παραλλαγή! »; a coat color in the egg odds: χρώμα |
| rare traits | σπάνια γνωρίσματα | |
| bond | φιλία | « πόντοι φιλίας » |

## Currencies and items

| English | Greek | Notes |
|---|---|---|
| Gems | Πετράδια (one: Πετράδι) | capitalized as a game currency |
| Light | Φως (genitive Φωτός) | capitalized; « {n} Φως », « {n} λεπ. Φωτός » |
| ticket | εισιτήριο | |
| treat | λιχουδιά | |
| Mushroom Biscuit | Μπισκότο μανιτάρι | |
| sticker | αυτοκόλλητο | |
| Flower Sugar | Ζάχαρη λουλουδιών | counted as « Ζάχαρη λουλουδιών ×{n} » |
| seed | σπόρος | |
| decoration | διακοσμητικό | |
| Discovery Egg | Αυγό ανακάλυψης | |
| Meadow Egg, Precious Meadow Egg | Αυγό του Λιβαδιού, Πολύτιμο αυγό του Λιβαδιού | |
| Starter Pack | Πακέτο εκκίνησης | |
| Dusk (decoration line) | «Σούρουπο» | after the name |

## Places

| English | Greek | Notes |
|---|---|---|
| refuge | καταφύγιο | |
| Meadow | Λιβάδι (το Λιβάδι) | |
| Forest | Δάσος (το Δάσος) | |
| Lake | Λίμνη | |
| Mountain | Βουνό | |
| Cloudlands (future biome) | Νησιά των σύννεφων | a plain sky word, see `el_forbidden.txt` |
| Starlight (future biome) | Έναστρος ουρανός | ουρανός (sky) is allowed |
| biome | βιότοπος | |
| zone | ζώνη | |
| zones of the Meadow | Αφετηρία, Ανθισμένο λιβάδι, Στροφή του ρυακιού, Περιβόλι, Λόφος, Ύψωμα του μύλου, Όχθη της λιμνούλας, Άκρη του δάσους | |
| cairn (future landmark) | πέτρινος σωρός | |

## Rarities

Κοινό, Ασυνήθιστο, Σπάνιο, Επικό, Θρυλικό, Παραμυθένιο (Fabulous). Neuter, like ζωάκι and αυγό.

## Activities

| English | Greek |
|---|---|
| Activities (hub) | Δραστηριότητες |
| Grooming | Περιποίηση |
| Hide and Seek | Κρυφτό |
| Nut Drop | Βροχή φουντουκιών |
| Pawprint Memory | Μνήμη με πατουσάκια (short: Μνήμη) |
| Expeditions | Εκδρομές |
| Garden | Κήπος |
| Treasure Fishing | Ψάρεμα θησαυρών |
| Lily Pad Hop | Άλματα στα νούφαρα |
| Baking | Ζαχαροπλαστική (a cake: κέικ) |
| Photo Safari | Φωτοσαφάρι |
| Cuddle Match | Αγκαλίτσες |
| Glide | Πέταγμα |
| Constellations | Αστερισμοί |
| Firefly Rhythm | Ρυθμός των πυγολαμπίδων |
| Bubbles | Φούσκες |
| Tile Puzzle | Συρόμενο παζλ |
| Burrow Maze | Λαβύρινθος της φωλιάς |
| Grand Contest | Μεγάλος διαγωνισμός |
| challenge | πρόκληση |
| practice | εξάσκηση |
| streak | σερί |
| record, best | ρεκόρ |

## Features

| English | Greek |
|---|---|
| rendez-vous (daily visit) | το ραντεβού της ημέρας |
| Today | Σήμερα |
| wish, wishes | επιθυμία, Επιθυμίες; « Η επιθυμία εκπληρώθηκε » |
| daily gift | Δώρο της ημέρας |
| welcome gift | Δώρο καλωσορίσματος |
| calendar, stamp | Ημερολόγιο, σφράγισε (a box: κουτάκι) |
| weekly journal | Τετράδιο της εβδομάδας (not ημερολόγιο, which is the calendar) |
| album | άλμπουμ |
| Codex | Κατάλογος |
| mastery | μαεστρία |
| upgrade | αναβάθμιση |
| glow | λάμψη |

## UI verbs

| English | Greek |
|---|---|
| Collect (Light) | Μάζεψε |
| Claim, Collect (a reward) | Παραλαβή; Claim all: Παραλαβή όλων |
| Play | Παίξε |
| Play again | Ξανά |
| Continue, Resume | Συνέχεια |
| Close | Κλείσιμο |
| Cancel | Άκυρο |
| OK | OK |
| Open | Άνοιξε |
| Swap | Αλλαγή |
| See | Δες |
| Back | Πίσω |
| Quit | Έξοδος |
| Details | Λεπτομέρειες |
