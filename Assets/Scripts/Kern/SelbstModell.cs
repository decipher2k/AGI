using System.Collections.Generic;
using BilligAGI.Modelle;
using UnityEngine;

namespace BilligAGI.Kern
{
    public class SelbstModell
    {
        private Dictionary<string, float> kompetenzen;
        private Dictionary<string, int> versuchsZaehler;
        private Dictionary<string, int> erfolgsZaehler;

        public SelbstModell()
        {
            kompetenzen = new Dictionary<string, float>
            {
                ["physik"] = 0.1f,
                ["sozial"] = 0.1f,
                ["navigation"] = 0.1f,
                ["greifen"] = 0.1f,
                ["werfen"] = 0.1f,
                ["interaktion"] = 0.1f,
                ["planung"] = 0.1f,
                ["kommunikation"] = 0.1f,
            };
            versuchsZaehler = new Dictionary<string, int>();
            erfolgsZaehler = new Dictionary<string, int>();
        }

        public void AktualisiereKompetenz(string domaene, bool erfolg)
        {
            domaene = domaene.ToLowerInvariant();

            if (!versuchsZaehler.ContainsKey(domaene))
                versuchsZaehler[domaene] = 0;
            if (!erfolgsZaehler.ContainsKey(domaene))
                erfolgsZaehler[domaene] = 0;

            versuchsZaehler[domaene]++;
            if (erfolg) erfolgsZaehler[domaene]++;

            // Gleitender Durchschnitt
            float aktuelle = GetKompetenz(domaene);
            float neuerWert = erfolg ? aktuelle + 0.05f : aktuelle - 0.02f;
            kompetenzen[domaene] = Mathf.Clamp01(neuerWert);
        }

        public float GetKompetenz(string domaene)
        {
            domaene = domaene.ToLowerInvariant();
            return kompetenzen.TryGetValue(domaene, out float val) ? val : 0.1f;
        }

        public Dictionary<string, float> GetAlleKompetenzen()
        {
            return new Dictionary<string, float>(kompetenzen);
        }

        public string KommuniziereKompetenz(string domaene)
        {
            float k = GetKompetenz(domaene);
            if (k > 0.8f) return $"'{domaene}' beherrsche ich gut.";
            if (k > 0.5f) return $"'{domaene}' kann ich einigermassen.";
            if (k > 0.2f) return $"'{domaene}' da bin ich noch unsicher.";
            return $"'{domaene}' ist mir noch kaum bekannt.";
        }

        public bool KannIchDas(Ziel ziel)
        {
            if (ziel == null) return false;

            // Domaene aus Ziel-Typ ableiten
            string domaene = ZielTypZuDomaene(ziel.typ);
            float kompetenz = GetKompetenz(domaene);

            // Hoehere Schwierigkeit = hoehere Kompetenz noetig
            return kompetenz >= 0.3f || ziel.prioritaet < 0.5f;
        }

        public string GetSelbstbeschreibung()
        {
            var teile = new List<string>();
            foreach (var kvp in kompetenzen)
            {
                teile.Add($"{kvp.Key}: {kvp.Value:F2}");
            }
            return $"Kompetenzen: {string.Join(", ", teile)}";
        }

        private string ZielTypZuDomaene(ZielTyp typ)
        {
            switch (typ)
            {
                case ZielTyp.EXPLORATION: return "navigation";
                case ZielTyp.EXPERIMENT: return "physik";
                case ZielTyp.AUFGABE: return "planung";
                case ZielTyp.SOZIAL: return "sozial";
                default: return "allgemein";
            }
        }
    }
}
