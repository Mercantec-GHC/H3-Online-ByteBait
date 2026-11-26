# Beskedprotokol: Oplà ⇄ spil (v1)

Samme beskeder bruges uanset om forbindelsen er USB eller WiFi.
Hardware: Arduino MKR WiFi 1010 + MKR IoT Carrier Rev2.

## 1. Linjeformat

- ASCII-tekst, én besked pr. linje, afsluttet med `\n` (`\r` ignoreres).
- `KOMMANDO` eller `KOMMANDO:arg1:arg2`. Kommandoer med STORE BOGSTAVER, argumenter er heltal eller faste ord.
- Maks. 32 tegn pr. linje (uden `\n`).
- USB: 115200 baud, 8N1. Browseren bruger Web Serial (Chrome/Edge).
- WiFi: Oplà'en forbinder ud til backend (Dokploy) over HTTPS/WSS og sender de samme linjer. Backend sender dem videre til spillet. Oplà'en kører ingen server.

## 2. Oplà → spil

| Besked | Betydning | Argumenter |
|---|---|---|
| `HELLO:<via>:<v>` | Sendes ved opstart/forbindelse | via = `USB`/`WIFI`, v = protokolversion (`1`) |
| `THROW:<styrke>` | Kastebevægelse registreret | 0–100 (blødt = lavt vand, hårdt = dybt vand) |
| `HOOK` | Ryk opad, når fisken bider | – |
| `TILT:L` / `TILT:R` | Træk til venstre/højre og løft (hale ind) | – |
| `HOLD:1` / `HOLD:0` | Holdt helt stille / bevæger sig igen (give line) | – |
| `YANK` | Hurtigt træk opad, når LED'erne er blå | – |
| `PONG:<n>` | Svar på `PING:<n>` | n = tæller |

`HOLD` sendes kun ved skift (1 = stille, 0 = bevægelse), ikke løbende.
Gentagne beskeder af samme type må ikke sendes oftere end hvert 100. ms.

## 3. Spil → Oplà

| Besked | Betydning | Argumenter |
|---|---|---|
| `LED:<farve>` | Alle 5 LED'er i samme farve | `RED`, `YELLOW`, `GREEN`, `BLUE`, `OFF` |
| `BUZZ:LOST` | Fisk mistet ("EEEEEEEUUHH") | – |
| `PING:<n>` | Tjek forbindelse og mål forsinkelse | n = tæller |

Brug af `LED`:
- Timingmåler: spillet sender zonens farve (`RED`/`YELLOW`/`GREEN`) løbende.
- Yank-signal: `LED:BLUE`.
- Fisk fanget: `LED:GREEN`.
- Slut: `LED:OFF`.

## 4. Ugyldige beskeder

- Ukendt kommando, forkert antal argumenter eller tal uden for området: **ignorer** beskeden.
- Svar højst én gang pr. sekund med `ERR:<kode>`: `UNKNOWN`, `ARGS`, `RANGE`, `TOOLONG`.
- Over 32 tegn: kassér resten af linjen, send `ERR:TOOLONG`.
- Dårlig input må aldrig få programmet til at crashe eller hænge.
- Spillet sender `PING` hvert femte sekund. Hvis spillet ikke har modtaget et matchende `PONG` i 15 sekunder, er forbindelsen tabt.
- Ved USB-timeout frigiver spillet porten og viser `USB connection lost. No reply for 15 seconds.` Spilleren vælger **Connect USB** for at oprette en ny forbindelse.

## 5. Forsinkelse

- Spillet måler tur/retur-tid med `PING:<n>` / `PONG:<n>` (forsinkelse ≈ halvdelen).
- Spillet bruger tallet til at aflæse nålen, hvor den var, da kastet skete.
- Oplà'en svarer på `PING` med det samme og må ikke vente på andet.

## 6. Valg af USB eller WiFi

