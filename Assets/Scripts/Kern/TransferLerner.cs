using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using BilligAGI.Modelle;
using BilligAGI.Gedaechtnis;

namespace BilligAGI.Kern
{
    // ============================================================
    //  TransferLerner — Abstrahiert Erfahrungsmuster zu Schemata
    //  und wendet sie in neuen Domaenen an.
    //
    //  Pipeline:
    //  1. SchemaMining: Erfahrungen → abstrakte Regeln (Schemata)
    //  2. SchemaMatching: Neue Situation → passende Schemata finden
    //  3. SchemaAnwendung: Schema → konkrete Handlungsempfehlung
    //  4. SchemaUpdate: Ergebnis → Konfidenz anpassen
    // ============================================================

    [Serializable]
    public class TransferSchema
    {
        public string id;
        public string name;                             // Menschenlesbar: "Greif-Transfer"
        public string abstrakteRegel;                   // "Wenn [Typ-X Situation] → [Typ-Y Aktion] → [Typ-Z Ergebnis]"
        public string quellDomaene;                     // z.B. "physik_manipulation"
        public List<string> angewandteDomaenen = new(); // Wo schon transferiert
        public float konfidenz;                         // 0–1
        public int anwendungen;
        public int erfolge;
        public float erfolgsRate => anwendungen > 0 ? (float)erfolge / anwendungen : 0f;

        // Abstrakte Muster (domaenenunabhaengig)
        public string bedingungsMuster;                 // z.B. "objekt_nah AND greifbar"
        public string aktionsMuster;                    // z.B. "GREIFEN → BEWEGEN → ABLEGEN"
        public string ergebnisMuster;                   // z.B. "objekt_an_zielort"
        public List<string> kausalStruktur = new();     // Abstrakte Kausalkette

        public List<string> quellErfahrungsIds = new();
        public string zeitstempel;

        public TransferSchema()
        {
            id = Guid.NewGuid().ToString("N").Substring(0, 8);
            zeitstempel = DateTime.UtcNow.ToString("o");
        }
    }

    [Serializable]
    public class SchemaAnwendung
    {
        public string schemaId;
        public string zielDomaene;
        public string konkreteAktion;                   // Uebersetzte Aktion fuer aktuelle Domaene
        public float vorhergesagteErfolgsChance;
        public string begruendung;                      // Warum dieses Schema passt
    }

    [Serializable]
    public class TransferErgebnis
    {
        public bool schemaGefunden;
        public List<SchemaAnwendung> anwendungen = new();
        public string zusammenfassung;
    }

    public class TransferLerner
    {
        private readonly LLMAdapter llm;
        private readonly ErfahrungsSpeicher erfahrungen;
        private readonly AnalogieEngine analogie;
        private readonly KausalGraph kausalGraph;
        private readonly SubsymbolikKernel subsymbolik;
        private readonly InstanzClusterer clusterer;
        private readonly AGIConfig config;

        private List<TransferSchema> schemata = new();
        private int zyklusSeitLetztemMining;

        private const string SPEICHER_PFAD = "transfer_schemata.json";
        private const int MIN_ERFAHRUNGEN_PRO_MUSTER = 3;
        private const int MINING_INTERVALL = 100; // Alle N Zyklen

        // ---- LLM-Prompts ----
        private const string EXTRACT_SYSTEM = @"Du bist ein Schema-Extraktor. Analysiere die Erfahrungen und extrahiere ABSTRAKTE, 
domaenenunabhaengige Handlungsmuster. Jedes Schema soll uebertragbar sein auf 
voellig andere Situationen mit aehnlicher STRUKTUR (nicht aehnlichem Inhalt).

Antworte als JSON-Array:
[{
  ""name"": ""kurzer Name"",
  ""abstrakteRegel"": ""Wenn [abstrakte Bedingung] dann [abstrakte Aktion] fuehrt zu [abstraktes Ergebnis]"",
  ""bedingungsMuster"": ""formale Bedingung ohne konkrete Objekte"",
  ""aktionsMuster"": ""Aktionssequenz als Typen"",
  ""ergebnisMuster"": ""erwartetes abstraktes Ergebnis"",
  ""kausalStruktur"": [""ursache1 → wirkung1"", ""wirkung1 → wirkung2""],
  ""konfidenz"": 0.5
}]

Regeln:
- Ersetze konkrete Objekte durch Typvariablen: Kiste→[Objekt], Wald→[Ort], rot→[Eigenschaft]
- Fokussiere auf die STRUKTUR: Was ist das Muster HINTER den Erfahrungen?
- Nur Muster mit >=3 stuetzenden Erfahrungen
- Max 5 Schemata pro Anfrage";

