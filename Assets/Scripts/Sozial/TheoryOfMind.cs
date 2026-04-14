using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BilligAGI.Modelle;
using BilligAGI.Kern;
using UnityEngine;

namespace BilligAGI.Sozial
{
    public class TheoryOfMind
    {
        private Dictionary<string, MentalesModell> modelle;
        private readonly LLMAdapter llm;

        public TheoryOfMind(LLMAdapter llm)
        {
            this.llm = llm;
            modelle = new Dictionary<string, MentalesModell>();
        }

        public MentalesModell ErstelleModell(string entitaetId, string name)
        {
            var modell = new MentalesModell
            {
                entitaetId = entitaetId,
                name = name,
                wissen = new List<string>(),
                glauben = new List<string>(),
                ziele = new List<string>(),
                letzteBeobachtung = "",
                vertrauensLevel = 0.5f,
                zeitstempel = System.DateTime.UtcNow.ToString("o"),
            };
            modelle[entitaetId] = modell;
            return modell;
        }

        public void AktualisiereMitBeobachtung(string entitaetId, string beobachtung, SensorDaten daten)
        {
            if (!modelle.TryGetValue(entitaetId, out var modell))
            {
                modell = ErstelleModell(entitaetId, entitaetId);
            }

            modell.letzteBeobachtung = beobachtung;
            modell.zeitstempel = System.DateTime.UtcNow.ToString("o");

            // Was hat die Entitaet GESEHEN?
            // Approximation: Objekte in Sichtlinie der Entitaet
            if (daten?.nahbereichObjekte != null)
            {
                foreach (var obj in daten.nahbereichObjekte)
                {
                    string wissensEintrag = $"Sieht: {obj.name} bei {obj.distanz:F1}m";
                    if (!modell.wissen.Contains(wissensEintrag))
                        modell.wissen.Add(wissensEintrag);
                }
            }

            // Was hat die Entitaet GETAN?
            if (!string.IsNullOrEmpty(beobachtung))
            {
                string aktionsEintrag = $"Tat: {beobachtung}";
                if (!modell.wissen.Contains(aktionsEintrag))
                    modell.wissen.Add(aktionsEintrag);
            }
        }

        public List<string> WasWeissSie(string entitaetId)
        {
            return modelle.TryGetValue(entitaetId, out var m)
                ? new List<string>(m.wissen)
                : new List<string>();
        }

        public List<string> WasGlaubtSie(string entitaetId)
        {
            return modelle.TryGetValue(entitaetId, out var m)
                ? new List<string>(m.glauben)
                : new List<string>();
        }

        public List<string> WasWillSie(string entitaetId)
        {
            return modelle.TryGetValue(entitaetId, out var m)
                ? new List<string>(m.ziele)
                : new List<string>();
        }

        public async Task<string> VorhersageVerhalten(string entitaetId, string situation)
        {
            if (!modelle.TryGetValue(entitaetId, out var modell))
                return "Keine Daten ueber diese Entitaet.";

            string prompt = $"Entitaet '{modell.name}' hat folgendes Wissen: {string.Join("; ", modell.wissen)}\n" +
                $"Sie glaubt: {string.Join("; ", modell.glauben)}\n" +
                $"Ihre Ziele: {string.Join("; ", modell.ziele)}\n" +
                $"Situation: {situation}\n\n" +
                $"Was wird die Entitaet wahrscheinlich tun? Antworte in 1-2 Saetzen.";

            var antwort = await llm.FreieAnfrage(prompt);
            return antwort?.inhalt ?? "Vorhersage nicht moeglich.";
        }

        public bool FalseBeliefErkannt(string entitaetId, string thema)
        {
            if (!modelle.TryGetValue(entitaetId, out var modell))
                return false;

            string themaLower = thema.ToLowerInvariant();

            // Pruefe ob das Thema im Wissen der Entitaet fehlt
            bool weissBescheid = modell.wissen.Any(w =>
                w.ToLowerInvariant().Contains(themaLower));

            // Wenn die Entitaet es nicht weiss, hat sie potentiell einen False Belief
            return !weissBescheid;
        }

        public void InjiziereWissen(string entitaetId, string wissen)
        {
            if (modelle.TryGetValue(entitaetId, out var modell))
            {
                if (!modell.wissen.Contains(wissen))
                    modell.wissen.Add(wissen);
            }
        }

        public void InjiziereGlauben(string entitaetId, string glaube)
        {
            if (modelle.TryGetValue(entitaetId, out var modell))
            {
                if (!modell.glauben.Contains(glaube))
                    modell.glauben.Add(glaube);
            }
        }

        public void InjiziereZiel(string entitaetId, string ziel)
        {
            if (modelle.TryGetValue(entitaetId, out var modell))
            {
                if (!modell.ziele.Contains(ziel))
                    modell.ziele.Add(ziel);
            }
        }

        public MentalesModell GetModell(string entitaetId)
        {
            return modelle.TryGetValue(entitaetId, out var m) ? m : null;
        }

        public List<string> AlleEntitaeten() => modelle.Keys.ToList();
    }
}
