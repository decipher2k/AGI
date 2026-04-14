using System;
using System.Collections.Generic;
using System.Linq;
using BilligAGI.Modelle;
using BilligAGI.Intentionalitaet;
using BilligAGI.Daten;
using UnityEngine;

namespace BilligAGI.Kern
{
    // ============================================================
    //  SelbstCurriculum — Selbstgesteuertes Lernen
    //
    //  Das System identifiziert SELBST was es lernen muss
    //  und erstellt dafuer ein strukturiertes Curriculum:
    //
    //  1. Schwachstellen-Analyse: Wo bin ich schlecht?
    //     - SelbstModell: Niedrige Kompetenzen
    //     - MetaKognition: Ineffektive Strategien, Blinde Flecken
    //     - Kontrafaktische Analyse: Haeufig falsche Entscheidungen
    //
    //  2. Lernziel-Generierung: Was SOLL ich lernen?
    //     - Priorisiert nach Nutzen (wie oft relevant?) × Defizit
    //     - Zone der naechsten Entwicklung (nicht zu leicht, nicht zu schwer)
    //
    //  3. UebungsAufgaben: Konkrete Handlungen zum Verbessern
    //     - Generiert passend zur Schwaeche
    //     - Schwierigkeit adaptiv (steigend bei Erfolg)
    //
    //  4. Fortschritts-Tracking: Lerne ich wirklich?
    //     - Kompetenz-Deltas ueber Zeit
    //     - Stagnations-Erkennung → Strategie wechseln
    //
    //  Unterschied zu MetaZielSystem:
    //  - MetaZielSystem: "Es gibt was Interessantes → Ziel erstellen"
    //  - SelbstCurriculum: "Ich bin hier schwach → systematisch trainieren"
    // ============================================================

    [Serializable]
    public class LernZiel
    {
        public string id;
        public string domaene;                     // z.B. "physik", "navigation", "sozial"
        public string beschreibung;
        public float defizit;                      // 1 - kompetenz (0-1, hoeher = mehr Bedarf)
        public float relevanz;                     // Wie oft ist diese Domaene relevant (0-1)
        public float prioritaet;                   // defizit × relevanz
        public LernZielStatus status = LernZielStatus.OFFEN;
        public float startKompetenz;
        public float aktuelleKompetenz;
        public int uebungenAbsolviert;
        public int uebungenErfolgreich;
        public float schwierigkeit = 0.3f;         // Adaptive Schwierigkeit (0-1)
        public int erstelltInZyklus;
        public string quelle;                     // "kompetenz", "metakognition", "kontrafaktisch", "blindfleck"
    }

    [Serializable]
    public enum LernZielStatus { OFFEN, AKTIV, ABGESCHLOSSEN, PAUSIERT }

    [Serializable]
    public class UebungsAufgabe
    {
        public string lernZielId;
        public string beschreibung;
        public AktionsTyp empfohleneAktion;
        public float schwierigkeit;
        public string domaene;
    }

    [Serializable]
    public class CurriculumStatistik
    {
        public int lernZieleErstellt;
        public int lernZieleAbgeschlossen;
        public int uebungenGesamt;
        public int uebungenErfolgreich;
        public float durchschnittlicherKompetenzZuwachs;
        public int strategieWechsel;
        public Dictionary<string, float> kompetenzHistorie = new();  // Domaene → bester erreichter Wert
    }

    public class SelbstCurriculum
    {
        private readonly SelbstModell selbstModell;
        private readonly MetaKognition metaKognition;
        private readonly MentaleSimulation mentaleSim;
        private readonly ZielManager zielManager;
        private readonly AGIConfig config;

        private List<LernZiel> lernZiele = new();
        private LernZiel aktivesLernZiel;
        private CurriculumStatistik statistik;
        private int zyklusZaehler;
        private Dictionary<string, float> letzteKompetenzen = new();

