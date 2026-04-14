using System.Collections.Generic;
using System.Linq;
using BilligAGI.Modelle;
using UnityEngine;

namespace BilligAGI.Sozial
{
    public class Alchemie
    {
        private Dictionary<string, AlchemischePhase> entitaetPhasen;

        public Alchemie()
        {
            entitaetPhasen = new Dictionary<string, AlchemischePhase>();
        }

        public AlchemischePhase ErkennePhase(string situation, List<Erfahrung> verlauf)
        {
            if (string.IsNullOrEmpty(situation))
                return AlchemischePhase.Nigredo;

            string lower = situation.ToLowerInvariant();

            // Schluesselwort-basierte Erkennung
            if (EnthaeltEines(lower, "krise", "zerfall", "schatten", "dunkel", "verlust", "chaos", "zusammenbruch"))
                return AlchemischePhase.Nigredo;

            if (EnthaeltEines(lower, "reflexion", "reinigung", "klärung", "klaerung", "unterscheidung", "analyse", "ordnung"))
                return AlchemischePhase.Albedo;

            if (EnthaeltEines(lower, "einsicht", "erwachen", "erkenntnis", "durchbruch", "morgenroete", "neu"))
                return AlchemischePhase.Citrinitas;

            if (EnthaeltEines(lower, "integration", "vollendung", "synthese", "harmonie", "vereinigung", "ganzheit"))
                return AlchemischePhase.Rubedo;

            // Verlauf-basierte Erkennung: Phasen-Progression
            if (verlauf != null && verlauf.Count > 0)
            {
                int negativ = verlauf.Count(e => e.belohnung < 0);
                int positiv = verlauf.Count(e => e.belohnung > 0);
                float ratio = verlauf.Count > 0 ? (float)positiv / verlauf.Count : 0.5f;

                if (ratio < 0.25f) return AlchemischePhase.Nigredo;
                if (ratio < 0.5f) return AlchemischePhase.Albedo;
                if (ratio < 0.75f) return AlchemischePhase.Citrinitas;
                return AlchemischePhase.Rubedo;
            }

            return AlchemischePhase.Nigredo;
        }

        public string TransformationsImpuls(AlchemischePhase phase, string kontext)
        {
            switch (phase)
            {
                case AlchemischePhase.Nigredo:
                    return $"[Nigredo — Solve] Konfrontation mit dem was zerbrochen ist. " +
                           $"Was genau funktioniert hier nicht? Welcher Schatten zeigt sich? " +
                           $"Kontext: {kontext}";

                case AlchemischePhase.Albedo:
                    return $"[Albedo — Reinigung] Die Truebung klaert sich. " +
                           $"Was kann unterschieden werden? Was ist wesentlich, was nicht? " +
                           $"Kontext: {kontext}";

                case AlchemischePhase.Citrinitas:
                    return $"[Citrinitas — Erwachen] Eine neue Einsicht daemmert. " +
                           $"Welche Verbindung wurde vorher nicht gesehen? Was wird jetzt klar? " +
                           $"Kontext: {kontext}";

                case AlchemischePhase.Rubedo:
                    return $"[Rubedo — Coniunctio] Gegensaetze vereinigen sich. " +
                           $"Was kann nun integriert werden? Welche Ganzheit entsteht? " +
                           $"Kontext: {kontext}";

                default:
                    return $"Phase unbekannt. Kontext: {kontext}";
            }
        }

        public string SolveEtCoagula(string prima, string kontext)
        {
            return $"[Solve] Zerlegung von '{prima}': Was sind die Bestandteile?\n" +
                   $"[Coagula] Neuzusammensetzung: Was entsteht wenn die Teile anders kombiniert werden?\n" +
                   $"Kontext: {kontext}";
        }

        public void SetzePhase(string entitaetId, AlchemischePhase phase)
        {
            entitaetPhasen[entitaetId] = phase;
        }

        public AlchemischePhase GetPhase(string entitaetId)
        {
            return entitaetPhasen.TryGetValue(entitaetId, out var phase)
                ? phase : AlchemischePhase.Nigredo;
        }

        private bool EnthaeltEines(string text, params string[] woerter)
        {
            return woerter.Any(w => text.Contains(w));
        }
    }
}
