using System;
using System.Collections.Generic;
using System.Linq;
using BilligAGI.Modelle;
using BilligAGI.Daten;
using UnityEngine;

namespace BilligAGI.Sozial
{
    /// <summary>
    /// Episodisches Archetypen-Gedaechtnis.
    ///
    /// Kein Lexikon. Keine globale Definition pro Archetyp.
    /// Stattdessen: Eine Sammlung konkreter Instanzen — Situationen in denen
    /// sich ein archetypisches Muster gezeigt hat.
    ///
    /// Die "Bedeutung" eines Archetyps ergibt sich aus seinen Instanzen.
    /// In verschiedenen Kontexten (physik, sozial, existenziell) kann derselbe
    /// Archetyp-Name verschiedene Bedeutungen haben — pro Kontext-Cluster
    /// wird separat konvergiert.
    ///
    /// Erkennung geschieht durch Aehnlichkeit zu vergangenen Instanzen,
    /// nicht durch Nachschlag einer Definition.
    /// </summary>
    public class ArchetypenGedaechtnis
    {
        private List<Archetyp> seedArchetypen;                    // Ausgangshypothesen (unveraenderlich)
        private List<ArchetypInstanz> instanzen;                  // Alle episodischen Instanzen
        private Dictionary<string, ArchetypCluster> cluster;      // Konvergierte Interpretationen pro Kontext
        private int instanzZaehler;

        private const string INSTANZEN_DATEI = "archetypen_instanzen.json";
        private const string CLUSTER_DATEI = "archetypen_cluster.json";
        private const int MAX_INSTANZEN_PRO_CLUSTER = 50;

        public ArchetypenGedaechtnis()
        {
            // Seed-Daten sind Hypothesen, nicht Wahrheiten
            seedArchetypen = DatenLader.LadeListe<Archetyp>("archetypen.json") ?? new List<Archetyp>();

            // Episodisches Gedaechtnis laden
            instanzen = DatenLader.LadeListe<ArchetypInstanz>(INSTANZEN_DATEI) ?? new List<ArchetypInstanz>();
            instanzZaehler = instanzen.Count;

            // Cluster laden
            var clusterListe = DatenLader.LadeListe<ArchetypCluster>(CLUSTER_DATEI) ?? new List<ArchetypCluster>();
            cluster = new Dictionary<string, ArchetypCluster>();
            foreach (var c in clusterListe)
                cluster[$"{c.archetypName}::{c.kontextCluster}"] = c;

            Debug.Log($"[ArchetypenGedaechtnis] {seedArchetypen.Count} Seed-Hypothesen, " +
                $"{instanzen.Count} Instanzen, {cluster.Count} Cluster geladen.");
        }

        // --- Seed-Zugriff (Hypothesen, nicht Wahrheiten) ---