        private const int ANALYSE_INTERVALL = 20;         // Alle N Zyklen Schwachstellen analysieren
        private const int MAX_LERNZIELE = 10;
        private const float MIN_DEFIZIT = 0.3f;           // Unter dieser Schwelle kein Lernziel
        private const float ABSCHLUSS_SCHWELLE = 0.15f;   // Kompetenz-Delta ab dem Lernziel "gelernt"
        private const float SCHWIERIGKEITS_STEP = 0.1f;   // Schwierigkeit steigt/sinkt
        private const float ZPD_UNTERGRENZE = 0.2f;       // Zone of Proximal Development
        private const float ZPD_OBERGRENZE = 0.7f;        // Nicht zu schwer
        private const int MAX_UEBUNGEN_PRO_ZIEL = 30;
        private const string PERSISTENZ_DATEI = "selbst_curriculum.json";

        // Relevanz-Gewichte: Wie oft ist diese Domaene typischerweise gefragt
        private static readonly Dictionary<string, float> DOMAENEN_RELEVANZ = new()
        {
            ["navigation"] = 0.9f,
            ["physik"] = 0.7f,
            ["greifen"] = 0.8f,
            ["interaktion"] = 0.7f,
            ["sozial"] = 0.6f,
            ["planung"] = 0.9f,
            ["kommunikation"] = 0.8f,
            ["werfen"] = 0.4f
        };

        // Welche AktionsTypen trainieren welche Domaene
        private static readonly Dictionary<string, AktionsTyp[]> DOMAENEN_AKTIONEN = new()
        {
            ["navigation"] = new[] { AktionsTyp.Bewegen, AktionsTyp.Drehen },
            ["physik"] = new[] { AktionsTyp.Werfen, AktionsTyp.Schieben, AktionsTyp.Ziehen },
            ["greifen"] = new[] { AktionsTyp.Greifen, AktionsTyp.Ablegen },
            ["interaktion"] = new[] { AktionsTyp.Interagieren, AktionsTyp.Oeffnen, AktionsTyp.Schliessen, AktionsTyp.Aktivieren },
            ["sozial"] = new[] { AktionsTyp.Sprechen, AktionsTyp.ZeigenAuf },
            ["planung"] = new[] { AktionsTyp.Beobachten, AktionsTyp.Warten },
            ["kommunikation"] = new[] { AktionsTyp.Sprechen, AktionsTyp.Hoeren },
            ["werfen"] = new[] { AktionsTyp.Werfen }
        };

        public SelbstCurriculum(
            SelbstModell selbstModell,
            MetaKognition metaKognition,
            MentaleSimulation mentaleSim,
            ZielManager zielManager,
            AGIConfig config)
        {
            this.selbstModell = selbstModell;
            this.metaKognition = metaKognition;
            this.mentaleSim = mentaleSim;
            this.zielManager = zielManager;
            this.config = config;

            var gespeichert = DatenLader.Lade<CurriculumPersistenz>(PERSISTENZ_DATEI);
            if (gespeichert != null)
            {
                lernZiele = gespeichert.lernZiele ?? new List<LernZiel>();
                statistik = gespeichert.statistik ?? new CurriculumStatistik();
                aktivesLernZiel = lernZiele.FirstOrDefault(z => z.status == LernZielStatus.AKTIV);
                Debug.Log($"[Curriculum] {statistik.lernZieleErstellt} Ziele, " +
                    $"{statistik.uebungenGesamt} Uebungen historisch.");
            }
            else
            {
                statistik = new CurriculumStatistik();
            }

            // Initiale Kompetenzen snapshot
            SpeichereKompetenzSnapshot();
        }

        // ======== 1. Periodische Schwachstellen-Analyse ========

