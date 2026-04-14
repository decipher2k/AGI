using System;
using System.Collections.Generic;
using System.Linq;
using BilligAGI.Modelle;
using BilligAGI.Daten;
using UnityEngine;

namespace BilligAGI.Kern
{
    // ============================================================
    //  MentaleSimulation — "Theater im Kopf"
    //
    //  Das System kann hypothetische Szenarien DURCHSPIELEN
    //  ohne tatsaechlich zu handeln:
    //
    //  1. "Was passiert wenn ich X tue?" → Forward-Rollout
    //  2. "Welche Aktionssequenz fuehrt zum besten Ergebnis?"
    //     → Mehrere Pfade simulieren, dann vergleichen
    //  3. "Was waere gewesen wenn ich Y statt X getan haette?"
    //     → Kontrafaktische Simulation
    //
    //  Nutzt PrediktivesWeltModell fuer Forward-Prediction
    //  und IntuitiverPhysikSimulator fuer Physik-Constraints.
    //
    //  Ergebnis fliesst in Planer-Entscheidungen ein:
    //  Statt blind LLM-Plan auszufuehren, werden Aktionen
    //  zuerst mental simuliert.
    // ============================================================

    [Serializable]
    public class SimulierterPfad
    {
        public AktionsTyp[] aktionen;
        public float kumulativeBelohnung;
        public float[] endZustand;
        public List<SimulationsSchritt> schritte = new();
        public float konfidenz;                     // Sinkt mit Pfadlaenge
        public float physikStabilitaet;             // Wie physikalisch plausibel
    }

    [Serializable]
    public class SimulationsSchritt
    {
        public int schrittNummer;
        public AktionsTyp aktion;
        public float[] zustandVorher;
        public float[] zustandNachher;
        public float belohnung;
        public float vorhersageKonfidenz;
    }

    [Serializable]
    public class WasWennErgebnis
    {
        public string frage;
        public AktionsTyp aktion;
        public float[] startZustand;
        public SimulierterPfad simulierterPfad;
        public string beschreibung;
    }

    [Serializable]
    public class KontrafaktischesErgebnis
    {
        public string beschreibung;
        public AktionsTyp tatsaechlicheAktion;
        public AktionsTyp alternativeAktion;
        public float tatsaechlicheBelohnung;
        public float kontrafaktischeBelohnung;
        public float differenz;                     // positiv = alternative waere besser gewesen
        public string bewertung;
    }

    [Serializable]
    public class MentaleSimStatistik
    {
        public int simulationenGesamt;
        public int wasWennAnfragen;
        public int kontrafaktischeAnfragen;
        public int planVerbesserungen;             // Wie oft hat Simulation den Plan verbessert
        public float durchschnittlichePfadLaenge;
    }

    public class MentaleSimulation
    {
        private readonly PrediktivesWeltModell weltModell;
        private readonly IntuitiverPhysikSimulator physikSim;
        private readonly AGIConfig config;

        private MentaleSimStatistik statistik;
        private SimulierterPfad letzterBesterPfad;
        private List<KontrafaktischesErgebnis> kontrafaktischeHistorie = new();

        // Simulations-Parameter
        private const int MAX_ROLLOUT_TIEFE = 8;         // Max Schritte pro Simulation
        private const int PLANUNGS_BREITE = 5;            // Wie viele Pfade parallel
        private const int MAX_KONTRAFAKTISCHE = 20;       // Historie-Groesse
        private const float KONFIDENZ_ZERFALL = 0.85f;    // Pro Schritt
        private const float MIN_MODELL_KONFIDENZ = 0.3f;  // Ab hier Simulation abbrechen
        private const string PERSISTENZ_DATEI = "mentale_sim_statistik.json";

        public MentaleSimulation(
            PrediktivesWeltModell weltModell,
            IntuitiverPhysikSimulator physikSim,
            AGIConfig config)
        {
            this.weltModell = weltModell;
            this.physikSim = physikSim;
            this.config = config;

            statistik = DatenLader.Lade<MentaleSimStatistik>(PERSISTENZ_DATEI) ?? new MentaleSimStatistik();
            Debug.Log($"[MentaleSim] Initialisiert. {statistik.simulationenGesamt} bisherige Simulationen.");
        }

        // ======== 1. "Was passiert wenn ich X tue?" ========

