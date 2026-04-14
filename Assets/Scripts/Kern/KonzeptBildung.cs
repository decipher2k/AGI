using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using BilligAGI.Modelle;
using BilligAGI.Gedaechtnis;

namespace BilligAGI.Kern
{
    // ============================================================
    //  KonzeptBildung — Spontane Kategorienbildung
    //
    //  Entdeckt unbenannte Muster in subsymbolischen Clustern
    //  und erfindet dafuer neue Konzepte (Kategorien).
    //
    //  Pipeline:
    //  1. SubsymbolikKernel.ErkenneVerdecktesMuster() → Trigger
    //  2. Cluster-Mitglieder sammeln → Erfahrungen laden
    //  3. LLM benennt + definiert die neue Kategorie
    //  4. KonzeptRevision registriert das Konzept
    //  5. Erfahrungen werden mit neuem Konzept getaggt
    //  6. Bei Widersprüchen → Konzept spalten/verschmelzen/verwerfen
    // ============================================================

    [Serializable]
    public class KonzeptBildungErgebnis
    {
        public bool neuesKonzeptEntdeckt;
        public string konzeptId;
        public string konzeptName;
        public string definition;
        public int stuetzendeErfahrungen;
        public string zusammenfassung;
    }

    public class KonzeptBildung
    {
        private readonly LLMAdapter llm;
        private readonly SubsymbolikKernel subsymbolik;
        private readonly ErfahrungsSpeicher erfahrungen;
        private readonly KonzeptRevision konzeptRevision;
        private readonly KausalGraph kausalGraph;
        private readonly AGIConfig config;

        private int zyklusSeitLetzterPruefung;
        private readonly HashSet<string> bereitsEntdeckteClusterKeys = new();

        private const int PRUEFUNGS_INTERVALL = 50; // Alle N Zyklen pruefen
        private const int MIN_CLUSTER_GROESSE = 3;

        private const string BENENNUNGS_SYSTEM = @"Du bist ein Konzept-Entdecker. Dir werden Erfahrungen gezeigt, die ein 
subsymbolisches Clustering-System als zusammengehoerend gruppiert hat — 
aber NOCH KEINEN NAMEN haben.

Deine Aufgabe:
1. Finde heraus, was diese Erfahrungen GEMEINSAM haben
2. Erfinde einen PRAEGNANTEN deutschen Namen fuer diese Kategorie
3. Schreibe eine klare Definition

Antworte als JSON:
{
  ""name"": ""KategorieName"",
  ""definition"": ""Klare Definition: Was gehoert zu dieser Kategorie und was nicht"",
  ""gemeinsameEigenschaft"": ""Was alle Mitglieder verbindet"",
  ""abgrenzung"": ""Wovon sich diese Kategorie unterscheidet"",
  ""kausalHypothese"": ""Welche Ursache-Wirkungs-Beziehung koennte dahinterstecken (oder null)""
}

Regeln:
- Der Name soll ABSTRAKT sein, nicht an einzelne Objekte gebunden
- Die Definition muss auf NEUE, noch ungesehene Faelle anwendbar sein
- Wenn die Erfahrungen kein echtes Muster haben, antworte: {""name"": null}";

        public KonzeptBildung(
            LLMAdapter llm,
            SubsymbolikKernel subsymbolik,
            ErfahrungsSpeicher erfahrungen,
            KonzeptRevision konzeptRevision,
            KausalGraph kausalGraph,
            AGIConfig config)
        {
            this.llm = llm;
            this.subsymbolik = subsymbolik;
            this.erfahrungen = erfahrungen;
            this.konzeptRevision = konzeptRevision;
            this.kausalGraph = kausalGraph;
            this.config = config;
        }

        // ===========================================================
        //  HAUPTMETHODE: Wird jeden Zyklus aufgerufen
        // ===========================================================

