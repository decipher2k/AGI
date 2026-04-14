using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using BilligAGI.Modelle;
using BilligAGI.Gedaechtnis;
using BilligAGI.Intentionalitaet;

namespace BilligAGI.Kern
{
    // ============================================================
    //  HypothesenEngine — Aktive Hypothesenbildung + Experimentplanung
    //
    //  Geht ueber NeugierSystem hinaus:
    //  1. Beobachtung → "Das ist seltsam" (Anomalie-Erkennung)
    //  2. Hypothesenbildung → "Vielleicht weil..." (LLM-gestuetzt)
    //  3. Experimentdesign → "Um das zu pruefen, muesste ich..."
    //  4. Auswertung → "Hat die Hypothese gestimmt?"
    //
    //  Nutzt: NeugierSystem (Trigger), KausalGraph (Vorhersagen),
    //         KausalesReasoning (Interventions-Simulation),
    //         ZielManager (Experiment als Ziel eintragen)
    // ============================================================

    public enum HypothesenStatus
    {
        Offen,           // Gerade gebildet, noch nicht getestet
        InPruefung,      // Experiment laeuft
        Bestaetigt,      // Durch Evidenz gestuetzt
        Widerlegt,       // Durch Evidenz widerlegt
        Unklar           // Evidenz nicht eindeutig
    }

    [Serializable]
    public class AktiveHypothese
    {
        public string id;
        public string beschreibung;                     // "Rote Objekte sind schwerer als blaue"
        public string vorhersage;                       // "Wenn ich ein rotes Objekt greife, brauche ich mehr Kraft"
        public string experiment;                       // "Greife ein rotes und ein blaues Objekt, vergleiche Belohnung"
        public HypothesenStatus status;
        public float konfidenz;                         // Aktuelle Ueber zeugung [0–1]
        public float prioritaet;                        // Wie wichtig zu testen [0–1]
        public string domaene;
        public List<string> stuetzendeErfahrungen = new();
        public List<string> widersprechendeErfahrungen = new();
        public string zeitstempel;

        public AktiveHypothese()
        {
            id = Guid.NewGuid().ToString("N").Substring(0, 8);
            zeitstempel = DateTime.UtcNow.ToString("o");
            status = HypothesenStatus.Offen;
        }
    }

    [Serializable]
    public class HypothesenErgebnis
    {
        public bool neueHypothese;
        public AktiveHypothese hypothese;
        public string zusammenfassung;
    }

    public class HypothesenEngine
    {
        private readonly LLMAdapter llm;
        private readonly KausalGraph kausalGraph;
        private readonly KausalesReasoning kausalesReasoning;
        private readonly ErfahrungsSpeicher erfahrungen;
        private readonly NeugierSystem neugier;
        private readonly SelbstModell selbstModell;
        private readonly AGIConfig config;

        private List<AktiveHypothese> hypothesen = new();
        private int zyklusSeitLetzterBildung;

        private const int BILDUNGS_INTERVALL = 30;  // Alle N Zyklen
        private const int MAX_AKTIVE_HYPOTHESEN = 20;
        private const string SPEICHER_PFAD = "hypothesen.json";

        private const string HYPOTHESEN_SYSTEM = @"Du bist ein wissenschaftlicher Hypothesen-Generator. 
Analysiere die Beobachtungen und bilde TESTBARE Hypothesen.

Antworte als JSON:
{
  ""hypothese"": ""klare, praegnante Aussage die wahr oder falsch sein kann"",
  ""vorhersage"": ""was muesste passieren, WENN die Hypothese stimmt"",
  ""experiment"": ""konkrete Aktion die der Agent ausfuehren kann, um die Hypothese zu pruefen"",
  ""konfidenz"": 0.0-1.0,
  ""domaene"": ""physik|sozial|navigation|planung|allgemein""
}

Regeln:
- Die Hypothese muss FALSIFIZIERBAR sein (Karl Popper)
- Das Experiment muss mit den verfuegbaren Aktionen durchfuehrbar sein
  (Bewegen, Greifen, Ablegen, Werfen, Schieben, Beobachten, Hoeren, 
   Interagieren, Sprechen, ZeigenAuf, Drehen, Springen)
- Bevorzuge ueberraschende/kontraintuitive Hypothesen
- Wenn nichts Interessantes zu finden ist: {""hypothese"": null}";

