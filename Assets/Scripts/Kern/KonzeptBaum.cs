using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BilligAGI.Modelle;
using BilligAGI.Gedaechtnis;
using UnityEngine;

namespace BilligAGI.Kern
{
    // ============================================================
    //  KonzeptBaum — Hierarchische Abstraktion
    //
    //  Organisiert flache Konzepte in einer Baumstruktur:
    //    Lebewesen
    //      ├── Tier
    //      │   ├── Raubtier
    //      │   └── Beute
    //      └── Pflanze
    //
    //  Operationen:
    //  1. Bottom-Up: Aehnliche Konzepte → LLM findet Oberbegriff
    //  2. Top-Down: Konzept mit zu vielen Anwendungen → LLM spaltet
    //  3. Traversierung: Von spezifisch → abstrakt (oder umgekehrt)
    //  4. Vererbung: Oberbegriff-Eigenschaften gelten fuer Kinder
    //
    //  Nutzt KonzeptRevision fuer die hermeneutische Revision der
    //  Hierarchie selbst — der Baum ist nicht fix, sondern revidierbar.
    // ============================================================

    [Serializable]
    public class KonzeptKnoten
    {
        public string konzeptId;            // Referenz auf Konzept in KonzeptRevision
        public string name;
        public string parentId;             // null = Wurzel
        public List<string> kinderIds = new();
        public int abstraktionsEbene;       // 0 = Wurzel, hoeher = spezifischer
        public float spezifitaet;           // 0–1: wie spezifisch (0=abstrakt, 1=konkret)
        public int erfahrungsAbdeckung;     // Wie viele Erfahrungen dieses Konzept matchen
        public string zeitstempel;

        public KonzeptKnoten()
        {
            zeitstempel = DateTime.UtcNow.ToString("o");
        }
    }

    public class KonzeptBaum
    {
        private readonly LLMAdapter llm;
        private readonly KonzeptRevision revision;
        private readonly ErfahrungsSpeicher erfahrungen;
        private readonly AGIConfig config;

        private Dictionary<string, KonzeptKnoten> knoten = new();
        private List<string> wurzeln = new(); // Top-Level Konzepte (kein Parent)

        private int zyklusSeitLetzterReorganisation;
        private const int REORGANISATIONS_INTERVALL = 80;
        private const int MIN_FUER_ABSTRAKTION = 3;    // Min. Geschwister fuer Oberbegriff
        private const int MAX_FUER_SPALTUNG = 15;      // Max. Erfahrungen bevor gespalten wird
        private const string SPEICHER_PFAD = "konzept_baum.json";

        private const string HIERARCHIE_SYSTEM = @"Du bist ein Taxonomie-Experte. 
Analysiere die gegebenen Konzepte und bilde eine Hierarchie.

Antworte als JSON:
{
  ""oberbegriff"": ""Name des abstrakteren Konzepts"",
  ""definition"": ""Was alle Kindkonzepte gemeinsam haben"",
  ""begruendung"": ""Warum diese Gruppierung sinnvoll ist""
}

Regeln:
- Der Oberbegriff muss ALLGEMEINER sein als alle Kinder
- Er muss eine NEUE nuetzliche Kategorie sein, kein trivialer Container
- Wenn keine sinnvolle Hierarchie existiert: {""oberbegriff"": null}";

        private const string SPALTUNGS_SYSTEM = @"Du bist ein Taxonomie-Experte.
Ein Konzept ist zu breit. Schlage Unterkategorien vor.

Antworte als JSON:
{
  ""unterkategorien"": [
    {""name"": ""Spezifischer Name"", ""definition"": ""Was diese Unterkategorie ausmacht""},
    ...
  ]
}

Regeln:
- Mindestens 2, maximal 4 Unterkategorien
- Jede muss sich KLAR von den anderen unterscheiden
- Zusammen muessen sie das Elternkonzept VOLLSTÄNDIG abdecken
- Wenn Spaltung unsinnig: {""unterkategorien"": []}";

