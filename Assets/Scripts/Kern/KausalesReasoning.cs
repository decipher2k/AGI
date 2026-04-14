using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using BilligAGI.Modelle;
using BilligAGI.Gedaechtnis;

namespace BilligAGI.Kern
{
    // ============================================================
    //  KausalesReasoning — Echtes "Warum?" statt nur Korrelation
    //
    //  Drei Ebenen kausalen Denkens (Pearl's Ladder):
    //  1. Assoziation:  "Wenn X, dann oft Y" (= KausalGraph bisher)
    //  2. Intervention: "Was passiert, wenn ich X TUE?"
    //  3. Kontrafaktisch: "Was WAERE passiert, wenn ich X NICHT getan haette?"
    //
    //  Nutzt: KausalGraph (Daten), PrediktivesWeltModell (Simulation),
    //         LLM (tiefes Reasoning), ErfahrungsSpeicher (Evidenz)
    // ============================================================

    public enum KausaleEbene
    {
        Assoziation,     // P(Y|X) — Beobachtung
        Intervention,    // P(Y|do(X)) — Handlung
        Kontrafaktisch   // P(Y_x'|X,Y) — Was waere wenn
    }

    [Serializable]
    public class KausaleAnalyse
    {
        public string frage;                            // "Warum ist X passiert?"
        public KausaleEbene ebene;
        public List<KausaleHypothese> hypothesen = new();
        public string besteErklaerung;
        public string erklaerung { get => besteErklaerung; set => besteErklaerung = value; }
        public float konfidenz;
        public string zeitstempel;
    }

    [Serializable]
    public class KausaleHypothese
    {
        public string ursache;
        public string wirkung;
        public float wahrscheinlichkeit;
        public float konfidenz { get => wahrscheinlichkeit; set => wahrscheinlichkeit = value; }
        public string evidenz;                          // Stuetzende Erfahrungen
        public string mechanismus;                      // WIE verursacht X → Y?
        public bool interventionell;                    // Durch do() bestaetigt?
        public bool kontrafaktischGeprueft;             // Durch Simulation bestaetigt?
    }

    [Serializable]
    public class InterventionsErgebnis
    {
        public string aktion;
        public float vorhergesagterEffekt;              // Prediktives Weltmodell
        public float erwarteteBelohnung { get => vorhergesagterEffekt; set => vorhergesagterEffekt = value; }
        public float tatsaechlicherEffekt;              // Falls schon beobachtet
        public float kausalStaerke;                     // Differenz zu Baseline
        public float deltaBelohnung { get => kausalStaerke; set => kausalStaerke = value; }
        public string beschreibung;
    }

    public class KausalesReasoning
    {
        private readonly LLMAdapter llm;
        private readonly KausalGraph kausalGraph;
        private readonly PrediktivesWeltModell weltModell;
        private readonly ErfahrungsSpeicher erfahrungen;
        private readonly AGIConfig config;

        private const string WARUM_SYSTEM = @"Du bist ein kausaler Reasoning-Agent. Analysiere WARUM etwas passiert ist.
Unterscheide klar zwischen:
- KORRELATION: X und Y treten zusammen auf
- KAUSALITAET: X VERURSACHT Y (es gibt einen Mechanismus)

Gegeben: Eine Frage und Evidenz aus dem Kausalgraph + Erfahrungen.

Antworte als JSON:
{
  ""hypothesen"": [
    {
      ""ursache"": ""vermutete Ursache"",
      ""wirkung"": ""beobachtete Wirkung"",
      ""wahrscheinlichkeit"": 0.0-1.0,
      ""mechanismus"": ""WIE verursacht die Ursache die Wirkung?"",
      ""evidenz"": ""welche Beobachtungen stuetzen das?"",
      ""alternative"": ""welche andere Erklaerung gaebe es?""
    }
  ],
  ""besteErklaerung"": ""praegnante Zusammenfassung der wahrscheinlichsten Ursache"",
  ""konfidenz"": 0.0-1.0,
  ""offeneFragen"": [""was muesste man pruefen um sicherer zu sein?""]
}

Regeln:
- Nenne IMMER mindestens eine Alternative (Confound)
- Bewerte ob die Evidenz kausal oder nur korrelativ ist
- Wenn unsicher: sage es ehrlich (niedrige Konfidenz)";

