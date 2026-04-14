using UnityEngine;

namespace BilligAGI.Bio
{
    public class PflanzenWachstum : MonoBehaviour
    {
        [Header("Wachstum")]
        public float wachstumsRate = 0.01f; // Pro Sekunde
        public float maxScale = 3f;
        public float aktuellerScale = 0.3f;

        [Header("Bedingungen")]
        public float wasserBedarf = 0.3f;  // 0-1
        public float lichtBedarf = 0.3f;   // 0-1

        [Header("Phasen-Prefabs")]
        public GameObject setzlingPrefab;
        public GameObject pflanzePrefab;
        public GameObject baumPrefab;

        private float aktuellesWasser;
        private float aktuellesLicht;
        private int aktuellePhase; // 0=Setzling, 1=Pflanze, 2=Baum

        private void Update()
        {
            // Wachstum basierend auf Bedingungen
            if (aktuellesWasser >= wasserBedarf && aktuellesLicht >= lichtBedarf)
            {
                float wachstum = wachstumsRate * Time.deltaTime *
                                 (aktuellesWasser + aktuellesLicht) / 2f;
                aktuellerScale = Mathf.Min(maxScale, aktuellerScale + wachstum);
                transform.localScale = Vector3.one * aktuellerScale;
            }

            // Phasenwechsel
            if (aktuellerScale > maxScale * 0.66f && aktuellePhase < 2)
            {
                WechslePhase(2);
            }
            else if (aktuellerScale > maxScale * 0.33f && aktuellePhase < 1)
            {
                WechslePhase(1);
            }
        }

        public void SetzeUmgebung(float wasser, float licht)
        {
            aktuellesWasser = Mathf.Clamp01(wasser);
            aktuellesLicht = Mathf.Clamp01(licht);
        }

        public void SetzeVonWetter(WetterSystem wetter, Light sonne)
        {
            if (wetter != null)
            {
                aktuellesWasser = wetter.AktuellesWetter == Modelle.WetterTyp.Regen
                    ? wetter.AktuelleIntensitaet : 0.1f;
            }
            if (sonne != null)
            {
                aktuellesLicht = Mathf.Clamp01(sonne.intensity / 1.5f);
            }
        }

        public (float scale, int phase, float wasser, float licht) GetStatus()
        {
            return (aktuellerScale, aktuellePhase, aktuellesWasser, aktuellesLicht);
        }

        private void WechslePhase(int neuePhase)
        {
            aktuellePhase = neuePhase;
            Debug.Log($"[Pflanze] {gameObject.name} → Phase {neuePhase} (Scale: {aktuellerScale:F2})");

            // Prefab-Swap (optional)
            // In der Praxis: Mesh/Material wechseln statt Objekt ersetzen
        }
    }
}
