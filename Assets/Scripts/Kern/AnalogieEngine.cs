using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BilligAGI.Modelle;
using BilligAGI.Kern;

namespace BilligAGI.Kern
{
    public class AnalogieEngine
    {
        private readonly LLMAdapter llm;

        public AnalogieEngine(LLMAdapter llm)
        {
            this.llm = llm;
        }

        public async Task<List<Analogie>> SucheAnalogien(string konzept, List<Erfahrung> erfahrungen)
        {
            var analogien = new List<Analogie>();
            if (erfahrungen == null || erfahrungen.Count < 2) return analogien;

            // 1. Lokale Strukturvergleiche
            var gruppen = GruppierNachStruktur(erfahrungen);
            foreach (var gruppe in gruppen)
            {
                if (gruppe.Value.Count >= 2)
                {
                    var a = gruppe.Value[0];
                    var b = gruppe.Value[1];
                    analogien.Add(new Analogie
                    {
                        quellDommaene = a.kontext ?? "unbekannt",
                        zielDomaene = b.kontext ?? "unbekannt",
                        mapping = $"{a.aktion} ↔ {b.aktion}",
                        staerke = 0.5f + gruppe.Value.Count * 0.1f,
                        transferHypothese = $"Wenn '{a.ergebnis}' bei '{a.aktion}', dann moeglicherweise auch bei '{b.aktion}'."
                    });
                }
            }

            // 2. LLM-basierte tiefe Analogien
            if (analogien.Count < 3)
            {
                string prompt = $"Suche Analogien zum Konzept '{konzept}' basierend auf diesen Erfahrungen:\n";
                foreach (var e in erfahrungen.Take(10))
                    prompt += $"- {e.aktion}: {e.ergebnis}\n";
                prompt += "\nListe 3 Analogien als JSON-Array: [{\"quell\": \"...\", \"ziel\": \"...\", \"mapping\": \"...\", \"staerke\": 0.5}]";

                var antwort = await llm.FreieAnfrage(prompt);
                if (antwort != null)
                {
                    try
                    {
                        var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<List<AnalogieRoh>>(antwort.inhalt);
                        if (parsed != null)
                        {
                            foreach (var p in parsed)
                            {
                                analogien.Add(new Analogie
                                {
                                    quellDommaene = p.quell,
                                    zielDomaene = p.ziel,
                                    mapping = p.mapping,
                                    staerke = p.staerke,
                                    transferHypothese = $"Transfer von '{p.quell}' nach '{p.ziel}': {p.mapping}"
                                });
                            }
                        }
                    }
                    catch { }
                }
            }

            analogien.Sort((a, b) => b.staerke.CompareTo(a.staerke));
            return analogien;
        }

        private Dictionary<string, List<Erfahrung>> GruppierNachStruktur(List<Erfahrung> erfahrungen)
        {
            var gruppen = new Dictionary<string, List<Erfahrung>>();
            foreach (var e in erfahrungen)
            {
                // Strukturschluessel: Ergebnis-Muster (vereinfacht)
                string key = e.ergebnis?.Split(' ').FirstOrDefault() ?? "andere";
                if (!gruppen.ContainsKey(key))
                    gruppen[key] = new List<Erfahrung>();
                gruppen[key].Add(e);
            }
            return gruppen;
        }

        [System.Serializable]
        private class AnalogieRoh
        {
            public string quell;
            public string ziel;
            public string mapping;
            public float staerke;
        }
    }

    [System.Serializable]
    public class Analogie
    {
        public string quellDommaene;
        public string zielDomaene;
        public string mapping;
        public float staerke;
        public string transferHypothese;
    }
}