        /// <summary>
        /// Prueft periodisch ob unbenannte Muster im subsymbolischen Raum existieren
        /// und erzeugt daraus neue Konzepte.
        /// </summary>
        public async Task<KonzeptBildungErgebnis> ZyklusTick()
        {
            zyklusSeitLetzterPruefung++;
            if (zyklusSeitLetzterPruefung < PRUEFUNGS_INTERVALL)
                return null;

            zyklusSeitLetzterPruefung = 0;

            // 1. Gibt es unbenannte Cluster?
            bool verdecktesMuster = subsymbolik.ErkenneVerdecktesMuster();
            if (!verdecktesMuster) return null;

            // 2. Unbenannte Zustaende sammeln
            var unbenannte = SammelUnbenannteZustaende();
            if (unbenannte.Count < MIN_CLUSTER_GROESSE) return null;

            // 3. Clustern
            var cluster = ClustereUnbenannte(unbenannte);

            // 4. Pro Cluster: Konzept bilden
            foreach (var gruppe in cluster)
            {
                if (gruppe.Count < MIN_CLUSTER_GROESSE) continue;

                // Duplikat-Check: Haben wir diesen Cluster schon mal verarbeitet?
                string clusterKey = string.Join(",",
                    gruppe.Select(z => z.quellId).OrderBy(id => id));
                if (bereitsEntdeckteClusterKeys.Contains(clusterKey)) continue;

                var ergebnis = await BenenneCluster(gruppe);
                if (ergebnis != null && ergebnis.neuesKonzeptEntdeckt)
                {
                    bereitsEntdeckteClusterKeys.Add(clusterKey);
                    return ergebnis;
                }
            }

            return null;
        }

        // ===========================================================
        //  CORE: Cluster benennen + als Konzept registrieren
        // ===========================================================

        private async Task<KonzeptBildungErgebnis> BenenneCluster(List<LatenterZustand> clusterMitglieder)
        {
            // Quell-Erfahrungen laden
            var alleErfahrungen = erfahrungen.Alle();
            var quellIds = new HashSet<string>(clusterMitglieder.Select(z => z.quellId));
            var relevanteErfahrungen = alleErfahrungen
                .Where(e => quellIds.Contains(e.id))
                .ToList();

            if (relevanteErfahrungen.Count < MIN_CLUSTER_GROESSE)
                return null;

            // LLM-Prompt bauen
            var prompt = BauePrompt(relevanteErfahrungen);
            var antwort = await llm.FreieAnfrage(prompt, BENENNUNGS_SYSTEM);
            if (antwort == null || string.IsNullOrWhiteSpace(antwort.inhalt))
                return null;

            // Parsen
            string json = antwort.inhalt;
            string name = ExtractJsonString(json, "name");
            if (string.IsNullOrEmpty(name) || name == "null")
                return null; // LLM sagt: kein echtes Muster

            string definition = ExtractJsonString(json, "definition") ?? "";
            string gemeinsam = ExtractJsonString(json, "gemeinsameEigenschaft") ?? "";
            string kausalHyp = ExtractJsonString(json, "kausalHypothese");

            // Neues Konzept erstellen
            var konzept = new Konzept
            {
                name = name,
                typ = KonzeptTyp.Emergent,
                aktuelleDefinition = definition,
                ursprungsDefinition = definition,
                anzahlAnwendungen = relevanteErfahrungen.Count
            };

            // Bei KonzeptRevision registrieren (fuer spaetere Revision)
            konzeptRevision.RegistriereKonzept(konzept);

            // Erfahrungen mit neuem Konzept taggen
            foreach (var e in relevanteErfahrungen)
            {
                if (!e.konzepte.Contains(name))
                    e.konzepte.Add(name);
            }

            // Subsymbolische Zustaende labeln
            foreach (var z in clusterMitglieder)
                subsymbolik.SetzeLabel(z.quellId, name);

            // Kausale Hypothese registrieren (falls vorhanden)
            if (!string.IsNullOrEmpty(kausalHyp) && kausalHyp != "null")
            {
                kausalGraph.FuegeKausalitaetHinzu(
                    gemeinsam, name, 0.3f, "mechanismus");
            }

            Debug.Log($"[KonzeptBildung] Neues Konzept entdeckt: '{name}' — {definition}");
            Debug.Log($"[KonzeptBildung] Basiert auf {relevanteErfahrungen.Count} Erfahrungen, als Emergent registriert.");

            return new KonzeptBildungErgebnis
            {
                neuesKonzeptEntdeckt = true,
                konzeptId = konzept.id,
                konzeptName = name,
                definition = definition,
                stuetzendeErfahrungen = relevanteErfahrungen.Count,
                zusammenfassung = $"Neues Konzept '{name}': {definition} (basiert auf {relevanteErfahrungen.Count} Erfahrungen)"
            };
        }

