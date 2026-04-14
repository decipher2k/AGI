using System;
using System.Collections.Generic;
using UnityEngine;

namespace BilligAGI.Physik
{
    /// <summary>
    /// Biologische Simulationen: Lotka-Volterra, SIR, logistisches Wachstum, Homoeostase, Mendel.
    /// Verwendet einfache numerische Methoden (Euler) statt MathNet.Numerics als Fallback.
    /// </summary>
    public class BioSimulation
    {
        // --- Lotka-Volterra (Raeuber-Beute) ---
        public (List<float> beute, List<float> raeuber) LotkaVolterra(
            float beute0, float raeuber0,
            float alpha, float beta, float gamma, float delta,
            float dt, int schritte)
        {
            var beuteListe = new List<float> { beute0 };
            var raeuberListe = new List<float> { raeuber0 };

            float b = beute0, r = raeuber0;
            for (int i = 0; i < schritte; i++)
            {
                float db = (alpha * b - beta * b * r) * dt;
                float dr = (delta * b * r - gamma * r) * dt;
                b = Mathf.Max(0, b + db);
                r = Mathf.Max(0, r + dr);
                beuteListe.Add(b);
                raeuberListe.Add(r);
            }

            return (beuteListe, raeuberListe);
        }

        // --- SIR-Modell (Epidemiologie) ---
        public (List<float> s, List<float> i, List<float> r) SIR(
            float s0, float i0, float r0,
            float beta, float gamma,
            float dt, int schritte)
        {
            var sL = new List<float> { s0 };
            var iL = new List<float> { i0 };
            var rL = new List<float> { r0 };
            float n = s0 + i0 + r0;

            float s = s0, inf = i0, rec = r0;
            for (int t = 0; t < schritte; t++)
            {
                float ds = -beta * s * inf / n * dt;
                float di = (beta * s * inf / n - gamma * inf) * dt;
                float dr = gamma * inf * dt;
                s += ds; inf += di; rec += dr;
                s = Mathf.Max(0, s); inf = Mathf.Max(0, inf); rec = Mathf.Max(0, rec);
                sL.Add(s); iL.Add(inf); rL.Add(rec);
            }

            return (sL, iL, rL);
        }

        // --- Logistisches Wachstum ---
        public List<float> LogistischesWachstum(
            float p0, float r, float k,
            float dt, int schritte)
        {
            var liste = new List<float> { p0 };
            float p = p0;
            for (int t = 0; t < schritte; t++)
            {
                float dp = r * p * (1 - p / k) * dt;
                p = Mathf.Max(0, p + dp);
                liste.Add(p);
            }
            return liste;
        }

        // --- Homoeostase ---
        public float HomoeostaseSchritt(float istWert, float sollWert, float regulationsRate, float stoerung)
        {
            float diff = sollWert - istWert;
            float korrektur = diff * regulationsRate;
            return istWert + korrektur + stoerung;
        }

        public List<float> HomoeostaseSimulation(
            float istWert, float sollWert, float rate,
            List<float> stoerungen, int schritte)
        {
            var ergebnisse = new List<float> { istWert };
            float w = istWert;
            for (int t = 0; t < schritte; t++)
            {
                float stoerung = (t < stoerungen.Count) ? stoerungen[t] : 0f;
                w = HomoeostaseSchritt(w, sollWert, rate, stoerung);
                ergebnisse.Add(w);
            }
            return ergebnisse;
        }

        // --- Mendel-Genetik (vereinfacht) ---
        public Dictionary<string, float> MendelKreuzung(string elter1, string elter2)
        {
            // Genotypen: AA, Aa, aa
            var allele1 = ElternAllele(elter1);
            var allele2 = ElternAllele(elter2);

            var ergebnis = new Dictionary<string, float>();
            int gesamt = allele1.Count * allele2.Count;

            foreach (var a1 in allele1)
            {
                foreach (var a2 in allele2)
                {
                    string genotyp = Sortiere(a1, a2);
                    if (ergebnis.ContainsKey(genotyp))
                        ergebnis[genotyp] += 1f / gesamt;
                    else
                        ergebnis[genotyp] = 1f / gesamt;
                }
            }

            return ergebnis;
        }

        private List<char> ElternAllele(string genotyp)
        {
            if (genotyp.Length != 2) return new List<char> { 'A', 'a' }; // Fallback Aa
            return new List<char> { genotyp[0], genotyp[1] };
        }

        private string Sortiere(char a, char b)
        {
            if (char.IsUpper(a)) return $"{a}{b}";
            if (char.IsUpper(b)) return $"{b}{a}";
            return $"{a}{b}";
        }
    }
}
