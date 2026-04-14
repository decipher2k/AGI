using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BilligAGI.Kern;
using BilligAGI.Modelle;

namespace BilligAGI.UI
{
    public class ZielAnzeige : MonoBehaviour
    {
        [Header("Referenzen")]
        public AGIKern agiKern;

        [Header("UI")]
        public Transform zielListeParent;
        public GameObject zielEintragPrefab;

        private float updateIntervall = 1f;
        private float letzterUpdate;

        private void Update()
        {
            if (Time.time - letzterUpdate < updateIntervall) return;
            letzterUpdate = Time.time;
            AktualisiereAnzeige();
        }

        private void AktualisiereAnzeige()
        {
            if (agiKern == null || zielListeParent == null) return;

            // Alte Eintraege loeschen
            foreach (Transform child in zielListeParent)
                Destroy(child.gameObject);

            var ziele = agiKern.GetZielManager()?.GetAlleAktiven();
            if (ziele == null) return;

            foreach (var ziel in ziele)
            {
                if (zielEintragPrefab != null)
                {
                    var eintrag = Instantiate(zielEintragPrefab, zielListeParent);
                    var text = eintrag.GetComponentInChildren<Text>();
                    if (text != null)
                    {
                        string statusIcon = ZielStatusIcon(ziel.status);
                        string farbe = ZielFarbe(ziel);
                        text.text = $"{statusIcon} {ziel.beschreibung} (Prio: {ziel.effektivePrioritaet:F2})";
                        text.color = ParseFarbe(farbe);
                    }
                }
            }

            // Historie (letzte 5)
            var historie = agiKern.GetZielManager()?.GetHistorie();
            if (historie != null)
            {
                int start = Mathf.Max(0, historie.Count - 5);
                for (int i = start; i < historie.Count; i++)
                {
                    var z = historie[i];
                    if (zielEintragPrefab != null)
                    {
                        var eintrag = Instantiate(zielEintragPrefab, zielListeParent);
                        var text = eintrag.GetComponentInChildren<Text>();
                        if (text != null)
                        {
                            text.text = $"{ZielStatusIcon(z.status)} {z.beschreibung}";
                            text.color = z.status == ZielStatus.ERREICHT
                                ? Color.green : new Color(0.7f, 0.3f, 0.3f);
                        }
                    }
                }
            }
        }

        private string ZielStatusIcon(ZielStatus status)
        {
            switch (status)
            {
                case ZielStatus.AKTIV: return ">";
                case ZielStatus.GEPLANT: return "~";
                case ZielStatus.ERREICHT: return "+";
                case ZielStatus.GESCHEITERT: return "x";
                case ZielStatus.GEPARKT: return "-";
                default: return "?";
            }
        }

        private string ZielFarbe(Ziel ziel)
        {
            if (ziel.typ == ZielTyp.REVISION) return "gelb";
            if (ziel.effektivePrioritaet > 0.8f) return "rot";
            return "weiss";
        }

        private Color ParseFarbe(string farbe)
        {
            switch (farbe)
            {
                case "gelb": return Color.yellow;
                case "rot": return new Color(1f, 0.4f, 0.4f);
                default: return Color.white;
            }
        }
    }
}