        private const string MATCH_SYSTEM = @"Du bewertest ob ein abstraktes Schema auf eine neue Situation anwendbar ist.
Gegeben: Ein Schema (abstraktes Muster) und eine aktuelle Situation.

Antworte als JSON:
{
  ""passt"": true/false,
  ""konkreteAktion"": ""die konkrete Aktion fuer diese Situation"",
  ""erfolgsChance"": 0.0-1.0,
  ""begruendung"": ""warum das Schema hier passt oder nicht"",
  ""domaenenMapping"": ""[Objekt]→Kiste, [Ort]→Werkstatt""
}

Sei STRENG: Ein Schema passt nur, wenn die STRUKTURELLE Aehnlichkeit hoch ist.
Oberflaechliche Wortaehnlichkeit reicht nicht.";

        public TransferLerner(
            LLMAdapter llm,
            ErfahrungsSpeicher erfahrungen,
            AnalogieEngine analogie,
            KausalGraph kausalGraph,
            SubsymbolikKernel subsymbolik,
            InstanzClusterer clusterer,
            AGIConfig config)
        {
            this.llm = llm;
            this.erfahrungen = erfahrungen;
            this.analogie = analogie;
            this.kausalGraph = kausalGraph;
            this.subsymbolik = subsymbolik;
            this.clusterer = clusterer;
            this.config = config;

            LadeVonDisk();
        }

        // ===========================================================
        //  1. SCHEMA-MINING: Erfahrungen → abstrakte Schemata
        // ===========================================================

