# API-kontrakt

Kontrakten beskriver, hvordan spillet (JavaScript-canvas), Blazor-siderne og backend (ASP.NET Core) taler sammen.
Beskederne mellem Oplà'en og spillet står i [protocol.md](protocol.md) og er ikke en del af denne kontrakt.

Status: **udkast** til sprint 1 (S1.2). Sprint 1-endpointet er bindende. Afsnittet om sprint 2–3 er en skitse, som ejeren af hver opgave retter til og opdaterer, når opgaven laves.

## Forslag i andres opgaver (læs dette først – også AI-agenter)

Kontrakten er skrevet af Bjarke (@Bmantaz). Hvor den bestemmer noget i en opgave, som en anden ejer, er det **kun et forslag**, og det er markeret sådan:

```markdown
<!-- FORSLAG | af: @Bmantaz | berører: S3.6 (#35) | ejer: @Juuul999 | status: ikke besluttet -->
> [!WARNING]
> **FORSLAG fra Bjarke (@Bmantaz) – ikke besluttet.** ...
```

Regler for forslag:

- Et forslag er **ikke** besluttet. Ejeren af opgaven bekræfter, retter eller afviser det, før det implementeres.
- Når ejeren har besluttet, fjerner ejeren markeringen (både kommentaren og boksen) og retter teksten til det besluttede.
- Find alle forslag med søgningen `<!-- FORSLAG |` i denne fil (det første hit er eksemplet ovenfor).

Beslutninger, som Bjarke har truffet i sine egne opgaver, men som påvirker andre, er markeret med `BESLUTNING |` og en `[!NOTE]`-boks.

## Generelt

