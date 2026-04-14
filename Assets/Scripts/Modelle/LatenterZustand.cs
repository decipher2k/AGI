using System;

namespace BilligAGI.Modelle
{
    public enum LatentHerkunft { Sensorik, Text, Aktion, Konzept, Erfahrung }

    [Serializable]
    public class LatenterZustand
    {
        public string kontextId;
        public string quellId { get => kontextId; set => kontextId = value; }
        public float[] embedding;
        public float[] vektor { get => embedding; set => embedding = value; }
        public LatentHerkunft herkunft;
        public string label;
        public string zeitstempel;
        public float drift;

        public LatenterZustand(int dim = 128)
        {
            embedding = new float[dim];
            zeitstempel = DateTime.UtcNow.ToString("o");
        }
    }
}
