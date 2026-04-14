using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BilligAGI.Modelle;
using BilligAGI.Daten;
using UnityEngine;

namespace BilligAGI.Kern
{
    /// <summary>
    /// Meta-Kognition: Das System beobachtet sich selbst.
    ///
    /// Nicht "denke nach" (das macht der Planer).
    /// Sondern "denke darueber nach wie ich denke":
    ///
    /// 1. Strategie-Tracking: Welche Handlungsstrategien funktionieren in welchen Kontexten?
    /// 2. Lern-Monitoring: Lerne ich noch oder stagniere ich?
    /// 3. Bias-Erkennung: Bevorzuge ich bestimmte Aktionen ohne Grund?
    /// 4. Pipeline-Empfehlungen: Welche Subsysteme helfen, welche nicht?
    ///
    /// Generiert MetaEinsichten die in die Entscheidungsfindung einfliessen.
    /// </summary>
    public class MetaKognition
    {
        private List<StrategieEpisode> strategieHistorie;
        private Dictionary<string, StrategieStatistik> strategieStats;
        private List<MetaEinsicht> einsichten;
        private LernKurve lernKurve;
        private int zyklusZaehler;

        private const string PERSISTENZ_DATEI = "meta_kognition.json";
        private const int ANALYSE_INTERVALL = 20;     // Alle N Zyklen tiefe Analyse
        private const int MAX_HISTORIE = 500;
        private const int MIN_STICHPROBE = 10;         // Mindestens N Episoden fuer Statistik

        public MetaKognition()
        {
            var gespeichert = DatenLader.Lade<MetaKognitionZustand>(PERSISTENZ_DATEI);
            if (gespeichert != null)
            {
                strategieHistorie = gespeichert.strategieHistorie ?? new List<StrategieEpisode>();
                strategieStats = gespeichert.strategieStats ?? new Dictionary<string, StrategieStatistik>();
                einsichten = gespeichert.einsichten ?? new List<MetaEinsicht>();
                lernKurve = gespeichert.lernKurve ?? new LernKurve();
                Debug.Log($"[MetaKognition] {strategieHistorie.Count} Episoden, {einsichten.Count} Einsichten geladen.");
            }
            else
            {
                strategieHistorie = new List<StrategieEpisode>();
                strategieStats = new Dictionary<string, StrategieStatistik>();
                einsichten = new List<MetaEinsicht>();
                lernKurve = new LernKurve();
            }
        }

        // ======== Strategie-Tracking ========

        /// <summary>
        /// Registriert eine Entscheidung: Was wurde getan, in welchem Kontext, mit welchem Ergebnis.
        /// </summary>
        public void RegistriereEntscheidung(
            string strategie,           // z.B. "exploration", "rl_empfehlung", "llm_plan", "kreativ"
            string kontextCluster,      // z.B. "physik", "sozial"
            float belohnung,
            bool erfolg,
            float rlKonfidenz,         // War RL involviert?
            bool llmGenutzt)           // War LLM involviert?
        {
            var episode = new StrategieEpisode
            {
                strategie = strategie,
                kontextCluster = kontextCluster,
                belohnung = belohnung,
                erfolg = erfolg,
                rlKonfidenz = rlKonfidenz,
                llmGenutzt = llmGenutzt,
                zeitstempel = Time.time,
                zyklusNummer = zyklusZaehler
            };

            strategieHistorie.Add(episode);
            while (strategieHistorie.Count > MAX_HISTORIE)
                strategieHistorie.RemoveAt(0);

            // Statistik aktualisieren
            string key = $"{strategie}::{kontextCluster}";
            if (!strategieStats.TryGetValue(key, out var stats))
            {
                stats = new StrategieStatistik { strategie = strategie, kontextCluster = kontextCluster };
                strategieStats[key] = stats;
            }
            stats.anzahl++;
            stats.erfolge += erfolg ? 1 : 0;
            stats.durchschnittBelohnung =
                (stats.durchschnittBelohnung * (stats.anzahl - 1) + belohnung) / stats.anzahl;
        }

        // ======== Lern-Monitoring ========

