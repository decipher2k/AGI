using System;
using System.Collections.Generic;
using BilligAGI.Modelle;
using BilligAGI.Daten;
using UnityEngine;

namespace BilligAGI.Kern
{
    public class EmotionsSystem
    {
        private readonly AGIConfig config;
        public EmotionalerZustand zustand { get; private set; }

        // Decay-Raten
        private float angstDecay;
        private float neugierDecay;
        private float frustrationDecay;
        private float zufriedenheitDecay;
        private float ueberraschungDecay;

        // Baselines
        private float angstBaseline;
        private float neugierBaseline;

        public EmotionsSystem(AGIConfig config)
        {
            this.config = config;
            zustand = new EmotionalerZustand();

            // Config auslesen
            angstDecay = config.emotionsDecayRate;
            neugierDecay = config.emotionsDecayRate * 0.5f; // Neugier persistent
            frustrationDecay = config.emotionsDecayRate * 1.2f;
            zufriedenheitDecay = config.emotionsDecayRate * 0.8f;
            ueberraschungDecay = config.emotionsDecayRate * 3f; // Schneller Decay

            angstBaseline = config.emotionsBaseline;
            neugierBaseline = config.emotionsBaseline + 0.1f; // Leicht neugierig als Default
        }

        public void Aktualisiere(Erfahrung erfahrung)
        {
            if (erfahrung == null) return;

            bool erfolg = erfahrung.belohnung > 0;
            bool ueberraschend = erfahrung.belohnung > 0.8f || erfahrung.belohnung < -0.8f;

            if (!erfolg)
            {
                zustand.angst = Mathf.Clamp01(zustand.angst + 0.1f);
                zustand.frustration = Mathf.Clamp01(zustand.frustration + 0.15f);
                zustand.zufriedenheit = Mathf.Max(0, zustand.zufriedenheit - 0.1f);
            }
            else
            {
                zustand.zufriedenheit = Mathf.Clamp01(zustand.zufriedenheit + 0.15f);
                zustand.angst = Mathf.Max(0, zustand.angst - 0.05f);
                zustand.frustration = Mathf.Max(0, zustand.frustration - 0.1f);
            }

            if (ueberraschend)
            {
                zustand.ueberraschung = Mathf.Clamp01(zustand.ueberraschung + 0.4f);
                zustand.neugier = Mathf.Clamp01(zustand.neugier + 0.2f);
            }

            // Neue Objekte/Bereiche erhoehen Neugier
            if (erfahrung.konzepte != null && erfahrung.konzepte.Contains("neu"))
                zustand.neugier = Mathf.Clamp01(zustand.neugier + 0.15f);

            // Vertrauen pro Domaene
            if (!string.IsNullOrEmpty(erfahrung.kontext))
            {
                string domain = erfahrung.kontext;
                if (!zustand.vertrauen.ContainsKey(domain))
                    zustand.vertrauen[domain] = 0.5f;

                if (erfolg)
                    zustand.vertrauen[domain] = Mathf.Clamp01(zustand.vertrauen[domain] + 0.05f);
                else
                    zustand.vertrauen[domain] = Mathf.Max(0, zustand.vertrauen[domain] - 0.08f);
            }
        }

        public void Tick()
        {
            // Alle Emotionen bewegen sich langsam Richtung Baseline
            zustand.angst = Lerp(zustand.angst, angstBaseline, angstDecay);
            zustand.neugier = Lerp(zustand.neugier, neugierBaseline, neugierDecay);
            zustand.frustration = Lerp(zustand.frustration, 0f, frustrationDecay);
            zustand.zufriedenheit = Lerp(zustand.zufriedenheit, config.emotionsBaseline, zufriedenheitDecay);
            zustand.ueberraschung = Lerp(zustand.ueberraschung, 0f, ueberraschungDecay);
        }

        public EmotionsModulation GetModulation()
        {
            return new EmotionsModulation
            {
                explorationsFaktor = Mathf.Clamp01(zustand.neugier - zustand.angst * 0.5f),
                vorsichtsFaktor = Mathf.Clamp01(zustand.angst + (1f - zustand.zufriedenheit) * 0.3f),
                kreativitaetsFaktor = Mathf.Clamp01(
                    (zustand.frustration + zustand.neugier) / 2f -
                    zustand.angst * 0.3f),
                lernPrioritaet = Mathf.Clamp01(
                    zustand.ueberraschung * 0.6f + zustand.neugier * 0.4f)
            };
        }

        public float GesamtValenz()
        {
            float positiv = zustand.zufriedenheit + zustand.neugier * 0.3f;
            float negativ = zustand.angst + zustand.frustration;
            return Mathf.Clamp(positiv - negativ, -1f, 1f);
        }

        public bool KritischerZustand()
        {
            return zustand.angst > 0.8f && zustand.frustration > 0.7f;
        }

        public void Persistiere()
        {
            DatenLader.Speichere("emotionen_zustand.json", zustand);
        }

        public void LadeZustand()
        {
            var geladen = DatenLader.Lade<EmotionalerZustand>("emotionen_zustand.json");
            if (geladen != null) zustand = geladen;
        }

        private float Lerp(float aktuell, float ziel, float rate)
        {
            return aktuell + (ziel - aktuell) * rate;
        }
    }
}