        private const string KONTRAFAKTISCH_SYSTEM = @"Kontrafaktisches Reasoning: Was WAERE passiert, wenn eine andere Aktion gewaehlt worden waere?

Gegeben: Tatsaechliche Situation + Aktion + Ergebnis + Alternative Aktion.

Antworte als JSON:
{
  ""kontrafaktischesErgebnis"": ""was waere wahrscheinlich passiert"",
  ""differenz"": ""worin unterscheidet sich das vom tatsaechlichen Ergebnis"",
  ""kausalSchluss"": ""was lernen wir daraus ueber die Ursache"",
  ""konfidenz"": 0.0-1.0
}";

        public KausalesReasoning(
            LLMAdapter llm,
            KausalGraph kausalGraph,
            PrediktivesWeltModell weltModell,
            ErfahrungsSpeicher erfahrungen,
            AGIConfig config)
        {
            this.llm = llm;
            this.kausalGraph = kausalGraph;
            this.weltModell = weltModell;
            this.erfahrungen = erfahrungen;
            this.config = config;
        }

        // ===========================================================
        //  1. WARUM-ANALYSE: "Warum ist X passiert?"
        // ===========================================================

        /// <summary>
        /// Vollstaendige kausale Analyse einer Beobachtung.
        /// Kombiniert KausalGraph-Daten + Erfahrungen + LLM-Reasoning.
        /// </summary>
        public async Task<KausaleAnalyse> WarumAnalyse(string wirkung)
        {
            var analyse = new KausaleAnalyse
            {
                frage = $"Warum ist '{wirkung}' passiert?",
                ebene = KausaleEbene.Assoziation,
                zeitstempel = DateTime.UtcNow.ToString("o")
            };

            // 1. Kausalgraph befragen: Welche bekannten Ursachen gibt es?
            var kanten = kausalGraph.GetKantenFuer(wirkung);
            var ursachenKanten = kanten.Where(k => k.wirkung == wirkung).ToList();

            // 2. Erfahrungen befragen: Welche Erfahrungen enthalten diese Wirkung?
            var relevanteErfahrungen = await erfahrungen.FindeAehnliche(wirkung, 5);

            // 3. LLM-basiertes tiefes Reasoning
            string prompt = BaueWarumPrompt(wirkung, ursachenKanten, relevanteErfahrungen);
            var antwort = await llm.FreieAnfrage(prompt, WARUM_SYSTEM);

            if (antwort != null && !string.IsNullOrWhiteSpace(antwort.inhalt))
            {
                ParseWarumAntwort(antwort.inhalt, analyse);
            }

            // 4. Graph-basierte Hypothesen ergaenzen (ohne LLM)
            foreach (var kante in ursachenKanten.OrderByDescending(k => k.konfidenz))
            {
                bool schonDrin = analyse.hypothesen.Any(h =>
                    h.ursache.Equals(kante.ursache, StringComparison.OrdinalIgnoreCase));
                if (!schonDrin)
                {
                    analyse.hypothesen.Add(new KausaleHypothese
                    {
                        ursache = kante.ursache,
                        wirkung = kante.wirkung,
                        wahrscheinlichkeit = kante.konfidenz,
                        evidenz = $"{kante.bestaetigungen}x beobachtet",
                        mechanismus = kante.ebene == "mechanismus" ? "bekannter Mechanismus" : "nur Korrelation"
                    });
                }
            }

            if (analyse.hypothesen.Count > 0)
                analyse.konfidenz = analyse.hypothesen.Max(h => h.wahrscheinlichkeit);

            return analyse;
        }

        // ===========================================================
        //  2. INTERVENTION: "Was passiert, wenn ich X TUE?"
        // ===========================================================