        /// <summary>
        /// Registriert einen Lern-Datenpunkt fuer die Lernkurve.
        /// </summary>
        public void RegistriereLernSchritt(float belohnung, int rlBekannteZustaende, int gesamtInstanzen)
        {
            lernKurve.belohnungen.Add(belohnung);
            lernKurve.rlZustaende.Add(rlBekannteZustaende);
            lernKurve.instanzenAnzahl.Add(gesamtInstanzen);

            while (lernKurve.belohnungen.Count > 200)
            {
                lernKurve.belohnungen.RemoveAt(0);
                lernKurve.rlZustaende.RemoveAt(0);
                lernKurve.instanzenAnzahl.RemoveAt(0);
            }
        }

        // ======== Tiefe Analyse (periodisch) ========

        /// <summary>
        /// Tick: Wird jeden Zyklus aufgerufen. Periodisch tiefe Analyse.
        /// </summary>
        public void Tick()
        {
            zyklusZaehler++;
            if (zyklusZaehler % ANALYSE_INTERVALL != 0) return;

            AnalysiereStrategien();
            AnalysiereLernkurve();
            ErkenneBias();
            Persistiere();
        }

        /// <summary>
        /// Welche Strategien funktionieren in welchen Kontexten?
        /// </summary>
        private void AnalysiereStrategien()
        {
            foreach (var kvp in strategieStats)
            {
                var stats = kvp.Value;
                if (stats.anzahl < MIN_STICHPROBE) continue;

                float erfolgsRate = stats.erfolge / (float)stats.anzahl;

                // Strategie in bestimmtem Kontext sehr erfolgreich → Einsicht
                if (erfolgsRate > 0.7f && stats.anzahl >= 15)
                {
                    FuegeEinsichtHinzu(new MetaEinsicht
                    {
                        typ = MetaEinsichtTyp.StrategieEffektiv,
                        beschreibung = $"'{stats.strategie}' funktioniert gut in '{stats.kontextCluster}' " +
                            $"(Erfolgsrate: {erfolgsRate:P0}, N={stats.anzahl})",
                        konfidenz = Math.Min(1f, stats.anzahl / 30f),
                        kontextCluster = stats.kontextCluster,
                        strategie = stats.strategie
                    });
                }

                // Strategie schlecht → Warnung
                if (erfolgsRate < 0.3f && stats.anzahl >= 15)
                {
                    FuegeEinsichtHinzu(new MetaEinsicht
                    {
                        typ = MetaEinsichtTyp.StrategieIneffektiv,
                        beschreibung = $"'{stats.strategie}' ist wenig erfolgreich in '{stats.kontextCluster}' " +
                            $"(Erfolgsrate: {erfolgsRate:P0}, N={stats.anzahl})",
                        konfidenz = Math.Min(1f, stats.anzahl / 30f),
                        kontextCluster = stats.kontextCluster,
                        strategie = stats.strategie
                    });
                }
            }
        }

        /// <summary>
        /// Stagniert das Lernen oder verbessert es sich noch?
        /// </summary>
        private void AnalysiereLernkurve()
        {
            if (lernKurve.belohnungen.Count < 20) return;

            // Vergleiche letzte 10 mit vorherigen 10
            var letzte = lernKurve.belohnungen.TakeLast(10).ToList();
            var vorherige = lernKurve.belohnungen.Skip(lernKurve.belohnungen.Count - 20).Take(10).ToList();

            float durchschnittLetzte = letzte.Average();
            float durchschnittVorherige = vorherige.Average();

            float delta = durchschnittLetzte - durchschnittVorherige;

            if (Math.Abs(delta) < 0.02f && lernKurve.belohnungen.Count > 50)
            {
                FuegeEinsichtHinzu(new MetaEinsicht
                {
                    typ = MetaEinsichtTyp.LernStagnation,
                    beschreibung = $"Lernkurve stagniert (Δ={delta:F3}). Ueberlegung: " +
                        "Mehr Exploration? Neuer Kontext? Kreative Strategie?",
                    konfidenz = 0.6f
                });
            }
            else if (delta > 0.05f)
            {
                FuegeEinsichtHinzu(new MetaEinsicht
                {
                    typ = MetaEinsichtTyp.LernFortschritt,
                    beschreibung = $"Positive Lernkurve (Δ=+{delta:F3}). Aktuelle Strategie funktioniert.",
                    konfidenz = 0.7f
                });
            }
            else if (delta < -0.05f)
            {
                FuegeEinsichtHinzu(new MetaEinsicht
                {
                    typ = MetaEinsichtTyp.LernRegression,
                    beschreibung = $"WARNUNG: Performance verschlechtert sich (Δ={delta:F3}). " +
                        "Moeglicherweise Umgebungsaenderung oder Ueberanpassung.",
                    konfidenz = 0.8f
                });
            }

            // RL-Zustandsraum waechst noch?
            if (lernKurve.rlZustaende.Count >= 20)
            {
                int letzteZustaende = lernKurve.rlZustaende.Last();
                int vorherigeZustaende = lernKurve.rlZustaende[lernKurve.rlZustaende.Count - 10];
                if (letzteZustaende == vorherigeZustaende && letzteZustaende > 0)
                {
                    FuegeEinsichtHinzu(new MetaEinsicht
                    {
                        typ = MetaEinsichtTyp.ExplorationErschoepft,
                        beschreibung = "RL-Zustandsraum waechst nicht mehr — alle erreichbaren Zustaende bekannt?",
                        konfidenz = 0.5f
                    });
                }
            }
        }

