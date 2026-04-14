using System.Collections.Generic;
using System.Linq;
using BilligAGI.Modelle;
using BilligAGI.Welt;
using UnityEngine;

namespace BilligAGI.Kern
{
    public class NeugierSystem
    {
        private readonly AGIConfig config;
        private List<Hypothese> aktiveHypothesen;

        public NeugierSystem(AGIConfig config)
        {
            this.config = config;
            aktiveHypothesen = new List<Hypothese>();
        }

        public List<Hypothese> GeneriereHypothesen(WeltZustand welt, SelbstModell selbst, KausalGraph kausal)
        {
            var hypothesen = new List<Hypothese>();

            // 1. Wissenslücken im SelbstModell
            if (selbst != null)
            {
                foreach (var kvp in selbst.GetAlleKompetenzen())
                {
                    if (kvp.Value < 0.3f)
                    {
                        hypothesen.Add(new Hypothese
                        {
                            beschreibung = $"Kompetenz in '{kvp.Key}' ist niedrig ({kvp.Value:F2}). Experiment zur Verbesserung.",
                            prioritaet = 1f - kvp.Value,
                            domaene = kvp.Key,
                            typ = HypotheseTyp.Wissensluecke
                        });
                    }
                }
            }

            // 2. Niedrige Konfidenz in Kausalregeln
            if (kausal != null)
            {
                foreach (var kante in kausal.GetNiedrigeKonfidenz(0.4f))
                {
                    hypothesen.Add(new Hypothese
                    {
                        beschreibung = $"'{kante.ursache}' → '{kante.wirkung}' hat niedrige Konfidenz ({kante.konfidenz:F2}). Experiment empfohlen.",
                        prioritaet = 0.5f + (1f - kante.konfidenz) * 0.5f,
                        domaene = "kausal",
                        typ = HypotheseTyp.NiedrigeKonfidenz
                    });
                }
            }

            // 3. Unerforschte Weltbereiche
            if (welt?.objekte != null)
            {
                foreach (var obj in welt.objekte.Values)
                {
                    if (obj.zustand == "unbekannt" || string.IsNullOrEmpty(obj.zustand))
                    {
                        hypothesen.Add(new Hypothese
                        {
                            beschreibung = $"Objekt '{obj.name}' ist unexploriert. Was kann man damit tun?",
                            prioritaet = 0.6f,
                            domaene = "exploration",
                            typ = HypotheseTyp.Exploration
                        });
                    }
                }
            }

            hypothesen.Sort((a, b) => b.prioritaet.CompareTo(a.prioritaet));
            aktiveHypothesen = hypothesen;
            return hypothesen;
        }

        public float Unsicherheit(string domaene, SelbstModell selbst)
        {
            if (selbst == null) return 1f;
            float kompetenz = selbst.GetKompetenz(domaene);
            return 1f - kompetenz;
        }

        public List<string> UnerforschteBereiche(WeltModell welt)
        {
            var bereiche = new List<string>();
            if (welt?.zustand?.objekte == null) return bereiche;

            foreach (var obj in welt.zustand.objekte.Values)
            {
                if (obj.zustand == "unbekannt" || string.IsNullOrEmpty(obj.zustand))
                    bereiche.Add($"{obj.name} bei [{obj.position[0]:F0},{obj.position[2]:F0}]");
            }
            return bereiche;
        }

        public Ziel HypotheseZuZiel(Hypothese hyp)
        {
            return new Ziel
            {
                id = System.Guid.NewGuid().ToString(),
                beschreibung = hyp.beschreibung,
                typ = ZielTyp.EXPLORATION,
                prioritaet = hyp.prioritaet,
                status = ZielStatus.AKTIV
            };
        }

        public List<Hypothese> GetAktive() => aktiveHypothesen;
    }

    [System.Serializable]
    public class Hypothese
    {
        public string beschreibung;
        public float prioritaet;
        public string domaene;
        public HypotheseTyp typ;
    }

    public enum HypotheseTyp
    {
        Wissensluecke,
        NiedrigeKonfidenz,
        Exploration,
        Experiment
    }
}