        public KonzeptBaum(
            LLMAdapter llm,
            KonzeptRevision revision,
            ErfahrungsSpeicher erfahrungen,
            AGIConfig config)
        {
            this.llm = llm;
            this.revision = revision;
            this.erfahrungen = erfahrungen;
            this.config = config;

            LadeVonDisk();
        }

        // ===========================================================
        //  1. ZYKLUS-HOOK: Periodische Reorganisation
        // ===========================================================

        public async Task<string> ZyklusTick()
        {
            zyklusSeitLetzterReorganisation++;
            if (zyklusSeitLetzterReorganisation < REORGANISATIONS_INTERVALL)
                return null;

            zyklusSeitLetzterReorganisation = 0;

            // Neue Konzepte ohne Knoten einpflegen
            SynchroMitRevision();

            // Bottom-Up: Aehnliche Wurzeln gruppieren
            string ergebnis = await VersucheBottomUpAbstraktion();

            // Top-Down: Zu breite Konzepte spalten
            if (ergebnis == null)
                ergebnis = await VersucheTopDownSpaltung();

            if (ergebnis != null)
                SpeichereAufDisk();

            return ergebnis;
        }

        // ===========================================================
        //  2. BOTTOM-UP: Geschwister → Oberbegriff
        // ===========================================================

        private async Task<string> VersucheBottomUpAbstraktion()
        {
            // Finde Gruppen von Wurzelknoten die zusammengehoeren koennten
            var wurzelKnoten = wurzeln
                .Where(id => knoten.ContainsKey(id))
                .Select(id => knoten[id])
                .ToList();

            if (wurzelKnoten.Count < MIN_FUER_ABSTRAKTION)
                return null;

            // Versuche Paare/Tripel von Wurzeln zu gruppieren
            // Strategie: Paar mit aehnlichsten Namen/Definitionen
            var kandidaten = FindeGruppierungsKandidaten(wurzelKnoten);
            if (kandidaten.Count < 2)
                return null;

            // LLM fragen ob ein Oberbegriff sinnvoll ist
            string prompt = BaueAbstraktionsPrompt(kandidaten);
            var antwort = await llm.FreieAnfrage(prompt, HIERARCHIE_SYSTEM);
            if (antwort == null || string.IsNullOrWhiteSpace(antwort.inhalt))
                return null;

            string oberbegriff = ExtractJsonString(antwort.inhalt, "oberbegriff");
            if (string.IsNullOrEmpty(oberbegriff) || oberbegriff == "null")
                return null;

            string definition = ExtractJsonString(antwort.inhalt, "definition") ?? oberbegriff;

            // Neues Konzept fuer den Oberbegriff erstellen
            var neuesKonzept = new Konzept
            {
                name = oberbegriff,
                typ = KonzeptTyp.Emergent,
                aktuelleDefinition = definition,
                ursprungsDefinition = definition
            };
            revision.RegistriereKonzept(neuesKonzept);

            // Knoten fuer Oberbegriff
            var oberKnoten = new KonzeptKnoten
            {
                konzeptId = neuesKonzept.id,
                name = oberbegriff,
                abstraktionsEbene = 0,
                spezifitaet = 0.2f
            };

            // Kinder zuordnen
            foreach (var kind in kandidaten)
            {
                kind.parentId = neuesKonzept.id;
                kind.abstraktionsEbene = oberKnoten.abstraktionsEbene + 1;
                oberKnoten.kinderIds.Add(kind.konzeptId);
                wurzeln.Remove(kind.konzeptId);

                // Rekursiv Ebenen anpassen
                AktualisiereEbenen(kind, kind.abstraktionsEbene);
            }

            knoten[neuesKonzept.id] = oberKnoten;
            wurzeln.Add(neuesKonzept.id);

            string zusammenfassung = $"Oberbegriff '{oberbegriff}' gebildet fuer: " +
                string.Join(", ", kandidaten.Select(k => k.name));
            Debug.Log($"[KonzeptBaum] {zusammenfassung}");

            return zusammenfassung;
        }

