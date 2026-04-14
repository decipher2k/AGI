using System.Collections.Generic;
using System.Linq;
using BilligAGI.Modelle;
using BilligAGI.Daten;

namespace BilligAGI.Sozial
{
    public class Mechanismen
    {
        private List<SozialMechanismus> alleMechanismen;

        public Mechanismen()
        {
            alleMechanismen = DatenLader.LadeListe<SozialMechanismus>("sozial_regeln.json")
                ?? new List<SozialMechanismus>();
        }

        public List<ErkannterMechanismus> Erkenne(string text, SozialeAnalyse kontext)
        {
            var erkannt = new List<ErkannterMechanismus>();
            if (string.IsNullOrEmpty(text)) return erkannt;

            string lower = text.ToLowerInvariant();

            foreach (var mech in alleMechanismen)
            {
                if (mech.erkennungsMuster == null) continue;

                float score = 0f;
                int treffer = 0;

                foreach (var muster in mech.erkennungsMuster)
                {
                    if (lower.Contains(muster.ToLowerInvariant()))
                    {
                        treffer++;
                        score += 1f / mech.erkennungsMuster.Count;
                    }
                }

                if (treffer > 0 || (kontext != null && KontextPasst(mech, kontext)))
                {
                    erkannt.Add(new ErkannterMechanismus
                    {
                        mechanismus = mech.name,
                        kategorie = mech.kategorie,
                        konfidenz = UnityEngine.Mathf.Clamp01(score + (KontextPasst(mech, kontext) ? 0.2f : 0f)),
                        belegstelle = text.Length > 100 ? text.Substring(0, 100) + "..." : text,
                        gegenmassnahme = mech.gegenmassnahme
                    });
                }
            }

            // Nach Konfidenz sortieren
            erkannt.Sort((a, b) => b.konfidenz.CompareTo(a.konfidenz));
            return erkannt;
        }

        public SozialMechanismus GetMechanismus(string name)
        {
            return alleMechanismen.FirstOrDefault(m =>
                m.name.ToLowerInvariant() == name.ToLowerInvariant());
        }

        public List<SozialMechanismus> GetKategorie(string kategorie)
        {
            return alleMechanismen.Where(m =>
                m.kategorie?.ToLowerInvariant() == kategorie.ToLowerInvariant()).ToList();
        }

        private bool KontextPasst(SozialMechanismus mech, SozialeAnalyse kontext)
        {
            if (kontext == null || kontext.aktiveArchetypen == null) return false;

            // Bestimmte Archetypen aktivieren bestimmte Mechanismen
            foreach (var arch in kontext.aktiveArchetypen)
            {
                if (arch == "Herrscher" && mech.kategorie == "autoritaet") return true;
                if (arch == "Trickster" && mech.kategorie == "kognitive_verzerrungen") return true;
                if (arch == "Kind" && mech.kategorie == "konformitaet") return true;
            }
            return false;
        }
    }
}