        // ===========================================================
        //  Manueller Trigger (fuer Chat-Befehl)
        // ===========================================================

        /// <summary>
        /// Erzwingt sofortige Konzeptbildung (ignoriert Intervall).
        /// </summary>
        public async Task<KonzeptBildungErgebnis> ErzwingeKonzeptbildung()
        {
            zyklusSeitLetzterPruefung = PRUEFUNGS_INTERVALL;
            return await ZyklusTick();
        }

        // ===========================================================
        //  STATUS
        // ===========================================================

        public string GetStatusText()
        {
            return $"Entdeckte emergente Konzepte: {bereitsEntdeckteClusterKeys.Count}\n" +
                   $"Naechste Pruefung in: {PRUEFUNGS_INTERVALL - zyklusSeitLetzterPruefung} Zyklen";
        }

        // ===========================================================
        //  PRIVATE: Hilfsmethoden
        // ===========================================================

        private List<LatenterZustand> SammelUnbenannteZustaende()
        {
            // Alle latenten Zustaende ohne Label durchsuchen
            // SubsymbolikKernel.Aehnlichste() gibt uns Zugang via leere Abfrage
            // Wir brauchen einen Trick: mit Null-Vektor alle holen
            var nullVektor = new float[64];
            var alle = subsymbolik.Aehnlichste(nullVektor, 1000);
            return alle.Where(z => string.IsNullOrEmpty(z.label)).ToList();
        }

        private List<List<LatenterZustand>> ClustereUnbenannte(List<LatenterZustand> zustaende)
        {
            if (zustaende.Count < MIN_CLUSTER_GROESSE)
                return new List<List<LatenterZustand>>();

            var vektoren = zustaende.Select(z => z.vektor).ToList();
            int maxK = Math.Min(8, zustaende.Count / MIN_CLUSTER_GROESSE);
            if (maxK < 2) maxK = 2;

            // Optimale Cluster-Anzahl finden (extern ueber InstanzClusterer nicht moeglich
            // da der nur ArchetypInstanz nimmt — wir machen einfaches K-Means hier)
            int bestK = FindeOptimalesK(vektoren, maxK);
            var assignments = KMeansLokal(vektoren, bestK);

            // Gruppieren
            var cluster = new Dictionary<int, List<LatenterZustand>>();
            for (int i = 0; i < assignments.Length; i++)
            {
                int k = assignments[i];
                if (!cluster.ContainsKey(k))
                    cluster[k] = new List<LatenterZustand>();
                cluster[k].Add(zustaende[i]);
            }

            return cluster.Values.ToList();
        }

        private string BauePrompt(List<Erfahrung> erfahrungen)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Diese {erfahrungen.Count} Erfahrungen wurden von einem subsymbolischen System als zusammengehoerend erkannt:");
            sb.AppendLine();

            foreach (var e in erfahrungen.Take(15))
            {
                sb.AppendLine($"- Aktion: {e.aktion}");
                sb.AppendLine($"  Kontext: {e.kontext}");
                sb.AppendLine($"  Ergebnis: {e.ergebnis}");
                sb.AppendLine($"  Belohnung: {e.belohnung:F2}");
                if (e.konzepte?.Count > 0)
                    sb.AppendLine($"  Existierende Tags: {string.Join(", ", e.konzepte)}");
                sb.AppendLine();
            }

            sb.AppendLine("Was haben diese Erfahrungen gemeinsam? Benenne die Kategorie.");
            return sb.ToString();
        }

        // --- Minimales K-Means (64D, ohne InstanzClusterer-Abhaengigkeit) ---

        private int FindeOptimalesK(List<float[]> vektoren, int maxK)
        {
            float letzteTraegheit = float.MaxValue;
            float groessteDrop = 0;
            int bestesK = 2;

            for (int k = 1; k <= maxK; k++)
            {
                var assignments = KMeansLokal(vektoren, k);
                float traegheit = BerechneTraegheit(vektoren, assignments, k);

                if (k > 1)
                {
                    float drop = letzteTraegheit - traegheit;
                    if (drop > groessteDrop)
                    {
                        groessteDrop = drop;
                        bestesK = k;
                    }
                }
                letzteTraegheit = traegheit;
            }
            return bestesK;
        }