        /// <summary>
        /// Simuliert eine einzelne Aktion vom aktuellen Zustand aus.
        /// Gibt vorhergesagten Zustand + Belohnung zurueck.
        /// </summary>
        public WasWennErgebnis WasPassiertWenn(float[] aktuellerZustand, AktionsTyp aktion)
        {
            if (aktuellerZustand == null || weltModell == null || !weltModell.Aktiv)
                return null;

            var pfad = SimulierePfad(aktuellerZustand, new[] { aktion });

            statistik.wasWennAnfragen++;
            statistik.simulationenGesamt++;

            return new WasWennErgebnis
            {
                frage = $"Was passiert wenn ich '{aktion}' tue?",
                aktion = aktion,
                startZustand = aktuellerZustand,
                simulierterPfad = pfad,
                beschreibung = BeschreibeSimulation(pfad)
            };
        }

        // ======== 2. Beste Aktionssequenz finden ========

        /// <summary>
        /// Simuliert mehrere Aktionssequenzen und gibt die beste zurueck.
        /// Beam-Search-artig: Breite x Tiefe Simulationen.
        /// </summary>
        public SimulierterPfad FindeBesteSequenz(float[] startZustand, int tiefe = 0)
        {
            if (startZustand == null || weltModell == null || !weltModell.Aktiv)
                return null;

            if (tiefe <= 0) tiefe = Mathf.Min(MAX_ROLLOUT_TIEFE, weltModell.GetAnzahlTransitionen() / 50 + 2);

            var alleAktionen = (AktionsTyp[])Enum.GetValues(typeof(AktionsTyp));
            var pfade = new List<SimulierterPfad>();

            // Phase 1: Alle Startaktionen einzeln bewerten
            var startBewertungen = new List<(AktionsTyp aktion, float belohnung, float[] endZustand)>();
            foreach (var aktion in alleAktionen)
            {
                var vorhersage = weltModell.Vorhersage(startZustand, aktion);
                if (vorhersage == null) continue;
                startBewertungen.Add((aktion, vorhersage.vorhergesagteBelohnung, vorhersage.vorhergesagterZustand));
            }

            // Top-N Startaktionen weiterverfolgen
            var topStarts = startBewertungen
                .OrderByDescending(x => x.belohnung)
                .Take(PLANUNGS_BREITE)
                .ToList();

            // Phase 2: Fuer jede Top-Start-Aktion den besten Pfad rollout-en
            foreach (var (startAktion, _, zwischenZustand) in topStarts)
            {
                var aktionsSequenz = new List<AktionsTyp> { startAktion };
                var aktuellerZustand = zwischenZustand;
                float konfidenz = KONFIDENZ_ZERFALL;

                // Greedy expansion: Waehle jeweils die beste naechste Aktion
                for (int schritt = 1; schritt < tiefe && konfidenz > MIN_MODELL_KONFIDENZ; schritt++)
                {
                    AktionsTyp besteNaechste = AktionsTyp.Beobachten;
                    float besteBelohnung = float.MinValue;
                    float[] besterZustand = null;

                    foreach (var naechsteAktion in alleAktionen)
                    {
                        var vorhersage = weltModell.Vorhersage(aktuellerZustand, naechsteAktion);
                        if (vorhersage != null && vorhersage.vorhergesagteBelohnung > besteBelohnung)
                        {
                            besteBelohnung = vorhersage.vorhergesagteBelohnung;
                            besteNaechste = naechsteAktion;
                            besterZustand = vorhersage.vorhergesagterZustand;
                        }
                    }

                    aktionsSequenz.Add(besteNaechste);
                    aktuellerZustand = besterZustand ?? aktuellerZustand;
                    konfidenz *= KONFIDENZ_ZERFALL;
                }

                // Simuliere den vollstaendigen Pfad
                var pfad = SimulierePfad(startZustand, aktionsSequenz.ToArray());
                pfade.Add(pfad);
            }

            // Besten Pfad waehlen (Belohnung * Konfidenz)
            var bester = pfade.OrderByDescending(p =>
                p.kumulativeBelohnung * p.konfidenz).FirstOrDefault();

            letzterBesterPfad = bester;
            statistik.simulationenGesamt += pfade.Count;
            statistik.durchschnittlichePfadLaenge =
                (statistik.durchschnittlichePfadLaenge * (statistik.simulationenGesamt - pfade.Count)
                + pfade.Sum(p => p.aktionen.Length)) / statistik.simulationenGesamt;

            return bester;
        }

        // ======== 3. Kontrafaktische Analyse ========

