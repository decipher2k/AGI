using System.Threading.Tasks;
using BilligAGI.Modelle;
using BilligAGI.Welt;
using UnityEngine;

namespace BilligAGI.Intentionalitaet
{
    public class Ausfuehrer
    {
        private readonly AktionsController aktionsController;
        private readonly WeltModell weltModell;
        private bool notbremseAktiv;

        public Ausfuehrer(AktionsController aktionsController, WeltModell weltModell)
        {
            this.aktionsController = aktionsController;
            this.weltModell = weltModell;
            notbremseAktiv = false;
        }

        public async Task<AktionsErgebnis> FuehreAus(Aktion aktion)
        {
            if (notbremseAktiv)
            {
                return new AktionsErgebnis
                {
                    erfolg = false,
                    beschreibung = "Notbremse aktiv. Keine Ausfuehrung."
                };
            }

            if (aktionsController == null)
            {
                return new AktionsErgebnis
                {
                    erfolg = false,
                    beschreibung = "Kein AktionsController."
                };
            }

            Debug.Log($"[Ausfuehrer] Fuehre aus: {aktion.name} ({aktion.parameter})");

            AktionsErgebnis ergebnis;

            switch (aktion.typ)
            {
                case AktionsTyp.Bewegen:
                    var pos = ParsePosition(aktion.parameter);
                    ergebnis = await aktionsController.Bewegen(pos);
                    break;

                case AktionsTyp.Greifen:
                    var obj = FindeObjekt(aktion.parameter);
                    ergebnis = aktionsController.Greifen(obj);
                    break;

                case AktionsTyp.Ablegen:
                    var ablagePos = ParsePosition(aktion.parameter);
                    ergebnis = aktionsController.Ablegen(ablagePos);
                    break;

                case AktionsTyp.Interagieren:
                    var interObj = FindeObjekt(aktion.parameter);
                    ergebnis = aktionsController.Interagieren(interObj);
                    break;

                case AktionsTyp.Beobachten:
                    var richtung = ParsePosition(aktion.parameter);
                    ergebnis = aktionsController.Beobachten(richtung);
                    break;

                case AktionsTyp.Werfen:
                    var wurfRichtung = ParsePosition(aktion.parameter);
                    ergebnis = aktionsController.Werfen(wurfRichtung);
                    break;

                case AktionsTyp.Warten:
                    float sekunden = 3f;
                    float.TryParse(aktion.parameter, out sekunden);
                    await aktionsController.Warten(sekunden);
                    ergebnis = new AktionsErgebnis
                    {
                        erfolg = true,
                        beschreibung = $"Gewartet: {sekunden:F1}s"
                    };
                    break;

                case AktionsTyp.Sprechen:
                    aktionsController.Sprechen(aktion.parameter);
                    ergebnis = new AktionsErgebnis
                    {
                        erfolg = true,
                        beschreibung = $"Gesprochen: {aktion.parameter}"
                    };
                    break;

                default:
                    ergebnis = new AktionsErgebnis
                    {
                        erfolg = false,
                        beschreibung = $"Unbekannter Aktionstyp: {aktion.typ}"
                    };
                    break;
            }

            // WeltModell aktualisieren
            if (ergebnis.erfolg && weltModell != null)
            {
                weltModell.AktualisiereRelationen();
            }

            return ergebnis;
        }

        public bool IstAusfuehrbar(Aktion aktion, WeltZustand welt)
        {
            if (aktion == null || welt == null) return false;
            // Grundpruefung: Haben wir den Controller?
            return aktionsController != null && !notbremseAktiv;
        }

        public void Notbremse()
        {
            notbremseAktiv = true;
            if (aktionsController != null)
            {
                var agent = aktionsController.agent;
                if (agent != null && agent.NavAgent != null)
                    agent.NavAgent.ResetPath();
            }
            Debug.LogWarning("[Ausfuehrer] NOTBREMSE aktiviert!");
        }

        public void NotbremseAufheben()
        {
            notbremseAktiv = false;
            Debug.Log("[Ausfuehrer] Notbremse aufgehoben.");
        }

        private Vector3 ParsePosition(string parameter)
        {
            if (string.IsNullOrEmpty(parameter)) return Vector3.zero;
            var teile = parameter.Replace("[", "").Replace("]", "").Split(',');
            if (teile.Length >= 3 &&
                float.TryParse(teile[0].Trim(), out float x) &&
                float.TryParse(teile[1].Trim(), out float y) &&
                float.TryParse(teile[2].Trim(), out float z))
            {
                return new Vector3(x, y, z);
            }
            return Vector3.zero;
        }

        private GameObject FindeObjekt(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return GameObject.Find(name);
        }
    }
}
