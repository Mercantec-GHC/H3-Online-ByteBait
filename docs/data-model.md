# Datamodel

Datamodellen for ByteBait (S1.11). Tabellerne ligger i Neon og oprettes af migrations i `Backend/ByteBait.Data`. Navne er på engelsk og i snake_case, og enums gemmes som tekst med små bogstaver, fx `rarity = 'rare'`.

Ejer: Frederik (@Juuul999). Ændringer i modellen laves med en ny migration, se [Backend/README.md](../Backend/README.md#migrations).

## ER-diagram

```mermaid
erDiagram
    lanes ||--|{ levels : "har"
    lanes ||--|{ fish_species : "har"
    players ||--o{ casts : "kaster"
    players ||--o{ player_equipment : "ejer"
    equipment ||--o{ player_equipment : "ejes af"
    levels ||--o{ casts : "kastet på"
    fish_species ||--o{ casts : "på krogen"
    casts ||--o| catches : "ender som"

    lanes {
        int id PK
        int number UK "1-4"
        string name "fx Nordborg Sø"
        string city
        double latitude
        double longitude
    }
    levels {
        int id PK
        int lane_id FK
        int number "1 eller 2"
        int required_fish
    }
    fish_species {
        int id PK
        int lane_id FK
        string name UK
        string rarity "common, rare, legendary"
        string depth "shallow, deep"
        double min_weight_kg
        double max_weight_kg
        double min_length_cm
        double max_length_cm
        int price
    }
    rarity_chances {
        string rarity PK "rare, legendary"
        double base_chance
    }
    zone_multipliers {
        string zone PK "red, yellow, green"
        double multiplier
    }
    equipment {
        int id PK
        string type "rod, bait, line"
        string name UK
        int price
        int power
    }
    players {
        uuid id PK
        string user_name UK
        int money
        string aquarium_share_code UK
        timestamptz created_at
    }
    player_equipment {
        uuid player_id PK, FK
        int equipment_id PK, FK
        timestamptz purchased_at
    }
    casts {
        int id PK
        string zone
        double strength "0-1"
        timestamptz cast_at
        timestamptz created_at
        uuid player_id FK
        int level_id FK
        int fish_species_id FK
        double weight_kg
        double length_cm
    }
    catches {
        int id PK
        int cast_id FK, UK
        string result "caught, lost"
        timestamptz resolved_at
        timestamptz sold_at
        int sold_for
        bool in_aquarium
    }
```

`rarity_chances` og `zone_multipliers` har ingen relationer. De er indstillinger, som spillogikken læser, når fisken vælges.

## Tabellerne

| Tabel | Indhold |
|-------|---------|
| `lanes` | De fire baner. Koordinaterne bruges til vejret fra Open-Meteo |
| `levels` | To niveauer pr. bane med antal fisk, der skal fanges |
| `fish_species` | Fiskearter pr. bane med sjældenhed, dybde, størrelse og pris |
| `rarity_chances` | Basischancen for sjældne og legendariske fisk |
| `zone_multipliers` | Kastzonens multiplikator: rød ×0, gul ×1, grøn ×1,5 |
| `equipment` | Stænger, madding og liner til salg i hub'en |
| `players` | Spillerkonti med penge og kode til det delte akvarium |
| `player_equipment` | Hvilket udstyr hver spiller ejer |
| `casts` | Hvert kast. Fra sprint 2 også spilleren, niveauet og den fisk, serveren har valgt |
| `catches` | Hvordan fighten endte, og om fisken er solgt eller i akvariet |

## Sådan bruges modellen

**Et kast og en fangst (S2.7, S2.8).** Når spillet sender et kast, vælger serveren fisken og gemmer art, vægt og længde på kastet. Når fighten er slut, gemmes en række i `catches` med `caught` eller `lost`. Der kan kun være én fangst pr. kast.

**Fangstchancer.** Chancen for en sjælden fisk er `base_chance × multiplier`, fx 20 % × 1,5 = 30 % ved et grønt kast. Almindelige fisk har ingen række: deres chance er resten op til 100 %, så chancerne altid giver 100 % tilsammen. Admin-panelet (S3.7) skal sikre, at summen af de sjældne chancer gange den største multiplikator ikke bliver over 100 %, for det kan databasen ikke tjekke på tværs af rækker.

**Dybde.** Et blødt kast lander på lavt vand og et hårdt kast på dybt vand. Spillogikken vælger kun mellem de arter på banen, der har den dybde.

**Udstyr.** `power` er ét tal pr. udstyr, og spillogikken bestemmer, hvad det gør for hver type. Det bedste udstyr af hver type, som spilleren ejer, er det, der bruges.

**Hub og akvarium (S3.5, S3.6).** En fanget fisk bliver liggende, indtil den sælges eller flyttes til akvariet. `sold_for` gemmer prisen ved salget, så gamle salg ikke ændrer sig, når admin retter priserne. Et akvarium deles via `players.aquarium_share_code`.

**Spillerkonti (S2.13).** `players` bliver til brugertabellen for ASP.NET Core Identity. Id'et er et `Guid` og ændrer sig ikke, så kast og penge bliver ved med at høre til samme spiller.

**Statistik (S3.8).** Antal fanget og mistet pr. art findes ved at tælle `catches` grupperet efter `casts.fish_species_id`.

## Beslutninger

| Beslutning | Hvorfor |
|------------|---------|
| Fremskridt gemmes ikke. Hvor mange fisk spilleren har fanget på et niveau, og om en bane er låst op, tælles fra `catches` | Tallet kan aldrig komme ud af trit med fangsterne. Retter admin et krav, gælder det med det samme |
| Ingen tabel til sessions. Kastet gemmer `level_id` | Sessions giver halvfærdige rækker, når spilleren lukker browseren midt i et niveau. `level_id` er nok til at vide, hvor fisken blev fanget, og til at tælle fremskridt. Hvis spil-API'et får brug for en session, kan den laves senere uden at ændre resten |
| Fisken gemmes på kastet og ikke på fangsten | Serveren vælger fisken, før fighten starter (Bjarkes beslutning i API-kontrakten), så kastet findes, før resultatet gør |
| Fangsten har ikke sit eget `player_id` | Spilleren står på kastet. Ét sted at slå det op betyder, at de to aldrig kan være uenige |
| Kravet på 10, 15, 20 og 25 fisk pr. bane fordeles som 5+5, 7+8, 10+10 og 12+13 | Produktbeskrivelsen siger "fordelt på banens 2 niveauer". Kravet ligger pr. niveau, så det kan rettes i admin |
| Arter, niveauer, baner og udstyr, der er brugt, kan ikke slettes | Statistik og fangster peger på dem. Admin-panelet skal svare `409`, når databasen afviser en sletning |
| Sletter man en spiller, slettes kast, fangster og udstyr også | Spillerens data hører kun til spilleren |
| Tidspunkter i UTC, penge og priser som heltal | Samme regler som i API-kontrakten |

## Constraints i databasen

Databasen afviser ugyldige værdier, også hvis API'et glemmer at tjekke dem:

- Enums må kun have de tilladte værdier (`ck_*_zone`, `ck_*_rarity`, osv.)
- `casts.strength` mellem 0 og 1, `weight_kg` og `length_cm` over 0
- `casts`: art, vægt og længde er enten alle sat eller alle tomme, fordi serveren vælger dem samtidig
- `levels.number` er 1 eller 2
- `fish_species`: min-vægt og min-længde over 0, max mindst lige så stor som min, pris mindst 0
- `rarity_chances.base_chance` mellem 0 og 1, kun for `rare` og `legendary`
- `zone_multipliers.multiplier` mindst 0
- `players.money` mindst 0, så et køb aldrig giver negativ saldo
- `catches`: kun fanget fisk kan sælges eller komme i akvariet, ikke begge dele, og et salg har både tidspunkt og pris
- Unikke navne på arter, udstyr og spillere, unikt banenummer og niveau 1–2 kun én gang pr. bane

## Startdata

`rarity_chances` og `zone_multipliers` får eksempeltallene fra produktbeskrivelsen i migrationen `GameModel`. Baner, niveauer, fiskearter og udstyr kommer i S2.10 (#26) med mindst én almindelig, én sjælden og én legendarisk art pr. bane.
