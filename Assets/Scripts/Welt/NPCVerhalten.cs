using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using BilligAGI.Modelle;

namespace BilligAGI.Welt
{
    public enum NPCRolle { Sammler, Waechter, Wanderer, Beobachter, Sozial }

    [RequireComponent(typeof(NavMeshAgent))]
    public class NPCVerhalten : MonoBehaviour
    {
        [Header("Identitaet")]
        public string npcId;
        public string anzeigeName;
        public NPCRolle rolle = NPCRolle.Wanderer;

        [Header("Verhalten")]
        public float aktionsRadius = 15f;
        public float warteZeit = 3f;
        public float sichtweite = 10f;
        public List<string> interessanteObjekte = new List<string>(); // Tags

        private NavMeshAgent navAgent;
        private float warteTimer;
        private Vector3 startPosition;
        private GameObject getragenObjekt;

        // Beobachtbare Aktionshistorie — die AGI kann diese lesen
        private List<NPCAktion> aktionsHistorie = new List<NPCAktion>();
        private NPCAktion aktuelleAktion;
        private const int MAX_HISTORIE = 50;

        public string AktuelleAktionsBeschreibung => aktuelleAktion?.beschreibung ?? "wartet";
        public List<NPCAktion> AktionsHistorie => aktionsHistorie;
        public GameObject GetragenObjekt => getragenObjekt;

        private void Awake()
        {
            navAgent = GetComponent<NavMeshAgent>();
            startPosition = transform.position;

            if (string.IsNullOrEmpty(npcId))
                npcId = $"npc_{gameObject.GetInstanceID()}";
            if (string.IsNullOrEmpty(anzeigeName))
                anzeigeName = gameObject.name;
        }

        private void Update()
        {
            if (navAgent.pathPending) return;

            // Wenn am Ziel angekommen, warten und dann naechste Aktion
            if (!navAgent.hasPath || navAgent.remainingDistance < 0.5f)
            {
                warteTimer -= Time.deltaTime;
                if (warteTimer <= 0f)
                {
                    FuehreRollenAktionAus();
                    warteTimer = warteZeit + Random.Range(-1f, 2f);
                }
            }
        }

        private void FuehreRollenAktionAus()
        {
            switch (rolle)
            {
                case NPCRolle.Sammler:
                    AktionSammler();
                    break;
                case NPCRolle.Waechter:
                    AktionWaechter();
                    break;
                case NPCRolle.Wanderer:
                    AktionWanderer();
                    break;
                case NPCRolle.Beobachter:
                    AktionBeobachter();
                    break;
                case NPCRolle.Sozial:
                    AktionSozial();
                    break;
            }
        }

        private void AktionSammler()
        {
            // Suche greifbare Objekte in der Naehe
            if (getragenObjekt == null)
            {
                var ziel = FindeNaechstesObjekt("greifbar");
                if (ziel != null)
                {
                    BewegZu(ziel.transform.position);
                    RegistriereAktion("bewegt_sich_zu", $"bewegt sich zu {ziel.name}");
                }
                else
                {
                    WandereZufaellig();
                }
            }
            else
            {
                // Zum Startpunkt zurueckbringen
                BewegZu(startPosition);
                RegistriereAktion("traegt_zurueck", $"traegt {getragenObjekt.name} zurueck");
            }
        }

        private void AktionWaechter()
        {
            // Patroulliert zwischen Punkten nahe der Startposition
            var ziel = startPosition + new Vector3(
                Random.Range(-aktionsRadius * 0.3f, aktionsRadius * 0.3f),
                0,
                Random.Range(-aktionsRadius * 0.3f, aktionsRadius * 0.3f));
            BewegZu(ziel);
            RegistriereAktion("patroulliert", "patroulliert im Gebiet");
        }

        private void AktionWanderer()
        {
            WandereZufaellig();
        }

