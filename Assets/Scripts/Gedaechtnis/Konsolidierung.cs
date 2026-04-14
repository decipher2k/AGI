using System.Collections.Generic;
using System.Linq;
using BilligAGI.Modelle;
using UnityEngine;

namespace BilligAGI.Gedaechtnis
{
    public class Konsolidierung
    {
        private readonly ErfahrungsSpeicher speicher;
        private readonly AGIConfig config;

        public Konsolidierung(ErfahrungsSpeicher speicher, AGIConfig config)
        {
            this.speicher = speicher;
            this.config = config;
        }

        public void Konsolidiere()
        {
            var alle = speicher.Alle();
            if (alle.Count < 3) return;

            VerallgemeinereAehnliche(alle);
            ErkenneWiderspruche(alle);
            WeiteAltAb(alle);

            Debug.Log($"[Konsolidierung] {alle.Count} Erfahrungen konsolidiert.");
        }

        private void VerallgemeinereAehnliche(List<Erfahrung> alle)
        {
            // Gruppiere nach Aktion
            var gruppen = alle.GroupBy(e => e.aktion ?? "").Where(g => g.Count() >= 3);

            foreach (var gruppe in gruppen)
            {
                var erfahrungen = gruppe.ToList();

                // Alle gleiche Aktion, gleiches Ergebnis?
                var ergebnisGruppen = erfahrungen.GroupBy(e => e.ergebnis ?? "");
                foreach (var eg in ergebnisGruppen)
                {
                    if (eg.Count() >= 3)
                    {
                        // Konfidenz erhoehen
                        float konfidenzBoost = Mathf.Min(0.3f, eg.Count() * 0.05f);
                        foreach (var erf in eg)
                        {
                            erf.belohnung = Mathf.Clamp(erf.belohnung + konfidenzBoost, -1f, 1f);
                        }
                        Debug.Log($"[Konsolidierung] Verallgemeinert: '{gruppe.Key}' → '{eg.Key}' ({eg.Count()}x → Konfidenz +{konfidenzBoost:F2})");
                    }
                }
            }
        }

        private void ErkenneWiderspruche(List<Erfahrung> alle)
        {
            var gruppen = alle.GroupBy(e => e.aktion ?? "");

            foreach (var gruppe in gruppen)
            {
                var erfahrungen = gruppe.ToList();
                if (erfahrungen.Count < 2) continue;

                var ergebnisse = erfahrungen.Select(e => e.ergebnis ?? "").Distinct().ToList();
                if (ergebnisse.Count > 1)
                {
                    // Widerspruch: gleiche Aktion, verschiedene Ergebnisse
                    // Koennte kontextabhaengig sein oder tatsaechlicher Widerspruch
                    Debug.LogWarning($"[Konsolidierung] Widerspruch: '{gruppe.Key}' hat {ergebnisse.Count} verschiedene Ergebnisse.");
                    foreach (var erf in erfahrungen)
                    {
                        if (!erf.konzepte.Contains("widerspruch"))
                            erf.konzepte.Add("widerspruch");
                    }
                }
            }
        }

        private void WeiteAltAb(List<Erfahrung> alle)
        {
            if (alle.Count <= config.langzeitMaxErfahrungen) return;

            // Sortiere nach Zeitstempel (aelteste zuerst)
            var nachZeit = alle.OrderBy(e => e.zeitstempel).ToList();

            int zuVergessen = alle.Count - config.langzeitMaxErfahrungen;
            for (int i = 0; i < zuVergessen; i++)
            {
                var erf = nachZeit[i];
                // Sicherheitsrelevantes nicht vergessen
                if (erf.konzepte.Contains("one-shot") || erf.konzepte.Contains("sicherheit"))
                    continue;

                // Abwertung statt Loeschung
                erf.belohnung *= (1f - config.forgettingRate);
            }
        }

        public List<Erfahrung> PruefeKonzeptWiderspruche(List<Erfahrung> erfahrungen)
        {
            return erfahrungen.Where(e => e.konzepte.Contains("widerspruch")).ToList();
        }
    }
}
