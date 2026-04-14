using System;

namespace BilligAGI.Modelle
{
    [Serializable]
    public class BenchmarkErgebnis
    {
        public string szenarioId;
        public float erfolgsquote;
        public float zeitBisZiel;
        public int llmCalls;
        public float lokalQuote;
        public float kreativScore;
        public float stabilitaetScore;
        public string zeitstempel;

        public BenchmarkErgebnis()
        {
            zeitstempel = DateTime.UtcNow.ToString("o");
        }
    }

    [Serializable]
    public class BenchmarkSzenario
    {
        public string id;
        public string name;
        public string beschreibung;
        public string zielBeschreibung;
        public float zielErfolgsquote;
        public float zielZeitLimit;
        public int zielMaxLLMCalls;
    }
}
