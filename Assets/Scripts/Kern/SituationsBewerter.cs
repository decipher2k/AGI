using BilligAGI.Modelle;

namespace BilligAGI.Kern
{
    public class SituationsBewerter
    {
        private readonly AGIConfig config;

        public SituationsBewerter(AGIConfig config)
        {
            this.config = config;
        }

        public SituationsBewertung Bewerte(
            VAKOGProfil vakog,
            WeltZustand welt,
            AgentZustand agent,
            EmotionalerZustand emotionen,
            Ziel aktivesZiel)
        {
            var bewertung = new SituationsBewertung();

            // VAKOG-Intensitaet
            if (vakog != null)
            {
                bewertung.vakogIntensitaet =
                    (vakog.visuell + vakog.auditiv + vakog.kinesthetisch +
                     vakog.olfaktorisch + vakog.gustatorisch) / 5f;
            }

            // Physik-Relevanz
            bewertung.physikRelevanz = 0.3f; // Baseline
            if (welt?.objekte != null && welt.objekte.Count > 5)
                bewertung.physikRelevanz = 0.6f;

            // Sozial-Relevanz
            bewertung.sozialRelevanz = 0.2f; // Baseline

            // Emotionale Ladung
            if (emotionen != null)
            {
                bewertung.emotionaleLadung =
                    (emotionen.angst + emotionen.frustration +
                     emotionen.ueberraschung + emotionen.neugier) / 4f;
            }

            // Ziel-Relevanz
            bewertung.zielRelevanz = aktivesZiel != null ? aktivesZiel.effektivePrioritaet : 0f;

            // Weltzustand-Relevanz
            if (welt?.historie != null && welt.historie.Count > 0)
                bewertung.weltRelevanz = UnityEngine.Mathf.Clamp01(welt.historie.Count / 10f);

            // Gesamt
            bewertung.gesamtRelevanz =
                bewertung.vakogIntensitaet * 0.15f +
                bewertung.physikRelevanz * 0.2f +
                bewertung.sozialRelevanz * 0.15f +
                bewertung.emotionaleLadung * 0.15f +
                bewertung.zielRelevanz * 0.2f +
                bewertung.weltRelevanz * 0.15f;

            return bewertung;
        }
    }

    [System.Serializable]
    public class SituationsBewertung
    {
        public float vakogIntensitaet;
        public float physikRelevanz;
        public float sozialRelevanz;
        public float emotionaleLadung;
        public float zielRelevanz;
        public float weltRelevanz;
        public float gesamtRelevanz;
    }
}