        /// <summary>
        /// Analysiert die letzten N Erfahrungen und extrahiert transferierbare Schemata.
        /// Wird periodisch aufgerufen (alle MINING_INTERVALL Zyklen).
        /// </summary>
        public async Task<List<TransferSchema>> SchemaMining(int letztNErfahrungen = 50)
        {
            var alleErfahrungen = erfahrungen.Alle();
            if (alleErfahrungen.Count < MIN_ERFAHRUNGEN_PRO_MUSTER)
                return new List<TransferSchema>();

            // Letzte N nehmen, nach Belohnung sortiert (beste zuerst)
            var relevante = alleErfahrungen
                .OrderByDescending(e => Math.Abs(e.belohnung))
                .Take(letztNErfahrungen)
                .ToList();

            // Schritt 1: Lokal clustern — strukturell aehnliche Erfahrungen gruppieren
            var cluster = ClustereErfahrungen(relevante);

            // Schritt 2: Pro Cluster mit genug Mitgliedern → LLM-basierte Abstraktion
            var neueSchemataNamen = new HashSet<string>(
                schemata.Select(s => s.name.ToLowerInvariant()));
            var neueSchemata = new List<TransferSchema>();

            foreach (var gruppe in cluster.Where(g => g.Count >= MIN_ERFAHRUNGEN_PRO_MUSTER))
            {
                var prompt = BaueExtractionPrompt(gruppe);
                var antwort = await llm.FreieAnfrage(prompt, EXTRACT_SYSTEM);
                if (antwort == null || string.IsNullOrWhiteSpace(antwort.inhalt))
                    continue;

                var extrahierte = ParseSchemata(antwort.inhalt, gruppe);
                foreach (var schema in extrahierte)
                {
                    // Duplikate vermeiden (nach Name)
                    if (neueSchemataNamen.Contains(schema.name.ToLowerInvariant()))
                        continue;
                    // Catastrophic Forgetting Schutz: Bestehende Schemata mit gleichem Namen nicht ueberschreiben
                    if (schemata.Any(s => s.name.Equals(schema.name, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    neueSchemataNamen.Add(schema.name.ToLowerInvariant());
                    neueSchemata.Add(schema);
                }
            }

            // Einfuegen + persistieren
            if (neueSchemata.Count > 0)
            {
                schemata.AddRange(neueSchemata);
                SpeichereAufDisk();
                Debug.Log($"[TransferLerner] {neueSchemata.Count} neue Schemata extrahiert. Gesamt: {schemata.Count}");
            }

            return neueSchemata;
        }

        // ===========================================================
        //  2. SCHEMA-MATCHING: Situation → passende Schemata
        // ===========================================================

        /// <summary>
        /// Findet Schemata die auf die aktuelle Situation anwendbar sein koennten.
        /// Zweistufig: erst lokal filtern, dann LLM-Bewertung.
        /// </summary>
        public async Task<TransferErgebnis> FindeAnwendbareSchemata(
            string situation, float[] zustand, string aktuelleDomaene = null)
        {
            var ergebnis = new TransferErgebnis();
            if (schemata.Count == 0)
            {
                ergebnis.zusammenfassung = "Keine Schemata vorhanden. Mining noch nicht durchgefuehrt.";
                return ergebnis;
            }

            // Phase 1: Lokales Vorfiltern — Keywords + Erfolgsrate
            var kandidaten = VorfiltereSchemata(situation, aktuelleDomaene);
            if (kandidaten.Count == 0)
            {
                ergebnis.zusammenfassung = "Kein Schema passt strukturell zur aktuellen Situation.";
                return ergebnis;
            }

            // Phase 2: LLM bewertet Top-K Kandidaten
            var topK = kandidaten.Take(5).ToList();
            foreach (var schema in topK)
            {
                var anwendung = await BewerteSchemaFuerSituation(schema, situation);
                if (anwendung != null && anwendung.vorhergesagteErfolgsChance > 0.3f)
                    ergebnis.anwendungen.Add(anwendung);
            }

            ergebnis.schemaGefunden = ergebnis.anwendungen.Count > 0;
            ergebnis.zusammenfassung = ergebnis.schemaGefunden
                ? $"{ergebnis.anwendungen.Count} uebertragbare(s) Schema(ta) gefunden."
                : "Schemata vorhanden, aber keines passt strukturell.";

            return ergebnis;
        }

        // ===========================================================
        //  3. SCHEMA-UPDATE: Ergebnis zurueckmelden
        // ===========================================================

        /// <summary>
        /// Aktualisiert Schema-Konfidenz nach Anwendung in neuer Domaene.
        /// </summary>
        public void AktualisiereSchema(string schemaId, bool erfolgreich, string neueDomaene)
        {
            var schema = schemata.FirstOrDefault(s => s.id == schemaId);
            if (schema == null) return;

            schema.anwendungen++;
            if (erfolgreich) schema.erfolge++;

            // Bayesianisches Update: Konfidenz anpassen
            float lernrate = 0.1f;
            float signal = erfolgreich ? 1f : -0.5f;
            schema.konfidenz = Mathf.Clamp01(schema.konfidenz + lernrate * signal);

            // Neue Domaene registrieren
            if (!string.IsNullOrEmpty(neueDomaene) &&
                !schema.angewandteDomaenen.Contains(neueDomaene))
            {
                schema.angewandteDomaenen.Add(neueDomaene);
            }

            // Catastrophic Forgetting Schutz:
            // Multi-Domain Schemata (bereits in >=2 Domaenen erfolgreich) werden geschuetzt
            bool istMultiDomain = schema.angewandteDomaenen.Count >= 2 && schema.erfolgsRate >= 0.3f;
            float minKonfidenz = istMultiDomain ? 0.2f : 0.05f;

            // Schemata mit dauerhaft schlechter Erfolgsrate abwerten — aber nicht unter Minimum
            if (schema.anwendungen >= 10 && schema.erfolgsRate < 0.2f)
            {
                float neueKonfidenz = schema.konfidenz * 0.5f;
                schema.konfidenz = Math.Max(neueKonfidenz, minKonfidenz);
                Debug.Log($"[TransferLerner] Schema '{schema.name}' abgewertet " +
                    $"(Erfolgsrate {schema.erfolgsRate:P0}, Min: {minKonfidenz:F2}" +
                    $"{(istMultiDomain ? ", GESCHUETZT als Multi-Domain" : "")})");
            }

            SpeichereAufDisk();
        }

        // ===========================================================
        //  4. ZYKLUS-HOOK: Periodisches Mining + Transfer-Check
        // ===========================================================

        /// <summary>
        /// Wird jeden AGI-Zyklus aufgerufen. Fuehrt periodisch Mining durch
        /// und prueft ob Transfer fuer aktuelle Situation moeglich ist.
        /// </summary>
        public async Task<TransferErgebnis> ZyklusTick(string input, float[] zustand)
        {
            zyklusSeitLetztemMining++;

            // Periodisches Mining
            if (zyklusSeitLetztemMining >= config.transferMiningIntervall)
            {
                zyklusSeitLetztemMining = 0;
                await SchemaMining(config.transferMiningSampleGroesse);
            }

            // Transfer-Check nur wenn Input vorhanden
            if (string.IsNullOrEmpty(input)) return null;

            return await FindeAnwendbareSchemata(input, zustand);
        }

        // ===========================================================
        //  5. STATUS + REPORTING
        // ===========================================================

        public string GetStatusText()
        {
            if (schemata.Count == 0)
                return "Keine Transfer-Schemata vorhanden.";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Transfer-Schemata: {schemata.Count}");
            sb.AppendLine($"Naechstes Mining in: {config.transferMiningIntervall - zyklusSeitLetztemMining} Zyklen");

            var top = schemata
                .OrderByDescending(s => s.konfidenz)
                .Take(5);

            foreach (var s in top)
            {
                string domaenen = s.angewandteDomaenen.Count > 0
                    ? string.Join(", ", s.angewandteDomaenen)
                    : "(nur Quell-Domaene)";
                sb.AppendLine($"  [{s.konfidenz:F2}] {s.name} — {s.anwendungen}x angewandt, " +
                    $"Erfolg: {s.erfolgsRate:P0}, Domaenen: {domaenen}");
            }
            return sb.ToString();
        }

        public List<TransferSchema> GetAlleSchemata() => new(schemata);

        public int SchemaAnzahl => schemata.Count;

        // ===========================================================
        //  PRIVATE: Clustering
        // ===========================================================

        private List<List<Erfahrung>> ClustereErfahrungen(List<Erfahrung> erfahrungen)
        {
            // Strategie: Gruppiere nach (Aktionstyp + Ergebnis-Polaritaet)
            // Dann innerhalb jeder Gruppe prüfe Kontextaehnlichkeit
            var gruppen = new Dictionary<string, List<Erfahrung>>();

            foreach (var e in erfahrungen)
            {
                // Schluessel: Hauptaktion + Positiv/Negativ
                string hauptAktion = e.aktionenListe?.FirstOrDefault()?.typ.ToString() ?? "unbekannt";
                string polaritaet = e.belohnung > 0 ? "positiv" : (e.belohnung < -0.3f ? "negativ" : "neutral");
                string key = $"{hauptAktion}_{polaritaet}";

                if (!gruppen.ContainsKey(key))
                    gruppen[key] = new List<Erfahrung>();
                gruppen[key].Add(e);
            }

            // Zusaetzlich: Subsymbolisches Clustering fuer versteckte Muster
            var latente = erfahrungen
                .Select(e => subsymbolik.EmbeddeKontext(e))
                .Where(l => l != null)
                .ToList();

            if (latente.Count >= 6)
            {
                var vektoren = latente.Select(l => l.vektor).ToList();
                int optK = clusterer.OptimaleClusterAnzahl(vektoren, Math.Min(8, latente.Count / 2));
                var assignments = clusterer.KMeans(vektoren, optK);

                // Subsymbolische Cluster als zusaetzliche Gruppen
                for (int k = 0; k < optK; k++)
                {
                    var mitglieder = new List<Erfahrung>();
                    for (int i = 0; i < assignments.Length && i < erfahrungen.Count; i++)
                    {
                        if (assignments[i] == k)
                            mitglieder.Add(erfahrungen[i]);
                    }
                    if (mitglieder.Count >= MIN_ERFAHRUNGEN_PRO_MUSTER)
                    {
                        string key = $"latent_cluster_{k}";
                        if (!gruppen.ContainsKey(key))
                            gruppen[key] = mitglieder;
                    }
                }
            }

            return gruppen.Values.ToList();
        }

        // ===========================================================
        //  PRIVATE: LLM-basierte Extraktion
        // ===========================================================

        private string BaueExtractionPrompt(List<Erfahrung> gruppe)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Analysiere diese {gruppe.Count} strukturell aehnlichen Erfahrungen:");
            sb.AppendLine();

            foreach (var e in gruppe.Take(15))
            {
                sb.AppendLine($"- Aktion: {e.aktion}");
                sb.AppendLine($"  Kontext: {e.kontext}");
                sb.AppendLine($"  Ergebnis: {e.ergebnis}");
                sb.AppendLine($"  Belohnung: {e.belohnung:F2}");
                if (e.konzepte?.Count > 0)
                    sb.AppendLine($"  Tags: {string.Join(", ", e.konzepte)}");
                sb.AppendLine();
            }

            sb.AppendLine("Extrahiere abstrakte, uebertragbare Handlungsschemata.");
            return sb.ToString();
        }

        private List<TransferSchema> ParseSchemata(string json, List<Erfahrung> quellGruppe)
        {
            var result = new List<TransferSchema>();

            try
            {
                // JSON-Array aus dem LLM-Output extrahieren
                int start = json.IndexOf('[');
                int end = json.LastIndexOf(']');
                if (start < 0 || end <= start) return result;
                string arrayJson = json.Substring(start, end - start + 1);

                // Minimale JSON-Verarbeitung ohne Abhängigkeit
                var items = JsonArraySplit(arrayJson);
                foreach (var item in items)
                {
                    var schema = new TransferSchema
                    {
                        name = ExtractJsonString(item, "name") ?? "Unbenannt",
                        abstrakteRegel = ExtractJsonString(item, "abstrakteRegel") ?? "",
                        bedingungsMuster = ExtractJsonString(item, "bedingungsMuster") ?? "",
                        aktionsMuster = ExtractJsonString(item, "aktionsMuster") ?? "",
                        ergebnisMuster = ExtractJsonString(item, "ergebnisMuster") ?? "",
                        konfidenz = ExtractJsonFloat(item, "konfidenz", 0.5f),
                        quellDomaene = ErmittleDomaene(quellGruppe),
                        quellErfahrungsIds = quellGruppe.Select(e => e.id).ToList()
                    };

                    // Kausale Struktur parsen
                    string kausalStr = ExtractJsonArray(item, "kausalStruktur");
                    if (!string.IsNullOrEmpty(kausalStr))
                    {
                        schema.kausalStruktur = kausalStr
                            .Split(new[] { "\",\"", "\", \"" }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim('"', '[', ']', ' '))
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .ToList();
                    }

                    // Schema auch im KausalGraph registrieren
                    foreach (var kausal in schema.kausalStruktur)
                    {
                        var teile = kausal.Split(new[] { "→", "->" }, StringSplitOptions.None);
                        if (teile.Length == 2)
                        {
                            kausalGraph.FuegeKausalitaetHinzu(
                                teile[0].Trim(), teile[1].Trim(),
                                schema.konfidenz, "prinzip");
                        }
                    }

                    result.Add(schema);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TransferLerner] Fehler beim Parsen der Schemata: {ex.Message}");
            }

            return result;
        }

        // ===========================================================
        //  PRIVATE: Vorfilter + LLM-Bewertung
        // ===========================================================

        private List<TransferSchema> VorfiltereSchemata(string situation, string aktuelleDomaene)
        {
            // Score pro Schema: Keyword-Overlap + Konfidenz + Erfolgsrate
            var scored = new List<(TransferSchema schema, float score)>();

            string situationLower = situation.ToLowerInvariant();
            var situationWords = situationLower
                .Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet();

            foreach (var schema in schemata)
            {
                float score = 0f;

                // 1. Keyword-Overlap zwischen Situation und Schema
                var schemaWords = $"{schema.abstrakteRegel} {schema.bedingungsMuster} {schema.aktionsMuster}"
                    .ToLowerInvariant()
                    .Split(new[] { ' ', ',', '.', '→', '[', ']' }, StringSplitOptions.RemoveEmptyEntries)
                    .ToHashSet();

                int overlap = situationWords.Intersect(schemaWords).Count();
                score += overlap * 0.2f;

                // 2. Konfidenz-Bonus
                score += schema.konfidenz * 0.3f;

                // 3. Erfolgsrate-Bonus
                if (schema.anwendungen > 0)
                    score += schema.erfolgsRate * 0.3f;

                // 4. Cross-Domain Bonus: Schema wurde schon in mehreren Domaenen erfolgreich
                if (schema.angewandteDomaenen.Count > 1)
                    score += 0.2f;

                // 5. Neue-Domaene Bonus: Schema wurde HIER noch nie angewandt
                if (!string.IsNullOrEmpty(aktuelleDomaene) &&
                    !schema.angewandteDomaenen.Contains(aktuelleDomaene) &&
                    schema.quellDomaene != aktuelleDomaene)
                {
                    score += 0.15f; // Echter Transfer!
                }

                if (score > 0.1f)
                    scored.Add((schema, score));
            }

            return scored
                .OrderByDescending(x => x.score)
                .Select(x => x.schema)
                .ToList();
        }

        private async Task<SchemaAnwendung> BewerteSchemaFuerSituation(
            TransferSchema schema, string situation)
        {
            string prompt = $"Schema:\n" +
                $"  Name: {schema.name}\n" +
                $"  Regel: {schema.abstrakteRegel}\n" +
                $"  Bedingung: {schema.bedingungsMuster}\n" +
                $"  Aktion: {schema.aktionsMuster}\n" +
                $"  Ergebnis: {schema.ergebnisMuster}\n" +
                $"  Bisherige Erfolgsrate: {schema.erfolgsRate:P0} ({schema.anwendungen}x)\n\n" +
                $"Aktuelle Situation:\n  {situation}\n\n" +
                $"Ist dieses Schema hier anwendbar?";

            var antwort = await llm.FreieAnfrage(prompt, MATCH_SYSTEM);
            if (antwort == null || string.IsNullOrWhiteSpace(antwort.inhalt))
                return null;

            try
            {
                string json = antwort.inhalt;
                bool passt = json.Contains("\"passt\": true") || json.Contains("\"passt\":true");
                if (!passt) return null;

                return new SchemaAnwendung
                {
                    schemaId = schema.id,
                    zielDomaene = ErmittleDomaeneAusText(situation),
                    konkreteAktion = ExtractJsonString(json, "konkreteAktion") ?? "",
                    vorhergesagteErfolgsChance = ExtractJsonFloat(json, "erfolgsChance", 0.5f),
                    begruendung = ExtractJsonString(json, "begruendung") ?? ""
                };
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TransferLerner] Schema-Matching Parse-Fehler: {ex.Message}");
                return null;
            }
        }

        // ===========================================================
        //  PRIVATE: Domaenen-Erkennung
        // ===========================================================

        private string ErmittleDomaene(List<Erfahrung> gruppe)
        {
            // Haeufigsten Kontext als Domaene nehmen
            var kontexte = gruppe
                .Where(e => !string.IsNullOrEmpty(e.kontext))
                .GroupBy(e => e.kontext)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            return kontexte?.Key ?? "allgemein";
        }

        private string ErmittleDomaeneAusText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "allgemein";
            string lower = text.ToLowerInvariant();

            // Einfache Keyword-basierte Domaenenerkennung
            if (lower.Contains("greif") || lower.Contains("werf") || lower.Contains("schieb") ||
                lower.Contains("physik") || lower.Contains("objekt"))
                return "physik_manipulation";
            if (lower.Contains("sprech") || lower.Contains("sag") || lower.Contains("frag") ||
                lower.Contains("dialog") || lower.Contains("npc"))
                return "sozial_interaktion";
            if (lower.Contains("navigier") || lower.Contains("geh") || lower.Contains("lauf") ||
                lower.Contains("weg") || lower.Contains("ort"))
                return "navigation";
            if (lower.Contains("plan") || lower.Contains("strateg") || lower.Contains("ziel"))
                return "planung";
            if (lower.Contains("bau") || lower.Contains("erstell") || lower.Contains("konstruier"))
                return "konstruktion";

            return "allgemein";
        }