        /// <summary>
        /// Haupttick: Periodisch Schwachstellen analysieren + aktives Lernziel verwalten.
        /// Gibt eine UebungsAufgabe zurueck wenn ein Training laeuft.
        /// </summary>
        public UebungsAufgabe ZyklusTick(float letzteBelohnung, bool letzteAktionErfolgreich)
        {
            zyklusZaehler++;

            // Aktives Lernziel: Uebung auswerten
            if (aktivesLernZiel != null)
            {
                AktualisiereAktivesZiel(letzteBelohnung, letzteAktionErfolgreich);
            }

            // Periodisch: Neue Schwachstellen analysieren
            if (zyklusZaehler % ANALYSE_INTERVALL == 0)
            {
                AnalysiereSchwachstellen();
                PruefeAbgeschlosseneZiele();
                SpeichereKompetenzSnapshot();
                Persistiere();
            }

            // Wenn kein aktives Ziel: Bestes waehlen
            if (aktivesLernZiel == null || aktivesLernZiel.status != LernZielStatus.AKTIV)
            {
                AktiviereBestesLernZiel();
            }

            // Uebung generieren
            if (aktivesLernZiel != null && aktivesLernZiel.status == LernZielStatus.AKTIV)
            {
                return GeneriereUebung(aktivesLernZiel);
            }

            return null;
        }

        // ======== 2. Schwachstellen-Analyse ========

        private void AnalysiereSchwachstellen()
        {
            // Quelle 1: Kompetenz-Defizite
            var kompetenzen = selbstModell?.GetAlleKompetenzen();
            if (kompetenzen != null)
            {
                foreach (var kvp in kompetenzen)
                {
                    float defizit = 1f - kvp.Value;
                    if (defizit < MIN_DEFIZIT) continue;

                    float relevanz = DOMAENEN_RELEVANZ.TryGetValue(kvp.Key, out float r) ? r : 0.5f;
                    float priority = defizit * relevanz;

                    ErstelleOderAktualisiereLernZiel(
                        kvp.Key,
                        $"Kompetenz '{kvp.Key}' verbessern (aktuell: {kvp.Value:F2})",
                        defizit, relevanz, "kompetenz");
                }
            }

            // Quelle 2: MetaKognition-Einsichten
            var einsichten = metaKognition?.GetAktuelleEinsichten(10);
            if (einsichten != null)
            {
                foreach (var e in einsichten)
                {
                    if (e.typ == MetaEinsichtTyp.StrategieIneffektiv &&
                        !string.IsNullOrEmpty(e.kontextCluster))
                    {
                        ErstelleOderAktualisiereLernZiel(
                            e.kontextCluster,
                            $"Strategie fuer '{e.kontextCluster}' verbessern: {e.strategie} ist ineffektiv",
                            0.6f, 0.7f, "metakognition");
                    }

                    if (e.typ == MetaEinsichtTyp.BlindFleck &&
                        !string.IsNullOrEmpty(e.kontextCluster))
                    {
                        ErstelleOderAktualisiereLernZiel(
                            e.kontextCluster,
                            $"Blinden Fleck '{e.kontextCluster}' explorieren",
                            0.7f, 0.5f, "blindfleck");
                    }

                    if (e.typ == MetaEinsichtTyp.LernStagnation)
                    {
                        // Stagnation → Strategie wechseln statt neues Ziel
                        if (aktivesLernZiel != null)
                        {
                            aktivesLernZiel.schwierigkeit = Mathf.Max(
                                0.1f, aktivesLernZiel.schwierigkeit - SCHWIERIGKEITS_STEP);
                            statistik.strategieWechsel++;
                        }
                    }
                }
            }

            // Quelle 3: Kontrafaktische Analyse — wo mache ich oft Fehler?
            if (mentaleSim != null)
            {
                var historie = mentaleSim.GetKontrafaktischeHistorie();
                if (historie.Count >= 5)
                {
                    // Gruppiere nach Aktionstyp: Welche Aktion wurde haeufig suboptimal gewaehlt?
                    var fehler = historie.Where(k => k.differenz > 0.1f)
                        .GroupBy(k => k.tatsaechlicheAktion)
                        .OrderByDescending(g => g.Count())
                        .FirstOrDefault();

                    if (fehler != null && fehler.Count() >= 3)
                    {
                        string domaene = AktionZuDomaene(fehler.Key);
                        ErstelleOderAktualisiereLernZiel(
                            domaene,
                            $"'{fehler.Key}' oft suboptimal — kontrafaktisch besser moeglich " +
                            $"(∅Δ={fehler.Average(k => k.differenz):F2})",
                            0.5f, 0.8f, "kontrafaktisch");
                    }
                }
            }

            // Aufraumen: Zu viele Lernziele?
            while (lernZiele.Count(z => z.status == LernZielStatus.OFFEN) > MAX_LERNZIELE)
            {
                var schwaecstes = lernZiele
                    .Where(z => z.status == LernZielStatus.OFFEN)
                    .OrderBy(z => z.prioritaet)
                    .First();
                lernZiele.Remove(schwaecstes);
            }
        }

