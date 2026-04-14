using System.Collections.Generic;
using BilligAGI.Modelle;
using BilligAGI.Welt;
using UnityEngine;

namespace BilligAGI.Intentionalitaet
{
    public class Monitor
    {
        private readonly WeltModell weltModell;
        private readonly Kern.EmotionsSystem emotionen;

        public Monitor(WeltModell weltModell, Kern.EmotionsSystem emotionen)
        {
            this.weltModell = weltModell;
            this.emotionen = emotionen;
        }

        public MonitorErgebnis Ueberwache(Plan plan, int aktuellerSchritt)
        {
            var ergebnis = new MonitorErgebnis();

            if (plan == null || plan.aktionen.Count == 0)
            {
                ergebnis.entscheidung = MonitorEntscheidung.ABBRECHEN;
                ergebnis.grund = "Kein Plan.";
                return ergebnis;
            }

            if (aktuellerSchritt >= plan.aktionen.Count)
            {
                ergebnis.entscheidung = MonitorEntscheidung.WEITER;
                ergebnis.grund = "Plan abgeschlossen.";
                ergebnis.planAbgeschlossen = true;
                return ergebnis;
            }

            var aktion = plan.aktionen[aktuellerSchritt];

            // Zu viele Umplanungen?
            if (plan.umplanungen > 3)
            {
                ergebnis.entscheidung = MonitorEntscheidung.NEUES_ZIEL;
                ergebnis.grund = $"Zu viele Umplanungen ({plan.umplanungen}).";
                return ergebnis;
            }

            // Emotionaler Notstand?
            if (emotionen != null && emotionen.KritischerZustand())
            {
                ergebnis.entscheidung = MonitorEntscheidung.UMPLANEN;
                ergebnis.grund = "Kritischer emotionaler Zustand (Angst + Frustration).";
                return ergebnis;
            }

            ergebnis.entscheidung = MonitorEntscheidung.WEITER;
            ergebnis.grund = $"Schritt {aktuellerSchritt + 1}/{plan.aktionen.Count}: {aktion.name}";
            return ergebnis;
        }

        public string ErkenneUeberraschung(WeltZustand erwartet, WeltZustand tatsaechlich)
        {
            if (erwartet == null || tatsaechlich == null) return null;

            float abweichung = weltModell?.ErwartungVsRealitaet(erwartet, tatsaechlich) ?? 0f;

            if (abweichung > 0.3f)
            {
                string beschreibung = $"Ueberraschung! Abweichung: {abweichung:F2}. ";

                // Details sammeln
                foreach (var kvp in erwartet.objekte)
                {
                    if (!tatsaechlich.objekte.ContainsKey(kvp.Key))
                    {
                        beschreibung += $"Objekt '{kvp.Value.name}' fehlt. ";
                    }
                    else
                    {
                        var erw = kvp.Value;
                        var tat = tatsaechlich.objekte[kvp.Key];
                        if (erw.zustand != tat.zustand)
                            beschreibung += $"'{tat.name}' ist '{tat.zustand}' statt '{erw.zustand}'. ";
                    }
                }

                // Emotionalen Zustand aktualisieren
                if (emotionen != null)
                {
                    var tempErfahrung = new Erfahrung
                    {
                        belohnung = abweichung > 0.5f ? -0.8f : -0.3f,
                        emotionalerZustand = new EmotionalerZustand
                        {
                            ueberraschung = abweichung
                        }
                    };
                    emotionen.Aktualisiere(tempErfahrung);
                }

                return beschreibung;
            }

            return null;
        }
    }

    [System.Serializable]
    public class MonitorErgebnis
    {
        public MonitorEntscheidung entscheidung;
        public string grund;
        public bool planAbgeschlossen;
    }

    public enum MonitorEntscheidung
    {
        WEITER,
        UMPLANEN,
        ABBRECHEN,
        NEUES_ZIEL
    }
}
