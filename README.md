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
- **Hardware:** Arduino

## Hemmeligheder

Koder, nøgler og connection strings må **aldrig** committes. Følgende er ignoreret i `.gitignore`:

- `arduino_secrets.h` / `secrets.h` (WiFi-koder, API-nøgler)
- `.env`-filer (fx Neon connection string)
- `appsettings.Development.json` og andre lokale appsettings
- Certifikater og nøglefiler (`*.pem`, `*.key`, `*.pfx` …)

Brug i stedet `dotnet user-secrets` eller miljøvariabler lokalt, og del eksempel-filer (fx `.env.example`) uden rigtige værdier.
