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
