using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MATTAR.GraphAnalysis;

public class EpidemicSourceInferenceGeo
{
    private readonly double beta;
    private readonly double mu;
    private readonly double k; // β / (β + μ)

    public EpidemicSourceInferenceGeo(double beta = 0.35, double mu = 0.12)
    {
        this.beta = beta;
        this.mu = mu;
        this.k = beta / (beta + mu);
    }

    // ====================== DISTANCE HAVERSINE (réelle) ======================
    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0; // rayon de la Terre en km

        double dLat = ToRadians(lat2 - lat1);
        double dLon = ToRadians(lon2 - lon1);

        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c; // distance en km
    }

    private double Distance(Node a, Node b)
    {
        return HaversineDistance(a.Lat, a.Lon, b.Lat, b.Lon);
    }

    // ====================== CONSTRUCTION DU GRAPHE DE CONTACTS ======================
    private Dictionary<int, List<int>> BuildContactGraph(List<Node> infected, double maxContactKm = 5.0)
    {
        var graph = new Dictionary<int, List<int>>();
        foreach (var node in infected)
            graph[node.Id] = new List<int>();

        for (int i = 0; i < infected.Count; i++)
        {
            for (int j = i + 1; j < infected.Count; j++)
            {
                if (Distance(infected[i], infected[j]) <= maxContactKm)
                {
                    graph[infected[i].Id].Add(infected[j].Id);
                    graph[infected[j].Id].Add(infected[i].Id);
                }
            }
        }
        return graph;
    }

    // ====================== FORMULE FERMÉE POUR I₀(n) (comme sur le tableau) ======================
    private double ComputeI0ClosedForm(int n, double I0_0, double[] totalInfected)
    {
        if (n < 0) return I0_0;

        double[] a = new double[n + 1];
        for (int i = 0; i <= n; i++)
        {
            double I = Math.Max(totalInfected[i], 1.0);
            a[i] = 1.0 - (k / I);
            if (a[i] <= 0) return 0.0; // probabilité nulle
        }

        // Produit ∏ a_i en espace log (très stable)
        double logProd = 0.0;
        for (int i = 0; i <= n; i++)
            logProd += Math.Log(a[i]);

        double prodAll = Math.Exp(logProd);

        // Somme des termes ∑ (produit partiel)
        double sum = 0.0;
        for (int j = 0; j <= n; j++)
        {
            double logPartial = 0.0;
            for (int i = j + 1; i <= n; i++)
                logPartial += Math.Log(a[i]);

            double partialProd = (j == n) ? 1.0 : Math.Exp(logPartial);
            sum += partialProd;
        }

        return I0_0 * prodAll + k * sum;
    }

    // ====================== CALCUL DE VRAISEMBLANCE PLUS PRÉCIS ======================
    // On utilise maintenant :
    // - l'arbre BFS pour déterminer le nombre max de couches (générations)
    // - une progression linéaire réaliste du nombre total d'infectés (basée sur les données observées)
    // - la formule fermée I₀(final) comme score de vraisemblance
    private double ScoreAsSource(int sourceId, List<Node> infected, Dictionary<int, List<int>> graph)
    {
        var infectedSet = new HashSet<int>(infected.Select(n => n.Id));
        var queue = new Queue<int>();
        var visited = new HashSet<int>();
        var layer = new Dictionary<int, int>(); // distance depuis la source

        queue.Enqueue(sourceId);
        visited.Add(sourceId);
        layer[sourceId] = 0;

        int maxLayer = 0;
        while (queue.Count > 0)
        {
            int u = queue.Dequeue();
            foreach (int v in graph.GetValueOrDefault(u, new List<int>()))
            {
                if (!visited.Contains(v) && infectedSet.Contains(v))
                {
                    visited.Add(v);
                    layer[v] = layer[u] + 1;
                    maxLayer = Math.Max(maxLayer, layer[v]);
                    queue.Enqueue(v);
                }
            }
        }

        if (visited.Count != infected.Count) return 0.0; // le graphe n'est pas connecté depuis cette source

        // Vraisemblance plus précise :
        // T = nombre de pas de temps proportionnel au diamètre de l'arbre
        int T = Math.Max(20, maxLayer * 3);
        double[] totalInfected = new double[T + 1];
        totalInfected[0] = 1.0;

        // Progression réaliste : on atteint exactement le nombre observé d'infectés à la fin
        double target = infected.Count;
        for (int t = 1; t <= T; t++)
        {
            totalInfected[t] = 1.0 + (target - 1.0) * (t / (double)T);
        }

        double i0Final = ComputeI0ClosedForm(T, 1.0, totalInfected);

        // Score = I₀(final)  (plus il est grand, plus la source est probable)
        return i0Final;
    }

    // ====================== RECHERCHE DU MEILLEUR PATIENT ZÉRO ======================
    public Node FindBestSource(List<Node> infected)
    {
        var graph = BuildContactGraph(infected);
        double bestScore = -1;
        Node bestNode = null;

        foreach (var candidate in infected)
        {
            double score = ScoreAsSource(candidate.Id, infected, graph);
            if (score > bestScore)
            {
                bestScore = score;
                bestNode = candidate;
            }
        }
        return bestNode;
    }

    // ====================== EXPORT GEOJSON (prêt pour Leaflet, geojson.io, QGIS…) ======================
    public void ExportToGeoJson(List<Node> infected, Node bestSource, string filePath = "epidemic_map.geojson")
    {
        var features = new List<object>();

        // Points infectés (rouges)
        foreach (var node in infected)
        {
            features.Add(new
            {
                type = "Feature",
                geometry = new { type = "Point", coordinates = new[] { node.Lon, node.Lat } },
                properties = new { type = "infected", id = node.Id, name = $"Cas {node.Id}" }
            });
        }

        // Patient zéro estimé (gros point jaune)
        if (bestSource != null)
        {
            features.Add(new
            {
                type = "Feature",
                geometry = new { type = "Point", coordinates = new[] { bestSource.Lon, bestSource.Lat } },
                properties = new { type = "source", name = "Patient Zéro Estimé", score = "MAX" }
            });
        }

        // Arbre d'infection (lignes jaunes) depuis le patient zéro
        var graph = BuildContactGraph(infected);
        var parent = new Dictionary<int, int>();
        var queue = new Queue<int>();
        queue.Enqueue(bestSource.Id);
        parent[bestSource.Id] = -1;

        while (queue.Count > 0)
        {
            int u = queue.Dequeue();
            foreach (int v in graph.GetValueOrDefault(u, new List<int>()))
            {
                if (!parent.ContainsKey(v))
                {
                    parent[v] = u;
                    queue.Enqueue(v);

                    var uNode = infected.First(n => n.Id == u);
                    var vNode = infected.First(n => n.Id == v);

                    var lineCoords = new[]
                    {
                        new[] { uNode.Lon, uNode.Lat },
                        new[] { vNode.Lon, vNode.Lat }
                    };

                    features.Add(new
                    {
                        type = "Feature",
                        geometry = new { type = "LineString", coordinates = lineCoords },
                        properties = new { type = "infection_tree" }
                    });
                }
            }
        }

        // Zone probable autour du patient zéro (cercle de 8 km)
        features.Add(new
        {
            type = "Feature",
            geometry = new { type = "Point", coordinates = new[] { bestSource.Lon, bestSource.Lat } },
            properties = new { type = "source_area", radius_km = 8.0 }
        });

        var geoJson = new
        {
            type = "FeatureCollection",
            features
        };

        string json = JsonSerializer.Serialize(geoJson, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);

        Console.WriteLine($"✅ GeoJSON exporté → {filePath}");
        Console.WriteLine($"   Patient zéro estimé : Node {bestSource.Id}");
    }
}
