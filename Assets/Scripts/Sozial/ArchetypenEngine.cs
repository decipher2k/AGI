using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BilligAGI.Modelle;
using BilligAGI.Kern;
using UnityEngine;

namespace BilligAGI.Sozial
{
    /// <summary>
    /// Instanzbasierte Archetypen-Erkennung.
    ///
    /// Erkennung geschieht durch drei Quellen:
    /// 1. Aehnlichkeit zu vergangenen Instanzen im gleichen Kontext
    /// 2. Seed-Hypothesen als Fallback (wenn noch keine Instanzen existieren)
    /// 3. Emergente Muster (wenn nichts passt, aber etwas Neues sichtbar wird)
    ///
    /// Jede Erkennung erzeugt eine ArchetypInstanz — ein konkretes episodisches
    /// Gedaechtnis-Element. Nicht eine Zaehler-Erhoehung.
    /// </summary>
    public class ArchetypenEngine
    {
        private readonly ArchetypenGedaechtnis gedaechtnis;
        private readonly LLMAdapter llm;
        private readonly KonzeptRevision konzeptRevision;
        private Dictionary<string, string> aktiveZuordnungen; // entitaetId → archetypName

        public ArchetypenEngine(ArchetypenGedaechtnis gedaechtnis, LLMAdapter llm, KonzeptRevision konzeptRevision = null)
        {
            this.gedaechtnis = gedaechtnis;
            this.llm = llm;
            this.konzeptRevision = konzeptRevision;
            aktiveZuordnungen = new Dictionary<string, string>();
        }

