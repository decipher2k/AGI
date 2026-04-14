using System.Collections.Generic;
using System.Threading.Tasks;
using BilligAGI.Modelle;
using BilligAGI.Kern;

namespace BilligAGI.Sozial
{
    public class SozialEngine
    {
        private readonly Mechanismen mechanismen;
        private readonly ArchetypenEngine archetypenEngine;
        private readonly ArchetypenGedaechtnis archetypenGedaechtnis;
        private readonly Alchemie alchemie;
        private readonly TheoryOfMind tom;
        private readonly LLMAdapter llm;

        public SozialEngine(LLMAdapter llm, KonzeptRevision konzeptRevision = null)
        {
            this.llm = llm;
            mechanismen = new Mechanismen();
            archetypenGedaechtnis = new ArchetypenGedaechtnis();
            archetypenEngine = new ArchetypenEngine(archetypenGedaechtnis, llm, konzeptRevision);
            alchemie = new Alchemie();
            tom = new TheoryOfMind(llm);
        }

        public Alchemie GetAlchemie() => alchemie;

        public async Task<SozialeAnalyse> Analysiere(
            string text, string situation,
            List<Erfahrung> verlauf,
            SensorDaten sensorDaten = null)
        {
            var analyse = new SozialeAnalyse
            {
                zeitstempel = System.DateTime.UtcNow.ToString("o"),
            };

            // 1. Mechanismen erkennen
            analyse.erkannteMechanismen = mechanismen.Erkenne(text, analyse);

            // 2. Archetypen kontextuell erkennen — erzeugt episodische Instanzen
            var instanzen = await archetypenEngine.ErkenneAlleArchetypen(situation, verlauf);
            analyse.aktiveArchetypen = new List<string>();
            foreach (var inst in instanzen)
                analyse.aktiveArchetypen.Add(inst.archetypName);

            // 3. Alchemische Phase
            var phase = alchemie.ErkennePhase(situation, verlauf);
            analyse.alchemischePhase = phase.ToString();
            analyse.transformationsImpuls = alchemie.TransformationsImpuls(phase, situation);

            // 4. Theory of Mind — alle bekannten Entitaeten
            analyse.tomVorhersagen = new Dictionary<string, string>();
            foreach (var eid in tom.AlleEntitaeten())
            {
                var vorhersage = await tom.VorhersageVerhalten(eid, situation);
                analyse.tomVorhersagen[eid] = vorhersage;
            }

            return analyse;
        }

        public Mechanismen GetMechanismen() => mechanismen;
        public ArchetypenEngine GetArchetypenEngine() => archetypenEngine;
        public ArchetypenGedaechtnis GetArchetypenGedaechtnis() => archetypenGedaechtnis;
        public TheoryOfMind GetTheoryOfMind() => tom;
    }
}
