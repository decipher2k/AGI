using System;
using System.Collections.Generic;

namespace BilligAGI.Modelle
{
    public enum KonzeptTyp
    {
        Archetyp, Mechanismus, AlchemischePhase,
        PhysikKategorie, VAKOGBedeutung, KausalBegriff, Abgeleitet
    }

    public enum DriftKlassifikation
    {
        BESTAETIGT, VERSCHOBEN, WIDERSPROCHEN, ERWEITERT, ABGELEITET, UMSTRITTEN
    }

    [Serializable]
    public class Konzept
    {
        public string id;
        public string name;
        public KonzeptTyp typ;
        public string aktuelleDefinition;
        public string ursprungsDefinition;
        public List<KonzeptRevisionSchritt> revisionsHistorie = new List<KonzeptRevisionSchritt>();
        public int anzahlAnwendungen;
        public string letztePruefung;
        public float driftScore;

        public Konzept()
        {
            id = Guid.NewGuid().ToString("N").Substring(0, 8);
        }
    }

    [Serializable]
    public class KonzeptRevisionSchritt
    {
        public int passNummer;
        public string vorherigeDefinition;
        public string neueDefinition;
        public string ausloeser; // welche Erfahrung
        public List<string> evidenz = new List<string>();
        public string zeitstempel;

        public KonzeptRevisionSchritt()
        {
            zeitstempel = DateTime.UtcNow.ToString("o");
        }
    }

    [Serializable]
    public class KonzeptRevisionErgebnis
    {
        public string konzeptId;
        public string alteDefinition;
        public string neueDefinition;
        public DriftKlassifikation driftKlassifikation;
        public List<string> betroffeneErfahrungen = new List<string>();
        public bool rueckpropagationNoetig;
        public float driftScore;
    }
}