        public HypothesenEngine(
            LLMAdapter llm,
            KausalGraph kausalGraph,
            KausalesReasoning kausalesReasoning,
            ErfahrungsSpeicher erfahrungen,
            NeugierSystem neugier,
            SelbstModell selbstModell,
            AGIConfig config)
        {
            this.llm = llm;
            this.kausalGraph = kausalGraph;
            this.kausalesReasoning = kausalesReasoning;
            this.erfahrungen = erfahrungen;
            this.neugier = neugier;
            this.selbstModell = selbstModell;
            this.config = config;

            LadeVonDisk();
        }

        // ===========================================================
        //  1. ZYKLUS-HOOK: Periodische Hypothesenbildung
        // ===========================================================

        public async Task<HypothesenErgebnis> ZyklusTick(Erfahrung letzteErfahrung)
        {
            zyklusSeitLetzterBildung++;

            // Bestehende Hypothesen gegen neue Erfahrung pruefen
            if (letzteErfahrung != null)
                PruefeGegenErfahrung(letzteErfahrung);

            // Periodisch neue Hypothesen bilden
            if (zyklusSeitLetzterBildung < BILDUNGS_INTERVALL)
                return null;

            zyklusSeitLetzterBildung = 0;
            return await BildeNeueHypothese();
        }

        // ===========================================================
        //  2. HYPOTHESENBILDUNG: Anomalien → Hypothesen
        // ===========================================================

        private async Task<HypothesenErgebnis> BildeNeueHypothese()
        {
            if (hypothesen.Count(h => h.status == HypothesenStatus.Offen) >= MAX_AKTIVE_HYPOTHESEN)
                return null;

            // Quellen fuer Hypothesen sammeln
            var anomalien = FindeAnomalien();
            var neugierHypothesen = neugier.GetAktive();
            var schwacheKanten = kausalGraph.GetNiedrigeKonfidenz(0.4f);

            // Keine Anomalien? Nichts zu tun
            if (anomalien.Count == 0 && neugierHypothesen.Count == 0 && schwacheKanten.Count == 0)
                return null;

            string prompt = BaueHypothesenPrompt(anomalien, neugierHypothesen, schwacheKanten);
            var antwort = await llm.FreieAnfrage(prompt, HYPOTHESEN_SYSTEM);
            if (antwort == null || string.IsNullOrWhiteSpace(antwort.inhalt))
                return null;

            // Parsen
            string json = antwort.inhalt;
            string hypText = ExtractJsonString(json, "hypothese");
            if (string.IsNullOrEmpty(hypText) || hypText == "null")
                return null;

            // Duplikat-Check
            if (hypothesen.Any(h => h.beschreibung.Equals(hypText, StringComparison.OrdinalIgnoreCase)))
                return null;

            var neueHypothese = new AktiveHypothese
            {
                beschreibung = hypText,
                vorhersage = ExtractJsonString(json, "vorhersage") ?? "",
                experiment = ExtractJsonString(json, "experiment") ?? "",
                konfidenz = ExtractJsonFloat(json, "konfidenz", 0.5f),
                domaene = ExtractJsonString(json, "domaene") ?? "allgemein",
                prioritaet = BerechneHypothesenPrioritaet(hypText)
            };

            hypothesen.Add(neueHypothese);
            SpeichereAufDisk();

            Debug.Log($"[HypothesenEngine] Neue Hypothese: '{hypText}' → Test: {neueHypothese.experiment}");

            return new HypothesenErgebnis
            {
                neueHypothese = true,
                hypothese = neueHypothese,
                zusammenfassung = $"Hypothese: {hypText}\nVorhersage: {neueHypothese.vorhersage}\nExperiment: {neueHypothese.experiment}"
            };
        }

        // ===========================================================
        //  3. PRUEFUNG: Erfahrungen gegen Hypothesen testen
        // ===========================================================

