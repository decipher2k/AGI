using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using BilligAGI.Modelle;

namespace BilligAGI.Kern
{
    // =====================================================================
    // ErfahrungsExporter: Konvertiert AGI-Erfahrungen in Training-Daten
    //
    // Exportiert in 3 Formaten:
    //   1. JSONL (OpenAI Chat-Format) — fuer Supervised Fine-Tuning (SFT)
    //   2. DPO-Paare (chosen/rejected) — fuer Direct Preference Optimization
    //   3. Reward-Dataset — fuer Reward Model Training
    //
    // Nutzt Belohnungssignal + EmotionalerZustand als Qualitaetsfilter.
    // =====================================================================

    [Serializable]
    public class TrainingsSample
    {
        public string systemPrompt;
        public string userInput;
        public string assistantOutput;
        public float belohnung;
        public float relevanz;
        public string zeitstempel;
        public string kontext;
    }

    [Serializable]
    public class DPOPaar
    {
        public string systemPrompt;
        public string userInput;
        public string chosenOutput;    // Hohe Belohnung
        public string rejectedOutput;  // Niedrige Belohnung
        public float chosenBelohnung;
        public float rejectedBelohnung;
    }

    [Serializable]
    public class ExportStatistik
    {
        public int gesamtErfahrungen;
        public int exportiert;
        public int gefiltert;
        public float durchschnittsBelohnung;
        public int dpoPaare;
        public string exportPfad;
        public string zeitstempel;
    }

    public class ErfahrungsExporter
    {
        private readonly string exportVerzeichnis;

        public ErfahrungsExporter(string basisVerzeichnis = null)
        {
            exportVerzeichnis = basisVerzeichnis ??
                Path.Combine(Application.persistentDataPath, "training_data");

            if (!Directory.Exists(exportVerzeichnis))
                Directory.CreateDirectory(exportVerzeichnis);
        }

        // ========== SFT-Export (Supervised Fine-Tuning) ==========

        /// <summary>
        /// Exportiert die besten Erfahrungen als JSONL im OpenAI-Chat-Format.
        /// </summary>
        public ExportStatistik ExportiereAlsSFT(
            List<Erfahrung> erfahrungen,
            float minBelohnung = 0.3f,
            int maxSamples = 5000,
            string systemBasis = null)
        {
            var stats = new ExportStatistik
            {
                gesamtErfahrungen = erfahrungen.Count,
                zeitstempel = DateTime.UtcNow.ToString("o")
            };

            // Filtern: nur positive Erfahrungen mit genuegend Inhalt
            var gefiltert = erfahrungen
                .Where(e => e.belohnung >= minBelohnung &&
                            !string.IsNullOrEmpty(e.aktion) &&
                            !string.IsNullOrEmpty(e.ergebnis) &&
                            e.ergebnis.Length > 10)
                .OrderByDescending(e => e.belohnung * e.relevanz)
                .Take(maxSamples)
                .ToList();

            stats.gefiltert = erfahrungen.Count - gefiltert.Count;
            stats.exportiert = gefiltert.Count;
            stats.durchschnittsBelohnung = gefiltert.Count > 0
                ? gefiltert.Average(e => e.belohnung) : 0f;

            string dateiName = $"sft_{DateTime.UtcNow:yyyyMMdd_HHmmss}.jsonl";
            string pfad = Path.Combine(exportVerzeichnis, dateiName);
            stats.exportPfad = pfad;

            using (var writer = new StreamWriter(pfad))
            {
                foreach (var erf in gefiltert)
                {
                    var sample = BaueSFTSample(erf, systemBasis);
                    writer.WriteLine(sample);
                }
            }

            Debug.Log($"[ErfahrungsExporter] SFT-Export: {stats.exportiert}/{stats.gesamtErfahrungen} " +
                      $"Erfahrungen → {pfad} (∅ Belohnung: {stats.durchschnittsBelohnung:F2})");

            return stats;
        }

