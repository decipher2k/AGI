using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BilligAGI.Kern
{
    // ============================================================
    //  EWCSchutz — Elastic Weight Consolidation
    //
    //  Schuetzt das DQN vor Catastrophic Forgetting:
    //  Nach jeder "Task-Phase" (z.B. TrainingsPhase-Wechsel oder
    //  manueller Konsolidierung) werden die aktuellen Gewichte +
    //  die Fisher Information Matrix gespeichert.
    //
    //  Beim naechsten Lernen wird ein Penalty-Term addiert, der
    //  wichtige Gewichte (hohe Fisher-Werte) vor Aenderung schuetzt.
    //
    //  Loss_gesamt = Loss_task + (lambda/2) * Σ F_i * (θ_i - θ*_i)²
    //
    //  Billig-Implementierung: Fisher wird ueber Replay-Buffer
    //  approximiert (empirische FIM via Gradienten-Quadrate).
    // ============================================================

    [Serializable]
    public class EWCSnapshot
    {
        public string phasenName;
        public string zeitstempel;
        public float[] gewichte;          // Alle Gewichte θ* flach
        public float[] fisherDiagonale;   // Diagonale der FIM
    }

    public class EWCSchutz
    {
        private List<EWCSnapshot> snapshots = new();
        private float lambda;             // Staerke des EWC-Penalties
        private const int MAX_SNAPSHOTS = 5;  // Letzte N Phasen behalten

        public EWCSchutz(float lambda = 400f)
        {
            this.lambda = lambda;
        }

        // ===========================================================
        //  1. SNAPSHOT ERSTELLEN: Nach Task/Phase-Abschluss
        // ===========================================================

        /// <summary>
        /// Erstellt einen EWC-Snapshot: Speichert aktuelle Gewichte +
        /// berechnet Fisher Information aus den letzten Replay-Transitions.
        /// </summary>
        public void ErstelleSnapshot(
            string phasenName,
            float[,] w1, float[] b1,
            float[,] w2, float[] b2,
            float[,] w3, float[] b3,
            List<RLTransition> replayBuffer,
            int inputDim, int hidden1, int hidden2, int outputDim)
        {
            // Gewichte flach ablegen
            float[] gewichte = FlattenAlleGewichte(w1, b1, w2, b2, w3, b3);

            // Fisher Information approximieren
            float[] fisher = BerechneFisherDiagonale(
                w1, b1, w2, b2, w3, b3,
                replayBuffer,
                inputDim, hidden1, hidden2, outputDim);

            var snapshot = new EWCSnapshot
            {
                phasenName = phasenName,
                zeitstempel = DateTime.UtcNow.ToString("o"),
                gewichte = gewichte,
                fisherDiagonale = fisher
            };

            snapshots.Add(snapshot);

            // Maximal N Snapshots behalten
            while (snapshots.Count > MAX_SNAPSHOTS)
                snapshots.RemoveAt(0);

            Debug.Log($"[EWC] Snapshot '{phasenName}' erstellt. " +
                $"Fisher-Max: {fisher.Max():F4}, Fisher-Avg: {fisher.Average():F6}. " +
                $"Snapshots: {snapshots.Count}");
        }

        // ===========================================================
        //  2. EWC-PENALTY BERECHNEN: Pro Gewicht
        // ===========================================================

        /// <summary>
        /// Berechnet den EWC-Gradienten-Penalty fuer jedes Gewicht.
        /// Wird waehrend des Backprop-Updates von DQNLerner aufgerufen.
        /// Gibt einen Vektor zurueck der zum Gradienten SUBTRAHIERT wird.
        /// </summary>
        public float[] BerechneGradientenPenalty(
            float[,] w1, float[] b1,
            float[,] w2, float[] b2,
            float[,] w3, float[] b3)
        {
            if (snapshots.Count == 0)
                return null;

            float[] aktuelleGewichte = FlattenAlleGewichte(w1, b1, w2, b2, w3, b3);
            float[] penalty = new float[aktuelleGewichte.Length];

            foreach (var snap in snapshots)
            {
                if (snap.gewichte.Length != aktuelleGewichte.Length) continue;

                for (int i = 0; i < aktuelleGewichte.Length; i++)
                {
                    // Gradient des EWC-Terms: lambda * F_i * (theta_i - theta*_i)
                    float diff = aktuelleGewichte[i] - snap.gewichte[i];
                    penalty[i] += lambda * snap.fisherDiagonale[i] * diff;
                }
            }

            // Durch Anzahl Snapshots mitteln (Online-EWC)
            float faktor = 1f / snapshots.Count;
            for (int i = 0; i < penalty.Length; i++)
                penalty[i] *= faktor;

            return penalty;
        }

        /// <summary>
        /// Wendet den EWC-Penalty auf einzelne Gewichtsmatrizen an.
        /// Gibt Penalty-Arrays fuer jede Matrix zurueck.
        /// </summary>
        public void BerechneSchichtPenalties(
            float[,] w1, float[] b1,
            float[,] w2, float[] b2,
            float[,] w3, float[] b3,
            out float[,] pw1, out float[] pb1,
            out float[,] pw2, out float[] pb2,
            out float[,] pw3, out float[] pb3)
        {
            int h1 = w1.GetLength(0), inp = w1.GetLength(1);
            int h2 = w2.GetLength(0), h1c = w2.GetLength(1);
            int od = w3.GetLength(0), h2c = w3.GetLength(1);

            pw1 = new float[h1, inp]; pb1 = new float[h1];
            pw2 = new float[h2, h1c]; pb2 = new float[h2];
            pw3 = new float[od, h2c]; pb3 = new float[od];

            if (snapshots.Count == 0) return;

            float[] penalty = BerechneGradientenPenalty(w1, b1, w2, b2, w3, b3);
            if (penalty == null) return;

            // Penalty zurueck auf Matrizen aufteilen
            int idx = 0;
            for (int i = 0; i < h1; i++)
                for (int j = 0; j < inp; j++)
                    pw1[i, j] = penalty[idx++];
            for (int i = 0; i < h1; i++)
                pb1[i] = penalty[idx++];
            for (int i = 0; i < h2; i++)
                for (int j = 0; j < h1c; j++)
                    pw2[i, j] = penalty[idx++];
            for (int i = 0; i < h2; i++)
                pb2[i] = penalty[idx++];
            for (int i = 0; i < od; i++)
                for (int j = 0; j < h2c; j++)
                    pw3[i, j] = penalty[idx++];
            for (int i = 0; i < od; i++)
                pb3[i] = penalty[idx++];
        }

        // ===========================================================
        //  3. FISHER INFORMATION BERECHNUNG
        // ===========================================================

        /// <summary>
        /// Empirische Fisher Information Matrix (Diagonale) via Gradienten-Quadrate.
        /// Approximiert ueber eine Stichprobe aus dem Replay-Buffer.
        /// F_i ≈ (1/N) * Σ (∂L/∂θ_i)²
        /// </summary>
        private float[] BerechneFisherDiagonale(
            float[,] w1, float[] b1,
            float[,] w2, float[] b2,
            float[,] w3, float[] b3,
            List<RLTransition> replay,
            int inputDim, int hidden1, int hidden2, int outputDim)
        {
            int gesamtGewichte = GesamtGewichteAnzahl(w1, b1, w2, b2, w3, b3);
            float[] fisherSumme = new float[gesamtGewichte];

            // Stichprobe: maximal 200 Transitions
            int n = Math.Min(200, replay.Count);
            if (n == 0)
            {
                // Kein Replay → einheitliche Fisher (leichter Schutz ueberall)
                for (int i = 0; i < gesamtGewichte; i++)
                    fisherSumme[i] = 0.01f;
                return fisherSumme;
            }

            var rng = new System.Random(42);
            var indices = Enumerable.Range(0, replay.Count)
                .OrderBy(_ => rng.Next())
                .Take(n).ToList();

            foreach (int idx in indices)
            {
                var t = replay[idx];

                // Forward mit Zwischenwerten
                float[] h1_pre = new float[hidden1];
                float[] h1_act = new float[hidden1];
                for (int i = 0; i < hidden1; i++)
                {
                    float sum = b1[i];
                    for (int j = 0; j < inputDim; j++)
                        sum += w1[i, j] * t.zustandVorher[j];
                    h1_pre[i] = sum;
                    h1_act[i] = sum > 0f ? sum : 0f;
                }

                float[] h2_pre = new float[hidden2];
                float[] h2_act = new float[hidden2];
                for (int i = 0; i < hidden2; i++)
                {
                    float sum = b2[i];
                    for (int j = 0; j < hidden1; j++)
                        sum += w2[i, j] * h1_act[j];
                    h2_pre[i] = sum;
                    h2_act[i] = sum > 0f ? sum : 0f;
                }

                float[] output = new float[outputDim];
                for (int i = 0; i < outputDim; i++)
                {
                    float sum = b3[i];
                    for (int j = 0; j < hidden2; j++)
                        sum += w3[i, j] * h2_act[j];
                    output[i] = sum;
                }

                // Log-Likelihood Gradient (fuer die gewaehlte Aktion)
                int aktionsIdx = (int)t.aktion;
                float[] dOut = new float[outputDim];
                // Softmax-Gradient-Approx: einfach den Q-Fehler nutzen
                float qTarget = t.belohnung; // vereinfacht (keine Diskontierung noetig fuer Fisher)
                dOut[aktionsIdx] = output[aktionsIdx] - qTarget;

                // Backprop: Gradienten berechnen (gleiche Logik wie DQN)
                float[] dH2 = new float[hidden2];
                for (int j = 0; j < hidden2; j++)
                {
                    float grad = 0f;
                    for (int i = 0; i < outputDim; i++)
                        grad += dOut[i] * w3[i, j];
                    dH2[j] = h2_pre[j] > 0f ? grad : 0f;
                }

                float[] dH1 = new float[hidden1];
                for (int j = 0; j < hidden1; j++)
                {
                    float grad = 0f;
                    for (int i = 0; i < hidden2; i++)
                        grad += dH2[i] * w2[i, j];
                    dH1[j] = h1_pre[j] > 0f ? grad : 0f;
                }

                // Gradienten-Quadrate akkumulieren (= empirische Fisher-Diagonale)
                int gIdx = 0;

                // w1 Gradienten²
                for (int i = 0; i < hidden1; i++)
                    for (int j = 0; j < inputDim; j++)
                    {
                        float g = dH1[i] * t.zustandVorher[j];
                        fisherSumme[gIdx++] += g * g;
                    }
                // b1
                for (int i = 0; i < hidden1; i++)
                {
                    float g = dH1[i];
                    fisherSumme[gIdx++] += g * g;
                }
                // w2
                for (int i = 0; i < hidden2; i++)
                    for (int j = 0; j < hidden1; j++)
                    {
                        float g = dH2[i] * h1_act[j];
                        fisherSumme[gIdx++] += g * g;
                    }
                // b2
                for (int i = 0; i < hidden2; i++)
                {
                    float g = dH2[i];
                    fisherSumme[gIdx++] += g * g;
                }
                // w3
                for (int i = 0; i < outputDim; i++)
                    for (int j = 0; j < hidden2; j++)
                    {
                        float g = dOut[i] * h2_act[j];
                        fisherSumme[gIdx++] += g * g;
                    }
                // b3
                for (int i = 0; i < outputDim; i++)
                {
                    float g = dOut[i];
                    fisherSumme[gIdx++] += g * g;
                }
            }

            // Mitteln + Clipping
            for (int i = 0; i < gesamtGewichte; i++)
            {
                fisherSumme[i] /= n;
                // Clipping: Fisher-Werte nicht zu gross werden lassen
                fisherSumme[i] = Math.Min(fisherSumme[i], 100f);
            }

            return fisherSumme;
        }

        // ===========================================================
        //  4. STATUS + API
        // ===========================================================

        public int SnapshotAnzahl => snapshots.Count;
        public float Lambda => lambda;

        public void SetzeLambda(float neuerLambda) =>
            lambda = Math.Max(0f, neuerLambda);

        public string GetStatusText()
        {
            if (snapshots.Count == 0)
                return "EWC: Kein Snapshot vorhanden. Noch kein Vergessensschutz aktiv.";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"EWC: {snapshots.Count} Snapshots, Lambda: {lambda:F0}");
            foreach (var snap in snapshots)
            {
                float maxF = snap.fisherDiagonale.Max();
                float avgF = snap.fisherDiagonale.Average();
                sb.AppendLine($"  [{snap.phasenName}] Gewichte: {snap.gewichte.Length}, " +
                    $"Fisher max: {maxF:F4}, avg: {avgF:F6}");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Gibt die Gesamtgroesse aller abgesicherten Gewichte zurueck.
        /// </summary>
        public int GeschuetzteGewichte()
        {
            if (snapshots.Count == 0) return 0;
            return snapshots[0].gewichte.Length;
        }

        // ===========================================================
        //  PRIVATE: Hilfsfunktionen
        // ===========================================================

        private float[] FlattenAlleGewichte(
            float[,] w1, float[] b1,
            float[,] w2, float[] b2,
            float[,] w3, float[] b3)
        {
            int total = GesamtGewichteAnzahl(w1, b1, w2, b2, w3, b3);
            float[] flat = new float[total];
            int idx = 0;

            foreach (float v in w1) flat[idx++] = v;
            foreach (float v in b1) flat[idx++] = v;
            foreach (float v in w2) flat[idx++] = v;
            foreach (float v in b2) flat[idx++] = v;
            foreach (float v in w3) flat[idx++] = v;
            foreach (float v in b3) flat[idx++] = v;

            return flat;
        }

        private int GesamtGewichteAnzahl(
            float[,] w1, float[] b1,
            float[,] w2, float[] b2,
            float[,] w3, float[] b3)
        {
            return w1.Length + b1.Length + w2.Length + b2.Length + w3.Length + b3.Length;
        }
    }
}
