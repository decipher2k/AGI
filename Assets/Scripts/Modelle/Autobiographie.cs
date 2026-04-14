using System;
using System.Collections.Generic;

namespace BilligAGI.Modelle
{
    [Serializable]
    public class Autobiographie
    {
        public List<AutobiographieKapitel> kapitel = new List<AutobiographieKapitel>();
        public string aktuellePhase = "Nigredo"; // Nigredo/Albedo/Citrinitas/Rubedo
        public List<string> identitaetsaussagen = new List<string>();
        public string entwicklungsVerlauf;
    }

    [Serializable]
    public class AutobiographieKapitel
    {
        public int nummer;
        public string titel;
        public string zusammenfassung;
        public List<string> schluesselErfahrungen = new List<string>();
        public List<string> gelernteKonzepte = new List<string>();
        public string emotionalerGrundton;
        public AlchemischePhase alchemischePhase;
        public int anzahlErfahrungen;
        public string zeitstempel;
        public string zeitraumVon;
        public string zeitraumBis;
    }
}
