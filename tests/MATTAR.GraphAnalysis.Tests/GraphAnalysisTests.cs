using MATTAR.GraphAnalysis;
using NUnit.Framework;
using System.Collections.Generic;

namespace MATTAR.GraphAnalysis.Tests;

public class GraphAnalysisTests
{
    [Test]
    public void FindBestSource_WithSampleData_ShouldReturnNode()
    {
        // Arrange
        var model = new EpidemicSourceInferenceGeo(beta: 0.35, mu: 0.12);

        // Données simulées inspirées de la carte de l'épisode
        var infected = new List<Node>
        {
            new Node { Id = 0,  Lat = 34.0522, Lon = -118.2437 },
            new Node { Id = 1,  Lat = 34.0614, Lon = -118.3000 },
            new Node { Id = 2,  Lat = 34.0400, Lon = -118.2500 },
            new Node { Id = 3,  Lat = 34.0800, Lon = -118.2000 },
            new Node { Id = 4,  Lat = 34.0200, Lon = -118.2800 },
            new Node { Id = 5,  Lat = 34.0700, Lon = -118.2200 },
            new Node { Id = 6,  Lat = 34.0500, Lon = -118.2600 },
            new Node { Id = 7,  Lat = 34.0300, Lon = -118.2100 },
            new Node { Id = 8,  Lat = 34.0650, Lon = -118.2350 },
            new Node { Id = 9,  Lat = 34.0450, Lon = -118.2650 },
        };

        // Act
        Node bestSource = model.FindBestSource(infected);

        // Assert
        Assert.IsNotNull(bestSource, "La meilleure source ne devrait pas être nulle.");
        TestContext.WriteLine($"Patient zéro estimé : Node {bestSource.Id}");
        
        // Optionnel: Vérifier l'exportation
        model.ExportToGeoJson(infected, bestSource, "test_output.geojson");
        Assert.IsTrue(System.IO.File.Exists("test_output.geojson"), "Le fichier GeoJSON devrait être généré.");
    }
}
