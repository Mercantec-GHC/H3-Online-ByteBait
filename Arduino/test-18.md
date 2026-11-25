# Test af #18: kalibrering og følsomhed

Alle fire tester med **samme firmware i normal spiltilstand**. Du skal ikke bruge `MOTION_TEST_MODE`.

## Før du starter (alle)
1. Branchen for #104 (kalibrering) er hentet, og `Arduino/bytebait/` kompilerer.
2. `config.h`: `MOTION_TEST_MODE = false`, `MOTION_PLOT_MODE = false`, `PLOT_MODE = false`. `arduino_secrets.h`: `WIFI_THROW_PROBE 0` (eller filen mangler).
3. Upload, åbn **Serial Monitor** (115200 baud, **New Line**). Luk spillet, så porten er fri.

## Sådan skifter Oplà'en mellem kast og kamp
- Efter et **kast** (`THROW:<n>`) er Oplà'en i **kamp**: bevægelserne virker, og berøringsknapperne gør ingenting.
- Skriv `LED:RED` i Serial Monitor (som spillet gør, når en ny timingmåler starter) for at komme tilbage til **kast**: nu virker knapperne, og et nyt kast giver `THROW`. Efter 60 sek. uden `LED:RED` sker det af sig selv.

## Rækkefølgen

### A. Opstartskalibrering
1. Tryk reset. LED'erne er **gule**: hold Oplà'en stille, som du vil holde en fiskestang.
2. Efter ca. 1,5 sek. blinker de **grønt**, og `HELLO:USB:1` kommer.
3. Prøv også at ryste Oplà'en hele tiden efter reset: efter 6 sek. blinker den **rødt** og starter alligevel.
4. Prøv en **langsom drejning** (ca. 90° over et par sekunder) lige efter reset: LED'erne skal blive gule, til du holder stille.

### B. Følsomhed (i kast-tilstand)
Skriv `LED:RED` først, hvis du lige har kastet.
1. Pad 2: mere følsom. De lilla LED'er viser niveauet (1-5) i under 1 sek. Pad 1: mindre følsom. Standard er niveau 3.
2. Skriv `LED:GREEN`, tryk pad 2, og vent. De grønne LED'er skal komme **tilbage** efter den lilla visning.
3. Pad 3: kalibrér igen (gul). Skriv `PING:1` mens den er gul: `PONG:1` skal komme med det samme. Hold stille: grøn. Ryst i 6 sek.: rød, og standardpositionen bruges.

### C. Kast (10 stk.) på dit niveau
1. Kast, og se `THROW:<n>`. Skriv `LED:RED` før næste kast.
2. Tæl, hvor mange af 10 kast der giver **præcis ét** `THROW`. Blødt kast skal give lav styrke og hårdt kast høj.

### D. Bevægelser (10 pr. bevægelse)
1. Kast én gang (`THROW:<n>`). Nu har du 60 sek. til bevægelserne.
2. Lav bevægelserne, og tæl rigtige beskeder: `HOOK` (moderat ryk opad), `TILT:L` (træk til venstre og løft), `TILT:R` (træk til højre og løft), `HOLD:1` og `HOLD:0` (hold stille, bevæg igen) og `YANK` (hårdt, hurtigt træk opad).
3. Ved 60 sek.: skriv `LED:RED`, kast igen, og fortsæt.
4. **Sideværts følsomhed:** prøv `TILT:L` og `TILT:R` både på niveau 3 og på niveau 5 (skift niveau efter `LED:RED`, kast så igen).

### E. Dit niveau
Skriv det niveau (1-5), hvor bevægelserne virker bedst, i skemaet.

## Skema

| Person | Kast /10 | Hook /10 | Venstre /10 | Højre /10 | Hold (1 og 0) /10 | Yank /10 | Bedste niveau (1-5) |
|---|---|---|---|---|---|---|---|
| Assis | | | | | | | |
| Andreas | | | | | | | |
| Bjarke | | | | | | | |
| Frederik | | | | | | | |

Skriv i #18 (og i PR #104) hvad der ikke virkede og hvorfor, fx om `TILT:L`/`TILT:R` er byttet om for dig.

## Tjek til sidst
- `config.h` er uændret (dine egne tal kan stå i din lokale fil, men ikke i en commit).
- `arduino_secrets.h` er ikke committet.
