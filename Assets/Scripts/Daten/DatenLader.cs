using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace BilligAGI.Daten
{
    /// <summary>
    /// Laedt und speichert JSON-Datendateien aus Assets/Data/.
    /// </summary>
    public static class DatenLader
    {
        private static string DataPath => Path.Combine(Application.streamingAssetsPath, "Data");

        public static T Lade<T>(string dateiName) where T : new()
        {
            string pfad = Path.Combine(DataPath, dateiName);
            if (!File.Exists(pfad))
            {
                Debug.LogWarning($"[DatenLader] Datei nicht gefunden: {pfad} — erzeuge Default.");
                return new T();
            }
            try
            {
                string json = File.ReadAllText(pfad);
                return JsonConvert.DeserializeObject<T>(json) ?? new T();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[DatenLader] Konnte JSON nicht laden ({pfad}): {ex.Message}. Verwende Default-Wert.");
                return new T();
            }
        }

        public static void Speichere<T>(string dateiName, T daten)
        {
            string pfad = Path.Combine(DataPath, dateiName);
            string verzeichnis = Path.GetDirectoryName(pfad);
            if (!Directory.Exists(verzeichnis))
                Directory.CreateDirectory(verzeichnis);
            string json = JsonConvert.SerializeObject(daten, Formatting.Indented);
            File.WriteAllText(pfad, json);
        }

        public static List<T> LadeListe<T>(string dateiName)
        {
            string pfad = Path.Combine(DataPath, dateiName);
            if (!File.Exists(pfad))
            {
                Debug.LogWarning($"[DatenLader] Datei nicht gefunden: {pfad}");
                return new List<T>();
            }
            string json = File.ReadAllText(pfad);
            return JsonConvert.DeserializeObject<List<T>>(json) ?? new List<T>();
        }

        public static Dictionary<string, T> LadeDict<T>(string dateiName)
        {
            string pfad = Path.Combine(DataPath, dateiName);
            if (!File.Exists(pfad))
            {
                Debug.LogWarning($"[DatenLader] Datei nicht gefunden: {pfad}");
                return new Dictionary<string, T>();
            }
            string json = File.ReadAllText(pfad);
            return JsonConvert.DeserializeObject<Dictionary<string, T>>(json)
                   ?? new Dictionary<string, T>();
        }
    }
}