        private void ErstelleOderAktualisiereLernZiel(
            string domaene, string beschreibung, float defizit, float relevanz, string quelle)
        {
            // Existiert bereits ein offenes Lernziel fuer diese Domaene?
            var bestehendes = lernZiele.FirstOrDefault(z =>
                z.domaene == domaene &&
                (z.status == LernZielStatus.OFFEN || z.status == LernZielStatus.AKTIV));

            if (bestehendes != null)
            {
                // Prioritaet aktualisieren
                bestehendes.defizit = Mathf.Max(bestehendes.defizit, defizit);
                bestehendes.relevanz = Mathf.Max(bestehendes.relevanz, relevanz);
                bestehendes.prioritaet = bestehendes.defizit * bestehendes.relevanz;
                return;
            }

            // Neues Lernziel
            var ziel = new LernZiel
            {
                id = Guid.NewGuid().ToString("N").Substring(0, 8),
                domaene = domaene,
                beschreibung = beschreibung,
                defizit = defizit,
                relevanz = relevanz,
                prioritaet = defizit * relevanz,
                startKompetenz = selbstModell?.GetKompetenz(domaene) ?? 0.1f,
                aktuelleKompetenz = selbstModell?.GetKompetenz(domaene) ?? 0.1f,
                erstelltInZyklus = zyklusZaehler,
                quelle = quelle,
                schwierigkeit = BerechneStartSchwierigkeit(domaene)
            };

            lernZiele.Add(ziel);
            statistik.lernZieleErstellt++;

            Debug.Log($"[Curriculum] Neues Lernziel: '{domaene}' — {beschreibung} " +
                $"(Prioritaet: {ziel.prioritaet:F2}, Quelle: {quelle})");
        }

        // ======== 3. Aktives Lernziel verwalten ========

        private void AktiviereBestesLernZiel()
        {
            var kandidat = lernZiele
                .Where(z => z.status == LernZielStatus.OFFEN)
                .OrderByDescending(z => z.prioritaet)
                .FirstOrDefault();

            if (kandidat == null) return;

            // Zone der naechsten Entwicklung pruefen
            float kompetenz = selbstModell?.GetKompetenz(kandidat.domaene) ?? 0.1f;
            if (kompetenz > ZPD_OBERGRENZE)
            {
                // Schon ziemlich gut → niedrigere Prioritaet
                kandidat.prioritaet *= 0.5f;
                return;
            }

            kandidat.status = LernZielStatus.AKTIV;
            aktivesLernZiel = kandidat;

            // Ziel auch dem ZielManager als Trainings-Ziel melden
            if (zielManager != null)
            {
                var freieSlots = 3 - (zielManager.GetAlleAktiven()?.Count ?? 0);
                if (freieSlots > 0)
                {
                    zielManager.FormuliereZiel(
                        $"[Curriculum] {kandidat.beschreibung}",
                        kandidat.domaene == "physik" || kandidat.domaene == "werfen"
                            ? ZielTyp.EXPERIMENT
                            : kandidat.domaene == "sozial" || kandidat.domaene == "kommunikation"
                                ? ZielTyp.SOZIAL
                                : ZielTyp.EXPLORATION,
                        kandidat.prioritaet * 0.7f);  // Etwas niedrigere Prio als echte User-Ziele
                }
            }

            Debug.Log($"[Curriculum] Lernziel aktiviert: '{kandidat.domaene}' " +
                $"(Defizit: {kandidat.defizit:F2}, Schwierigkeit: {kandidat.schwierigkeit:F2})");
        }

