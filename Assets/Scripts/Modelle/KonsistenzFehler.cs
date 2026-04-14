using System;
using System.Collections.Generic;

namespace BilligAGI.Modelle
{
    public enum KonsistenzFehlerTyp { LOGISCH, TEMPORAL, RAEUMLICH }

    [Serializable]
    public class KonsistenzFehler
    {
        public string id;
        public KonsistenzFehlerTyp typ;
        public List<string> betroffeneEntitaeten = new List<string>();
        public float schweregrad;
        public bool autoRepariert;
        public string ursache;
        public string zeitstempel;

        public KonsistenzFehler()
        {
            id = Guid.NewGuid().ToString("N").Substring(0, 8);
            zeitstempel = DateTime.UtcNow.ToString("o");
        }
    }
}