        /// <summary>
        /// "Was waere gewesen wenn ich Y statt X gemacht haette?"
        /// Vergleicht tatsaechliches Ergebnis mit simulierter Alternative.
        /// </summary>
        public KontrafaktischesErgebnis Kontrafaktisch(
            float[] zustandVorher,
            AktionsTyp tatsaechlicheAktion,
            float tatsaechlicheBelohnung,
            AktionsTyp alternativeAktion)
        {
            if (zustandVorher == null || weltModell == null || !weltModell.Aktiv)
                return null;

            var alternativVorhersage = weltModell.Vorhersage(zustandVorher, alternativeAktion);
            if (alternativVorhersage == null) return null;

            float differenz = alternativVorhersage.vorhergesagteBelohnung - tatsaechlicheBelohnung;

            var ergebnis = new KontrafaktischesErgebnis
            {
                tatsaechlicheAktion = tatsaechlicheAktion,
                alternativeAktion = alternativeAktion,
                tatsaechlicheBelohnung = tatsaechlicheBelohnung,
                kontrafaktischeBelohnung = alternativVorhersage.vorhergesagteBelohnung,
                differenz = differenz,
                beschreibung = $"Statt '{tatsaechlicheAktion}' haette '{alternativeAktion}' " +
                    $"eine Belohnung von {alternativVorhersage.vorhergesagteBelohnung:F2} " +
                    $"gebracht (Δ={differenz:+0.00;-0.00}).",
                bewertung = differenz > 0.1f ? "Alternative waere besser gewesen."
                    : differenz < -0.1f ? "Richtige Entscheidung getroffen."
                    : "Kein wesentlicher Unterschied."
            };

            kontrafaktischeHistorie.Add(ergebnis);
            while (kontrafaktischeHistorie.Count > MAX_KONTRAFAKTISCHE)
                kontrafaktischeHistorie.RemoveAt(0);

            statistik.kontrafaktischeAnfragen++;
            return ergebnis;
        }

        // ======== 4. Plan mental vorab testen ========

        /// <summary>
        /// Nimmt einen Plan (Liste von Aktionen) und simuliert ihn mental.
        /// Gibt zurueck ob der Plan erfolgversprechend ist.
        /// </summary>
        public (float erwarteteGesamtBelohnung, float konfidenz, string bewertung)
            TestePlanMental(float[] startZustand, List<Aktion> planAktionen)
        {
            if (startZustand == null || planAktionen == null || planAktionen.Count == 0)
                return (0f, 0f, "Kein Plan zum Testen.");

            if (weltModell == null || !weltModell.Aktiv)
                return (0f, 0f, "Weltmodell nicht aktiv.");

            var aktionsTypen = planAktionen.Select(a => a.typ).ToArray();
            var pfad = SimulierePfad(startZustand, aktionsTypen);

            string bewertung;
            if (pfad.kumulativeBelohnung > 0.5f && pfad.konfidenz > 0.4f)
                bewertung = "Plan sieht vielversprechend aus.";
            else if (pfad.kumulativeBelohnung > 0f)
                bewertung = "Plan koennte funktionieren, aber unsicher.";
            else if (pfad.kumulativeBelohnung > -0.3f)
                bewertung = "Plan bringt voraussichtlich wenig.";
            else
                bewertung = "Plan scheint kontraproduktiv — Umplanung empfohlen.";

            return (pfad.kumulativeBelohnung, pfad.konfidenz, bewertung);
        }

        // ======== 5. Periodischer Tick ========

