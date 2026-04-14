using System;
using System.Collections.Generic;

namespace BilligAGI.Modelle
{
    [Serializable]
    public class Plan
    {
        public string zielId;
        public List<Aktion> aktionen = new List<Aktion>();
        public int aktuellerSchritt;
        public int umplanungen;
        public List<string> anpassungen = new List<string>();
        public float geschaetzteDauer; // Sekunden
        public string erstelltAm;

        public Plan()
        {
            erstelltAm = DateTime.UtcNow.ToString("o");
        }

        public Aktion AktuelleAktion()
        {
            if (aktuellerSchritt >= 0 && aktuellerSchritt < aktionen.Count)
                return aktionen[aktuellerSchritt];
            return null;
        }

        public bool IstAbgeschlossen() => aktuellerSchritt >= aktionen.Count;
    }
}