        public Archetyp GetSeed(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return seedArchetypen.FirstOrDefault(a =>
                a.name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public List<Archetyp> AlleSeedArchetypen() => new List<Archetyp>(seedArchetypen);

        // --- Instanzen: Das episodische Gedaechtnis ---

        /// <summary>
        /// Speichert eine konkrete Erkennung als Instanz.
        /// Das IST das Gedaechtnis — nicht eine Zaehler-Erhoehung.
        /// </summary>
        public ArchetypInstanz SpeichereInstanz(
            string archetypName, string situation, string verhalten,
            string interpretation, string aspekt, string entitaetId,
            string kontextCluster, float konfidenz, List<string> kontextMerkmale)
        {
            var instanz = new ArchetypInstanz
            {
                id = $"ai_{++instanzZaehler}",
                archetypName = archetypName,
                situation = situation,
                verhalten = verhalten,
                interpretation = interpretation,
                aspekt = aspekt,
                entitaetId = entitaetId,
                kontextCluster = kontextCluster ?? "allgemein",
                konfidenz = konfidenz,
                zeitstempel = Time.time,
                kontextMerkmale = kontextMerkmale ?? new List<string>()
            };

            instanzen.Add(instanz);

            // Zum passenden Cluster hinzufuegen
            string clusterKey = $"{archetypName}::{instanz.kontextCluster}";
            if (!cluster.TryGetValue(clusterKey, out var c))
            {
                c = new ArchetypCluster
                {
                    archetypName = archetypName,
                    kontextCluster = instanz.kontextCluster,
                    konfidenz = 0.3f,
                    revisionsPass = 0
                };
                cluster[clusterKey] = c;
            }
            c.instanzIds.Add(instanz.id);

            // Cluster-Groesse begrenzen (aelteste Instanzen raus, aber nicht loeschen)
            while (c.instanzIds.Count > MAX_INSTANZEN_PRO_CLUSTER)
                c.instanzIds.RemoveAt(0);

            Persistiere();
            return instanz;
        }

        /// <summary>
        /// Alle Instanzen eines bestimmten Archetyps.
        /// </summary>
        public List<ArchetypInstanz> InstanzenVon(string archetypName)
        {
            string lower = archetypName.ToLowerInvariant();
            return instanzen.Where(i =>
                i.archetypName.Equals(archetypName, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        /// <summary>
        /// Instanzen eines Archetyps in einem bestimmten Kontext.
        /// DAS ist kontextabhaengig — "Held" in "physik" vs "Held" in "sozial".
        /// </summary>
        public List<ArchetypInstanz> InstanzenImKontext(string archetypName, string kontextCluster)
        {
            return instanzen.Where(i =>
                i.archetypName.Equals(archetypName, StringComparison.OrdinalIgnoreCase) &&
                i.kontextCluster == kontextCluster).ToList();
        }

        /// <summary>
        /// Die letzten N Instanzen (kontextunabhaengig) — fuer Prompt-Kontext.
        /// </summary>
        public List<ArchetypInstanz> LetzteInstanzen(int n = 10)
        {
            return instanzen.OrderByDescending(i => i.zeitstempel).Take(n).ToList();
        }

        // --- Cluster: Kontextabhaengige Interpretationen ---

        /// <summary>
        /// Die konvergierte Bedeutung eines Archetyps IN EINEM BESTIMMTEN KONTEXT.
        /// Gibt null zurueck wenn es noch keine konvergierte Interpretation gibt.
        /// </summary>
        public string GetKontextBedeutung(string archetypName, string kontextCluster)
        {
            string key = $"{archetypName}::{kontextCluster}";
            if (cluster.TryGetValue(key, out var c) && !string.IsNullOrEmpty(c.konvergierteInterpretation))
                return c.konvergierteInterpretation;
            return null;
        }

        /// <summary>
        /// ALLE Bedeutungen eines Archetyps, aufgeschluesselt nach Kontext.
        /// "Held" kann in "physik" etwas anderes bedeuten als in "sozial".
        /// </summary>
        public Dictionary<string, string> GetAlleBedeutungen(string archetypName)
        {
            var ergebnis = new Dictionary<string, string>();
            foreach (var kvp in cluster)
            {
                if (kvp.Value.archetypName.Equals(archetypName, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(kvp.Value.konvergierteInterpretation))
                {
                    ergebnis[kvp.Value.kontextCluster] = kvp.Value.konvergierteInterpretation;
                }
            }
            return ergebnis;
        }

        /// <summary>
        /// Gibt die beste verfuegbare Beschreibung fuer einen Archetyp:
        /// 1. Konvergierte kontextabhaengige Interpretation (wenn vorhanden)
        /// 2. Kontextunabhaengige Zusammenfassung aus allen Instanzen
        /// 3. Seed-Hypothese als Fallback (markiert)
        /// </summary>
        public string GetBesteBeschreibung(string archetypName, string kontextCluster = null)
        {
            // 1. Kontext-spezifisch
            if (!string.IsNullOrEmpty(kontextCluster))
            {
                var kontextBed = GetKontextBedeutung(archetypName, kontextCluster);
                if (kontextBed != null)
                    return kontextBed;
            }

            // 2. Allgemein (wenn genuegend Instanzen)
            var allgemein = GetKontextBedeutung(archetypName, "allgemein");
            if (allgemein != null) return allgemein;

            // 3. Aus Instanzen: Die letzten Interpretationen zusammenfassen
            var inst = InstanzenVon(archetypName);
            if (inst.Count >= 3)
            {
                var letzte = inst.OrderByDescending(i => i.zeitstempel).Take(5);
                return $"[AUS ERFAHRUNG, {inst.Count} Instanzen] " +
                    string.Join("; ", letzte.Select(i => i.interpretation).Where(s => !string.IsNullOrEmpty(s)));
            }

            // 4. Seed-Hypothese
            var seed = GetSeed(archetypName);
            if (seed != null)
                return $"[HYPOTHESE] {seed.lichtAspekt} / {seed.schattenAspekt} — {seed.motivation}";

            return null;
        }

        /// <summary>
        /// Aktualisiert die konvergierte Interpretation eines Clusters.
        /// Wird von KonzeptRevision aufgerufen nach hermeneutischem Zirkel.
        /// </summary>
        public void AktualisiereCluster(string archetypName, string kontextCluster,
            string neueInterpretation, float konfidenz, int pass)
        {
            string key = $"{archetypName}::{kontextCluster}";
            if (!cluster.TryGetValue(key, out var c))
            {
                c = new ArchetypCluster
                {
                    archetypName = archetypName,
                    kontextCluster = kontextCluster
                };
                cluster[key] = c;
            }
            c.konvergierteInterpretation = neueInterpretation;
            c.konfidenz = konfidenz;
            c.revisionsPass = pass;
            c.letzteRevision = DateTime.UtcNow.ToString("o");
            Persistiere();
        }

        /// <summary>
        /// Registriert einen emergenten Archetyp (nicht aus Seed-Daten).
        /// </summary>
        public Archetyp RegistriereEmergentenArchetyp(string name, string lichtAspekt,
            string schattenAspekt, string motivation, List<string> prototypen)
        {
            var neuer = new Archetyp
            {
                name = name,
                lichtAspekt = lichtAspekt,
                schattenAspekt = schattenAspekt,
                motivation = motivation,
                prototypischeVerhaltensweisen = prototypen ?? new List<string>(),
                quelle = ArchetypQuelle.Emergent
            };
            seedArchetypen.Add(neuer); // Wird zu den bekannten Mustern hinzugefuegt
            Debug.Log($"[ArchetypenGedaechtnis] Emergenter Archetyp: {name}");
            Persistiere();
            return neuer;
        }

        public Archetyp GetGegenarchetyp(string name)
        {
            var a = GetSeed(name);
            if (a == null || string.IsNullOrEmpty(a.gegenarchetyp)) return null;
            return GetSeed(a.gegenarchetyp);
        }

        /// <summary>
        /// Alle bekannten Archetyp-Namen (Seed + Emergent).
        /// </summary>
        public List<string> AlleArchetypNamen()
        {
            var namen = new HashSet<string>(seedArchetypen.Select(s => s.name));
            foreach (var i in instanzen)
                namen.Add(i.archetypName);
            return namen.ToList();
        }

        /// <summary>
        /// Wie viele Instanzen hat ein Archetyp insgesamt und pro Kontext?
        /// </summary>
        public Dictionary<string, int> InstanzStatistik(string archetypName)
        {
            var stats = new Dictionary<string, int>();
            var inst = InstanzenVon(archetypName);
            stats["gesamt"] = inst.Count;
            foreach (var group in inst.GroupBy(i => i.kontextCluster))
                stats[group.Key] = group.Count();
            return stats;
        }

        /// <summary>
        /// Cluster die genug Instanzen haben aber noch keine konvergierte
        /// Interpretation — Kandidaten fuer hermeneutische Revision.
        /// </summary>
        public List<ArchetypCluster> RevisionskandidatenHolen(int minInstanzen = 5)
        {
            return cluster.Values.Where(c =>
                c.instanzIds.Count >= minInstanzen &&
                string.IsNullOrEmpty(c.konvergierteInterpretation)).ToList();
        }

        public int GesamtInstanzen => instanzen.Count;
        public int GesamtCluster => cluster.Count;

        public void Persistiere()
        {
            DatenLader.Speichere(INSTANZEN_DATEI, instanzen);
            DatenLader.Speichere(CLUSTER_DATEI, cluster.Values.ToList());
        }
    }
}