        /// <summary>
        /// Simuliert eine Intervention via Prediktives Weltmodell.
        /// Vergleicht vorhergesagten Effekt mit Baseline (nichts tun).
        /// </summary>
        public InterventionsErgebnis SimuliereIntervention(
            float[] aktuellerZustand, AktionsTyp aktion)
        {
            if (weltModell == null || !weltModell.Aktiv)
                return new InterventionsErgebnis
                {
                    aktion = aktion.ToString(),
                    beschreibung = "Prediktives Weltmodell nicht aktiv."
                };

            // Vorhersage MIT Intervention
            var mitAktion = weltModell.Vorhersage(aktuellerZustand, aktion);

            // Vorhersage OHNE Intervention (Baseline = Warten)
            var ohneAktion = weltModell.Vorhersage(aktuellerZustand, AktionsTyp.Warten);

            // Kausale Staerke = Differenz zur Baseline
            float kausalStaerke = mitAktion.vorhergesagteBelohnung - ohneAktion.vorhergesagteBelohnung;

            return new InterventionsErgebnis
            {
                aktion = aktion.ToString(),
                vorhergesagterEffekt = mitAktion.vorhergesagteBelohnung,
                kausalStaerke = kausalStaerke,
                beschreibung = kausalStaerke > 0.1f
                    ? $"{aktion} hat wahrscheinlich einen POSITIVEN kausalen Effekt (+{kausalStaerke:F2})"
                    : kausalStaerke < -0.1f
                        ? $"{aktion} hat wahrscheinlich einen NEGATIVEN kausalen Effekt ({kausalStaerke:F2})"
                        : $"{aktion} hat vermutlich keinen kausalen Effekt (Δ={kausalStaerke:F2})"
            };
        }

        /// <summary>
        /// Simuliert alle Aktionstypen und rankt nach kausaler Staerke.
        /// </summary>
        public List<InterventionsErgebnis> RankeInterventionen(float[] zustand)
        {
            var ergebnisse = new List<InterventionsErgebnis>();
            foreach (AktionsTyp aktion in Enum.GetValues(typeof(AktionsTyp)))
            {
                ergebnisse.Add(SimuliereIntervention(zustand, aktion));
            }
            return ergebnisse.OrderByDescending(e => e.kausalStaerke).ToList();
        }

        // ===========================================================
        //  3. KONTRAFAKTISCH: "Was waere passiert, wenn...?"
        // ===========================================================

        /// <summary>
        /// Kontrafaktische Analyse: Was waere passiert mit alternativer Aktion?
        /// Kombiniert Weltmodell-Simulation + LLM-Reasoning.
        /// </summary>
        public async Task<string> KontrafaktischeAnalyse(
            Erfahrung erfahrung, AktionsTyp alternativeAktion)
        {
            string kontrafaktischBeschreibung = "";

            // 1. Simulation via PrediktivesWeltModell (wenn Zustand verfuegbar)
            if (weltModell != null && weltModell.Aktiv && erfahrung.vakog != null)
            {
                // Vereinfachter 20D-Zustand aus Erfahrung rekonstruieren
                float[] zustand = RekonstruiereZustand(erfahrung);
                if (zustand != null)
                {
                    var tatsaechlich = weltModell.Vorhersage(zustand,
                        erfahrung.aktionenListe?.FirstOrDefault()?.typ ?? AktionsTyp.Beobachten);
                    var alternativ = weltModell.Vorhersage(zustand, alternativeAktion);

                    kontrafaktischBeschreibung =
                        $"Simulation: Tatsaechlich → Reward {tatsaechlich.vorhergesagteBelohnung:F2}, " +
                        $"Alternativ ({alternativeAktion}) → Reward {alternativ.vorhergesagteBelohnung:F2}. " +
                        $"Differenz: {alternativ.vorhergesagteBelohnung - tatsaechlich.vorhergesagteBelohnung:F2}";
                }
            }

            // 2. LLM-basierte tiefe Analyse
            string prompt =
                $"Tatsaechliche Situation:\n" +
                $"  Aktion: {erfahrung.aktion}\n" +
                $"  Kontext: {erfahrung.kontext}\n" +
                $"  Ergebnis: {erfahrung.ergebnis}\n" +
                $"  Belohnung: {erfahrung.belohnung:F2}\n\n" +
                $"Alternative Aktion: {alternativeAktion}\n\n" +
                (string.IsNullOrEmpty(kontrafaktischBeschreibung) ? "" :
                    $"Weltmodell-Simulation: {kontrafaktischBeschreibung}\n\n") +
                "Was waere wahrscheinlich passiert?";

            var antwort = await llm.FreieAnfrage(prompt, KONTRAFAKTISCH_SYSTEM);
            if (antwort != null && !string.IsNullOrWhiteSpace(antwort.inhalt))
            {
                string schluss = ExtractJsonString(antwort.inhalt, "kausalSchluss");
                if (!string.IsNullOrEmpty(schluss))
                    return schluss;
                return antwort.inhalt;
            }

            return kontrafaktischBeschreibung.Length > 0
                ? kontrafaktischBeschreibung
                : "Kontrafaktische Analyse nicht moeglich (zu wenig Daten).";
        }