        /// <summary>
        /// Erkennt systematische Verzerrungen im Verhalten.
        /// </summary>
        private void ErkenneBias()
        {
            if (strategieHistorie.Count < 30) return;

            // Bias 1: Wird LLM zu oft/selten genutzt?
            var letzte30 = strategieHistorie.TakeLast(30).ToList();
            float llmRate = letzte30.Count(e => e.llmGenutzt) / (float)letzte30.Count;

            if (llmRate > 0.9f)
            {
                FuegeEinsichtHinzu(new MetaEinsicht
                {
                    typ = MetaEinsichtTyp.BiasErkannt,
                    beschreibung = $"LLM-Abhaengigkeit: {llmRate:P0} der letzten Entscheidungen brauchten LLM. " +
                        "RL sollte mehr Entscheidungen uebernehmen koennen.",
                    konfidenz = 0.7f
                });
            }

            // Bias 2: Werden bestimmte AktionsTypen systematisch bevorzugt?
            var strategieVerteilung = letzte30.GroupBy(e => e.strategie)
                .Select(g => new { strategie = g.Key, anteil = g.Count() / (float)letzte30.Count })
                .OrderByDescending(x => x.anteil).ToList();

            if (strategieVerteilung.Count > 0 && strategieVerteilung[0].anteil > 0.6f)
            {
                FuegeEinsichtHinzu(new MetaEinsicht
                {
                    typ = MetaEinsichtTyp.BiasErkannt,
                    beschreibung = $"Strategie-Bias: '{strategieVerteilung[0].strategie}' wird in " +
                        $"{strategieVerteilung[0].anteil:P0} aller Faelle gewaehlt.",
                    konfidenz = 0.6f
                });
            }

            // Bias 3: Kontexte die vermieden werden
            var kontextVerteilung = letzte30.GroupBy(e => e.kontextCluster)
                .ToDictionary(g => g.Key, g => g.Count());
            var moeglicheKontexte = new[] { "physik", "sozial", "existenziell", "epistemisch", "allgemein" };
            foreach (var k in moeglicheKontexte)
            {
                if (!kontextVerteilung.ContainsKey(k) && zyklusZaehler > 100)
                {
                    FuegeEinsichtHinzu(new MetaEinsicht
                    {
                        typ = MetaEinsichtTyp.BlindFleck,
                        beschreibung = $"Blinder Fleck: Kontext '{k}' wurde nie aktiv aufgesucht.",
                        konfidenz = 0.4f
                    });
                }
            }
        }

        // ======== API fuer andere Subsysteme ========

        /// <summary>
        /// Beste Strategie fuer einen bestimmten Kontext (aus Erfahrung, nicht LLM).
        /// </summary>
        public string EmpfiehlStrategie(string kontextCluster)
        {
            var kandidaten = strategieStats.Values
                .Where(s => s.kontextCluster == kontextCluster && s.anzahl >= MIN_STICHPROBE)
                .OrderByDescending(s => s.erfolge / (float)s.anzahl)
                .ThenByDescending(s => s.durchschnittBelohnung)
                .ToList();

            return kandidaten.Count > 0 ? kandidaten[0].strategie : null;
        }

        /// <summary>
        /// Gibt aktive Einsichten zurueck (fuer Prompt-Kontext oder Logging).
        /// </summary>
        public List<MetaEinsicht> GetAktuelleEinsichten(int n = 5)
        {
            return einsichten
                .OrderByDescending(e => e.konfidenz)
                .ThenByDescending(e => e.zyklusNummer)
                .Take(n).ToList();
        }

