using System;
using BilligAGI.Daten;
using UnityEngine;

namespace BilligAGI.Kern
{
    [Serializable]
    public class ZyklusStabilitaetsStatistik
    {
        public int gemesseneZyklen;
        public float durchschnittMs;
        public float maxMs;
        public int ueberlastZyklen;
        public int qosReduktionen;
    }

    // Quality-of-Service fuer den AGI-Zyklus:
    // Bei Lastspitzen werden nur Zusatzteile reduziert (nicht Kernlogik),
    // damit Antworten stabil bleiben und der Zyklus nicht ausfranst.
    public class ZyklusStabilisator
    {
        private ZyklusStabilitaetsStatistik statistik;

        private float emaMs;
        private int consecutiveOverload;

        private const float WARN_MS = 120f;
        private const float HARD_MS = 220f;
        private const float EMA_ALPHA = 0.15f;
        private const int OVERLOAD_DECAY_INTERVAL = 5;
        private const string PERSISTENZ_DATEI = "zyklus_stabilitaet.json";

        public ZyklusStabilisator()
        {
            statistik = DatenLader.Lade<ZyklusStabilitaetsStatistik>(PERSISTENZ_DATEI) ?? new ZyklusStabilitaetsStatistik();
            emaMs = Mathf.Max(0f, statistik.durchschnittMs);
        }

        public void RegistriereZyklus(float zyklusMs)
        {
            statistik.gemesseneZyklen++;
            statistik.maxMs = Mathf.Max(statistik.maxMs, zyklusMs);

            emaMs = emaMs <= 0.01f ? zyklusMs : Mathf.Lerp(emaMs, zyklusMs, EMA_ALPHA);
            statistik.durchschnittMs =
                ((statistik.durchschnittMs * (statistik.gemesseneZyklen - 1)) + zyklusMs) /
                Mathf.Max(1, statistik.gemesseneZyklen);

            if (zyklusMs >= WARN_MS || emaMs >= WARN_MS)
            {
                consecutiveOverload++;
                statistik.ueberlastZyklen++;
            }
            else if (statistik.gemesseneZyklen % OVERLOAD_DECAY_INTERVAL == 0)
            {
                consecutiveOverload = Mathf.Max(0, consecutiveOverload - 1);
            }

            if (statistik.gemesseneZyklen % 25 == 0)
                Persistiere();
        }

        public bool ErlaubeErweiterteAntworten()
        {
            bool hard = emaMs >= HARD_MS || consecutiveOverload >= 3;
            bool warn = emaMs >= WARN_MS || consecutiveOverload >= 1;

            if (hard)
            {
                statistik.qosReduktionen++;
                return false;
            }

            if (warn)
            {
                // Bei Warnstufe nur jede zweite Runde tiefe Zusatzanalyse.
                bool erlaubt = (statistik.gemesseneZyklen % 2) == 0;
                if (!erlaubt) statistik.qosReduktionen++;
                return erlaubt;
            }

            return true;
        }

        public string GetStatusText()
        {
            string stufe = emaMs >= HARD_MS || consecutiveOverload >= 3
                ? "HARD"
                : (emaMs >= WARN_MS || consecutiveOverload >= 1 ? "WARN" : "OK");

            return $"QoS={stufe} | EMA={emaMs:F1}ms | Avg={statistik.durchschnittMs:F1}ms | " +
                   $"Max={statistik.maxMs:F1}ms | Overload={statistik.ueberlastZyklen} | " +
                   $"Reduktionen={statistik.qosReduktionen}";
        }

        public ZyklusStabilitaetsStatistik GetStatistik() => statistik;

        public void Persistiere()
        {
            DatenLader.Speichere(PERSISTENZ_DATEI, statistik);
        }
    }
}
