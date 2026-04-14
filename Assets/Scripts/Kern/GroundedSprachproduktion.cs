using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BilligAGI.Daten;
using BilligAGI.Modelle;
using BilligAGI.Welt;
using UnityEngine;

namespace BilligAGI.Kern
{
    [Serializable]
    public class SprachGroundingStatistik
    {
        public int antwortenVeredelt;
        public int wortErklaerungen;
        public int entscheidungsErklaerungen;
        public float durchschnittlicheGroundingStaerke;
        public int letzteAktualisierung;
    }

    public class GroundedSprachproduktion
    {
        private readonly GroundingBruecke groundingBruecke;
        private readonly WeltModell weltModell;
        private readonly MentaleSimulation mentaleSimulation;
        private readonly IntuitiverPhysikSimulator physikSimulator;

        private SprachGroundingStatistik statistik;
        private int zyklusZaehler;

        private const int MAX_WORT_ANZAHL = 6;
        private const int PERSIST_INTERVALL = 30;
        private const string PERSISTENZ_DATEI = "grounded_sprachproduktion.json";

        private static readonly HashSet<string> Stoppwoerter = new(StringComparer.OrdinalIgnoreCase)
        {
            "der", "die", "das", "ein", "eine", "und", "oder", "aber", "mit", "von", "zu",
            "im", "in", "auf", "an", "am", "ist", "sind", "war", "ich", "du", "wir", "ihr"
        };

        public GroundedSprachproduktion(
            GroundingBruecke groundingBruecke,
            WeltModell weltModell,
            MentaleSimulation mentaleSimulation,
            IntuitiverPhysikSimulator physikSimulator)
        {
            this.groundingBruecke = groundingBruecke;
            this.weltModell = weltModell;
            this.mentaleSimulation = mentaleSimulation;
            this.physikSimulator = physikSimulator;

            statistik = DatenLader.Lade<SprachGroundingStatistik>(PERSISTENZ_DATEI) ?? new SprachGroundingStatistik();
            Debug.Log($"[GroundedSprache] Initialisiert. Veredelte Antworten: {statistik.antwortenVeredelt}");
        }

        public string VeredleAntwort(string input, string basisAntwort, float[] zustandsVektor = null)
        {
            if (string.IsNullOrWhiteSpace(basisAntwort) || string.IsNullOrWhiteSpace(input))
                return basisAntwort;

            var woerter = ExtrahiereSchluesselwoerter(input);
            if (woerter.Count == 0)
                return basisAntwort;

            var geerdete = new List<GroundingEintrag>();
            foreach (var wort in woerter)
            {
                var eintrag = groundingBruecke?.GetGroundingFuerWort(wort);
                if (eintrag != null && eintrag.groundingStaerke >= 0.2f)
                    geerdete.Add(eintrag);
            }

            if (geerdete.Count == 0)
                return basisAntwort;

            float avg = geerdete.Average(g => g.groundingStaerke);
            statistik.antwortenVeredelt++;
            statistik.durchschnittlicheGroundingStaerke =
                ((statistik.durchschnittlicheGroundingStaerke * (statistik.antwortenVeredelt - 1)) + avg) /
                Mathf.Max(1, statistik.antwortenVeredelt);

            var sb = new StringBuilder();
            sb.AppendLine(basisAntwort.Trim());

            var erinnerungen = groundingBruecke?.AktiviereSensorischeErinnerung(input, 2) ?? new List<SensorischeErinnerung>();
            if (erinnerungen.Count > 0)
            {
                var top = erinnerungen.OrderByDescending(e => e.aehnlichkeit).First();
                sb.AppendLine();
                sb.Append($"Sensorischer Bezug: '{top.wort}' fuehlt sich erfahrungsnah an (Aehnlichkeit {top.aehnlichkeit:F2}).");
            }

            string weltBezug = BaueWeltBezug(input);
            if (!string.IsNullOrEmpty(weltBezug))
            {
                sb.AppendLine();
                sb.Append(weltBezug);
            }

            if (zustandsVektor != null && mentaleSimulation != null)
            {
                var pfad = mentaleSimulation.FindeBesteSequenz(zustandsVektor, 3);
                if (pfad != null && pfad.aktionen != null && pfad.aktionen.Length > 0)
                {
                    sb.AppendLine();
                    sb.Append($"Naechster plausibler Schritt: {pfad.aktionen[0]} (simulierte Konfidenz {pfad.konfidenz:F2}).");
                }
            }

            if (zyklusZaehler % PERSIST_INTERVALL == 0)
                Persistiere();

            return sb.ToString().Trim();
        }

