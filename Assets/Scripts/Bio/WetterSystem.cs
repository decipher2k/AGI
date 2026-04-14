using UnityEngine;
using BilligAGI.Modelle;

namespace BilligAGI.Bio
{
    public class WetterSystem : MonoBehaviour
    {
        [Header("Partikel")]
        public ParticleSystem regenPartikel;
        public ParticleSystem schneePartikel;

        [Header("Wind")]
        public WindZone windZone;

        [Header("Licht & Nebel")]
        public Light sonne;

        private WetterTyp aktuellesWetter = WetterTyp.Klar;
        private float aktuelleIntensitaet = 0.5f;

        public WetterTyp AktuellesWetter => aktuellesWetter;
        public float AktuelleIntensitaet => aktuelleIntensitaet;

        public void SetzeWetter(WetterTyp typ, float intensitaet)
        {
            aktuellesWetter = typ;
            aktuelleIntensitaet = Mathf.Clamp01(intensitaet);

            // Alle Partikel stoppen
            StoppeAllePartikel();

            switch (typ)
            {
                case WetterTyp.Regen:
                    if (regenPartikel != null)
                    {
                        var emission = regenPartikel.emission;
                        emission.rateOverTime = 100f * intensitaet;
                        regenPartikel.Play();
                    }
                    RenderSettings.fogDensity = 0.01f * intensitaet;
                    break;

                case WetterTyp.Schnee:
                    if (schneePartikel != null)
                    {
                        var emission = schneePartikel.emission;
                        emission.rateOverTime = 80f * intensitaet;
                        schneePartikel.Play();
                    }
                    break;

                case WetterTyp.Nebel:
                    RenderSettings.fog = true;
                    RenderSettings.fogDensity = 0.05f * intensitaet;
                    break;

                case WetterTyp.Sturm:
                    if (regenPartikel != null)
                    {
                        var emission = regenPartikel.emission;
                        emission.rateOverTime = 200f * intensitaet;
                        regenPartikel.Play();
                    }
                    if (windZone != null)
                    {
                        windZone.windMain = 5f * intensitaet;
                        windZone.windTurbulence = 3f * intensitaet;
                    }
                    break;

                case WetterTyp.Klar:
                default:
                    RenderSettings.fog = false;
                    if (windZone != null)
                    {
                        windZone.windMain = 0.5f;
                        windZone.windTurbulence = 0.3f;
                    }
                    break;
            }

            Debug.Log($"[Wetter] {typ} (Intensitaet: {intensitaet:F2})");
        }

        // Sensorauswirkungen
        public float SichtweiteModifikator()
        {
            switch (aktuellesWetter)
            {
                case WetterTyp.Nebel: return 0.3f;
                case WetterTyp.Regen: return 0.7f;
                case WetterTyp.Sturm: return 0.4f;
                case WetterTyp.Schnee: return 0.6f;
                default: return 1f;
            }
        }

        public float LautstaerkeModifikator()
        {
            switch (aktuellesWetter)
            {
                case WetterTyp.Regen: return 0.5f + aktuelleIntensitaet * 0.3f;
                case WetterTyp.Sturm: return 0.8f;
                default: return 0.1f;
            }
        }

        public float GeruchsModifikator()
        {
            return aktuellesWetter == WetterTyp.Regen ? 0.7f : 0.2f;
        }

        private void StoppeAllePartikel()
        {
            if (regenPartikel != null) regenPartikel.Stop();
            if (schneePartikel != null) schneePartikel.Stop();
            RenderSettings.fog = false;
        }
    }
}
