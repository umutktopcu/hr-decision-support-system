const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');

const productionScript = path.resolve(
    __dirname,
    '../../../src/HrDecisionSupport.Web/wwwroot/js/job-requisition.js');
const { applyPreferredCompetencySuggestion } = require(productionScript);

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
        isRequired: false
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
        isRequired: true
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