        private void PruefeGegenErfahrung(Erfahrung e)
        {
            foreach (var hyp in hypothesen.Where(h => h.status == HypothesenStatus.Offen ||
                                                       h.status == HypothesenStatus.InPruefung))
            {
                // Ist diese Erfahrung relevant fuer die Hypothese?
                float relevanz = BerechneRelevanz(hyp, e);
                if (relevanz < 0.3f) continue;

                hyp.status = HypothesenStatus.InPruefung;

                // Stuetzt oder widerspricht die Erfahrung?
                bool stuetzt = PasstZurVorhersage(hyp, e);

                if (stuetzt)
                {
                    hyp.stuetzendeErfahrungen.Add(e.id);
                    hyp.konfidenz = Mathf.Clamp01(hyp.konfidenz + 0.1f);
                }
                else
                {
                    hyp.widersprechendeErfahrungen.Add(e.id);
                    hyp.konfidenz = Mathf.Clamp01(hyp.konfidenz - 0.15f);
                }

                // Status-Update nach genuegend Evidenz
                int gesamt = hyp.stuetzendeErfahrungen.Count + hyp.widersprechendeErfahrungen.Count;
                if (gesamt >= 3)
                {
                    float stuetzRate = (float)hyp.stuetzendeErfahrungen.Count / gesamt;
                    if (stuetzRate >= 0.7f)
                    {
                        hyp.status = HypothesenStatus.Bestaetigt;
                        // Bestaetigt → als kausale Kante mit hoher Konfidenz eintragen
                        kausalGraph.FuegeKausalitaetHinzu(
                            hyp.beschreibung, hyp.vorhersage, hyp.konfidenz, "mechanismus");
                        Debug.Log($"[HypothesenEngine] BESTAETIGT: {hyp.beschreibung} ({stuetzRate:P0})");
                    }
                    else if (stuetzRate <= 0.3f)
                    {
                        hyp.status = HypothesenStatus.Widerlegt;
                        Debug.Log($"[HypothesenEngine] WIDERLEGT: {hyp.beschreibung} ({stuetzRate:P0})");
                    }
                    else if (gesamt >= 5)
                    {
                        hyp.status = HypothesenStatus.Unklar;
                    }
                }
            }

            // Alte abgeschlossene Hypothesen aufraeumen
            if (hypothesen.Count > MAX_AKTIVE_HYPOTHESEN * 2)
            {
                hypothesen = hypothesen
                    .Where(h => h.status == HypothesenStatus.Offen ||
                                h.status == HypothesenStatus.InPruefung ||
                                h.status == HypothesenStatus.Bestaetigt)
                    .Concat(hypothesen
                        .Where(h => h.status == HypothesenStatus.Widerlegt ||
                                    h.status == HypothesenStatus.Unklar)
                        .OrderByDescending(h => h.zeitstempel)
                        .Take(10))
                    .ToList();
            }
        }

        // ===========================================================
        //  4. ANOMALIE-ERKENNUNG: "Das ist seltsam"
        // ===========================================================

        private List<string> FindeAnomalien()
        {
            var anomalien = new List<string>();
            var alle = erfahrungen.Alle();
            if (alle.Count < 10) return anomalien;

            var letzte = alle.OrderByDescending(e => e.zeitstempel).Take(20).ToList();
            float durchschnittBelohnung = letzte.Average(e => e.belohnung);

            // 1. Ueberraschend schlechte/gute Ergebnisse
            foreach (var e in letzte.Take(5))
            {
                float abweichung = Math.Abs(e.belohnung - durchschnittBelohnung);
                if (abweichung > 0.5f)
                {
                    string art = e.belohnung > durchschnittBelohnung
                        ? "ueberraschend gut" : "ueberraschend schlecht";
                    anomalien.Add($"'{e.aktion}' war {art} (Δ={abweichung:F2})");
                }
            }

            // 2. Widersprueche: Gleiche Aktion, unterschiedliche Ergebnisse
            var aktionsGruppen = letzte.GroupBy(e => e.aktion?.ToLowerInvariant() ?? "");
            foreach (var gruppe in aktionsGruppen.Where(g => g.Count() >= 2))
            {
                float min = gruppe.Min(e => e.belohnung);
                float max = gruppe.Max(e => e.belohnung);
                if (max - min > 0.6f)
                {
                    anomalien.Add($"'{gruppe.Key}' fuehrt zu unterschiedlichen Ergebnissen " +
                        $"(Belohnung: {min:F2} bis {max:F2})");
                }
            }

            // 3. Schwache Kausalketten  
            var schwacheKetten = kausalGraph.GetNiedrigeKonfidenz(0.3f);
            foreach (var kante in schwacheKetten.Take(3))
            {
                anomalien.Add($"Unsichere Kausalitaet: '{kante.ursache}' → '{kante.wirkung}' " +
                    $"(Konfidenz: {kante.konfidenz:F2})");
            }

            return anomalien;
        }

