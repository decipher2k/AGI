using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using BilligAGI.Modelle;
using BilligAGI.Gedaechtnis;

namespace BilligAGI.Kern
{
    // =====================================================================
    // SelbstOptimierung: Meta-Loop fuer kontinuierliche Selbstverbesserung
    //
    // Orchestriert den kompletten Fine-Tuning-Kreislauf:
    //   1. Erfahrungen sammeln (via AutoTrainer / normaler Betrieb)
    //   2. Beste Erfahrungen exportieren (ErfahrungsExporter → SFT + DPO)
    //   3. Fine-Tuning-Job starten (FineTuningManager → lokales Backend)
    //   4. Auf Fertigstellung warten (periodisches Polling)
    //   5. Neues Modell aktivieren + LLM hot-swap (LLMAdapter.WechsleModell)
    //   6. Evaluierungsphase: N Zyklen mit neuem Modell messen
    //   7. Vergleich: besser → behalten, schlechter → Rollback
    //   8. Repeat
    //
    // Haengt als MonoBehaviour an ein GameObject.
    // Braucht Referenz auf AGIKern (fuer Zugriff auf alle Subsysteme).
    // =====================================================================

    public enum OptimierungsPhase
    {
        Warten,             // Erfahrungen sammeln
        Exportieren,        // Daten exportieren
        TrainingLaeuft,     // Fine-Tuning-Job aktiv
        ModellWechsel,      // Neues Modell wird aktiviert
        Evaluierung,        // Neues Modell wird getestet
        Entscheidung        // Vergleich + Commit/Rollback
    }

    [Serializable]
    public class OptimierungsStatistik
    {
        public int durchlaeufeGesamt;
        public int erfolgreicheUpdates;
        public int rollbacks;
        public float besteVerbesserung;
        public float kumulierteVerbesserung;
        public string letzterDurchlauf;
        public OptimierungsPhase aktuellePhase;
    }

    public class SelbstOptimierung : MonoBehaviour
    {
        [Header("Referenzen")]
        public AGIKern agiKern;

        [Header("Status")]
        [SerializeField] private OptimierungsPhase phase = OptimierungsPhase.Warten;
        [SerializeField] private int zyklenSeitLetztemExport;
        [SerializeField] private int evaluierungsZyklenGezaehlt;
        [SerializeField] private bool aktiv;

        // Subsystem-Referenzen (von AGIKern geholt)
        private ErfahrungsExporter exporter;
        private FineTuningManager fineTuning;
        private LLMAdapter llm;
        private ErfahrungsSpeicher erfahrungen;
        private AGIConfig config;

        // Evaluierungs-Tracking
        private List<float> evaluierungsBelohnungen = new List<float>();
        private float belohnungVorWechsel;
        private string altesModell;

        // Statistik
        private OptimierungsStatistik statistik = new OptimierungsStatistik();

        // Polling-Timer
        private float pollingTimer;
        private const float POLLING_INTERVALL = 30f; // Alle 30 Sek Job-Status pruefen

        public OptimierungsPhase Phase => phase;
        public OptimierungsStatistik Statistik => statistik;
        public bool Aktiv => aktiv;

        private void Start()
        {
            if (agiKern == null)
            {
                Debug.LogError("[SelbstOptimierung] Kein AGIKern zugewiesen!");
                enabled = false;
                return;
            }
        }

        /// <summary>
        /// Wird von AGIKern nach Initialisierung aufgerufen.
        /// </summary>
        public void Initialisiere(
            AGIConfig config,
            LLMAdapter llm,
            ErfahrungsSpeicher erfahrungen,
            ErfahrungsExporter exporter,
            FineTuningManager fineTuning)
        {
            this.config = config;
            this.llm = llm;
            this.erfahrungen = erfahrungen;
            this.exporter = exporter;
            this.fineTuning = fineTuning;

            aktiv = config.fineTuningAktiv;
            phase = OptimierungsPhase.Warten;

            Debug.Log($"[SelbstOptimierung] Initialisiert (aktiv: {aktiv})");
        }

        private void Update()
        {
            if (!aktiv || config == null || !config.fineTuningAktiv) return;

            switch (phase)
            {
                case OptimierungsPhase.Warten:
                    TickWarten();
                    break;

                case OptimierungsPhase.TrainingLaeuft:
                    TickTraining();
                    break;

                case OptimierungsPhase.Evaluierung:
                    TickEvaluierung();
                    break;
            }
        }

        // ========== Phase: Warten (Erfahrungen sammeln) ==========

        private void TickWarten()
        {
            zyklenSeitLetztemExport++;

            // Genuegend Erfahrungen + genuegend Zyklen seit letztem Export?
            int erfahrungsAnzahl = erfahrungen?.Anzahl() ?? 0;

            if (erfahrungsAnzahl >= config.minErfahrungenFuerFineTuning &&
                zyklenSeitLetztemExport >= config.fineTuningIntervallZyklen)
            {
                _ = StarteExportUndTraining();
            }
        }