        // ===========================================================
        //  3. TOP-DOWN: Zu breite Konzepte spalten
        // ===========================================================

        private async Task<string> VersucheTopDownSpaltung()
        {
            // Finde Konzepte die zu viele verschiedene Erfahrungen abdecken
            foreach (var kvp in knoten)
            {
                var k = kvp.Value;
                if (k.kinderIds.Count > 0) continue; // Nur Blaetter spalten

                var erfs = revision.ErfahrungenMitKonzept(k.konzeptId);
                if (erfs == null || erfs.Count < MAX_FUER_SPALTUNG) continue;

                // LLM fragen wie man spalten kann
                string prompt = BaueSpaltungsPrompt(k, erfs);
                var antwort = await llm.FreieAnfrage(prompt, SPALTUNGS_SYSTEM);
                if (antwort == null || string.IsNullOrWhiteSpace(antwort.inhalt))
                    continue;

                var unterKategorien = ParseUnterkategorien(antwort.inhalt);
                if (unterKategorien.Count < 2)
                    continue;

                // Unterkategorien als neue Konzepte erstellen
                foreach (var (name, def) in unterKategorien)
                {
                    var subKonzept = new Konzept
                    {
                        name = name,
                        typ = KonzeptTyp.Emergent,
                        aktuelleDefinition = def,
                        ursprungsDefinition = def
                    };
                    revision.RegistriereKonzept(subKonzept);

                    var subKnoten = new KonzeptKnoten
                    {
                        konzeptId = subKonzept.id,
                        name = name,
                        parentId = k.konzeptId,
                        abstraktionsEbene = k.abstraktionsEbene + 1,
                        spezifitaet = Math.Min(1f, k.spezifitaet + 0.2f)
                    };

                    knoten[subKonzept.id] = subKnoten;
                    k.kinderIds.Add(subKonzept.id);
                }

                string zusammenfassung = $"'{k.name}' gespalten in: " +
                    string.Join(", ", unterKategorien.Select(u => u.name));
                Debug.Log($"[KonzeptBaum] {zusammenfassung}");

                return zusammenfassung;
            }

            return null;
        }

        // ===========================================================
        //  4. TRAVERSIERUNG + ABFRAGEN
        // ===========================================================

        /// <summary>
        /// Gibt den Pfad vom spezifischsten zum abstraktesten Konzept.
        /// Z.B. ["Pudel", "Hund", "Tier", "Lebewesen"]
        /// </summary>
        public List<string> PfadNachOben(string konzeptId)
        {
            var pfad = new List<string>();
            string current = konzeptId;

            while (current != null && knoten.TryGetValue(current, out var k))
            {
                pfad.Add(k.name);
                current = k.parentId;
                if (pfad.Count > 20) break; // Endlosschleifen-Schutz
            }

            return pfad;
        }

        /// <summary>
        /// Gibt alle Kinder (direkt + transitiv) eines Konzepts.
        /// </summary>
        public List<KonzeptKnoten> AlleNachkommen(string konzeptId)
        {
            var ergebnis = new List<KonzeptKnoten>();
            if (!knoten.TryGetValue(konzeptId, out var start)) return ergebnis;

            var queue = new Queue<KonzeptKnoten>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var k = queue.Dequeue();
                foreach (var kindId in k.kinderIds)
                {
                    if (knoten.TryGetValue(kindId, out var kind))
                    {
                        ergebnis.Add(kind);
                        queue.Enqueue(kind);
                    }
                }
            }

            return ergebnis;
        }

        /// <summary>
        /// Findet den naechsten gemeinsamen Vorfahren zweier Konzepte.
        /// </summary>
        public string GemeinsamerVorfahr(string idA, string idB)
        {
            var pfadA = PfadNachObenIds(idA);
            var pfadB = new HashSet<string>(PfadNachObenIds(idB));
            return pfadA.FirstOrDefault(id => pfadB.Contains(id));
        }

