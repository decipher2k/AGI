using System;
using System.Collections.Generic;
using System.Linq;
using BilligAGI.Daten;
using UnityEngine;

namespace BilligAGI.Kern
{
    public class KausalGraph
    {
        private Dictionary<string, KausalKnoten> knoten;
        private List<KausalKante> kanten;
        private string persistenzPfad = "kausal_graph.json";

        public KausalGraph()
        {
            knoten = new Dictionary<string, KausalKnoten>();
            kanten = new List<KausalKante>();
            Lade();
        }

        public void FuegeKausalitaetHinzu(string ursache, string wirkung, float konfidenz, string ebene = "beobachtung")
        {
            SicherKnoten(ursache);
            SicherKnoten(wirkung);

            var existierend = kanten.FirstOrDefault(k => k.ursache == ursache && k.wirkung == wirkung);
            if (existierend != null)
            {
                existierend.konfidenz = Mathf.Min(1f, existierend.konfidenz + konfidenz * 0.2f);
                existierend.bestaetigungen++;
            }
            else
            {
                kanten.Add(new KausalKante
                {
                    ursache = ursache,
                    wirkung = wirkung,
                    konfidenz = konfidenz,
                    ebene = ebene,
                    bestaetigungen = 1,
                    zeitstempel = DateTime.UtcNow.ToString("o")
                });
            }
        }

        public void FuegeTemporaleKausalitaetHinzu(string ursache, string wirkung, float typischeDauer)
        {
            FuegeKausalitaetHinzu(ursache, wirkung, 0.5f, "temporal");
            var kante = kanten.Last();
            kante.typischeDauer = typischeDauer;
        }

        public List<string> WarumKette(string ursache, string wirkung, int maxTiefe = 5)
        {
            var kette = new List<string>();
            var besucht = new HashSet<string>();
            return SucheKette(ursache, wirkung, kette, besucht, maxTiefe)
                ? kette : new List<string> { "Keine Kausalkette gefunden." };
        }

        public List<string> WasPassiertWenn(string ursache)
        {
            var ergebnis = new List<string>();
            foreach (var k in kanten.Where(k => k.ursache == ursache).OrderByDescending(k => k.konfidenz))
            {
                ergebnis.Add($"→ {k.wirkung} (Konfidenz: {k.konfidenz:F2}, Ebene: {k.ebene})");
            }
            return ergebnis;
        }

        public List<KausalKante> GetNiedrigeKonfidenz(float schwelle)
        {
            return kanten.Where(k => k.konfidenz < schwelle).ToList();
        }

        public List<KausalKante> GetKantenFuer(string knotenName)
        {
            return kanten.Where(k => k.ursache == knotenName || k.wirkung == knotenName).ToList();
        }

        public void Persistiere()
        {
            var data = new KausalGraphData
            {
                knoten = knoten.Values.ToList(),
                kanten = kanten
            };
            DatenLader.Speichere(persistenzPfad, data);
        }

        private void Lade()
        {
            var data = DatenLader.Lade<KausalGraphData>(persistenzPfad);
            if (data != null)
            {
                if (data.knoten != null)
                    foreach (var k in data.knoten)
                        knoten[k.name] = k;
                if (data.kanten != null)
                    kanten = data.kanten;
            }
        }

        private void SicherKnoten(string name)
        {
            if (!knoten.ContainsKey(name))
                knoten[name] = new KausalKnoten { name = name, ebene = "beobachtung" };
        }

        private bool SucheKette(string aktuell, string ziel, List<string> kette,
            HashSet<string> besucht, int maxTiefe)
        {
            if (maxTiefe <= 0) return false;
            if (aktuell == ziel) { kette.Add(aktuell); return true; }
            if (!besucht.Add(aktuell)) return false;

            kette.Add(aktuell);
            foreach (var k in kanten.Where(k => k.ursache == aktuell))
            {
                if (SucheKette(k.wirkung, ziel, kette, besucht, maxTiefe - 1))
                    return true;
            }
            kette.RemoveAt(kette.Count - 1);
            return false;
        }

        [Serializable]
        public class KausalKnoten
        {
            public string name;
            public string ebene; // beobachtung, mechanismus, prinzip
        }

        [Serializable]
        public class KausalKante
        {
            public string ursache;
            public string wirkung;
            public float konfidenz;
            public string ebene;
            public int bestaetigungen;
            public float typischeDauer; // Sekunden, 0 = instant
            public string zeitstempel;
        }

        [Serializable]
        private class KausalGraphData
        {
            public List<KausalKnoten> knoten;
            public List<KausalKante> kanten;
        }
    }
}
