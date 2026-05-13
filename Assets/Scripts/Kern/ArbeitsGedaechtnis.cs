using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BilligAGI.Modelle;

namespace BilligAGI.Kern
{
    /// <summary>
    /// Arbeitsgedaechtnis (Working Memory) — strukturierter Kontext-Buffer.
    /// Baut fuer jede LLM-Anfrage einen kohaerenten, gewichteten Kontext zusammen.
    ///
    /// Statt isolierte Prompts zu schicken, bekommt das LLM:
    /// - Letzte N Interaktionen (echte Konversation)
    /// - Top-K relevanteste Erinnerungen (gewichtet)
    /// - Aktives Ziel + Plan-Fortschritt
    /// - Emotionaler Zustand + Selbstmodell
    /// - Aktuelle Beliefs aus dem BDI-System
    /// - Welt-Zusammenfassung
    /// </summary>
    public class ArbeitsGedaechtnis
    {
        private readonly int maxInteraktionen;
        private readonly int maxErinnerungen;
        private readonly int maxTokenBudget;

        // Interaktions-Buffer (Konversationsverlauf)
        private readonly List<Interaktion> interaktionen;

        // Aktueller Kontext (wird pro Zyklus aktualisiert)
        private string aktivesZiel;
        private string planStatus;
        private string emotionalerZustand;
        private string selbstModellZusammenfassung;
        private string weltZusammenfassung;
        private string sozialerKontext;
        private List<string> aktuelleBeliefs;

        public ArbeitsGedaechtnis(int maxInteraktionen = 10, int maxErinnerungen = 5, int maxTokenBudget = 3000)
        {
            this.maxInteraktionen = maxInteraktionen;
            this.maxErinnerungen = maxErinnerungen;
            this.maxTokenBudget = maxTokenBudget;
            interaktionen = new List<Interaktion>();
            aktuelleBeliefs = new List<string>();
        }

        // ===== Kontext-Update (pro Zyklus) =====

        public void AktualisiereZiel(Ziel ziel, Plan plan, int planSchritt)
        {
            if (ziel != null)
            {
                aktivesZiel = $"{ziel.name}: {ziel.beschreibung}";
                if (plan != null && plan.aktionen.Count > 0)
                    planStatus = $"Schritt {planSchritt + 1}/{plan.aktionen.Count}: {plan.aktionen[Math.Min(planSchritt, plan.aktionen.Count - 1)].name}";
                else
                    planStatus = "Kein aktiver Plan";
            }
            else
            {
                aktivesZiel = null;
                planStatus = null;
            }
        }

        public void AktualisiereEmotionen(EmotionalerZustand emo)
        {
            if (emo == null) { emotionalerZustand = null; return; }

            var dominante = new List<string>();
            if (emo.angst > 0.4f) dominante.Add($"Angst({emo.angst:F1})");
            if (emo.neugier > 0.4f) dominante.Add($"Neugier({emo.neugier:F1})");
            if (emo.frustration > 0.4f) dominante.Add($"Frustration({emo.frustration:F1})");
            if (emo.zufriedenheit > 0.4f) dominante.Add($"Zufriedenheit({emo.zufriedenheit:F1})");
            if (emo.ueberraschung > 0.4f) dominante.Add($"Ueberraschung({emo.ueberraschung:F1})");

            emotionalerZustand = dominante.Count > 0
                ? string.Join(", ", dominante)
                : "Neutral";
        }

        public void AktualisiereSelbstModell(SelbstModell selbst)
        {
            if (selbst == null) { selbstModellZusammenfassung = null; return; }

            var kompetenzen = selbst.GetAlleKompetenzen();
            if (kompetenzen.Count == 0) { selbstModellZusammenfassung = null; return; }

            var top = kompetenzen.OrderByDescending(k => k.Value).Take(3);
            var bottom = kompetenzen.OrderBy(k => k.Value).Take(2);

            var sb = new StringBuilder();
            sb.Append("Staerken: ");
            sb.Append(string.Join(", ", top.Select(k => $"{k.Key}({k.Value:F1})")));
            sb.Append(". Schwaechen: ");
            sb.Append(string.Join(", ", bottom.Select(k => $"{k.Key}({k.Value:F1})")));
            selbstModellZusammenfassung = sb.ToString();
        }

        public void AktualisiereWelt(WeltZustand welt)
        {
            if (welt == null) { weltZusammenfassung = null; return; }
            weltZusammenfassung = $"Tageszeit: {welt.tageszeit:F1}h, Wetter: {welt.wetter}, " +
                $"Objekte: {welt.objekte?.Count ?? 0}, Intensitaet: {welt.wetterIntensitaet:F1}";
        }

        public void AktualisiereSozialesUmfeld(SozialeAnalyse analyse)
        {
            if (analyse == null) { sozialerKontext = null; return; }
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(analyse.archetyp))
                sb.Append($"Archetyp: {analyse.archetyp} ({analyse.archetypAspekt}). ");
            if (!string.IsNullOrEmpty(analyse.alchemischePhase))
                sb.Append($"Phase: {analyse.alchemischePhase}. ");
            if (analyse.tomVorhersagen?.Count > 0)
            {
                sb.Append("ToM: ");
                foreach (var kv in analyse.tomVorhersagen.Take(2))
                    sb.Append($"{kv.Key}→{kv.Value}; ");
            }
            sozialerKontext = sb.Length > 0 ? sb.ToString() : null;
        }

        public void SetzeBeliefs(List<string> beliefs)
        {
            aktuelleBeliefs = beliefs ?? new List<string>();
        }

        // ===== Interaktions-Verlauf =====

