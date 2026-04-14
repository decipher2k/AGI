using System;
using System.Collections.Generic;

namespace BilligAGI.Modelle
{
    [Serializable]
    public class PhysikRegel
    {
        public string id;
        public string wenn; // Bedingung
        public string dann; // Ergebnis
        public float konfidenz;
        public string quelle; // "experiment", "llm", "beobachtung"
        public int anzahlBestaetigungen;
        public string zeitstempel;
        public List<string> tags = new List<string>();

        // Kompatibilitaets-Aliase fuer aeltere Aufrufer
        public string beschreibung { get => wenn; set => wenn = value; }
        public string ergebnis { get => dann; set => dann = value; }
        public int bestaetigungen { get => anzahlBestaetigungen; set => anzahlBestaetigungen = value; }

        public PhysikRegel()
        {
            id = Guid.NewGuid().ToString("N").Substring(0, 8);
            zeitstempel = DateTime.UtcNow.ToString("o");
        }
    }

    [Serializable]
    public class ExperimentErgebnis
    {
        public string hypothese;
        public string beobachtung;
        public bool bestaetigt;
        public SensorDaten sensorDaten;
        public PhysikRegel extrahierteRegel;
        public string zeitstempel;

        // Kompatibilitaet: vorher/nachher Snapshot-Pattern
        public SensorDaten sensorDatenVorher;
        public SensorDaten sensorDatenNachher;
    }

    [Serializable]
    public class PlausibilitaetsErgebnis
    {
        public string aussage;
        public bool plausibel;
        public float konfidenz;
        public string begruendung;
        public string quelle; // "regel", "llm", "experiment"
        public bool experimentVorgeschlagen;

        // Kompatibilitaets-Aliase
        public string erklaerung { get => begruendung; set => begruendung = value; }
        public string basierung { get => quelle; set => quelle = value; }
        public string experimentVorschlag
        {
            get => experimentVorgeschlagen ? "Experiment vorgeschlagen" : string.Empty;
            set => experimentVorgeschlagen = !string.IsNullOrWhiteSpace(value);
        }
    }
}