I sprint 1 bruger spillets sketch USB som standard. Den særskilte [WiFi-prøve](../Arduino/README.md#ét-fysisk-kast-over-wifi-til-browseren-44b) vælges ved at bygge med `WIFI_THROW_PROBE=1`. Den sender ét `THROW` pr. kast over HTTPS til testfanen og bruger Serial til log.

Trinnene nedenfor beskriver det automatiske transportvalg, der er planlagt i #38.

1. Ved opstart venter Oplà'en op til 2 sek. på, at USB-porten åbnes (`Serial`).
2. Åbnes den, bruges USB. Ellers bruges WiFi.
3. Hold touch-knap 0 nede ved opstart for at tvinge WiFi.
4. Oplà'en sender `HELLO:<via>:1`, når forbindelsen er oppe.
5. WiFi-navn, kode og backend-adressen ligger i `arduino_secrets.h` (aldrig i git).

## 7. Eksempel

```
Oplà → spil:  HELLO:USB:1
Spil → Oplà:  PING:1
Oplà → spil:  PONG:1
Spil → Oplà:  LED:YELLOW
Spil → Oplà:  LED:GREEN
Oplà → spil:  THROW:72
Oplà → spil:  HOOK
Spil → Oplà:  LED:BLUE
Oplà → spil:  YANK
Spil → Oplà:  LED:GREEN
```

## 8. Til v0.1.0 (20/11)

Kun disse beskeder skal virke i første release: `HELLO`, `THROW`, `LED:<farve>`, `PING`/`PONG`.
Resten (`HOOK`, `TILT`, `HOLD`, `YANK`, `BUZZ`) kommer i sprint 2.

## 9. Bevægelser og kampfase (S2.1, #17)

`HOOK`, `TILT:L`, `TILT:R`, `HOLD:1`/`HOLD:0` og `YANK` er alle accelerationer, ligesom `THROW`.
Oplà'en kan ikke se forskel på dem uden at kende fasen, så den har to tilstande:

| Tilstand | Hvad Oplà'en leder efter | Skifter til næste tilstand når |
|---|---|---|
| **Kast** (ved start) | kun `THROW` | `THROW` er sendt → kamp |
| **Kamp** | kun `HOOK`, `TILT`, `HOLD`, `YANK` | spillet sender `LED:RED` eller `LED:YELLOW` (nyt timingmåler-kast), `BUZZ:LOST` eller efter 60 sek. → kast |

Konsekvenser for spillet:
- Spillet kan ikke få `THROW` og `HOOK` for den samme bevægelse.
- Næste kast kan først registreres, når kampfasen er slut. Den slutter, når spillet sender `LED:RED` eller `LED:YELLOW`, når spillet sender `BUZZ:LOST`, eller efter 60 sekunders timeout.
- `HOLD:1` kommer, når Oplà'en har holdt sig helt stille i 400 ms, også før fisken bider. Spillet bruger den kun i kampfasen.
- Bevægelserne kommer først ca. 150 ms efter, de er startet (Oplà'en måler toppen, før den vælger).
- `HOOK` = moderat ryk opad, `YANK` = hårdere og hurtigere ryk opad.

## 10. Kalibrering og følsomhed (S2.2, #18)

Kalibreringen ændrer ingen beskeder. Den påvirker kun, hvornår og hvor let Oplà'en sender dem.

- **Ved opstart** venter Oplà'en, til den holdes stille i 1,5 sek. (LED'erne er gule), og gemmer hvilepositionen (grøn). Giver den op efter 6 sek. (rød), bruges standardværdierne.
- `HELLO:<via>:1` sendes først bagefter. Der kan altså gå op til ca. 7 sek. fra opstart, før spillet hører noget. Spillets 15 sek. timeout på `PONG` er stadig nok.
- **Følsomhed:** fem niveauer (standard 3). Alle grænser for kast og bevægelser divideres med niveauets værdi, så niveau 3 giver præcis værdierne i `config.h`. Niveauet nulstilles ved genstart.
- **Berøringsknapper:** pad 1 = mindre følsom, pad 2 = mere følsom, pad 3 = kalibrér igen. Pad 0 er reserveret til at tvinge WiFi (#38). Knapperne ignoreres i kamp-tilstand.
