using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BilligAGI.Modelle;
using BilligAGI.Intentionalitaet;
using BilligAGI.Daten;
using UnityEngine;

namespace BilligAGI.Kern
{
    // ============================================================
    //  LangzeitPlaner — Hierarchische Langzeit-Planung
    //
    //  Problem mit bestehendem Planer:
    //  - Erstellt nur flache Aktionslisten (1 Ebene)
    //  - Kein Horizont ueber mehrere Zyklen hinweg
    //  - Keine Vorab-Bewertung via MentaleSimulation
    //  - Keine Meilensteine oder Zwischenziele
    //
    //  LangzeitPlaner ergaenzt dies:
    //  1. Ziel → Teilziel-Hierarchie (beliebig tief)
    //  2. Jeder Teilplan wird mental vorab simuliert
    //  3. Meilensteine mit Erfolgskriterien
    //  4. Adaptive Umplanung wenn Meilenstein scheitert
    //  5. Fortschritts-Monitoring ueber Zyklen hinweg
    //  6. Feedback-Loop: Kontrafaktische Analyse → Plan-Revision
    //
    //  Arbeitet MIT dem bestehenden Planer zusammen:
    //  LangzeitPlaner zerlegt → Planer erstellt Detailplaene
    // ============================================================

    [Serializable]
    public class LangzeitPlan
    {
        public string id;
        public string zielId;
        public string beschreibung;
        public List<Meilenstein> meilensteine = new();
        public int aktuellerMeilenstein;
        public float gesamtFortschritt;           // 0-1
        public float erwarteteBelohnung;
        public float simulationsKonfidenz;
        public int umplanungen;
        public int erstelltInZyklus;
        public string erstelltAm;
        public LangzeitPlanStatus status = LangzeitPlanStatus.AKTIV;
    }

    [Serializable]
    public enum LangzeitPlanStatus { AKTIV, PAUSIERT, ABGESCHLOSSEN, GESCHEITERT }

    [Serializable]
    public class Meilenstein
    {
        public int nummer;
        public string beschreibung;
        public string erfolgsBedingung;           // Natuerlichsprachlich
        public ZielTyp typ;
        public float geschaetzteDauer;            // Zyklen
        public float fortschritt;                 // 0-1
        public MeilensteinStatus status = MeilensteinStatus.OFFEN;
        public float simulierteBelohnung;
        public float tatsaechlicheBelohnung;
        public int startZyklus;
        public int abschlussZyklus;
    }

    [Serializable]
    public enum MeilensteinStatus { OFFEN, AKTIV, ABGESCHLOSSEN, GESCHEITERT, UEBERSPRUNGEN }

    [Serializable]
    public class LangzeitPlanStatistik
    {
        public int plaeneErstellt;
        public int plaeneAbgeschlossen;
        public int plaeneGescheitert;
        public int gesamtUmplanungen;
        public int meilensteineErreicht;
        public int meilensteineGescheitert;
        public float durchschnittlicheGenauigkeit;   // Simulation vs. Realitaet
        public int vorabSimulationenGespart;          // Plaene die verworfen wurden weil Simulation schlecht
    }

    public class LangzeitPlaner
    {
        private readonly Planer planer;
        private readonly MentaleSimulation mentaleSim;
        private readonly ZielManager zielManager;
        private readonly SelbstModell selbstModell;
        private readonly MetaKognition metaKognition;
        private readonly LLMAdapter llm;
        private readonly AGIConfig config;

        private LangzeitPlan aktiverPlan;
        private List<LangzeitPlan> planHistorie = new();
        private LangzeitPlanStatistik statistik;
        private int zyklusZaehler;

        private const int MAX_MEILENSTEINE = 8;          // Max Teilziele pro Plan
        private const int MAX_HISTORIE = 30;
        private const float MIN_SIMULATIONS_KONFIDENZ = 0.2f;
        private const float UMPLANUNGS_SCHWELLE = -0.1f;  // Meilenstein-Belohnung unter diesem Wert → umplanen
        private const int MAX_UMPLANUNGEN = 3;
        private const int FORTSCHRITTS_CHECK_INTERVALL = 5;
        private const string PERSISTENZ_DATEI = "langzeit_planer.json";

