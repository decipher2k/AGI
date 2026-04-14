using System;
using System.Collections.Generic;

namespace BilligAGI.Modelle
{
    public enum IntentTyp
    {
        Frage, Befehl, Zielanfrage, Statusanfrage,
        Revision, Kreativauftrag, Chat, Unbekannt
    }

    [Serializable]
    public class SemantikFrame
    {
        public IntentTyp intentTyp;
        public Dictionary<string, string> slots = new Dictionary<string, string>(); // Objekt, Ort, Zeit, Entität
        public List<string> kontextBezuege = new List<string>();
        public float konfidenz;
        public bool kannOhneLLM;
    }
}
