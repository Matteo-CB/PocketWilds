# T36 : revue des sons du rendez-vous du jour

8 octobre 2026. Six fichiers pour le souhait accompli, le cadeau du jour, la case du calendrier et l'arrivée des Gemmes
(doc 05 « Sensations et surprises », fiches 17 à 19 et section 6.1 ; doc 08, J-SFX-CURRENCY-004). Aucun son de Gemmes
n'existait : ni dans le catalogue `SFX` de `pw_audio_sfx.py`, ni dans `AudioPipeline.Events()`, ni dans `AudioEventId`.

## Comment c'est fait et mesuré

- Recettes : `SourceArt/Audio/Tools/pw_audio_sfx.py`, section « T36 », avec les instruments de la maison (`glass`,
  `tine`) et `pw_audio_dsp.py`. Aucun échantillon, aucun générateur, rien de téléchargé.
- Rendu : `render_sfx` de `pw_audio_build.py` limité aux quatre entrées T36 du catalogue (masters 48 kHz 24 bits dans
  `SourceArt/Audio/Masters/SFX/`, fichiers du jeu 48 kHz 16 bits mono dans `Assets/_Game/Art/Audio/SFX/`, queue
  silencieuse coupée, niveau calé par la construction).
- Sons existants : les 56 sons rendus à nouveau à part avec le script modifié sont identiques à l'octet (masters et
  fichiers du jeu). Aucun fichier existant n'a été réécrit.
- Mesures : `pw_audio_build.py report` (niveau sur les 200 ms les plus fortes, pondération K, échelle LUFS ; crête et
  vraie crête suréchantillonnée 4 fois). En plus : point où le son passe sous -40 dB de son maximum, part de 2 à 5 kHz
  dans la pire trame de 20 ms, énergie sous 300 Hz.
