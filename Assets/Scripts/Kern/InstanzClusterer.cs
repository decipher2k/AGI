using System;
using System.Collections.Generic;
using System.Linq;
using BilligAGI.Modelle;
using UnityEngine;

namespace BilligAGI.Kern
{
    /// <summary>
    /// K-Means Clustering fuer ArchetypInstanzen. Kein LLM. Reine Mathematik.
    ///
    /// Statt Claude zu fragen "sind diese Instanzen aehnlich?" berechnet
    /// der Clusterer die Aehnlichkeit aus den numerischen Merkmalen:
    /// - Konfidenz, Zeitstempel, Kontextmerkmale-Overlap, Aspekt
    ///
    /// Wird vom ArchetypenGedaechtnis genutzt um Instanzen automatisch
    /// in Kontextcluster zu gruppieren — ohne LLM-Call.
    ///
    /// Zusaetzlich: Allgemeiner Erfahrungs-Clusterer fuer beliebige float-Vektoren.
    /// </summary>
    public class InstanzClusterer
    {
        private readonly System.Random rng = new System.Random();

        // --- Archetyp-Instanz-Clustering ---

        /// <summary>
        /// Kodiert eine ArchetypInstanz als float-Vektor fuer Clustering.
        /// 8 Dimensionen:
        /// [0] konfidenz
        /// [1] aspekt: 0 = licht, 1 = schatten
        /// [2-6] kontextCluster one-hot: physik, sozial, existenziell, epistemisch, allgemein
        /// [7] zeitstempel (normiert)
        /// </summary>
        public float[] KodiereInstanz(ArchetypInstanz instanz, float maxZeit)
        {
            float[] v = new float[8];
            v[0] = instanz.konfidenz;
            v[1] = instanz.aspekt == "schatten" ? 1f : 0f;

            // One-hot Kontextcluster
            switch (instanz.kontextCluster)
            {
                case "physik":        v[2] = 1f; break;
                case "sozial":        v[3] = 1f; break;
                case "existenziell":  v[4] = 1f; break;
                case "epistemisch":   v[5] = 1f; break;
                default:              v[6] = 1f; break; // allgemein
            }

            v[7] = maxZeit > 0 ? instanz.zeitstempel / maxZeit : 0f;
            return v;
        }

        /// <summary>
        /// Berechnet Textueberlappung zwischen Kontextmerkmalen zweier Instanzen.
        /// Jaccard-Index: |A ∩ B| / |A ∪ B|
        /// </summary>
        public float KontextMerkmaleUeberlappung(ArchetypInstanz a, ArchetypInstanz b)
        {
            if (a.kontextMerkmale == null || b.kontextMerkmale == null) return 0f;
            var setA = new HashSet<string>(a.kontextMerkmale);
            var setB = new HashSet<string>(b.kontextMerkmale);
            if (setA.Count == 0 && setB.Count == 0) return 1f;

            int schnitt = setA.Intersect(setB).Count();
            int vereinigung = setA.Union(setB).Count();
            return vereinigung > 0 ? schnitt / (float)vereinigung : 0f;
        }

        /// <summary>
        /// Findet die N aehnlichsten Instanzen zu einer gegebenen Instanz.
        /// Kombiniert Vektor-Distanz + Kontextmerkmal-Overlap.
        /// </summary>
        public List<(ArchetypInstanz instanz, float aehnlichkeit)> FindeAehnliche(
            ArchetypInstanz ziel, List<ArchetypInstanz> kandidaten, int n = 5)
        {
            float maxZeit = kandidaten.Count > 0
                ? kandidaten.Max(i => i.zeitstempel) : 1f;
            float[] zielVektor = KodiereInstanz(ziel, maxZeit);

            var bewertet = new List<(ArchetypInstanz instanz, float aehnlichkeit)>();
            foreach (var k in kandidaten)
            {
                if (k.id == ziel.id) continue;

                float[] kVektor = KodiereInstanz(k, maxZeit);

                // 60% Vektor-Aehnlichkeit + 40% Kontextmerkmal-Overlap
                float vektorAehn = ZustandsEncoder.KosinusAehnlichkeit(zielVektor, kVektor);
                float merkmaleAehn = KontextMerkmaleUeberlappung(ziel, k);
                float gesamt = 0.6f * vektorAehn + 0.4f * merkmaleAehn;

                bewertet.Add((k, gesamt));
            }

            return bewertet.OrderByDescending(x => x.aehnlichkeit).Take(n).ToList();
        }

        // --- Allgemeines K-Means Clustering ---

        /// <summary>
        /// K-Means Clustering auf float-Vektoren. Reine Mathematik.
        /// Gibt Cluster-Zuordnungen zurueck: Index i → Cluster-ID des i-ten Vektors.
        /// </summary>
        public int[] KMeans(List<float[]> vektoren, int k, int maxIterationen = 50)
        {
            if (vektoren == null || vektoren.Count == 0) return new int[0];
            if (k <= 0 || k > vektoren.Count) k = Math.Min(vektoren.Count, 3);

            int n = vektoren.Count;
            int dim = vektoren[0].Length;
            int[] zuordnung = new int[n];

            // Zufaellige Zentroiden (K-Means++ Initialisierung)
            float[][] zentroiden = InitialisiereZentroiden(vektoren, k);

            for (int iter = 0; iter < maxIterationen; iter++)
            {
                // Zuordnung: Jeder Vektor zum naechsten Zentroid
                bool aenderung = false;
                for (int i = 0; i < n; i++)
                {
                    int naechster = NaechsterZentroid(vektoren[i], zentroiden);
                    if (naechster != zuordnung[i])
                    {
                        zuordnung[i] = naechster;
                        aenderung = true;
                    }
                }

                if (!aenderung) break; // Konvergiert

                // Zentroiden neu berechnen
                zentroiden = BerechneZentroiden(vektoren, zuordnung, k, dim);
            }

            return zuordnung;
        }

