using System;
using UnityEngine;

namespace BilligAGI.Kern
{
    public enum Betriebsmodus { Normal, Sparmodus, Lokalmodus, Recovery }

    public class SystemMetriken
    {
        public float apiLatenzMs;
        public float fehlerRate;          // 0-1, letzte N Anfragen
        public int aufeinanderfolgendeFehler;
        public float tokenBudgetVerbraucht; // 0-1
        public bool apiErreichbar;
        public DateTime letzteErfolgreicheAnfrage;
    }

    public class RobustheitsManager
    {
        private readonly AGIConfig config;
        private Betriebsmodus aktuellerModus = Betriebsmodus.Normal;
        private DateTime degradationStart;
        private int fehlZaehler;
        private float letzteFehlerRate;
        private DateTime letzterRecoveryVersuch;

        public Betriebsmodus AktuellerModus => aktuellerModus;

        public RobustheitsManager(AGIConfig config)
        {
            this.config = config;
        }

        public Betriebsmodus BestimmeModus(SystemMetriken m)
        {
            if (!m.apiErreichbar || m.aufeinanderfolgendeFehler >= 3)
            {
                if (aktuellerModus < Betriebsmodus.Lokalmodus)
                {
                    aktuellerModus = Betriebsmodus.Lokalmodus;
                    degradationStart = DateTime.UtcNow;
                    Debug.LogWarning("[Robustheit] → LOKALMODUS: API nicht erreichbar");
                }
            }
            else if (m.fehlerRate > 0.3f || m.apiLatenzMs > 10000f)
            {
                if (aktuellerModus < Betriebsmodus.Sparmodus)
                {
                    aktuellerModus = Betriebsmodus.Sparmodus;
                    Debug.LogWarning($"[Robustheit] → SPARMODUS: Fehlerrate={m.fehlerRate:P0}, Latenz={m.apiLatenzMs:F0}ms");
                }
            }
            else if (m.tokenBudgetVerbraucht > 0.9f)
            {
                if (aktuellerModus < Betriebsmodus.Sparmodus)
                {
                    aktuellerModus = Betriebsmodus.Sparmodus;
                    Debug.LogWarning("[Robustheit] → SPARMODUS: Token-Budget fast erschoepft");
                }
            }
            else if (aktuellerModus != Betriebsmodus.Normal && m.apiErreichbar && m.fehlerRate < 0.1f)
            {
                // Recovery pruefen
                aktuellerModus = Betriebsmodus.Normal;
                Debug.Log("[Robustheit] → NORMALBETRIEB wiederhergestellt");
            }

            // Notbremse
            if (aktuellerModus >= Betriebsmodus.Lokalmodus)
            {
                float degradationsDauer = (float)(DateTime.UtcNow - degradationStart).TotalSeconds;
                if (degradationsDauer > config.apiRecoveryMaxSekunden)
                {
                    aktuellerModus = Betriebsmodus.Recovery;
                    Debug.LogError($"[Robustheit] → RECOVERY: Degradation seit {degradationsDauer:F0}s");
                }
            }

            return aktuellerModus;
        }

        public void RecoveryTick(bool apiTest)
        {
            if (aktuellerModus != Betriebsmodus.Recovery && aktuellerModus != Betriebsmodus.Lokalmodus)
                return;

            if (apiTest)
            {
                fehlZaehler = 0;
                aktuellerModus = Betriebsmodus.Sparmodus; // Erstmal Sparmodus, nicht direkt Normal
                Debug.Log("[Robustheit] API wieder erreichbar → SPARMODUS (Stabilisierung)");
            }
            else
            {
                fehlZaehler++;
                Debug.LogWarning($"[Robustheit] Recovery-Versuch {fehlZaehler} fehlgeschlagen");
            }
        }

        public void MeldeErfolg()
        {
            fehlZaehler = 0;
            letzteFehlerRate = Math.Max(0f, letzteFehlerRate - 0.05f);
        }

        public void MeldeFehler()
        {
            fehlZaehler++;
            letzteFehlerRate = Math.Min(1f, letzteFehlerRate + 0.1f);
        }

        public bool SollLLMGenutztWerden()
        {
            switch (aktuellerModus)
            {
                case Betriebsmodus.Normal: return true;
                case Betriebsmodus.Sparmodus: return true; // Aber mit reduzierten Prompts
                case Betriebsmodus.Lokalmodus: return false;
                case Betriebsmodus.Recovery: return false;
                default: return true;
            }
        }

        public int MaxTokensImAktuellenModus()
        {
            switch (aktuellerModus)
            {
                case Betriebsmodus.Normal: return config.maxTokensProAnfrage;
                case Betriebsmodus.Sparmodus: return config.maxTokensProAnfrage / 2;
                default: return 0;
            }
        }

        public string StatusBericht()
        {
            return $"Modus: {aktuellerModus}, Fehler: {fehlZaehler}, FehlerRate: {letzteFehlerRate:P0}";
        }

        public Betriebsmodus GetAktuellerModus() => aktuellerModus;
    }
}