        /// <summary>
        /// Berechnet die semantische Distanz zwischen zwei Konzepten.
        /// 0 = gleich, hoeher = weiter entfernt.
        /// </summary>
        public int SemantischeDistanz(string idA, string idB)
        {
            var pfadA = PfadNachObenIds(idA);
            var pfadB = PfadNachObenIds(idB);

            string vorfahr = GemeinsamerVorfahr(idA, idB);
            if (vorfahr == null) return int.MaxValue;

            int distA = pfadA.IndexOf(vorfahr);
            int distB = pfadB.IndexOf(vorfahr);
            return distA + distB;
        }

        // ===========================================================
        //  5. SYNCHRONISATION + STATUS
        // ===========================================================

        /// <summary>
        /// Synchronisiert den Baum mit neuen Konzepten aus KonzeptRevision.
        /// </summary>
        private void SynchroMitRevision()
        {
            // Alle bekannten Konzepte aus der Revision durchgehen
            // Neue als Wurzelknoten einpflegen
            var alleKonzepte = revision.GetAlleKonzepte();
            if (alleKonzepte == null) return;

            foreach (var k in alleKonzepte)
            {
                if (knoten.ContainsKey(k.id)) continue;

                var neuerKnoten = new KonzeptKnoten
                {
                    konzeptId = k.id,
                    name = k.name,
                    abstraktionsEbene = 0, // erstmal Wurzel
                    spezifitaet = 0.5f     // neutral
                };

                knoten[k.id] = neuerKnoten;
                wurzeln.Add(k.id);
            }
        }

        public string GetStatusText()
        {
            int gesamt = knoten.Count;
            int wurzelN = wurzeln.Count;
            int maxTiefe = knoten.Count > 0
                ? knoten.Values.Max(k => k.abstraktionsEbene) : 0;
            int blaetter = knoten.Values.Count(k => k.kinderIds.Count == 0);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"KonzeptBaum: {gesamt} Knoten, {wurzelN} Wurzeln, " +
                $"Max. Tiefe: {maxTiefe}, Blätter: {blaetter}");
            sb.AppendLine($"Nächste Reorganisation in: " +
                $"{REORGANISATIONS_INTERVALL - zyklusSeitLetzterReorganisation} Zyklen");

            // Top-Level Struktur anzeigen
            foreach (var wId in wurzeln.Take(5))
            {
                if (!knoten.TryGetValue(wId, out var w)) continue;
                string kinder = w.kinderIds.Count > 0
                    ? $" ({w.kinderIds.Count} Kinder)"
                    : " (Blatt)";
                sb.AppendLine($"  {w.name}{kinder}");
            }
            if (wurzeln.Count > 5)
                sb.AppendLine($"  ... +{wurzeln.Count - 5} weitere");