        private int[] KMeansLokal(List<float[]> vektoren, int k, int maxIter = 30)
        {
            if (k <= 0) k = 1;
            if (k > vektoren.Count) k = vektoren.Count;

            var rng = new System.Random(42);
            int dim = vektoren[0].Length;

            // K-Means++ Init
            var zentren = new float[k][];
            zentren[0] = (float[])vektoren[rng.Next(vektoren.Count)].Clone();

            for (int c = 1; c < k; c++)
            {
                var dists = vektoren.Select(v =>
                {
                    float minD = float.MaxValue;
                    for (int j = 0; j < c; j++)
                        minD = Math.Min(minD, EuklidischeDist(v, zentren[j]));
                    return minD * minD;
                }).ToArray();

                float summe = dists.Sum();
                float r = (float)(rng.NextDouble() * summe);
                float cum = 0;
                for (int i = 0; i < dists.Length; i++)
                {
                    cum += dists[i];
                    if (cum >= r) { zentren[c] = (float[])vektoren[i].Clone(); break; }
                }
                zentren[c] ??= (float[])vektoren[rng.Next(vektoren.Count)].Clone();
            }

            var assignments = new int[vektoren.Count];

            for (int iter = 0; iter < maxIter; iter++)
            {
                bool changed = false;

                // Zuordnen
                for (int i = 0; i < vektoren.Count; i++)
                {
                    int best = 0;
                    float bestDist = float.MaxValue;
                    for (int c = 0; c < k; c++)
                    {
                        float d = EuklidischeDist(vektoren[i], zentren[c]);
                        if (d < bestDist) { bestDist = d; best = c; }
                    }
                    if (assignments[i] != best) { assignments[i] = best; changed = true; }
                }

                if (!changed) break;

                // Zentren aktualisieren
                for (int c = 0; c < k; c++)
                {
                    var center = new float[dim];
                    int count = 0;
                    for (int i = 0; i < vektoren.Count; i++)
                    {
                        if (assignments[i] == c)
                        {
                            for (int d = 0; d < dim; d++) center[d] += vektoren[i][d];
                            count++;
                        }
                    }
                    if (count > 0)
                        for (int d = 0; d < dim; d++) center[d] /= count;
                    zentren[c] = center;
                }
            }

            return assignments;
        }

        private float BerechneTraegheit(List<float[]> vektoren, int[] assignments, int k)
        {
            int dim = vektoren[0].Length;
            var zentren = new float[k][];
            var counts = new int[k];

            for (int c = 0; c < k; c++) zentren[c] = new float[dim];
            for (int i = 0; i < vektoren.Count; i++)
            {
                int c = assignments[i];
                for (int d = 0; d < dim; d++) zentren[c][d] += vektoren[i][d];
                counts[c]++;
            }
            for (int c = 0; c < k; c++)
                if (counts[c] > 0)
                    for (int d = 0; d < dim; d++) zentren[c][d] /= counts[c];

            float summe = 0;
            for (int i = 0; i < vektoren.Count; i++)
                summe += EuklidischeDist(vektoren[i], zentren[assignments[i]]);
            return summe;
        }

        private static float EuklidischeDist(float[] a, float[] b)
        {
            float sum = 0;
            for (int i = 0; i < a.Length && i < b.Length; i++)
            {
                float diff = a[i] - b[i];
                sum += diff * diff;
            }
            return (float)Math.Sqrt(sum);
        }

        // --- JSON-Helfer ---

        private static string ExtractJsonString(string json, string key)
        {
            string pattern = $"\"{key}\"";
            int idx = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            int colonIdx = json.IndexOf(':', idx + pattern.Length);
            if (colonIdx < 0) return null;
            int quoteStart = json.IndexOf('"', colonIdx + 1);
            if (quoteStart < 0) return null;
            int quoteEnd = json.IndexOf('"', quoteStart + 1);
            while (quoteEnd > 0 && json[quoteEnd - 1] == '\\')
                quoteEnd = json.IndexOf('"', quoteEnd + 1);
            if (quoteEnd < 0) return null;
            return json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1)
                .Replace("\\\"", "\"")
                .Replace("\\n", "\n");
        }
    }
}