        // ===========================================================
        //  4. ZYKLUS-HOOK: Kausale Beobachtungen registrieren
        // ===========================================================

        /// <summary>
        /// Registriert eine neue Beobachtung als kausale Kante.
        /// Unterscheidet drei Evidenz-Ebenen je nach Kontext.
        /// </summary>
        public void RegistriereBeobachtung(
            string aktion, string ergebnis, float belohnung, bool warGeplant)
        {
            if (string.IsNullOrEmpty(aktion) || string.IsNullOrEmpty(ergebnis))
                return;

            // Ebene bestimmen: geplante Aktion = hoehere kausale Evidenz
            string ebene = warGeplant ? "mechanismus" : "beobachtung";
            float konfidenz = warGeplant ? 0.6f : 0.3f;

            // Positive/negative Ergebnisse separat tracken
            if (belohnung > 0.3f)
                kausalGraph.FuegeKausalitaetHinzu(aktion, ergebnis + "_positiv", konfidenz, ebene);
            else if (belohnung < -0.3f)
                kausalGraph.FuegeKausalitaetHinzu(aktion, ergebnis + "_negativ", konfidenz, ebene);

            // Allgemeine Kante immer
            kausalGraph.FuegeKausalitaetHinzu(aktion, ergebnis, konfidenz * 0.5f, ebene);
        }

        // ===========================================================
        //  5. STATUS
        // ===========================================================

        public string GetStatusText()
        {
            var kanten = kausalGraph.GetNiedrigeKonfidenz(1.1f); // Alle Kanten
            int gesamt = kanten.Count;
            int mechanismus = kanten.Count(k => k.ebene == "mechanismus");
            int prinzip = kanten.Count(k => k.ebene == "prinzip");
            int schwach = kausalGraph.GetNiedrigeKonfidenz(0.3f).Count;

            return $"Kausale Kanten: {gesamt} (davon {mechanismus} Mechanismen, {prinzip} Prinzipien)\n" +
                   $"Schwache Kanten (<0.3): {schwach}\n" +
                   $"Weltmodell aktiv: {weltModell?.Aktiv ?? false}";
        }

        // ===========================================================
        //  PRIVATE: Helfer
        // ===========================================================

        private string BaueWarumPrompt(
            string wirkung,
            List<KausalGraph.KausalKante> bekannteUrsachen,
            List<Erfahrung> relevanteErfahrungen)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"WARUM ist '{wirkung}' passiert?\n");

            if (bekannteUrsachen.Count > 0)
            {
                sb.AppendLine("Bekannte kausale Verbindungen im Graph:");
                foreach (var k in bekannteUrsachen.Take(10))
                    sb.AppendLine($"  {k.ursache} → {k.wirkung} " +
                        $"(Konfidenz: {k.konfidenz:F2}, {k.bestaetigungen}x beobachtet, Ebene: {k.ebene})");
                sb.AppendLine();
            }

