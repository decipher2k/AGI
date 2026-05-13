using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BilligAGI.Modelle;
using BilligAGI.Welt;

namespace BilligAGI.Kern
{
    // =====================================================================
    // AutoTrainer: Automatisiertes Kurrikulum-Training fuer Billig-AGI
    //
    // Haengt als MonoBehaviour an ein GameObject. Braucht Referenz auf AGIKern.
    // Generiert synthetische Inputs, verwaltet Explorationsziele,
    // trackt Lernfortschritt und eskaliert durch 6 Phasen.
    //
    // Setup: GameObject → AutoTrainer → agiKern zuweisen → Play
    // =====================================================================

    public class AutoTrainer : MonoBehaviour
    {
        [Header("Referenzen")]
        public AGIKern agiKern;

        [Header("Training")]
        public bool trainingAktiv = true;
        public float trainingsIntervall = 3f;          // Sekunden zwischen Inputs
        public float konsolidierungsIntervall = 120f;   // Sekunden zwischen Konsolidierungen
        public int maxZyklenProSitzung = 1000;
        public TrainingsPhase startPhase = TrainingsPhase.Beobachten;

        [Header("Neugier-Modus (Phase Frei)")]
        public bool neugierAlsInput = true;             // Hypothesen als Input nutzen
        public float neugierInputChance = 0.3f;         // Auch in fruehen Phasen manchmal Neugier

        [Header("Logging")]
        public bool detailLog = true;
        public int logIntervall = 10;                   // Alle N Zyklen Status loggen

        // Interner Zustand
        private TrainingsKurrikulum kurrikulum;
        private TrainingsStatistik statistik;
        private float letzterInput;
        private float letzteKonsolidierung;
        private string letztesZielId;
        private bool wartet;
        private List<NPCInfo> npcCache;
        private float npcCacheTimer;
        private const float NPC_CACHE_INTERVALL = 10f;

        private void Start()
        {
            if (agiKern == null)
            {
                Debug.LogError("[AutoTrainer] Kein AGIKern zugewiesen!");
                enabled = false;
                return;
            }

            kurrikulum = new TrainingsKurrikulum(startPhase);

            statistik = new TrainingsStatistik
            {
                phase = startPhase,
                startZeit = DateTime.UtcNow.ToString("o")
            };

            npcCache = new List<NPCInfo>();

            Debug.Log($"[AutoTrainer] Gestartet — Phase: {startPhase}, Intervall: {trainingsIntervall}s");
        }

        private void Update()
        {
            if (!trainingAktiv || !agiKern.IstInitialisiert()) return;

            // API-Anfragen haben Vorrang: Der AutoTrainer darf den AGI-Kern nicht
            // dauerhaft mit synthetischen Inputs belegen, sonst erhalten OpenAI-
            // kompatible Clients nur 503/busy-Antworten.
            if (agiKern.HatWartendeApiAnfragen())
                return;

            if (wartet)
            {
                if (agiKern.IstBeschaeftigt())
                    return;

                wartet = false;
            }

            if (agiKern.IstBeschaeftigt())
                return;

            if (statistik.zyklenGesamt >= maxZyklenProSitzung)
            {
                if (trainingAktiv)
                {
                    Debug.Log($"[AutoTrainer] Max. Zyklen erreicht ({maxZyklenProSitzung}). Training pausiert.");
                    trainingAktiv = false;
                    LogStatistik();
                }
                return;
            }

            // NPC-Cache aktualisieren
            npcCacheTimer -= Time.deltaTime;
            if (npcCacheTimer <= 0f)
            {
                AktualisiereNPCCache();
                npcCacheTimer = NPC_CACHE_INTERVALL;
            }

            // Periodische Konsolidierung
            if (Time.time - letzteKonsolidierung > konsolidierungsIntervall)
            {
                agiKern.GetKonsolidierung()?.Konsolidiere();
                letzteKonsolidierung = Time.time;
                if (detailLog) Debug.Log("[AutoTrainer] Konsolidierung ausgefuehrt.");
            }

            // Naechsten Input senden
            if (Time.time - letzterInput < trainingsIntervall) return;
            letzterInput = Time.time;

            FuehreTrainingsSchrittAus();
        }

