using System;
using System.Collections.Generic;

namespace BilligAGI.Modelle
{
    [Serializable]
    public class Erfahrung
    {
        public string id;
        public string aktion;
        public string kontext;
        public string ergebnis;
        public VAKOGProfil vakog;
        public WeltZustand weltKontext;
        public SensorDaten sensorSnapshot;
        public List<Aktion> aktionenListe = new List<Aktion>();
        public string zielId;
        public float belohnung;
        public string zeitstempel;
        public ZeitlicherKontext zeitlicherKontext;
        public EmotionalerZustand emotionalerZustand;
        public List<string> konzepte = new List<string>();
        public bool oneShotGelernt;
        public float relevanz = 1f; // Fuer Langzeitlernen

        public Erfahrung()
        {
            id = Guid.NewGuid().ToString("N").Substring(0, 8);
            zeitstempel = DateTime.UtcNow.ToString("o");
        }
    }
}