        private void AktualisiereAktivesZiel(float belohnung, bool erfolg)
        {
            if (aktivesLernZiel == null) return;

            aktivesLernZiel.uebungenAbsolviert++;
            statistik.uebungenGesamt++;

            if (erfolg)
            {
                aktivesLernZiel.uebungenErfolgreich++;
                statistik.uebungenErfolgreich++;
            }

            // Kompetenz aktualisieren
            aktivesLernZiel.aktuelleKompetenz =
                selbstModell?.GetKompetenz(aktivesLernZiel.domaene) ?? aktivesLernZiel.aktuelleKompetenz;

            // Schwierigkeit adaptiv anpassen
            float erfolgsRate = aktivesLernZiel.uebungenAbsolviert > 0
                ? aktivesLernZiel.uebungenErfolgreich / (float)aktivesLernZiel.uebungenAbsolviert
                : 0f;

            if (erfolgsRate > 0.7f && aktivesLernZiel.uebungenAbsolviert >= 5)
            {
                // Zu leicht → schwerer machen
                aktivesLernZiel.schwierigkeit = Mathf.Min(
                    1f, aktivesLernZiel.schwierigkeit + SCHWIERIGKEITS_STEP);
            }
            else if (erfolgsRate < 0.3f && aktivesLernZiel.uebungenAbsolviert >= 5)
            {
                // Zu schwer → leichter machen
                aktivesLernZiel.schwierigkeit = Mathf.Max(
                    0.1f, aktivesLernZiel.schwierigkeit - SCHWIERIGKEITS_STEP);
            }

            // Max-Uebungen erreicht?
            if (aktivesLernZiel.uebungenAbsolviert >= MAX_UEBUNGEN_PRO_ZIEL)
            {
                float delta = aktivesLernZiel.aktuelleKompetenz - aktivesLernZiel.startKompetenz;
                if (delta > ABSCHLUSS_SCHWELLE)
                {
                    aktivesLernZiel.status = LernZielStatus.ABGESCHLOSSEN;
                    statistik.lernZieleAbgeschlossen++;
                    TrackKompetenzZuwachs(aktivesLernZiel.domaene, delta);
                    Debug.Log($"[Curriculum] Lernziel '{aktivesLernZiel.domaene}' abgeschlossen! " +
                        $"(+{delta:F2} Kompetenz)");
                }
                else
                {
                    // Nicht genug gelernt → pausieren
                    aktivesLernZiel.status = LernZielStatus.PAUSIERT;
                    Debug.Log($"[Curriculum] Lernziel '{aktivesLernZiel.domaene}' pausiert " +
                        $"(nur +{delta:F2} Kompetenz nach {MAX_UEBUNGEN_PRO_ZIEL} Uebungen)");
                }
                aktivesLernZiel = null;
            }
        }

        private void PruefeAbgeschlosseneZiele()
        {
            if (aktivesLernZiel == null) return;

            float delta = aktivesLernZiel.aktuelleKompetenz - aktivesLernZiel.startKompetenz;
            if (delta >= ABSCHLUSS_SCHWELLE)
            {
                aktivesLernZiel.status = LernZielStatus.ABGESCHLOSSEN;
                statistik.lernZieleAbgeschlossen++;
                TrackKompetenzZuwachs(aktivesLernZiel.domaene, delta);
                Debug.Log($"[Curriculum] Lernziel '{aktivesLernZiel.domaene}' " +
                    $"vorzeitig abgeschlossen! (+{delta:F2})");
                aktivesLernZiel = null;
            }
        }

