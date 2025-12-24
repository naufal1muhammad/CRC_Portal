// @ts-nocheck
(function () {
    // Create namespace once
    window.StaffPatientTemplates = window.StaffPatientTemplates || {};

    // -----------------------------
    // Helpers (scoped to rootEl)
    // -----------------------------
    let rootEl = null;

    function q(id) {
        if (!rootEl) return null;
        return rootEl.querySelector("#" + id);
    }

    function showWrap(wrapId, show) {
        const el = q(wrapId);
        if (!el) return;
        el.style.display = show ? "" : "none";
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

    // Convert server bit-ish values (true/false/1/0/"YES"/"NO") to true/false
    function parseAnyToBool(raw) {
        if (raw === true || raw === false) return raw;
        if (raw === 1 || raw === 0) return raw === 1;
        const v = (raw ?? "").toString().trim().toLowerCase();
        if (v === "1" || v === "true" || v === "yes") return true;
        if (v === "0" || v === "false" || v === "no") return false;
        return false;
    }

    // For <select> that uses option values "true" and "false" (with "" as placeholder)
    function parseBitSelect(raw) {
        const v = (raw ?? "").toString().trim().toLowerCase();
        if (v === "true" || v === "1" || v === "yes") return true;
        if (v === "false" || v === "0" || v === "no") return false;
        return null;
    }

    function bitSelectValue(raw) {
        if (raw === null || raw === undefined || String(raw).trim() === "") return "";
        return parseAnyToBool(raw) ? "true" : "false";
    }

    function requireBitSelect(id, message) {
        const raw = getVal(id);
        const b = parseBitSelect(raw);
        if (b === null) {
            focusEl(id);
            throw new Error(message);
        }
        return b;
    }

    function requireIntSelect(id, message) {
        const raw = getVal(id);
        if (isEmpty(raw)) {
            focusEl(id);
            throw new Error(message);
        }
        const n = parseInt(raw, 10);
        if (Number.isNaN(n)) {
            focusEl(id);
            throw new Error(message);
        }
        return n;
    }

    function requireSelectText(id, message) {
        const v = getVal(id);
        if (isEmpty(v)) {
            focusEl(id);
            throw new Error(message);
        }
        return v;
    }

    function requireNonEmpty(id, message) {
        const v = getVal(id);
        if (isEmpty(v)) {
            focusEl(id);
            throw new Error(message);
        }
        return v;
    }

    // -----------------------------
    // conditional logic
    // -----------------------------
    function refreshConditionals() {
        // Colonoscopy status details required only when INCOMPLETE (false)
        const status = parseBitSelect(getVal("pc_colonoscopyStatus"));
        showWrap("wrap_pc_colonoscopyStatusDetails", status === false);

        // Findings detail wrappers required only when ABNORMAL (false)
        const findings = [
            { sel: "pc_findingsAnus", wrap: "wrap_pc_findingsAnusDetails" },
            { sel: "pc_findingsRectum", wrap: "wrap_pc_findingsRectumDetails" },
            { sel: "pc_findingsSigmoidColon", wrap: "wrap_pc_findingsSigmoidColonDetails" },
            { sel: "pc_findingsDescendingColon", wrap: "wrap_pc_findingsDescendingColonDetails" },
            { sel: "pc_findingsSplenicFlexure", wrap: "wrap_pc_findingsSplenicFlexureDetails" },
            { sel: "pc_findingsTransverseColon", wrap: "wrap_pc_findingsTransverseColonDetails" },
            { sel: "pc_findingsHepaticFlexure", wrap: "wrap_pc_findingsHepaticFlexureDetails" },
            { sel: "pc_findingsAscendingColon", wrap: "wrap_pc_findingsAscendingColonDetails" },
            { sel: "pc_findingsCaecum", wrap: "wrap_pc_findingsCaecumDetails" },
        ];

        findings.forEach((f) => {
            const v = parseBitSelect(getVal(f.sel));
            showWrap(f.wrap, v === false);
        });

        // HPE details required only when YES (true)
        const hpe = parseBitSelect(getVal("pc_hpeStatus"));
        showWrap("wrap_pc_hpeDetails", hpe === true);
    }

    // -----------------------------
    // lifecycle
    // -----------------------------
    function init(root) {
        rootEl = root;

        // wire conditional toggles
        [
            "pc_colonoscopyStatus",
            "pc_findingsAnus",
            "pc_findingsRectum",
            "pc_findingsSigmoidColon",
            "pc_findingsDescendingColon",
            "pc_findingsSplenicFlexure",
            "pc_findingsTransverseColon",
            "pc_findingsHepaticFlexure",
            "pc_findingsAscendingColon",
            "pc_findingsCaecum",
            "pc_hpeStatus",
        ].forEach((id) => {
            const el = q(id);
            if (el) el.addEventListener("change", refreshConditionals);
        });

        refreshConditionals();
    }

    function clear() {
        setVal("pc_colonoscopyStatus", "");
        setVal("pc_colonoscopyStatusDetails", "");
        setVal("pc_bowelPreparation", "");

        setVal("pc_findingsAnus", "");
        setVal("pc_findingsAnusDetails", "");
        setVal("pc_findingsRectum", "");
        setVal("pc_findingsRectumDetails", "");
        setVal("pc_findingsSigmoidColon", "");
        setVal("pc_findingsSigmoidColonDetails", "");
        setVal("pc_findingsDescendingColon", "");
        setVal("pc_findingsDescendingColonDetails", "");
        setVal("pc_findingsSplenicFlexure", "");
        setVal("pc_findingsSplenicFlexureDetails", "");
        setVal("pc_findingsTransverseColon", "");
        setVal("pc_findingsTransverseColonDetails", "");
        setVal("pc_findingsHepaticFlexure", "");
        setVal("pc_findingsHepaticFlexureDetails", "");
        setVal("pc_findingsAscendingColon", "");
        setVal("pc_findingsAscendingColonDetails", "");
        setVal("pc_findingsCaecum", "");
        setVal("pc_findingsCaecumDetails", "");

        setVal("pc_hpeStatus", "");
        setVal("pc_hpeDetails", "");

        setVal("pc_complications", "");
        setVal("pc_complicationsDetails", "");

        setVal("pc_dischargePlan", "");

        refreshConditionals();
    }

    function fill(data) {
        if (!data) return;

        setVal("pc_colonoscopyStatus", bitSelectValue(data.ColonoscopyStatus));
        setVal("pc_colonoscopyStatusDetails", data.ColonoscopyStatus_Details ?? "");
        setVal("pc_bowelPreparation", data.BowelPreparation ?? "");

        setVal("pc_findingsAnus", bitSelectValue(data.Findings_Anus));
        setVal("pc_findingsAnusDetails", data.Findings_AnusDetails ?? "");

        setVal("pc_findingsRectum", bitSelectValue(data.Findings_Rectum));
        setVal("pc_findingsRectumDetails", data.Findings_RectumDetails ?? "");

        setVal("pc_findingsSigmoidColon", bitSelectValue(data.Findings_SigmoidColon));
        setVal("pc_findingsSigmoidColonDetails", data.Findings_SigmoidColonDetails ?? "");

        setVal("pc_findingsDescendingColon", bitSelectValue(data.Findings_DescendingColon));
        setVal("pc_findingsDescendingColonDetails", data.Findings_DescendingColonDetails ?? "");

        setVal("pc_findingsSplenicFlexure", bitSelectValue(data.Findings_SplenicFlexure));
        setVal("pc_findingsSplenicFlexureDetails", data.Findings_SplenicFlexureDetails ?? "");

        setVal("pc_findingsTransverseColon", bitSelectValue(data.Findings_TransverseColon));
        setVal("pc_findingsTransverseColonDetails", data.Findings_TransverseColonDetails ?? "");

        setVal("pc_findingsHepaticFlexure", bitSelectValue(data.Findings_HepaticFlexure));
        setVal("pc_findingsHepaticFlexureDetails", data.Findings_HepaticFlexureDetails ?? "");

        setVal("pc_findingsAscendingColon", bitSelectValue(data.Findings_AscendingColon));
        setVal("pc_findingsAscendingColonDetails", data.Findings_AscendingColonDetails ?? "");

        setVal("pc_findingsCaecum", bitSelectValue(data.Findings_Caecum));
        setVal("pc_findingsCaecumDetails", data.Findings_CaecumDetails ?? "");

        setVal("pc_hpeStatus", bitSelectValue(data.HPE_Status));
        setVal("pc_hpeDetails", data.HPE_Details ?? "");

        setVal("pc_complications", data.Complications ?? "");
        setVal("pc_complicationsDetails", data.Complications_Details ?? "");

        setVal("pc_dischargePlan", data.DischargePlan ?? "");

        refreshConditionals();
    }

    function collect() {
        // Mandatorys
        const colonoscopyStatus = requireBitSelect("pc_colonoscopyStatus", "Colonoscopy Status is required.");
        let colonoscopyStatusDetails = getVal("pc_colonoscopyStatusDetails");
        if (colonoscopyStatus === false) {
            colonoscopyStatusDetails = requireNonEmpty("pc_colonoscopyStatusDetails", "Please specify details for INCOMPLETE colonoscopy status.");
        } else {
            colonoscopyStatusDetails = isEmpty(colonoscopyStatusDetails) ? null : colonoscopyStatusDetails;
        }

        const bowelPrep = requireIntSelect("pc_bowelPreparation", "Bowel Preparedness is required.");

        function collectFinding(selId, detailsId, label) {
            const isNormal = requireBitSelect(selId, label + " is required.");
            let details = getVal(detailsId);
            if (isNormal === false) {
                details = requireNonEmpty(detailsId, "Please specify details for ABNORMAL " + label + ".");
            } else {
                details = isEmpty(details) ? null : details;
            }
            return { isNormal, details };
        }

        const anus = collectFinding("pc_findingsAnus", "pc_findingsAnusDetails", "Findings: Anus");
        const rectum = collectFinding("pc_findingsRectum", "pc_findingsRectumDetails", "Findings: Rectum");
        const sigmoid = collectFinding("pc_findingsSigmoidColon", "pc_findingsSigmoidColonDetails", "Findings: Sigmoid Colon");
        const descending = collectFinding("pc_findingsDescendingColon", "pc_findingsDescendingColonDetails", "Findings: Descending Colon");
        const splenic = collectFinding("pc_findingsSplenicFlexure", "pc_findingsSplenicFlexureDetails", "Findings: Splenic Flexure");
        const transverse = collectFinding("pc_findingsTransverseColon", "pc_findingsTransverseColonDetails", "Findings: Transverse Colon");
        const hepatic = collectFinding("pc_findingsHepaticFlexure", "pc_findingsHepaticFlexureDetails", "Findings: Hepatic Flexure");
        const ascending = collectFinding("pc_findingsAscendingColon", "pc_findingsAscendingColonDetails", "Findings: Ascending Colon");
        const caecum = collectFinding("pc_findingsCaecum", "pc_findingsCaecumDetails", "Findings: Caecum");

        const hpeStatus = requireBitSelect("pc_hpeStatus", "HPE Taken is required.");
        let hpeDetails = getVal("pc_hpeDetails");
        if (hpeStatus === true) {
            hpeDetails = requireNonEmpty("pc_hpeDetails", "Please specify HPE details when HPE Taken is YES.");
        } else {
            hpeDetails = isEmpty(hpeDetails) ? null : hpeDetails;
        }

        const complications = requireSelectText("pc_complications", "Procedure Complications is required.");
        const complicationsDetailsRaw = getVal("pc_complicationsDetails");
        const dischargePlan = requireSelectText("pc_dischargePlan", "Discharge Plan is required.");

        return {
            ColonoscopyStatus: colonoscopyStatus,
            ColonoscopyStatus_Details: colonoscopyStatusDetails,
            BowelPreparation: bowelPrep,

            Findings_Anus: anus.isNormal,
            Findings_AnusDetails: anus.details,
            Findings_Rectum: rectum.isNormal,
            Findings_RectumDetails: rectum.details,
            Findings_SigmoidColon: sigmoid.isNormal,
            Findings_SigmoidColonDetails: sigmoid.details,
            Findings_DescendingColon: descending.isNormal,
            Findings_DescendingColonDetails: descending.details,
            Findings_SplenicFlexure: splenic.isNormal,
            Findings_SplenicFlexureDetails: splenic.details,
            Findings_TransverseColon: transverse.isNormal,
            Findings_TransverseColonDetails: transverse.details,
            Findings_HepaticFlexure: hepatic.isNormal,
            Findings_HepaticFlexureDetails: hepatic.details,
            Findings_AscendingColon: ascending.isNormal,
            Findings_AscendingColonDetails: ascending.details,
            Findings_Caecum: caecum.isNormal,
            Findings_CaecumDetails: caecum.details,

            HPE_Status: hpeStatus,
            HPE_Details: hpeDetails,

            Complications: complications,
            Complications_Details: isEmpty(complicationsDetailsRaw) ? null : complicationsDetailsRaw,

            DischargePlan: dischargePlan,
        };
    }

    // -----------------------------
    // REGISTER TEMPLATE (key must match dropdown value)
    // -----------------------------
    const api = {
        type: "PATIENT COLONOSCOPY",
        rootSelector: "#tmplPatientColonoscopy",
        init,
        clear,
        fill,
        collect,
    };

    window.StaffPatientTemplates["PATIENT COLONOSCOPY"] = api;
})();