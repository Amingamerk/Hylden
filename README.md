# Hylden

Ugens dagligvaretilbud i butikkerne omkring Odense, en prisgraf pr. vare og en indkøbsliste
der finder ud af hvor det er billigst. Alt kører i browseren som en statisk Blazor
WebAssembly-side på GitHub Pages. Der er ingen server og ingen database.

## Sådan hænger det sammen

```
GitHub Action (hver morgen)
   └─ Dagligvarer.Fetcher  →  henter tilbud fra Tjek (eTilbudsavis)
        └─ skriver JSON i src/Dagligvarer.Web/wwwroot/data/
             ├─ latest.json                butikker + gældende tilbud
             └─ snapshots/ÅÅÅÅ-MM-DD.json  billigste pris pr. vare og kæde den dag
   └─ committer filerne, bygger Dagligvarer.Web og udgiver på GitHub Pages

Browseren
   └─ Dagligvarer.Web (Blazor WebAssembly) læser JSON-filerne
        └─ indkøbslisten gemmes i browserens localStorage
```

Prishistorik findes ikke i noget gratis API, så den bygges af snapshot-filerne: en ny fil
pr. dag. Grafen bliver mere interessant jo længere projektet har kørt. Øverst på siden
står hvornår data sidst er hentet.

## Projekter

- `src/Dagligvarer.Core`: modeller, `TextKey` (sammenligning af varenavne), `Categorizer`
  (madkategorier), `ShoppingPlanner` (billigste butik pr. vare, med maks. antal butikker).
- `src/Dagligvarer.Fetcher`: konsolprogram der henter fra Tjek og skriver JSON.
- `src/Dagligvarer.Web`: selve siden (Blazor WebAssembly).
- `tests`: xUnit-tests af kategorisering og indkøbsplanlægning.

## Kør lokalt

Kræver [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```
dotnet run --project src/Dagligvarer.Fetcher   # henter friske data (valgfrit, der ligger allerede data)
dotnet run --project src/Dagligvarer.Web       # starter siden
dotnet test                                    # tests
```

## Udgivelse

`.github/workflows/deploy.yml` kører hver morgen, ved push til `main` og manuelt
(fanen Actions, Run workflow). Under Settings, Pages skal kilden være "GitHub Actions".

## Placering

Søgningen bruger et groft punkt i Odense (koordinat rundet til to decimaler) og en radius
på 1 km. Det står i `src/Dagligvarer.Fetcher/AppOptions.cs`. En præcis adresse skal aldrig
skrives i repoet.

## Datakilde

Tilbud og butikker kommer fra Tjek (firmaet bag eTilbudsavis), `squid-api.tjek.com/v2`.
API'et er ikke officielt dokumenteret og kan ændre sig. Fejler hentningen, fejler Action-kørslen,
og de seneste data bliver liggende.