- Bords : chaque fichier commence sur 0 (fondu d'entrée de 1,5 ms) et finit sur 0 (fondu de 8 ms de la coupe).

## Résultats

| Fichier | Durée du fichier | Sous -40 dB à | Niveau mesuré (cible) | Crête (vraie crête) | 2 à 5 kHz, pire trame |
| --- | --- | --- | --- | --- | --- |
| `SFX_Reward_WishDone_01` | 0,889 s | 0,77 s | -18,0 (-18) | -9,2 dBFS (-9,1) | -12,9 dB |
| `SFX_Reward_WishDone_02` | 0,832 s | 0,75 s | -18,0 (-18) | -8,6 dBFS (-8,6) | -12,9 dB |
| `SFX_Reward_GiftOpen_01` | 0,909 s | 0,81 s | -17,0 (-17) | -4,7 dBFS (-4,7) | -11,6 dB |
| `SFX_UI_PawStamp_01` | 0,302 s | 0,19 s | -21,0 (-21) | -3,0 dBFS (-3,0) | -11,3 dB |
| `SFX_UI_PawStamp_02` | 0,295 s | 0,21 s | -21,0 (-21) | -3,2 dBFS (-3,2) | -12,0 dB |
| `SFX_Currency_Gems_01` | 0,724 s | 0,59 s | -19,0 (-19) | -9,3 dBFS (-9,3) | -13,9 dB |

Tous à moins de 0,1 LU de leur cible, crêtes sous -1 dBFS. Repères de la famille pour la part de 2 à 5 kHz : collecte de
Lumière -11,1, favori -12,0, zone ouverte -8,9. Au-dessus de 6 kHz, les nouveaux sons restent entre -25 et -45 dB, comme
la récompense (-26) et le favori (-24).

## Les sons

### Souhait accompli (`SFX_Reward_WishDone_01`, `_02`, J-SFX-FEEL-009)

- Intention : la bulle au-dessus de l'animal éclate, le souhait est réalisé. Joyeux, tendre, jamais une fanfare ; il
  précède de 0,77 s le son des Gemmes, qui le résout.
- Recette : une bulle douce (sinus rond dont la hauteur monte vers fa, comme une bulle qui se referme, un souffle de
  film à peine audible), puis deux notes de verre qui montent, chacune doublée d'une lame de kalimba une octave plus bas
  pour la chaleur. Variante 1 : la puis do (tierce). Variante 2 : la puis ré (quarte), bulle un peu plus haute, notes un
  peu plus tôt. Les deux variantes ne lassent pas quand trois souhaits se suivent à 400 ms d'écart.
- La note la plus forte est la seconde (à 0,19 s) : la récompense arrive en haut du geste.

### Cadeau du jour (`SFX_Reward_GiftOpen_01`, J-SFX-FEEL-010)

- Intention : la petite boîte ronde en bois clair s'ouvre ; le couvercle saute puis se pose à côté (fiche 18).
- Recette : un toc creux de couvercle en bois (note la, partiels de bois courts, air de la boîte vers do, contact
  arrondi) avec une petite bouffée d'air ; quatre petites notes de verre qui montent sur fa majeur (fa, la, do, fa) à
  60 ms d'écart, sur une lame de kalimba grave ; à 0,21 s, un toc léger : le couvercle posé à côté. Le son « Claim »
  existant reste celui de la prise des objets.

### Case du calendrier (`SFX_UI_PawStamp_01`, `_02`, J-SFX-FEEL-011)

- Intention : le tampon patte se pose sur la case du jour. Feutré, tactile, sans note de musique.
- Recette : la bouffée du feutre (souffle de 300 à 1 300 Hz, attaque de 6 ms), un petit choc sourd qui glisse de 420 à
  330 Hz, une touche légère du carton, puis le feutre encré qui se décolle à 0,15 s, très bas. Filtré sous 3,2 kHz.
  Variante 2 : tampon un peu plus grave, appui un peu plus lent, décollement à 0,17 s.
- L'impact est au début du fichier : le jeu le joue au moment où le tampon touche (fin de la descente de 140 ms).
- Le grave n'est pas indispensable : l'énergie sous 300 Hz est à -17 et -12 dB, le corps du son est entre 300 Hz et
  1,3 kHz, que les haut-parleurs de téléphone rendent.

### Gemmes (`SFX_Currency_Gems_01`, J-SFX-CURRENCY-004)

- Intention : les Gemmes arrivent au compteur (souhait accompli, cases à Gemmes du calendrier, plus tard d'autres
  sources). Un carillon de verre clair et rond, « précieux » sans pièces ni bruit de casino (doc 08).
- Recette : do et fa de verre roulés ensemble (12 ms d'écart) sur une lame de kalimba en fa, puis un la plus doux à
  70 ms : un accord de fa majeur, une perle de verre. Distinct de la collecte de Lumière (une seule note de do).
- Niveau : aucune cible dans les docs. -19 est un choix, entre la collecte de Lumière (-21) et le souhait accompli
  (-18) qu'il suit. À valider à l'écoute dans le jeu.

## Grille de la charte « fait main »

Les points 1 à 37, 39 et 40 portent sur les formes, la lumière, l'animation et les icônes : sans objet pour un son.

| Point | Souhait | Cadeau | Tampon | Gemmes |
| --- | --- | --- | --- | --- |
| 3. Chaque détail a une fonction écrite | vert : chaque couche nommée dans la recette | vert | vert | vert |
| 38. Aucune étoile à quatre branches | vert : noms de verre, de bulle, de perle | vert | vert | vert |
| 41. Source éditable, export reproductible | vert : master WAV et recette ; rendu déterministe, le même script redonne les mêmes octets | vert | vert | vert |
| 42. Aucun générateur par IA (12.1) | vert : synthèse maison | vert | vert | vert |
| 43. Vocabulaire | vert : noms et textes sans mot interdit, ni cloche grave ni chœur | vert | vert | vert |
| 44. Fait par la même main (13.5) | vert : fa majeur, verre et kalimba de la maison | vert : idem, toc de bois comme l'œuf | vert : bruit feutré de la famille de l'interface | vert : fa majeur, verre et kalimba |

Critères du doc 08 :

| Critère | Résultat |
| --- | --- |
| Attaques douces, aucun clic | attaques de 1,5 à 13 ms (10 à 90 %), premier et dernier échantillon à 0 |
| Pas d'aigu perçant, pas de pic de 2 à 5 kHz | parts mesurées dans la fourchette de la famille (tableau des résultats) |
| Pas de longue queue | petite salle (`ROOM`), pas de grande salle ; tous les fichiers sous 0,91 s |
| Pas de dépendance au grave | corps des sons entre 300 Hz et 2 kHz |
| Ni pièces, ni casino, ni urgence | verre, bois, feutre ; aucune série de pièces, aucune montée sans fin |
| Variantes pour les sons fréquents | souhait et tampon en deux variantes ; cadeau en une seule (une fois par jour) ; Gemmes en une seule, variées par le léger écart de hauteur du service |

## Intégration (côté Unity)

Ajouter les identifiants et définitions (doc 05, section 6.4 pour l'ordre), puis lancer « Pocket Wilds/Audio/Build
Audio and Haptics (T34B) » : les nouveaux fichiers prennent les réglages d'import (mono, ADPCM sous 1,2 s, en mémoire)
et entrent dans `DEF_AudioEvents`. Les `.meta` sont créés par Unity à l'import.