            if (relevanteErfahrungen.Count > 0)
            {
                sb.AppendLine("Relevante Erfahrungen:");
                foreach (var e in relevanteErfahrungen.Take(8))
                    sb.AppendLine($"  - {e.aktion} → {e.ergebnis} (Belohnung: {e.belohnung:F2})");
                sb.AppendLine();
            }

            sb.AppendLine("Analysiere: Was ist die URSACHE (nicht nur Korrelation)?");
            return sb.ToString();
        }

        private void ParseWarumAntwort(string json, KausaleAnalyse analyse)
        {
            try
            {
                analyse.besteErklaerung = ExtractJsonString(json, "besteErklaerung") ?? "";
                float konfidenz = ExtractJsonFloat(json, "konfidenz", 0.5f);
                analyse.konfidenz = konfidenz;

                // Hypothesen parsen
                int arrStart = json.IndexOf("\"hypothesen\"");
                if (arrStart >= 0)
                {
                    int bracketStart = json.IndexOf('[', arrStart);
                    int bracketEnd = FindeMatchendeBracket(json, bracketStart);
                    if (bracketStart >= 0 && bracketEnd > bracketStart)
                    {
                        string arr = json.Substring(bracketStart, bracketEnd - bracketStart + 1);
                        var items = JsonArraySplit(arr);
                        foreach (var item in items)
                        {
                            analyse.hypothesen.Add(new KausaleHypothese
                            {
                                ursache = ExtractJsonString(item, "ursache") ?? "",
                                wirkung = ExtractJsonString(item, "wirkung") ?? "",
                                wahrscheinlichkeit = ExtractJsonFloat(item, "wahrscheinlichkeit", 0.5f),
                                mechanismus = ExtractJsonString(item, "mechanismus") ?? "",
                                evidenz = ExtractJsonString(item, "evidenz") ?? ""
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KausalesReasoning] Parse-Fehler: {ex.Message}");
            }
        }

        private float[] RekonstruiereZustand(Erfahrung e)
        {
            // Vereinfachte Zustandsrekonstruktion aus Erfahrungsdaten
            if (e.vakog == null) return null;
            var z = new float[20];
            z[0] = e.vakog.visuell;
            z[1] = e.vakog.auditiv;
            z[2] = e.vakog.kinaesthetisch;
            z[3] = e.vakog.olfaktorisch;
            z[4] = e.vakog.gustatorisch;
            if (e.emotionalerZustand != null)
            {
                z[5] = e.emotionalerZustand.angst;
                z[6] = e.emotionalerZustand.neugier;
                z[7] = e.emotionalerZustand.frustration;
                z[8] = e.emotionalerZustand.zufriedenheit;
                z[9] = e.emotionalerZustand.ueberraschung;
            }
            z[12] = Math.Abs(e.belohnung);
            return z;
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

        private static int FindeMatchendeBracket(string json, int start)
        {
            if (start < 0 || start >= json.Length) return -1;
            int depth = 0;
            for (int i = start; i < json.Length; i++)
            {
                if (json[i] == '[') depth++;
                else if (json[i] == ']') { depth--; if (depth == 0) return i; }
            }
            return -1;
        }

        private static List<string> JsonArraySplit(string arrayJson)
        {
            var items = new List<string>();
            int depth = 0;
            int itemStart = -1;
            for (int i = 0; i < arrayJson.Length; i++)
            {
                if (arrayJson[i] == '{') { if (depth == 0) itemStart = i; depth++; }
                else if (arrayJson[i] == '}')
                {
                    depth--;
                    if (depth == 0 && itemStart >= 0)
                    { items.Add(arrayJson.Substring(itemStart, i - itemStart + 1)); itemStart = -1; }
                }
            }
            return items;
        }
    }
}
