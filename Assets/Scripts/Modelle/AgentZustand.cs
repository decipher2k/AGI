using System;
using System.Collections.Generic;

namespace BilligAGI.Modelle
{
    [Serializable]
    public class AgentZustand
    {
        public float[] position = new float[3];
        public float[] orientierung = new float[3];
        public List<string> inventar = new List<string>();
        public float energie = 1f;
        public string aktivesZielId;
        public string aktuellerModus; // "reaktiv" / "autonom"
        public string zeitstempel;
    }
}
