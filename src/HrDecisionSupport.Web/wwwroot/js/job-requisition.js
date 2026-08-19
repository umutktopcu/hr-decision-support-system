/**
 * job-requisition.js
 * Shared helpers for Job Requisition and Matching UI.
 * Uses native fetch. No jQuery dependency for API calls.
 */

// ──────────────────────────────────────────────
// API helpers
// ──────────────────────────────────────────────

/**
 * Extracts a human-readable error message from a backend API response.
 * Handles { errors: [...] }, { message: '' }, and plain text.
 */
async function parseApiError(response) {
    let body = null;
    try { body = await response.clone().json(); } catch (_) { body = null; }

    if (!body) {
        try { return await response.text(); } catch (_) { return `HTTP ${response.status}`; }
    }

    // Validation error shape: { errors: [ { code, message } ] }
    if (body.errors && Array.isArray(body.errors) && body.errors.length > 0) {
        return body.errors.map(e => e.message || e.code || JSON.stringify(e)).join('\n');
    }
    // Single error shape: { code, message }
    if (body.message) return body.message;
    if (body.code)    return `${body.code}: ${body.message || ''}`;

    return `HTTP ${response.status}`;
}

/**
 * Show a Bootstrap dismissible alert inside container element.
 * @param {HTMLElement} el - container element
 * @param {string} html - message (plain text or safe HTML)
 * @param {'danger'|'warning'|'success'|'info'} type
 */
function showAlert(el, html, type = 'danger') {
    el.innerHTML = `
        <div class="alert alert-${type} alert-dismissible fade show" role="alert">
            <span style="white-space:pre-line">${escHtml(html)}</span>
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Kapat"></button>
        </div>`;
}

function clearAlert(el) { el.innerHTML = ''; }

