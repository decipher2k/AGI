using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BilligAGI.Modelle;
using BilligAGI.Gedaechtnis;
using BilligAGI.Daten;
using UnityEngine;

namespace BilligAGI.Kern
{
    public class KonzeptRevision
    {
        private readonly LLMAdapter llm;
        private readonly ErfahrungsSpeicher speicher;
        private readonly AGIConfig config;
        private Dictionary<string, Konzept> konzepte;
        private List<KonzeptRevisionSchritt> historie;
        private Dictionary<string, int> anwendungsZaehler;

        public KonzeptRevision(LLMAdapter llm, ErfahrungsSpeicher speicher, AGIConfig config)
        {
            this.llm = llm;
            this.speicher = speicher;
            this.config = config;
            konzepte = new Dictionary<string, Konzept>();
            historie = new List<KonzeptRevisionSchritt>();
            anwendungsZaehler = new Dictionary<string, int>();

            LadeHistorie();
        }

        public void RegistriereKonzept(Konzept konzept)
        {
            if (konzept == null || string.IsNullOrEmpty(konzept.id)) return;
            konzepte[konzept.id] = konzept;
            if (!anwendungsZaehler.ContainsKey(konzept.id))
                anwendungsZaehler[konzept.id] = 0;
        }

        public void ZaehleAnwendung(string konzeptId)
        {
            if (anwendungsZaehler.ContainsKey(konzeptId))
                anwendungsZaehler[konzeptId]++;
        }

        public bool SollteRevidiertWerden(string konzeptId)
        {
            if (!anwendungsZaehler.TryGetValue(konzeptId, out int count)) return false;
            return count >= config.konzeptRevisionSchwelle;
        }

        public async Task<KonzeptRevisionErgebnis> Revidiere(string konzeptId)
        {
            if (!konzepte.TryGetValue(konzeptId, out var konzept))
                return null;

            Debug.Log($"[KonzeptRevision] Starte Revision: {konzept.name} (Typ: {konzept.typ})");

            var ergebnis = new KonzeptRevisionErgebnis
            {
                konzeptId = konzeptId,
                zeitstempel = DateTime.UtcNow.ToString("o")
            };

            // Schritt A: Baseline
            string baseline = konzept.ursprungsDefinition ?? konzept.aktuelleDefinition;

            // Erfahrungen mit diesem Konzept sammeln
            var relevante = ErfahrungenMitKonzept(konzeptId);
            if (relevante.Count < 3)
            {
                ergebnis.neueDefinition = baseline;
                ergebnis.drift = DriftKlassifikation.BESTAETIGT;
                ergebnis.driftScore = 0f;
                return ergebnis;
            }

            // Schritt B: Hermeneutischer Zirkel (Passes)
            string aktuelleRevision = baseline;
            for (int pass = 1; pass <= config.konzeptRevisionMaxPasses; pass++)
            {
                string prompt;
                if (pass == 1)
                {
                    prompt = $"Konzept '{konzept.name}': Ausgangsdefinition: \"{baseline}\"\n\n" +
                        $"Hier sind {relevante.Count} Erfahrungen in denen dieses Konzept angewendet wurde:\n";
                    foreach (var e in relevante.Take(15))
                        prompt += $"- {e.aktion}: {e.ergebnis} (Belohnung: {e.belohnung:F1})\n";
                    prompt += $"\nFrage: Passt die Definition? Was TUT '{konzept.name}' in diesen konkreten Situationen? " +
                        $"Antwort in 2-3 Saetzen.";
                }
                else
                {
                    prompt = $"Vorherige Revision (Pass {pass-1}) von '{konzept.name}': \"{aktuelleRevision}\"\n\n" +
                        $"Drueckt die Erfahrung zurueck? Gibt es Faelle wo die Definition nicht passt? " +
                        $"Reformuliere SPEZIFISCHER (nicht allgemeiner). Antworte in 2-3 Saetzen.";
                }

                var antwort = await llm.FreieAnfrage(prompt);
                if (antwort == null) break;

                string neueRevision = antwort.inhalt;

                // Konvergenz-Test
                if (pass > 1)
                {
                    float aehnlichkeit = await MisseDrift(aktuelleRevision, neueRevision);
                    if (aehnlichkeit < 0.1f)
                    {
                        Debug.Log($"[KonzeptRevision] Konvergiert nach Pass {pass}.");
                        break;
                    }
                }

                aktuelleRevision = neueRevision;
                historie.Add(new KonzeptRevisionSchritt
                {
                    konzeptId = konzeptId,
                    pass = pass,
                    definition = aktuelleRevision,
                    zeitstempel = DateTime.UtcNow.ToString("o")
                });
            }

            // Schritt C: Selbstkritik
            string kritik = await SelbstKritik(konzeptId, aktuelleRevision, relevante);

            // Schritt D: Drift messen
            float driftScore = await MisseDrift(baseline, aktuelleRevision);
            var klassifikation = await Klassifiziere(baseline, aktuelleRevision, relevante);

            ergebnis.alteDefinition = baseline;
            ergebnis.neueDefinition = aktuelleRevision;
            ergebnis.driftScore = driftScore;
            ergebnis.drift = klassifikation;
            ergebnis.selbstKritik = kritik;

            // Konzept aktualisieren
            konzept.aktuelleDefinition = aktuelleRevision;
            konzept.driftScore = driftScore;
            konzept.drift = klassifikation;

            // Zaehler zuruecksetzen
            anwendungsZaehler[konzeptId] = 0;

            Persistiere();
            return ergebnis;
        }