        // ===========================================================
        //  5. STATUS + API
        // ===========================================================

        public string GetStatusText()
        {
            int offen = hypothesen.Count(h => h.status == HypothesenStatus.Offen);
            int pruefung = hypothesen.Count(h => h.status == HypothesenStatus.InPruefung);
            int bestaetigt = hypothesen.Count(h => h.status == HypothesenStatus.Bestaetigt);
            int widerlegt = hypothesen.Count(h => h.status == HypothesenStatus.Widerlegt);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Hypothesen: {hypothesen.Count} gesamt " +
                $"(Offen: {offen}, In Pruefung: {pruefung}, Bestaetigt: {bestaetigt}, Widerlegt: {widerlegt})");
            sb.AppendLine($"Naechste Hypothesenbildung in: {BILDUNGS_INTERVALL - zyklusSeitLetzterBildung} Zyklen");

            var top = hypothesen
                .Where(h => h.status == HypothesenStatus.Offen || h.status == HypothesenStatus.InPruefung)
                .OrderByDescending(h => h.prioritaet)
                .Take(3);

            foreach (var h in top)
                sb.AppendLine($"  [{h.status}] {h.beschreibung} (Konfidenz: {h.konfidenz:F2})");

            return sb.ToString();
        }

        public List<AktiveHypothese> GetOffene() =>
            hypothesen.Where(h => h.status == HypothesenStatus.Offen ||
                                   h.status == HypothesenStatus.InPruefung).ToList();

        public List<AktiveHypothese> GetBestaetigte() =>
            hypothesen.Where(h => h.status == HypothesenStatus.Bestaetigt).ToList();

        public List<AktiveHypothese> GetAlle() => new(hypothesen);

        /// <summary>
        /// Erzwingt sofortige Hypothesenbildung.
        /// </summary>
        public async Task<HypothesenErgebnis> ErzwingeHypothesenbildung()
        {
            zyklusSeitLetzterBildung = BILDUNGS_INTERVALL;
            return await BildeNeueHypothese();
        }

        // ===========================================================
        //  PRIVATE: Relevanz + Vorhersage-Matching
        // ===========================================================

        private float BerechneRelevanz(AktiveHypothese hyp, Erfahrung e)
        {
            // Einfaches Keyword-Matching zwischen Hypothese und Erfahrung
            var hypWords = (hyp.beschreibung + " " + hyp.vorhersage)
                .ToLowerInvariant()
                .Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet();

            var expWords = (e.aktion + " " + e.ergebnis + " " + e.kontext)
                .ToLowerInvariant()
                .Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet();

            int overlap = hypWords.Intersect(expWords).Count();
            if (hypWords.Count == 0) return 0f;
            return (float)overlap / hypWords.Count;
        }

        private bool PasstZurVorhersage(AktiveHypothese hyp, Erfahrung e)
        {
            // Vereinfacht: Wenn die Vorhersage positive Belohnung impliziert
            // und die Erfahrung positiv ist → stuetzt
            bool vorhersagePositiv = hyp.vorhersage.ToLowerInvariant()
                .Contains("positiv") || hyp.vorhersage.Contains("besser") ||
                hyp.vorhersage.Contains("erfolg") || hyp.vorhersage.Contains("mehr");

            bool erfahrungPositiv = e.belohnung > 0.2f;

            // Beide positiv oder beide negativ → stuetzt
            return vorhersagePositiv == erfahrungPositiv;
        }

        private float BerechneHypothesenPrioritaet(string hypothesenText)
        {
            float prio = 0.5f;

            // Unbekannte Domaenen → hoehere Prioritaet
            string lower = hypothesenText.ToLowerInvariant();
            var domaenen = new[] { "physik", "sozial", "navigation", "planung" };
            foreach (var d in domaenen)
            {
                if (lower.Contains(d))
                {
                    float kompetenz = selbstModell?.GetKompetenz(d) ?? 0.1f;
                    prio = Math.Max(prio, 1f - kompetenz);
                }
            }

            // Widerspruchs-Keywords → hoehere Prioritaet
            if (lower.Contains("widerspruch") || lower.Contains("seltsam") ||
                lower.Contains("unerwartet") || lower.Contains("warum"))
                prio += 0.2f;

            return Mathf.Clamp01(prio);
        }