        // ======== 4. Uebungs-Generierung ========

        /// <summary>
        /// Generiert eine passende Uebungsaufgabe fuer das aktive Lernziel.
        /// Waehlt AktionsTyp basierend auf Domaene und Schwierigkeit.
        /// </summary>
        private UebungsAufgabe GeneriereUebung(LernZiel ziel)
        {
            AktionsTyp[] moeglicheAktionen = DOMAENEN_AKTIONEN.TryGetValue(ziel.domaene, out var aktionen)
                ? aktionen
                : new[] { AktionsTyp.Beobachten };

            // Waehle Aktion basierend auf Schwierigkeit
            // Niedrige Schwierigkeit → einfachere Aktionen (am Anfang der Liste)
            // Hohe Schwierigkeit → schwierigere Aktionen (am Ende)
            int index = Mathf.FloorToInt(ziel.schwierigkeit * (moeglicheAktionen.Length - 1));
            index = Mathf.Clamp(index, 0, moeglicheAktionen.Length - 1);
            AktionsTyp aktionsTyp = moeglicheAktionen[index];

            string beschreibung = GeneriereUebungsBeschreibung(ziel.domaene, aktionsTyp, ziel.schwierigkeit);

            return new UebungsAufgabe
            {
                lernZielId = ziel.id,
                beschreibung = beschreibung,
                empfohleneAktion = aktionsTyp,
                schwierigkeit = ziel.schwierigkeit,
                domaene = ziel.domaene
            };
        }

        private string GeneriereUebungsBeschreibung(string domaene, AktionsTyp aktion, float schwierigkeit)
        {
            string level = schwierigkeit switch
            {
                < 0.3f => "einfach",
                < 0.6f => "mittel",
                < 0.8f => "fortgeschritten",
                _ => "schwer"
            };

            return domaene switch
            {
                "navigation" => $"[{level}] Navigiere zu einem {(schwierigkeit > 0.5f ? "entfernten" : "nahen")} Punkt",
                "physik" => $"[{level}] Teste eine physikalische Interaktion: {aktion}",
                "greifen" => $"[{level}] Greife {(schwierigkeit > 0.5f ? "einen kleinen" : "einen")} Gegenstand und lege ihn ab",
                "interaktion" => $"[{level}] Interagiere mit einem Objekt via {aktion}",
                "sozial" => $"[{level}] Fuehre eine soziale Interaktion durch: {aktion}",
                "planung" => $"[{level}] Plane eine {(schwierigkeit > 0.5f ? "mehrstufige" : "zweiteilige")} Aktion",
                "kommunikation" => $"[{level}] Kommuniziere mit einem NPC: {aktion}",
                "werfen" => $"[{level}] Wirf einen Gegenstand {(schwierigkeit > 0.5f ? "auf ein Ziel" : "in eine Richtung")}",
                _ => $"[{level}] Uebe '{domaene}' mit Aktion: {aktion}"
            };
        }

        // ======== Hilfsmethoden ========

        private float BerechneStartSchwierigkeit(string domaene)
        {
            float kompetenz = selbstModell?.GetKompetenz(domaene) ?? 0.1f;
            // Start-Schwierigkeit leicht ueber aktuellem Koennen (ZPD)
            return Mathf.Clamp(kompetenz + 0.1f, ZPD_UNTERGRENZE, ZPD_OBERGRENZE);
        }

        private string AktionZuDomaene(AktionsTyp aktion)
        {
            foreach (var kvp in DOMAENEN_AKTIONEN)
            {
                if (kvp.Value.Contains(aktion)) return kvp.Key;
            }
            return "allgemein";
        }

