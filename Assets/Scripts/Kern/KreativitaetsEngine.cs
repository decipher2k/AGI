using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BilligAGI.Modelle;
using Newtonsoft.Json;
using UnityEngine;

namespace BilligAGI.Kern
{
    public class KreativitaetsEngine
    {
        private readonly AGIConfig config;
        private readonly LLMAdapter llm;
        private List<KreativHeuristik> heuristiken;

        [Serializable]
        public class KreativHeuristik
        {
            public string name;
            public string beschreibung;
            public float gewicht;
            public int erfolge;
            public int versuche;

            public float Erfolgsrate => versuche > 0 ? (float)erfolge / versuche : 0.5f;
        }

        [Serializable]
        private class HeuristikContainer { public List<KreativHeuristik> heuristiken; }

        public KreativitaetsEngine(AGIConfig config, LLMAdapter llm)
        {
            this.config = config;
            this.llm = llm;
            LadeHeuristiken();
        }

        private void LadeHeuristiken()
        {
            heuristiken = Daten.DatenLader.Lade<HeuristikContainer>("kreativitaets_heuristiken.json")?.heuristiken
                          ?? new List<KreativHeuristik>();
        }

        public async Task<List<KreativIdee>> GeneriereIdeen(Ziel ziel, WeltZustand welt, List<Erfahrung> erfahrungen)
        {
            var ideen = new List<KreativIdee>();
            int maxVarianten = config.kreativitaetMaxVarianten;

            // Gewichtete Heuristik-Auswahl
            var sortiert = heuristiken.OrderByDescending(h => h.gewicht * h.Erfolgsrate).ToList();

            string prompt = $"Ziel: {ziel.name}\nBeschreibung: {ziel.beschreibung}\n\n" +
                           $"Bekannte Erfahrungen: {erfahrungen.Count}\n" +
                           $"Weltzustand: {welt?.objekte.Count ?? 0} Objekte, Wetter: {welt?.wetter}\n\n" +
                           $"Generiere {maxVarianten} verschiedene kreative Loesungsansaetze.\n" +
                           "Verwende diese Strategien:\n";

            foreach (var h in sortiert.Take(4))
                prompt += $"- {h.name}: {h.beschreibung}\n";

            prompt += "\nFuer jeden Ansatz: Beschreibung, Strategie (analogie/mutation/kombination/perspektivwechsel), " +
                     "geschaetzte Novelty (0-1), geschaetzte Utility (0-1). Antworte als JSON-Array.";

            var antwort = await llm.Analysiere(prompt,
                "Du bist ein Kreativitaetsmodul einer AGI. Generiere diverse, plausible Loesungsansaetze.");

            // Parse LLM-Antwort
            try
            {
                string jsonPart = ExtrahiereJson(antwort.text);
                var roheIdeen = JsonConvert.DeserializeObject<List<RoheIdee>>(jsonPart);
                if (roheIdeen != null)
                {
                    foreach (var ri in roheIdeen.Take(maxVarianten))
                    {
                        ideen.Add(new KreativIdee
                        {
                            beschreibung = ri.beschreibung ?? "Unbenannte Idee",
                            quelle = ParseQuelle(ri.strategie),
                            noveltyScore = Mathf.Clamp01(ri.novelty),
                            utilityScore = Mathf.Clamp01(ri.utility),
                            plausibilitaetScore = 0.5f, // Wird spaeter bewertet
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Kreativ] Parse-Fehler: {ex.Message}");
                // Fallback: Eine manuelle Idee
                ideen.Add(new KreativIdee
                {
                    beschreibung = $"Standard-Ansatz fuer: {ziel.name}",
                    quelle = KreativQuelle.Mutation,
                    noveltyScore = 0.3f,
                    utilityScore = 0.7f,
                    plausibilitaetScore = 0.8f,
                });
            }

            return ideen;
        }

        public KreativIdee Bewerte(KreativIdee idee)
        {
            // Schwellwert-Check
            bool novelOk = idee.noveltyScore >= config.kreativitaetNoveltySchwelle;
            bool utilOk = idee.utilityScore >= config.kreativitaetUtilitySchwelle;
            bool plausOk = idee.plausibilitaetScore >= 0.4f;
            bool sicher = idee.risikoScore < 0.8f;

            if (novelOk && utilOk && plausOk && sicher)
                idee.status = KreativStatus.VORGESCHLAGEN;
            else
                idee.status = KreativStatus.VERWORFEN;

            return idee;
        }

        public List<KreativIdee> SelektiereTopK(List<KreativIdee> ideen, int k)
        {
            return ideen
                .Where(i => i.status != KreativStatus.VERWORFEN)
                .OrderByDescending(i => i.GesamtScore())
                .Take(k)
                .ToList();
        }

        public async Task<ABErgebnis> VergleicheMitBaseline(Plan baseline, List<Plan> kreativVarianten)
        {
            // Simplifizierter A/B-Vergleich
            var ergebnis = new ABErgebnis
            {
                baselinePlanId = baseline.zielId,
                baselineErfolgsrate = 0.5f, // Wird durch Ausfuehrung bestimmt
                kreativErfolgsrate = 0.5f,
                kreativBesser = false,
            };

            if (kreativVarianten.Count > 0)
            {
                // Heuristik-basiert: kreative Variante mit weniger Schritten oder mehr Novelty
                var bestKreativ = kreativVarianten.OrderBy(p => p.schritte.Count).First();
                ergebnis.kreativPlanId = bestKreativ.zielId;
                ergebnis.kreativBesser = bestKreativ.schritte.Count <= baseline.schritte.Count;
            }

            return ergebnis;
        }

        public void LerneAusKreativErgebnis(KreativIdee idee, bool erfolgreich)
        {
            if (erfolgreich)
                idee.status = KreativStatus.UEBERNOMMEN;
            else
                idee.status = KreativStatus.VERWORFEN;

            // Heuristik-Gewichte anpassen
            string quelleStr = idee.quelle.ToString().ToLowerInvariant();
            var heuristik = heuristiken.FirstOrDefault(h =>
                h.name.ToLowerInvariant().Contains(quelleStr) ||
                quelleStr.Contains(h.name.ToLowerInvariant().Replace("_", "")));

            if (heuristik != null)
            {
                heuristik.versuche++;
                if (erfolgreich)
                {
                    heuristik.erfolge++;
                    heuristik.gewicht = Mathf.Min(2f, heuristik.gewicht + 0.1f);
                }
                else
                {
                    heuristik.gewicht = Mathf.Max(0.1f, heuristik.gewicht - 0.05f);
                }
            }

            // Persistieren
            SpeichereHeuristiken();
        }

        private void SpeichereHeuristiken()
        {
            var container = new HeuristikContainer { heuristiken = heuristiken };
            Daten.DatenLader.Speichere("kreativitaets_heuristiken.json", container);
        }

        private static KreativQuelle ParseQuelle(string strategie)
        {
            if (string.IsNullOrEmpty(strategie)) return KreativQuelle.Mutation;
            strategie = strategie.ToLowerInvariant();
            if (strategie.Contains("analog")) return KreativQuelle.Analogie;
            if (strategie.Contains("kombin")) return KreativQuelle.Kombination;
            if (strategie.Contains("perspektiv")) return KreativQuelle.Perspektivwechsel;
            return KreativQuelle.Mutation;
        }

        private static string ExtrahiereJson(string text)
        {
            // Finde JSON-Array im Text
            int start = text.IndexOf('[');
            int end = text.LastIndexOf(']');
            if (start >= 0 && end > start)
                return text.Substring(start, end - start + 1);
            return "[]";
        }

        [Serializable]
        private class RoheIdee
        {
            public string beschreibung;
            public string strategie;
            public float novelty;
            public float utility;
        }
    }
}