        public async Task Rueckpropagiere(KonzeptRevisionErgebnis ergebnis)
        {
            if (ergebnis == null || ergebnis.driftScore < config.konzeptDriftSchwelle) return;

            Debug.Log($"[KonzeptRevision] Rueckpropagation: DriftScore {ergebnis.driftScore:F2}");

            // 1. Erfahrungen markieren
            var betroffene = ErfahrungenMitKonzept(ergebnis.konzeptId);
            speicher.MarkiereFuerNeuauswertung(betroffene.Select(e => e.id).ToList());

            // 2. Abhaengige Konzepte identifizieren
            var abhaengige = AbhaengigeKonzepte(ergebnis.konzeptId);
            foreach (var dep in abhaengige)
            {
                Debug.Log($"[KonzeptRevision] Abhaengiges Konzept vorgemerkt: {dep}");
                // Trigger fuer naechsten Zyklus
                if (anwendungsZaehler.ContainsKey(dep))
                    anwendungsZaehler[dep] = config.konzeptRevisionSchwelle;
            }
        }

        public List<string> AbhaengigeKonzepte(string konzeptId)
        {
            // Einfache Heuristik: Konzepte die im Text des anderen referenziert werden
            var deps = new List<string>();
            if (!konzepte.TryGetValue(konzeptId, out var quelle)) return deps;

            foreach (var kvp in konzepte)
            {
                if (kvp.Key == konzeptId) continue;
                if (kvp.Value.aktuelleDefinition != null &&
                    kvp.Value.aktuelleDefinition.ToLowerInvariant().Contains(quelle.name.ToLowerInvariant()))
                {
                    deps.Add(kvp.Key);
                }
            }
            return deps;
        }

        public List<Erfahrung> ErfahrungenMitKonzept(string konzeptId)
        {
            return speicher.Alle().Where(e =>
                e.konzepte != null && e.konzepte.Contains(konzeptId)).ToList();
        }

        public async Task<float> MisseDrift(string alt, string neu)
        {
            if (string.IsNullOrEmpty(alt) || string.IsNullOrEmpty(neu)) return 1f;
            if (alt == neu) return 0f;

            var prompt = $"Wie gross ist die semantische Verschiebung zwischen diesen Definitionen? " +
                $"Antwort als Zahl 0.0-1.0.\nAlt: \"{alt}\"\nNeu: \"{neu}\"";
            var antwort = await llm.FreieAnfrage(prompt);
            if (antwort != null && float.TryParse(
                antwort.inhalt.Trim().Replace(",", "."),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float score))
            {
                return Mathf.Clamp01(score);
            }
            return 0.5f; // Fallback
        }

        public async Task<DriftKlassifikation> Klassifiziere(string ursprung, string aktuell, List<Erfahrung> evidenz)
        {
            var prompt = $"Klassifiziere die Drift zwischen:\nUrsprung: \"{ursprung}\"\nAktuell: \"{aktuell}\"\n" +
                $"Antwort NUR mit einem Wort: BESTAETIGT, VERSCHOBEN, WIDERSPROCHEN, ERWEITERT, ABGELEITET, UMSTRITTEN";
            var antwort = await llm.FreieAnfrage(prompt);
            if (antwort != null)
            {
                string text = antwort.inhalt.Trim().ToUpperInvariant();
                if (Enum.TryParse<DriftKlassifikation>(text, out var result))
                    return result;
            }
            return DriftKlassifikation.VERSCHOBEN;
        }

        public async Task<string> SelbstKritik(string konzeptId, string revision, List<Erfahrung> evidenz)
        {
            var prompt = $"Kritisiere diese Revision des Konzepts '{konzeptId}':\n\"{revision}\"\n\n" +
                $"Pruefe: Confirmation Bias? Koennte dieselbe Evidenz etwas anderes bedeuten? " +
                $"Ist das Ergebnis zu sauber? Kommt die Einsicht aus Erfahrungen oder nur aus der Ausgangsdefinition?";
            var antwort = await llm.FreieAnfrage(prompt);
            return antwort?.inhalt ?? "Keine Selbstkritik moeglich.";
        }

        // Kompositionelle Konzeptschoepfung
        public async Task<Konzept> VerschmelzeKonzepte(string idA, string idB, List<Erfahrung> evidenz)
        {
            if (!konzepte.TryGetValue(idA, out var a) || !konzepte.TryGetValue(idB, out var b))
                return null;

            var prompt = $"Konzept A '{a.name}': {a.aktuelleDefinition}\n" +
                $"Konzept B '{b.name}': {b.aktuelleDefinition}\n" +
                $"Diese treten haeufig zusammen auf. Schlage ein neues verschmolzenes Konzept vor. " +
                $"JSON: {{\"name\": \"...\", \"definition\": \"...\"}}";
            var antwort = await llm.FreieAnfrage(prompt);
            if (antwort == null) return null;

            try
            {
                var roh = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(antwort.inhalt);
                return new Konzept
                {
                    id = $"merged_{idA}_{idB}",
                    name = roh.GetValueOrDefault("name", $"{a.name}+{b.name}"),
                    typ = KonzeptTyp.Emergent,
                    ursprungsDefinition = roh.GetValueOrDefault("definition", ""),
                    aktuelleDefinition = roh.GetValueOrDefault("definition", ""),
                    drift = DriftKlassifikation.ABGELEITET
                };
            }
            catch { return null; }
        }

        public void Persistiere()
        {
            DatenLader.Speichere("konzept_revisionen.json", historie);
        }

        private void LadeHistorie()
        {
            var geladen = DatenLader.LadeListe<KonzeptRevisionSchritt>("konzept_revisionen.json");
            if (geladen != null) historie = geladen;
        }
    }
}