        // ===========================================================
        //  PRIVATE: Persistenz
        // ===========================================================

        private void SpeichereAufDisk()
        {
            try
            {
                string json = JsonUtility.ToJson(new SchemaListe { schemata = schemata }, true);
                string pfad = System.IO.Path.Combine(Application.persistentDataPath, SPEICHER_PFAD);
                System.IO.File.WriteAllText(pfad, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TransferLerner] Speichern fehlgeschlagen: {ex.Message}");
            }
        }

        private void LadeVonDisk()
        {
            try
            {
                string pfad = System.IO.Path.Combine(Application.persistentDataPath, SPEICHER_PFAD);
                if (!System.IO.File.Exists(pfad)) return;

                string json = System.IO.File.ReadAllText(pfad);
                var liste = JsonUtility.FromJson<SchemaListe>(json);
                if (liste?.schemata != null)
                {
                    schemata = liste.schemata;
                    Debug.Log($"[TransferLerner] {schemata.Count} Schemata von Disk geladen.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TransferLerner] Laden fehlgeschlagen: {ex.Message}");
            }
        }

        [Serializable]
        private class SchemaListe
        {
            public List<TransferSchema> schemata = new();
        }

        // ===========================================================
        //  PRIVATE: Minimaler JSON-Parser (kein Newtonsoft noetig)
        // ===========================================================

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
            return json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1)
                .Replace("\\\"", "\"")
                .Replace("\\n", "\n");
        }