        public LangzeitPlaner(
            Planer planer,
            MentaleSimulation mentaleSim,
            ZielManager zielManager,
            SelbstModell selbstModell,
            MetaKognition metaKognition,
            LLMAdapter llm,
            AGIConfig config)
        {
            this.planer = planer;
            this.mentaleSim = mentaleSim;
            this.zielManager = zielManager;
            this.selbstModell = selbstModell;
            this.metaKognition = metaKognition;
            this.llm = llm;
            this.config = config;

            var gespeichert = DatenLader.Lade<LangzeitPlanPersistenz>(PERSISTENZ_DATEI);
            if (gespeichert != null)
            {
                statistik = gespeichert.statistik ?? new LangzeitPlanStatistik();
                planHistorie = gespeichert.historie ?? new List<LangzeitPlan>();
                Debug.Log($"[LangzeitPlaner] {statistik.plaeneErstellt} Plaene, " +
                    $"{statistik.meilensteineErreicht} Meilensteine historisch.");
            }
            else
            {
                statistik = new LangzeitPlanStatistik();
            }
        }

        // ======== 1. Langzeit-Plan erstellen ========

        /// <summary>
        /// Zerlegt ein Ziel in eine Meilenstein-Hierarchie.
        /// Jeder Meilenstein wird mental vorab simuliert.
        /// Gibt null zurueck wenn Simulation zeigt dass Plan aussichtslos.
        /// </summary>
        public async Task<LangzeitPlan> ErstelleLangzeitPlan(
            Ziel ziel, WeltZustand welt, float[] aktuellerZustand)
        {
            if (ziel == null || llm == null) return null;

            // Schritt 1: LLM zerlegt Ziel in Teilziele/Meilensteine
            var meilensteine = await ZerlegeInMeilensteine(ziel, welt);
            if (meilensteine == null || meilensteine.Count == 0)
            {
                Debug.LogWarning("[LangzeitPlaner] Konnte Ziel nicht zerlegen.");
                return null;
            }

            // Schritt 2: Jeden Meilenstein mental simulieren (wenn moeglich)
            float gesamtErwarteteBelohnung = 0f;
            float minKonfidenz = 1f;

            if (mentaleSim != null && aktuellerZustand != null)
            {
                foreach (var ms in meilensteine)
                {
                    // Erster Meilenstein: Von aktuellem Zustand simulieren
                    var pfad = mentaleSim.FindeBesteSequenz(aktuellerZustand, 4);
                    if (pfad != null)
                    {
                        ms.simulierteBelohnung = pfad.kumulativeBelohnung;
                        gesamtErwarteteBelohnung += pfad.kumulativeBelohnung;
                        minKonfidenz = Mathf.Min(minKonfidenz, pfad.konfidenz);
                    }
                }
            }

            // Schritt 3: Kompetenz-Check — kann ich das ueberhaupt?
            float kompetenz = selbstModell?.GetKompetenz(
                ZielTypZuDomaene(ziel.typ)) ?? 0.1f;
            bool kompetenzReicht = kompetenz >= 0.2f || meilensteine.Count <= 2;

            // Schritt 4: Plan verwerfen wenn offensichtlich aussichtslos
            if (minKonfidenz < MIN_SIMULATIONS_KONFIDENZ &&
                gesamtErwarteteBelohnung < UMPLANUNGS_SCHWELLE &&
                !kompetenzReicht)
            {
                statistik.vorabSimulationenGespart++;
                Debug.Log($"[LangzeitPlaner] Plan verworfen — Simulation zu schlecht " +
                    $"(Belohnung={gesamtErwarteteBelohnung:F2}, Konfidenz={minKonfidenz:F2})");
                return null;
            }

            var plan = new LangzeitPlan
            {
                id = Guid.NewGuid().ToString("N").Substring(0, 8),
                zielId = ziel.id,
                beschreibung = ziel.beschreibung,
                meilensteine = meilensteine,
                aktuellerMeilenstein = 0,
                erwarteteBelohnung = gesamtErwarteteBelohnung,
                simulationsKonfidenz = minKonfidenz,
                erstelltInZyklus = zyklusZaehler,
                erstelltAm = DateTime.UtcNow.ToString("o")
            };

            aktiverPlan = plan;
            statistik.plaeneErstellt++;
            Persistiere();

            Debug.Log($"[LangzeitPlaner] Neuer Plan: '{ziel.beschreibung}' → " +
                $"{meilensteine.Count} Meilensteine, erwartet: {gesamtErwarteteBelohnung:F2}");

            return plan;
        }

