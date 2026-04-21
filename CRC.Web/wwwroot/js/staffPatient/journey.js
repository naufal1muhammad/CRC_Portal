// @ts-nocheck
(function () {
    let journeyLoadedOnce = false;

    // ----------------------------
    // JOURNEY endpoints live HERE (not in details.js)
    // ----------------------------
    const ENDPOINTS = {
        getTimeline: "/StaffPatient/GetTimeline",
        getTemplate: "/StaffPatient/GetJourneyTemplate",

        getPatientAssessment: "/StaffPatient/GetPatientAssessment",
        savePatientAssessment: "/StaffPatient/SavePatientAssessment",

        getPatientColonoscopy: "/StaffPatient/GetPatientColonoscopy",
        savePatientColonoscopy: "/StaffPatient/SavePatientColonoscopy",

        getPatientFollowUp: "/StaffPatient/GetPatientFollowUp",
        savePatientFollowUp: "/StaffPatient/SavePatientFollowUp",
    };

    const TEMPLATE_CONFIG = {
        "PATIENT ASSESSMENT": {
            script: "/js/staffPatient/templates/patientAssessment.js",
            get: () => ENDPOINTS.getPatientAssessment,
            save: () => ENDPOINTS.savePatientAssessment,
        },
        "COLONOSCOPY": {
            script: "/js/staffPatient/templates/patientColonoscopy.js",
            get: () => ENDPOINTS.getPatientColonoscopy,
            save: () => ENDPOINTS.savePatientColonoscopy,
        },
        "PATIENT FOLLOW UP": {
            script: "/js/staffPatient/templates/patientFollowUp.js",
            get: () => ENDPOINTS.getPatientFollowUp,
            save: () => ENDPOINTS.savePatientFollowUp,
        },
    };

    // --------- helpers from details.js ----------
    function ctx() {
        return window.StaffPatient || {};
    }
    function h() {
        return ctx().helpers || {};
    }

    function el(id) {
        return h().el ? h().el(id) : document.getElementById(id);
    }

    function setMsg(text, cssClass) {
        if (h().setMsg) h().setMsg("spJourneyMsg", text, cssClass);
    }

    async function getJson(url) {
        if (h().getJson) return await h().getJson(url);
        const res = await fetch(url, { headers: { Accept: "application/json" } });
        if (!res.ok) throw new Error("HTTP " + res.status);
        return await res.json();
    }

    async function postJson(url, payload) {
        const res = await fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json", Accept: "application/json" },
            body: JSON.stringify(payload),
        });
        if (!res.ok) throw new Error("HTTP " + res.status);
        return await res.json();
    }

    function escapeHtml(str) {
        return (str ?? "")
            .toString()
            .replace(/[&<>"']/g, (s) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[s]));
    }

    function normalizeTypeKey(type) {
        return (type || "").toString().trim().toUpperCase();
    }

    function pick(obj, keys, fallback = "") {
        for (const k of keys) {
            if (obj && obj[k] !== undefined && obj[k] !== null && String(obj[k]).trim() !== "") return obj[k];
        }
        return fallback;
    }

    function toLocalDisplay(dt) {
        if (!dt) return "";
        // if already formatted by server, just return it
        const s = dt.toString();
        const d = new Date(s);
        if (isNaN(d.getTime())) return s;
        // dd/MM/yyyy HH:mm
        const pad = (n) => String(n).padStart(2, "0");
        return `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
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
            minute: "2-digit"
        });
    }

    // ------------- template JS loader -------------
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

    // ----------------------------
    // ACTIVE template state (fixes init/collect)
    // ----------------------------
    let activeTemplateKey = null;
    let activeTemplateApi = null;

    function resolveTemplateApi(typeKeyUpper) {
        const templates = window.StaffPatientTemplates || {};

        // Preferred: exact key match (e.g. "PATIENT ASSESSMENT")
        if (templates[typeKeyUpper]) return templates[typeKeyUpper];

        // Backward compatibility alias
        if (typeKeyUpper === "PATIENT ASSESSMENT" && templates.PatientAssessment) return templates.PatientAssessment;

        return null;
    }

    // ✅ You already have activateTemplate — we KEEP it and ensure it's used correctly.
    async function activateTemplate(typeRaw, hostEl, assessmentDataOrNull) {
        if (!hostEl) throw new Error("Template host not found.");

        const typeKey = normalizeTypeKey(typeRaw);

        // reset active
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

        // ✅ IMPORTANT: init AFTER HTML is injected
        const templateRoot = api.rootSelector ? (hostEl.querySelector(api.rootSelector) || hostEl) : hostEl;
        api.init(templateRoot);

        // set active
        activeTemplateKey = typeKey;
        activeTemplateApi = api;

        // Fill (edit) or clear (add)
        if (assessmentDataOrNull && typeof api.fill === "function") {
            api.fill(assessmentDataOrNull);
        } else if (typeof api.clear === "function") {
            api.clear();
        }
    }

    // ---------------- Timeline ----------------
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
            const journeyDateRaw = pick(item, ["journeyDate", "PatientJourney_Date", "patientJourneyDate", "journeyDateDisplay"], "");
            const createdAtRaw = pick(item, ["createdAt", "Created_At", "CreatedAt", "createdAtLocal", "createdAtDisplay"], "");
            const createdByRaw = pick(item, [
                "createdByStaffName",
                "createdByName",
                "CreatedByStaffName",
                "CreatedBy_Staff_Name",
                "createdBy",
                "CreatedBy_Staff_ID",
                "createdByStaffId",
            ], "");

            const updatedAtRaw = pick(item, ["updatedAt", "Updated_At", "UpdatedAt", "updatedAtLocal", "updatedAtDisplay"], "");
            const updatedByRaw = pick(item, [
                "updatedByStaffName",
                "updatedByName",
                "UpdatedByStaffName",
                "UpdatedBy_Staff_Name",
                "updatedBy",
                "UpdatedBy_Staff_ID",
                "updatedByStaffId",
            ], "");

            const card = document.createElement("div");
            card.className = "border rounded p-3 bg-white js-journey-item";
            card.style.cursor = "pointer";
            card.dataset.journeyId = String(id);
            card.dataset.journeyType = String(type || "");
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
                        const friendly = a === "CREATED" ? "Created" : (a === "UPDATED" || a === "EDITED") ? "Edited" : (a || "Action");
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
          <div class="text-muted small">Click to edit</div>
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
        const patientId = (ctx().patientId || (h().getPatientIdFromDom ? h().getPatientIdFromDom() : "") || "").trim();
        if (!patientId) return;

        setMsg("Loading timeline...", "text-muted");

        const url = ENDPOINTS.getTimeline + "?patientId=" + encodeURIComponent(patientId);
        const result = await getJson(url);

        if (!result.success) throw new Error(result.message || "Failed to load timeline.");
        renderTimeline(result.data || []);
        setMsg("", "text-muted");
    }

    // ---------------- Modal flow ----------------
    function modal() {
        const m = el("journeyModal");
        if (!m) return null;
        return bootstrap.Modal.getOrCreateInstance(m);
    }

    function resetModal() {
        el("pj_patientJourneyId").value = "0";
        el("pj_journeyDate").value = "";
        el("pj_journeyType").value = "";
        el("pj_auditNote").value = "";
        el("pj_formError").textContent = "";

        const host = el("journeyTemplateHost");
        if (host) {
            host.innerHTML = `<div class="text-muted">Select a Journey Type to load the template.</div>`;
        }

        activeTemplateKey = null;
        activeTemplateApi = null;
    }

    async function loadTemplate(typeRaw, assessmentDataOrNull) {
        const host = el("journeyTemplateHost");
        if (!host) return;

        const typeKey = normalizeTypeKey(typeRaw);
        if (!typeKey) {
            host.innerHTML = `<div class="text-muted">Select a Journey Type to load the template.</div>`;
            activeTemplateKey = null;
            activeTemplateApi = null;
            return;
        }

        // 1) Load HTML partial
        const url = ENDPOINTS.getTemplate + "?type=" + encodeURIComponent(typeRaw);
        const res = await fetch(url, { headers: { Accept: "text/html" } });
        if (!res.ok) throw new Error("Failed to load template HTML.");
        const html = await res.text();

        // 2) Inject HTML FIRST
        host.innerHTML = html;

        // 3) ✅ Activate template AFTER injection (this is the key fix)
        await activateTemplate(typeRaw, host, assessmentDataOrNull);
    }

    async function openAddModal() {
        resetModal();
        el("journeyModalTitle").textContent = "Add Patient Journey";

        const now = new Date();
        el("pj_journeyDate").value = toDateTimeLocalInput(now);

        modal()?.show();
    }

    async function openEditModal(patientJourneyId, typeHintRaw) {
        resetModal();
        el("journeyModalTitle").textContent = "Edit Patient Journey";
        el("pj_patientJourneyId").value = String(patientJourneyId || 0);

        const typeHintKey = normalizeTypeKey(typeHintRaw);
        const cfgHint = TEMPLATE_CONFIG[typeHintKey];
        const getEndpoint = (cfgHint && typeof cfgHint.get === "function") ? cfgHint.get() : ENDPOINTS.getPatientAssessment;
        const url = getEndpoint + "?patientJourneyId=" + encodeURIComponent(patientJourneyId);
        const result = await getJson(url);
        if (!result.success) throw new Error(result.message || "Failed to load journey details.");

        // Support both shapes:
        // { success:true, journey:{...}, assessment:{...} }
        // OR { success:true, data:{ journey:{...}, assessment:{...} } }
        const journey = result.journey || result.data?.journey || {};
        const assessment = result.assessment || result.data?.assessment || {};

        const typeRaw = (journey.journeyType || journey.PjAppType_Name || "PATIENT ASSESSMENT").toString().trim();
        el("pj_journeyType").value = typeRaw;

        // datetime-local input value
        const journeyDateInput =
            journey.journeyDateInput ||
            journey.patientJourneyDateInput ||
            toDateTimeLocalInput(journey.journeyDate || journey.PatientJourney_Date || journey.patientJourneyDate);

        if (journeyDateInput) el("pj_journeyDate").value = journeyDateInput;

        // ✅ Load HTML + activate + fill assessment
        await loadTemplate(typeRaw, assessment);

        modal()?.show();
    }

    async function saveJourney() {
        const patientId = (ctx().patientId || "").trim();
        const journeyId = parseInt(el("pj_patientJourneyId")?.value || "0", 10) || 0;
        const journeyDate = (el("pj_journeyDate")?.value || "").trim();
        const typeRaw = (el("pj_journeyType")?.value || "").trim();
        const note = (el("pj_auditNote")?.value || "").trim();

        el("pj_formError").textContent = "";

        if (!patientId) return;
        if (!journeyDate) return (el("pj_formError").textContent = "Journey Date is required.");
        if (!typeRaw) return (el("pj_formError").textContent = "Journey Type is required.");

        const typeKey = normalizeTypeKey(typeRaw);

        // ✅ Use activated template (best) or resolve fallback
        const api = activeTemplateKey === typeKey ? activeTemplateApi : resolveTemplateApi(typeKey);

        if (!api || typeof api.collect !== "function") {
            el("pj_formError").textContent = "Template is not ready. Please reselect Journey Type.";
            return;
        }

        const payload = Object.assign(
            {
                patientJourneyId: journeyId,
                patientId: patientId,
                patientJourneyDate: journeyDate,
                auditNote: note,
                journeyType: typeRaw,
            },
            api.collect()
        );

        const cfg = TEMPLATE_CONFIG[typeKey];
        if (!cfg || typeof cfg.save !== "function") {
            el("pj_formError").textContent = "Unknown Journey Type. Please reselect.";
            return;
        }

        const result = await postJson(cfg.save(), payload);
        if (!result.success) throw new Error(result.message || "Failed saving journey.");

        modal()?.hide();
        await loadTimeline();
    }

    // ---------------- init / events ----------------
    document.addEventListener("DOMContentLoaded", function () {
        console.log("staffPatient/journey.js loaded ✅");

        // Load timeline when Journey tab is shown (first time)
        const journeyTabBtn = document.getElementById("tab-journey-tab");
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

        // Add button
        const btnAdd = el("btnAddJourney");
        if (btnAdd) {
            btnAdd.addEventListener("click", async function () {
                try {
                    await openAddModal();
                } catch (err) {
                    console.error(err);
                    setMsg(err.message || "Failed to open Add Journey.", "text-danger");
                }
            });
        }

        // Type change → load template (and init it)
        const ddlType = el("pj_journeyType");
        if (ddlType) {
            ddlType.addEventListener("change", async function () {
                try {
                    await loadTemplate((ddlType.value || "").trim(), null);
                } catch (err) {
                    console.error(err);
                    el("pj_formError").textContent = err.message || "Failed to load template.";
                }
            });
        }

        // Save
        const btnSave = el("btnSaveJourney");
        if (btnSave) {
            btnSave.addEventListener("click", async function () {
                try {
                    await saveJourney();
                } catch (err) {
                    console.error(err);
                    el("pj_formError").textContent = err.message || "Failed saving journey.";
                }
            });
        }

        // Click timeline item → edit
        const timelineHost = el("journeyTimeline");
        if (timelineHost) {
            timelineHost.addEventListener("click", async function (e) {
                const card = e.target.closest(".js-journey-item");
                if (!card) return;

                const id = parseInt(card.dataset.journeyId || "0", 10) || 0;
                if (!id) return;

                try {
                    await openEditModal(id, card.dataset.journeyType || "");
                } catch (err) {
                    console.error(err);
                    setMsg(err.message || "Failed to open edit modal.", "text-danger");
                }
            });
        }
    });
})();