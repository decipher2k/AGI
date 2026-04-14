using UnityEngine;

namespace BilligAGI
{
    public enum LLMAnbieter { Anthropic, OpenAI }

    [CreateAssetMenu(fileName = "AGIConfig", menuName = "BilligAGI/Config")]
    public class AGIConfig : ScriptableObject
    {
        [Header("LLM-Anbindung")]
        public LLMAnbieter llmAnbieter = LLMAnbieter.Anthropic;
        public string llmApiKey = "";
        public string llmModel = "claude-sonnet-4-20250514";
        public string llmApiUrl = "https://api.anthropic.com/v1/messages";
        public int maxTokensProAnfrage = 4096;

        [Header("Vektor-DB")]
        public string vektorDbUrl = "http://localhost:8000";
        public string vektorDbTyp = "chroma"; // "chroma" oder "qdrant"

        [Header("Physik & Sozial")]
        [Range(0f, 1f)] public float physikKonfidenzSchwelle = 0.7f;
        [Range(0f, 1f)] public float sozialKonfidenzSchwelle = 0.6f;

        [Header("Autonomer Modus")]
        public bool autonomerModus = false;
        public float zyklusIntervall = 2f;
        public float autonomModusTickRate = 2f;
        public int maxAutonomeSchritte = 100;

        [Header("VAKOG")]
        [Range(0f, 1f)] public float vakogSchwellwerte = 0.3f;

        [Header("KonzeptRevision (Hermeneutischer Zirkel)")]
        public int konzeptRevisionNachNAnwendungen = 10;
        public int konzeptRevisionMaxPasses = 7;
        [Range(0f, 1f)] public float konzeptDriftSchwelle = 0.3f;
        public int maxRueckpropagationsTiefe = 3;

        [Header("Emotionen")]
        [Range(0f, 1f)] public float emotionsDecayRate = 0.05f;
        [Range(0f, 1f)] public float emotionsSchwelle = 0.3f;
        [Range(0f, 1f)] public float emotionsBaseline = 0.2f;

        [Header("One-Shot-Lernen")]
        [Range(0f, 1f)] public float oneShotSchwelle = 0.8f;

        [Header("Narratives Selbst")]
        public int autobiographieKapitelLaenge = 20;

        [Header("Theory of Mind")]
        public int tomMaxEntitaeten = 10;

        [Header("LLM-Unabhaengigkeit")]
        public bool llmFallbackModusAktiv = true;
        [Range(0f, 1f)] public float llmUnabhaengigkeitsZielquote = 0.6f;

        [Header("Subsymbolik")]
        public bool subsymbolikAktiv = true;
        public int subsymbolikDim = 128;

        [Header("Kreativitaet")]
        [Range(0f, 1f)] public float kreativitaetNoveltySchwelle = 0.65f;
        [Range(0f, 1f)] public float kreativitaetUtilitySchwelle = 0.55f;
        public bool kreativitaetABTestAktiv = true;
        public int kreativitaetMaxVarianten = 5;

        [Header("Langzeitlernen")]
        [Range(0f, 0.1f)] public float forgettingRate = 0.01f;

        [Header("Konsistenzpruefung")]
        public int konsistenzPruefIntervall = 10;

        [Header("Robustheit")]
        public float apiRecoveryMaxSekunden = 120f;

        [Header("Sicherheit")]
        public int maxAutonomeSchritteProSitzung = 500;
        public bool notbremseAktiv = true;
    }
}
