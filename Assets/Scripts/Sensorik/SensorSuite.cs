using UnityEngine;
using BilligAGI.Modelle;
using System.Collections.Generic;

namespace BilligAGI.Sensorik
{
    public class SensorSuite : MonoBehaviour
    {
        [Header("Konfiguration")]
        public float sensorRadius = 10f;
        public int raycastAnzahl = 12;
        public float sensorFrequenzHz = 5f; // Updates pro Sekunde
        public LayerMask sensorLayer = ~0;

        [Header("Referenzen")]
        public Camera agentCamera;

        private RenderTexture renderTexture;
        private Texture2D readbackTexture;
        private float naechsterUpdate;
        private SensorDaten letzterSnapshot;
        private Color[] vorherigePixel;

        public SensorDaten LetzterSnapshot => letzterSnapshot;

        private void Awake()
        {
            if (agentCamera == null)
                agentCamera = GetComponentInChildren<Camera>();

            renderTexture = new RenderTexture(84, 84, 16);
            readbackTexture = new Texture2D(84, 84, TextureFormat.RGB24, false);

            if (agentCamera != null)
                agentCamera.targetTexture = renderTexture;

            letzterSnapshot = new SensorDaten();
        }

        private void Update()
        {
            if (Time.time < naechsterUpdate) return;
            naechsterUpdate = Time.time + 1f / sensorFrequenzHz;
            AktualisiereSensoren();
        }

        public SensorDaten AktualisiereSensoren()
        {
            var daten = new SensorDaten();
            var pos = transform.position;

            // Position & Rotation
            daten.agentenPosition = new float[] { pos.x, pos.y, pos.z };
            var rot = transform.eulerAngles;
            daten.agentenRotation = new float[] { rot.x, rot.y, rot.z };
            daten.zeitstempel = System.DateTime.UtcNow.ToString("o");

            // Visuell: Kamera-Analyse
            if (agentCamera != null && renderTexture != null)
            {
                RenderTexture.active = renderTexture;
                readbackTexture.ReadPixels(new Rect(0, 0, 84, 84), 0, 0);
                readbackTexture.Apply();
                RenderTexture.active = null;

                Color[] pixel = readbackTexture.GetPixels();

                // Helligkeit
                float helligkeit = 0f;
                float r = 0f, g = 0f, b = 0f;
                for (int i = 0; i < pixel.Length; i++)
                {
                    helligkeit += pixel[i].grayscale;
                    r += pixel[i].r; g += pixel[i].g; b += pixel[i].b;
                }
                int count = pixel.Length;
                daten.helligkeit = helligkeit / count;
                daten.dominanteFarbe = new float[] { r / count, g / count, b / count };

                // Bewegungserkennung (Frame-Differenz)
                if (vorherigePixel != null && vorherigePixel.Length == pixel.Length)
                {
                    float diff = 0f;
                    for (int i = 0; i < pixel.Length; i += 10) // Sampling
                    {
                        diff += Mathf.Abs(pixel[i].grayscale - vorherigePixel[i].grayscale);
                    }
                    daten.bewegungsIntensitaet = diff / (pixel.Length / 10f);
                }
                vorherigePixel = pixel;
            }

            // Spatial: Raycasts
            var raycasts = new List<RaycastInfo>();
            for (int i = 0; i < raycastAnzahl; i++)
            {
                float winkel = i * (360f / raycastAnzahl);
                Vector3 richtung = Quaternion.Euler(0, winkel, 0) * transform.forward;
                if (Physics.Raycast(pos + Vector3.up, richtung, out RaycastHit hit, sensorRadius, sensorLayer))
                {
                    raycasts.Add(new RaycastInfo
                    {
                        distanz = hit.distance,
                        getroffenerTyp = hit.collider.tag,
                        getroffenerName = hit.collider.gameObject.name,
                        punkt = new float[] { hit.point.x, hit.point.y, hit.point.z }
                    });
                }
            }
            daten.raycasts = raycasts.ToArray();

            // Nahbereich-Objekte (SphereCast)
            var nahObjekte = new List<NahbereichObjekt>();
            Collider[] colliders = Physics.OverlapSphere(pos, sensorRadius, sensorLayer);
            foreach (var col in colliders)
            {
                if (col.gameObject == gameObject) continue;
                Vector3 dir = col.transform.position - pos;
                nahObjekte.Add(new NahbereichObjekt
                {
                    id = col.gameObject.GetInstanceID().ToString(),
                    name = col.gameObject.name,
                    typ = col.tag,
                    distanz = dir.magnitude,
                    richtung = new float[] { dir.normalized.x, dir.normalized.y, dir.normalized.z },
                    tags = new string[] { col.tag }
                });
            }
            daten.nahbereichObjekte = nahObjekte.ToArray();

            // Kinaesthetisch
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
                daten.geschwindigkeit = rb.linearVelocity.magnitude;

            // Audio (vereinfacht — AudioListener Basis)
            daten.audioPegel = 0f; // Wird in Integration mit AudioListener erweitert

            letzterSnapshot = daten;
            return daten;
        }

        public void RegistriereKollision(float kraft)
        {
            if (letzterSnapshot != null)
                letzterSnapshot.kollisionsKraft = kraft;
        }

        private void OnCollisionEnter(Collision collision)
        {
            float kraft = collision.impulse.magnitude;
            RegistriereKollision(kraft);
        }

        private void OnDestroy()
        {
            if (renderTexture != null)
                renderTexture.Release();
        }
    }
}
