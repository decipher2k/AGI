using System;

namespace BilligAGI.Modelle
{
    public enum KreativQuelle { Analogie, Mutation, Kombination, Perspektivwechsel }
    public enum KreativStatus { VORGESCHLAGEN, GETESTET, VERWORFEN, UEBERNOMMEN }

    [Serializable]
    public class KreativIdee
    {
        public string id;
        public string beschreibung;
        public KreativQuelle quelle;
        public float noveltyScore;
        public float utilityScore;
        public float plausibilitaetScore;
        public float risikoScore;
        public KreativStatus status = KreativStatus.VORGESCHLAGEN;

        public KreativIdee()
        {
            id = Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        public float GesamtScore() =>
            (noveltyScore + utilityScore + plausibilitaetScore) / 3f;
    }

    [Serializable]
    public class ABErgebnis
    {
        public string baselinePlanId;
        public string kreativPlanId;
        public float baselineErfolgsrate;
        public float kreativErfolgsrate;
        public float baselineZeit;
        public float kreativZeit;
        public int baselineLLMCalls;
        public int kreativLLMCalls;
        public bool kreativBesser;
    }
}
