using System;
using UnityEngine;

namespace BilligAGI.Modelle
{
    [Serializable]
    public class SensorDaten
    {
        // Visuell
        public float helligkeit;
        public float[] dominanteFarbe = new float[3]; // r, g, b
        public float bewegungsIntensitaet;

        // Spatial
        public RaycastInfo[] raycasts;
        public NahbereichObjekt[] nahbereichObjekte;

        // Kinästhetisch
        public float kollisionsKraft;
        public float geschwindigkeit;

        // Auditiv
        public float audioPegel;

        // Kontextdaten
        public float[] agentenPosition = new float[3];
        public float[] agentenRotation = new float[3];
        public string zeitstempel;
    }

    [Serializable]
    public class RaycastInfo
    {
        public float distanz;
        public string getroffenerTyp;
        public string getroffenerName;
        public float[] punkt = new float[3];
    }

    [Serializable]
    public class NahbereichObjekt
    {
        public string id;
        public string name;
        public string typ;
        public float distanz;
        public float[] richtung = new float[3];
        public string[] tags;
    }
}
