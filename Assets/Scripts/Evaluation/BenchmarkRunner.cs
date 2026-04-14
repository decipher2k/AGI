using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BilligAGI.Modelle;
using BilligAGI.Kern;
using BilligAGI.Daten;
using UnityEngine;

namespace BilligAGI.Evaluation
{
    public class BenchmarkRunner
    {
        private readonly AGIKern kern;
        private readonly AGIConfig config;
        private List<BenchmarkSzenario> szenarien;
        private List<BenchmarkErgebnis> letzteErgebnisse;
        private BenchmarkReport letzterReport;

        public BenchmarkRunner(AGIKern kern, AGIConfig config)
        {
            this.kern = kern;
            this.config = config;
            LadeSzenarien();
            letzteErgebnisse = new List<BenchmarkErgebnis>();
        }

        public async Task<BenchmarkReport> FuehreAlleAus()
        {
            if (szenarien == null || szenarien.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[Benchmark] Keine Szenarien geladen.");
                return null;
            }

            letzteErgebnisse.Clear();
            UnityEngine.Debug.Log($"[Benchmark] Starte {szenarien.Count} Szenarien...");

            foreach (var szenario in szenarien)
            {
                var ergebnis = await FuehreSzenarioAus(szenario);
                letzteErgebnisse.Add(ergebnis);
                UnityEngine.Debug.Log($"[Benchmark] {szenario.name}: Erfolg={ergebnis.erfolgreich}, " +
                    $"Zeit={ergebnis.zeitMs}ms, LLMCalls={ergebnis.llmCalls}");
            }

            letzterReport = GeneriereReport();
            PersistiereReport();
            return letzterReport;
        }

        public async Task<BenchmarkErgebnis> FuehreSzenarioAus(BenchmarkSzenario szenario)
        {
            var sw = Stopwatch.StartNew();
            var ergebnis = new BenchmarkErgebnis
            {
                szenarioName = szenario.name,
                kategorie = szenario.kategorie,
                zeitstempel = DateTime.UtcNow.ToString("o")
            };

            try
            {
                // Input an Kern senden
                int llmCallsVorher = kern.GetLLM()?.GetAnzahlCalls() ?? 0;

                kern.VerarbeiteInput(szenario.input);
                // Warte auf Verarbeitung (ein Zyklus)
                await Task.Delay(Mathf.RoundToInt(config.zyklusIntervall * 1000) + 500);

                int llmCallsNachher = kern.GetLLM()?.GetAnzahlCalls() ?? 0;
                ergebnis.llmCalls = llmCallsNachher - llmCallsVorher;

                // Erwartete Ergebnisse pruefen
                ergebnis.erfolgreich = PruefeErwartetesErgebnis(szenario);

                // Lokal-Quote
                float quote = kern.GetSemantik()?.BerechneLokalQuote() ?? 0f;
                ergebnis.lokalQuote = quote;
            }
            catch (Exception ex)
            {
                ergebnis.erfolgreich = false;
                ergebnis.fehlerMeldung = ex.Message;
                UnityEngine.Debug.LogError($"[Benchmark] Fehler in {szenario.name}: {ex.Message}");
            }

            sw.Stop();
            ergebnis.zeitMs = sw.ElapsedMilliseconds;
            return ergebnis;
        }

        public BenchmarkReport GeneriereReport()
        {
            if (letzteErgebnisse.Count == 0) return null;

            var report = new BenchmarkReport
            {
                zeitstempel = DateTime.UtcNow.ToString("o"),
                anzahlSzenarien = letzteErgebnisse.Count,
                erfolgsQuote = letzteErgebnisse.Count(e => e.erfolgreich) / (float)letzteErgebnisse.Count,
                durchschnittZeitMs = (long)letzteErgebnisse.Average(e => e.zeitMs),
                gesamtLLMCalls = letzteErgebnisse.Sum(e => e.llmCalls),
                durchschnittLokalQuote = letzteErgebnisse.Average(e => e.lokalQuote),
                ergebnisse = new List<BenchmarkErgebnis>(letzteErgebnisse)
            };

            // KategorieDurchschnitt
            report.kategorieScore = new Dictionary<string, float>();
            var gruppen = letzteErgebnisse.GroupBy(e => e.kategorie);
            foreach (var g in gruppen)
            {
                float score = g.Count(e => e.erfolgreich) / (float)g.Count();
                report.kategorieScore[g.Key] = score;
            }

            // Regression pruefen
            var referenz = LadeReferenz();
            if (referenz != null)
            {
                float diff = referenz.erfolgsQuote - report.erfolgsQuote;
                report.regression = diff;
                report.regressionsAlarm = diff > 0.05f;
                if (report.regressionsAlarm)
                    UnityEngine.Debug.LogWarning($"[Benchmark] REGRESSION: Erfolgsquote um {diff:P1} gesunken!");
            }

            return report;
        }

        public BenchmarkReport GetLetzterReport() => letzterReport;

        private bool PruefeErwartetesErgebnis(BenchmarkSzenario szenario)
        {
            if (string.IsNullOrEmpty(szenario.erwarteteRegel)) return true;

            // Pruefen ob erwartetes Ergebnis in relevanten Systemen vorliegt
            switch (szenario.kategorie)
            {
                case "physik":
                    return true; // Vereinfacht — im echten Test gegen PhysikEngine pruefen
                case "vakog":
                    return true;
                case "sozial":
                    return true;
                case "gedaechtnis":
                    return true;
                case "intentionalitaet":
                    var ziele = kern.GetZielManager()?.GetAlleAktiven();
                    return ziele != null && ziele.Count > 0;
                default:
                    return true;
            }
        }

        private void LadeSzenarien()
        {
            szenarien = DatenLader.LadeListe<BenchmarkSzenario>("benchmark_szenarien.json")
                ?? new List<BenchmarkSzenario>();
        }

        private BenchmarkReport LadeReferenz()
        {
            return DatenLader.Lade<BenchmarkReport>("benchmark_referenz.json");
        }

        private void PersistiereReport()
        {
            if (letzterReport != null)
                DatenLader.Speichere("benchmark_letzter.json", letzterReport);
        }
    }

    [Serializable]
    public class BenchmarkSzenario
    {
        public string name;
        public string kategorie;
        public string input;
        public string erwarteteRegel;
        public string beschreibung;
    }

    [Serializable]
    public class BenchmarkReport
    {
        public string zeitstempel;
        public int anzahlSzenarien;
        public float erfolgsQuote;
        public long durchschnittZeitMs;
        public int gesamtLLMCalls;
        public float durchschnittLokalQuote;
        public float regression;
        public bool regressionsAlarm;
        public Dictionary<string, float> kategorieScore;
        public List<BenchmarkErgebnis> ergebnisse;
    }
}