        /// <summary>
        /// Bestimmt die optimale Cluster-Anzahl (Elbow-Methode, vereinfacht).
        /// </summary>
        public int OptimaleClusterAnzahl(List<float[]> vektoren, int maxK = 8)
        {
            if (vektoren.Count < 3) return 1;
            maxK = Math.Min(maxK, vektoren.Count);

            float vorherigeInertia = float.MaxValue;
            float groessterAbfall = 0f;
            int besterK = 2;

            for (int k = 1; k <= maxK; k++)
            {
                var zuordnung = KMeans(vektoren, k);
                float inertia = BerechneInertia(vektoren, zuordnung, k);

                if (k > 1)
                {
                    float abfall = vorherigeInertia - inertia;
                    if (abfall > groessterAbfall)
                    {
                        groessterAbfall = abfall;
                        besterK = k;
                    }
                }
                vorherigeInertia = inertia;
            }

            return besterK;
        }

        /// <summary>
        /// Clustert ArchetypInstanzen eines Archetyps und schlaegt
        /// Kontextcluster-Aufteilungen vor.
        /// Gibt Dictionary zurueck: Cluster-ID → Liste von Instanz-IDs.
        /// </summary>
        public Dictionary<int, List<string>> ClustereInstanzen(List<ArchetypInstanz> instanzen)
        {
            if (instanzen.Count < 3)
            {
                // Zu wenige — alle in einen Cluster
                var single = new Dictionary<int, List<string>>();
                single[0] = instanzen.Select(i => i.id).ToList();
                return single;
            }

            float maxZeit = instanzen.Max(i => i.zeitstempel);
            var vektoren = instanzen.Select(i => KodiereInstanz(i, maxZeit)).ToList();

            int k = OptimaleClusterAnzahl(vektoren);
            var zuordnung = KMeans(vektoren, k);

            var ergebnis = new Dictionary<int, List<string>>();
            for (int i = 0; i < zuordnung.Length; i++)
            {
                int cluster = zuordnung[i];
                if (!ergebnis.ContainsKey(cluster))
                    ergebnis[cluster] = new List<string>();
                ergebnis[cluster].Add(instanzen[i].id);
            }

            return ergebnis;
        }

        // --- Private Helfer ---

        private float[][] InitialisiereZentroiden(List<float[]> vektoren, int k)
        {
            // K-Means++ Initialisierung
            int dim = vektoren[0].Length;
            float[][] zentroiden = new float[k][];

            // Erster Zentroid zufaellig
            zentroiden[0] = (float[])vektoren[rng.Next(vektoren.Count)].Clone();

            for (int c = 1; c < k; c++)
            {
                // Waehle naechsten Zentroid mit Wahrscheinlichkeit proportional zur Distanz
                float[] distanzen = new float[vektoren.Count];
                float gesamtDistanz = 0f;

                for (int i = 0; i < vektoren.Count; i++)
                {
                    float minDist = float.MaxValue;
                    for (int j = 0; j < c; j++)
                    {
                        float d = ZustandsEncoder.Distanz(vektoren[i], zentroiden[j]);
                        if (d < minDist) minDist = d;
                    }
                    distanzen[i] = minDist * minDist; // Quadrierte Distanz
                    gesamtDistanz += distanzen[i];
                }

                // Roulette-Wheel Selection
                float schwelle = (float)(rng.NextDouble() * gesamtDistanz);
                float kumulativ = 0f;
                for (int i = 0; i < vektoren.Count; i++)
                {
                    kumulativ += distanzen[i];
                    if (kumulativ >= schwelle)
                    {
                        zentroiden[c] = (float[])vektoren[i].Clone();
                        break;
                    }
                }

                // Fallback
                if (zentroiden[c] == null)
                    zentroiden[c] = (float[])vektoren[rng.Next(vektoren.Count)].Clone();
            }

            return zentroiden;
        }

        private int NaechsterZentroid(float[] vektor, float[][] zentroiden)
        {
            int naechster = 0;
            float minDist = ZustandsEncoder.Distanz(vektor, zentroiden[0]);
            for (int i = 1; i < zentroiden.Length; i++)
            {
                float d = ZustandsEncoder.Distanz(vektor, zentroiden[i]);
                if (d < minDist) { minDist = d; naechster = i; }
            }
            return naechster;
        }

        private float[][] BerechneZentroiden(List<float[]> vektoren, int[] zuordnung, int k, int dim)
        {
            float[][] zentroiden = new float[k][];
            int[] zaehler = new int[k];

            for (int c = 0; c < k; c++)
                zentroiden[c] = new float[dim];

            for (int i = 0; i < vektoren.Count; i++)
            {
                int cluster = zuordnung[i];
                zaehler[cluster]++;
                for (int d = 0; d < dim; d++)
                    zentroiden[cluster][d] += vektoren[i][d];
            }

            for (int c = 0; c < k; c++)
            {
                if (zaehler[c] > 0)
                    for (int d = 0; d < dim; d++)
                        zentroiden[c][d] /= zaehler[c];
            }

            return zentroiden;
        }

        private float BerechneInertia(List<float[]> vektoren, int[] zuordnung, int k)
        {
            int dim = vektoren[0].Length;
            float[][] zentroiden = BerechneZentroiden(vektoren, zuordnung, k, dim);

            float inertia = 0f;
            for (int i = 0; i < vektoren.Count; i++)
            {
                float d = ZustandsEncoder.Distanz(vektoren[i], zentroiden[zuordnung[i]]);
                inertia += d * d;
            }

            return inertia;
        }
    }
}
