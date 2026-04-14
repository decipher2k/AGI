using System;
using System.Collections.Generic;
using System.Linq;

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
        public string id;
        public string name;
        public AktionsTyp typ;
        public string parameter = "";
        public int reihenfolge;
        public string erwartetesErgebnis;
        public string tatsaechlichesErgebnis;
        public float geschaetzteDauer;

        // Optionaler Kompatibilitaetszugriff fuer alte Dictionary-basierte Aufrufer.
        public Dictionary<string, string> parameterMap
        {
            get
            {
                var map = new Dictionary<string, string>();
                if (string.IsNullOrWhiteSpace(parameter))
                    return map;
                map["raw"] = parameter;
                return map;
            }
            set
            {
                if (value == null || value.Count == 0)
                {
                    parameter = "";
                    return;
                }
                parameter = value.ContainsKey("raw")
                    ? value["raw"]
                    : string.Join(",", value.Select(kv => $"{kv.Key}={kv.Value}"));
            }
        }
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
