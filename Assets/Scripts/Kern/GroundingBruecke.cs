using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BilligAGI.Modelle;
using BilligAGI.Sensorik;
using BilligAGI.Gedaechtnis;
using BilligAGI.Daten;
using UnityEngine;

namespace BilligAGI.Kern
{
    // ============================================================
    //  GroundingBruecke — Schliesst die Sensory-Language-Luecke
    //
    //  Problem: VAKOGLexikon mappt Woerter → sensorische Profile,
    //           aber bisher nur einweg:
    //           - LLM schaetzt Profile (nicht geerdet)
    //           - AktualisiereAusErfahrung() existiert, wird aber
    //             nicht systematisch aufgerufen
    //
    //  Loesung — Bidirektionaler Kreislauf:
    //
    //  1. ERFAHRUNG → WORT:
    //     Aus jeder Erfahrung Schluesselwoerter extrahieren,
    //     deren sensorische Profile aus den echten SensorDaten
    //     ins VAKOGLexikon schreiben (grounding)
    //
    //  2. WORT → ERINNERUNG:
    //     Bei Wort-Input: Suche Erfahrungen mit aehnlichem
    //     VAKOG-Profil → sensorische Erinnerung aktivieren
    //
    //  3. STATISTIK:
    //     Tracke welche Woerter erfahrungsgeerdet sind vs.
    //     nur LLM-geschaetzt → Grounding-Score des Vokabulars
    // ============================================================

    [Serializable]
    public class GroundingEintrag
    {
        public string wort;
        public int erfahrungsAnzahl;       // Wie oft erlebt
        public float groundingStaerke;     // 0–1: wie gut geerdet
        public string letztesUpdate;
        public List<string> erfahrungsIds = new(); // Letzte 5 Erfahrungen
    }

    [Serializable]
    public class GroundingStatistik
    {
        public int gesamtGeerdeteWoerter;
        public int gesamtGroundingUpdates;
        public Dictionary<string, GroundingEintrag> eintraege = new();
    }

    [Serializable]
    public class SensorischeErinnerung
    {
        public string wort;
        public VAKOGProfil profil;
        public string erfahrungsBeschreibung;
        public float aehnlichkeit;
    }

    public class GroundingBruecke
    {
        private readonly VAKOGLexikon lexikon;
        private readonly ErfahrungsSpeicher erfahrungen;
        private readonly AGIConfig config;

        private GroundingStatistik statistik;
        private int zyklusZaehler;

        private const int GROUNDING_INTERVALL = 5;       // Haeufiger als andere Systeme
        private const int MAX_WOERTER_PRO_ERFAHRUNG = 8; // Nicht jedes Wort grounded
        private const int MAX_ERFAHRUNGS_IDS = 5;
        private const string PERSISTENZ_DATEI = "grounding_statistik.json";

        // Stoppwoerter: Funktionswoerter die nicht geerdet werden muessen
        private static readonly HashSet<string> StoppWoerter = new(StringComparer.OrdinalIgnoreCase)
        {
            "der", "die", "das", "ein", "eine", "und", "oder", "aber", "wenn",
            "dann", "ist", "sind", "war", "hat", "haben", "wird", "werden",
            "mit", "von", "zu", "in", "an", "auf", "fuer", "um", "bei",
            "ich", "du", "er", "sie", "es", "wir", "ihr", "mein", "dein",
            "nicht", "kein", "keine", "auch", "noch", "schon", "nur", "sehr",
            "the", "a", "an", "is", "are", "was", "has", "have", "will",
            "with", "from", "to", "in", "on", "for", "at", "by", "it",
            "i", "you", "he", "she", "we", "they", "my", "your", "his",
            "not", "no", "also", "yet", "already", "only", "very",
            // Chat-/UI-Metadaten und sehr allgemeine Dialogwoerter nicht grounden.
            "hallo", "geht", "bitte", "hier", "erste", "erster", "chat", "chatverlauf",
            "vorherige", "vorheriger", "vorheriges", "sensorischer", "bezug", "aehnlichkeit"
        };

        public GroundingBruecke(VAKOGLexikon lexikon, ErfahrungsSpeicher erfahrungen, AGIConfig config)
        {
            this.lexikon = lexikon;
            this.erfahrungen = erfahrungen;
            this.config = config;

            statistik = DatenLader.Lade<GroundingStatistik>(PERSISTENZ_DATEI) ?? new GroundingStatistik();
            Debug.Log($"[GroundingBruecke] Initialisiert. {statistik.gesamtGeerdeteWoerter} geerdete Woerter.");
        }

        // ======== 1. ERFAHRUNG → WORT (Grounding) ========

