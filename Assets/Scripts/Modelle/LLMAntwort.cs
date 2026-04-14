using System;

namespace BilligAGI.Modelle
{
    [Serializable]
    public class LLMAntwort
    {
        public string text;
        public string inhalt { get => text; set => text = value; }
        public int tokensUsed;
        public float kosten;
        public float dauerMs;
        public bool ausFallback; // true = kam nicht vom LLM sondern lokal
    }
}
