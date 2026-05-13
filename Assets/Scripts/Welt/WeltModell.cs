using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using BilligAGI.Modelle;

namespace BilligAGI.Welt
{
    public class WeltModell
    {
        public WeltZustand zustand;
        private const int MAX_HISTORIE = 100;
        private string persistenzPfad = "weltmodell.json";

        public WeltModell()
        {
            zustand = new WeltZustand();
            zustand.zeitstempel = DateTime.UtcNow.ToString("o");
        }

        public void RegistriereObjekt(GameObject obj)
        {
            if (obj == null) return;
            string id = obj.GetInstanceID().ToString();
            var pos = obj.transform.position;

            var weltObj = new WeltObjekt
            {
                id = id,
                name = obj.name,
                typ = obj.tag,
                position = new float[] { pos.x, pos.y, pos.z },
                rotation = new float[] {
                    obj.transform.eulerAngles.x,
                    obj.transform.eulerAngles.y,
                    obj.transform.eulerAngles.z
                },
                zustand = "normal"
            };

            // Tags sammeln
            weltObj.tags.Add(obj.tag);

            UebernehmePhysikParameter(obj, weltObj);

            zustand.objekte[id] = weltObj;
        }


        private void UebernehmePhysikParameter(GameObject obj, WeltObjekt weltObj)
        {
            var rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                weltObj.masseKg = Mathf.Max(0.001f, rb.mass);
                weltObj.luftWiderstand = Mathf.Max(0f, rb.drag);
                weltObj.geschwindigkeit = new float[] { rb.velocity.x, rb.velocity.y, rb.velocity.z };
            }

            var collider = obj.GetComponent<Collider>();
            var material = collider != null ? collider.sharedMaterial : null;
            if (material != null)
            {
                weltObj.reibung = Mathf.Clamp01((material.staticFriction + material.dynamicFriction) * 0.5f);
                weltObj.elastizitaet = Mathf.Clamp01(material.bounciness);
            }

            string name = obj.name.ToLowerInvariant();
            if (name.Contains("glas") || name.Contains("glass")) weltObj.bruchSchwelle = 8f;
            else if (name.Contains("papier") || name.Contains("paper")) weltObj.bruchSchwelle = 2f;
            else if (name.Contains("stein") || name.Contains("rock")) weltObj.bruchSchwelle = 200f;
            else if (rb != null) weltObj.bruchSchwelle = Mathf.Max(10f, rb.mass * 25f);
        }

        public void EntferneObjekt(string id)
        {
            if (zustand.objekte.ContainsKey(id))
            {
                ProtokollAenderung(id, "entfernt", "vorhanden", "entfernt");
                zustand.objekte.Remove(id);
            }
        }

        public void AktualisiereObjektPosition(GameObject obj)
        {
            string id = obj.GetInstanceID().ToString();
            if (zustand.objekte.TryGetValue(id, out var weltObj))
            {
                var pos = obj.transform.position;
                string vorher = $"[{weltObj.position[0]:F1},{weltObj.position[1]:F1},{weltObj.position[2]:F1}]";
                weltObj.position = new float[] { pos.x, pos.y, pos.z };
                var rb = obj.GetComponent<Rigidbody>();
                if (rb != null)
                    weltObj.geschwindigkeit = new float[] { rb.velocity.x, rb.velocity.y, rb.velocity.z };
                string nachher = $"[{pos.x:F1},{pos.y:F1},{pos.z:F1}]";
                ProtokollAenderung(id, "bewegt", vorher, nachher);
            }
        }

        public void AktualisiereRelationen()
        {
            // Fuer jedes Objekt: Relationen zu nahen Objekten berechnen
            var objekte = zustand.objekte.Values.ToList();
            foreach (var obj in objekte)
            {
                obj.relationen.Clear();
                foreach (var anderes in objekte)
                {
                    if (obj.id == anderes.id) continue;
                    float distanz = Distanz(obj.position, anderes.position);
                    if (distanz > 5f) continue;

                    // Ueber/unter
                    float hDiff = obj.position[1] - anderes.position[1];
                    if (hDiff > 0.3f)
                        obj.relationen["ueber_" + anderes.id] = anderes.name;
                    else if (hDiff < -0.3f)
                        obj.relationen["unter_" + anderes.id] = anderes.name;

                    // Neben
                    if (distanz < 2f && Mathf.Abs(hDiff) < 0.3f)
                        obj.relationen["neben_" + anderes.id] = anderes.name;
                }
            }
        }

        public float ErwartungVsRealitaet(WeltZustand erwartet, WeltZustand tatsaechlich)
        {
            if (erwartet == null || tatsaechlich == null) return 0f;
            int gesamt = 0, abweichungen = 0;

            foreach (var kvp in erwartet.objekte)
            {
                gesamt++;
                if (!tatsaechlich.objekte.TryGetValue(kvp.Key, out var tObj))
                {
                    abweichungen++;
                    continue;
                }
                if (Distanz(kvp.Value.position, tObj.position) > 1f)
                    abweichungen++;
                if (kvp.Value.zustand != tObj.zustand)
                    abweichungen++;
            }

            return gesamt > 0 ? (float)abweichungen / gesamt : 0f;
        }

        public List<WeltObjekt> ObjekteInReichweite(Vector3 pos, float radius)
        {
            var ergebnis = new List<WeltObjekt>();
            float[] p = { pos.x, pos.y, pos.z };
            foreach (var obj in zustand.objekte.Values)
            {
                if (Distanz(p, obj.position) <= radius)
                    ergebnis.Add(obj);
            }
            return ergebnis;
        }

        public WeltObjekt FindeObjektNachName(string name)
        {
            name = name.ToLowerInvariant();
            return zustand.objekte.Values.FirstOrDefault(o =>
                o.name.ToLowerInvariant().Contains(name));
        }

        public void Persistiere()
        {
            Daten.DatenLader.Speichere(persistenzPfad, zustand);
        }

        public void LadeVonDisk()
        {
            var geladen = Daten.DatenLader.Lade<WeltZustand>(persistenzPfad);
            if (geladen != null && geladen.objekte.Count > 0)
            {
                zustand = geladen;
                Debug.Log($"[WeltModell] {zustand.objekte.Count} Objekte geladen.");
            }
        }

        private void ProtokollAenderung(string objektId, string typ, string vorher, string nachher)
        {
            zustand.historie.Add(new WeltAenderung
            {
                objektId = objektId,
                aenderungsTyp = typ,
                vorher = vorher,
                nachher = nachher,
                zeitstempel = DateTime.UtcNow.ToString("o")
            });
            if (zustand.historie.Count > MAX_HISTORIE)
                zustand.historie.RemoveAt(0);
        }

        private static float Distanz(float[] a, float[] b)
        {
            if (a == null || b == null || a.Length < 3 || b.Length < 3) return float.MaxValue;
            float dx = a[0] - b[0], dy = a[1] - b[1], dz = a[2] - b[2];
            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
