using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BilligAGI.Modelle;
using BilligAGI.Kern;
using BilligAGI.Daten;
using UnityEngine;

namespace BilligAGI.Intentionalitaet
{
    public class Planer
    {
        private readonly LLMAdapter llm;
        private readonly ZeitModell zeitModell;
        private readonly AnalogieEngine analogie;
        private List<AktionsDefinition> aktionsLexikon;

        public Planer(LLMAdapter llm, ZeitModell zeitModell, AnalogieEngine analogie)
        {
            this.llm = llm;
            this.zeitModell = zeitModell;
            this.analogie = analogie;
            aktionsLexikon = DatenLader.LadeListe<AktionsDefinition>("aktions_lexikon.json")
                ?? new List<AktionsDefinition>();
        }

        public async Task<Plan> ErstellePlan(Ziel ziel, WeltZustand welt)
        {
            var plan = new Plan
            {
                id = System.Guid.NewGuid().ToString(),
                zielId = ziel.id,
                aktionen = new List<Aktion>()
            };

            // LLM: Ziel → Teilziele → Aktionssequenz
            string prompt = $"Erstelle einen Aktionsplan fuer: '{ziel.beschreibung}'\n" +
                $"Weltzustand: {welt.objekte.Count} Objekte, Wetter: {welt.wetter}\n" +
                $"Verfuegbare Aktionen: {string.Join(", ", aktionsLexikon.Select(a => a.name))}\n\n" +
                $"Antworte als JSON-Array: [{{\"name\": \"aktion\", \"parameter\": \"...\"}}]";

            var antwort = await llm.FreieAnfrage(prompt);
            if (antwort != null)
            {
                try
                {
                    var schritte = Newtonsoft.Json.JsonConvert.DeserializeObject<List<PlanSchritt>>(antwort.inhalt);
                    if (schritte != null)
                    {
                        int schritt = 0;
                        foreach (var s in schritte)
                        {
                            var def = aktionsLexikon.FirstOrDefault(a =>
                                a.name.ToLowerInvariant() == s.name?.ToLowerInvariant());

                            plan.aktionen.Add(new Aktion
                            {
                                id = System.Guid.NewGuid().ToString(),
                                name = s.name,
                                typ = def?.typ ?? AktionsTyp.Interagieren,
                                parameter = s.parameter ?? "",
                                reihenfolge = schritt++,
                                geschaetzteDauer = zeitModell?.SchaetzeDauer(s.name) ?? 5f
                            });
                        }
                    }
                }
                catch { }
            }

            // Fallback: Trivialer Plan
            if (plan.aktionen.Count == 0)
            {
                plan.aktionen.Add(new Aktion
                {
                    id = System.Guid.NewGuid().ToString(),
                    name = "beobachten",
                    typ = AktionsTyp.Beobachten,
                    reihenfolge = 0,
                    geschaetzteDauer = 3f
                });
            }

            // Gesamtdauer schaetzen
            plan.geschaetzteDauer = plan.aktionen.Sum(a => a.geschaetzteDauer);

            return plan;
        }

        public async Task<Plan> PlaneUm(Plan plan, string hindernis)
        {
            string prompt = $"Der Plan '{plan.zielId}' wurde durch '{hindernis}' blockiert.\n" +
                $"Bisherige Schritte: {string.Join(" → ", plan.aktionen.Select(a => a.name))}\n" +
                $"Erstelle einen alternativen Plan. JSON-Array: [{{\"name\": \"aktion\", \"parameter\": \"...\"}}]";

            var antwort = await llm.FreieAnfrage(prompt);
            if (antwort != null)
            {
                try
                {
                    var schritte = Newtonsoft.Json.JsonConvert.DeserializeObject<List<PlanSchritt>>(antwort.inhalt);
                    if (schritte != null && schritte.Count > 0)
                    {
                        plan.aktionen.Clear();
                        int schritt = 0;
                        foreach (var s in schritte)
                        {
                            plan.aktionen.Add(new Aktion
                            {
                                id = System.Guid.NewGuid().ToString(),
                                name = s.name,
                                typ = AktionsTyp.Interagieren,
                                parameter = s.parameter ?? "",
                                reihenfolge = schritt++,
                                geschaetzteDauer = zeitModell?.SchaetzeDauer(s.name) ?? 5f
                            });
                        }
                        plan.umplanungen++;
                    }
                }
                catch { }
            }
            return plan;
        }

        public async Task<Plan> PlaneKreativ(Plan plan, List<Erfahrung> erfahrungen)
        {
            // Analogie-basierter kreativer Plan
            if (analogie != null && erfahrungen != null)
            {
                var analogs = await analogie.SucheAnalogien(plan.zielId, erfahrungen);
                if (analogs.Count > 0)
                {
                    string prompt = $"Basierend auf der Analogie: '{analogs[0].transferHypothese}'\n" +
                        $"Erstelle einen kreativen alternativen Plan. JSON-Array: [{{\"name\": \"aktion\", \"parameter\": \"...\"}}]";

                    var antwort = await llm.FreieAnfrage(prompt);
                    if (antwort != null)
                    {
                        try
                        {
                            var schritte = Newtonsoft.Json.JsonConvert.DeserializeObject<List<PlanSchritt>>(antwort.inhalt);
                            if (schritte != null && schritte.Count > 0)
                            {
                                plan.aktionen.Clear();
                                int s = 0;
                                foreach (var schritt in schritte)
                                {
                                    plan.aktionen.Add(new Aktion
                                    {
                                        id = System.Guid.NewGuid().ToString(),
                                        name = schritt.name,
                                        typ = AktionsTyp.Interagieren,
                                        parameter = schritt.parameter ?? "",
                                        reihenfolge = s++
                                    });
                                }
                                plan.umplanungen++;
                            }
                        }
                        catch { }
                    }
                }
            }
            return plan;
        }

        public bool PlanValidieren(Plan plan, WeltZustand welt)
        {
            if (plan == null || plan.aktionen.Count == 0) return false;
            // Einfache Validierung: Alle Aktionen muessen im Lexikon sein
            foreach (var aktion in plan.aktionen)
            {
                bool bekannt = aktionsLexikon.Any(a =>
                    a.name.ToLowerInvariant() == aktion.name?.ToLowerInvariant());
                if (!bekannt)
                {
                    Debug.LogWarning($"[Planer] Unbekannte Aktion im Plan: {aktion.name}");
                    // Nicht sofort ablehnen — LLM kann kreative Aktionen vorschlagen
                }
            }
            return true;
        }

        [System.Serializable]
        private class PlanSchritt
        {
            public string name;
            public string parameter;
        }
    }
}