        private static float ExtractJsonFloat(string json, string key, float fallback)
        {
            string pattern = $"\"{key}\"";
            int idx = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return fallback;
            int colonIdx = json.IndexOf(':', idx + pattern.Length);
            if (colonIdx < 0) return fallback;

            int start = colonIdx + 1;
            while (start < json.Length && (json[start] == ' ' || json[start] == '\t'))
                start++;

            int end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '.' || json[end] == '-'))
                end++;

            if (end <= start) return fallback;
            if (float.TryParse(json.Substring(start, end - start),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float val))
                return val;
            return fallback;
        }

        private static string ExtractJsonArray(string json, string key)
        {
            string pattern = $"\"{key}\"";
            int idx = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            int bracketStart = json.IndexOf('[', idx);
            if (bracketStart < 0) return null;
            int bracketEnd = json.IndexOf(']', bracketStart);
            if (bracketEnd < 0) return null;
            return json.Substring(bracketStart, bracketEnd - bracketStart + 1);
        }

        private static List<string> JsonArraySplit(string arrayJson)
        {
            var items = new List<string>();
            int depth = 0;
            int itemStart = -1;

            for (int i = 0; i < arrayJson.Length; i++)
            {
                char c = arrayJson[i];
                if (c == '{')
                {
                    if (depth == 0) itemStart = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && itemStart >= 0)
                    {
                        items.Add(arrayJson.Substring(itemStart, i - itemStart + 1));
                        itemStart = -1;
                    }
                }
            }

            return items;
        }
    }
}
