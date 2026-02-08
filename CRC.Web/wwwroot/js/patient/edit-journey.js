// @ts-nocheck
(function () {
    let journeyLoadedOnce = false;

    // Reuse StaffPatient READ endpoints (Admin/Super/Staff)
    const ENDPOINTS = {
        getTimeline: "/StaffPatient/GetTimeline",
        getTemplate: "/StaffPatient/GetJourneyTemplate",

        getPatientAssessment: "/StaffPatient/GetPatientAssessment",
        getPatientColonoscopy: "/StaffPatient/GetPatientColonoscopy",
        getPatientFollowUp: "/StaffPatient/GetPatientFollowUp",
    };

    const TEMPLATE_CONFIG = {
        "PATIENT ASSESSMENT": {
            script: "/js/staffPatient/templates/patientAssessment.js",
            get: () => ENDPOINTS.getPatientAssessment,
        },
        "PATIENT COLONOSCOPY": {
            script: "/js/staffPatient/templates/patientColonoscopy.js",
            get: () => ENDPOINTS.getPatientColonoscopy,
        },
        "PATIENT FOLLOW UP": {
            script: "/js/staffPatient/templates/patientFollowUp.js",
            get: () => ENDPOINTS.getPatientFollowUp,
        },
    };

    // ---------------- basic helpers ----------------
    function el(id) {
        return document.getElementById(id);
    }

    function getPatientId() {
        const root = document.querySelector("[data-patient-id]");
        return root ? (root.getAttribute("data-patient-id") || "").trim() : "";
    }

    function setMsg(text, cssClass) {
        const host = el("spJourneyMsg");
        if (!host) return;
        host.className = (cssClass || "text-muted") + " small mb-3";
        host.textContent = text || "";
    }

    async function getJson(url) {
        const res = await fetch(url, { headers: { Accept: "application/json" } });
        if (!res.ok) throw new Error("HTTP " + res.status);
        return await res.json();
    }

    function escapeHtml(str) {
        return (str ?? "")
            .toString()
            .replace(/[&<>"']/g, (s) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[s]));
    }

    function pick(obj, keys, fallback = "") {
        for (const k of keys) {
            if (obj && obj[k] !== undefined && obj[k] !== null && String(obj[k]).trim() !== "") return obj[k];
        }
        return fallback;
    }

    function normalizeTypeKey(type) {
        return (type || "").toString().trim().toUpperCase();
    }

    function fmtMY(dt) {
        if (!dt) return "";
        const d = new Date(dt);
        if (Number.isNaN(d.getTime())) return String(dt); // fallback if backend sent non-ISO
        return d.toLocaleString("en-MY", {
            timeZone: "Asia/Kuala_Lumpur",
            year: "numeric",
            month: "2-digit",
            day: "2-digit",
            hour: "2-digit",
            minute: "2-digit",
        });
    }

    function toDateTimeLocalInput(dt) {
        if (!dt) return "";
        const s = dt.toString();
        // If already "YYYY-MM-DDTHH:mm"
        if (/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(s)) return s;

        const d = new Date(s);
        if (isNaN(d.getTime())) return "";

        const pad = (n) => String(n).padStart(2, "0");
        return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
    }

    // ---------------- template loader (reuse staff template modules) ----------------
    const loadedScripts = new Set();
    function loadScriptOnce(src) {
        return new Promise((resolve, reject) => {
            if (loadedScripts.has(src)) return resolve();

            const s = document.createElement("script");
            s.src = src;
            s.async = true;
            s.onload = () => {
                loadedScripts.add(src);
                resolve();
            };
            s.onerror = () => reject(new Error("Failed to load script: " + src));
            document.head.appendChild(s);
        });
    }

    let activeTemplateKey = null;
    let activeTemplateApi = null;

    function resolveTemplateApi(typeKeyUpper) {
        const templates = window.StaffPatientTemplates || {};
        if (templates[typeKeyUpper]) return templates[typeKeyUpper];
        if (typeKeyUpper === "PATIENT ASSESSMENT" && templates.PatientAssessment) return templates.PatientAssessment;
        return null;
    }

    function makeTemplateReadOnly(root) {
        if (!root) return;

        // Inputs/textareas: readonly; selects/checkboxes/radios: disabled
        root.querySelectorAll("input, textarea, select").forEach((ctrl) => {
            const tag = (ctrl.tagName || "").toLowerCase();
            if (tag === "select") {
                ctrl.disabled = true;
                return;
            }

            const type = (ctrl.getAttribute("type") || "").toLowerCase();
            if (type === "checkbox" || type === "radio" || type === "file") {
                ctrl.disabled = true;
                return;
            }

            // For date/datetime-local, disable to avoid picker edits
            if (type === "date" || type === "datetime-local") {
                ctrl.disabled = true;
                return;
            }

            ctrl.readOnly = true;
        });

        // Any buttons inside template should not be clickable
        root.querySelectorAll("button").forEach((b) => (b.disabled = true));
    }

    async function activateTemplate(typeRaw, hostEl, assessmentDataOrNull) {
        if (!hostEl) throw new Error("Template host not found.");

        const typeKey = normalizeTypeKey(typeRaw);
        activeTemplateKey = null;
        activeTemplateApi = null;

        // Load template JS if needed (skip if already registered)
        const cfg = TEMPLATE_CONFIG[typeKey];
        if (cfg && cfg.script && !resolveTemplateApi(typeKey)) {
            await loadScriptOnce(cfg.script);
        }

        const api = resolveTemplateApi(typeKey);
        if (!api) throw new Error("Template module not registered for: " + typeKey);
        if (typeof api.init !== "function") throw new Error("Template module missing init() for: " + typeKey);

        const templateRoot = api.rootSelector ? (hostEl.querySelector(api.rootSelector) || hostEl) : hostEl;
        api.init(templateRoot);

        activeTemplateKey = typeKey;
        activeTemplateApi = api;

        if (assessmentDataOrNull && typeof api.fill === "function") {
            api.fill(assessmentDataOrNull);
        }

        // ✅ read-only (after fill so conditional sections render correctly)
        makeTemplateReadOnly(templateRoot);
    }

    async function loadTemplate(typeRaw, assessmentDataOrNull) {
        const host = el("journeyTemplateHost");
        if (!host) return;

        const typeKey = normalizeTypeKey(typeRaw);
        if (!typeKey) {
            host.innerHTML = `<div class="text-muted">No journey type.</div>`;
            return;
        }

        const url = ENDPOINTS.getTemplate + "?type=" + encodeURIComponent(typeRaw);
        const res = await fetch(url, { headers: { Accept: "text/html" } });
        if (!res.ok) throw new Error("Failed to load template.");
        const html = await res.text();
        host.innerHTML = html;

        await activateTemplate(typeRaw, host, assessmentDataOrNull);
    }

    // ---------------- timeline rendering ----------------
    function renderTimeline(rows) {
        const host = el("journeyTimeline");
        if (!host) return;

        host.innerHTML = "";

        if (!rows || rows.length === 0) {
            host.innerHTML = `<div class="text-muted">No journey records yet.</div>`;
            return;
        }

        const wrapper = document.createElement("div");
        wrapper.className = "d-flex flex-column gap-2";

        rows.forEach((item) => {
            const id = pick(item, ["patientJourneyId", "PatientJourney_ID", "patientJourney_ID"], 0);
            const type = pick(item, ["journeyType", "PjAppType_Name", "pjAppTypeName"], "");
            const journeyDateRaw = pick(item, ["journeyDate", "PatientJourney_Date", "patientJourneyDate"], "");
            const createdAtRaw = pick(item, ["createdAt", "Created_At", "CreatedAt", "createdAtDisplay"], "");
            const createdByRaw = pick(
                item,
                [
                    "createdByStaffName",
                    "createdByName",
                    "CreatedByStaffName",
                    "CreatedBy_Staff_Name",
                    "createdBy",
                    "CreatedBy_Staff_ID",
                    "createdByStaffId",
                ],
                ""
            );

            const updatedAtRaw = pick(item, ["updatedAt", "Updated_At", "UpdatedAt", "updatedAtDisplay"], "");
            const updatedByRaw = pick(
                item,
                [
                    "updatedByStaffName",
                    "updatedByName",
                    "UpdatedByStaffName",
                    "UpdatedBy_Staff_Name",
                    "updatedBy",
                    "UpdatedBy_Staff_ID",
                    "updatedByStaffId",
                ],
                ""
            );

            const card = document.createElement("div");
            card.className = "border rounded p-3 bg-white js-journey-item";
            card.style.cursor = "pointer";
            card.dataset.journeyId = String(id);
            card.dataset.journeyType = String(type || "");

            const journeyDate = journeyDateRaw ? escapeHtml(fmtMY(journeyDateRaw) || journeyDateRaw) : "-";
            const createdAt = createdAtRaw ? escapeHtml(fmtMY(createdAtRaw) || createdAtRaw) : "-";
            const createdBy = createdByRaw ? escapeHtml(createdByRaw) : "-";
            const updatedAt = updatedAtRaw ? escapeHtml(fmtMY(updatedAtRaw) || updatedAtRaw) : "";
            const updatedBy = updatedByRaw ? escapeHtml(updatedByRaw) : "-";

            const auditEvents = item.auditEvents || item.AuditEvents || item.audit || [];
            let auditHtml = "";
            if (Array.isArray(auditEvents) && auditEvents.length > 0) {
                const itemsHtml = auditEvents
                    .map((ev) => {
                        const actionRaw = pick(ev, ["action", "Audit_Action", "auditAction"], "");
                        const atRaw = pick(ev, ["at", "Audit_At", "auditAt"], "");
                        const staffRaw = pick(ev, ["staffName", "Staff_Name", "staff"], "-");
                        const noteRaw = pick(ev, ["note", "Audit_Note", "auditNote"], "");

                        const a = (actionRaw || "").toString().trim().toUpperCase();
                        const friendly = a === "CREATED" ? "Created" : a === "UPDATED" || a === "EDITED" ? "Edited" : a || "Action";
                        const at = atRaw ? escapeHtml(fmtMY(atRaw) || atRaw) : "-";
                        const staff = escapeHtml(staffRaw || "-");
                        const note = noteRaw ? ` <span class="text-muted">— ${escapeHtml(noteRaw)}</span>` : "";

                        return `<li class="small"><span class="fw-semibold">${friendly}</span> by ${staff} <span class="text-muted">at</span> ${at}${note}</li>`;
                    })
                    .join("");

                auditHtml = `
                    <details class="mt-2" open>
                        <summary class="small text-muted">Audit history (${auditEvents.length})</summary>
                        <ul class="mt-2 mb-0 ps-3">${itemsHtml}</ul>
                    </details>`;
            }

            card.innerHTML = `
                <div class="d-flex justify-content-between align-items-start">
                    <div>
                        <div class="fw-bold">${escapeHtml(type || "Journey")}</div>
                        <div class="text-muted small">Journey Date: ${journeyDate}</div>
                    </div>
                    <div class="text-muted small">Click to view</div>
                </div>
                <hr class="my-2"/>
                <div class="small">
                    <div><span class="text-muted">Created by</span> ${createdBy} <span class="text-muted">at</span> ${createdAt}</div>
                    ${updatedAtRaw ? `<div class="mt-1"><span class="text-muted">Edited by</span> ${updatedBy} <span class="text-muted">at</span> ${updatedAt}</div>` : ""}
                </div>
                ${auditHtml}
            `;

            wrapper.appendChild(card);
        });

        host.appendChild(wrapper);
    }

    async function loadTimeline() {
        const patientId = getPatientId();
        if (!patientId) {
            setMsg("Please save basic details first.", "text-muted");
            renderTimeline([]);
            return;
        }

        setMsg("Loading timeline...", "text-muted");
        const url = ENDPOINTS.getTimeline + "?patientId=" + encodeURIComponent(patientId);
        const result = await getJson(url);

        if (!result.success) throw new Error(result.message || "Failed to load timeline.");
        renderTimeline(result.data || []);
        setMsg("", "text-muted");
    }

    // ---------------- modal (view-only) ----------------
    function modal() {
        const m = el("journeyModal");
        if (!m) return null;
        return bootstrap.Modal.getOrCreateInstance(m);
    }

    function resetModal() {
        if (el("pj_patientJourneyId")) el("pj_patientJourneyId").value = "0";
        if (el("pj_journeyDate")) el("pj_journeyDate").value = "";
        if (el("pj_journeyType")) el("pj_journeyType").value = "";
        if (el("pj_formError")) el("pj_formError").textContent = "";

        const host = el("journeyTemplateHost");
        if (host) host.innerHTML = `<div class="text-muted">Loading journey details...</div>`;

        activeTemplateKey = null;
        activeTemplateApi = null;
    }

    async function openViewModal(patientJourneyId, typeHintRaw) {
        resetModal();
        if (el("journeyModalTitle")) el("journeyModalTitle").textContent = "View Patient Journey";

        if (el("pj_patientJourneyId")) el("pj_patientJourneyId").value = String(patientJourneyId || 0);

        const typeHintKey = normalizeTypeKey(typeHintRaw);
        const cfgHint = TEMPLATE_CONFIG[typeHintKey];
        const getEndpoint = (cfgHint && typeof cfgHint.get === "function") ? cfgHint.get() : ENDPOINTS.getPatientAssessment;
        const url = getEndpoint + "?patientJourneyId=" + encodeURIComponent(patientJourneyId);
        const result = await getJson(url);
        if (!result.success) throw new Error(result.message || "Failed to load journey details.");

        const journey = result.journey || result.data?.journey || {};
        const assessment = result.assessment || result.data?.assessment || {};

        const typeRaw = (journey.journeyType || journey.PjAppType_Name || "PATIENT ASSESSMENT").toString().trim();
        if (el("pj_journeyType")) el("pj_journeyType").value = typeRaw;

        const journeyDateInput =
            journey.journeyDateInput ||
            journey.patientJourneyDateInput ||
            toDateTimeLocalInput(journey.journeyDate || journey.PatientJourney_Date || journey.patientJourneyDate);
        if (journeyDateInput && el("pj_journeyDate")) el("pj_journeyDate").value = journeyDateInput;

        await loadTemplate(typeRaw, assessment);
        modal()?.show();
    }

    // ---------------- init / events ----------------
    document.addEventListener("DOMContentLoaded", function () {
        // Load timeline when Journey tab is shown (first time)
        const journeyTabBtn = document.getElementById("tab-journey");
        if (journeyTabBtn) {
            journeyTabBtn.addEventListener("shown.bs.tab", async function () {
                if (journeyLoadedOnce) return;
                journeyLoadedOnce = true;

                try {
                    await loadTimeline();
                } catch (err) {
                    console.error(err);
                    setMsg(err.message || "Error loading journey timeline.", "text-danger");
                }
            });
        }

        // Click timeline item → view modal
        const timelineHost = el("journeyTimeline");
        if (timelineHost) {
            timelineHost.addEventListener("click", async function (e) {
                const card = e.target.closest(".js-journey-item");
                if (!card) return;

                const id = parseInt(card.dataset.journeyId || "0", 10) || 0;
                if (!id) return;

                try {
                    await openViewModal(id, card.dataset.journeyType || "");
                } catch (err) {
                    console.error(err);
                    setMsg(err.message || "Failed to open journey.", "text-danger");
                }
            });
        }
    });
})();
