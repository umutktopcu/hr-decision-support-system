const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');

const productionScript = path.resolve(
    __dirname,
    '../../../src/HrDecisionSupport.Web/wwwroot/js/job-requisition.js');
const {
    applyPreferredCompetencySuggestion,
    initPositionBenchmark
} = require(productionScript);

const skill = (competencyId, competencyName = 'C#') => ({
    competencyId,
    competencyName
});

test('absent suggestion is added exactly once as preferred', () => {
    const mandatory = [];
    const preferred = [];

    const changed = applyPreferredCompetencySuggestion(skill('skill-x'), mandatory, preferred);

    assert.equal(changed, true);
    assert.deepEqual(mandatory, []);
    assert.deepEqual(preferred, [{
        competencyId: 'skill-x',
        competencyName: 'C#',
        isRequired: false
    }]);
});

test('already preferred suggestion is not duplicated or rewritten', () => {
    const existing = {
        competencyId: 'skill-x',
        competencyName: 'Existing C#',
        isRequired: false,
        minimumExperienceMonths: 24,
        minimumProficiencyLevel: 4,
        notes: 'Persisted preferred details'
    };
    const preferred = [existing];

    const changed = applyPreferredCompetencySuggestion(skill('skill-x'), [], preferred);

    assert.equal(changed, false);
    assert.equal(preferred.length, 1);
    assert.strictEqual(preferred[0], existing);
});

test('already mandatory suggestion remains mandatory and is not added as preferred', () => {
    const existing = {
        competencyId: 'skill-x',
        competencyName: 'Mandatory C#',
        isRequired: true,
        minimumExperienceMonths: 36,
        minimumProficiencyLevel: 5,
        notes: 'Persisted mandatory details'
    };
    const mandatory = [existing];
    const preferred = [];

    const changed = applyPreferredCompetencySuggestion(skill('skill-x'), mandatory, preferred);

    assert.equal(changed, false);
    assert.equal(mandatory.length, 1);
    assert.strictEqual(mandatory[0], existing);
    assert.equal(mandatory[0].isRequired, true);
    assert.deepEqual(preferred, []);
});

test('stable identity allows equal names with different IDs and rejects repeated apply', () => {
    const mandatory = [];
    const preferred = [];
    const first = skill('skill-x', 'Same Name');
    const second = skill('skill-y', 'Same Name');

    assert.equal(applyPreferredCompetencySuggestion(first, mandatory, preferred), true);
    assert.equal(applyPreferredCompetencySuggestion(first, mandatory, preferred), false);
    assert.equal(applyPreferredCompetencySuggestion(second, mandatory, preferred), true);

    assert.equal(preferred.filter(item => item.competencyId === 'skill-x').length, 1);
    assert.equal(preferred.filter(item => item.competencyId === 'skill-y').length, 1);
    assert.equal(preferred.length, 2);
});

const classList = () => ({
    add() {},
    remove() {},
    toggle() {}
});

const applyButton = (attribute, value) => ({
    dataset: value === undefined ? {} : { [attribute]: value },
    disabled: false,
    textContent: '',
    classList: classList(),
    closest(selector) {
        const marker = `data-${attribute.replace(/[A-Z]/g, letter => `-${letter.toLowerCase()}`)}`;
        return selector.includes(marker) ? this : null;
    }
});

test('locked Edit guard blocks every benchmark mutation path', async () => {
    const mandatory = [];
    const preferred = [];
    const existingLanguage = {
        languageId: 'language-existing',
        languageName: 'Existing language',
        minimumProficiency: 3,
        proficiencyName: 'B1',
        hardFilterEnabled: true
    };
    const languages = [existingLanguage];
    const experienceInput = {
        value: '12',
        addEventListener() {}
    };
    const educationSelect = {
        value: '2',
        addEventListener() {}
    };
    const positionSelect = {
        value: 'position-x',
        listeners: {},
        addEventListener(name, handler) { this.listeners[name] = handler; }
    };
    const analyzeButton = {
        disabled: true,
        listeners: {},
        addEventListener(name, handler) { this.listeners[name] = handler; }
    };
    const skillButton = applyButton('benchmarkApplySkill', 'skill-x');
    const experienceButton = applyButton('benchmarkApplyExperience');
    const educationButton = applyButton('benchmarkApplyEducation');
    const existingLanguageButton = applyButton('benchmarkApplyLanguage', 'language-existing');
    const absentLanguageButton = applyButton('benchmarkApplyLanguage', 'language-absent');
    const applyButtons = [
        skillButton,
        experienceButton,
        educationButton,
        existingLanguageButton,
        absentLanguageButton
    ];
    const container = {
        innerHTML: '',
        classList: classList(),
        listeners: {},
        addEventListener(name, handler) { this.listeners[name] = handler; },
        querySelectorAll(selector) {
            return applyButtons.filter(button => button.closest(selector));
        },
        querySelector(selector) {
            return applyButtons.find(button => button.closest(selector)) || null;
        }
    };
    const originalDocument = global.document;
    const originalFetch = global.fetch;
    global.document = { addEventListener() {} };
    global.fetch = async () => ({
        ok: true,
        json: async () => ({
            positionName: 'Backend Developer',
            positionCode: 'BE',
            totalEmployees: 3,
            sampleSizeStatus: 3,
            benchmark: {
                skills: { items: [], coverage: { knownProfiles: 3, totalEmployees: 3 } },
                experience: { isSupported: false, coverage: { knownProfiles: 0, totalEmployees: 3 } },
                education: { distribution: [], coverage: { knownProfiles: 0, totalEmployees: 3 } },
                languages: { items: [], coverage: { knownProfiles: 0, totalEmployees: 3 } }
            },
            suggestions: {
                preferredCompetencies: [skill('skill-x')],
                minimumRelevantExperienceMonths: 24,
                minimumEducationLevel: 3,
                languages: [
                    { languageId: 'language-existing', languageName: 'Existing language', minimumProficiency: 4 },
                    { languageId: 'language-absent', languageName: 'Absent language', minimumProficiency: 4 }
                ]
            }
        })
    });

    try {
        initPositionBenchmark({
            positionSelectEl: positionSelect,
            analyzeButtonEl: analyzeButton,
            buttonTextEl: { textContent: '' },
            spinnerEl: { classList: classList() },
            containerEl: container,
            warningEl: { innerHTML: '', classList: classList() },
            experienceInputEl: experienceInput,
            educationSelectEl: educationSelect,
            mandatoryStore: mandatory,
            preferredStore: preferred,
            languageStore: languages,
            renderPreferredSkills: () => assert.fail('locked skill apply rendered a mutation'),
            renderLanguages: () => assert.fail('locked language apply rendered a mutation'),
            canApplySuggestions: () => false
        });

        await analyzeButton.listeners.click();
        for (const button of applyButtons) {
            container.listeners.click({ target: button });
        }

        assert.deepEqual(mandatory, []);
        assert.deepEqual(preferred, []);
        assert.equal(experienceInput.value, '12');
        assert.equal(educationSelect.value, '2');
        assert.equal(languages.length, 1);
        assert.strictEqual(languages[0], existingLanguage);
        assert.equal(existingLanguage.minimumProficiency, 3);
        assert.equal(existingLanguage.hardFilterEnabled, true);
        assert.ok(applyButtons.every(button => button.disabled));
    } finally {
        global.document = originalDocument;
        global.fetch = originalFetch;
    }
});
