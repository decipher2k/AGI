using System;
using System.Collections.Generic;

namespace BilligAGI.Modelle
{
    public enum KonzeptTyp
    {
        Archetyp, Mechanismus, AlchemischePhase,
        PhysikKategorie, VAKOGBedeutung, KausalBegriff, Abgeleitet,
        Emergent  // Automatisch durch KonzeptBildung entdeckt
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
        public DriftKlassifikation drift = DriftKlassifikation.BESTAETIGT;

        public Konzept()
        {
            id = Guid.NewGuid().ToString("N").Substring(0, 8);
        }
    }

    [Serializable]
    public class KonzeptRevisionSchritt
    {
        public string konzeptId;
        public int passNummer;
        public int pass { get => passNummer; set => passNummer = value; }
        public string vorherigeDefinition;
        public string neueDefinition;
        public string definition { get => neueDefinition; set => neueDefinition = value; }
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
        public DriftKlassifikation drift { get => driftKlassifikation; set => driftKlassifikation = value; }
        public List<string> betroffeneErfahrungen = new List<string>();
        public bool rueckpropagationNoetig;
        public float driftScore;
        public string selbstKritik;
        public string zeitstempel;
    }
}