        // ========== Phase: Export + Training starten ==========

        private async Task StarteExportUndTraining()
        {
            phase = OptimierungsPhase.Exportieren;
            statistik.aktuellePhase = phase;

            Debug.Log("[SelbstOptimierung] Starte Daten-Export fuer Fine-Tuning...");

            try
            {
                var alleErfahrungen = erfahrungen.Alle();

                // SFT-Export (beste Erfahrungen)
                var sftStats = exporter.ExportiereAlsSFT(alleErfahrungen, 0.3f, 5000);

                // DPO-Export (Praeferenz-Paare)
                var dpoStats = exporter.ExportiereAlsDPO(alleErfahrungen, 0.5f, 0.0f, 2000);

                Debug.Log($"[SelbstOptimierung] Export fertig: " +
                    $"SFT={sftStats.exportiert} Samples, DPO={dpoStats.dpoPaare} Paare");

                if (sftStats.exportiert < 50)
                {
                    Debug.LogWarning("[SelbstOptimierung] Zu wenig qualitativ gute Erfahrungen. Warte weiter.");
                    phase = OptimierungsPhase.Warten;
                    statistik.aktuellePhase = phase;
                    return;
                }

                // Aktuelle Belohnung messen (Baseline)
                belohnungVorWechsel = BerechneAktuelleBelohnung();
                altesModell = llm.GetAktuellesModell();

                // Fine-Tuning-Job starten
                var version = await fineTuning.StarteFineTuning(
                    sftStats.exportPfad,
                    sftStats.exportiert,
                    belohnungVorWechsel);

                if (version == null || version.status == FineTuningStatus.TrainingFehlgeschlagen)
                {
                    Debug.LogWarning("[SelbstOptimierung] Fine-Tuning-Job konnte nicht gestartet werden.");
                    phase = OptimierungsPhase.Warten;
                    statistik.aktuellePhase = phase;
                    return;
                }

                phase = OptimierungsPhase.TrainingLaeuft;
                statistik.aktuellePhase = phase;
                pollingTimer = 0f;
                zyklenSeitLetztemExport = 0;

                Debug.Log($"[SelbstOptimierung] Fine-Tuning-Job gestartet (Gen {version.generation})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SelbstOptimierung] Fehler beim Export/Training: {ex.Message}");
                phase = OptimierungsPhase.Warten;
                statistik.aktuellePhase = phase;
            }
        }

        // ========== Phase: Training laeuft (Polling) ==========

        private void TickTraining()
        {
            pollingTimer += Time.deltaTime;
            if (pollingTimer < POLLING_INTERVALL) return;
            pollingTimer = 0f;

            _ = PruefeTrainingsStatus();
        }

        private async Task PruefeTrainingsStatus()
        {
            bool fertig = await fineTuning.PruefeJobStatus();

            if (!fertig) return; // Noch am trainieren

            if (fineTuning.Status == FineTuningStatus.TrainingFertig)
            {
                // Modell aktivieren
                phase = OptimierungsPhase.ModellWechsel;
                statistik.aktuellePhase = phase;

                string neuesModell = fineTuning.AktiviereNeuestesModell();

                if (!string.IsNullOrEmpty(neuesModell))
                {
                    llm.WechsleModell(neuesModell);
                    Debug.Log($"[SelbstOptimierung] Modell gewechselt: {altesModell} → {neuesModell}");

                    // Evaluierungsphase starten
                    evaluierungsBelohnungen.Clear();
                    evaluierungsZyklenGezaehlt = 0;
                    phase = OptimierungsPhase.Evaluierung;
                    statistik.aktuellePhase = phase;
                }
                else
                {
                    Debug.LogWarning("[SelbstOptimierung] Kein neues Modell verfuegbar nach Training.");
                    phase = OptimierungsPhase.Warten;
                    statistik.aktuellePhase = phase;
                }
            }
            else if (fineTuning.Status == FineTuningStatus.TrainingFehlgeschlagen)
            {
                Debug.LogWarning("[SelbstOptimierung] Training fehlgeschlagen. Zurueck zum Warten.");
                phase = OptimierungsPhase.Warten;
                statistik.aktuellePhase = phase;
            }
        }

        // ========== Phase: Evaluierung ==========

        private void TickEvaluierung()
        {
            evaluierungsZyklenGezaehlt++;

            // Belohnung des aktuellen Zyklus erfassen
            float aktBelohnung = HoleAktuelleBelohnung();
            if (aktBelohnung != float.MinValue)
                evaluierungsBelohnungen.Add(aktBelohnung);

            // Genuegend Evaluierungszyklen?
            if (evaluierungsZyklenGezaehlt >= config.evaluierungsZyklen)
            {
                TreffeEntscheidung();
            }
        }