        /// <summary>
        /// Automatische kontrafaktische Analyse der letzten Aktion.
        /// Wird im Zyklus aufgerufen wenn eine Erfahrung vorliegt.
        /// </summary>
        public KontrafaktischesErgebnis ZyklusTick(
            float[] zustandVorher,
            AktionsTyp ausgefuehrteAktion,
            float erhalteneBelohnung)
        {
            if (zustandVorher == null || weltModell == null || !weltModell.Aktiv)
                return null;

            // Finde die beste alternative Aktion
            var alleAktionen = (AktionsTyp[])Enum.GetValues(typeof(AktionsTyp));
            AktionsTyp besteAlternative = ausgefuehrteAktion;
            float besteBelohnung = float.MinValue;

            foreach (var alt in alleAktionen)
            {
                if (alt == ausgefuehrteAktion) continue;
                var vorhersage = weltModell.Vorhersage(zustandVorher, alt);
                if (vorhersage != null && vorhersage.vorhergesagteBelohnung > besteBelohnung)
                {
                    besteBelohnung = vorhersage.vorhergesagteBelohnung;
                    besteAlternative = alt;
                }
            }

            // Nur kontrafaktische Analyse wenn deutlicher Unterschied
            if (besteAlternative == ausgefuehrteAktion) return null;
            if (Mathf.Abs(besteBelohnung - erhalteneBelohnung) < 0.05f) return null;

            var ergebnis = Kontrafaktisch(zustandVorher, ausgefuehrteAktion,
                erhalteneBelohnung, besteAlternative);

            if (ergebnis != null && ergebnis.differenz > 0.2f)
            {
                statistik.planVerbesserungen++;
                Debug.Log($"[MentaleSim] Kontrafaktisch: '{ergebnis.alternativeAktion}' " +
                    $"waere besser gewesen (Δ={ergebnis.differenz:+0.00})");
            }

            return ergebnis;
        }

        // ======== Simulation-Kern ========

        private SimulierterPfad SimulierePfad(float[] startZustand, AktionsTyp[] aktionen)
        {
            var pfad = new SimulierterPfad
            {
                aktionen = aktionen,
                konfidenz = 1f,
                physikStabilitaet = 1f
            };

            float[] aktuellerZustand = startZustand;
            float kumulativeBelohnung = 0f;

            for (int i = 0; i < aktionen.Length; i++)
            {
                var vorhersage = weltModell.Vorhersage(aktuellerZustand, aktionen[i]);
                if (vorhersage == null)
                {
                    pfad.konfidenz *= 0.5f; // Unbekannter Uebergang
                    break;
                }

                var schritt = new SimulationsSchritt
                {
                    schrittNummer = i,
                    aktion = aktionen[i],
                    zustandVorher = aktuellerZustand,
                    zustandNachher = vorhersage.vorhergesagterZustand,
                    belohnung = vorhersage.vorhergesagteBelohnung,
                    vorhersageKonfidenz = vorhersage.konfidenz
                };

                pfad.schritte.Add(schritt);
                kumulativeBelohnung += vorhersage.vorhergesagteBelohnung;
                pfad.konfidenz *= vorhersage.konfidenz * KONFIDENZ_ZERFALL;

                aktuellerZustand = vorhersage.vorhergesagterZustand;

                // Abbruch bei zu niedriger Konfidenz
                if (pfad.konfidenz < MIN_MODELL_KONFIDENZ)
                    break;
            }

            pfad.kumulativeBelohnung = kumulativeBelohnung;
            pfad.endZustand = aktuellerZustand;

            return pfad;
        }

        private string BeschreibeSimulation(SimulierterPfad pfad)
        {
            if (pfad == null || pfad.schritte.Count == 0)
                return "Simulation fehlgeschlagen.";

            var teile = new List<string>();
            foreach (var s in pfad.schritte)
            {
                string bewertung = s.belohnung > 0.1f ? "+" : s.belohnung < -0.1f ? "−" : "○";
                teile.Add($"{bewertung}{s.aktion}");
            }

            return $"[{string.Join("→", teile)}] " +
                $"Σ={pfad.kumulativeBelohnung:F2}, Konfidenz={pfad.konfidenz:F2}";
        }

        // ======== Status ========

        public string GetStatusText()
        {
            bool modellAktiv = weltModell?.Aktiv ?? false;
            int transitionen = weltModell?.GetAnzahlTransitionen() ?? 0;

            return $"Simulationen: {statistik.simulationenGesamt} | " +
                $"Was-Wenn: {statistik.wasWennAnfragen} | " +
                $"Kontrafaktisch: {statistik.kontrafaktischeAnfragen} | " +
                $"Plan-Verbesserungen: {statistik.planVerbesserungen} | " +
                $"∅ Pfadlaenge: {statistik.durchschnittlichePfadLaenge:F1} | " +
                $"Weltmodell: {(modellAktiv ? $"aktiv ({transitionen} Transitionen)" : "inaktiv")}";
        }

        public SimulierterPfad GetLetzterBesterPfad() => letzterBesterPfad;

        public List<KontrafaktischesErgebnis> GetKontrafaktischeHistorie() => kontrafaktischeHistorie;

        public MentaleSimStatistik GetStatistik() => statistik;

        public void Persistiere()
        {
            DatenLader.Speichere(PERSISTENZ_DATEI, statistik);
        }
    }
}
