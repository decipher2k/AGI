using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace BilligAGI.Gedaechtnis
{
    public class VektorDB
    {
        private readonly string baseUrl;
        private readonly HttpClient client;
        private bool istVerbunden;

        // Lokaler Fallback: In-Memory + Disk
        private Dictionary<string, VektorEintrag> lokalerSpeicher;
        private string lokalPfad = "vektor_cache.json";

        public VektorDB(string baseUrl = "http://localhost:8000")
        {
            this.baseUrl = baseUrl;
            client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            lokalerSpeicher = new Dictionary<string, VektorEintrag>();
            istVerbunden = false;
        }

        public async Task Initialisiere()
        {
            try
            {
                var response = await client.GetAsync($"{baseUrl}/api/v1/heartbeat");
                istVerbunden = response.IsSuccessStatusCode;
            }
            catch
            {
                istVerbunden = false;
                Debug.Log("[VektorDB] Kein externer Server — verwende lokalen Speicher.");
                LadeLokal();
            }
        }

        public async Task Speichere(string id, float[] embedding, string collection,
            Dictionary<string, object> metadata = null)
        {
            var eintrag = new VektorEintrag
            {
                id = id,
                embedding = embedding,
                collection = collection,
                metadata = metadata ?? new Dictionary<string, object>(),
                zeitstempel = DateTime.UtcNow.ToString("o")
            };

            if (istVerbunden)
            {
                try
                {
                    var body = JsonConvert.SerializeObject(new
                    {
                        ids = new[] { id },
                        embeddings = new[] { embedding },
                        metadatas = new[] { metadata }
                    });
                    var content = new StringContent(body, Encoding.UTF8, "application/json");
                    await client.PostAsync($"{baseUrl}/api/v1/collections/{collection}/add", content);
                }
                catch
                {
                    Debug.LogWarning("[VektorDB] Speichern auf Server fehlgeschlagen, lokal speichern.");
                    lokalerSpeicher[$"{collection}:{id}"] = eintrag;
                }
            }
            else
            {
                lokalerSpeicher[$"{collection}:{id}"] = eintrag;
            }
        }

        public async Task<List<SuchErgebnis>> Suche(float[] queryEmbedding, string collection,
            int topK = 5, Dictionary<string, object> filter = null)
        {
            if (istVerbunden)
            {
                try
                {
                    var body = JsonConvert.SerializeObject(new
                    {
                        query_embeddings = new[] { queryEmbedding },
                        n_results = topK,
                        where = filter
                    });
                    var content = new StringContent(body, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(
                        $"{baseUrl}/api/v1/collections/{collection}/query", content);
                    var json = await response.Content.ReadAsStringAsync();
                    // Parse ChromaDB response
                    return ParseChromaResponse(json);
                }
                catch
                {
                    Debug.LogWarning("[VektorDB] Server-Suche fehlgeschlagen, lokal suchen.");
                }
            }

            // Lokale Cosine-Similarity
            return LokalesSuchen(queryEmbedding, collection, topK);
        }

        public void PersistiereLokal()
        {
            Daten.DatenLader.Speichere(lokalPfad, lokalerSpeicher);
        }

        private void LadeLokal()
        {
            var geladen = Daten.DatenLader.Lade<Dictionary<string, VektorEintrag>>(lokalPfad);
            if (geladen != null)
                lokalerSpeicher = geladen;
        }

        private List<SuchErgebnis> LokalesSuchen(float[] query, string collection, int topK)
        {
            var scored = new List<(string id, float score, Dictionary<string, object> meta)>();

            foreach (var kvp in lokalerSpeicher)
            {
                if (!kvp.Key.StartsWith(collection + ":")) continue;
                float sim = CosineSimilarity(query, kvp.Value.embedding);
                scored.Add((kvp.Value.id, sim, kvp.Value.metadata));
            }

            scored.Sort((a, b) => b.score.CompareTo(a.score));

            var ergebnis = new List<SuchErgebnis>();
            for (int i = 0; i < Mathf.Min(topK, scored.Count); i++)
            {
                ergebnis.Add(new SuchErgebnis
                {
                    id = scored[i].id,
                    score = scored[i].score,
                    metadata = scored[i].meta
                });
            }
            return ergebnis;
        }

        private float CosineSimilarity(float[] a, float[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return 0f;
            float dot = 0f, magA = 0f, magB = 0f;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                magA += a[i] * a[i];
                magB += b[i] * b[i];
            }
            float denom = Mathf.Sqrt(magA) * Mathf.Sqrt(magB);
            return denom > 0 ? dot / denom : 0f;
        }

        private List<SuchErgebnis> ParseChromaResponse(string json)
        {
            // Vereinfachtes Parsing — ChromaDB gibt verschachtelte Arrays zurueck
            try
            {
                var resp = JsonConvert.DeserializeObject<ChromaQueryResponse>(json);
                var ergebnis = new List<SuchErgebnis>();
                if (resp?.ids != null && resp.ids.Length > 0)
                {
                    for (int i = 0; i < resp.ids[0].Length; i++)
                    {
                        ergebnis.Add(new SuchErgebnis
                        {
                            id = resp.ids[0][i],
                            score = resp.distances != null && resp.distances.Length > 0
                                ? 1f - resp.distances[0][i] : 0f,
                            metadata = resp.metadatas?[0]?[i]
                        });
                    }
                }
                return ergebnis;
            }
            catch { return new List<SuchErgebnis>(); }
        }

        [Serializable]
        public class VektorEintrag
        {
            public string id;
            public float[] embedding;
            public string collection;
            public Dictionary<string, object> metadata;
            public string zeitstempel;
        }

        [Serializable]
        public class SuchErgebnis
        {
            public string id;
            public float score;
            public Dictionary<string, object> metadata;
        }

        [Serializable]
        private class ChromaQueryResponse
        {
            public string[][] ids;
            public float[][] distances;
            public Dictionary<string, object>[][] metadatas;
        }
    }
}