        // ======== 2. Zyklus-Tick: Fortschritt pruefen ========

        /// <summary>
        /// Wird jeden Zyklus aufgerufen. Prueft Fortschritt, triggert Umplanung.
        /// </summary>
        public async Task<string> ZyklusTick(float belohnung, float[] aktuellerZustand)
        {
            zyklusZaehler++;
            if (aktiverPlan == null || aktiverPlan.status != LangzeitPlanStatus.AKTIV)
                return null;

            var ms = GetAktuellerMeilenstein();
            if (ms == null) return null;

            // Meilenstein als aktiv markieren
            if (ms.status == MeilensteinStatus.OFFEN)
            {
                ms.status = MeilensteinStatus.AKTIV;
                ms.startZyklus = zyklusZaehler;
            }

            // Belohnung akkumulieren
            ms.tatsaechlicheBelohnung += belohnung;

            // Periodischer Fortschritts-Check
            if (zyklusZaehler % FORTSCHRITTS_CHECK_INTERVALL == 0)
            {
                return await PruefeFortschritt(ms, aktuellerZustand, belohnung);
            }

            return null;
        }

        // ======== 3. Meilenstein abschliessen ========

        /// <summary>
        /// Markiert aktuellen Meilenstein als abgeschlossen und geht zum naechsten.
        /// </summary>
        public string MeilensteinAbschliessen(bool erfolg, string ergebnis)
        {
            if (aktiverPlan == null) return "Kein aktiver Plan.";

            var ms = GetAktuellerMeilenstein();
            if (ms == null) return "Kein aktiver Meilenstein.";

            ms.abschlussZyklus = zyklusZaehler;

            if (erfolg)
            {
                ms.status = MeilensteinStatus.ABGESCHLOSSEN;
                ms.fortschritt = 1f;
                statistik.meilensteineErreicht++;

                // Simulationsgenauigkeit tracken
                if (ms.simulierteBelohnung != 0f)
                {
                    float genauigkeit = 1f - Mathf.Abs(
                        ms.simulierteBelohnung - ms.tatsaechlicheBelohnung) /
                        Mathf.Max(1f, Mathf.Abs(ms.simulierteBelohnung));
                    genauigkeit = Mathf.Clamp01(genauigkeit);
                    statistik.durchschnittlicheGenauigkeit =
                        (statistik.durchschnittlicheGenauigkeit *
                        (statistik.meilensteineErreicht - 1) + genauigkeit) /
                        statistik.meilensteineErreicht;
                }
            }
            else
            {
                ms.status = MeilensteinStatus.GESCHEITERT;
                statistik.meilensteineGescheitert++;
            }

            // Naechster Meilenstein?
            aktiverPlan.aktuellerMeilenstein++;
            AktualisiereFortschritt();

            if (aktiverPlan.aktuellerMeilenstein >= aktiverPlan.meilensteine.Count)
            {
                // Plan abgeschlossen
                bool planErfolg = aktiverPlan.meilensteine.Count(m =>
                    m.status == MeilensteinStatus.ABGESCHLOSSEN) >
                    aktiverPlan.meilensteine.Count / 2;

                if (planErfolg)
                {
                    aktiverPlan.status = LangzeitPlanStatus.ABGESCHLOSSEN;
                    statistik.plaeneAbgeschlossen++;
                    var result = $"Langzeit-Plan '{aktiverPlan.beschreibung}' abgeschlossen! " +
                        $"({aktiverPlan.meilensteine.Count(m => m.status == MeilensteinStatus.ABGESCHLOSSEN)}/" +
                        $"{aktiverPlan.meilensteine.Count} Meilensteine)";
                    ArchiviereAktivenPlan();
                    return result;
                }
                else
                {
                    aktiverPlan.status = LangzeitPlanStatus.GESCHEITERT;
                    statistik.plaeneGescheitert++;
                    var result = $"Langzeit-Plan '{aktiverPlan.beschreibung}' gescheitert.";
                    ArchiviereAktivenPlan();
                    return result;
                }
            }

            Persistiere();
            var naechster = GetAktuellerMeilenstein();
            return $"Meilenstein {ms.nummer} {(erfolg ? "✓" : "✗")}. " +
                $"Naechster: {naechster?.beschreibung ?? "keiner"}";
        }

