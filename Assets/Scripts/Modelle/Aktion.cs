using System;
using System.Collections.Generic;

namespace BilligAGI.Modelle
{
    public enum AktionsTyp
    {
        Bewegen, Greifen, Ablegen, Werfen, Schieben, Ziehen,
        Beobachten, Hoeren, Warten,
        Interagieren, Oeffnen, Schliessen, Aktivieren,
        Sprechen, ZeigenAuf, Drehen, Springen
    }

    [Serializable]
    public class Aktion
    {
        public string name;
        public AktionsTyp typ;
        public Dictionary<string, string> parameter = new Dictionary<string, string>();
        public string erwartetesErgebnis;
        public string tatsaechlichesErgebnis;
        public float geschaetzteDauer;
    }

    [Serializable]
    public class AktionsErgebnis
    {
        public bool erfolg;
        public string beschreibung;
        public SensorDaten sensorDatenNachher;
        public Dictionary<string, string> zustandsAenderungen = new Dictionary<string, string>();
    }

    [Serializable]
    public class AktionsDefinition
    {
        public string name;
        public AktionsTyp typ;
        public List<string> vorbedingungen = new List<string>();
        public List<string> effekte = new List<string>();
        public string beschreibung;
    }
}
