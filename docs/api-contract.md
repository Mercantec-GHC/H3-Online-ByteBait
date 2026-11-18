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
| CSRF | Fra S2.13: endpoints, der ændrer data (`POST`, `PUT`, `DELETE`), kræver et antiforgery-token |
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
| `500` | Uventet fejl på serveren |

## Sprint 1

### Gem et kast

`POST /api/casts`

Spillet kalder endpointet, når Oplà'en har registreret et kast, og browseren har aflæst zonen på timingmåleren.
I sprint 1 kræver endpointet ikke login.

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

### Konto (S2.13)

Endpoints og navne er fastlagt i S2.13 (#76).

| Metode | URL | Beskrivelse |
|--------|-----|-------------|
| `POST` | `/api/account/register` | Opret konto: `{ "userName": "...", "password": "..." }`. Kræver ikke login. `409` hvis navnet er taget |
| `POST` | `/api/account/login` | Log ind: `{ "userName": "...", "password": "..." }`. Kræver ikke login. Sætter login-cookien |
| `POST` | `/api/account/logout` | Log ud. Sletter login-cookien |
| `GET` | `/api/account/me` | Aktuel spiller (se forslaget nedenfor). `401` uden login |

<!-- FORSLAG | af: @Bmantaz | berører: S2.13 (#76) | ejer: @Juuul999 | status: ikke besluttet -->
> [!WARNING]
> **FORSLAG fra Bjarke (@Bmantaz) – ikke besluttet.**
> Berører S2.13 (#76, Frederik @Juuul999).
> Svaret fra `GET /api/account/me`: `{ "id": "<guid>", "userName": "...", "roles": ["admin"], "money": 120 }`.

### Baner, niveauer og vejr (S2.9, S3.2, S3.4)

| Metode | URL | Beskrivelse |
|--------|-----|-------------|
| `GET` | `/api/lanes` | De fire baner med niveauer, krav (antal fisk), spillerens fremskridt og om banen er låst op |
| `GET` | `/api/lanes/{laneId}/weather` | Det aktuelle vejr i banens by (cachet pr. by), se nedenfor. Vejrets effekter på spillet kommer i S3.4 |

#### Vejr pr. bane (S2.9 – lavet)

`GET /api/lanes/{laneId}/weather`

Vejret hentes fra Open-Meteo for banens by og caches i 10 minutter pr. by. Kræver ikke login endnu (kommer med S2.13).

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
- `503` hvis Open-Meteo ikke svarer, og der aldrig er hentet vejr for byen. Spillet kan så bruge klart vejr

<!-- FORSLAG | af: @Bmantaz | berører: S3.2 (#31) | ejer: @Zikka1987, @Bmantaz | status: ikke besluttet -->
> [!WARNING]
> **FORSLAG fra Bjarke (@Bmantaz) – ikke besluttet.**
> Berører S3.2 (#31, delt med Andreas @Zikka1987).
> Fremskridt og oplåsning af baner gemmes i backend og hentes via `GET /api/lanes`.

### Spil: niveau, kast og fangst (S2.7, S2.8)

| Metode | URL | Beskrivelse |
|--------|-----|-------------|
| `POST` | `/api/levels/{levelId}/start` | Start et niveau. Svar: `sessionId`, krav, antal fangede fisk og vejrets effekter |
| `POST` | `/api/casts` | Samme endpoint som i sprint 1, udvidet med `sessionId`. Serveren bestemmer fisken med RNG og returnerer den i svaret (`fish`: art, sjældenhed, vægt, længde) |
| `POST` | `/api/catches` | Gem resultatet af fighten: `{ "castId": 1, "result": "caught" }` eller `"lost"`. Svar: fangsten og niveauets fremskridt |

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

## Åbne spørgsmål

1. **Antiforgery (Frederik @Juuul999, S2.13):** Hvordan henter Blazor og spillet tokenet, og hvilken header sendes det i?
2. **WiFi direkte til Dokploy (Abdi @AssisDk og Bjarke @Bmantaz, S3.9):** Skal Oplà'en selv kalde API'et over WiFi, eller går alt gennem spillet i browseren? Hvis Oplà'en kalder API'et direkte, kan den ikke bruge login-cookien.