function escHtml(str) {
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

// ──────────────────────────────────────────────
// Formatting helpers
// ──────────────────────────────────────────────

function fmtPct(decimal) {
    if (decimal == null) return '—';
    return `${Math.round(decimal * 100)}%`;
}

function fmtScore(val, decimals = 4) {
    if (val == null) return '—';
    return Number(val).toFixed(decimals);
}

function fmtDate(isoStr) {
    if (!isoStr) return '—';
    // DateOnly serialised as "YYYY-MM-DD"
    return isoStr.substring(0, 10);
}

function fmtDateTime(isoStr) {
    if (!isoStr) return '—';
    const d = new Date(isoStr);
    return d.toLocaleDateString('tr-TR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
}

/**
 * Status badge HTML for JobRequisitionStatus integer.
 * Values: Draft=1, Open=2, OnHold=3, Closed=4, Cancelled=5
 */
function statusBadge(statusInt) {
    const map = {
        1: ['Draft',     'bg-secondary'],
        2: ['Open',      'bg-success'],
        3: ['On Hold',   'bg-warning text-dark'],
        4: ['Closed',    'bg-dark'],
        5: ['Cancelled', 'bg-danger'],
    };
    const [label, cls] = map[statusInt] || ['?', 'bg-secondary'];
    return `<span class="badge ${cls}">${label}</span>`;
}

/**
 * DegreeLevel label from int.
 */
function degreeLevelLabel(val) {
    const map = { 1:'Lise', 2:'Ön Lisans', 3:'Lisans', 4:'Yüksek Lisans', 5:'Doktora', 99:'Diğer' };
    return map[val] || val;
}

function languageProficiencyLabel(val) {
    const map = { 1:'A1', 2:'A2', 3:'B1', 4:'B2', 5:'C1', 6:'C2', 7:'Ana dil' };
    return map[val] || val;
}

function educationLevelRank(value) {
    const map = { 1:1, 2:2, 3:3, 4:4, 5:5, 99:0 };
    return Object.prototype.hasOwnProperty.call(map, value) ? map[value] : -1;
}

function applyPreferredCompetencySuggestion(suggestion, mandatoryStore, preferredStore, canApply = true) {
    if (!canApply || !suggestion) return false;

    const competencyId = suggestion.competencyId;
    if (mandatoryStore.some(item => item.competencyId === competencyId)
        || preferredStore.some(item => item.competencyId === competencyId)) {
        return false;
    }

    preferredStore.push({
        competencyId,
        competencyName: suggestion.competencyName,
        isRequired: false
    });
    return true;
}

function languageProficiencyRank(value) {
    const map = { 1:1, 2:2, 3:3, 4:4, 5:5, 6:6 };
    return Object.prototype.hasOwnProperty.call(map, value) ? map[value] : 0;
}

function benchmarkPercentage(decimal) {
    if (decimal == null) return '—';
    return `${new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 1 }).format(decimal * 100)}%`;
}

function benchmarkMedianProficiencyLabel(rank) {
    if (rank == null) return 'Yeterlilik bilgisi yok';
    if (Number.isInteger(rank)) return languageProficiencyLabel(rank);

    const lower = Math.floor(rank);
    const upper = Math.ceil(rank);
    return `${languageProficiencyLabel(lower)}–${languageProficiencyLabel(upper)} arası`;
}

// ──────────────────────────────────────────────
// Position benchmark display (Create and Edit pages)
// ──────────────────────────────────────────────

function initPositionBenchmark(cfg) {
    const {
        positionSelectEl,
        analyzeButtonEl,
        buttonTextEl,
        spinnerEl,
        containerEl,
        warningEl,
        experienceInputEl,
        educationSelectEl,
        mandatoryStore,
        preferredStore,
        languageStore,
        renderPreferredSkills,
        renderLanguages,
        canApplySuggestions: configuredCanApplySuggestions
    } = cfg;
    const canApplySuggestions = typeof configuredCanApplySuggestions === 'function'
        ? configuredCanApplySuggestions
        : () => configuredCanApplySuggestions !== false;
    let requestVersion = 0;
    let abortController = null;
    let currentBenchmark = null;
    let benchmarkSuggestionApplied = false;

    function markBenchmarkSuggestionApplied() {
        benchmarkSuggestionApplied = true;
        refreshSuggestionActionStates();
    }

    function setActionState(button, disabled, text) {
        button.disabled = disabled;
        button.textContent = text;
        button.classList.toggle('btn-outline-primary', !disabled);
        button.classList.toggle('btn-outline-secondary', disabled);
    }

    function refreshSuggestionActionStates() {
        if (!currentBenchmark) return;

        if (!canApplySuggestions()) {
            containerEl.querySelectorAll(`
                [data-benchmark-apply-skill],
                [data-benchmark-apply-experience],
                [data-benchmark-apply-education],
                [data-benchmark-apply-language]`)
                .forEach(button => setActionState(button, true, 'Düzenleme kilitli'));
            return;
        }

        containerEl.querySelectorAll('[data-benchmark-apply-skill]').forEach(button => {
            const competencyId = button.dataset.benchmarkApplySkill;
            if (mandatoryStore.some(item => item.competencyId === competencyId)) {
                setActionState(button, true, 'Zaten zorunlu');
            } else if (preferredStore.some(item => item.competencyId === competencyId)) {
                setActionState(button, true, 'Zaten eklendi');
            } else {
                setActionState(button, false, 'Tercih Edilenlere Ekle');
            }
        });

        const experienceButton = containerEl.querySelector('[data-benchmark-apply-experience]');
        if (experienceButton) {
            const suggested = currentBenchmark.suggestions.minimumRelevantExperienceMonths;
            const parsed = Number.parseInt(experienceInputEl.value, 10);
            const current = experienceInputEl.value === '' || !Number.isFinite(parsed) ? null : parsed;
            if (current === suggested) {
                setActionState(experienceButton, true, 'Kullanılıyor');
            } else if (current !== null && current > suggested) {
                setActionState(experienceButton, true, 'Mevcut değer daha sıkı');
            } else {
                setActionState(experienceButton, false, `${suggested} ayı kullan`);
            }
        }

        const educationButton = containerEl.querySelector('[data-benchmark-apply-education]');
        if (educationButton) {
            const suggested = currentBenchmark.suggestions.minimumEducationLevel;
            const currentValue = educationSelectEl.value;
            const currentRank = educationLevelRank(currentValue);
            const suggestedRank = educationLevelRank(suggested);
            if (currentValue !== '' && currentRank === suggestedRank) {
                setActionState(educationButton, true, 'Kullanılıyor');
            } else if (currentRank > suggestedRank) {
                setActionState(educationButton, true, 'Mevcut değer daha sıkı');
            } else {
                setActionState(educationButton, false, 'Bu eğitim seviyesini kullan');
            }
        }

        containerEl.querySelectorAll('[data-benchmark-apply-language]').forEach(button => {
            const languageId = button.dataset.benchmarkApplyLanguage;
            const suggestion = currentBenchmark.suggestions.languages
                .find(item => item.languageId === languageId);
            if (!suggestion) return;

            const existing = languageStore.find(item => item.languageId === languageId);
            if (!existing) {
                setActionState(button, false, 'Dil Gereksinimi Olarak Ekle');
                return;
            }

            const currentRank = languageProficiencyRank(existing.minimumProficiency);
            const suggestedRank = languageProficiencyRank(suggestion.minimumProficiency);
            if (currentRank === suggestedRank) {
                setActionState(button, true, 'Zaten eklendi');
            } else if (currentRank > suggestedRank) {
                setActionState(button, true, 'Mevcut gereksinim daha sıkı');
            } else {
                setActionState(
                    button,
                    false,
                    `${languageProficiencyLabel(suggestion.minimumProficiency)} seviyesine yükselt`);
            }
        });
    }

    function invalidateBenchmark() {
        requestVersion += 1;
        if (abortController) {
            abortController.abort();
            abortController = null;
        }

        if (benchmarkSuggestionApplied) {
            warningEl.innerHTML = `
                <div class="alert alert-warning py-2 mb-0" role="alert">
                    Pozisyon değişti. Önceki pozisyon benchmarkından forma eklediğiniz gereksinimleri gözden geçirin.
                </div>`;
            warningEl.classList.remove('d-none');
            benchmarkSuggestionApplied = false;
        }

        currentBenchmark = null;
        containerEl.innerHTML = '';
        containerEl.classList.add('d-none');
        analyzeButtonEl.disabled = !positionSelectEl.value;
        buttonTextEl.textContent = 'Pozisyonu Analiz Et';
        spinnerEl.classList.add('d-none');
    }

    containerEl.addEventListener('click', event => {
        const applyControl = event.target.closest(`
            [data-benchmark-apply-skill],
            [data-benchmark-apply-experience],
            [data-benchmark-apply-education],
            [data-benchmark-apply-language]`);
        if (!applyControl) return;
        if (!canApplySuggestions()) {
            refreshSuggestionActionStates();
            return;
        }

        const skillButton = event.target.closest('[data-benchmark-apply-skill]');
        if (skillButton && currentBenchmark) {
            const competencyId = skillButton.dataset.benchmarkApplySkill;
            const suggestion = currentBenchmark.suggestions.preferredCompetencies
                .find(item => item.competencyId === competencyId);
            if (!applyPreferredCompetencySuggestion(
                suggestion,
                mandatoryStore,
                preferredStore,
                canApplySuggestions())) {
                refreshSuggestionActionStates();
                return;
            }

            renderPreferredSkills();
            markBenchmarkSuggestionApplied();
            return;
        }

        const experienceButton = event.target.closest('[data-benchmark-apply-experience]');
        if (experienceButton && currentBenchmark) {
            const suggested = currentBenchmark.suggestions.minimumRelevantExperienceMonths;
            const parsed = Number.parseInt(experienceInputEl.value, 10);
            const current = experienceInputEl.value === '' || !Number.isFinite(parsed) ? null : parsed;
            if (suggested !== null && (current === null || current < suggested)) {
                experienceInputEl.value = suggested;
                markBenchmarkSuggestionApplied();
            } else {
                refreshSuggestionActionStates();
            }
            return;
        }

        const educationButton = event.target.closest('[data-benchmark-apply-education]');
        if (educationButton && currentBenchmark) {
            const suggested = currentBenchmark.suggestions.minimumEducationLevel;
            const currentRank = educationLevelRank(educationSelectEl.value);
            const suggestedRank = educationLevelRank(suggested);
            if (suggested !== null && suggested !== 99 && currentRank < suggestedRank) {
                educationSelectEl.value = String(suggested);
                markBenchmarkSuggestionApplied();
            } else {
                refreshSuggestionActionStates();
            }
            return;
        }

        const languageButton = event.target.closest('[data-benchmark-apply-language]');
        if (languageButton && currentBenchmark) {
            const languageId = languageButton.dataset.benchmarkApplyLanguage;
            const suggestion = currentBenchmark.suggestions.languages
                .find(item => item.languageId === languageId);
            if (!suggestion) return;

            const existing = languageStore.find(item => item.languageId === languageId);
            if (!existing) {
                languageStore.push({
                    languageId: suggestion.languageId,
                    languageName: suggestion.languageName,
                    minimumProficiency: suggestion.minimumProficiency,
                    proficiencyName: languageProficiencyLabel(suggestion.minimumProficiency),
                    hardFilterEnabled: false
                });
                renderLanguages();
                markBenchmarkSuggestionApplied();
                return;
            }

            if (languageProficiencyRank(existing.minimumProficiency)
                < languageProficiencyRank(suggestion.minimumProficiency)) {
                existing.minimumProficiency = suggestion.minimumProficiency;
                existing.proficiencyName = languageProficiencyLabel(suggestion.minimumProficiency);
                renderLanguages();
                markBenchmarkSuggestionApplied();
            } else {
                refreshSuggestionActionStates();
            }
        }
    });

    async function analyzeBenchmark() {
        const requestedPositionId = positionSelectEl.value;
        if (!requestedPositionId || analyzeButtonEl.disabled) return;

        const version = ++requestVersion;
        abortController = new AbortController();
        analyzeButtonEl.disabled = true;
        buttonTextEl.textContent = 'Analiz Ediliyor...';
        spinnerEl.classList.remove('d-none');
        containerEl.innerHTML = '';
        containerEl.classList.add('d-none');

        try {
            const response = await fetch(`/api/position-benchmarks/${encodeURIComponent(requestedPositionId)}`, {
                signal: abortController.signal
            });
            if (!response.ok) throw new Error('benchmark_request_failed');

            const benchmark = await response.json();
            if (version !== requestVersion || positionSelectEl.value !== requestedPositionId) return;

            currentBenchmark = benchmark;
            containerEl.innerHTML = renderPositionBenchmark(benchmark);
            containerEl.classList.remove('d-none');
            refreshSuggestionActionStates();
        } catch (error) {
            if (error.name === 'AbortError' || version !== requestVersion) return;

            currentBenchmark = null;
            containerEl.innerHTML = `
                <div class="alert alert-danger mb-0" role="alert">
                    Pozisyon benchmarkı alınamadı. Lütfen tekrar deneyin.
                </div>`;
            containerEl.classList.remove('d-none');
        } finally {
            if (version === requestVersion) {
                abortController = null;
                analyzeButtonEl.disabled = !positionSelectEl.value;
                buttonTextEl.textContent = 'Pozisyonu Analiz Et';
                spinnerEl.classList.add('d-none');
            }
        }
    }

    positionSelectEl.addEventListener('change', invalidateBenchmark);
    experienceInputEl.addEventListener('input', refreshSuggestionActionStates);
    educationSelectEl.addEventListener('change', refreshSuggestionActionStates);
    document.addEventListener('requisition-form-state-changed', refreshSuggestionActionStates);
    analyzeButtonEl.addEventListener('click', analyzeBenchmark);
    invalidateBenchmark();
}

function renderPositionBenchmark(result) {
    const total = result.totalEmployees;
    const status = result.sampleSizeStatus;
    const statusHtml = status === 1
        ? '<div class="alert alert-secondary py-2">Bu pozisyonda analiz edilebilecek aktif çalışan bulunamadı.</div>'
        : status === 2
            ? `<div class="alert alert-warning py-2">Sınırlı veri: yalnızca ${total} aktif çalışan üzerinden benchmark hesaplandı. Örneklem büyüklüğü nedeniyle gereksinim önerisi üretilmedi.</div>`
            : '<div class="alert alert-success py-2">Yeterli örneklem üzerinden benchmark hesaplandı.</div>';

    if (status === 1) {
        return benchmarkCardHeader(result, statusHtml);
    }

    const benchmark = result.benchmark;
    const suggestions = status === 3
        ? renderBenchmarkSuggestions(result.suggestions, benchmark.skills.items)
        : '';

    return `
        <div class="card border-primary-subtle bg-light shadow-sm">
            <div class="card-body">
                ${benchmarkHeaderContent(result, statusHtml)}
                <div class="row g-3">
                    <div class="col-lg-6">${renderSkillBenchmark(benchmark.skills)}</div>
                    <div class="col-lg-6">${renderExperienceBenchmark(benchmark.experience)}</div>
                    <div class="col-lg-6">${renderEducationBenchmark(benchmark.education)}</div>
                    <div class="col-lg-6">${renderLanguageBenchmark(benchmark.languages)}</div>
                </div>
                ${suggestions}
            </div>
        </div>`;
}

function benchmarkCardHeader(result, statusHtml) {
    return `
        <div class="card border-primary-subtle bg-light shadow-sm">
            <div class="card-body">
                ${benchmarkHeaderContent(result, statusHtml)}
            </div>
        </div>`;
}

function benchmarkHeaderContent(result, statusHtml) {
    return `
        <div class="d-flex justify-content-between align-items-start gap-2 flex-wrap mb-2">
            <div>
                <h5 class="card-title mb-1">Pozisyon Benchmarkı</h5>
                <div class="text-muted small">${escHtml(result.positionName)} (${escHtml(result.positionCode)})</div>
            </div>
            <span class="badge bg-primary">Analiz edilen çalışan: ${result.totalEmployees}</span>
        </div>
        ${statusHtml}`;
}

function renderCoverage(label, coverage) {
    return `<div class="text-muted small mb-2">${label}: <strong>${coverage.knownProfiles} / ${coverage.totalEmployees}</strong></div>`;
}

function renderSkillBenchmark(skills) {
    const rows = skills.items.length === 0
        ? '<div class="text-muted small">Kayıtlı beceri bulunamadı.</div>'
        : `<div class="table-responsive"><table class="table table-sm mb-0">
            <thead><tr><th>Beceri</th><th>Çalışan</th><th>Oran</th></tr></thead>
            <tbody>${skills.items.map(item => `
                <tr>
                    <td>${escHtml(item.competencyName)}</td>
                    <td>${item.employeeCount} / ${item.knownProfileCount}</td>
                    <td>${benchmarkPercentage(item.percentage)}</td>
                </tr>`).join('')}</tbody>
        </table></div>`;

    return `<section><h6 class="fw-bold">Beceriler</h6>${renderCoverage('Skill profili bulunan', skills.coverage)}${rows}</section>`;
}

function renderExperienceBenchmark(experience) {
    if (!experience.isSupported) {
        return `<section><h6 class="fw-bold">İlgili Deneyim</h6>
            ${renderCoverage('Verisi bulunan', experience.coverage)}
            <div class="text-muted small">Bu pozisyon için yapılandırılmış ilgili deneyim benchmarkı mevcut değil.</div>
        </section>`;
    }

    const median = experience.medianMonths == null
        ? 'Medyan hesaplamak için kayıtlı deneyim verisi yok.'
        : `Medyan: <strong>${new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 1 }).format(experience.medianMonths)} ay</strong>`;
    return `<section><h6 class="fw-bold">İlgili Deneyim</h6>
        ${renderCoverage('Verisi bulunan', experience.coverage)}
        <div class="small">${median}</div>
    </section>`;
}

function renderEducationBenchmark(education) {
    const rows = education.distribution.length === 0
        ? '<div class="text-muted small">Kayıtlı eğitim bilgisi bulunamadı.</div>'
        : `<ul class="list-unstyled small mb-0">${education.distribution.map(item => `
            <li class="d-flex justify-content-between gap-2">
                <span>${escHtml(degreeLevelLabel(item.degreeLevel))}</span>
                <span>${item.employeeCount} / ${item.knownProfileCount} (${benchmarkPercentage(item.percentage)})</span>
            </li>`).join('')}</ul>`;
    return `<section><h6 class="fw-bold">Eğitim</h6>${renderCoverage('Eğitim profili bulunan', education.coverage)}${rows}</section>`;
}

function renderLanguageBenchmark(languages) {
    const rows = languages.items.length === 0
        ? '<div class="text-muted small">Kayıtlı dil bilgisi bulunamadı.</div>'
        : `<ul class="list-group list-group-flush small">${languages.items.map(item => `
            <li class="list-group-item bg-transparent px-0 py-1">
                <div class="d-flex justify-content-between gap-2">
                    <strong>${escHtml(item.languageName)}</strong>
                    <span>${item.employeeCount} / ${item.knownProfileCount} (${benchmarkPercentage(item.percentage)})</span>
                </div>
                <div class="text-muted">Medyan yeterlilik: ${escHtml(benchmarkMedianProficiencyLabel(item.medianProficiencyRank))}
                    · Yeterlilik verisi: ${item.knownProficiencyCount} / ${item.employeeCount}
                    ${item.nativeSpeakerCount > 0 ? ` · Ana dil: ${item.nativeSpeakerCount}` : ''}
                </div>
            </li>`).join('')}</ul>`;
    return `<section><h6 class="fw-bold">Diller</h6>${renderCoverage('Dil profili bulunan', languages.coverage)}${rows}</section>`;
}

function renderBenchmarkSuggestions(suggestions, skillFrequencies) {
    const skills = suggestions.preferredCompetencies.length === 0
        ? '<li>Beceri önerisi yok</li>'
        : suggestions.preferredCompetencies.map(item => {
            const frequency = skillFrequencies.find(value => value.competencyId === item.competencyId);
            const prevalence = frequency
                ? `<span class="text-muted">Çalışanlarda: ${frequency.employeeCount} / ${frequency.knownProfileCount} (${benchmarkPercentage(frequency.percentage)})</span>`
                : '';

            return `
            <li class="d-flex align-items-center justify-content-between gap-2 mb-1">
                <span>${escHtml(item.competencyName)} <span class="text-muted">(Tercih edilen)</span> ${prevalence}</span>
                <button type="button" class="btn btn-sm btn-outline-primary"
                        data-benchmark-apply-skill="${escHtml(item.competencyId)}">Tercih Edilenlere Ekle</button>
            </li>`;
        }).join('');
    const experience = suggestions.minimumRelevantExperienceMonths == null
        ? 'Öneri yok'
        : `<div>${suggestions.minimumRelevantExperienceMonths} ay</div>
           <button type="button" class="btn btn-sm btn-outline-primary mt-1"
                   data-benchmark-apply-experience>${suggestions.minimumRelevantExperienceMonths} ayı kullan</button>`;
    const education = suggestions.minimumEducationLevel == null || suggestions.minimumEducationLevel === 99
        ? 'Öneri yok'
        : `<div>${escHtml(degreeLevelLabel(suggestions.minimumEducationLevel))}</div>
           <button type="button" class="btn btn-sm btn-outline-primary mt-1"
                   data-benchmark-apply-education>Bu eğitim seviyesini kullan</button>`;
    const languages = suggestions.languages.length === 0
        ? '<li>Dil önerisi yok</li>'
        : suggestions.languages.map(item => `
            <li class="d-flex align-items-center justify-content-between gap-2 mb-1">
                <span>${escHtml(item.languageName)} ${escHtml(languageProficiencyLabel(item.minimumProficiency))}
                    <span class="badge bg-secondary">Yumuşak</span>
                </span>
                <button type="button" class="btn btn-sm btn-outline-primary"
                        data-benchmark-apply-language="${escHtml(item.languageId)}">Dil Gereksinimi Olarak Ekle</button>
            </li>`).join('');

    return `
        <section class="border-top mt-3 pt-3" data-benchmark-suggestions>
            <h6 class="fw-bold">Önerilen Gereksinimler</h6>
            <div class="text-muted small mb-2">Yalnızca seçtiğiniz öneriler forma uygulanır.</div>
            <div class="row g-2 small">
                <div class="col-md-6"><strong>Tercih Edilen Beceriler</strong><ul class="mb-0">${skills}</ul></div>
                <div class="col-md-3"><strong>Minimum İlgili Deneyim</strong><div>${experience}</div></div>
                <div class="col-md-3"><strong>Minimum Eğitim</strong><div>${education}</div></div>
                <div class="col-12"><strong>Diller</strong><ul class="mb-0">${languages}</ul></div>
            </div>
        </section>`;
}

/**
 * Convert threshold decimal (0.50) to display percentage (50).
 * Returns '' if null.
 */
function thresholdToDisplay(decimal) {
    if (decimal == null) return '';
    return Math.round(decimal * 100);
}

/**
 * Convert display percentage string/number (50) to decimal (0.50).
 * Returns null if empty.
 */
function displayToThreshold(pct) {
    if (pct === '' || pct == null) return null;
    return parseFloat(pct) / 100;
}

// ──────────────────────────────────────────────
// Skill picker component
// ──────────────────────────────────────────────

/**
 * Initialise a skill picker widget.
 * @param {object} cfg
 *   selectEl    - <select> element for competency choice
 *   addBtn      - <button> to add selected competency
 *   listEl      - container <div/ul> to render added items
 *   store       - array reference that will hold { competencyId, competencyName, isRequired }
 *   isRequired  - boolean: true = mandatory, false = preferred
 *   otherStore  - the OTHER store, used to prevent duplicates across both lists
 *   competencies - options array from API: [ { id, name } ]
 */
function initSkillPicker(cfg) {
    const { selectEl, addBtn, listEl, store, isRequired, otherStore, competencies } = cfg;

    // Populate select
    competencies.forEach(c => {
        const opt = document.createElement('option');
        opt.value = c.id;
        opt.textContent = c.name;
        selectEl.appendChild(opt);
    });

    addBtn.addEventListener('click', () => {
        const id = selectEl.value;
        if (!id) return;
        const name = selectEl.options[selectEl.selectedIndex].text;

        // Prevent duplicate in own list
        if (store.some(s => s.competencyId === id)) {
            showAlert(addBtn.closest('[data-skills-container]').querySelector('[data-skills-alert]'),
                `${name} zaten eklendi.`, 'warning');
            return;
        }
        // Prevent duplicate across mandatory/preferred
        if (otherStore.some(s => s.competencyId === id)) {
            showAlert(addBtn.closest('[data-skills-container]').querySelector('[data-skills-alert]'),
                `${name} diğer listede zaten mevcut.`, 'warning');
            return;
        }
        clearAlert(addBtn.closest('[data-skills-container]').querySelector('[data-skills-alert]'));

        store.push({ competencyId: id, competencyName: name, isRequired });
        renderSkillList(listEl, store, competencies);
    });

    renderSkillList(listEl, store, competencies);
}

function renderSkillList(listEl, store, competencies) {
    listEl.innerHTML = '';
    document.dispatchEvent(new CustomEvent('requisition-form-state-changed'));
    if (store.length === 0) {
        listEl.innerHTML = '<span class="text-muted small">Henüz yetkinlik eklenmedi.</span>';
        return;
    }
    store.forEach((item, idx) => {
        const row = document.createElement('div');
        row.className = 'd-flex align-items-center gap-2 mb-1';
        row.innerHTML = `
            <span class="badge bg-primary">${escHtml(item.competencyName)}</span>
            <button type="button" class="btn btn-sm btn-outline-danger py-0 px-1"
                    data-idx="${idx}" title="Kaldır">✕</button>`;
        row.querySelector('button').addEventListener('click', () => {
            store.splice(idx, 1);
            renderSkillList(listEl, store, competencies);
        });
        listEl.appendChild(row);
    });
}

// ──────────────────────────────────────────────
// Language requirement component
// ──────────────────────────────────────────────

function initLanguagePicker(cfg) {
    const { langSelectEl, profSelectEl, hardFilterEl, addBtn, listEl, store, languageProficiencyLevels } = cfg;

    addBtn.addEventListener('click', () => {
        const langId   = langSelectEl.value;
        if (!langId) return;
        const langName = langSelectEl.options[langSelectEl.selectedIndex].text;
        const prof     = parseInt(profSelectEl.value, 10);
        const profName = profSelectEl.options[profSelectEl.selectedIndex].text;
        const hardFilter = hardFilterEl.checked;

        if (store.some(s => s.languageId === langId)) {
            return; // already added
        }

        store.push({ languageId: langId, languageName: langName, minimumProficiency: prof, proficiencyName: profName, hardFilterEnabled: hardFilter });
        renderLanguageList(listEl, store);
    });
}

function renderLanguageList(listEl, store) {
    listEl.innerHTML = '';
    document.dispatchEvent(new CustomEvent('requisition-form-state-changed'));
    if (store.length === 0) {
        listEl.innerHTML = '<span class="text-muted small">Henüz dil gereksinimi eklenmedi.</span>';
        return;
    }
    store.forEach((item, idx) => {
        const row = document.createElement('div');
        row.className = 'd-flex align-items-center gap-2 mb-1 flex-wrap';
        row.innerHTML = `
            <span class="badge bg-info text-dark">${escHtml(item.languageName)}</span>
            <span class="text-muted small">Min: ${escHtml(item.proficiencyName)}</span>
            ${item.hardFilterEnabled ? '<span class="badge bg-danger">Sert Filtre</span>' : '<span class="badge bg-secondary">Yumuşak</span>'}
            <button type="button" class="btn btn-sm btn-outline-danger py-0 px-1"
                    data-idx="${idx}" title="Kaldır">✕</button>`;
        row.querySelector('button').addEventListener('click', () => {
            store.splice(idx, 1);
            renderLanguageList(listEl, store);
        });
        listEl.appendChild(row);
    });
}

if (typeof module === 'object' && module.exports) {
    module.exports = { applyPreferredCompetencySuggestion, initPositionBenchmark };
}