        /// <summary>
        /// Wird bei jeder neuen Erfahrung aufgerufen.
        /// Extrahiert Schluesselwoerter und schreibt deren sensorische
        /// Signatur ins VAKOGLexikon.
        /// </summary>
        public void GroundeErfahrung(Erfahrung erfahrung)
        {
            if (erfahrung == null) return;
            if (erfahrung.sensorSnapshot == null && erfahrung.vakog == null) return;

            // Schluesselwoerter aus Aktion + Ergebnis extrahieren
            var woerter = ExtrahiereSchluesselwoerter(
                $"{erfahrung.aktion} {erfahrung.ergebnis}");

            if (woerter.Count == 0) return;

            // Fuer jedes Wort: SensorDaten ins Lexikon grounded
            foreach (var wort in woerter)
            {
                // Wenn SensorDaten vorhanden → direkt grounded (beste Qualitaet)
                if (erfahrung.sensorSnapshot != null)
                {
                    lexikon.AktualisiereAusErfahrung(wort, erfahrung.sensorSnapshot);
                }

                // Grounding-Statistik aktualisieren
                AktualisiereGroundingEintrag(wort, erfahrung.id);
            }

            statistik.gesamtGroundingUpdates++;
        }

        // ======== 2. WORT → ERINNERUNG (Sensorische Aktivierung) ========

        /// <summary>
        /// Bei einem Wort-Input: Suche Erfahrungen deren VAKOG-Profil
        /// aehnlich zum gespeicherten Profil des Wortes ist.
        /// Gibt sensorische Erinnerungen zurueck.
        /// </summary>
        public List<SensorischeErinnerung> AktiviereSensorischeErinnerung(string text, int maxErinnerungen = 3)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<SensorischeErinnerung>();

            var woerter = ExtrahiereSchluesselwoerter(text);
            var erinnerungen = new List<SensorischeErinnerung>();

            foreach (var wort in woerter)
            {
                // Profil aus Lexikon holen
                var profil = lexikon.GetProfil(wort);
                if (profil == null) continue;

                // Ist das Wort geerdet? (Nur geerdete Profile liefern echte Erinnerungen)
                if (!statistik.eintraege.TryGetValue(wort, out var eintrag))
                    continue;
                if (eintrag.groundingStaerke < 0.2f)
                    continue; // Zu schwach geerdet

                // Suche passende Erfahrungen
                var passende = SuchePassendeErfahrungen(profil, 2);
                foreach (var erf in passende)
                {
                    erinnerungen.Add(new SensorischeErinnerung
                    {
                        wort = wort,
                        profil = erf.vakog,
                        erfahrungsBeschreibung = $"{erf.aktion}: {erf.ergebnis}",
                        aehnlichkeit = BerechneVAKOGAehnlichkeit(profil, erf.vakog)
                    });
                }
            }

            // Nach Aehnlichkeit sortieren, Top-N zurueckgeben
            return erinnerungen
                .OrderByDescending(e => e.aehnlichkeit)
                .Take(maxErinnerungen)
                .ToList();
        }

        // ======== 3. Periodischer Tick ========

        /// <summary>
        /// Periodische Nachverarbeitung: Batch-Grounding ueber
        /// Erfahrungen die noch nicht verarbeitet wurden.
        /// </summary>
        public void ZyklusTick(Erfahrung aktuelleErfahrung)
        {
            zyklusZaehler++;

            // Jede Erfahrung wird sofort gegrounded
            if (aktuelleErfahrung != null)
                GroundeErfahrung(aktuelleErfahrung);

            // Periodisch: Persistieren
            if (zyklusZaehler % (GROUNDING_INTERVALL * 10) == 0)
            {
                Persistiere();
                lexikon.Persistiere();
            }
        }

        // ======== Status-Abfragen ========

        public string GetStatusText()
        {
            float groundingRate = BerechneGroundingRate();
            return $"Geerdete Woerter: {statistik.gesamtGeerdeteWoerter} | " +
                $"Updates: {statistik.gesamtGroundingUpdates} | " +
                $"Grounding-Rate: {groundingRate:P0} | " +
                $"Lexikon-Gesamt: {lexikon.AnzahlProfile}";
        }

        /// <summary>
        /// Anteil der Woerter im Lexikon die erfahrungsgeerdet sind (vs. nur LLM).
        /// </summary>
        public float BerechneGroundingRate()
        {
            int gesamt = lexikon.AnzahlProfile;
            if (gesamt == 0) return 0f;
            return Mathf.Clamp01(statistik.gesamtGeerdeteWoerter / (float)gesamt);
        }

        /// <summary>
        /// Pruefe ob ein bestimmtes Wort erfahrungsgeerdet ist.
        /// </summary>
        public GroundingEintrag GetGroundingFuerWort(string wort)
        {
            wort = wort.ToLowerInvariant().Trim();
            return statistik.eintraege.TryGetValue(wort, out var eintrag) ? eintrag : null;
        }

