# MATTAR.GraphAnalysis

Bibliothèque **.NET (C#)** pour l’**analyse de graphes** avec un premier module d’**inférence de la source d’une épidémie** à partir de cas géolocalisés.

> Dépôt : https://github.com/mattar-shadi/MATTAR.GraphAnalysis

## Fonctionnalités

- **Inférence de “patient zéro” (source la plus probable)**
  - Construction d’un **graphe de contacts** entre cas infectés à partir de la distance géographique.
  - Parcours **BFS** depuis une source candidate pour vérifier la connectivité et estimer des “couches” (générations).
  - Calcul d’un **score de vraisemblance** basé sur une formule fermée `I₀(n)`.
- **Calcul de distance Haversine** (distance réelle sur Terre).
- **Export GeoJSON**
  - Points des cas infectés.
  - Point “source” (patient zéro estimé).
  - Arbre d’infection (lignes).
  - Zone probable autour de la source (rayon paramétrable, actuellement indiqué à 8 km dans les propriétés).

## Structure du projet

```
.
├── src/
│   └── MATTAR.GraphAnalysis/
│       ├── MATTAR.GraphAnalysis.csproj
│       └── EpidemicSourceInferenceGeo.cs
└── .gitignore
```

## Prérequis

- **.NET SDK** compatible avec la cible du projet.
  - Le projet cible actuellement : **`net10.0`** (voir `src/MATTAR.GraphAnalysis/MATTAR.GraphAnalysis.csproj`).

## Installation / Build

À la racine du dépôt :

```bash
dotnet build
```

Pour packager (si vous souhaitez en faire un package) :

```bash
dotnet pack -c Release
```

## Utilisation

Le fichier principal est `EpidemicSourceInferenceGeo.cs`.

### Exemple (C#)

```csharp
using MATTAR.GraphAnalysis;

var infected = new List<Node>
{
    new Node { Id = 1, Lat = 48.8566, Lon = 2.3522 },
    new Node { Id = 2, Lat = 48.8584, Lon = 2.2945 },
    new Node { Id = 3, Lat = 48.8606, Lon = 2.3376 },
};

var inference = new EpidemicSourceInferenceGeo(beta: 0.35, mu: 0.12);

Node best = inference.FindBestSource(infected);
Console.WriteLine($"Best source: {best?.Id}");

inference.ExportToGeoJson(infected, best, filePath: "epidemic_map.geojson");
```

### Détails de l’algorithme (résumé)

1. **Graphe de contacts** : deux nœuds sont connectés si leur distance (Haversine) est ≤ `maxContactKm` (par défaut 5 km).
2. Pour chaque nœud candidat :
   - BFS pour construire des couches (distance depuis la source) et vérifier que tous les infectés sont atteignables.
   - Définition d’un horizon temporel `T` (lié au diamètre en couches).
   - Construction d’une progression linéaire du nombre total d’infectés jusqu’au total observé.
   - Score = `I₀(T)` calculé via `ComputeI0ClosedForm`.

## Paramètres importants

- `beta` (β) et `mu` (μ) dans `EpidemicSourceInferenceGeo`.
- `maxContactKm` dans `BuildContactGraph` (par défaut 5 km).
- `filePath` dans `ExportToGeoJson` (par défaut `epidemic_map.geojson`).

## Sortie GeoJSON

Le fichier GeoJSON généré peut être visualisé avec :

- https://geojson.io/
- **QGIS**
- **Leaflet** (web)

## Roadmap (idées)

- Ajouter des **tests unitaires**.
- Ajouter un **exemple exécutable** (console app) dans un dossier `samples/`.
- Permettre une **pondération des arêtes** (distance / probabilité de contact).
- Utiliser des données temporelles (`InfectionTime`) si disponibles.

## Licence

Aucune licence n’est définie pour le moment. Si vous souhaitez une réutilisation claire (MIT/Apache-2.0/GPL…), ajoutez un fichier `LICENSE`.