        /// <summary>
        /// Erkennt ein archetypisches Muster in einer Situation.
        /// Gibt eine neue ArchetypInstanz zurueck (nicht einen globalen Archetyp).
        ///
        /// Der Prompt enthaelt:
        /// - Vergangene Instanzen im gleichen Kontext (Erfahrung, nicht Definition)
        /// - Seed-Hypothesen als Orientierung
        /// - Die Frage ob sich hier etwas Neues zeigt
        /// </summary>
        public async Task<ArchetypInstanz> ErkenneArchetyp(
            string situation, string entitaetId = null,
            List<Erfahrung> kontext = null, string kontextCluster = null)
        {
            // Kontext-Cluster automatisch bestimmen wenn nicht angegeben
            if (string.IsNullOrEmpty(kontextCluster))
                kontextCluster = BestimmeKontextCluster(situation);

            // --- Prompt aufbauen: Erfahrung vor Theorie ---
            string promptTeile = $"Situation: \"{situation}\"\n";

            // 1. Vergangene Instanzen im gleichen Kontext (DAS ist das episodische Gedaechtnis)
            var letzteInstanzen = gedaechtnis.LetzteInstanzen(8);
            if (letzteInstanzen.Count > 0)
            {
                promptTeile += "\nBisherige Erkennungen (juengste zuerst):\n";
                foreach (var inst in letzteInstanzen)
                {
                    promptTeile += $"- [{inst.kontextCluster}] {inst.archetypName} ({inst.aspekt}): " +
                        $"\"{inst.situation}\" — {inst.interpretation}\n";
                }
            }

            // 2. Falls es konvergierte Bedeutungen gibt, diese zeigen
            var alleNamen = gedaechtnis.AlleArchetypNamen();
            var bedeutungenImKontext = new List<string>();
            foreach (var name in alleNamen)
            {
                var bed = gedaechtnis.GetKontextBedeutung(name, kontextCluster);
                if (bed != null)
                    bedeutungenImKontext.Add($"- {name} [in '{kontextCluster}']: {bed}");
            }
            if (bedeutungenImKontext.Count > 0)
            {
                promptTeile += $"\nKonvergierte Bedeutungen im Kontext '{kontextCluster}':\n";
                promptTeile += string.Join("\n", bedeutungenImKontext) + "\n";
            }

            // 3. Seed-Hypothesen als Orientierung (NICHT als Wahrheit)
            var seeds = gedaechtnis.AlleSeedArchetypen();
            promptTeile += "\nBekannte Muster-Hypothesen (Orientierung, nicht Dogma):\n";
            foreach (var s in seeds)
                promptTeile += $"- {s.name}: Licht={s.lichtAspekt}, Schatten={s.schattenAspekt}, Motivation={s.motivation}\n";

            // 4. Erfahrungen mit dieser Entitaet
            if (kontext != null && kontext.Count > 0)
            {
                promptTeile += "\nBisherige Erfahrungen mit dieser Entitaet:\n";
                foreach (var e in kontext.TakeLast(5))
                    promptTeile += $"- {e.aktion}: {e.ergebnis}\n";
            }

            string prompt = promptTeile +
                "\nFrage: Zeigt sich in dieser Situation ein archetypisches Muster?\n" +
                "Beurteile anhand des VERHALTENS und KONTEXTS, nicht anhand von Keywords.\n" +
                "Beachte: Das gleiche Muster kann in verschiedenen Kontexten verschiedene Bedeutungen haben.\n" +
                "Wenn sich etwas zeigt das zu KEINEM bekannten Muster passt, beschreibe das neue Muster.\n\n" +
                "Antwort als JSON: {\"archetyp\": \"Name oder KEINER oder NEU\", " +
                "\"aspekt\": \"licht oder schatten\", " +
                "\"verhalten\": \"Was tut der Akteur konkret?\", " +
                "\"interpretation\": \"Warum zeigt sich dieses Muster HIER?\", " +
                "\"kontextMerkmale\": [\"merkmal1\", \"merkmal2\"], " +
                "\"konfidenz\": 0.0-1.0, " +
                "\"neues_muster\": \"nur wenn NEU\"}";

            var antwort = await llm.FreieAnfrage(prompt);
            if (antwort == null) return null;

            try
            {
                var ergebnis = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(antwort.inhalt);
                if (ergebnis == null) return null;

                string erkannterName = ergebnis.GetValueOrDefault("archetyp", "KEINER")?.ToString() ?? "KEINER";
                float konfidenz = 0.5f;
                if (ergebnis.TryGetValue("konfidenz", out var konfObj))
                    float.TryParse(konfObj.ToString().Replace(",", "."),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out konfidenz);

                if (erkannterName == "KEINER") return null;

                if (erkannterName == "NEU")
                {
                    string neuesMuster = ergebnis.GetValueOrDefault("neues_muster", "Unbenanntes Muster")?.ToString();
                    return await VerarbeiteEmergentenArchetyp(neuesMuster, situation, entitaetId, kontextCluster, ergebnis);
                }

                // --- Instanz erzeugen und speichern ---
                string verhalten = ergebnis.GetValueOrDefault("verhalten", "")?.ToString() ?? "";
                string interpretation = ergebnis.GetValueOrDefault("interpretation", "")?.ToString() ?? "";
                string aspekt = ergebnis.GetValueOrDefault("aspekt", "licht")?.ToString() ?? "licht";

                List<string> kontextMerkmale = new List<string>();
                if (ergebnis.TryGetValue("kontextMerkmale", out var merkmaleObj) &&
                    merkmaleObj is Newtonsoft.Json.Linq.JArray merkmaleArr)
                    kontextMerkmale = merkmaleArr.Select(m => m.ToString()).ToList();

                var instanz = gedaechtnis.SpeichereInstanz(
                    erkannterName, situation, verhalten, interpretation,
                    aspekt, entitaetId, kontextCluster, konfidenz, kontextMerkmale);

                // KonzeptRevision informieren
                konzeptRevision?.ZaehleAnwendung($"archetyp_{erkannterName.ToLowerInvariant()}");

                if (!string.IsNullOrEmpty(entitaetId))
                    aktiveZuordnungen[entitaetId] = erkannterName;

                return instanz;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Erkennt alle potentiell aktiven Archetypen in einer komplexen Situation.
        /// Erzeugt eine Instanz pro erkanntem Muster.
        /// </summary>
        public async Task<List<ArchetypInstanz>> ErkenneAlleArchetypen(
            string situation, List<Erfahrung> kontext = null, string kontextCluster = null)
        {
            if (string.IsNullOrEmpty(kontextCluster))
                kontextCluster = BestimmeKontextCluster(situation);

            var ergebnis = new List<ArchetypInstanz>();
            var alleNamen = gedaechtnis.AlleArchetypNamen();

            // Kontext aus letzten Instanzen
            var letzteInstanzen = gedaechtnis.LetzteInstanzen(5);
            string instanzKontext = "";
            if (letzteInstanzen.Count > 0)
            {
                instanzKontext = "\nLetzte Erkennungen:\n" +
                    string.Join("\n", letzteInstanzen.Select(i =>
                        $"- {i.archetypName} in '{i.kontextCluster}': {i.interpretation}"));
            }

            string prompt =
                $"Situation: \"{situation}\"\n{instanzKontext}\n" +
                $"Bekannte Archetyp-Muster: {string.Join(", ", alleNamen)}\n" +
                $"Welche archetypischen Muster sind in dieser Situation GLEICHZEITIG aktiv?\n" +
                $"Pro Muster: Name, Aspekt (licht/schatten), kurze Begruendung.\n" +
                $"Antwort als JSON-Array: [{{\"name\": \"...\", \"aspekt\": \"...\", \"begruendung\": \"...\"}}]\n" +
                $"Wenn keiner passt: []";

            var antwort = await llm.FreieAnfrage(prompt);
            if (antwort == null) return ergebnis;

            try
            {
                var erkannte = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(antwort.inhalt);
                if (erkannte == null) return ergebnis;

                foreach (var e in erkannte)
                {
                    string name = e.GetValueOrDefault("name", "");
                    string aspekt = e.GetValueOrDefault("aspekt", "licht");
                    string begruendung = e.GetValueOrDefault("begruendung", "");

                    var instanz = gedaechtnis.SpeichereInstanz(
                        name, situation, "", begruendung, aspekt,
                        null, kontextCluster, 0.5f, new List<string>());

                    konzeptRevision?.ZaehleAnwendung($"archetyp_{name.ToLowerInvariant()}");
                    ergebnis.Add(instanz);
                }
            }
            catch { }

            return ergebnis;
        }

        /// <summary>
        /// Verarbeitet einen emergenten Archetyp und speichert die erste Instanz.
        /// </summary>
        private async Task<ArchetypInstanz> VerarbeiteEmergentenArchetyp(
            string musterBeschreibung, string situation,
            string entitaetId, string kontextCluster,
            Dictionary<string, object> erkennungsDaten)
        {
            string prompt =
                $"Ein neues archetypisches Muster wurde beobachtet:\n" +
                $"Situation: \"{situation}\"\n" +
                $"Beobachtetes Muster: \"{musterBeschreibung}\"\n\n" +
                $"Strukturiere dieses neue Muster als Archetyp.\n" +
                $"JSON: {{\"name\": \"...\", \"lichtAspekt\": \"...\", \"schattenAspekt\": \"...\", " +
                $"\"motivation\": \"...\", \"prototypischeVerhaltensweisen\": [\"...\", \"...\"]}}";

            var antwort = await llm.FreieAnfrage(prompt);
            if (antwort == null) return null;

            try
            {
                var daten = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(antwort.inhalt);
                string name = daten.GetValueOrDefault("name", "Unbenannt")?.ToString() ?? "Unbenannt";
                string licht = daten.GetValueOrDefault("lichtAspekt", "")?.ToString() ?? "";
                string schatten = daten.GetValueOrDefault("schattenAspekt", "")?.ToString() ?? "";
                string motivation = daten.GetValueOrDefault("motivation", "")?.ToString() ?? "";

                List<string> prototypen = new List<string>();
                if (daten.TryGetValue("prototypischeVerhaltensweisen", out var v) &&
                    v is Newtonsoft.Json.Linq.JArray arr)
                    prototypen = arr.Select(x => x.ToString()).ToList();

                // Seed registrieren (als Hypothese)
                gedaechtnis.RegistriereEmergentenArchetyp(name, licht, schatten, motivation, prototypen);

                // Erste Instanz speichern
                string aspekt = erkennungsDaten.GetValueOrDefault("aspekt", "licht")?.ToString() ?? "licht";
                string interpretation = erkennungsDaten.GetValueOrDefault("interpretation", musterBeschreibung)?.ToString() ?? musterBeschreibung;

                var instanz = gedaechtnis.SpeichereInstanz(
                    name, situation, "", interpretation, aspekt,
                    entitaetId, kontextCluster, 0.4f, new List<string>());

                // KonzeptRevision registrieren
                if (konzeptRevision != null)
                {
                    var konzept = new Konzept
                    {
                        id = $"archetyp_{name.ToLowerInvariant()}",
                        name = name,
                        typ = KonzeptTyp.Archetyp,
                        aktuelleDefinition = $"Emergent: {musterBeschreibung}",
                        drift = DriftKlassifikation.ABGELEITET
                    };
                    konzeptRevision.RegistriereKonzept(konzept);
                }

                Debug.Log($"[ArchetypenEngine] Emergenter Archetyp: {name}");
                return instanz;
            }
            catch
            {
                return null;
            }
        }

        public string GetAktuellerArchetyp(string entitaetId)
        {
            return aktiveZuordnungen.TryGetValue(entitaetId, out var name) ? name : null;
        }

        /// <summary>
        /// Spannungsanalyse zwischen zwei Archetypen — nutzt kontext-spezifische Bedeutungen.
        /// </summary>
        public async Task<string> AnalysiereSpannung(string archetyp1, string archetyp2, string kontextCluster = null)
        {
            var a1 = gedaechtnis.GetSeed(archetyp1);
            var a2 = gedaechtnis.GetSeed(archetyp2);
            if (a1 == null || a2 == null) return "Unbekannte Archetypen.";

            if (a1.gegenarchetyp == a2.name || a2.gegenarchetyp == a1.name)
                return $"Starke Spannung: {a1.name} vs {a2.name} (Gegensatzpaar). " +
                       $"Motivations-Konflikt: '{a1.motivation}' vs '{a2.motivation}'.";

            string bedeutung1 = gedaechtnis.GetBesteBeschreibung(a1.name, kontextCluster);
            string bedeutung2 = gedaechtnis.GetBesteBeschreibung(a2.name, kontextCluster);

            string prompt =
                $"Archetyp A '{a1.name}': {bedeutung1}\n" +
                $"Archetyp B '{a2.name}': {bedeutung2}\n" +
                $"Wie stehen diese Muster zueinander? Spannung, Allianz, oder neutral? " +
                $"Antworte in 1-2 Saetzen.";

            var antwort = await llm.FreieAnfrage(prompt);
            return antwort?.inhalt ?? $"Dynamik zwischen {a1.name} und {a2.name} unklar.";
        }

        public (string lichtAspekt, string schattenAspekt) GetDualitaet(string entitaetId)
        {
            var name = GetAktuellerArchetyp(entitaetId);
            if (name == null) return (null, null);
            var arch = gedaechtnis.GetSeed(name);
            return (arch?.lichtAspekt, arch?.schattenAspekt);
        }

        /// <summary>
        /// Bestimmt den Kontext-Cluster einer Situation (einfache Heuristik).
        /// </summary>
        private string BestimmeKontextCluster(string situation)
        {
            if (string.IsNullOrEmpty(situation)) return "allgemein";
            string lower = situation.ToLowerInvariant();

            if (lower.Contains("kampf") || lower.Contains("gefahr") || lower.Contains("verletzt") ||
                lower.Contains("flucht") || lower.Contains("angriff"))
                return "physik";

            if (lower.Contains("sprechen") || lower.Contains("gruppe") || lower.Contains("vertrauen") ||
                lower.Contains("luege") || lower.Contains("hilfe") || lower.Contains("zusammen"))
                return "sozial";

            if (lower.Contains("tod") || lower.Contains("sinn") || lower.Contains("warum") ||
                lower.Contains("allein") || lower.Contains("zweck"))
                return "existenziell";

            if (lower.Contains("entdecken") || lower.Contains("lernen") || lower.Contains("verstehen") ||
                lower.Contains("wissen") || lower.Contains("erkennen"))
                return "epistemisch";

            return "allgemein";
        }

        public ArchetypenGedaechtnis GetGedaechtnis() => gedaechtnis;
    }
}
