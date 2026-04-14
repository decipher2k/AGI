using System;
using System.Collections.Generic;
using UnityEngine;

namespace BilligAGI.Modelle
{
    public enum WetterTyp { Klar, Bewoelkt, Regen, Schnee, Nebel, Sturm }

    [Serializable]
    public class WeltZustand
    {
        public Dictionary<string, WeltObjekt> objekte = new Dictionary<string, WeltObjekt>();
        public WetterTyp wetter = WetterTyp.Klar;
        public float wetterIntensitaet = 0.5f;
        public float tageszeit = 12f; // 0-24
        public List<WeltAenderung> historie = new List<WeltAenderung>();
        public string zeitstempel;
    }

    [Serializable]
    public class WeltObjekt
    {
        public string id;
        public string name;
        public string typ;
        public float[] position = new float[3]; // x, y, z
        public float[] rotation = new float[3];
        public List<string> tags = new List<string>();
        public string zustand; // "offen", "geschlossen", etc.
        public Dictionary<string, string> relationen = new Dictionary<string, string>(); // "auf": "tisch_01"
    }

    [Serializable]
    public class WeltAenderung
    {
        public string objektId;
        public string aenderungsTyp;
        public string vorher;
        public string nachher;
        public string zeitstempel;
    }

    [Serializable]
    public class WeltBeschreibung
    {
        public string name;
        public string biom; // "wald", "wiese", "innen"
        public int breite = 100;
        public int tiefe = 100;
        public List<string> prefabTypen = new List<string>();
        public int objektDichte = 20;
    }
}