        private void FuehreTrainingsSchrittAus()
        {
            var welt = agiKern.GetWeltModell()?.zustand;

            // 1. Ggf. neues Ziel setzen
            VerwaltZiele(welt);

            // 2. Input generieren
            string input = null;

            // Neugier-basierter Input (Chance steigt mit Phase)
            bool nutzeNeugier = neugierAlsInput &&
                (kurrikulum.AktuellePhase == TrainingsPhase.Frei ||
                 UnityEngine.Random.value < neugierInputChance);

            if (nutzeNeugier)
                input = GeneriereNeugierInput();

            // Kurrikulum-Input als Fallback oder Standard
            if (string.IsNullOrEmpty(input))
                input = kurrikulum.GeneriereInput(welt, npcCache);

            // In Phase Frei ohne Input: autonomen Modus nutzen
            if (string.IsNullOrEmpty(input))
            {
                if (!agiKern.GetModus().Contains("AUTONOM"))
                    agiKern.SetzeAutonom(true);
                return;
            }

            // 3. Input an AGI senden
            agiKern.VerarbeiteInput(input);
            wartet = true;

            if (detailLog)
                Debug.Log($"[AutoTrainer] [{kurrikulum.AktuellePhase}] → \"{TruncateLog(input, 80)}\"");

            // 4. Statistik aktualisieren (Belohnung kommt asynchron im naechsten Zyklus)
            AktualisiereStatistik();

            // 5. Aufstieg pruefen
            kurrikulum.PruefeAufstieg();
            statistik.phase = kurrikulum.AktuellePhase;

            // 6. SelbstOptimierung benachrichtigen (Zyklen-Zaehler)
            agiKern.GetSelbstOptimierung()?.RegistriereZyklus();
        }

        // ---- Ziel-Verwaltung ----

        private void VerwaltZiele(WeltZustand welt)
        {
            var zielManager = agiKern.GetZielManager();
            var aktivesZiel = zielManager.GetAktivesZiel();

            // Kein aktives Ziel → neues generieren
            if (aktivesZiel == null)
            {
                var zielVorschlag = kurrikulum.GeneriereZiel(welt, npcCache);
                if (zielVorschlag.HasValue)
                {
                    var (beschreibung, typ) = zielVorschlag.Value;
                    float prio = 0.5f + (int)kurrikulum.AktuellePhase * 0.1f;
                    var neuesZiel = zielManager.FormuliereZiel(beschreibung, typ, prio);
                    letztesZielId = neuesZiel.id;

                    if (detailLog)
                        Debug.Log($"[AutoTrainer] Neues Ziel: {beschreibung} ({typ})");
                }
            }
            // Aktives Ziel zu lange offen → als gescheitert markieren und weiter
            else if (aktivesZiel.id == letztesZielId && kurrikulum.PhaseZyklen > 15)
            {
                // Ziel hat zu viele Zyklen gebraucht — weiter
                zielManager.ZielGescheitert(aktivesZiel.id, "Timeout im Training");
                statistik.zieleGescheitert++;
                kurrikulum.RegistriereZyklus(-0.2f, false);
                letztesZielId = null;
            }
        }

        // ---- Neugier-basierte Inputs ----

        private string GeneriereNeugierInput()
        {
            var welt = agiKern.GetWeltModell();
            var selbst = agiKern.GetSelbstModell();

            if (selbst == null) return null;

            // Niedrigste Kompetenz finden
            var kompetenzen = selbst.GetAlleKompetenzen();
            if (kompetenzen.Count > 0)
            {
                var niedrigste = kompetenzen.OrderBy(k => k.Value).First();
                if (niedrigste.Value < 0.4f)
                {
                    string[] templates = {
                        $"Uebe deine Faehigkeit in '{niedrigste.Key}'. Was koenntest du ausprobieren?",
                        $"Deine Kompetenz in '{niedrigste.Key}' ist noch niedrig. Experimentiere!",
                        $"Versuche etwas Neues im Bereich '{niedrigste.Key}'."
                    };
                    return templates[UnityEngine.Random.Range(0, templates.Length)];
                }
            }

            // Weltmodell: unerforschte Objekte
            if (welt?.zustand?.objekte != null)
            {
                var unbekannt = welt.zustand.objekte.Values
                    .Where(o => o.zustand == "unbekannt" || string.IsNullOrEmpty(o.zustand))
                    .ToList();

                if (unbekannt.Count > 0)
                {
                    var obj = unbekannt[UnityEngine.Random.Range(0, unbekannt.Count)];
                    return $"Untersuche {obj.name} — es ist noch unexploriert.";
                }
            }

            return null;
        }

        // ---- Statistik ----

