using System;
using System.Collections.Generic;

namespace BilligAGI.Modelle
{
    [Serializable]
    public class SozialeAnalyse
    {
        public List<ErkannterMechanismus> erkannteMechanismen = new List<ErkannterMechanismus>();
        public string archetyp;
        public string archetypAspekt; // "licht" oder "schatten"
        public List<string> aktiveArchetypen = new List<string>();
        public string alchemischePhase;
        public string transformationsImpuls;
        public Dictionary<string, string> tomVorhersagen = new Dictionary<string, string>();
        public float konfidenz;
        public string zeitstempel;
    }

    [Serializable]
    public class ErkannterMechanismus
    {
        public string name;
        public string kategorie;
        public float konfidenz;
        public string begruendung;
    }

    [Serializable]
    public class SozialMechanismus
    {
        public string name;
        public string kategorie;
        public string beschreibung;
        public List<string> erkennungsmuster = new List<string>();
        public string gegenmassnahme;
        public List<string> beispiele = new List<string>();
    }

    [Serializable]
    public class Archetyp
    {
        public string name;
        public string lichtAspekt;
        public string schattenAspekt;
        public string motivation;
        public string angst;
        public string staerke;
        public string schwaeche;
        public string gegenarchetyp;
        public List<string> prototypischeVerhaltensweisen = new List<string>();

        // Quelle: Wie wurde dieser Archetyp gefunden?
        public ArchetypQuelle quelle = ArchetypQuelle.SeedDaten;
    }

    /// <summary>
    /// Eine konkrete Instanz in der sich ein archetypisches Muster gezeigt hat.
    /// DAS ist die episodische Einheit — nicht der Archetyp-Name.
    /// Die AGI erkennt Archetypen durch Aehnlichkeit zu vergangenen Instanzen,
    /// nicht durch Lookup einer globalen Definition.
    /// </summary>
    [Serializable]
    public class ArchetypInstanz
    {
        public string id;
        public string archetypName;             // Welchem Muster zugeordnet (kann sich aendern!)
        public string situation;                // Konkrete Situation
        public string verhalten;                // Was tat der Akteur?
        public string interpretation;           // Warum passt dieses Muster hier?
        public string aspekt;                   // "licht" oder "schatten"
        public string entitaetId;               // Wer zeigte das Verhalten?
        public string kontextCluster;           // Situationstyp: "physik", "sozial", "existenziell" etc.
        public float konfidenz;
        public float zeitstempel;               // Unity-Zeit
        public List<string> kontextMerkmale = new List<string>(); // Was war kennzeichnend fuer diese Situation?
    }

    /// <summary>
    /// Ein Archetyp-Cluster: ergibt sich aus den Instanzen, nicht aus einer Definition.
    /// Die "Bedeutung" eines Archetyps IST die Gemeinsamkeit seiner Instanzen.
    /// Verschiedene Kontextcluster koennen verschiedene Bedeutungen haben.
    /// </summary>
    [Serializable]
    public class ArchetypCluster
    {
        public string archetypName;
        public string kontextCluster;           // z.B. "physik", "sozial", "allgemein"
        public List<string> instanzIds = new List<string>();
        public string konvergierteInterpretation; // Was BEDEUTET dieser Archetyp IN DIESEM Kontext?
        public float konfidenz;
        public int revisionsPass;               // Wie oft wurde die Interpretation revidiert?
        public string letzteRevision;           // Zeitstempel
    }

    [Serializable]
    public enum ArchetypQuelle
    {
        SeedDaten,       // Aus archetypen.json geladen (Ausgangshypothese)
        ErfahrungErkannt, // Durch Beobachtung in der Welt entdeckt
        Konvergiert,      // Durch iterative Revision stabilisiert
        Emergent          // Durch Konzeptschoepfung neu entstanden
    }

    public enum AlchemischePhase { Nigredo, Albedo, Citrinitas, Rubedo }
}
