using System.Threading.Tasks;
using BilligAGI.Modelle;
using BilligAGI.Kern;
using UnityEngine;

namespace BilligAGI.Physik
{
    public class RegelExtraktor
    {
        private readonly LLMAdapter llm;

        public RegelExtraktor(LLMAdapter llm)
        {
            this.llm = llm;
        }

        public async Task<PhysikRegel> ExtrahiereRegel(ExperimentErgebnis ergebnis)
        {
            if (ergebnis == null) return null;

            string prompt = $"Extrahiere eine physikalische Regel aus diesem Experimentergebnis.\n" +
                $"Hypothese: {ergebnis.hypothese}\n" +
                $"Beobachtung: {ergebnis.beobachtung}\n" +
                $"Bestaetigt: {ergebnis.bestaetigt}\n\n" +
                $"Antworte mit JSON: {{\"beschreibung\": \"...\", \"tags\": [\"tag1\",\"tag2\"], " +
                $"\"ergebnis\": \"bestaetigt\" oder \"widerlegt\"}}";

            var antwort = await llm.FreieAnfrage(prompt);
            if (antwort == null) return null;

            try
            {
                var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<PhysikRegel>(antwort.inhalt);
                if (parsed != null)
                {
                    parsed.konfidenz = ergebnis.bestaetigt ? 0.6f : 0.3f;
                    parsed.bestaetigungen = 1;
                    parsed.zeitstempel = System.DateTime.UtcNow.ToString("o");
                    return parsed;
                }
            }
            catch { }

            // Fallback: Regel manuell erstellen
            return new PhysikRegel
            {
                beschreibung = ergebnis.hypothese,
                ergebnis = ergebnis.bestaetigt ? "bestaetigt" : "widerlegt",
                konfidenz = ergebnis.bestaetigt ? 0.5f : 0.2f,
                bestaetigungen = 1,
                zeitstempel = System.DateTime.UtcNow.ToString("o"),
                tags = new System.Collections.Generic.List<string> { "auto-extrahiert" }
            };
        }

        public PhysikRegel ExtrahiereAusUnityPhysik(
            string objektTag1, string objektTag2,
            string kontaktTyp, Vector3 ergebnisKraft,
            bool objekt1InWasser, float objekt1Y)
        {
            // Spezifische Unity-basierte Regelextraktion
            string beschreibung;

            if (objekt1InWasser)
            {
                if (objekt1Y > 0f)
                    beschreibung = $"Objekt mit Tag '{objektTag1}' schwimmt auf Wasser.";
                else
                    beschreibung = $"Objekt mit Tag '{objektTag1}' sinkt in Wasser.";
            }
            else if (kontaktTyp == "kollision")
            {
                beschreibung = $"'{objektTag1}' kollidiert mit '{objektTag2}': Kraft={ergebnisKraft.magnitude:F1}.";
            }
            else
            {
                beschreibung = $"'{objektTag1}' interagiert mit '{objektTag2}' ({kontaktTyp}).";
            }

            return new PhysikRegel
            {
                beschreibung = beschreibung,
                ergebnis = "bestaetigt",
                konfidenz = 0.7f,
                bestaetigungen = 1,
                tags = new System.Collections.Generic.List<string> { objektTag1, objektTag2, kontaktTyp },
                zeitstempel = System.DateTime.UtcNow.ToString("o")
            };
        }
    }
}
