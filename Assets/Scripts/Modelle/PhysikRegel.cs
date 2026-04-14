using System;

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
    }
}
