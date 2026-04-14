using System;
using System.Collections.Generic;

namespace BilligAGI.Modelle
{
    [Serializable]
    public class MentalesModell
    {
        public string entitaetId;
        public string name;
        public List<string> wissen = new List<string>();   // Was weiß sie?
        public List<string> glauben = new List<string>();  // Was glaubt sie? (auch Falsches)
        public List<string> ziele = new List<string>();    // Was will sie?
        public List<string> erwartungen = new List<string>();
        public string letzteAktualisierung;
        public float konfidenz;

        public MentalesModell()
        {
            letzteAktualisierung = DateTime.UtcNow.ToString("o");
        }
    }
}