        private void TreffeEntscheidung()
        {
            phase = OptimierungsPhase.Entscheidung;
            statistik.aktuellePhase = phase;

            float belohnungNachWechsel = evaluierungsBelohnungen.Count > 0
                ? evaluierungsBelohnungen.Average()
                : 0f;

            float verbesserung = belohnungNachWechsel - belohnungVorWechsel;
            int generation = fineTuning.AktuelleGeneration;

            // Evaluierung im FineTuningManager registrieren
            fineTuning.RegistriereEvaluierung(generation, belohnungNachWechsel);

            statistik.durchlaeufeGesamt++;
            statistik.letzterDurchlauf = DateTime.UtcNow.ToString("o");

            if (verbesserung >= 0)
            {
                // Neues Modell ist besser oder gleich → behalten
                statistik.erfolgreicheUpdates++;
                statistik.kumulierteVerbesserung += verbesserung;
                if (verbesserung > statistik.besteVerbesserung)
                    statistik.besteVerbesserung = verbesserung;

                Debug.Log($"[SelbstOptimierung] ✓ Modell behalten! " +
                    $"Verbesserung: +{verbesserung:F3} " +
                    $"(vorher: {belohnungVorWechsel:F3}, nachher: {belohnungNachWechsel:F3})");
            }
            else
            {
                // Neues Modell ist schlechter → Rollback
                statistik.rollbacks++;

                string rollbackModell = fineTuning.RollbackModell();
                llm.WechsleModell(rollbackModell);

                Debug.LogWarning($"[SelbstOptimierung] ✗ Rollback! " +
                    $"Verschlechterung: {verbesserung:F3} " +
                    $"(vorher: {belohnungVorWechsel:F3}, nachher: {belohnungNachWechsel:F3}) " +
                    $"→ Zurueck zu {rollbackModell}");
            }

            phase = OptimierungsPhase.Warten;
            statistik.aktuellePhase = phase;
        }

        // ========== Belohnungs-Messung ==========

        private float BerechneAktuelleBelohnung()
        {
            // Durchschnittliche Belohnung der letzten N Erfahrungen
            var alle = erfahrungen?.Alle();
            if (alle == null || alle.Count == 0) return 0f;

            int n = Mathf.Min(50, alle.Count);
            return alle
                .OrderByDescending(e => e.zeitstempel)
                .Take(n)
                .Average(e => e.belohnung);
        }

        private float HoleAktuelleBelohnung()
        {
            var emotionen = agiKern?.GetEmotionen()?.zustand;
            if (emotionen == null) return float.MinValue;
            return emotionen.freude - emotionen.frustration;
        }

        // ========== Oeffentliche API ==========

        public void StarteOptimierung()
        {
            aktiv = true;
            phase = OptimierungsPhase.Warten;
            zyklenSeitLetztemExport = 0;
            Debug.Log("[SelbstOptimierung] Selbstoptimierung aktiviert.");
        }

        public void PauseOptimierung()
        {
            aktiv = false;
            Debug.Log("[SelbstOptimierung] Selbstoptimierung pausiert.");
        }

        /// <summary>
        /// Manueller Trigger: Sofort Export + Fine-Tuning starten
        /// (ignoriert Mindest-Zyklen, prueft aber Mindest-Erfahrungen)
        /// </summary>
        public void ErzwingeFineTuning()
        {
            if (phase != OptimierungsPhase.Warten)
            {
                Debug.LogWarning($"[SelbstOptimierung] Kann nicht starten — Phase: {phase}");
                return;
            }

            int erfahrungsAnzahl = erfahrungen?.Anzahl() ?? 0;
            if (erfahrungsAnzahl < 50)
            {
                Debug.LogWarning($"[SelbstOptimierung] Zu wenig Erfahrungen ({erfahrungsAnzahl}/50).");
                return;
            }

            _ = StarteExportUndTraining();
        }

        /// <summary>
        /// Manueller Rollback zum vorherigen Modell.
        /// </summary>
        public void ManuellRollback()
        {
            string modell = fineTuning?.RollbackModell();
            if (!string.IsNullOrEmpty(modell))
            {
                llm?.WechsleModell(modell);
                Debug.Log($"[SelbstOptimierung] Manueller Rollback zu: {modell}");
            }
        }

        public string GetStatusText()
        {
            string modell = fineTuning?.AktuellesModell ?? "unbekannt";
            int gen = fineTuning?.AktuelleGeneration ?? 0;

            return $"Phase: {phase} | Gen: {gen} | Modell: {modell} | " +
                   $"Updates: {statistik.erfolgreicheUpdates}/{statistik.durchlaeufeGesamt} | " +
                   $"Rollbacks: {statistik.rollbacks} | " +
                   $"Kumul. Verbesserung: {statistik.kumulierteVerbesserung:+0.000;-0.000;0}";
        }

        /// <summary>
        /// Wird extern (z.B. von AutoTrainer) jeden Zyklus aufgerufen,
        /// um den Zyklen-Zaehler zu inkrementieren.
        /// </summary>
        public void RegistriereZyklus()
        {
            if (phase == OptimierungsPhase.Warten)
                zyklenSeitLetztemExport++;
        }
    }
}