        public void RegistriereInteraktion(string input, string antwort)
        {
            if (string.IsNullOrEmpty(input)) return;

            interaktionen.Add(new Interaktion
            {
                input = input,
                antwort = antwort ?? "",
                zeitstempel = DateTime.UtcNow
            });

            while (interaktionen.Count > maxInteraktionen)
                interaktionen.RemoveAt(0);
        }

        // ===== System-Prompt Bauen =====

        /// <summary>
        /// Baut einen kohaerenten System-Prompt mit allem verfuegbaren Kontext.
        /// Budget-bewusst: Kuerzt wenn noetig.
        /// </summary>
        public string BaueSystemKontext(
            string basisSystem,
            List<Erfahrung> relevanteErinnerungen = null,
            List<Analogie> analogien = null,
            PlausibilitaetsErgebnis physikCheck = null,
            List<WissensDokument> externesWissen = null)
        {
            var sb = new StringBuilder();

            // 1. Basis-System-Prompt
            if (!string.IsNullOrEmpty(basisSystem))
                sb.AppendLine(basisSystem);

            // 2. Wer bin ich? (Selbstmodell)
            if (!string.IsNullOrEmpty(selbstModellZusammenfassung))
                sb.AppendLine($"\n[Selbstbild] {selbstModellZusammenfassung}");

            // 3. Wie fuehle ich mich? (Emotionen)
            if (!string.IsNullOrEmpty(emotionalerZustand))
                sb.AppendLine($"[Emotionaler Zustand] {emotionalerZustand}");

            // 4. Was will ich? (Ziel + Plan)
            if (!string.IsNullOrEmpty(aktivesZiel))
            {
                sb.AppendLine($"[Aktives Ziel] {aktivesZiel}");
                if (!string.IsNullOrEmpty(planStatus))
                    sb.AppendLine($"[Plan-Status] {planStatus}");
            }

            // 5. Wo bin ich? (Welt)
            if (!string.IsNullOrEmpty(weltZusammenfassung))
                sb.AppendLine($"[Umgebung] {weltZusammenfassung}");

            // 6. Soziales Umfeld
            if (!string.IsNullOrEmpty(sozialerKontext))
                sb.AppendLine($"[Soziales Umfeld] {sozialerKontext}");

            // 7. Was glaube ich? (Beliefs)
            if (aktuelleBeliefs.Count > 0)
            {
                sb.AppendLine("[Aktuelle Ueberzeugungen]");
                foreach (var b in aktuelleBeliefs.Take(5))
                    sb.AppendLine($"- {b}");
            }

            // 8. Externes Wissen (klar getrennt von eigener Erfahrung)
            if (externesWissen != null && externesWissen.Count > 0)
            {
                sb.AppendLine("\n[Externes Wissen / Wikipedia]");
                sb.AppendLine("Nutze diese Auszuege nur als externe Quelle; nicht als eigene Erfahrung ausgeben.");
                foreach (var w in externesWissen.Take(maxErinnerungen))
                    sb.AppendLine($"- {w.titel} ({w.url}): {Kuerze(w.text, 220)}");
            }

            // 9. Relevante Erinnerungen
            if (relevanteErinnerungen != null && relevanteErinnerungen.Count > 0)
            {
                sb.AppendLine("\n[Relevante Erinnerungen]");
                foreach (var e in relevanteErinnerungen.Take(maxErinnerungen))
                    sb.AppendLine($"- {e.aktion}: {Kuerze(e.ergebnis, 100)}");
            }

            // 10. Analogien
            if (analogien != null && analogien.Count > 0)
            {
                sb.AppendLine("\n[Analogien]");
                foreach (var a in analogien.Take(3))
                    sb.AppendLine($"- {a.quellDommaene} -> {a.zielDomaene}: {a.transferHypothese}");
            }

            // 11. Physik-Warnungen
            if (physikCheck != null && !physikCheck.plausibel)
                sb.AppendLine($"\n[Physik-Warnung] {physikCheck.begruendung}");

            // 12. Konversationsverlauf (letzte Interaktionen)
            if (interaktionen.Count > 0)
            {
                sb.AppendLine("\n[Bisheriger Gespraechsverlauf]");
                int start = Math.Max(0, interaktionen.Count - 5); // Letzte 5
                for (int i = start; i < interaktionen.Count; i++)
                {
                    var inter = interaktionen[i];
                    sb.AppendLine($"User: {Kuerze(inter.input, 80)}");
                    if (!string.IsNullOrEmpty(inter.antwort))
                        sb.AppendLine($"AGI: {Kuerze(inter.antwort, 120)}");
                }
            }

            // Budget-Check (grobe Schaetzung: 4 Zeichen ≈ 1 Token)
            string result = sb.ToString();
            int geschaetzteTokens = result.Length / 4;
            if (geschaetzteTokens > maxTokenBudget)
            {
                // Erinnerungen und Verlauf kuerzen
                return KuerzeAufBudget(result);
            }

            return result;
        }

        private string KuerzeAufBudget(string text)
        {
            // Einfache Strategie: Ab maxTokenBudget*4 Zeichen abschneiden
            int maxZeichen = maxTokenBudget * 4;
            if (text.Length <= maxZeichen) return text;
            return text.Substring(0, maxZeichen) + "\n[... Kontext gekuerzt ...]";
        }

        private static string Kuerze(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length <= maxLen ? text : text.Substring(0, maxLen) + "...";
        }

        public int GetInteraktionsAnzahl() => interaktionen.Count;
    }

    [Serializable]
    public class Interaktion
    {
        public string input;
        public string antwort;
        public DateTime zeitstempel;
    }
}