        // ======== 4. Adaptive Umplanung ========

        /// <summary>
        /// Prueft ob der aktuelle Plan noch funktioniert.
        /// Triggert Umplanung bei schlechtem Fortschritt.
        /// </summary>
        private async Task<string> PruefeFortschritt(
            Meilenstein ms, float[] aktuellerZustand, float letzteBelohnung)
        {
            int zyklenImMeilenstein = zyklusZaehler - ms.startZyklus;
            if (zyklenImMeilenstein < 3) return null; // Zu frueh fuer Bewertung

            // Durchschnittliche Belohnung pro Zyklus
            float durchschnitt = ms.tatsaechlicheBelohnung / zyklenImMeilenstein;

            // Meta-Kognition: Aktuelle Strategie funktioniert?
            bool strategieProblematisch = metaKognition?.SollteExplorationErhoehen() ?? false;

            // Umplanung noetig?
            bool umplanen = false;
            string grund = "";

            if (durchschnitt < UMPLANUNGS_SCHWELLE && zyklenImMeilenstein > 5)
            {
                umplanen = true;
                grund = $"Negative Durchschnittsbelohnung ({durchschnitt:F2}) " +
                    $"ueber {zyklenImMeilenstein} Zyklen";
            }
            else if (zyklenImMeilenstein > ms.geschaetzteDauer * 2 && ms.fortschritt < 0.3f)
            {
                umplanen = true;
                grund = $"Timeout: {zyklenImMeilenstein} Zyklen bei nur {ms.fortschritt:P0} Fortschritt";
            }
            else if (strategieProblematisch && durchschnitt < 0.05f)
            {
                umplanen = true;
                grund = "Meta-Kognition meldet Stagnation + schwacher Fortschritt";
            }

            if (umplanen && aktiverPlan.umplanungen < MAX_UMPLANUNGEN)
            {
                return await Umplanen(grund, aktuellerZustand);
            }
            else if (umplanen)
            {
                // Zu viele Umplanungen — Plan aufgeben
                ms.status = MeilensteinStatus.GESCHEITERT;
                return $"Meilenstein '{ms.beschreibung}' nach {MAX_UMPLANUNGEN} " +
                    $"Umplanungen aufgegeben.";
            }

            return null;
        }

