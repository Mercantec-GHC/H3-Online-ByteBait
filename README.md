# H3-Online-ByteBait

Afsluttende projekt på H3 – et online retro-fiskespil med Arduino-integration.

## Struktur

| Mappe | Indhold |
|-------|---------|
| `Frontend/` | Blazor WebAssembly-app. Spilskærmen er et `<canvas>` styret af JavaScript (ren canvas eller Phaser) |
| `Backend/` | ASP.NET Core Web API, som gemmer resultater m.m. i databasen |
| `Arduino/` | Kode til Arduino-hardwaren |

## Tech-stack

- **Frontend:** Blazor WebAssembly – siderne kører i browseren. Blazor-siden indlejrer canvasset og taler med spilkoden via **JS interop** (fx når en fisk er fanget, og resultatet skal gemmes)
- **Spil:** HTML5 `<canvas>` + JavaScript, evt. med [Phaser](https://phaser.io/) til retro-pixelgrafik
- **Backend:** ASP.NET Core Web API
- **Database:** [Neon.tech](https://neon.tech/) (serverless PostgreSQL)
- **Hardware:** Arduino Oplà IoT Kit (MKR WiFi 1010) – forbindes over både USB og WiFi
- **Deployment:** Skolens Dokploy – Test-ByteBait (fra `dev`) og Main-ByteBait (fra `main`), se [Deployment](#deployment-dokploy)

## Hemmeligheder

Koder, nøgler og connection strings må **aldrig** committes. Følgende er ignoreret i `.gitignore`:

- `arduino_secrets.h` / `secrets.h` (WiFi-koder, API-nøgler)
- `.env`-filer (fx Neon connection string)
- `appsettings.Development.json` og andre lokale appsettings
- Certifikater og nøglefiler (`*.pem`, `*.key`, `*.pfx` …)

Brug i stedet `dotnet user-secrets` eller miljøvariabler lokalt, og del eksempel-filer (fx `.env.example`) uden rigtige værdier.

## Branches og arbejdsgang

- **`dev`** er standard-branch. Alt arbejde kommer ind via pull requests mod `dev`.
- **`main`** bruges kun til releases. Hver fredag merges `dev` til `main` via en release-PR.
- Begge branches er beskyttet: ingen direkte push, mindst én godkendelse, og CI skal være grøn.

1. Tag dit næste issue fra det fastgjorte issue **📋 Sprint-board**.
2. Lav en branch fra `dev`, fx `S1-5-kast-detektion`.
3. Åbn en PR mod `dev` med `Closes #<nr>` i beskrivelsen.
4. En anden reviewer, CI bliver grøn, og PR'en merges. Issuet lukkes, og Dokploy deployer Test-ByteBait automatisk.

## Konventioner

| Område | Regel | Eksempel |
|--------|-------|----------|
| Sprog | **Kun engelsk** i spillet og koden: brugerflade, fejlbeskeder, cookie-politik og -banner, kode, felter, JSON og URL'er | "Username is already taken", `POST /api/account/register`, `{ "userName": "..." }` |
| Fejlsvar fra API'et | ASP.NET Core **ProblemDetails** (RFC 9457) med `title`/`detail` på engelsk | `{ "status": 409, "title": "Username is already taken", ... }` |
| Login | ASP.NET Core Identity med **cookie** (`HttpOnly`, `Secure`, `SameSite=Strict`) – spil og API ligger på samme domæne | – |
| Bruger-ID | Unikt `Guid` pr. spiller i databasen | – |
| Cookies | Kun teknisk nødvendige cookies (login, antiforgery). De kræver ikke samtykke, så login virker uden klik på banneret. Et informationsbanner ("Got it") og cookie-politikken på `/cookies` fortæller, hvilke cookies vi bruger ([cookievejledningen](https://digst.dk/tilsyn/sporingsteknologiomraadet/cookievejledningen/)) | – |

## Deployment (Dokploy)

Spillet deployes på skolens Dokploy i projektet **H3-Online-ByteBait**:

| App | Branch | Deployes når |
|-----|--------|--------------|
| **Test-ByteBait** | `dev` | Der merges til `dev` (Autodeploy) |
| **Main-ByteBait** | `main` | Release-PR'en merges til `main` om fredagen (Autodeploy) |

- Begge apps bygges fra `Dockerfile` i roden af repoet. API'et serverer også Blazor-filerne, så hver app er én container, der lytter på port 8080.
- Da `dev` og `main` kun kan opdateres via en PR med grøn CI, deployes der kun kode, der har bestået CI.
- Domæner: Dokploys standard indtil videre.
- Miljøvariabler sættes i Dokploy under **Environment** – aldrig i repoet:
  - `ASPNETCORE_ENVIRONMENT` (`Staging` i test, `Production` i produktion)
  - `ASPNETCORE_URLS=http://+:8080`
  - `ConnectionStrings__Default` – Neon connection string (test-branch til Test-ByteBait, produktions-branch til Main-ByteBait). Udfyldes, når Neon er oprettet (S1.10).

## Sprints og releases

| Sprint | Periode | Release | Mål |
|--------|---------|---------|-----|
| 1 – Hele kæden | 16.–20. nov | v0.1.0 | Oplà → browser → backend → Neon, kører på Dokploy |
| 2 – Spilbar kerne | 23.–27. nov | v0.2.0 | Fang en fisk på bane 1 kun med Oplà'en |
| 3 – Feature complete | 30. nov – 4. dec | v0.3.0 | Alle skal-krav virker, brugertest. Feature freeze |
| 4 – Finpudsning | 7.–11. dec | v1.0.0 | Fejlrettelser, balancering, docs og demo |

Hver release har et release-issue med den fælles release-DoD. Planen ligger i `.github/issues.json`.

## GitHub Actions

| Workflow | Hvad den gør |
|----------|--------------|
| **CI** | Bygger og tester .NET (mod en PostgreSQL-container, aldrig Neon), tjekker JavaScript-syntaks og kompilerer Arduino-sketchen. Kører ved PR og push til `dev` |
| **Deploy test** | Valgfri reserve: kalder et Dokploy-webhook, hvis secret `DOKPLOY_WEBHOOK_TEST` er sat. Ellers springes den over – deploy sker via Dokploys Autodeploy |
| **Release** | Ved merge til `main`: CI, tag + GitHub Release ud fra sprint-milestonen og lukker milestonen. Main-ByteBait deployes af Dokploys Autodeploy |
| **Sync sprint-plan** | Opretter labels, milestones og issues ud fra `.github/issues.json`. Overskriver ikke tekst i eksisterende issues |
| **Sprint-board** | Opdaterer **📋 Sprint-board** ved ændringer og hver hverdag kl. 07:30 |
| **PR → issue-status** | En PR med `Closes #12` sætter label `i-gang` på #12 |
