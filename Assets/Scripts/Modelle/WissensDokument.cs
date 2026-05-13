using System;

namespace BilligAGI.Modelle
{
    [Serializable]
    public class WissensDokument
    {
        public string id;
        public string quelle;
        public string titel;
        public string abschnitt;
        public string text;
        public string url;
        public string revision;
        public string sprache;
        public string zeitstempel;
        public float score;
    }
}