        private void AktualisiereStatistik()
        {
            statistik.zyklenGesamt++;
            statistik.zyklenInPhase++;
            statistik.letzterTick = DateTime.UtcNow.ToString("o");

            // Erfahrungen zaehlen
            var erfahrungen = agiKern.GetErfahrungen();
            if (erfahrungen != null)
                statistik.erfahrungenGesamt = erfahrungen.Anzahl();

            // RL-Stats
            var dqn = agiKern.GetDQN();
            var rl = agiKern.GetRL();
            if (dqn != null)
            {
                statistik.explorationRate = dqn.GetExplorationRate();
                statistik.dqnUpdates = dqn.GetGesamtUpdates();
            }
            else if (rl != null)
            {
                statistik.explorationRate = rl.GetExplorationRate();
            }

            // WeltModell-Stats
            var weltModell = agiKern.GetPrediktivesWeltModell();
            if (weltModell != null)
                statistik.weltModellTransitionen = weltModell.GetAnzahlTransitionen();

            // Ziel-Stats
            var zieleHistorie = agiKern.GetZielManager()?.GetHistorie();
            if (zieleHistorie != null)
            {
                statistik.zieleErreicht = zieleHistorie.Count(z => z.status == ZielStatus.ERREICHT);
                statistik.zieleGescheitert = zieleHistorie.Count(z => z.status == ZielStatus.GESCHEITERT);
            }

            // Belohnungs-Regression
            var emotionen = agiKern.GetEmotionen();
            if (emotionen != null)
            {
                float belohnung = emotionen.zustand.freude - emotionen.zustand.frustration;
                statistik.belohnungsHistorie.Add(belohnung);
                // Rolling average der letzten 20
                int n = Mathf.Min(20, statistik.belohnungsHistorie.Count);
                statistik.durchschnittsBelohnung = statistik.belohnungsHistorie
                    .Skip(statistik.belohnungsHistorie.Count - n).Average();

                kurrikulum.RegistriereZyklus(belohnung, belohnung > 0);
            }

            // Periodisches Logging
            if (statistik.zyklenGesamt % logIntervall == 0)
                LogStatistik();
        }

        private void LogStatistik()
        {
            Debug.Log(
                $"[AutoTrainer] ══════════════════════════════════════\n" +
                $"  Phase: {statistik.phase} | Zyklus: {statistik.zyklenGesamt}/{maxZyklenProSitzung}\n" +
                $"  Erfahrungen: {statistik.erfahrungenGesamt} | Ziele: {statistik.zieleErreicht}✓ {statistik.zieleGescheitert}✗\n" +
                $"  ∅ Belohnung: {statistik.durchschnittsBelohnung:F3} | Exploration: {statistik.explorationRate:P0}\n" +
                $"  DQN-Updates: {statistik.dqnUpdates} | WeltModell: {statistik.weltModellTransitionen} Transitionen\n" +
                $"══════════════════════════════════════");
        }

        // ---- NPC-Cache ----

        private void AktualisiereNPCCache()
        {
            npcCache.Clear();
            var npcs = FindObjectsByType<NPCVerhalten>(FindObjectsSortMode.None);
            foreach (var npc in npcs)
            {
                npcCache.Add(new NPCInfo
                {
                    id = npc.npcId,
                    name = npc.anzeigeName,
                    rolle = npc.rolle.ToString(),
                    aktuelleAktion = npc.AktuelleAktionsBeschreibung
                });
            }
        }

        // ---- Oeffentliche API ----

        public TrainingsStatistik GetStatistik() => statistik;
        public TrainingsPhase GetPhase() => kurrikulum?.AktuellePhase ?? TrainingsPhase.Beobachten;

        public void StartTraining()
        {
            trainingAktiv = true;
            Debug.Log("[AutoTrainer] Training gestartet.");
        }

        public void PauseTraining()
        {
            trainingAktiv = false;
            LogStatistik();
            Debug.Log("[AutoTrainer] Training pausiert.");
        }

        public void ResetTraining()
        {
            kurrikulum = new TrainingsKurrikulum(startPhase);
            statistik = new TrainingsStatistik
            {
                phase = startPhase,
                startZeit = DateTime.UtcNow.ToString("o")
            };
            letztesZielId = null;
            wartet = false;
            Debug.Log("[AutoTrainer] Training zurueckgesetzt.");
        }

        public void SetzePhase(TrainingsPhase phase)
        {
            kurrikulum = new TrainingsKurrikulum(phase);
            statistik.phase = phase;
            statistik.zyklenInPhase = 0;
            Debug.Log($"[AutoTrainer] Phase manuell gesetzt: {phase}");
        }

        // ---- Helfer ----

        private static string TruncateLog(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length <= maxLen ? text : text.Substring(0, maxLen) + "...";
        }
    }
}