        /// <summary>
        /// Gibt die am staerksten geerdeten Woerter zurueck.
        /// </summary>
        public List<GroundingEintrag> GetTopGeerdeteWoerter(int n = 10)
        {
            return statistik.eintraege.Values
                .OrderByDescending(e => e.groundingStaerke)
                .ThenByDescending(e => e.erfahrungsAnzahl)
                .Take(n)
                .ToList();
        }

        public GroundingStatistik GetStatistik() => statistik;

        // ======== Hilfsfunktionen ========

        private List<string> ExtrahiereSchluesselwoerter(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            var woerter = text.ToLowerInvariant()
                .Split(new[] { ' ', ',', '.', '!', '?', '\n', '\t', ':', ';', '"', '\'' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalisiereToken)
                .Where(w => w.Length >= 3)                     // Mindestens 3 Zeichen
                .Where(w => !StoppWoerter.Contains(w))         // Keine Stoppwoerter
                .Distinct()
                .Take(MAX_WOERTER_PRO_ERFAHRUNG)
                .ToList();

            return woerter;
        }

        private string NormalisiereToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return string.Empty;

            var sb = new StringBuilder(token.Length);
            foreach (char c in token.Trim())
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
                    sb.Append(c);
            }

            return sb.ToString().Trim('-', '_');
        }

        private void AktualisiereGroundingEintrag(string wort, string erfahrungsId)
        {
            wort = wort.ToLowerInvariant().Trim();

            if (!statistik.eintraege.TryGetValue(wort, out var eintrag))
            {
                eintrag = new GroundingEintrag { wort = wort };
                statistik.eintraege[wort] = eintrag;
                statistik.gesamtGeerdeteWoerter++;
            }

            eintrag.erfahrungsAnzahl++;
            eintrag.letztesUpdate = DateTime.UtcNow.ToString("o");

            // Grounding-Staerke: Logarithmisch steigend (schneller Anfang, langsames Plateau)
            eintrag.groundingStaerke = Mathf.Clamp01(
                Mathf.Log(1f + eintrag.erfahrungsAnzahl) / Mathf.Log(1f + 20f));
            // Bei 1 Erfahrung: ~0.16, 5: ~0.60, 10: ~0.77, 20: ~1.0

            // Letzte Erfahrungs-IDs (Ring-Buffer)
            eintrag.erfahrungsIds.Add(erfahrungsId);
            while (eintrag.erfahrungsIds.Count > MAX_ERFAHRUNGS_IDS)
                eintrag.erfahrungsIds.RemoveAt(0);
        }

        private List<Erfahrung> SuchePassendeErfahrungen(VAKOGProfil zielProfil, int maxAnzahl)
        {
            var alle = erfahrungen.Alle();
            if (alle.Count == 0) return new List<Erfahrung>();

            // Nur Erfahrungen mit VAKOG-Profil
            return alle
                .Where(e => e.vakog != null)
                .Select(e => new { erfahrung = e, aehnlichkeit = BerechneVAKOGAehnlichkeit(zielProfil, e.vakog) })
                .Where(x => x.aehnlichkeit > 0.5f) // Mindest-Aehnlichkeit
                .OrderByDescending(x => x.aehnlichkeit)
                .Take(maxAnzahl)
                .Select(x => x.erfahrung)
                .ToList();
        }

        private float BerechneVAKOGAehnlichkeit(VAKOGProfil a, VAKOGProfil b)
        {
            if (a == null || b == null) return 0f;

            // Cosine-aehnliche Metrik ueber die 5 VAKOG-Dimensionen
            float dotProduct =
                a.visuell * b.visuell +
                a.auditiv * b.auditiv +
                a.kinaesthetisch * b.kinaesthetisch +
                a.olfaktorisch * b.olfaktorisch +
                a.gustatorisch * b.gustatorisch;

            float magA = Mathf.Sqrt(
                a.visuell * a.visuell +
                a.auditiv * a.auditiv +
                a.kinaesthetisch * a.kinaesthetisch +
                a.olfaktorisch * a.olfaktorisch +
                a.gustatorisch * a.gustatorisch);

            float magB = Mathf.Sqrt(
                b.visuell * b.visuell +
                b.auditiv * b.auditiv +
                b.kinaesthetisch * b.kinaesthetisch +
                b.olfaktorisch * b.olfaktorisch +
                b.gustatorisch * b.gustatorisch);

            if (magA < 0.001f || magB < 0.001f) return 0f;
            return Mathf.Clamp01(dotProduct / (magA * magB));
        }

        private void Persistiere()
        {
            DatenLader.Speichere(PERSISTENZ_DATEI, statistik);
        }
    }
}
