# Backend

ASP.NET Core Web API. Forbinder til Neon.tech (PostgreSQL) via Entity Framework Core og Npgsql.

Connection string gemmes med `dotnet user-secrets` eller som miljøvariabel – aldrig i `appsettings.json`.

## Projekter

| Projekt | Indhold |
|---------|---------|
| `ByteBait.Data` | DbContext, entiteter, EF-konfiguration og migrations |
| `ByteBait.Data.Tests` | Tests af datamodellen og databasen |

API-projektet (S1.9) refererer til `ByteBait.Data` og tilføjer databasen i `Program.cs`:

```csharp
builder.Services.AddByteBaitData(builder.Configuration);
```

## Neon

Neon-projektet hedder `ByteBait` (PostgreSQL 16, Frankfurt). Databasen hedder `bytebait`, og der er en branch pr. miljø:

| Neon-branch | Bruges af | Connection string ligger i |
|-------------|-----------|----------------------------|
| `production` | Main-ByteBait | Dokploy, miljøvariablen `ConnectionStrings__Default` |
| `test` | Test-ByteBait | Dokploy, miljøvariablen `ConnectionStrings__Default` |
| `dev` | Lokal udvikling | `dotnet user-secrets` på din egen maskine |

CI kører mod en PostgreSQL 16-container og bruger ikke Neon.

Brug den pooled forbindelse fra Neon (værtsnavnet indeholder `-pooler`). Npgsql forstår ikke `postgresql://`-formatet, så den skal skrives sådan her:

```text
Host=<endpoint>-pooler.<region>.aws.neon.tech;Database=bytebait;Username=<bruger>;Password=<kode>;SSL Mode=Require
```

## Kom i gang lokalt

Fra roden af repoet:

```bash
# Installerer dotnet-ef i den version, der står i dotnet-tools.json
dotnet tool restore

# Gem connection string til Neon-branchen dev
dotnet user-secrets set "ConnectionStrings:Default" "<connection string>" -p Backend/ByteBait.Data

# Opret tabellerne
dotnet ef database update -p Backend/ByteBait.Data
```

Alle projekter i `Backend/` bruger samme `UserSecretsId` (se `Backend/Directory.Build.props`), så du kun skal sætte connection string én gang.

## Migrations

```bash
# Ny migration efter en ændring i en entitet
dotnet ef migrations add <Navn> -p Backend/ByteBait.Data -o Migrations

# Se SQL'en, før den køres mod Neon
dotnet ef migrations script --idempotent -p Backend/ByteBait.Data
```

Glemmer man at lave en migration, fejler testen `Model_has_no_changes_missing_a_migration` i CI.

## Konventioner i databasen

- Tabeller og kolonner er i snake_case (fx `casts.cast_at`), så man ikke skal bruge anførselstegn i SQL
- Tidspunkter gemmes i UTC (`timestamp with time zone`)
- Enums gemmes som tekst med små bogstaver, fx `zone = 'green'`
- CHECK-constraints i databasen afviser ugyldige værdier, fx `strength` uden for 0 til 1

## Tests

```bash
dotnet test
```

Databasetestene kører kun, hvis miljøvariablen `ConnectionStrings__Default` er sat. Det er den i CI. Testene læser ikke user-secrets, så de kan ikke komme til at skrive i Neon.