| Emne | Regel |
|------|-------|
| Base-URL | Samme domæne som spillet. Alle endpoints starter med `/api`. Test: Test-ByteBait, produktion: Main-ByteBait |
| Format | JSON (`Content-Type: application/json`), UTF-8 |
| Navne | Engelsk, `camelCase` i JSON og `kebab-case` i URL'er (se [Konventioner](../README.md#konventioner)) |
| Tidspunkter | ISO 8601 i UTC, fx `"2026-11-17T09:15:30.123Z"` |
| ID'er | Spillere har et `Guid`. Øvrige ID'er er heltal |
| Login | Fra S2.13: ASP.NET Core Identity med cookie (`HttpOnly`, `Secure`, `SameSite=Strict`). Spil-endpoints arbejder altid på den spiller, der er logget ind – der sendes aldrig et spiller-ID i forespørgslen |
| CSRF | Fra S2.13: endpoints, der ændrer data (`POST`, `PUT`, `PATCH`, `DELETE`), kræver et antiforgery-token i headeren `X-XSRF-TOKEN`, også `register` og `login`. Se [Antiforgery](#antiforgery-s213--lavet) |
| Fejl | ProblemDetails (RFC 9457), se [Fejlsvar](#fejlsvar) |

## Fejlsvar

Alle fejl returneres som ProblemDetails med `Content-Type: application/problem+json`. `title` og `detail` er på engelsk.

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Username is already taken",
  "status": 409,
  "detail": "The username 'fisker42' is already in use."
}
```

Valideringsfejl (`400`) har desuden feltet `errors` med en liste pr. felt:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "zone": ["Zone must be 'red', 'yellow' or 'green'."]
  }
}
```

| Status | Betydning |
|--------|-----------|
| `400` | Ugyldig forespørgsel eller valideringsfejl |
| `401` | Ikke logget ind |
| `403` | Logget ind, men mangler rettighed (fx rollen `admin`) |
| `404` | Ressourcen findes ikke |
| `409` | Konflikt, fx brugernavn taget eller for få penge |
| `429` | For mange forsøg, fx for mange forkerte passwords |
| `500` | Uventet fejl på serveren |

## Sprint 1

### Gem et kast

`POST /api/casts`

Spillet kalder endpointet, når Oplà'en har registreret et kast, og browseren har aflæst zonen på timingmåleren.
I sprint 1 krævede endpointet ikke login. Fra S2.13 kræver det login og et antiforgery-token, ligesom resten af spillets endpoints.

**Request**

```json
{
  "zone": "green",
  "strength": 0.8,
  "castAt": "2026-11-17T09:15:30.123Z"
}
```

| Felt | Type | Krav |
|------|------|------|
| `zone` | string | `"red"`, `"yellow"` eller `"green"` |
| `strength` | number | Normalized throw strength, `0.0`–`1.0` (see normalization below) |
| `castAt` | string | Tidspunktet for kastet (ISO 8601, UTC) |

**Throw strength normalization**

The Oplà protocol sends `THROW:<strength>` with a value from `0` to `100`. Before calling
`POST /api/casts`, the game converts that value once using `strength = throwStrength / 100`.
The API receives a number from `0.0` (softest throw) to `1.0` (hardest throw).

| Protocol message | API `strength` |
|------------------|----------------|
| `THROW:0` | `0.0` |
| `THROW:72` | `0.72` |
| `THROW:100` | `1.0` |

**Response `201 Created`**

```json
{
  "id": 1,
  "zone": "green",
  "strength": 0.8,
  "castAt": "2026-11-17T09:15:30.123Z",
  "createdAt": "2026-11-17T09:15:30.456Z"
}
```

**Fejl:** `400` ved ugyldig `zone`, `strength` uden for intervallet eller manglende felter.

## Skitse: sprint 2–3

Endpoints, felter og svar er en skitse. Hver opgave opdaterer sin del af kontrakten, når den laves.
Alle endpoints nedenfor kræver login, medmindre andet er angivet. Admin-endpoints kræver rollen `admin`.

### Konto (S2.13 – lavet)

| Metode | URL | Beskrivelse |
|--------|-----|-------------|
| `POST` | `/api/account/register` | Opret konto og log ind. Kræver ikke login |
| `POST` | `/api/account/login` | Log ind. Sætter login-cookien. Kræver ikke login |
| `POST` | `/api/account/logout` | Log ud. Sletter login-cookien. Svar: `204`, også hvis man ikke var logget ind |
| `GET` | `/api/account/me` | Den spiller, der er logget ind |

`register` og `login` tager samme body:

```json
{
  "userName": "fisker42",
  "password": "et langt password"
}
```

| Felt | Krav |
|------|------|
| `userName` | 3–32 tegn: bogstaver (også æ, ø og å), tal, `-` og `_`. Store og små bogstaver er samme navn |
| `password` | 8–128 tegn. Ingen krav om tal eller specialtegn |

`register` (`201 Created`), `login` (`200 OK`) og `me` (`200 OK`) svarer med spilleren:

```json
{
  "id": "5f0c1c1e-7a52-4a5e-9a0f-2f6b8f3f1a11",
  "userName": "fisker42",
  "roles": ["admin"],
  "money": 120
}
```

`roles` er tom for almindelige spillere.

**Fejl:**
- `400` ved ugyldigt navn eller password (`errors.userName` eller `errors.password`)
- `401` fra `login` ved forkert navn eller password. Svaret er det samme, uanset hvad der er forkert. `401` fra `me` uden login
- `409` fra `register`, hvis navnet er taget
- `429` fra `login`, når kontoen er låst. Det sker i 5 minutter efter 5 forkerte passwords
- `400` med titlen `Invalid antiforgery token`, hvis tokenet mangler eller er forkert (se nedenfor)

**Login-cookien** hedder `bytebait_login` og er `HttpOnly`, `Secure` og `SameSite=Strict`. Den udløber 7 dage efter, at spilleren sidst var aktiv. Den sendes kun over HTTPS.

#### Antiforgery (S2.13 – lavet)

`GET /api/account/antiforgery` kræver ikke login og svarer `200 OK`:

```json
{
  "token": "CfDJ8..."
}
```

Svaret sætter også cookien `bytebait_antiforgery` (`HttpOnly`, `Secure`, `SameSite=Strict`), som tokenet tjekkes mod.

- Alle `POST`, `PUT`, `PATCH` og `DELETE` til API'et skal sende tokenet i headeren `X-XSRF-TOKEN`. Det gælder også `register`, `login` og `logout`, så en anden side ikke kan logge spilleren ind på sin egen konto.
- Uden gyldigt token: `400` med titlen `Invalid antiforgery token`. Et endpoint, der kræver login, svarer `401` før tokenet tjekkes.
- **Tokenet hører til spilleren.** Hent et nyt efter `register`, `login` og `logout`. Det gamle virker ikke længere.
- Det hele kræver HTTPS. Lokalt skal API'et køre med https-profilen (`https://localhost:7172`), ellers sendes cookierne ikke.

Sådan bruger spillet og Blazor det:

1. `GET /api/account/antiforgery` og gem `token`
2. `POST /api/account/login` med headeren `X-XSRF-TOKEN: <token>`
3. `GET /api/account/antiforgery` igen og brug det nye token til resten af spillets kald

<!-- BESLUTNING | af: @Bmantaz og @Juuul999 | berører: S2.4 (#20), S2.5 (#21), S2.14 (#77) | ejer af beslutningen: @Juuul999 og @Bmantaz (S2.13, #76) -->
> [!NOTE]
> **BESLUTNING fra Frederik (@Juuul999) og Bjarke (@Bmantaz) i S2.13 (#76).** Påvirker S2.4 (#20) og S2.5 (#21, Andreas @Zikka1987) og S2.14 (#77, Frederik @Juuul999).
> Niveau, kast og fangst kræver login og antiforgery-token fra S2.13. Spillet skal logge ind og sende `X-XSRF-TOKEN` ved hvert `POST`. Indtil login-siderne i Blazor (#77) er lavet, kan USB-controlleren på `/game` ikke gemme kast på Test-ByteBait (svaret er `401`).

### Baner, niveauer og vejr (S2.9, S3.2, S3.4)

| Metode | URL | Beskrivelse |
|--------|-----|-------------|
| `GET` | `/api/lanes` | De fire baner med niveauer, krav (antal fisk), spillerens fremskridt og om banen er låst op |
| `GET` | `/api/lanes/{laneId}/weather` | Det aktuelle vejr i banens by (cachet pr. by), se nedenfor. Vejrets effekter på spillet kommer i S3.4 |

#### Vejr pr. bane (S2.9 – lavet)

`GET /api/lanes/{laneId}/weather`

Vejret hentes fra Open-Meteo for banens by og caches i 10 minutter pr. by. Kræver ikke login endnu (kommer med S2.13).

- Kun ét kald pr. by henter fra Open-Meteo ad gangen. Kald, der kommer imens, venter og får samme svar.
- Fejler Open-Meteo, prøves der først igen efter 1 minut. Indtil da svarer endpointet med det samme (det sidste vejr med `stale: true` eller `503`), så spillet aldrig venter på timeouten (5 sek.) ved hvert kald.
- Banernes id (1–4) er de samme som i tabellen `lanes`, som seedes i S2.10.

| Bane | By |
|------|----|
| `1` | Nordborg |
| `2` | Silkeborg |
| `3` | Ringkøbing |
| `4` | Vejle |

**Response `200 OK`**

```json
{
  "laneId": 3,
  "city": "Ringkøbing",
  "temperature": 7.4,
  "windSpeed": 9.2,
  "windGusts": 15.1,
  "precipitation": 0.3,
  "weatherCode": 61,
  "isDay": true,
  "observedAt": "2026-11-18T10:15:00Z",
  "stale": false
}
```

| Felt | Type | Betydning |
|------|------|-----------|
| `temperature` | number | Temperatur i °C |
| `windSpeed` | number | Vindhastighed i m/s (10 m over jorden) |
| `windGusts` | number | Vindstød i m/s |
| `precipitation` | number | Nedbør i mm (regn, byger og sne) |
| `weatherCode` | number | WMO-vejrkode fra Open-Meteo, fx `61` = let regn, `95` = tordenvejr |
| `isDay` | boolean | `false`, når det er mørkt i byen (nat i spillet) |
| `observedAt` | string | Tidspunktet for målingen (ISO 8601, UTC) |
| `stale` | boolean | `true`, hvis Open-Meteo ikke svarede, og det er det sidst hentede vejr |

**Fejl:**
- `404` hvis banen ikke findes
- `503` hvis Open-Meteo ikke svarer, og der aldrig er hentet vejr for byen. Spillet kan så bruge klart vejr og prøve igen senere

<!-- FORSLAG | af: @Bmantaz | berører: S3.2 (#31) | ejer: @Zikka1987, @Bmantaz | status: ikke besluttet -->
> [!WARNING]
> **FORSLAG fra Bjarke (@Bmantaz) – ikke besluttet.**
> Berører S3.2 (#31, delt med Andreas @Zikka1987).
> Fremskridt og oplåsning af baner gemmes i backend og hentes via `GET /api/lanes`.

### Spil: niveau, kast og fangst (S2.7 og S2.8 – lavet)

<!-- BESLUTNING | af: @Bmantaz | berører: S2.4 (#20), S2.5 (#21) | ejer af beslutningen: @Bmantaz (S2.7, #23) -->
> [!NOTE]
> **BESLUTNING fra Bjarke (@Bmantaz) i S2.7 (#23).** Påvirker S2.4 (#20) og S2.5 (#21, Andreas @Zikka1987).
> Der er **ingen session** og intet `sessionId`, som skitsen tidligere sagde. Det følger datamodellen (#96, [data-model.md](data-model.md)): spillet sender `levelId` med hvert kast, og fremskridtet tælles fra spillerens fangede fisk på niveauet.

| Metode | URL | Beskrivelse |
|--------|-----|-------------|
| `POST` | `/api/levels/{levelId}/start` | Start et niveau. Svar: krav og spillerens fremskridt |
| `POST` | `/api/casts` | Samme endpoint som i sprint 1, udvidet med `levelId`. Serveren bestemmer fisken og returnerer den i svaret (`fish`) |
| `POST` | `/api/catches` | Gem resultatet af fighten. Svar: fangsten og niveauets nye fremskridt |

Endpoints kræver login og antiforgery-token (S2.13). De arbejder altid på den spiller, der er logget ind: kast gemmes på spilleren, og fremskridtet tælles kun fra spillerens egne fangster. `401` uden login.

#### Start et niveau

`POST /api/levels/{levelId}/start` (ingen body)

**Response `200 OK`**

```json
{
  "levelId": 1,
  "laneId": 1,
  "laneNumber": 1,
  "levelNumber": 1,
  "requiredFish": 5,
  "caughtFish": 2,
  "completed": false
}
```

| Felt | Type | Betydning |
|------|------|-----------|
| `requiredFish` | number | Antal fisk, der skal fanges for at klare niveauet |
| `caughtFish` | number | Antal fisk, spilleren har fanget på niveauet (mistede fisk tæller ikke) |
| `completed` | boolean | `true`, når `caughtFish` ≥ `requiredFish` |

**Fejl:** `404` hvis niveauet ikke findes.

#### Kast på et niveau

`POST /api/casts` som i sprint 1, med det nye felt `levelId`:

```json
{
  "zone": "green",
  "strength": 0.8,
  "castAt": "2026-11-24T09:15:30.123Z",
  "levelId": 1
}
```

- `levelId` er valgfrit, så kast uden niveau (som i sprint 1) stadig virker. Kun et kast med `levelId` kan ende i en fangst.
- **Fejl:** `400` med `errors.levelId`, hvis niveauet ikke findes.

**Response `201 Created`** for et kast på et niveau (S2.8):

```json
{
  "id": 17,
  "zone": "green",
  "strength": 0.8,
  "castAt": "2026-11-24T09:15:30.123Z",
  "createdAt": "2026-11-24T09:15:30.456Z",
  "levelId": 1,
  "fish": {
    "speciesId": 5,
    "name": "Zander",
    "rarity": "rare",
    "weightKg": 2.41,
    "lengthCm": 50.6
  }
}
```

- `fish` er den fisk, der sidder på krogen. Spillet bruger den til krogning og fight og viser den, når den er fanget.
- `levelId` og `fish` er `null` for et kast uden niveau. `fish` er også `null`, hvis banen endnu ikke har fiskearter (startdata kommer i S2.10).

**Sådan vælges fisken (S2.8)**

1. **Dybde:** `strength` under `0.5` er et blødt kast (lavt vand), `0.5` og derover et hårdt kast (dybt vand). Kun arter på banen med den dybde kan bide. Har banen ingen arter dér, kan alle banens arter bide.
2. **Sjældenhed:** sjælden = basischance × zonens multiplikator, legendarisk = basischance × zonens multiplikator, almindelig = resten op til 100 %. Med startdataene (0,20 og 0,05) giver rød 100/0/0 %, gul 75/20/5 % og grøn 62,5/30/7,5 %.
3. Findes der ingen art med den trukne sjældenhed på dybden, bruges den næste lavere (legendarisk → sjælden → almindelig), og ellers den næste højere. Et kast får altid en fisk, hvis banen har arter.
4. **Art:** én af de passende arter, alle lige sandsynlige.
5. **Størrelse:** vægt og længde trækkes mellem artens min og max med samme tilfældige andel, så en tung fisk også er lang.

Chancer og multiplikatorer læses fra databasen ved hvert kast, så ændringer i admin-panelet (S3.7) gælder med det samme.

<!-- BESLUTNING | af: @Bmantaz | berører: S2.4 (#20), S2.5 (#21), S2.10 (#26) | ejer af beslutningen: @Bmantaz (S2.8, #24) -->
> [!NOTE]
> **BESLUTNING fra Bjarke (@Bmantaz) i S2.8 (#24).** Påvirker S2.4 og S2.5 (Andreas @Zikka1987) og S2.10 (Frederik @Juuul999).
> Grænsen mellem blødt og hårdt kast er `strength = 0.5`. Startdataene i S2.10 bør give hver bane mindst én almindelig art på både lavt og dybt vand, så begge slags kast har fisk at vælge imellem.

#### Gem en fangst

`POST /api/catches`

```json
{
  "castId": 17,
  "result": "caught"
}
```

| Felt | Type | Krav |
|------|------|------|
| `castId` | number | Kastet, fighten hørte til (fra svaret på `POST /api/casts`) |
| `result` | string | `"caught"` eller `"lost"` |

**Response `201 Created`**

```json
{
  "id": 4,
  "castId": 17,
  "result": "caught",
  "resolvedAt": "2026-11-24T09:16:02.512Z",
  "progress": {
    "levelId": 1,
    "laneId": 1,
    "laneNumber": 1,
    "levelNumber": 1,
    "requiredFish": 5,
    "caughtFish": 3,
    "completed": false
  }
}
```

**Fejl:**
- `400` ved ugyldigt `result` eller manglende felter
- `404` hvis kastet ikke findes eller tilhører en anden spiller
- `409` hvis
  - fighten for kastet allerede er gemt (én fangst pr. kast)
  - kastet ikke blev lavet på et niveau
  - `result` er `"caught"`, men kastet har ingen fisk (`fish` var `null`). `"lost"` kan stadig gemmes

<!-- BESLUTNING | af: @Bmantaz | berører: S2.4 (#20), S2.5 (#21) | ejer af beslutningen: @Bmantaz (S2.8, #24) -->
> [!NOTE]
> **BESLUTNING fra Bjarke (@Bmantaz) i S2.8 (#24).** Påvirker S2.4 (#20) og S2.5 (#21, Andreas @Zikka1987).
> Fisken forudbestemmes på serveren med RNG i det øjeblik, kastet gemmes – ud fra kastets zone, fangstchancerne og kast-multiplikatoren. Spillet får fisken i svaret fra `POST /api/casts` og kender den derfor før krogning og fight. Klienten sender aldrig art eller størrelse, så man ikke kan snyde. `POST /api/catches` gemmer kun, om fisken blev fanget eller mistet.

### Hub (S3.5)

| Metode | URL | Beskrivelse |
|--------|-----|-------------|
| `GET` | `/api/hub` | Spillerens penge, usolgte fisk, eget udstyr og udstyr til salg |
| `POST` | `/api/hub/sell` | Sælg fisk: `{ "catchIds": [1, 2, 3] }` |
| `POST` | `/api/hub/buy` | Køb udstyr: `{ "equipmentId": 4 }`. `409` hvis der ikke er penge nok |

### Akvarium (S3.6)

<!-- FORSLAG | af: @Bmantaz | berører: S3.6 (#35) | ejer: @Juuul999 | status: ikke besluttet -->
> [!WARNING]
> **FORSLAG fra Bjarke (@Bmantaz) – ikke besluttet.**
> Berører S3.6 (#35, Frederik @Juuul999). Hele tabellen nedenfor er et forslag.

| Metode | URL | Beskrivelse |
|--------|-----|-------------|
| `GET` | `/api/aquarium` | Spillerens fisk i akvariet (art, størrelse, sted, tidspunkt) |
| `POST` | `/api/aquarium` | Flyt en fangst til akvariet i stedet for at sælge den: `{ "catchId": 1 }` |
| `DELETE` | `/api/aquarium/{catchId}` | Fjern en fisk fra akvariet |
| `GET` | `/api/aquarium/shared/{shareCode}` | Et delt akvarium. Kræver ikke login |

### Admin (S2.11, S3.7, S3.8)

<!-- FORSLAG | af: @Bmantaz | berører: S2.11 (#27), S3.7 (#36), S3.8 (#37) | ejer: @Juuul999 | status: ikke besluttet -->
> [!WARNING]
> **FORSLAG fra Bjarke (@Bmantaz) – ikke besluttet.**
> Berører S2.11 (#27), S3.7 (#36) og S3.8 (#37, Frederik @Juuul999). Hele tabellen nedenfor er et forslag.

| Metode | URL | Beskrivelse |
|--------|-----|-------------|
| `GET`, `POST` | `/api/admin/species` | List og opret fiskearter |
| `GET`, `PUT`, `DELETE` | `/api/admin/species/{id}` | Hent, ret og slet en fiskeart (sjældenhed, pris, bane) |
| `GET`, `PUT` | `/api/admin/catch-chances` | Fangstchancer og kast-multiplikatorer |
| `PUT` | `/api/admin/equipment/{id}` | Ret pris på udstyr |
| `PUT` | `/api/admin/levels/{id}` | Ret antal fisk pr. niveau |
| `GET` | `/api/admin/stats/catches` | Antal fangster pr. art og bane |

## WiFi-kasteprøve til #4

I Development og Staging åbner `GET /api/test/wifi-throw` en testside med et nyt
test-ID (GUID). Siden lytter på `GET /api/test/wifi-throw/{id}/events` med
Server-Sent Events (SSE). Hændelsen `ready` bekræfter, at modtageren er registreret.
Sessionen lever i API-processens hukommelse, mens forbindelsen er åben.

Oplà'en sender `POST /api/test/wifi-throw/{id}` med `Content-Type: text/plain`.
Body er én ASCII-linje fra [protokollen](protocol.md), fx `THROW:72\n`, med heltal
0–100 og LF eller CRLF som linjeafslutning. API'et videresender linjen som
SSE-hændelsen `throw`. Sidens simulationsknap sender samme format med headeren
`X-Probe-Source: simulated`, som giver hændelsen `simulated-throw`. Kun `throw`
øger sidens fysiske tæller. Prøven skriver ingen spil- eller testdata i databasen.

| HTTP-status | Betydning |
|---|---|
| `204` | Beskeden er lagt i den åbne modtagers kø. Visning verificeres i browseren. |
| `400` / `415` | Ugyldig linje eller kilde / forkert medietype. |
| `409` | POST mangler en aktiv modtager, eller en anden forbindelse lytter allerede på ID'et. |
| `503` | Modtagerens kø er fuld, eller alle testforbindelser er optaget. |
| `404` | Prøvens ruter er utilgængelige i Production. |

Se [hardwareprøven](../Arduino/README.md) for opsætning og godkendelseskriterier.

## Åbne spørgsmål

1. **WiFi direkte til Dokploy (Abdi @AssisDk og Bjarke @Bmantaz, S3.9):** Skal Oplà'en selv kalde API'et over WiFi, eller går alt gennem spillet i browseren? Hvis Oplà'en kalder API'et direkte, kan den ikke bruge login-cookien.
