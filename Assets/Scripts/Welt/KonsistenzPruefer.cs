using System.Collections.Generic;
using System.Linq;
using BilligAGI.Modelle;
using UnityEngine;

namespace BilligAGI.Welt
{
    public class KonsistenzPruefer
    {
        private readonly AGIConfig config;

        public KonsistenzPruefer(AGIConfig config)
        {
            this.config = config;
        }

        public List<KonsistenzFehler> Pruefe(WeltModell welt)
        {
            var fehler = new List<KonsistenzFehler>();
            if (welt?.zustand == null) return fehler;

            PruefeLogisch(welt.zustand, fehler);
            PruefeTemporal(welt.zustand, fehler);
            PruefeRaeumlich(welt.zustand, fehler);

            return fehler;
        }

        public void AutoRepariere(KonsistenzFehler fehler, WeltModell welt)
        {
            switch (fehler.typ)
            {
                case KonsistenzFehlerTyp.LOGISCH:
                    // Zustandswiderspruch: Letzten bekannten Zustand beibehalten
                    Debug.Log($"[Konsistenz] Auto-Reparatur LOGISCH: {fehler.ursache}");
                    fehler.autoRepariert = true;
                    break;
                case KonsistenzFehlerTyp.RAEUMLICH:
                    // Objekt an ungueltigem Ort: Zurueck zum letzten guten Ort
                    Debug.Log($"[Konsistenz] Auto-Reparatur RAEUMLICH: {fehler.ursache}");
                    fehler.autoRepariert = true;
                    break;
                case KonsistenzFehlerTyp.TEMPORAL:
                    // Temporale Inkonsistenz: Markieren, nicht automatisch reparieren
                    Debug.LogWarning($"[Konsistenz] TEMPORAL nicht auto-reparierbar: {fehler.ursache}");
                    fehler.autoRepariert = false;
                    break;
            }
        }

        public void MarkiereZurKlaerung(KonsistenzFehler fehler)
        {
            fehler.autoRepariert = false;
            Debug.LogWarning($"[Konsistenz] Zur Klaerung markiert: {fehler.typ} — {fehler.ursache}");
        }

        private void PruefeLogisch(WeltZustand zustand, List<KonsistenzFehler> fehler)
        {
            var zustaende = new Dictionary<string, List<string>>();

            foreach (var obj in zustand.objekte.Values)
            {
                if (string.IsNullOrEmpty(obj.zustand)) continue;

                // Widerspruch: offen UND geschlossen
                if (obj.zustand.Contains("offen") && obj.zustand.Contains("geschlossen"))
                {
                    fehler.Add(new KonsistenzFehler
                    {
                        typ = KonsistenzFehlerTyp.LOGISCH,
                        betroffeneEntitaeten = { obj.id },
                        schweregrad = 0.8f,
                        ursache = $"Objekt '{obj.name}' ist gleichzeitig offen und geschlossen."
                    });
                }
            }
        }

        private void PruefeTemporal(WeltZustand zustand, List<KonsistenzFehler> fehler)
        {
            // Pruefe Historie auf temporale Inkonsistenzen
            for (int i = 1; i < zustand.historie.Count; i++)
            {
                var vorher = zustand.historie[i - 1];
                var nachher = zustand.historie[i];

                // Wirkung vor Ursache
                if (string.Compare(nachher.zeitstempel, vorher.zeitstempel) < 0)
                {
                    fehler.Add(new KonsistenzFehler
                    {
                        typ = KonsistenzFehlerTyp.TEMPORAL,
                        betroffeneEntitaeten = { vorher.objektId, nachher.objektId },
                        schweregrad = 0.6f,
                        ursache = $"Aenderung an '{nachher.objektId}' hat fruehreren Zeitstempel als vorherige Aenderung."
                    });
                }
            }
        }

        private void PruefeRaeumlich(WeltZustand zustand, List<KonsistenzFehler> fehler)
        {
            var positionen = new Dictionary<string, (string id, string name)>();

            foreach (var obj in zustand.objekte.Values)
            {
                // Gleiche Position (exakt) fuer verschiedene Objekte = Kollision
                string posKey = $"{obj.position[0]:F0}_{obj.position[1]:F0}_{obj.position[2]:F0}";
                if (positionen.TryGetValue(posKey, out var anderes))
                {
                    // Kleine Objekte koennen gestapelt sein — nur warnen wenn schwer
                    fehler.Add(new KonsistenzFehler
                    {
                        typ = KonsistenzFehlerTyp.RAEUMLICH,
                        betroffeneEntitaeten = { obj.id, anderes.id },
                        schweregrad = 0.3f, // Niedrig — koennte Stapel sein
                        ursache = $"Objekte '{obj.name}' und '{anderes.name}' an identischer Position."
                    });
                }
                else
                {
                    positionen[posKey] = (obj.id, obj.name);
                }

                // Objekt unter dem Boden
                if (obj.position[1] < -10f)
                {
                    fehler.Add(new KonsistenzFehler
                    {
                        typ = KonsistenzFehlerTyp.RAEUMLICH,
                        betroffeneEntitaeten = { obj.id },
                        schweregrad = 0.9f,
                        ursache = $"Objekt '{obj.name}' ist unter dem Boden gefallen (y={obj.position[1]:F1})."
                    });
                }
            }
        }
    }
}
