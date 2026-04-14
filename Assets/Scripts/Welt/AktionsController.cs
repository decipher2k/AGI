using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using BilligAGI.Modelle;

namespace BilligAGI.Welt
{
    public class AktionsController : MonoBehaviour
    {
        [Header("Referenzen")]
        public AGIAgent agent;
        public Sensorik.SensorSuite sensorSuite;

        [Header("Interaktion")]
        public float greifDistanz = 2f;
        public float interaktionsDistanz = 2.5f;
        public float wurfKraft = 10f;

        private void Awake()
        {
            if (agent == null) agent = GetComponent<AGIAgent>();
            if (sensorSuite == null) sensorSuite = GetComponent<Sensorik.SensorSuite>();
        }

        public async Task<AktionsErgebnis> Bewegen(Vector3 ziel)
        {
            var ergebnis = new AktionsErgebnis();
            if (!agent.VerbraucheEnergie(agent.energieProAktion))
            {
                ergebnis.erfolg = false;
                ergebnis.beschreibung = "Nicht genug Energie.";
                return ergebnis;
            }

            agent.NavAgent.SetDestination(ziel);

            // Warten auf Ankunft
            while (agent.NavAgent.pathPending)
                await Task.Yield();

            while (agent.NavAgent.remainingDistance > agent.NavAgent.stoppingDistance)
            {
                if (!agent.NavAgent.hasPath)
                {
                    ergebnis.erfolg = false;
                    ergebnis.beschreibung = "Kein Pfad gefunden.";
                    return ergebnis;
                }
                await Task.Yield();
            }

            ergebnis.erfolg = true;
            ergebnis.beschreibung = $"Angekommen bei [{ziel.x:F1},{ziel.y:F1},{ziel.z:F1}]";
            ergebnis.sensorDatenNachher = sensorSuite?.AktualisiereSensoren();
            return ergebnis;
        }

        public AktionsErgebnis Greifen(GameObject objekt)
        {
            var ergebnis = new AktionsErgebnis();

            if (objekt == null)
            {
                ergebnis.erfolg = false;
                ergebnis.beschreibung = "Kein Objekt angegeben.";
                return ergebnis;
            }

            float distanz = Vector3.Distance(transform.position, objekt.transform.position);
            if (distanz > greifDistanz)
            {
                ergebnis.erfolg = false;
                ergebnis.beschreibung = $"Objekt '{objekt.name}' zu weit entfernt ({distanz:F1}m).";
                return ergebnis;
            }

            if (!agent.HaendeFrei())
            {
                ergebnis.erfolg = false;
                ergebnis.beschreibung = "Haende nicht frei.";
                return ergebnis;
            }

            agent.VerbraucheEnergie(agent.energieProAktion * 0.5f);
            agent.NimmObjekt(objekt);
            ergebnis.erfolg = true;
            ergebnis.beschreibung = $"Objekt '{objekt.name}' gegriffen.";
            ergebnis.zustandsAenderungen["inventar"] = objekt.name;
            return ergebnis;
        }

        public AktionsErgebnis Ablegen(Vector3 position)
        {
            var ergebnis = new AktionsErgebnis();
            var obj = agent.LegeObjektAb();
            if (obj == null)
            {
                ergebnis.erfolg = false;
                ergebnis.beschreibung = "Nichts im Inventar.";
                return ergebnis;
            }

            obj.transform.position = position;
            agent.VerbraucheEnergie(agent.energieProAktion * 0.3f);
            ergebnis.erfolg = true;
            ergebnis.beschreibung = $"'{obj.name}' abgelegt bei [{position.x:F1},{position.y:F1},{position.z:F1}].";
            ergebnis.sensorDatenNachher = sensorSuite?.AktualisiereSensoren();
            return ergebnis;
        }

        public AktionsErgebnis Werfen(Vector3 richtung)
        {
            var ergebnis = new AktionsErgebnis();
            var obj = agent.LegeObjektAb();
            if (obj == null)
            {
                ergebnis.erfolg = false;
                ergebnis.beschreibung = "Nichts zum Werfen.";
                return ergebnis;
            }

            var rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.AddForce(richtung.normalized * wurfKraft, ForceMode.Impulse);
            }

            agent.VerbraucheEnergie(agent.energieProAktion);
            ergebnis.erfolg = true;
            ergebnis.beschreibung = $"'{obj.name}' geworfen.";
            return ergebnis;
        }

        public AktionsErgebnis Interagieren(GameObject objekt)
        {
            var ergebnis = new AktionsErgebnis();
            if (objekt == null)
            {
                ergebnis.erfolg = false;
                ergebnis.beschreibung = "Kein Objekt.";
                return ergebnis;
            }

            float distanz = Vector3.Distance(transform.position, objekt.transform.position);
            if (distanz > interaktionsDistanz)
            {
                ergebnis.erfolg = false;
                ergebnis.beschreibung = $"Zu weit entfernt ({distanz:F1}m).";
                return ergebnis;
            }

            agent.VerbraucheEnergie(agent.energieProAktion * 0.5f);

            // Kontext-basierte Interaktion via Tags
            if (objekt.CompareTag("Tuer"))
            {
                // Toggle Tuer
                bool istOffen = objekt.transform.localEulerAngles.y > 45f;
                objekt.transform.localEulerAngles = istOffen ? Vector3.zero : new Vector3(0, 90, 0);
                ergebnis.erfolg = true;
                ergebnis.beschreibung = istOffen ? $"Tuer '{objekt.name}' geschlossen." : $"Tuer '{objekt.name}' geoeffnet.";
                ergebnis.zustandsAenderungen[objekt.name] = istOffen ? "geschlossen" : "offen";
            }
            else if (objekt.CompareTag("Schalter"))
            {
                ergebnis.erfolg = true;
                ergebnis.beschreibung = $"Schalter '{objekt.name}' betaetigt.";
                ergebnis.zustandsAenderungen[objekt.name] = "aktiviert";
            }
            else
            {
                ergebnis.erfolg = true;
                ergebnis.beschreibung = $"Mit '{objekt.name}' interagiert.";
            }

            ergebnis.sensorDatenNachher = sensorSuite?.AktualisiereSensoren();
            return ergebnis;
        }

        public AktionsErgebnis Beobachten(Vector3 richtung)
        {
            transform.LookAt(transform.position + richtung);
            agent.VerbraucheEnergie(agent.energieProAktion * 0.1f);

            return new AktionsErgebnis
            {
                erfolg = true,
                beschreibung = $"Beobachte Richtung [{richtung.x:F1},{richtung.y:F1},{richtung.z:F1}].",
                sensorDatenNachher = sensorSuite?.AktualisiereSensoren()
            };
        }

        public void Sprechen(string text)
        {
            Debug.Log($"[Agent spricht] {text}");
            // In-Game: World-Space Canvas Sprechblase (wird in UI implementiert)
        }

        public async Task Warten(float sekunden)
        {
            float ende = Time.time + sekunden;
            while (Time.time < ende)
                await Task.Yield();
        }
    }
}