        private string BaueHypothesenPrompt(
            List<string> anomalien,
            List<Hypothese> neugierHyp,
            List<KausalGraph.KausalKante> schwacheKanten)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Der Agent hat folgende Beobachtungen gemacht:\n");

            if (anomalien.Count > 0)
            {
                sb.AppendLine("ANOMALIEN (unerwartete Ergebnisse):");
                foreach (var a in anomalien.Take(5))
                    sb.AppendLine($"  - {a}");
                sb.AppendLine();
            }

            if (neugierHyp.Count > 0)
            {
                sb.AppendLine("OFFENE FRAGEN (Neugier-System):");
                foreach (var h in neugierHyp.Take(5))
                    sb.AppendLine($"  - {h.beschreibung} (Prioritaet: {h.prioritaet:F2})");
                sb.AppendLine();
            }

            if (schwacheKanten.Count > 0)
            {
                sb.AppendLine("UNSICHERE KAUSALITAETEN:");
                foreach (var k in schwacheKanten.Take(5))
                    sb.AppendLine($"  - {k.ursache} → {k.wirkung} (Konfidenz: {k.konfidenz:F2})");
                sb.AppendLine();
            }

            sb.AppendLine("Bilde eine TESTBARE Hypothese und ein konkretes Experiment.");
            return sb.ToString();
        }

        // ===========================================================
        //  PERSISTENZ
        // ===========================================================

        private void SpeichereAufDisk()
        {
            try
            {
                string json = JsonUtility.ToJson(new HypothesenListe { hypothesen = hypothesen }, true);
                string pfad = System.IO.Path.Combine(Application.persistentDataPath, SPEICHER_PFAD);
                System.IO.File.WriteAllText(pfad, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HypothesenEngine] Speichern fehlgeschlagen: {ex.Message}");
            }
        }

        private void LadeVonDisk()
        {
            try
            {
                string pfad = System.IO.Path.Combine(Application.persistentDataPath, SPEICHER_PFAD);
                if (!System.IO.File.Exists(pfad)) return;
                string json = System.IO.File.ReadAllText(pfad);
                var liste = JsonUtility.FromJson<HypothesenListe>(json);
                if (liste?.hypothesen != null)
                {
                    hypothesen = liste.hypothesen;
                    Debug.Log($"[HypothesenEngine] {hypothesen.Count} Hypothesen geladen.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HypothesenEngine] Laden fehlgeschlagen: {ex.Message}");
            }
        }

        [Serializable]
        private class HypothesenListe
        {
            public List<AktiveHypothese> hypothesen = new();
        }

        // --- JSON-Helfer ---

        private static string ExtractJsonString(string json, string key)
        {
            string pattern = $"\"{key}\"";
            int idx = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            int colonIdx = json.IndexOf(':', idx + pattern.Length);
            if (colonIdx < 0) return null;
            int quoteStart = json.IndexOf('"', colonIdx + 1);
            if (quoteStart < 0) return null;
            int quoteEnd = json.IndexOf('"', quoteStart + 1);
            while (quoteEnd > 0 && json[quoteEnd - 1] == '\\')
                quoteEnd = json.IndexOf('"', quoteEnd + 1);
            if (quoteEnd < 0) return null;
            return json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
        }

        private static float ExtractJsonFloat(string json, string key, float fallback)
        {
            string pattern = $"\"{key}\"";
            int idx = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return fallback;
            int colonIdx = json.IndexOf(':', idx + pattern.Length);
            if (colonIdx < 0) return fallback;
            int start = colonIdx + 1;
            while (start < json.Length && (json[start] == ' ' || json[start] == '\t')) start++;
            int end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '.' || json[end] == '-')) end++;
            if (end <= start) return fallback;
            if (float.TryParse(json.Substring(start, end - start),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float val))
                return val;
            return fallback;
        }
    }
}
