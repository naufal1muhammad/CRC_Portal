// @ts-nocheck
(function () {
    // Create namespace once
    window.StaffPatientTemplates = window.StaffPatientTemplates || {};

    let rootEl = null;

    function q(id) {
        if (!rootEl) return null;
        return rootEl.querySelector("#" + id);
    }

    function focusEl(id) {
        const el = q(id);
        if (el && typeof el.focus === "function") el.focus();
    }

    function isEmpty(v) {
        return v === null || v === undefined || String(v).trim() === "";
    }

    function getVal(id) {
        const el = q(id);
        if (!el) return "";
        return (el.value ?? "").toString().trim();
    }

    function setVal(id, value) {
        const el = q(id);
        if (!el) return;
        el.value = value ?? "";
    }

    function getChecked(id) {
        const el = q(id);
        if (!el) return false;
        return !!el.checked;
    }

    function setChecked(id, checked) {
        const el = q(id);
        if (!el) return;
        el.checked = !!checked;
    }

    function parseAnyToBool(raw) {
        if (raw === true || raw === false) return raw;
        if (raw === 1 || raw === 0) return raw === 1;
        const v = (raw ?? "").toString().trim().toLowerCase();
        if (v === "1" || v === "true" || v === "yes") return true;
        if (v === "0" || v === "false" || v === "no") return false;
        return false;
    }

    function requireSelectText(id, message) {
        const v = getVal(id);
        if (isEmpty(v)) {
            focusEl(id);
            throw new Error(message);
        }
        return v;
    }

    function requireChecked(id, message) {
        if (!getChecked(id)) {
            focusEl(id);
            throw new Error(message);
        }
        return true;
    }

    // -----------------------------
    // Template API
    // -----------------------------
    function init(root) {
        rootEl = root;
    }

    function clear() {
        setVal("pf_hpeResults", "");
        setVal("pf_dischargePlan", "");
        setChecked("pf_dischargeSummaryStatus", false);
    }

    function fill(model) {
        if (!model) return;

        // accept both PascalCase / snake-case / exact column names
        setVal("pf_hpeResults", model.HPE_Results ?? model.hpe_Results ?? model.hpeResults ?? "");
        setVal("pf_dischargePlan", model.DischargePlan ?? model.dischargePlan ?? "");

        const ds = model.DischargeSummary_Status ?? model.dischargeSummary_Status ?? model.dischargeSummaryStatus;
        setChecked("pf_dischargeSummaryStatus", parseAnyToBool(ds));
    }

    function collect() {
        const hpeResults = requireSelectText("pf_hpeResults", "HPE Results: Results is required.");
        const dischargePlan = requireSelectText("pf_dischargePlan", "Discharge: Discharge Plan is required.");
        // Interpret "mandatory checkbox" as: must be checked (same pattern as Patient Assessment Management section)
        requireChecked("pf_dischargeSummaryStatus", "Discharge: Discharge Summary Given must be checked.");

        return {
            HPE_Results: hpeResults,
            DischargePlan: dischargePlan,
            DischargeSummary_Status: true,
        };
    }

    // -----------------------------
    // REGISTER TEMPLATE
    // -----------------------------
    const api = {
        type: "PATIENT FOLLOW UP",
        rootSelector: "#tmplPatientFollowUp",
        init,
        clear,
        fill,
        collect,
    };

    window.StaffPatientTemplates["PATIENT FOLLOW UP"] = api;
})();