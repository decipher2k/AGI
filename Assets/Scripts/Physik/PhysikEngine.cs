using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using BilligAGI.Modelle;
using BilligAGI.Kern;
using BilligAGI.Daten;

namespace BilligAGI.Physik
{
    public class PhysikEngine
    {
        private readonly LLMAdapter llm;
        private readonly AGIConfig config;
        private Dictionary<string, PhysikRegel> gelernteRegeln;
        private string regelPfad = "physik_regeln.json";

        public PhysikEngine(LLMAdapter llm, AGIConfig config)
        {
            this.llm = llm;
            this.config = config;
            gelernteRegeln = new Dictionary<string, PhysikRegel>();
            LadeRegeln();
        }

        public PlausibilitaetsErgebnis PruefePlausibilitaet(string aussage)
        {
            var ergebnis = new PlausibilitaetsErgebnis { aussage = aussage };

            // 1. Gelernte Regeln pruefen
            string key = aussage.Trim().ToLowerInvariant();
            foreach (var kvp in gelernteRegeln)
            {
                if (key.Contains(kvp.Key) || kvp.Key.Contains(key))
                {
                    ergebnis.plausibel = kvp.Value.ergebnis == "bestaetigt";
                    ergebnis.konfidenz = kvp.Value.konfidenz;
                    ergebnis.erklaerung = kvp.Value.beschreibung;
                    ergebnis.basierung = "gelernte_regel";
                    return ergebnis;
                }
            }

            // 2. Keine Regel gefunden → markieren fuer LLM-Pruefung
            ergebnis.plausibel = false;
            ergebnis.konfidenz = 0f;
            ergebnis.erklaerung = "Keine gelernte Regel. LLM-Pruefung oder Experiment erforderlich.";
            ergebnis.basierung = "keine";
            ergebnis.experimentVorschlag = $"Experiment: {aussage} in Unity testen.";
            return ergebnis;
        }

        public async Task<PlausibilitaetsErgebnis> PruefePlausibilitaetMitLLM(string aussage)
        {
            var lokal = PruefePlausibilitaet(aussage);
            if (lokal.konfidenz >= config.physikKonfidenzSchwelle)
                return lokal;

            // LLM-Einschaetzung
            string prompt = $"Ist folgende Aussage physikalisch plausibel? Antworte mit JSON: " +
                $"{{\"plausibel\": true/false, \"konfidenz\": 0.0-1.0, \"erklaerung\": \"...\"}}\n\nAussage: {aussage}";

            var antwort = await llm.FreieAnfrage(prompt);
            if (antwort == null) return lokal;

            try
            {
                var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<PlausibilitaetsErgebnis>(antwort.inhalt);
                if (parsed != null)
                {
                    parsed.aussage = aussage;
                    parsed.basierung = "llm";
                    if (parsed.konfidenz < config.physikKonfidenzSchwelle)
                        parsed.experimentVorschlag = $"Experiment empfohlen: {aussage}";
                    return parsed;
                }
            }
            catch { }

            return lokal;
        }

        public async Task<ExperimentErgebnis> FuehreExperimentAus(
            string hypothese,
            Welt.WeltController weltController,
            Welt.AktionsController aktionsController,
            Sensorik.SensorSuite sensorSuite)
        {
            var ergebnis = new ExperimentErgebnis
            {
                hypothese = hypothese,
                zeitstempel = System.DateTime.UtcNow.ToString("o")
            };

            Debug.Log($"[PhysikEngine] Experiment: {hypothese}");

            // LLM generiert Experimentplan
            string planPrompt = $"Generiere einen einfachen Unity-Experimentplan fuer: '{hypothese}'. " +
                $"Format JSON: {{\"schritte\": [\"schritt1\", \"schritt2\"], \"beobachtungsZiel\": \"was zu messen ist\"}}";

            var planAntwort = await llm.FreieAnfrage(planPrompt);

            // Sensorbeobachtung vorher
            ergebnis.sensorDatenVorher = sensorSuite?.AktualisiereSensoren();

            // Warten auf Physik-Simulation (ein paar Frames)
            await Task.Yield();
            await Task.Yield();

            // Sensorbeobachtung nachher
            ergebnis.sensorDatenNachher = sensorSuite?.AktualisiereSensoren();

            // Ergebnis durch LLM interpretieren lassen
            string interpretPrompt = $"Hypothese: {hypothese}\n" +
                $"Beobachtung vorher: {Newtonsoft.Json.JsonConvert.SerializeObject(ergebnis.sensorDatenVorher)}\n" +
                $"Beobachtung nachher: {Newtonsoft.Json.JsonConvert.SerializeObject(ergebnis.sensorDatenNachher)}\n" +
                $"War die Hypothese korrekt? Antworte: {{\"bestaetigt\": true/false, \"beschreibung\": \"...\"}}";

            var interpretAntwort = await llm.FreieAnfrage(interpretPrompt);
            if (interpretAntwort != null)
            {
                ergebnis.beobachtung = interpretAntwort.inhalt;
                ergebnis.bestaetigt = interpretAntwort.inhalt.Contains("true");
            }

            // Regel extrahieren und speichern
            var regelExtraktor = new RegelExtraktor(llm);
            var regel = await regelExtraktor.ExtrahiereRegel(ergebnis);
            if (regel != null)
            {
                SpeichereRegel(regel);
            }

            return ergebnis;
        }

        public void SpeichereRegel(PhysikRegel regel)
        {
            string key = regel.beschreibung.Trim().ToLowerInvariant();
            if (gelernteRegeln.ContainsKey(key))
            {
                gelernteRegeln[key].konfidenz = Mathf.Min(1f, gelernteRegeln[key].konfidenz + 0.1f);
                gelernteRegeln[key].bestaetigungen++;
            }
            else
            {
                gelernteRegeln[key] = regel;
            }
            PersistiereRegeln();
        }

        public int AnzahlGelernterRegeln() => gelernteRegeln.Count;

        private void LadeRegeln()
        {
            try
            {
                var daten = DatenLader.Lade<Dictionary<string, PhysikRegel>>(regelPfad);
                if (daten != null)
                    gelernteRegeln = daten;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PhysikEngine] Konnte Regeln nicht laden ({regelPfad}): {ex.Message}. Starte mit leerem Regelset.");
                gelernteRegeln = new Dictionary<string, PhysikRegel>();
            }
        }

        private void PersistiereRegeln()
        {
            DatenLader.Speichere(regelPfad, gelernteRegeln);
        }
    }
}