        private void AktionBeobachter()
        {
            // Bleibt stehen, dreht sich zu nahen Entitaeten
            var nahe = Physics.OverlapSphere(transform.position, sichtweite);
            foreach (var c in nahe)
            {
                if (c.gameObject == gameObject) continue;
                var npc = c.GetComponent<NPCVerhalten>();
                var agent = c.GetComponent<AGIAgent>();
                if (npc != null || agent != null)
                {
                    transform.LookAt(c.transform.position);
                    RegistriereAktion("beobachtet",
                        $"beobachtet {c.gameObject.name}");
                    return;
                }
            }
            RegistriereAktion("wartet", "wartet und schaut sich um");
        }

        private void AktionSozial()
        {
            // Bewegt sich zu anderen NPCs oder zum AGI-Agent
            var nahe = Physics.OverlapSphere(transform.position, aktionsRadius);
            foreach (var c in nahe)
            {
                if (c.gameObject == gameObject) continue;
                var anderer = c.GetComponent<NPCVerhalten>();
                if (anderer != null)
                {
                    BewegZu(anderer.transform.position);
                    RegistriereAktion("naehert_sich",
                        $"naehert sich {anderer.anzeigeName}");
                    return;
                }
            }
            WandereZufaellig();
        }

        private void WandereZufaellig()
        {
            var ziel = startPosition + new Vector3(
                Random.Range(-aktionsRadius, aktionsRadius),
                0,
                Random.Range(-aktionsRadius, aktionsRadius));
            BewegZu(ziel);
            RegistriereAktion("wandert", "wandert umher");
        }

        private void BewegZu(Vector3 position)
        {
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, aktionsRadius, NavMesh.AllAreas))
                navAgent.SetDestination(hit.position);
        }

        private GameObject FindeNaechstesObjekt(string tag)
        {
            float naheste = float.MaxValue;
            GameObject bestes = null;

            var objekte = Physics.OverlapSphere(transform.position, sichtweite);
            foreach (var c in objekte)
            {
                if (c.CompareTag(tag))
                {
                    float dist = Vector3.Distance(transform.position, c.transform.position);
                    if (dist < naheste)
                    {
                        naheste = dist;
                        bestes = c.gameObject;
                    }
                }
            }
            return bestes;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Sammler greift Objekte auf Kontakt
            if (rolle == NPCRolle.Sammler && getragenObjekt == null && other.CompareTag("greifbar"))
            {
                getragenObjekt = other.gameObject;
                other.transform.SetParent(transform);
                other.transform.localPosition = Vector3.up * 1.5f;
                RegistriereAktion("greift", $"greift {other.gameObject.name}");
            }
        }

        private void RegistriereAktion(string typ, string beschreibung)
        {
            aktuelleAktion = new NPCAktion
            {
                typ = typ,
                beschreibung = beschreibung,
                position = transform.position,
                zeitstempel = Time.time,
                sichtbareObjekte = SichtbareObjekte()
            };
            aktionsHistorie.Add(aktuelleAktion);

            if (aktionsHistorie.Count > MAX_HISTORIE)
                aktionsHistorie.RemoveAt(0);
        }

        private List<string> SichtbareObjekte()
        {
            var sichtbar = new List<string>();
            var nahe = Physics.OverlapSphere(transform.position, sichtweite);
            foreach (var c in nahe)
            {
                if (c.gameObject == gameObject) continue;
                // Sichtlinie pruefen
                Vector3 richtung = c.transform.position - transform.position;
                if (Physics.Raycast(transform.position + Vector3.up, richtung.normalized,
                    out RaycastHit hit, sichtweite))
                {
                    if (hit.collider == c)
                        sichtbar.Add(c.gameObject.name);
                }
            }
            return sichtbar;
        }

        public NPCAktion GetLetzteAktion() =>
            aktionsHistorie.Count > 0 ? aktionsHistorie[aktionsHistorie.Count - 1] : null;

        public List<NPCAktion> GetAktionenSeit(float unityZeit)
        {
            return aktionsHistorie.FindAll(a => a.zeitstempel >= unityZeit);
        }
    }

    [System.Serializable]
    public class NPCAktion
    {
        public string typ;
        public string beschreibung;
        public Vector3 position;
        public float zeitstempel;
        public List<string> sichtbareObjekte;
    }
}