        private void SpeichereKompetenzSnapshot()
        {
            var aktuelle = selbstModell?.GetAlleKompetenzen();
            if (aktuelle != null)
                letzteKompetenzen = new Dictionary<string, float>(aktuelle);
        }

        private void TrackKompetenzZuwachs(string domaene, float delta)
        {
            float bisherig = statistik.durchschnittlicherKompetenzZuwachs;
            int n = statistik.lernZieleAbgeschlossen;
            statistik.durchschnittlicherKompetenzZuwachs = (bisherig * (n - 1) + delta) / n;

            // Besten Wert speichern
            float aktuell = selbstModell?.GetKompetenz(domaene) ?? 0f;
            if (!statistik.kompetenzHistorie.ContainsKey(domaene) ||
                statistik.kompetenzHistorie[domaene] < aktuell)
            {
                statistik.kompetenzHistorie[domaene] = aktuell;
            }
        }

        // ======== API ========

        public LernZiel GetAktivesLernZiel() => aktivesLernZiel;

        public List<LernZiel> GetAlleLernZiele() => new List<LernZiel>(lernZiele);

        public List<LernZiel> GetOffeneLernZiele() =>
            lernZiele.Where(z => z.status == LernZielStatus.OFFEN)
                .OrderByDescending(z => z.prioritaet).ToList();

        public CurriculumStatistik GetStatistik() => statistik;

        public string GetStatusText()
        {
            var aktiv = aktivesLernZiel;
            int offene = lernZiele.Count(z => z.status == LernZielStatus.OFFEN);
            int abgeschlossen = lernZiele.Count(z => z.status == LernZielStatus.ABGESCHLOSSEN);

            string aktivText = aktiv != null
                ? $"Aktuell: '{aktiv.domaene}' — {aktiv.beschreibung}\n" +
                  $"  Fortschritt: {aktiv.uebungenAbsolviert} Uebungen " +
                  $"({aktiv.uebungenErfolgreich} erfolgreich), " +
                  $"Schwierigkeit: {aktiv.schwierigkeit:F1}, " +
                  $"Kompetenz: {aktiv.startKompetenz:F2} → {aktiv.aktuelleKompetenz:F2}"
                : "Kein aktives Lernziel.";

            return $"{aktivText}\n" +
                $"  Offene Ziele: {offene}, Abgeschlossen: {abgeschlossen}\n" +
                $"  Gesamt: {statistik.uebungenGesamt} Uebungen " +
                $"({statistik.uebungenErfolgreich} erfolgreich), " +
                $"∅ Kompetenz-Zuwachs: {statistik.durchschnittlicherKompetenzZuwachs:F3}";
        }

        public string GetLernZieleText()
        {
            if (lernZiele.Count == 0) return "Keine Lernziele.";

            var zeilen = new List<string>();
            foreach (var z in lernZiele.OrderByDescending(z => z.prioritaet).Take(10))
            {
                string statusSymbol = z.status switch
                {
                    LernZielStatus.AKTIV => "→",
                    LernZielStatus.ABGESCHLOSSEN => "✓",
                    LernZielStatus.PAUSIERT => "⏸",
                    _ => "○"
                };
                zeilen.Add($"  {statusSymbol} [{z.domaene}] {z.beschreibung} " +
                    $"(Prio: {z.prioritaet:F2}, Quelle: {z.quelle})");
            }
            return string.Join("\n", zeilen);
        }

        // ======== Persistenz ========

        public void Persistiere()
        {
            DatenLader.Speichere(PERSISTENZ_DATEI, new CurriculumPersistenz
            {
                lernZiele = lernZiele,
                statistik = statistik
            });
        }

        [Serializable]
        private class CurriculumPersistenz
        {
            public List<LernZiel> lernZiele;
            public CurriculumStatistik statistik;
        }
    }
}
