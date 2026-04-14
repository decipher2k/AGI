using System;
using System.Collections.Generic;
using System.Linq;
using BilligAGI.Modelle;
using UnityEngine;

namespace BilligAGI.Kern
{
    public class SubsymbolikKernel
    {
        private List<LatenterZustand> zustaende;
        private const int EMBEDDING_DIM = 64;

        public SubsymbolikKernel()
        {
            zustaende = new List<LatenterZustand>();
        }

        public LatenterZustand EmbeddeKontext(Erfahrung e)
        {
            float[] vektor = new float[EMBEDDING_DIM];
            if (e == null) return NeuerZustand(vektor, LatentHerkunft.ERFAHRUNG);

            // Hash-basiertes Embedding (vereinfacht)
            string text = $"{e.aktion} {e.kontext} {e.ergebnis}";
            for (int i = 0; i < text.Length; i++)
                vektor[i % EMBEDDING_DIM] += (float)text[i] / 256f;

            // Belohnung eincodieren
            vektor[0] = e.belohnung;

            // VAKOG eincodieren
            if (e.vakog != null)
            {
                vektor[1] = e.vakog.visuell;
                vektor[2] = e.vakog.auditiv;
                vektor[3] = e.vakog.kinesthetisch;
                vektor[4] = e.vakog.olfaktorisch;
                vektor[5] = e.vakog.gustatorisch;
            }

            // Normalisieren
            Normalisiere(vektor);

            var zustand = NeuerZustand(vektor, LatentHerkunft.ERFAHRUNG);
            zustand.quellId = e.id;
            zustaende.Add(zustand);
            return zustand;
        }

        public List<LatenterZustand> Aehnlichste(float[] query, int k = 5)
        {
            if (query == null || zustaende.Count == 0)
                return new List<LatenterZustand>();

            var scored = zustaende
                .Select(z => (zustand: z, score: CosineSimilarity(query, z.vektor)))
                .OrderByDescending(x => x.score)
                .Take(k)
                .Select(x => x.zustand)
                .ToList();

            return scored;
        }

        public void FusionSymbolischSubsymbolisch(
            Dictionary<string, float> konzeptKonfidenzen)
        {
            // Cluster finden und mit symbolischen Konzepten abgleichen
            var cluster = EinfacheClustering(3);
            foreach (var c in cluster)
            {
                if (c.Count < 2) continue;
                // Pruefen ob alle Elemente eines Clusters das gleiche Konzept teilen
                var gemeinsameLabels = c
                    .Where(z => !string.IsNullOrEmpty(z.label))
                    .Select(z => z.label)
                    .GroupBy(l => l)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault();

                if (gemeinsameLabels != null && gemeinsameLabels.Count() >= c.Count / 2)
                {
                    string label = gemeinsameLabels.Key;
                    if (konzeptKonfidenzen.ContainsKey(label))
                        konzeptKonfidenzen[label] = Mathf.Min(1f, konzeptKonfidenzen[label] + 0.05f);
                }
            }
        }

        public bool ErkenneVerdecktesMuster()
        {
            if (zustaende.Count < 5) return false;

            var cluster = EinfacheClustering(5);
            foreach (var c in cluster)
            {
                // Cluster ohne Label = verdecktes Muster
                bool hatLabels = c.Any(z => !string.IsNullOrEmpty(z.label));
                if (!hatLabels && c.Count >= 3)
                {
                    Debug.Log($"[Subsymbolik] Verdecktes Muster entdeckt: {c.Count} aehnliche Zustaende ohne Label.");
                    return true;
                }
            }
            return false;
        }

        public void SetzeLabel(string quellId, string label)
        {
            var z = zustaende.FirstOrDefault(x => x.quellId == quellId);
            if (z != null) z.label = label;
        }

        private LatenterZustand NeuerZustand(float[] vektor, LatentHerkunft herkunft)
        {
            return new LatenterZustand
            {
                id = Guid.NewGuid().ToString(),
                vektor = vektor,
                herkunft = herkunft,
                zeitstempel = DateTime.UtcNow.ToString("o")
            };
        }

        private List<List<LatenterZustand>> EinfacheClustering(int k)
        {
            // K-Means light (vereinfacht)
            var cluster = new List<List<LatenterZustand>>();
            for (int i = 0; i < k; i++)
                cluster.Add(new List<LatenterZustand>());

            if (zustaende.Count == 0) return cluster;

            // Zufaellige Zentroide
            var zentroide = new float[k][];
            for (int i = 0; i < k; i++)
            {
                int idx = UnityEngine.Random.Range(0, zustaende.Count);
                zentroide[i] = (float[])zustaende[idx].vektor.Clone();
            }

            // 5 Iterationen
            for (int iter = 0; iter < 5; iter++)
            {
                foreach (var c in cluster) c.Clear();

                foreach (var z in zustaende)
                {
                    int bester = 0;
                    float besteScore = -1f;
                    for (int i = 0; i < k; i++)
                    {
                        float sim = CosineSimilarity(z.vektor, zentroide[i]);
                        if (sim > besteScore)
                        {
                            besteScore = sim;
                            bester = i;
                        }
                    }
                    cluster[bester].Add(z);
                }

                // Zentroide aktualisieren
                for (int i = 0; i < k; i++)
                {
                    if (cluster[i].Count == 0) continue;
                    zentroide[i] = new float[EMBEDDING_DIM];
                    foreach (var z in cluster[i])
                        for (int d = 0; d < EMBEDDING_DIM; d++)
                            zentroide[i][d] += z.vektor[d];
                    for (int d = 0; d < EMBEDDING_DIM; d++)
                        zentroide[i][d] /= cluster[i].Count;
                }
            }

            return cluster;
        }

        private float CosineSimilarity(float[] a, float[] b)
        {
            if (a == null || b == null) return 0f;
            int len = Mathf.Min(a.Length, b.Length);
            float dot = 0f, magA = 0f, magB = 0f;
            for (int i = 0; i < len; i++)
            {
                dot += a[i] * b[i];
                magA += a[i] * a[i];
                magB += b[i] * b[i];
            }
            float denom = Mathf.Sqrt(magA) * Mathf.Sqrt(magB);
            return denom > 0 ? dot / denom : 0f;
        }

        private void Normalisiere(float[] v)
        {
            float norm = 0f;
            for (int i = 0; i < v.Length; i++) norm += v[i] * v[i];
            norm = Mathf.Sqrt(norm);
            if (norm > 0)
                for (int i = 0; i < v.Length; i++) v[i] /= norm;
        }
    }
}