        /// <summary>
        /// Gibt die aktuelle Lernkurven-Tendenz zurueck.
        /// </summary>
        public string GetLernStatus()
        {
            if (lernKurve.belohnungen.Count < 10) return "Zu wenige Daten.";

            var letzte = lernKurve.belohnungen.TakeLast(10);
            float schnitt = letzte.Average();

            if (schnitt > 0.3f) return $"Gut (∅ Belohnung: {schnitt:F2})";
            if (schnitt > 0f) return $"Leicht positiv (∅ Belohnung: {schnitt:F2})";
            if (schnitt > -0.2f) return $"Stagnierend (∅ Belohnung: {schnitt:F2})";
            return $"Problematisch (∅ Belohnung: {schnitt:F2})";
        }

        /// <summary>
        /// Gibt die LLM-Abhaengigkeitsrate zurueck (0-1).
        /// </summary>
        public float GetLLMAbhaengigkeit()
        {
            if (strategieHistorie.Count < 10) return 1f; // Am Anfang: alles LLM
            var letzte = strategieHistorie.TakeLast(30).ToList();
            return letzte.Count(e => e.llmGenutzt) / (float)letzte.Count;
        }

        /// <summary>
        /// Pipeline-Empfehlung: Sollte Exploration erhoeht werden?
        /// </summary>
        public bool SollteExplorationErhoehen()
        {
            return einsichten.Any(e =>
                e.typ == MetaEinsichtTyp.LernStagnation ||
                e.typ == MetaEinsichtTyp.ExplorationErschoepft);
        }

        private void FuegeEinsichtHinzu(MetaEinsicht einsicht)
        {
            einsicht.zyklusNummer = zyklusZaehler;

            // Doppelte vermeiden (gleicher Typ + gleiche Strategie/Kontext)
            einsichten.RemoveAll(e =>
                e.typ == einsicht.typ &&
                e.strategie == einsicht.strategie &&
                e.kontextCluster == einsicht.kontextCluster);

            einsichten.Add(einsicht);

            // Maximal 30 Einsichten behalten
            while (einsichten.Count > 30)
                einsichten.RemoveAt(0);
        }

        public void Persistiere()
        {
            DatenLader.Speichere(PERSISTENZ_DATEI, new MetaKognitionZustand
            {
                strategieHistorie = strategieHistorie,
                strategieStats = strategieStats,
                einsichten = einsichten,
                lernKurve = lernKurve
            });
        }
    }

    // ======== Datenmodelle ========

    [Serializable]
    public class StrategieEpisode
    {
        public string strategie;
        public string kontextCluster;
        public float belohnung;
        public bool erfolg;
        public float rlKonfidenz;
        public bool llmGenutzt;
        public float zeitstempel;
        public int zyklusNummer;
    }

    [Serializable]
    public class StrategieStatistik
    {
        public string strategie;
        public string kontextCluster;
        public int anzahl;
        public int erfolge;
        public float durchschnittBelohnung;
    }

    [Serializable]
    public class MetaEinsicht
    {
        public MetaEinsichtTyp typ;
        public string beschreibung;
        public float konfidenz;
        public string kontextCluster;
        public string strategie;
        public int zyklusNummer;
    }

    [Serializable]
    public enum MetaEinsichtTyp
    {
        StrategieEffektiv,       // Eine Strategie funktioniert gut in einem Kontext
        StrategieIneffektiv,     // Eine Strategie funktioniert schlecht
        LernStagnation,          // Lernkurve flacht ab
        LernFortschritt,         // Lernkurve steigt
        LernRegression,          // Performance verschlechtert sich
        ExplorationErschoepft,   // Zustandsraum scheint vollstaendig erkundet
        BiasErkannt,             // Systematische Verzerrung im Verhalten
        BlindFleck               // Kontext oder Strategie wird systematisch vermieden
    }

    [Serializable]
    public class LernKurve
    {
        public List<float> belohnungen = new List<float>();
        public List<int> rlZustaende = new List<int>();
        public List<int> instanzenAnzahl = new List<int>();
    }

    [Serializable]
    public class MetaKognitionZustand
    {
        public List<StrategieEpisode> strategieHistorie;
        public Dictionary<string, StrategieStatistik> strategieStats;
        public List<MetaEinsicht> einsichten;
        public LernKurve lernKurve;
    }
}
