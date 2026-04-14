using UnityEngine;
using UnityEngine.UI;
using BilligAGI.Kern;

namespace BilligAGI.UI
{
    public class StatusOverlay : MonoBehaviour
    {
        [Header("Referenzen")]
        public AGIKern agiKern;

        [Header("VAKOG-Balken")]
        public Slider visuellBar;
        public Slider auditivBar;
        public Slider kinesthetischBar;
        public Slider olfaktorischBar;
        public Slider gustatorischBar;

        [Header("Emotions-Balken")]
        public Slider angstBar;
        public Slider neugierBar;
        public Slider frustrationBar;
        public Slider zufriedenheitBar;
        public Slider ueberraschungBar;

        [Header("Texte")]
        public Text modusText;
        public Text zielText;
        public Text planSchrittText;
        public Text letzteErfahrungText;
        public Text phaseText;

        [Header("Sichtbarkeit")]
        public GameObject overlayPanel;
        public KeyCode toggleKey = KeyCode.F1;

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey) && overlayPanel != null)
                overlayPanel.SetActive(!overlayPanel.activeSelf);

            if (agiKern == null || overlayPanel == null || !overlayPanel.activeSelf) return;

            AktualisiereAnzeige();
        }

        private void AktualisiereAnzeige()
        {
            // Emotionen
            var emo = agiKern.GetEmotionen()?.zustand;
            if (emo != null)
            {
                SetSlider(angstBar, emo.angst);
                SetSlider(neugierBar, emo.neugier);
                SetSlider(frustrationBar, emo.frustration);
                SetSlider(zufriedenheitBar, emo.zufriedenheit);
                SetSlider(ueberraschungBar, emo.ueberraschung);
            }

            // Modus
            if (modusText != null)
                modusText.text = $"Modus: {agiKern.GetModus()}";

            // Aktives Ziel
            var ziel = agiKern.GetZielManager()?.GetAktivesZiel();
            if (zielText != null)
                zielText.text = ziel != null ? $"Ziel: {ziel.beschreibung}" : "Kein Ziel";

            // Phase
            var phase = agiKern.GetNarativ()?.AktuellePhase();
            if (phaseText != null && phase != null)
                phaseText.text = $"Phase: {phase}";
        }

        private void SetSlider(Slider slider, float wert)
        {
            if (slider != null) slider.value = wert;
        }
    }
}
