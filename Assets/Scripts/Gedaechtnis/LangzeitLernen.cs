using System.Collections.Generic;
using System.Linq;
using BilligAGI.Modelle;
using UnityEngine;

namespace BilligAGI.Gedaechtnis
{
    public class LangzeitLernen
    {
        private readonly ErfahrungsSpeicher speicher;
        private readonly AGIConfig config;
        private int zyklusSeitLetztemMonitor;

        public LangzeitLernen(ErfahrungsSpeicher speicher, AGIConfig config)
        {
            this.speicher = speicher;
            this.config = config;
            zyklusSeitLetztemMonitor = 0;
        }

        public void PriorisiereErfahrungen()
        {
            var alle = speicher.Alle();
            foreach (var erf in alle)
            {
                float relevanz = BerechneRelevanz(erf);
                // Relevanz beeinflusst Abrufpriorität (nicht direkt gespeichert,
                // aber ueber belohnung approximiert)
                if (relevanz < 0.1f && !erf.konzepte.Contains("one-shot"))
                {
                    erf.belohnung *= 0.95f; // Langsame Abwertung
                }
            }
        }

        public void KontrolliertesVergessen()
        {
            var alle = speicher.Alle();
            int vergessen = 0;

            foreach (var erf in alle)
            {
                // Schuetze sicherheitsrelevantes Wissen
                if (erf.konzepte.Contains("one-shot")) continue;
                if (erf.konzepte.Contains("sicherheit")) continue;
                if (Mathf.Abs(erf.belohnung) > 0.8f) continue; // Stark bewertete behalten

                // forgettingRate anwenden
                erf.belohnung *= (1f - config.forgettingRate);
                if (Mathf.Abs(erf.belohnung) < 0.01f)
                    vergessen++;
            }

            if (vergessen > 0)
                Debug.Log($"[LangzeitLernen] {vergessen} Erfahrungen stark abgewertet.");
        }

        public float DriftMonitor()
        {
            zyklusSeitLetztemMonitor++;
            if (zyklusSeitLetztemMonitor < 50) return 0f; // Nur alle 50 Zyklen pruefen
            zyklusSeitLetztemMonitor = 0;

            var alle = speicher.Alle();
            if (alle.Count < 20) return 0f;

            // Vergleiche Erfolgsquoten der letzten 20 vs. vorherige 20
            var sorted = alle.OrderByDescending(e => e.zeitstempel).ToList();
            var letzte20 = sorted.Take(20).ToList();
            var vorherige20 = sorted.Skip(20).Take(20).ToList();

            if (vorherige20.Count < 10) return 0f;

            float quotaLetzte = letzte20.Count(e => e.belohnung > 0) / (float)letzte20.Count;
            float quotaVorher = vorherige20.Count(e => e.belohnung > 0) / (float)vorherige20.Count;
            float drift = quotaVorher - quotaLetzte;

            if (drift > config.langzeitDriftSchwelle)
            {
                Debug.LogWarning($"[LangzeitLernen] Drift erkannt: Erfolgsquote sank um {drift:F2} " +
                    $"({quotaVorher:F2} → {quotaLetzte:F2}).");
            }

            return drift;
        }

        public void StabilisiereWissenskern()
        {
            var alle = speicher.Alle();

            // Haeufig bestaetigte Konzepte gegen Ueberschreiben absichern
            var konzeptHaeufigkeit = new Dictionary<string, int>();
            foreach (var erf in alle)
            {
                foreach (var k in erf.konzepte)
                {
                    if (!konzeptHaeufigkeit.ContainsKey(k))
                        konzeptHaeufigkeit[k] = 0;
                    konzeptHaeufigkeit[k]++;
                }
            }

            foreach (var kvp in konzeptHaeufigkeit)
            {
                if (kvp.Value >= config.langzeitStabilisierungsSchwelle)
                {
                    // Alle Erfahrungen mit diesem Konzept schuetzen
                    foreach (var erf in alle.Where(e => e.konzepte.Contains(kvp.Key)))
                    {
                        if (!erf.konzepte.Contains("stabil"))
                            erf.konzepte.Add("stabil");
                    }
                }
            }
        }

        private float BerechneRelevanz(Erfahrung erf)
        {
            float relevanz = 0f;

            // Absolute Belohnung
            relevanz += Mathf.Abs(erf.belohnung) * 0.4f;

            // Neuheit (kurzlich = relevanter)
            if (!string.IsNullOrEmpty(erf.zeitstempel))
            {
                var alter = System.DateTime.UtcNow -
                    System.DateTime.Parse(erf.zeitstempel).ToUniversalTime();
                relevanz += Mathf.Clamp01(1f - (float)alter.TotalHours / 24f) * 0.3f;
            }

            // Konzept-Reichtum
            relevanz += Mathf.Clamp01(erf.konzepte.Count / 5f) * 0.3f;

            return relevanz;
        }
    }
}
