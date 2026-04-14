using System;
using System.Collections.Generic;

namespace BilligAGI.Modelle
{
    [Serializable]
    public class ZeitlicherKontext
    {
        public string zeitstempel;
        public float dauer;          // Sekunden
        public int zyklusNummer;
        public string vorher;        // ErfahrungsId
        public string nachher;       // ErfahrungsId
        public string relativeZeit;  // "kurz nachdem...", "lange vor..."
        public float deadline = -1f; // -1 = keine
        public string erfahrungsId;
        public float unityZeit;
        public float dauerSekunden;
        public List<string> vorgaengerIds = new List<string>();
    }
}