            return sb.ToString();
        }

        public string GetBaumText()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var wId in wurzeln)
                DruckeBaumKnoten(sb, wId, "", true);
            return sb.ToString();
        }

        public async Task<string> ErzwingeReorganisation()
        {
            SynchroMitRevision();
            string ergebnis = await VersucheBottomUpAbstraktion();
            if (ergebnis == null)
                ergebnis = await VersucheTopDownSpaltung();
            if (ergebnis != null)
                SpeichereAufDisk();
            return ergebnis;
        }

        public KonzeptKnoten GetKnoten(string konzeptId) =>
            knoten.TryGetValue(konzeptId, out var k) ? k : null;

        public Dictionary<string, KonzeptKnoten> GetKnoten() => knoten;

        public int Tiefe => knoten.Count > 0
            ? knoten.Values.Max(k => k.abstraktionsEbene) : 0;

        public int KnotenAnzahl => knoten.Count;

        // ===========================================================
        //  PRIVATE: Hilfsfunktionen
        // ===========================================================

        private void DruckeBaumKnoten(System.Text.StringBuilder sb, string id,
            string prefix, bool istLetztes)
        {
            if (!knoten.TryGetValue(id, out var k)) return;

            string verbinder = istLetztes ? "└── " : "├── ";
            sb.AppendLine($"{prefix}{verbinder}{k.name} [Ebene {k.abstraktionsEbene}]");

            string neuerPrefix = prefix + (istLetztes ? "    " : "│   ");
            for (int i = 0; i < k.kinderIds.Count; i++)
                DruckeBaumKnoten(sb, k.kinderIds[i], neuerPrefix,
                    i == k.kinderIds.Count - 1);
        }

        private List<string> PfadNachObenIds(string konzeptId)
        {
            var pfad = new List<string>();
            string current = konzeptId;
            while (current != null && knoten.TryGetValue(current, out var k))
            {
                pfad.Add(current);
                current = k.parentId;
                if (pfad.Count > 20) break;
            }
            return pfad;
        }

        private List<KonzeptKnoten> FindeGruppierungsKandidaten(List<KonzeptKnoten> wurzelKnoten)
        {
            // Einfache Heuristik: Finde 2-3 Konzepte mit aehnlichen Namen/Definitionen
            // (Keyword-Overlap-basiert, kein LLM noetig)
            if (wurzelKnoten.Count < 2) return new();

            float besteAehnlichkeit = -1f;
            KonzeptKnoten besteA = null, besteB = null;

            for (int i = 0; i < wurzelKnoten.Count; i++)
            {
                for (int j = i + 1; j < wurzelKnoten.Count; j++)
                {
                    float aehnlichkeit = BerechneNamensAehnlichkeit(
                        wurzelKnoten[i].name, wurzelKnoten[j].name);
                    if (aehnlichkeit > besteAehnlichkeit)
                    {
                        besteAehnlichkeit = aehnlichkeit;
                        besteA = wurzelKnoten[i];
                        besteB = wurzelKnoten[j];
                    }
                }
            }

            if (besteA == null || besteAehnlichkeit < 0.1f) return new();

            var kandidaten = new List<KonzeptKnoten> { besteA, besteB };

            // Drittes Konzept suchen das auch passt
            foreach (var k in wurzelKnoten)
            {
                if (k == besteA || k == besteB) continue;
                float aA = BerechneNamensAehnlichkeit(k.name, besteA.name);
                float aB = BerechneNamensAehnlichkeit(k.name, besteB.name);
                if ((aA + aB) / 2 > besteAehnlichkeit * 0.5f)
                {
                    kandidaten.Add(k);
                    if (kandidaten.Count >= 4) break;
                }
            }

            return kandidaten;
        }

        private float BerechneNamensAehnlichkeit(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0f;
            var worteA = a.ToLowerInvariant()
                .Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet();
            var worteB = b.ToLowerInvariant()
                .Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet();
            if (worteA.Count == 0 || worteB.Count == 0) return 0f;
            int overlap = worteA.Intersect(worteB).Count();
            return (float)overlap / Math.Max(worteA.Count, worteB.Count);
        }

        private void AktualisiereEbenen(KonzeptKnoten kn, int neueEbene)
        {
            kn.abstraktionsEbene = neueEbene;
            foreach (var kindId in kn.kinderIds)
            {
                if (knoten.TryGetValue(kindId, out var kind))
                    AktualisiereEbenen(kind, neueEbene + 1);
            }
        }

        private string BaueAbstraktionsPrompt(List<KonzeptKnoten> kandidaten)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Folgende Konzepte sollen unter einem Oberbegriff zusammengefasst werden:\n");
            foreach (var k in kandidaten)
            {
                var konzept = revision.GetKonzept(k.konzeptId);
                string def = konzept?.aktuelleDefinition ?? "(keine Definition)";
                sb.AppendLine($"- {k.name}: {def}");
            }
            sb.AppendLine("\nGibt es einen sinnvollen Oberbegriff?");
            return sb.ToString();
        }

        private string BaueSpaltungsPrompt(KonzeptKnoten k, List<Erfahrung> erfs)
        {
            var sb = new System.Text.StringBuilder();
            var konzept = revision.GetKonzept(k.konzeptId);
            sb.AppendLine($"Konzept '{k.name}' ist zu breit (deckt {erfs.Count} verschiedene Erfahrungen ab).");
            sb.AppendLine($"Definition: {konzept?.aktuelleDefinition ?? "(keine)"}");
            sb.AppendLine($"\nBeispiel-Erfahrungen:");
            foreach (var e in erfs.Take(8))
                sb.AppendLine($"- {e.aktion}: {e.ergebnis} (Belohnung: {e.belohnung:F2})");
            sb.AppendLine($"\nIn welche Unterkategorien laesst sich '{k.name}' sinnvoll aufteilen?");
            return sb.ToString();
        }

        private List<(string name, string definition)> ParseUnterkategorien(string json)
        {
            var ergebnis = new List<(string, string)>();
            // Einfacher Parser fuer "unterkategorien"-Array
            int arrStart = json.IndexOf("[", StringComparison.Ordinal);
            int arrEnd = json.LastIndexOf("]", StringComparison.Ordinal);
            if (arrStart < 0 || arrEnd < 0) return ergebnis;

            string arrContent = json.Substring(arrStart + 1, arrEnd - arrStart - 1);
            // Jedes Objekt { ... } finden
            int pos = 0;
            while (pos < arrContent.Length)
            {
                int objStart = arrContent.IndexOf("{", pos, StringComparison.Ordinal);
                if (objStart < 0) break;
                int objEnd = arrContent.IndexOf("}", objStart, StringComparison.Ordinal);
                if (objEnd < 0) break;

                string obj = arrContent.Substring(objStart, objEnd - objStart + 1);
                string name = ExtractJsonString(obj, "name");
                string def = ExtractJsonString(obj, "definition");
                if (!string.IsNullOrEmpty(name))
                    ergebnis.Add((name, def ?? name));

                pos = objEnd + 1;
            }
            return ergebnis;
        }

        // ===========================================================
        //  PERSISTENZ
        // ===========================================================

        private void SpeichereAufDisk()
        {
            try
            {
                string json = JsonUtility.ToJson(
                    new KonzeptBaumDaten
                    {
                        knoten = knoten.Values.ToList(),
                        wurzeln = new List<string>(wurzeln)
                    }, true);
                string pfad = System.IO.Path.Combine(Application.persistentDataPath, SPEICHER_PFAD);
                System.IO.File.WriteAllText(pfad, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KonzeptBaum] Speichern fehlgeschlagen: {ex.Message}");
            }
        }

        private void LadeVonDisk()
        {
            try
            {
                string pfad = System.IO.Path.Combine(Application.persistentDataPath, SPEICHER_PFAD);
                if (!System.IO.File.Exists(pfad)) return;
                string json = System.IO.File.ReadAllText(pfad);
                var daten = JsonUtility.FromJson<KonzeptBaumDaten>(json);
                if (daten?.knoten != null)
                {
                    knoten = daten.knoten.ToDictionary(k => k.konzeptId, k => k);
                    wurzeln = daten.wurzeln ?? new();
                    Debug.Log($"[KonzeptBaum] {knoten.Count} Knoten geladen, {wurzeln.Count} Wurzeln.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KonzeptBaum] Laden fehlgeschlagen: {ex.Message}");
            }
        }

        [Serializable]
        private class KonzeptBaumDaten
        {
            public List<KonzeptKnoten> knoten = new();
            public List<string> wurzeln = new();
        }

        // --- JSON-Helfer ---

        private static string ExtractJsonString(string json, string key)
        {
            string pattern = $"\"{key}\"";
            int idx = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            int colonIdx = json.IndexOf(':', idx + pattern.Length);
            if (colonIdx < 0) return null;
            int quoteStart = json.IndexOf('"', colonIdx + 1);
            if (quoteStart < 0) return null;
            int quoteEnd = json.IndexOf('"', quoteStart + 1);
            while (quoteEnd > 0 && json[quoteEnd - 1] == '\\')
                quoteEnd = json.IndexOf('"', quoteEnd + 1);
            if (quoteEnd < 0) return null;
            return json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
        }
    }
}