        /// <summary>
        /// Passt den Plan an: Aktuellen Meilenstein umformulieren oder ueberspringen.
        /// </summary>
        private async Task<string> Umplanen(string grund, float[] aktuellerZustand)
        {
            if (aktiverPlan == null || llm == null) return null;

            aktiverPlan.umplanungen++;
            statistik.gesamtUmplanungen++;

            var ms = GetAktuellerMeilenstein();
            if (ms == null) return null;

            // Strategie: LLM fragt ob Meilenstein umformulieren oder ueberspringen
            string bisherigeMeilensteine = string.Join("\n",
                aktiverPlan.meilensteine.Select(m =>
                    $"  {m.nummer}. [{m.status}] {m.beschreibung}"));

            string prompt = $"Langzeit-Plan: '{aktiverPlan.beschreibung}'\n" +
                $"Meilensteine:\n{bisherigeMeilensteine}\n\n" +
                $"Problem beim Meilenstein {ms.nummer}: {grund}\n" +
                $"Kompetenz-Info: {selbstModell?.GetSelbstbeschreibung() ?? "unbekannt"}\n\n" +
                $"Antwort als JSON: {{\"aktion\": \"umformulieren|ueberspringen|aufteilen\", " +
                $"\"neuerText\": \"...\", \"begruendung\": \"...\"}}";

            var antwort = await llm.FreieAnfrage(prompt);
            if (antwort != null)
            {
                try
                {
                    var revision = Newtonsoft.Json.JsonConvert.DeserializeObject<PlanRevision>(antwort.inhalt);
                    if (revision != null)
                    {
                        if (revision.aktion == "ueberspringen")
                        {
                            ms.status = MeilensteinStatus.UEBERSPRUNGEN;
                            aktiverPlan.aktuellerMeilenstein++;
                            AktualisiereFortschritt();
                            Persistiere();
                            return $"Meilenstein '{ms.beschreibung}' uebersprungen: {revision.begruendung}";
                        }
                        else if (revision.aktion == "umformulieren" && !string.IsNullOrEmpty(revision.neuerText))
                        {
                            string alter = ms.beschreibung;
                            ms.beschreibung = revision.neuerText;
                            ms.status = MeilensteinStatus.OFFEN;
                            ms.tatsaechlicheBelohnung = 0f;
                            ms.startZyklus = zyklusZaehler;
                            Persistiere();
                            return $"Meilenstein umformuliert: '{alter}' → '{revision.neuerText}'";
                        }
                        else if (revision.aktion == "aufteilen" && !string.IsNullOrEmpty(revision.neuerText))
                        {
                            // Meilenstein in zwei aufteilen
                            ms.beschreibung = revision.neuerText;
                            ms.status = MeilensteinStatus.OFFEN;
                            ms.tatsaechlicheBelohnung = 0f;
                            ms.startZyklus = zyklusZaehler;

                            // Zweiten Meilenstein einfuegen
                            if (aktiverPlan.meilensteine.Count < MAX_MEILENSTEINE)
                            {
                                var neuerMs = new Meilenstein
                                {
                                    nummer = ms.nummer + 1,
                                    beschreibung = revision.begruendung,
                                    typ = ms.typ,
                                    geschaetzteDauer = ms.geschaetzteDauer / 2
                                };
                                aktiverPlan.meilensteine.Insert(
                                    aktiverPlan.aktuellerMeilenstein + 1, neuerMs);
                                RenummeriereMeilensteine();
                            }
                            Persistiere();
                            return $"Meilenstein aufgeteilt: '{ms.beschreibung}' + '{revision.begruendung}'";
                        }
                    }
                }
                catch { }
            }

            // Fallback: Meilenstein reset
            ms.status = MeilensteinStatus.OFFEN;
            ms.tatsaechlicheBelohnung = 0f;
            ms.startZyklus = zyklusZaehler;
            Persistiere();
            return $"Meilenstein '{ms.beschreibung}' wird erneut versucht (#{aktiverPlan.umplanungen}).";
        }

        // ======== Meilenstein-Zerlegung via LLM ========

