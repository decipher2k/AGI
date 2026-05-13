using System;
using BilligAGI.Modelle;

namespace BilligAGI.Wissen
{
    /// <summary>
    /// Entscheidet, ob eine Anfrage externe enzyklopaedische Fakten braucht.
    /// Embodied/agentische Anfragen bleiben beim internen Welt-, Ziel- und Erfahrungsmodell.
    /// </summary>
    public class WissensRouter
    {
        public bool BrauchtExternesWissen(string input, SemantikFrame frame = null)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;

            string lower = input.Trim().ToLowerInvariant();
            if (lower.Length < 8) return false;

            if (IstAgentOderWeltAnfrage(lower)) return false;

            bool istFrage = frame?.intentTyp == IntentTyp.Frage || lower.Contains("?");
            if (!istFrage) return false;

            return lower.StartsWith("was ist ")
                || lower.StartsWith("was sind ")
                || lower.StartsWith("wer ist ")
                || lower.StartsWith("wer war ")
                || lower.StartsWith("wann war ")
                || lower.StartsWith("wo liegt ")
                || lower.StartsWith("wo ist ")
                || lower.StartsWith("warum ")
                || lower.StartsWith("wie funktioniert ")
                || lower.StartsWith("wie entsteht ")
                || lower.StartsWith("erklaer ")
                || lower.StartsWith("erklär ")
                || lower.StartsWith("definiere ")
                || lower.Contains(" wikipedia")
                || lower.Contains(" allgemeinwissen")
                || lower.Contains(" externe quelle")
                || lower.Contains(" quellen");
        }

        private bool IstAgentOderWeltAnfrage(string lower)
        {
            string[] agentMarker =
            {
                "was siehst du", "was hoerst du", "was hörst du", "was fuehlst du", "was fühlst du",
                "was machst du", "was hast du", "dein ziel", "deine ziele", "dein plan",
                "bewege", "geh ", "gehe ", "greif", "nimm", "wirf", "erstelle", "baue",
                "in der szene", "in deiner welt", "im weltmodell", "status", "/stats", "/ziele"
            };

            foreach (string marker in agentMarker)
            {
                if (lower.Contains(marker)) return true;
            }
            return false;
        }
    }
}