        /// <summary>
        /// Baut ein einzelnes JSONL-Sample im OpenAI-Chat-Format.
        /// </summary>
        private string BaueSFTSample(Erfahrung erf, string systemBasis)
        {
            string systemPrompt = BaueSystemPrompt(erf, systemBasis);

            var messages = new JArray();
            messages.Add(new JObject { ["role"] = "system", ["content"] = systemPrompt });
            messages.Add(new JObject { ["role"] = "user", ["content"] = erf.aktion });
            messages.Add(new JObject { ["role"] = "assistant", ["content"] = erf.ergebnis });

            var sample = new JObject { ["messages"] = messages };
            return sample.ToString(Formatting.None);
        }

        private string BaueSystemPrompt(Erfahrung erf, string systemBasis)
        {
            var sb = new System.Text.StringBuilder();

            if (!string.IsNullOrEmpty(systemBasis))
                sb.AppendLine(systemBasis);
            else
                sb.AppendLine("Du bist ein verkörperter AGI-Agent in einer 3D-Welt. " +
                    "Antworte praezise, handle zielgerichtet, lerne aus Erfahrungen.");

            if (!string.IsNullOrEmpty(erf.kontext))
                sb.AppendLine($"[Kontext] {erf.kontext}");

            if (erf.emotionalerZustand != null)
            {
                sb.Append("[Emotionen] ");
                if (erf.emotionalerZustand.neugier > 0.5f) sb.Append("neugierig ");
                if (erf.emotionalerZustand.frustration > 0.5f) sb.Append("frustriert ");
                if (erf.emotionalerZustand.zufriedenheit > 0.5f) sb.Append("zufrieden ");
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        // ========== DPO-Export (Direct Preference Optimization) ==========

        /// <summary>
        /// Erzeugt DPO-Paare: chosen (hohe Belohnung) vs. rejected (niedrige).
        /// Paart Erfahrungen mit aehnlichem Input aber unterschiedlicher Belohnung.
        /// </summary>
        public ExportStatistik ExportiereAlsDPO(
            List<Erfahrung> erfahrungen,
            float gutSchwelle = 0.5f,
            float schlechtSchwelle = 0.0f,
            int maxPaare = 2000)
        {
            var stats = new ExportStatistik
            {
                gesamtErfahrungen = erfahrungen.Count,
                zeitstempel = DateTime.UtcNow.ToString("o")
            };

            // In gut/schlecht aufteilen
            var gute = erfahrungen
                .Where(e => e.belohnung >= gutSchwelle && !string.IsNullOrEmpty(e.ergebnis))
                .ToList();
            var schlechte = erfahrungen
                .Where(e => e.belohnung <= schlechtSchwelle && !string.IsNullOrEmpty(e.ergebnis))
                .ToList();

            var paare = new List<DPOPaar>();

            // Paare bilden: aehnlicher Kontext, unterschiedliche Qualitaet
            foreach (var gut in gute)
            {
                if (paare.Count >= maxPaare) break;

                // Finde schlechte Erfahrung mit aehnlichem Kontext
                var partner = FindeAehnlichsten(gut, schlechte);
                if (partner != null)
                {
                    paare.Add(new DPOPaar
                    {
                        systemPrompt = BaueSystemPrompt(gut, null),
                        userInput = gut.aktion,
                        chosenOutput = gut.ergebnis,
                        rejectedOutput = partner.ergebnis,
                        chosenBelohnung = gut.belohnung,
                        rejectedBelohnung = partner.belohnung
                    });
                }
            }

            stats.exportiert = paare.Count;
            stats.dpoPaare = paare.Count;
            stats.gefiltert = erfahrungen.Count - (gute.Count + schlechte.Count);

            string dateiName = $"dpo_{DateTime.UtcNow:yyyyMMdd_HHmmss}.jsonl";
            string pfad = Path.Combine(exportVerzeichnis, dateiName);
            stats.exportPfad = pfad;

            using (var writer = new StreamWriter(pfad))
            {
                foreach (var paar in paare)
                {
                    var json = new JObject
                    {
                        ["prompt"] = new JArray
                        {
                            new JObject { ["role"] = "system", ["content"] = paar.systemPrompt },
                            new JObject { ["role"] = "user", ["content"] = paar.userInput }
                        },
                        ["chosen"] = new JArray
                        {
                            new JObject { ["role"] = "assistant", ["content"] = paar.chosenOutput }
                        },
                        ["rejected"] = new JArray
                        {
                            new JObject { ["role"] = "assistant", ["content"] = paar.rejectedOutput }
                        }
                    };
                    writer.WriteLine(json.ToString(Formatting.None));
                }
            }

            Debug.Log($"[ErfahrungsExporter] DPO-Export: {paare.Count} Paare → {pfad}");
            return stats;
        }

        // ========== Reward-Dataset ==========

        /// <summary>
        /// Exportiert Belohnungs-Daten fuer Reward-Model-Training.
        /// </summary>
        public ExportStatistik ExportiereAlsReward(List<Erfahrung> erfahrungen, int maxSamples = 5000)
        {
            var stats = new ExportStatistik
            {
                gesamtErfahrungen = erfahrungen.Count,
                zeitstempel = DateTime.UtcNow.ToString("o")
            };

            var valide = erfahrungen
                .Where(e => !string.IsNullOrEmpty(e.aktion) && !string.IsNullOrEmpty(e.ergebnis))
                .OrderByDescending(e => Math.Abs(e.belohnung)) // Extreme zuerst
                .Take(maxSamples)
                .ToList();

            stats.exportiert = valide.Count;
            stats.durchschnittsBelohnung = valide.Count > 0 ? valide.Average(e => e.belohnung) : 0f;

            string dateiName = $"reward_{DateTime.UtcNow:yyyyMMdd_HHmmss}.jsonl";
            string pfad = Path.Combine(exportVerzeichnis, dateiName);
            stats.exportPfad = pfad;

            using (var writer = new StreamWriter(pfad))
            {
                foreach (var erf in valide)
                {
                    var json = new JObject
                    {
                        ["messages"] = new JArray
                        {
                            new JObject { ["role"] = "system", ["content"] = BaueSystemPrompt(erf, null) },
                            new JObject { ["role"] = "user", ["content"] = erf.aktion },
                            new JObject { ["role"] = "assistant", ["content"] = erf.ergebnis }
                        },
                        ["reward"] = erf.belohnung,
                        ["metadata"] = new JObject
                        {
                            ["kontext"] = erf.kontext ?? "",
                            ["zielId"] = erf.zielId ?? "",
                            ["zeitstempel"] = erf.zeitstempel
                        }
                    };
                    writer.WriteLine(json.ToString(Formatting.None));
                }
            }

            Debug.Log($"[ErfahrungsExporter] Reward-Export: {stats.exportiert} Samples → {pfad}");
            return stats;
        }

        // ========== Helfer ==========

        private Erfahrung FindeAehnlichsten(Erfahrung referenz, List<Erfahrung> kandidaten)
        {
            if (kandidaten.Count == 0) return null;

            // Einfache Aehnlichkeit: gleicher Kontext oder aehnliche Aktion
            return kandidaten
                .Where(k => k.kontext == referenz.kontext ||
                           AktionAehnlich(referenz.aktion, k.aktion))
                .OrderBy(k => k.belohnung) // Schlechteste zuerst
                .FirstOrDefault()
                ?? kandidaten.OrderBy(k => k.belohnung).First(); // Fallback
        }

        private bool AktionAehnlich(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            // Einfacher Wort-Overlap
            var worteA = a.ToLowerInvariant().Split(' ');
            var worteB = b.ToLowerInvariant().Split(' ');
            int gemeinsam = worteA.Intersect(worteB).Count();
            float overlap = gemeinsam / (float)Math.Max(worteA.Length, worteB.Length);
            return overlap > 0.3f;
        }

        public string GetExportVerzeichnis() => exportVerzeichnis;

        public List<string> GetVorhandeneExporte()
        {
            if (!Directory.Exists(exportVerzeichnis)) return new List<string>();
            return Directory.GetFiles(exportVerzeichnis, "*.jsonl")
                .Select(Path.GetFileName).ToList();
        }
    }
}
