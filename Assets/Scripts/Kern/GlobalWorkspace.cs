using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BilligAGI.Modelle;
using BilligAGI.Welt;

namespace BilligAGI.Kern
{
    /// <summary>
    /// Global-Workspace-Schicht: verdichtet konkurrierende Wahrnehmungen, Ziele,
    /// Emotionen, Erinnerungen und Modellvorhersagen zu einem expliziten Fokus.
    /// Das ist keine echte Bewusstseins-Implementierung, aber es macht die
    /// Architektur AGI-naeher, weil alle Module denselben priorisierten Kontext
    /// sehen statt isolierte Einzelheuristiken zu benutzen.
    /// </summary>
    public class GlobalWorkspace
    {
        private readonly int maxEintraege;
        private readonly List<WorkspaceEintrag> aktuelleEintraege = new List<WorkspaceEintrag>();
        private WorkspaceEintrag aktuellerFokus;

        public GlobalWorkspace(int maxEintraege = 12)
        {
            this.maxEintraege = Math.Max(3, maxEintraege);
        }

        public WorkspaceEintrag AktuellerFokus => aktuellerFokus;
        public IReadOnlyList<WorkspaceEintrag> AktuelleEintraege => aktuelleEintraege;

        public void Aktualisiere(
            string input,
            WeltZustand welt,
            AgentZustand agent,
            EmotionalerZustand emotionen,
            Ziel ziel,
            IReadOnlyList<Erfahrung> erinnerungen,
            SozialeAnalyse sozialAnalyse,
            PlausibilitaetsErgebnis physikCheck,
            float rlKonfidenz,
            AktionsTyp? rlAktion,
            AktionsTyp? weltmodellAktion,
            float weltmodellReward)
        {
            aktuelleEintraege.Clear();

            FuegeEintragHinzu("Nutzerintent", input, 0.85f, "sprachlich", !string.IsNullOrWhiteSpace(input));
            FuegeEintragHinzu("Aktives Ziel", ziel?.beschreibung ?? ziel?.name, 0.8f, "intentional", ziel != null);
            FuegeEintragHinzu("Emotionale Valenz", BeschreibeEmotionen(emotionen), BerechneEmotionsSalienz(emotionen), "affektiv", emotionen != null);
            FuegeEintragHinzu("Weltzustand", BeschreibeWelt(welt, agent), 0.45f, "situativ", welt != null);

            if (erinnerungen != null)
            {
                foreach (var erinnerung in erinnerungen.Take(3))
                {
                    float salienz = Math.Max(0.25f, Math.Min(0.75f, erinnerung.relevanz + Math.Abs(erinnerung.belohnung) * 0.2f));
                    FuegeEintragHinzu("Relevante Erinnerung", $"{erinnerung.aktion} -> {erinnerung.ergebnis}", salienz, "episodisch", true);
                }
            }

            if (physikCheck != null && !physikCheck.plausibel)
                FuegeEintragHinzu("Physik-Konflikt", physikCheck.begruendung, 0.9f, "kausal", true);

            if (sozialAnalyse?.erkannteMechanismen?.Count > 0)
                FuegeEintragHinzu("Sozialer Kontext", $"Archetyp {sozialAnalyse.archetyp}, Phase {sozialAnalyse.alchemischePhase}", 0.55f, "sozial", true);

            if (rlAktion.HasValue)
                FuegeEintragHinzu("RL-Handlungsimpuls", $"{rlAktion.Value} mit Konfidenz {rlKonfidenz:F2}", rlKonfidenz, "prozedural", rlKonfidenz > 0.1f);

            if (weltmodellAktion.HasValue)
                FuegeEintragHinzu("Imaginierte beste Aktion", $"{weltmodellAktion.Value} erwartet Reward {weltmodellReward:F2}", Math.Max(0.2f, Math.Min(0.8f, 0.4f + weltmodellReward * 0.2f)), "praediktiv", true);

            aktuelleEintraege.Sort((a, b) => b.salienz.CompareTo(a.salienz));
            if (aktuelleEintraege.Count > maxEintraege)
                aktuelleEintraege.RemoveRange(maxEintraege, aktuelleEintraege.Count - maxEintraege);
            aktuellerFokus = aktuelleEintraege.FirstOrDefault();
        }

        public string BauePromptKontext()
        {
            if (aktuelleEintraege.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("[Global Workspace: priorisierter gemeinsamer Aufmerksamkeitsfokus]");
            if (aktuellerFokus != null)
                sb.AppendLine($"Fokus: {aktuellerFokus.titel} - {aktuellerFokus.inhalt} (Salienz {aktuellerFokus.salienz:F2})");

            foreach (var eintrag in aktuelleEintraege)
                sb.AppendLine($"- [{eintrag.quelle}|{eintrag.salienz:F2}] {eintrag.titel}: {eintrag.inhalt}");

            sb.AppendLine("Nutze diesen Fokus, um Wahrnehmung, Handlung, Lernen und Sprache kohärent zu verbinden.");
            return sb.ToString();
        }

        private void FuegeEintragHinzu(string titel, string inhalt, float salienz, string quelle, bool bedingung)
        {
            if (!bedingung || string.IsNullOrWhiteSpace(inhalt)) return;
            aktuelleEintraege.Add(new WorkspaceEintrag
            {
                titel = titel,
                inhalt = inhalt.Trim(),
                salienz = Math.Max(0f, Math.Min(1f, salienz)),
                quelle = quelle,
                zeitstempelUtc = DateTime.UtcNow.ToString("o")
            });
        }

        private static string BeschreibeEmotionen(EmotionalerZustand emotionen)
        {
            if (emotionen == null) return null;
            return $"Valenz {emotionen.GesamtValenz():F2}, Neugier {emotionen.neugier:F2}, Frustration {emotionen.frustration:F2}, Zufriedenheit {emotionen.zufriedenheit:F2}";
        }

        private static float BerechneEmotionsSalienz(EmotionalerZustand emotionen)
        {
            if (emotionen == null) return 0f;
            return Math.Max(0.25f, Math.Min(0.85f, Math.Abs(emotionen.GesamtValenz()) + emotionen.neugier * 0.25f + emotionen.frustration * 0.25f));
        }

        private static string BeschreibeWelt(WeltZustand welt, AgentZustand agent)
        {
            if (welt == null) return null;
            string position = agent?.position != null && agent.position.Length >= 3
                ? $", AgentPos ({agent.position[0]:F1},{agent.position[1]:F1},{agent.position[2]:F1})"
                : string.Empty;
            return $"{welt.objekte?.Count ?? 0} Objekte, Wetter {welt.wetter}, Tageszeit {welt.tageszeit}{position}";
        }
    }

    [Serializable]
    public class WorkspaceEintrag
    {
        public string titel;
        public string inhalt;
        public float salienz;
        public string quelle;
        public string zeitstempelUtc;
    }
}
