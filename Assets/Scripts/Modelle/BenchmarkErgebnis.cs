using System;

namespace BilligAGI.Modelle
{
    [Serializable]
    public class BenchmarkErgebnis
    {
        public string szenarioId;
        public string szenarioName { get => szenarioId; set => szenarioId = value; }
        public string kategorie;
        public float erfolgsquote;
        public bool erfolgreich { get => erfolgsquote >= 1f; set => erfolgsquote = value ? 1f : 0f; }
        public float zeitBisZiel;
        public long zeitMs { get => (long)(zeitBisZiel * 1000f); set => zeitBisZiel = value / 1000f; }
        public int llmCalls;
        public float lokalQuote;
        public float kreativScore;
        public float stabilitaetScore;
        public string fehlerMeldung;
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
