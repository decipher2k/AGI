using System;
using System.Collections.Generic;

namespace BilligAGI.Modelle
{
    public enum ZielTyp { EXPLORATION, EXPERIMENT, KONSTRUKTION, VERSTAENDNIS, SOZIAL, REVISION, AUFGABE }
    public enum ZielStatus { GEPLANT, AKTIV, ERREICHT, GESCHEITERT, GEPARKT }

    [Serializable]
    public class Ziel
    {
        public string id;
        public string name;
        public string beschreibung;
        public float prioritaet;
        public float effektivePrioritaet;
        public ZielTyp typ;
        public ZielStatus status = ZielStatus.GEPLANT;
        public List<string> teilziele = new List<string>();
        public string erfolgsbedingung;
        public string ergebnis;
        public string zeitstempel;
        public string erstelltAm;
        public float deadline = -1f; // -1 = keine Deadline

        public Ziel()
        {
            id = Guid.NewGuid().ToString("N").Substring(0, 8);
            zeitstempel = DateTime.UtcNow.ToString("o");
        }
    }
}
