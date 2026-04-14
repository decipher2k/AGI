using System;
using System.Collections.Generic;

namespace BilligAGI.Modelle
{
    [Serializable]
    public class VAKOGProfil
    {
        public float visuell;
        public float auditiv;
        public float kinaesthetisch;
        public float olfaktorisch;
        public float gustatorisch;
        public string beschreibungV;
        public string beschreibungA;
        public string beschreibungK;
        public string beschreibungO;
        public string beschreibungG;

        public float Gesamtintensitaet =>
            (visuell + auditiv + kinaesthetisch + olfaktorisch + gustatorisch) / 5f;

        public string DominanterKanal()
        {
            float max = visuell;
            string kanal = "V";
            if (auditiv > max) { max = auditiv; kanal = "A"; }
            if (kinaesthetisch > max) { max = kinaesthetisch; kanal = "K"; }
            if (olfaktorisch > max) { max = olfaktorisch; kanal = "O"; }
            if (gustatorisch > max) { max = gustatorisch; kanal = "G"; }
            return kanal;
        }
    }

    [Serializable]
    public class VAKOGEintrag
    {
        public string wort;
        public VAKOGProfil profil;
        public string quelle; // "basis", "llm", "erfahrung"
    }
}
