using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using BilligAGI.Modelle;

namespace BilligAGI.Welt
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Rigidbody))]
    public class AGIAgent : MonoBehaviour
    {
        [Header("Referenzen")]
        public Transform handPunkt; // Wo gegriffene Objekte angehängt werden
        public Sensorik.SensorSuite sensorSuite;

        [Header("Energie")]
        public float maxEnergie = 1f;
        public float aktuelleEnergie = 1f;
        public float energieRegenRate = 0.01f;   // pro Sekunde
        public float energieProAktion = 0.02f;

        private NavMeshAgent navAgent;
        private Rigidbody rb;
        private List<GameObject> inventar = new List<GameObject>();
        private string aktivesZielId;

        public NavMeshAgent NavAgent => navAgent;
        public List<GameObject> Inventar => inventar;

        private void Awake()
        {
            navAgent = GetComponent<NavMeshAgent>();
            rb = GetComponent<Rigidbody>();

            if (sensorSuite == null)
                sensorSuite = GetComponent<Sensorik.SensorSuite>();
        }

        private void Update()
        {
            // Energie-Regeneration
            if (aktuelleEnergie < maxEnergie)
                aktuelleEnergie = Mathf.Min(maxEnergie, aktuelleEnergie + energieRegenRate * Time.deltaTime);
        }

        public AgentZustand ExportiereZustand()
        {
            var pos = transform.position;
            var rot = transform.eulerAngles;
            var inventarIds = new List<string>();
            foreach (var obj in inventar)
            {
                if (obj != null)
                    inventarIds.Add(obj.name);
            }

            return new AgentZustand
            {
                position = new float[] { pos.x, pos.y, pos.z },
                orientierung = new float[] { rot.x, rot.y, rot.z },
                inventar = inventarIds,
                energie = aktuelleEnergie,
                aktivesZielId = aktivesZielId,
                aktuellerModus = "reaktiv",
                zeitstempel = System.DateTime.UtcNow.ToString("o"),
            };
        }

        public void SetzeAktivesZiel(string zielId) => aktivesZielId = zielId;

        public bool VerbraucheEnergie(float menge)
        {
            if (aktuelleEnergie < menge) return false;
            aktuelleEnergie -= menge;
            return true;
        }

        public bool HaendeFrei() => inventar.Count == 0;

        public GameObject GetInventarObjekt(int index = 0)
        {
            if (index >= 0 && index < inventar.Count)
                return inventar[index];
            return null;
        }

        public void NimmObjekt(GameObject obj)
        {
            if (obj == null) return;
            inventar.Add(obj);
            obj.transform.SetParent(handPunkt != null ? handPunkt : transform);
            obj.transform.localPosition = Vector3.zero;
            var objRb = obj.GetComponent<Rigidbody>();
            if (objRb != null)
                objRb.isKinematic = true;
        }

        public GameObject LegeObjektAb(int index = 0)
        {
            if (index < 0 || index >= inventar.Count) return null;
            var obj = inventar[index];
            inventar.RemoveAt(index);
            obj.transform.SetParent(null);
            var objRb = obj.GetComponent<Rigidbody>();
            if (objRb != null)
                objRb.isKinematic = false;
            return obj;
        }
    }
}
