# Test af #18: kalibrering

## 1. Opstartskalibrering
1. Upload. Oplà'en lyser **gul**: hold den stille i ca. 1,5 sek.
2. Lyser den **grøn** et øjeblik og slukker, er hvilepositionen gemt.
3. Lyser den **rød**, lykkedes det ikke (du bevægede dig for meget i 6 sek.). Så bruges standardværdierne. Prøv igen med den berøringsknap, du bruger til "kalibrér igen" (pad 3).
4. Tjek: Serial Monitor viser `HELLO:USB:1` efter kalibreringen (op til ca. 7 sek. efter upload).

## 2. Følsomhed (berøringsknapper)
| Pad | Funktion |
|---|---|
| 1 | mindre følsom |
| 2 | mere følsom |
| 3 | kalibrér hvilepositionen igen |

- Hver berøring ændrer niveauet. De lilla LED'er viser niveauet (1-5 LED'er) i under 1 sek. Standard er 3.
- Niveauet nulstilles ved genstart.
- Knapperne virker ikke under kamp-tilstand (efter et kast), så en hånd, der rører en pad, ikke ændrer noget midt i spillet. I `MOTION_TEST_MODE = true` er kampen altid slået til, så sæt den til `false` for at teste knapperne (eller sæt den til `true` først, når du tester bevægelserne).

## 3. Test med hver person
Hver person får Oplà'en, holder den, som personen vil holde en fiskestang, og laver 10 forsøg pr. bevægelse (`MOTION_TEST_MODE = true`, ingen ændring af knapper først).
Hvis noget ikke virker, justerer personen følsomheden med pad 1/2 og prøver igen. Skriv det niveau ned, der virker bedst.

| Person | Hook /10 | Venstre /10 | Højre /10 | Hold stille (1 og 0) /10 | Yank /10 | Kast (10 stk.) /10 | Bedste niveau (1-5) |
|---|---|---|---|---|---|---|---|
| Assis | | | | | | | |
| Andreas | | | | | | | |
| Bjarke | | | | | | | |
| Frederik | | | | | | | |

Tjek især:
- `TILT:L` og `TILT:R` er ikke byttet om for nogen. Er de det for én, holder personen Oplà'en anderledes. Skriv det ned, så vi kan se, om der skal gøres noget (fx at `MOTION_LAT_SIGN` ikke passer til alle).
- Kast giver 10 `THROW` pr. person, og styrken ligger i et rimeligt område (blødt kast lavt, hårdt kast højt).

## 4. Når du er færdig
- `MOTION_TEST_MODE = false`, `MOTION_PLOT_MODE = false`, `WIFI_THROW_PROBE = 0`.
- Skriv skemaet i PR'en. `Closes #18`, hvis alle fire har testet. Ellers `Refs #18`.
