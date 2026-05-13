using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace BilligAGI.Welt
{
    public class WeltGenerator : MonoBehaviour
    {
        [Header("Prefab-Bibliothek")]
        public GameObject[] naturPrefabs;     // Baeume, Steine, Gras
        public GameObject[] gebaeudePrefabs;  // Waende, Tisch, Stuhl
        public GameObject[] objektPrefabs;    // Greifbare Alltagsgegenstaende
        public GameObject wasserPrefab;

        [Header("Terrain")]
        public Material terrainMaterial;
        public float terrainHoehe = 5f;
        public float noiseScale = 0.05f;

        [Header("NavMesh")]
        // Use MonoBehaviour to avoid hard compile-time dependency on Unity.AI.Navigation package.
        public MonoBehaviour navMeshSurface;

        [Header("Hierarchie")]
        public Transform worldParent;

        private Transform ZielParent => worldParent != null ? worldParent : transform;

        private void Awake()
        {
            EnsureNavMeshSurface();
        }

        private void EnsureNavMeshSurface()
        {
            if (navMeshSurface != null) return;

            // Try to find or add a NavMeshSurface at runtime
            navMeshSurface = GetComponent<MonoBehaviour>();
            if (navMeshSurface != null && navMeshSurface.GetType().Name == "NavMeshSurface")
                return;

            // Check if one already exists on this GameObject
            var existing = GetComponents<MonoBehaviour>();
            foreach (var c in existing)
            {
                if (c != null && c.GetType().Name == "NavMeshSurface")
                {
                    navMeshSurface = c;
                    Debug.Log("[WeltGenerator] Found existing NavMeshSurface on World GameObject.");
                    return;
                }
            }

            // Try to dynamically add NavMeshSurface from AI Navigation package
            var navType = System.Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
            if (navType != null)
            {
                navMeshSurface = (MonoBehaviour)gameObject.AddComponent(navType);
                Debug.Log("[WeltGenerator] Dynamically created NavMeshSurface component.");
            }
            else
            {
                Debug.LogWarning("[WeltGenerator] Unity.AI.Navigation package not found. " +
                    "NavMesh baking disabled. Agent cannot navigate.\n" +
                    "Install com.unity.ai.navigation via Package Manager.");
            }
        }

        public void GeneriereWelt(Modelle.WeltBeschreibung beschreibung)
        {
            Debug.Log($"[WeltGenerator] Generiere Welt: {beschreibung.name} ({beschreibung.biom})");

            // Terrain erzeugen
            ErzeugeTerrain(beschreibung.breite, beschreibung.tiefe);

            // Biom-basierte Vegetation
            switch (beschreibung.biom?.ToLowerInvariant())
            {
                case "wald":
                    VerteilePrefabs(naturPrefabs, beschreibung.objektDichte * 2, beschreibung.breite, beschreibung.tiefe);
                    break;
                case "wiese":
                    VerteilePrefabs(naturPrefabs, beschreibung.objektDichte / 2, beschreibung.breite, beschreibung.tiefe);
                    break;
                case "innen":
                    ErstelleRaum(10f, 4f, 10f);
                    break;
                default:
                    VerteilePrefabs(naturPrefabs, beschreibung.objektDichte, beschreibung.breite, beschreibung.tiefe);
                    break;
            }

            // Objekte verteilen
            VerteilePrefabs(objektPrefabs, beschreibung.objektDichte, beschreibung.breite, beschreibung.tiefe);

            // NavMesh baken
            TryBuildNavMesh();

            Debug.Log("[WeltGenerator] Welt generiert.");
        }

        public void ErstelleSzenario(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "raum mit tisch":
                    ErstelleRaum(8f, 3f, 8f);
                    if (gebaeudePrefabs != null && gebaeudePrefabs.Length > 0)
                        Instantiate(gebaeudePrefabs[0], new Vector3(0, 0.5f, 0), Quaternion.identity);
                    break;
                case "garten":
                    GeneriereWelt(new Modelle.WeltBeschreibung
                    {
                        name = "Garten", biom = "wiese", breite = 30, tiefe = 30, objektDichte = 15
                    });
                    break;
                case "teich":
                    GeneriereWelt(new Modelle.WeltBeschreibung
                    {
                        name = "Teich", biom = "wiese", breite = 40, tiefe = 40, objektDichte = 10
                    });
                    if (wasserPrefab != null)
                        Instantiate(wasserPrefab, new Vector3(10, 0, 10), Quaternion.identity);
                    break;
                default:
                    GeneriereWelt(new Modelle.WeltBeschreibung
                    {
                        name = name, biom = "wiese", breite = 50, tiefe = 50, objektDichte = 20
                    });
                    break;
            }

            TryBuildNavMesh();
        }

        private void ErzeugeTerrain(int breite, int tiefe)
        {
            var mesh = new Mesh();
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();

            for (int z = 0; z <= tiefe; z++)
            {
                for (int x = 0; x <= breite; x++)
                {
                    float hoehe = Mathf.PerlinNoise(x * noiseScale, z * noiseScale) * terrainHoehe;
                    vertices.Add(new Vector3(x - breite / 2f, hoehe, z - tiefe / 2f));
                    uvs.Add(new Vector2((float)x / breite, (float)z / tiefe));
                }
            }

            for (int z = 0; z < tiefe; z++)
            {
                for (int x = 0; x < breite; x++)
                {
                    int i = z * (breite + 1) + x;
                    triangles.Add(i);
                    triangles.Add(i + breite + 1);
                    triangles.Add(i + 1);
                    triangles.Add(i + 1);
                    triangles.Add(i + breite + 1);
                    triangles.Add(i + breite + 2);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();

            var terrainObj = new GameObject("Terrain");
            terrainObj.AddComponent<MeshFilter>().mesh = mesh;
            var renderer = terrainObj.AddComponent<MeshRenderer>();
            if (terrainMaterial != null)
                renderer.material = terrainMaterial;
            terrainObj.AddComponent<MeshCollider>().sharedMesh = mesh;
            terrainObj.layer = LayerMask.NameToLayer("Default");
            terrainObj.transform.SetParent(ZielParent, true);
        }

        private void VerteilePrefabs(GameObject[] prefabs, int anzahl, int breite, int tiefe)
        {
            if (prefabs == null || prefabs.Length == 0) return;

            for (int i = 0; i < anzahl; i++)
            {
                var prefab = prefabs[Random.Range(0, prefabs.Length)];
                if (prefab == null) continue;

                float x = Random.Range(-breite / 2f, breite / 2f);
                float z = Random.Range(-tiefe / 2f, tiefe / 2f);

                if (Physics.Raycast(new Vector3(x, 100f, z), Vector3.down, out RaycastHit hit, 200f))
                {
                    Instantiate(prefab, hit.point, Quaternion.Euler(0, Random.Range(0, 360f), 0), ZielParent);
                }
                else
                {
                    Instantiate(prefab, new Vector3(x, 0, z), Quaternion.Euler(0, Random.Range(0, 360f), 0), ZielParent);
                }
            }
        }

        private void ErstelleRaum(float breite, float hoehe, float tiefe)
        {
            // Boden
            var boden = GameObject.CreatePrimitive(PrimitiveType.Cube);
            boden.name = "Boden";
            boden.transform.localScale = new Vector3(breite, 0.1f, tiefe);
            boden.transform.position = new Vector3(0, 0, 0);
            boden.transform.SetParent(ZielParent, true);

            // Waende
            ErstelleWand("Wand_Nord", new Vector3(0, hoehe / 2, tiefe / 2), new Vector3(breite, hoehe, 0.1f));
            ErstelleWand("Wand_Sued", new Vector3(0, hoehe / 2, -tiefe / 2), new Vector3(breite, hoehe, 0.1f));
            ErstelleWand("Wand_Ost", new Vector3(breite / 2, hoehe / 2, 0), new Vector3(0.1f, hoehe, tiefe));
            ErstelleWand("Wand_West", new Vector3(-breite / 2, hoehe / 2, 0), new Vector3(0.1f, hoehe, tiefe));
        }

        private void ErstelleWand(string name, Vector3 position, Vector3 scale)
        {
            var wand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wand.name = name;
            wand.transform.position = position;
            wand.transform.localScale = scale;
            wand.isStatic = true;
            wand.transform.SetParent(ZielParent, true);
        }

        private void TryBuildNavMesh()
        {
            if (navMeshSurface == null) return;

            MethodInfo buildMethod = navMeshSurface.GetType().GetMethod("BuildNavMesh", BindingFlags.Instance | BindingFlags.Public);
            if (buildMethod != null)
                buildMethod.Invoke(navMeshSurface, null);
            else
                Debug.LogWarning("[WeltGenerator] NavMeshSurface-Komponente ohne BuildNavMesh()-Methode.");
        }
    }
}
