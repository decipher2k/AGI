using UnityEngine;
using UnityEngine.EventSystems;
using BilligAGI.Modelle;

namespace BilligAGI.Welt
{
    public class WeltController : MonoBehaviour
    {
        [Header("Referenzen")]
        public WeltModell weltModell;
        public Bio.WetterSystem wetterSystem;
        public Light directionalLight;

        [Header("Prefab-Registry")]
        public GameObject[] registriertePrefabs;

        public GameObject SpawnObjekt(string prefabName, Vector3 pos, Quaternion rot)
        {
            GameObject prefab = FindePrefab(prefabName);
            if (prefab == null)
            {
                Debug.LogWarning($"[WeltController] Prefab '{prefabName}' nicht gefunden.");
                return null;
            }

            var obj = Instantiate(prefab, pos, rot);
            obj.name = prefabName + "_" + Random.Range(1000, 9999);

            // Im WeltModell registrieren
            weltModell?.RegistriereObjekt(obj);

            return obj;
        }

        public void EntferneObjekt(GameObject obj)
        {
            if (obj == null) return;
            weltModell?.EntferneObjekt(obj.GetInstanceID().ToString());
            Destroy(obj);
        }

        public void BewegeObjekt(GameObject obj, Vector3 neuPos)
        {
            if (obj == null) return;
            obj.transform.position = neuPos;
            weltModell?.AktualisiereObjektPosition(obj);
        }

        public void SetzeWetter(WetterTyp typ, float intensitaet)
        {
            if (wetterSystem != null)
                wetterSystem.SetzeWetter(typ, intensitaet);
            if (weltModell != null)
            {
                weltModell.zustand.wetter = typ;
                weltModell.zustand.wetterIntensitaet = intensitaet;
            }
        }

        public void SetzeTageszeit(float stunde)
        {
            if (directionalLight != null)
            {
                float winkel = (stunde / 24f) * 360f - 90f;
                directionalLight.transform.rotation = Quaternion.Euler(winkel, -30, 0);

                // Lichtintensitaet nach Tageszeit
                float intensitaet = Mathf.Clamp01(Mathf.Sin(stunde / 24f * Mathf.PI));
                directionalLight.intensity = 0.2f + intensitaet * 1.3f;
            }
            if (weltModell != null)
                weltModell.zustand.tageszeit = stunde;
        }

        public void AusloesePhysikEvent(string eventTyp, Vector3 position)
        {
            switch (eventTyp.ToLowerInvariant())
            {
                case "explosion":
                    // Kraefte auf naheliegende Rigidbodies
                    foreach (var col in Physics.OverlapSphere(position, 5f))
                    {
                        var rb = col.GetComponent<Rigidbody>();
                        if (rb != null)
                            rb.AddExplosionForce(500f, position, 5f);
                    }
                    break;
                case "wasserfluss":
                    Debug.Log($"[WeltController] Wasserfluss bei {position}");
                    break;
                default:
                    Debug.Log($"[WeltController] Unbekanntes Event: {eventTyp}");
                    break;
            }
        }

        private GameObject FindePrefab(string name)
        {
            if (registriertePrefabs == null) return null;
            name = name.ToLowerInvariant();
            foreach (var p in registriertePrefabs)
            {
                if (p != null && p.name.ToLowerInvariant().Contains(name))
                    return p;
            }
            return null;
        }

        public int RegistriereSzeneObjekte(Transform root = null, bool clearVorher = false)
        {
            if (weltModell == null || weltModell.zustand == null)
                return 0;

            if (clearVorher)
                weltModell.zustand.objekte.Clear();

            int count = 0;
            Transform[] transforms = root != null
                ? root.GetComponentsInChildren<Transform>(true)
                : Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);

            foreach (var t in transforms)
            {
                if (t == null) continue;
                var go = t.gameObject;

                if (!go.activeInHierarchy) continue;
                if (go.GetComponent<Canvas>() != null) continue;
                if (go.GetComponent<EventSystem>() != null) continue;

                bool istWeltrelevant = go.GetComponent<Renderer>() != null
                    || go.GetComponent<Collider>() != null
                    || go.GetComponent<Rigidbody>() != null
                    || go.GetComponent<Light>() != null;

                if (!istWeltrelevant) continue;

                weltModell.RegistriereObjekt(go);
                count++;
            }

            weltModell.AktualisiereRelationen();
            return count;
        }
    }
}