        private async Task<List<Meilenstein>> ZerlegeInMeilensteine(Ziel ziel, WeltZustand welt)
        {
            string kompetenzInfo = selbstModell?.GetSelbstbeschreibung() ?? "";
            int objektAnzahl = welt?.objekte?.Count ?? 0;

            string prompt = $"Zerlege dieses Ziel in 2-{MAX_MEILENSTEINE} aufeinanderfolgende Meilensteine:\n" +
                $"Ziel: '{ziel.beschreibung}' (Typ: {ziel.typ})\n" +
                $"Welt: {objektAnzahl} Objekte, Wetter: {welt?.wetter}\n" +
                $"Kompetenzen: {kompetenzInfo}\n\n" +
                $"Antworte als JSON-Array: [{{\"beschreibung\": \"...\", \"erfolgsBedingung\": \"...\", " +
                $"\"geschaetzteDauer\": N}}]\n" +
                $"Jeder Meilenstein sollte ein konkretes, pruefbares Zwischenziel sein.";

            var antwort = await llm.FreieAnfrage(prompt);
            if (antwort == null) return FallbackMeilensteine(ziel);

            try
            {
                var roh = Newtonsoft.Json.JsonConvert.DeserializeObject<List<MeilensteinRoh>>(antwort.inhalt);
                if (roh != null && roh.Count > 0)
                {
                    var ergebnis = new List<Meilenstein>();
                    int nr = 1;
                    foreach (var r in roh.Take(MAX_MEILENSTEINE))
                    {
                        ergebnis.Add(new Meilenstein
                        {
                            nummer = nr++,
                            beschreibung = r.beschreibung ?? $"Schritt {nr}",
                            erfolgsBedingung = r.erfolgsBedingung ?? "",
                            typ = ziel.typ,
                            geschaetzteDauer = Mathf.Clamp(r.geschaetzteDauer, 3f, 50f)
                        });
                    }
                    return ergebnis;
                }
            }
            catch { }

            return FallbackMeilensteine(ziel);
        }

        private List<Meilenstein> FallbackMeilensteine(Ziel ziel)
        {
            return new List<Meilenstein>
            {
                new Meilenstein
                {
                    nummer = 1,
                    beschreibung = $"Umgebung fuer '{ziel.beschreibung}' erkunden",
                    typ = ZielTyp.EXPLORATION,
                    geschaetzteDauer = 10f
                },
                new Meilenstein
                {
                    nummer = 2,
                    beschreibung = $"'{ziel.beschreibung}' ausfuehren",
                    typ = ziel.typ,
                    geschaetzteDauer = 15f
                },
                new Meilenstein
                {
                    nummer = 3,
                    beschreibung = "Ergebnis ueberpruefen und festigen",
                    typ = ZielTyp.VERSTAENDNIS,
                    geschaetzteDauer = 5f
                }
            };
        }

        // ======== Hilfsmethoden ========

        private Meilenstein GetAktuellerMeilenstein()
        {
            if (aktiverPlan == null) return null;
            if (aktiverPlan.aktuellerMeilenstein >= aktiverPlan.meilensteine.Count) return null;
            return aktiverPlan.meilensteine[aktiverPlan.aktuellerMeilenstein];
        }

        private void AktualisiereFortschritt()
        {
            if (aktiverPlan == null || aktiverPlan.meilensteine.Count == 0) return;
            int abgeschlossen = aktiverPlan.meilensteine.Count(m =>
                m.status == MeilensteinStatus.ABGESCHLOSSEN ||
                m.status == MeilensteinStatus.UEBERSPRUNGEN);
            aktiverPlan.gesamtFortschritt = abgeschlossen / (float)aktiverPlan.meilensteine.Count;
        }

        private void RenummeriereMeilensteine()
        {
            for (int i = 0; i < aktiverPlan.meilensteine.Count; i++)
                aktiverPlan.meilensteine[i].nummer = i + 1;
        }

        private void ArchiviereAktivenPlan()
        {
            if (aktiverPlan == null) return;
            planHistorie.Add(aktiverPlan);
            while (planHistorie.Count > MAX_HISTORIE)
                planHistorie.RemoveAt(0);
            aktiverPlan = null;
            Persistiere();
        }

