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
    //  MetaZielSystem — Introspektionsgetriebene autonome Zielgenerierung
    //
    //  Schaut sich den Zustand aller Subsysteme an und generiert
    //  autonom Ziele — das System gibt sich SELBST Aufgaben:
    //
    //  Quellen:
    //  1. Kompetenzluecken (SelbstModell) → Verbesserungsziele
    //  2. Neugier-Hypothesen (NeugierSystem) → Explorationsziele
    //  3. Offene Hypothesen (HypothesenEngine) → Experimentziele
    //  4. Meta-Einsichten (MetaKognition) → Strategiewechselziele
    //  5. Duenne Konzeptbereiche (KonzeptBaum) → Verstaendnisziele
    //  6. Schwache Kausalketten (KausalGraph) → Klaerungsziele
    //
    //  Respektiert ZielManager-Kapazitaet (max 3 aktive Ziele).
    //  Wird NACH Narrativ + Neugier im Zyklus aufgerufen.
    // ============================================================

    [Serializable]
    public class MetaZielQuelle
    {
        public string name;                // z.B. "Kompetenzluecke", "Hypothese", "MetaEinsicht"
        public string beschreibung;
        public float dringlichkeit;        // 0–1
        public ZielTyp empfohlenerTyp;
        public string domaene;
    }

    [Serializable]
    public class MetaZielErgebnis
    {
        public int quellenGefunden;
        public int zieleGeneriert;
        public List<string> generierteBeschreibungen = new();
        public string zusammenfassung;
    }

    [Serializable]
    public class MetaZielStatistik
    {
        public int gesamtGeneriert;
        public int gesamtErreicht;
        public int gesamtGescheitert;
        public Dictionary<string, int> quellenVerteilung = new();
    }

    public class MetaZielSystem
    {
        private readonly SelbstModell selbstModell;
        private readonly NeugierSystem neugier;
        private readonly HypothesenEngine hypothesenEngine;
        private readonly MetaKognition metaKognition;
        private readonly KonzeptBaum konzeptBaum;
        private readonly KausalGraph kausalGraph;
        private readonly ZielManager zielManager;
        private readonly AGIConfig config;

        private MetaZielStatistik statistik;
        private int zyklusZaehler;
        private List<MetaZielQuelle> letzteQuellen = new();

        private const int GENERIERUNGS_INTERVALL = 15;   // Alle N Zyklen neue Ziele pruefen
        private const float MIN_DRINGLICHKEIT = 0.3f;    // Unter diesem Schwellwert kein Ziel
        private const int MAX_QUELLEN_PRO_ZYKLUS = 5;    // Max Quellen die zu Zielen werden
        private const string PERSISTENZ_DATEI = "meta_ziel_statistik.json";

        // Kompetenz-Schwellen
        private const float KOMPETENZ_SCHWACH = 0.25f;    // Unter diesem Wert → Verbesserungsziel
        private const float KOMPETENZ_MITTEL = 0.5f;      // Unter diesem Wert → optionales Ziel

        public MetaZielSystem(
            SelbstModell selbstModell,
            NeugierSystem neugier,
            HypothesenEngine hypothesenEngine,
            MetaKognition metaKognition,
            KonzeptBaum konzeptBaum,
            KausalGraph kausalGraph,
            ZielManager zielManager,
            AGIConfig config)
        {
            this.selbstModell = selbstModell;
            this.neugier = neugier;
            this.hypothesenEngine = hypothesenEngine;
            this.metaKognition = metaKognition;
            this.konzeptBaum = konzeptBaum;
            this.kausalGraph = kausalGraph;
            this.zielManager = zielManager;
            this.config = config;

            statistik = DatenLader.Lade<MetaZielStatistik>(PERSISTENZ_DATEI) ?? new MetaZielStatistik();
            Debug.Log($"[MetaZielSystem] Initialisiert. Bisher {statistik.gesamtGeneriert} Ziele generiert.");
        }

        // ======== Haupt-Tick ========

        public MetaZielErgebnis ZyklusTick()
        {
            zyklusZaehler++;
            if (zyklusZaehler % GENERIERUNGS_INTERVALL != 0)
                return null;

            // Pruefe ob ZielManager noch Kapazitaet hat
            var aktiveZiele = zielManager.GetAlleAktiven();
            int freieSlots = 3 - aktiveZiele.Count; // MAX_AKTIVE aus ZielManager
            if (freieSlots <= 0)
                return null; // Alle Slots belegt, keine neuen Ziele

            // Sammle Quellen aus allen Subsystemen
            var quellen = SammelQuellen();
            letzteQuellen = quellen;

            if (quellen.Count == 0)
                return new MetaZielErgebnis { zusammenfassung = "Keine dringenden Ziele identifiziert." };

            // Sortiere nach Dringlichkeit, filtere Duplikate mit bestehenden Zielen
            quellen = FiltreDuplikate(quellen, aktiveZiele);
            quellen.Sort((a, b) => b.dringlichkeit.CompareTo(a.dringlichkeit));

            // Generiere Ziele (max freieSlots, max MAX_QUELLEN_PRO_ZYKLUS)
            int zuGenerieren = Mathf.Min(freieSlots, Mathf.Min(quellen.Count, MAX_QUELLEN_PRO_ZYKLUS));
            var ergebnis = new MetaZielErgebnis();

            for (int i = 0; i < zuGenerieren; i++)
            {
                var quelle = quellen[i];
                if (quelle.dringlichkeit < MIN_DRINGLICHKEIT)
                    break;

                var ziel = zielManager.FormuliereZiel(
                    quelle.beschreibung,
                    quelle.empfohlenerTyp,
                    quelle.dringlichkeit);

                ergebnis.zieleGeneriert++;
                ergebnis.generierteBeschreibungen.Add(quelle.beschreibung);

                // Statistik
                statistik.gesamtGeneriert++;
                if (!statistik.quellenVerteilung.ContainsKey(quelle.name))
                    statistik.quellenVerteilung[quelle.name] = 0;
                statistik.quellenVerteilung[quelle.name]++;

                Debug.Log($"[MetaZielSystem] Neues Ziel aus '{quelle.name}': {quelle.beschreibung} " +
                    $"(Dringlichkeit: {quelle.dringlichkeit:F2}, Typ: {quelle.empfohlenerTyp})");
            }

            ergebnis.quellenGefunden = quellen.Count;
            ergebnis.zusammenfassung = ergebnis.zieleGeneriert > 0
                ? $"{ergebnis.zieleGeneriert} neue Ziele aus {ergebnis.quellenGefunden} Quellen generiert."
                : "Quellen gefunden, aber unter Dringlichkeitsschwelle.";

            Persistiere();
            return ergebnis;
        }

        // ======== Quellen-Sammlung ========

        private List<MetaZielQuelle> SammelQuellen()
        {
            var quellen = new List<MetaZielQuelle>();

            // 1. Kompetenzluecken
            SammelKompetenzluecken(quellen);

            // 2. Neugier-Hypothesen
            SammelNeugierHypothesen(quellen);

            // 3. Offene HypothesenEngine-Hypothesen
            SammelOffeneHypothesen(quellen);

            // 4. Meta-Einsichten (Stagnation, Blindflecken)
            SammelMetaEinsichten(quellen);

            // 5. Schwache Kausalketten
            SammelSchwacheKausalitaeten(quellen);

            // 6. Duenne Konzeptbereiche
            SammelDuenneKonzeptbereiche(quellen);

            return quellen;
        }

        private void SammelKompetenzluecken(List<MetaZielQuelle> quellen)
        {
            if (selbstModell == null) return;

            foreach (var kvp in selbstModell.GetAlleKompetenzen())
            {
                if (kvp.Value < KOMPETENZ_SCHWACH)
                {
                    quellen.Add(new MetaZielQuelle
                    {
                        name = "Kompetenzluecke",
                        beschreibung = $"Kompetenz '{kvp.Key}' ist sehr niedrig ({kvp.Value:F2}). " +
                            "Gezielte Uebung in diesem Bereich noetig.",
                        dringlichkeit = 0.8f - kvp.Value,  // Je niedriger, desto dringlicher
                        empfohlenerTyp = KompetenzZuZielTyp(kvp.Key),
                        domaene = kvp.Key
                    });
                }
                else if (kvp.Value < KOMPETENZ_MITTEL)
                {
                    quellen.Add(new MetaZielQuelle
                    {
                        name = "Kompetenzluecke",
                        beschreibung = $"Kompetenz '{kvp.Key}' ausbaufaehig ({kvp.Value:F2}). " +
                            "Gelegenheit zum Ueben nutzen.",
                        dringlichkeit = 0.4f - kvp.Value * 0.3f,
                        empfohlenerTyp = KompetenzZuZielTyp(kvp.Key),
                        domaene = kvp.Key
                    });
                }
            }
        }

        private void SammelNeugierHypothesen(List<MetaZielQuelle> quellen)
        {
            if (neugier == null) return;

            var aktiveHypothesen = neugier.GetAktive();
            foreach (var hyp in aktiveHypothesen)
            {
                // NeugierSystem.HypotheseZuZiel() konvertiert → wir nutzen das hier
                quellen.Add(new MetaZielQuelle
                {
                    name = "NeugierHypothese",
                    beschreibung = hyp.beschreibung,
                    dringlichkeit = hyp.prioritaet * 0.8f, // Leicht gedaempft vs. direkte Quellen
                    empfohlenerTyp = HypotheseTypZuZielTyp(hyp.typ),
                    domaene = hyp.domaene
                });
            }
        }

        private void SammelOffeneHypothesen(List<MetaZielQuelle> quellen)
        {
            if (hypothesenEngine == null) return;

            var offene = hypothesenEngine.GetOffene();
            // Nur die Top-3 offenen Hypothesen als Testquellen
            int max = Mathf.Min(3, offene.Count);
            for (int i = 0; i < max; i++)
            {
                var hyp = offene[i];
                quellen.Add(new MetaZielQuelle
                {
                    name = "OffeneHypothese",
                    beschreibung = $"Teste Hypothese: '{hyp.beschreibung}' — Experiment: {hyp.experiment}",
                    dringlichkeit = hyp.prioritaet * 0.7f,
                    empfohlenerTyp = ZielTyp.EXPERIMENT,
                    domaene = hyp.domaene
                });
            }
        }

        private void SammelMetaEinsichten(List<MetaZielQuelle> quellen)
        {
            if (metaKognition == null) return;

            // Stagnation → Exploration erhoehen
            if (metaKognition.SollteExplorationErhoehen())
            {
                quellen.Add(new MetaZielQuelle
                {
                    name = "MetaEinsicht_Stagnation",
                    beschreibung = "Lernstagnation erkannt. Neue Bereiche der Welt erkunden " +
                        "oder kreativere Strategien ausprobieren.",
                    dringlichkeit = 0.75f,
                    empfohlenerTyp = ZielTyp.EXPLORATION,
                    domaene = "exploration"
                });
            }

            // Blindflecken → gezielte Untersuchung
            var einsichten = metaKognition.GetAktuelleEinsichten();
            if (einsichten != null)
            {
                foreach (var e in einsichten)
                {
                    if (e.typ == MetaEinsichtTyp.BlindFleck)
                    {
                        quellen.Add(new MetaZielQuelle
                        {
                            name = "MetaEinsicht_BlindFleck",
                            beschreibung = $"Blinder Fleck erkannt: {e.beschreibung}",
                            dringlichkeit = 0.65f,
                            empfohlenerTyp = ZielTyp.VERSTAENDNIS,
                            domaene = e.kontextCluster ?? "allgemein"
                        });
                    }
                    else if (e.typ == MetaEinsichtTyp.StrategieIneffektiv && e.konfidenz > 0.6f)
                    {
                        quellen.Add(new MetaZielQuelle
                        {
                            name = "MetaEinsicht_IneffektiveStrategie",
                            beschreibung = $"Strategie '{e.strategie}' funktioniert schlecht in '{e.kontextCluster}'. " +
                                "Alternative Ansaetze suchen.",
                            dringlichkeit = 0.5f,
                            empfohlenerTyp = ZielTyp.REVISION,
                            domaene = e.kontextCluster ?? "allgemein"
                        });
                    }
                }
            }
        }

        private void SammelSchwacheKausalitaeten(List<MetaZielQuelle> quellen)
        {
            if (kausalGraph == null) return;

            var schwache = kausalGraph.GetNiedrigeKonfidenz(0.3f);
            // Nur Top-2 schwache Kanten als Zielquellen
            int max = Mathf.Min(2, schwache.Count);
            for (int i = 0; i < max; i++)
            {
                var kante = schwache[i];
                quellen.Add(new MetaZielQuelle
                {
                    name = "SchwacheKausalitaet",
                    beschreibung = $"Zusammenhang '{kante.ursache}' → '{kante.wirkung}' ist unsicher " +
                        $"(Konfidenz: {kante.konfidenz:F2}). Gezieltes Experiment empfohlen.",
                    dringlichkeit = 0.5f + (1f - kante.konfidenz) * 0.3f,
                    empfohlenerTyp = ZielTyp.EXPERIMENT,
                    domaene = "kausal"
                });
            }
        }

        private void SammelDuenneKonzeptbereiche(List<MetaZielQuelle> quellen)
        {
            if (konzeptBaum == null) return;

            // Suche Blaetter mit wenig Erfahrungsabdeckung
            var alleKnoten = konzeptBaum.GetKnoten();
            if (alleKnoten == null) return;

            var duenne = alleKnoten.Values
                .Where(k => k.kinderIds.Count == 0 && k.erfahrungsAbdeckung < 3)
                .OrderBy(k => k.erfahrungsAbdeckung)
                .Take(2);

            foreach (var knoten in duenne)
            {
                quellen.Add(new MetaZielQuelle
                {
                    name = "DuennerKonzeptbereich",
                    beschreibung = $"Konzept '{knoten.name}' hat wenig Erfahrungsbasis " +
                        $"({knoten.erfahrungsAbdeckung} Erfahrungen). Mehr Beispiele sammeln.",
                    dringlichkeit = 0.4f,
                    empfohlenerTyp = ZielTyp.VERSTAENDNIS,
                    domaene = "konzepte"
                });
            }
        }

        // ======== Duplikat-Filterung ========

        private List<MetaZielQuelle> FiltreDuplikate(List<MetaZielQuelle> quellen, List<Ziel> aktiveZiele)
        {
            if (aktiveZiele == null || aktiveZiele.Count == 0)
                return quellen;

            // Filtere Quellen deren Domaene + Typ bereits aktiv ist
            var aktiveDomaenen = new HashSet<string>();
            foreach (var z in aktiveZiele)
            {
                string domKey = $"{z.typ}:{z.beschreibung?.Substring(0, Math.Min(30, z.beschreibung?.Length ?? 0))}";
                aktiveDomaenen.Add(domKey);
            }

            return quellen.Where(q =>
            {
                string key = $"{q.empfohlenerTyp}:{q.beschreibung?.Substring(0, Math.Min(30, q.beschreibung?.Length ?? 0))}";
                return !aktiveDomaenen.Contains(key);
            }).ToList();
        }

        // ======== Manuelle Zielgenerierung erzwingen ========

        public MetaZielErgebnis ErzwingeGenerierung()
        {
            zyklusZaehler = GENERIERUNGS_INTERVALL - 1; // Naechster Tick genieriert
            return ZyklusTick();
        }

        // ======== Statistik-Tracking ========

        public void RegistriereZielErgebnis(string zielId, bool erreicht)
        {
            if (erreicht)
                statistik.gesamtErreicht++;
            else
                statistik.gesamtGescheitert++;
            Persistiere();
        }

        // ======== Status-Abfragen ========

        public string GetStatusText()
        {
            var aktive = zielManager?.GetAlleAktiven()?.Count ?? 0;
            float erfolgsrate = statistik.gesamtGeneriert > 0
                ? statistik.gesamtErreicht / (float)statistik.gesamtGeneriert
                : 0f;

            return $"Generiert: {statistik.gesamtGeneriert} | Erreicht: {statistik.gesamtErreicht} | " +
                $"Gescheitert: {statistik.gesamtGescheitert} | Erfolgsrate: {erfolgsrate:P0} | " +
                $"Aktive Slots: {aktive}/3 | Letzte Quellen: {letzteQuellen.Count} | " +
                $"Intervall: alle {GENERIERUNGS_INTERVALL} Zyklen";
        }

        public List<MetaZielQuelle> GetLetzteQuellen() => letzteQuellen;

        public MetaZielStatistik GetStatistik() => statistik;

        public string GetQuellenVerteilungText()
        {
            if (statistik.quellenVerteilung.Count == 0)
                return "Noch keine Ziele generiert.";

            var teile = statistik.quellenVerteilung
                .OrderByDescending(kvp => kvp.Value)
                .Select(kvp => $"{kvp.Key}: {kvp.Value}");
            return string.Join(", ", teile);
        }

        // ======== Hilfsfunktionen ========

        private ZielTyp KompetenzZuZielTyp(string domaene)
        {
            return domaene switch
            {
                "navigation" => ZielTyp.EXPLORATION,
                "physik" => ZielTyp.EXPERIMENT,
                "sozial" => ZielTyp.SOZIAL,
                "kommunikation" => ZielTyp.SOZIAL,
                "planung" => ZielTyp.AUFGABE,
                "greifen" => ZielTyp.EXPERIMENT,
                "werfen" => ZielTyp.EXPERIMENT,
                _ => ZielTyp.EXPLORATION
            };
        }

        private ZielTyp HypotheseTypZuZielTyp(HypotheseTyp typ)
        {
            return typ switch
            {
                HypotheseTyp.Wissensluecke => ZielTyp.VERSTAENDNIS,
                HypotheseTyp.NiedrigeKonfidenz => ZielTyp.EXPERIMENT,
                HypotheseTyp.Exploration => ZielTyp.EXPLORATION,
                HypotheseTyp.Experiment => ZielTyp.EXPERIMENT,
                _ => ZielTyp.EXPLORATION
            };
        }

        private void Persistiere()
        {
            DatenLader.Speichere(PERSISTENZ_DATEI, statistik);
        }
    }
}
