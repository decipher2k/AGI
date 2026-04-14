using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BilligAGI.Kern
{
    // =====================================================================
    // FineTuningManager: Steuert Fine-Tuning-Pipeline fuer lokale Modelle
    //
    // Unterstuetzte Backends:
    //   - LM Studio (POST /v1/fine-tuning/jobs)
    //   - Unsloth / axolotl via REST-Bridge
    //   - Jedes Backend das OpenAI-kompatible Fine-Tuning-API bietet
    //
    // Flow:
    //   1. Training-Daten hochladen (JSONL)
    //   2. Fine-Tuning-Job starten
    //   3. Status pollen
    //   4. Bei Erfolg: Modell-ID zurueckgeben → LLMAdapter wechselt
    //
    // Tracking: Versionierung aller trainierten Modelle mit Metriken.
    // =====================================================================

    public enum FineTuningStatus
    {
        Bereit,
        DatenExportiert,
        TrainingLaeuft,
        TrainingFertig,
        TrainingFehlgeschlagen,
        Evaluierung,
        ModellAktiv
    }

    [Serializable]
    public class ModellVersion
    {
        public string id;
        public string basisModell;
        public string fineTunedModellId;
        public int trainingSamples;
        public int dpoSamples;
        public float vorherBelohnung;      // ∅ Belohnung vor Wechsel
        public float nachherBelohnung;     // ∅ Belohnung nach Wechsel (evaluiert)
        public float verbesserung;         // nachher - vorher
        public string trainingsDatenPfad;
        public string zeitstempel;
        public FineTuningStatus status;
        public int generation;             // 0 = Basis, 1 = 1. Fine-Tune, ...
    }

    [Serializable]
    public class ModellHistorie
    {
        public List<ModellVersion> versionen = new List<ModellVersion>();
        public int aktuelleGeneration;
        public string aktuellesModell;
    }

    public class FineTuningManager
    {
        private readonly AGIConfig config;
        private readonly HttpClient httpClient;
        private readonly string persistenzPfad;
        private ModellHistorie historie;
        private FineTuningStatus status;
        private string aktuellerJobId;

        public FineTuningStatus Status => status;
        public int AktuelleGeneration => historie.aktuelleGeneration;
        public string AktuellesModell => historie.aktuellesModell;
        public List<ModellVersion> AlleVersionen => historie.versionen;

        public FineTuningManager(AGIConfig config)
        {
            this.config = config;
            httpClient = new HttpClient();
            persistenzPfad = Path.Combine(Application.persistentDataPath, "modell_historie.json");

            LadeHistorie();
            status = FineTuningStatus.Bereit;
        }

        // ========== Fine-Tuning starten ==========

        /// <summary>
        /// Startet einen Fine-Tuning-Job auf dem konfigurierten Backend.
        /// Gibt die neue ModellVersion zurueck (Status: TrainingLaeuft).
        /// </summary>
        public async Task<ModellVersion> StarteFineTuning(
            string trainingsDatenPfad,
            int trainingSamples,
            float aktuellerBelohnungsDurchschnitt,
            string suffix = null)
        {
            if (status == FineTuningStatus.TrainingLaeuft)
            {
                Debug.LogWarning("[FineTuning] Training laeuft bereits!");
                return null;
            }

            var version = new ModellVersion
            {
                id = Guid.NewGuid().ToString("N").Substring(0, 8),
                basisModell = config.llmModel,
                trainingSamples = trainingSamples,
                vorherBelohnung = aktuellerBelohnungsDurchschnitt,
                trainingsDatenPfad = trainingsDatenPfad,
                zeitstempel = DateTime.UtcNow.ToString("o"),
                generation = historie.aktuelleGeneration + 1,
                status = FineTuningStatus.TrainingLaeuft
            };

            // Fine-Tuning-Job via API starten
            var jobId = await StarteFineTuningJob(trainingsDatenPfad, suffix ?? $"agi-gen{version.generation}");

            if (string.IsNullOrEmpty(jobId))
            {
                version.status = FineTuningStatus.TrainingFehlgeschlagen;
                Debug.LogError("[FineTuning] Job konnte nicht gestartet werden.");
                return version;
            }

            aktuellerJobId = jobId;
            version.fineTunedModellId = jobId; // Wird nach Abschluss durch echte Modell-ID ersetzt
            status = FineTuningStatus.TrainingLaeuft;

            historie.versionen.Add(version);
            SpeichereHistorie();

            Debug.Log($"[FineTuning] Job gestartet: {jobId} (Gen {version.generation}, {trainingSamples} Samples)");
            return version;
        }

        /// <summary>
        /// Prüft den Status des laufenden Fine-Tuning-Jobs.
        /// Gibt true zurueck wenn fertig (erfolgreich oder fehlgeschlagen).
        /// </summary>
        public async Task<bool> PruefeJobStatus()
        {
            if (status != FineTuningStatus.TrainingLaeuft || string.IsNullOrEmpty(aktuellerJobId))
                return true; // Kein Job laufend

            try
            {
                string url = BaueApiUrl($"/v1/fine-tuning/jobs/{aktuellerJobId}");
                var response = await httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    Debug.LogWarning($"[FineTuning] Status-Abfrage fehlgeschlagen: {response.StatusCode}");
                    return false;
                }

                var json = JObject.Parse(await response.Content.ReadAsStringAsync());
                string jobStatus = json["status"]?.ToString() ?? "";

                switch (jobStatus)
                {
                    case "succeeded":
                        string neuesModell = json["fine_tuned_model"]?.ToString()
                            ?? json["model"]?.ToString()
                            ?? $"ft:{config.llmModel}:gen{historie.aktuelleGeneration + 1}";

                        AktualisiereVersion(aktuellerJobId, neuesModell, FineTuningStatus.TrainingFertig);
                        status = FineTuningStatus.TrainingFertig;

                        Debug.Log($"[FineTuning] Training erfolgreich! Neues Modell: {neuesModell}");
                        return true;

                    case "failed":
                    case "cancelled":
                        AktualisiereVersion(aktuellerJobId, null, FineTuningStatus.TrainingFehlgeschlagen);
                        status = FineTuningStatus.TrainingFehlgeschlagen;
                        Debug.LogError($"[FineTuning] Training fehlgeschlagen: {jobStatus}");
                        return true;

                    default:
                        // running, queued, validating_files, etc.
                        Debug.Log($"[FineTuning] Job-Status: {jobStatus}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FineTuning] Status-Pruefung fehlgeschlagen: {ex.Message}");
                return false;
            }
        }

        // ========== Modell aktivieren ==========

        /// <summary>
        /// Wechselt das aktive Modell auf die neueste Fine-Tuned-Version.
        /// Gibt den neuen Modellnamen zurueck.
        /// </summary>
        public string AktiviereNeuestesModell()
        {
            var neuesteVersion = historie.versionen
                .Where(v => v.status == FineTuningStatus.TrainingFertig)
                .OrderByDescending(v => v.generation)
                .FirstOrDefault();

            if (neuesteVersion == null)
            {
                Debug.LogWarning("[FineTuning] Kein fertig trainiertes Modell vorhanden.");
                return null;
            }

            neuesteVersion.status = FineTuningStatus.ModellAktiv;
            historie.aktuellesModell = neuesteVersion.fineTunedModellId;
            historie.aktuelleGeneration = neuesteVersion.generation;
            status = FineTuningStatus.ModellAktiv;

            SpeichereHistorie();

            Debug.Log($"[FineTuning] Modell aktiviert: {neuesteVersion.fineTunedModellId} (Gen {neuesteVersion.generation})");
            return neuesteVersion.fineTunedModellId;
        }

        /// <summary>
        /// Rollback zur vorherigen Modell-Version (falls neue schlechter performt).
        /// </summary>
        public string RollbackModell()
        {
            if (historie.versionen.Count < 2)
            {
                // Zurueck zum Basismodell
                historie.aktuellesModell = config.llmModel;
                historie.aktuelleGeneration = 0;
                SpeichereHistorie();
                return config.llmModel;
            }

            var vorherigeVersion = historie.versionen
                .Where(v => v.status == FineTuningStatus.ModellAktiv && v.generation < historie.aktuelleGeneration)
                .OrderByDescending(v => v.generation)
                .FirstOrDefault();

            if (vorherigeVersion != null)
            {
                historie.aktuellesModell = vorherigeVersion.fineTunedModellId;
                historie.aktuelleGeneration = vorherigeVersion.generation;
            }
            else
            {
                historie.aktuellesModell = config.llmModel;
                historie.aktuelleGeneration = 0;
            }

            SpeichereHistorie();
            Debug.Log($"[FineTuning] Rollback zu Gen {historie.aktuelleGeneration}: {historie.aktuellesModell}");
            return historie.aktuellesModell;
        }

        // ========== Evaluation registrieren ==========

        /// <summary>
        /// Registriert die Belohnung nach Modellwechsel fuer Vergleich.
        /// </summary>
        public void RegistriereEvaluierung(int generation, float belohnungNachher)
        {
            var version = historie.versionen.FirstOrDefault(v => v.generation == generation);
            if (version != null)
            {
                version.nachherBelohnung = belohnungNachher;
                version.verbesserung = belohnungNachher - version.vorherBelohnung;
                SpeichereHistorie();

                Debug.Log($"[FineTuning] Gen {generation} Evaluierung: " +
                    $"{version.vorherBelohnung:F3} → {belohnungNachher:F3} " +
                    $"({(version.verbesserung >= 0 ? "+" : "")}{version.verbesserung:F3})");
            }
        }

        // ========== Interne API-Calls ==========

        private async Task<string> StarteFineTuningJob(string trainingsDatenPfad, string suffix)
        {
            try
            {
                // Variante 1: OpenAI-kompatible Fine-Tuning-API
                string url = BaueApiUrl("/v1/fine-tuning/jobs");

                // Zuerst Datei hochladen
                string fileId = await LadeDateiHoch(trainingsDatenPfad);

                if (string.IsNullOrEmpty(fileId))
                {
                    // Variante 2: Direkter Pfad (fuer lokale Backends wie unsloth)
                    fileId = trainingsDatenPfad;
                }

                var jobBody = new JObject
                {
                    ["training_file"] = fileId,
                    ["model"] = config.llmModel,
                    ["suffix"] = suffix,
                    ["hyperparameters"] = new JObject
                    {
                        ["n_epochs"] = config.fineTuningEpochen,
                        ["learning_rate_multiplier"] = config.fineTuningLernrate
                    }
                };

                var content = new StringContent(jobBody.ToString(), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    string fehler = await response.Content.ReadAsStringAsync();
                    Debug.LogWarning($"[FineTuning] Job-Start fehlgeschlagen: {fehler}");
                    return null;
                }

                var responseJson = JObject.Parse(await response.Content.ReadAsStringAsync());
                return responseJson["id"]?.ToString();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FineTuning] Job-Start Exception: {ex.Message}");
                return null;
            }
        }

        private async Task<string> LadeDateiHoch(string pfad)
        {
            try
            {
                string url = BaueApiUrl("/v1/files");

                using var form = new MultipartFormDataContent();
                form.Add(new StringContent("fine-tune"), "purpose");
                form.Add(new ByteArrayContent(File.ReadAllBytes(pfad)), "file", Path.GetFileName(pfad));

                var response = await httpClient.PostAsync(url, form);
                if (!response.IsSuccessStatusCode) return null;

                var json = JObject.Parse(await response.Content.ReadAsStringAsync());
                return json["id"]?.ToString();
            }
            catch
            {
                return null; // Lokales Backend braucht keinen Upload
            }
        }

        private string BaueApiUrl(string endpunkt)
        {
            // Basis-URL ableiten: aus /v1/chat/completions → Basis
            string basis = config.fineTuningApiUrl;
            if (string.IsNullOrEmpty(basis))
            {
                basis = config.llmApiUrl;
                int v1Index = basis.IndexOf("/v1/", StringComparison.Ordinal);
                if (v1Index >= 0)
                    basis = basis.Substring(0, v1Index);
            }
            return basis.TrimEnd('/') + endpunkt;
        }

        // ========== Persistenz ==========

        private void LadeHistorie()
        {
            try
            {
                if (File.Exists(persistenzPfad))
                {
                    string json = File.ReadAllText(persistenzPfad);
                    historie = JsonConvert.DeserializeObject<ModellHistorie>(json) ?? new ModellHistorie();
                }
                else
                {
                    historie = new ModellHistorie
                    {
                        aktuellesModell = config.llmModel,
                        aktuelleGeneration = 0
                    };
                }
            }
            catch
            {
                historie = new ModellHistorie
                {
                    aktuellesModell = config.llmModel,
                    aktuelleGeneration = 0
                };
            }
        }

        private void SpeichereHistorie()
        {
            try
            {
                string json = JsonConvert.SerializeObject(historie, Formatting.Indented);
                File.WriteAllText(persistenzPfad, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FineTuning] Persistenz fehlgeschlagen: {ex.Message}");
            }
        }

        private void AktualisiereVersion(string jobId, string modellId, FineTuningStatus neuerStatus)
        {
            var version = historie.versionen.LastOrDefault(v => v.fineTunedModellId == jobId);
            if (version != null)
            {
                if (!string.IsNullOrEmpty(modellId))
                    version.fineTunedModellId = modellId;
                version.status = neuerStatus;
                SpeichereHistorie();
            }
        }

        // ========== Info ==========

        public string GetZusammenfassung()
        {
            return $"Gen {historie.aktuelleGeneration} | Modell: {historie.aktuellesModell} | " +
                   $"Status: {status} | Versionen: {historie.versionen.Count}";
        }
    }
}