        public string ErklaereWort(string wort)
        {
            if (string.IsNullOrWhiteSpace(wort))
                return "Bitte ein Wort angeben.";

            string key = wort.ToLowerInvariant().Trim();
            var eintrag = groundingBruecke?.GetGroundingFuerWort(key);
            if (eintrag == null)
                return $"'{key}' ist aktuell nicht erfahrungsgeerdet.";

            statistik.wortErklaerungen++;

            var erinnerungen = groundingBruecke?.AktiviereSensorischeErinnerung(key, 3) ?? new List<SensorischeErinnerung>();
            var top = erinnerungen.OrderByDescending(e => e.aehnlichkeit).FirstOrDefault();

            var text = $"'{key}': Grounding-Staerke {eintrag.groundingStaerke:F2}, {eintrag.erfahrungsAnzahl} Erfahrungen.";
            if (top != null)
            {
                text += $" Beispiel-Erinnerung: {top.erfahrungsBeschreibung} (Aehnlichkeit {top.aehnlichkeit:F2}).";
            }

            return text;
        }

        public string ErklaereEntscheidung(float[] zustandsVektor)
        {
            if (zustandsVektor == null)
                return "Keine Entscheidungs-Erklaerung moeglich (kein Zustandsvektor).";

            statistik.entscheidungsErklaerungen++;
            var teile = new List<string>();

            if (mentaleSimulation != null)
            {
                var pfad = mentaleSimulation.FindeBesteSequenz(zustandsVektor, 4);
                if (pfad != null && pfad.aktionen != null && pfad.aktionen.Length > 0)
                {
                    teile.Add($"Simulation priorisiert '{pfad.aktionen[0]}' (Reward {pfad.kumulativeBelohnung:F2}, Konfidenz {pfad.konfidenz:F2}).");
                }
            }

            var intuition = physikSimulator?.GetLetzteIntuition();
            if (intuition?.stabilitaet != null)
            {
                teile.Add($"Physik-Intuition meldet Stabilitaet {intuition.stabilitaet.stabilitaet:F2}: {intuition.stabilitaet.beschreibung}.");
            }

            if (teile.Count == 0)
                return "Noch keine Simulations- oder Physikdaten fuer eine Erklaerung vorhanden.";

            return string.Join(" ", teile);
        }

        public void ZyklusTick()
        {
            zyklusZaehler++;
            statistik.letzteAktualisierung = zyklusZaehler;

            if (zyklusZaehler % PERSIST_INTERVALL == 0)
                Persistiere();
        }

        public string GetStatusText()
        {
            return $"Antworten veredelt: {statistik.antwortenVeredelt} | " +
                   $"Wort-Erklaerungen: {statistik.wortErklaerungen} | " +
                   $"Entscheidungs-Erklaerungen: {statistik.entscheidungsErklaerungen} | " +
                   $"Ø Grounding-Staerke: {statistik.durchschnittlicheGroundingStaerke:F2}";
        }

        public SprachGroundingStatistik GetStatistik() => statistik;

        public void Persistiere()
        {
            DatenLader.Speichere(PERSISTENZ_DATEI, statistik);
        }

        private string BaueWeltBezug(string input)
        {
            var zustand = weltModell?.zustand;
            if (zustand?.objekte == null || zustand.objekte.Count == 0)
                return string.Empty;

            string text = input.ToLowerInvariant();
            var treffer = zustand.objekte.Values
                .Where(o => !string.IsNullOrWhiteSpace(o.name) && text.Contains(o.name.ToLowerInvariant()))
                .Take(2)
                .ToList();

            if (treffer.Count == 0)
                return string.Empty;

            var teile = treffer.Select(o =>
            {
                string ort = (o.position != null && o.position.Length >= 3)
                    ? $"[{o.position[0]:F1},{o.position[1]:F1},{o.position[2]:F1}]"
                    : "[unbekannt]";
                string zust = string.IsNullOrWhiteSpace(o.zustand) ? "ohne Zustand" : o.zustand;
                return $"{o.name} ({zust}, Pos {ort})";
            });

            return "Weltbezug: " + string.Join("; ", teile) + ".";
        }

        private List<string> ExtrahiereSchluesselwoerter(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            return text.ToLowerInvariant()
                .Split(new[] { ' ', ',', '.', '!', '?', ':', ';', '\n', '\t', '"', '\'' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 3)
                .Where(w => !Stoppwoerter.Contains(w))
                .Distinct()
                .Take(MAX_WORT_ANZAHL)
                .ToList();
        }
    }
}