        private string ZielTypZuDomaene(ZielTyp typ)
        {
            switch (typ)
            {
                case ZielTyp.EXPLORATION: return "navigation";
                case ZielTyp.EXPERIMENT: return "physik";
                case ZielTyp.AUFGABE: return "planung";
                case ZielTyp.SOZIAL: return "sozial";
                case ZielTyp.KONSTRUKTION: return "greifen";
                case ZielTyp.VERSTAENDNIS: return "allgemein";
                default: return "allgemein";
            }
        }

        // ======== API ========

        public LangzeitPlan GetAktiverPlan() => aktiverPlan;

        public Meilenstein GetAktuellenMeilenstein() => GetAktuellerMeilenstein();

        public List<LangzeitPlan> GetHistorie() => new List<LangzeitPlan>(planHistorie);

        public bool HatAktivenPlan() => aktiverPlan != null &&
            aktiverPlan.status == LangzeitPlanStatus.AKTIV;

        public string GetStatusText()
        {
            if (aktiverPlan == null)
                return $"Kein aktiver Langzeit-Plan. Historisch: {statistik.plaeneErstellt} erstellt, " +
                    $"{statistik.plaeneAbgeschlossen} abgeschlossen, {statistik.plaeneGescheitert} gescheitert.";

            var ms = GetAktuellerMeilenstein();
            return $"Plan: '{aktiverPlan.beschreibung}'\n" +
                $"  Fortschritt: {aktiverPlan.gesamtFortschritt:P0} " +
                $"(Meilenstein {aktiverPlan.aktuellerMeilenstein + 1}/{aktiverPlan.meilensteine.Count})\n" +
                $"  Aktuell: {ms?.beschreibung ?? "abgeschlossen"}\n" +
                $"  Umplanungen: {aktiverPlan.umplanungen}, " +
                $"Sim-Konfidenz: {aktiverPlan.simulationsKonfidenz:F2}\n" +
                $"  Statistik: {statistik.plaeneErstellt} Plaene, " +
                $"{statistik.meilensteineErreicht} Meilensteine erreicht, " +
                $"Genauigkeit: {statistik.durchschnittlicheGenauigkeit:P0}";
        }

        public string GetMeilensteineText()
        {
            if (aktiverPlan == null) return "Kein aktiver Plan.";

            var zeilen = new List<string>();
            foreach (var ms in aktiverPlan.meilensteine)
            {
                string statusSymbol = ms.status switch
                {
                    MeilensteinStatus.ABGESCHLOSSEN => "✓",
                    MeilensteinStatus.GESCHEITERT => "✗",
                    MeilensteinStatus.AKTIV => "→",
                    MeilensteinStatus.UEBERSPRUNGEN => "⤳",
                    _ => "○"
                };
                zeilen.Add($"  {statusSymbol} {ms.nummer}. {ms.beschreibung} " +
                    $"(Belohnung: {ms.tatsaechlicheBelohnung:F2})");
            }
            return string.Join("\n", zeilen);
        }

        public LangzeitPlanStatistik GetStatistik() => statistik;

        // ======== Persistenz ========

        public void Persistiere()
        {
            DatenLader.Speichere(PERSISTENZ_DATEI, new LangzeitPlanPersistenz
            {
                aktiverPlan = aktiverPlan,
                historie = planHistorie,
                statistik = statistik
            });
        }

        [Serializable]
        private class PlanRevision
        {
            public string aktion;
            public string neuerText;
            public string begruendung;
        }

        [Serializable]
        private class MeilensteinRoh
        {
            public string beschreibung;
            public string erfolgsBedingung;
            public float geschaetzteDauer;
        }

        [Serializable]
        private class LangzeitPlanPersistenz
        {
            public LangzeitPlan aktiverPlan;
            public List<LangzeitPlan> historie;
            public LangzeitPlanStatistik statistik;
        }
    }
}
