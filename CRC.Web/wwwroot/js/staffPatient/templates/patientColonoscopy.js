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

    // -----------------------------------------------------------------
    // Anomaly Modal: field IDs inside the modal (must match cshtml IDs)
    // -----------------------------------------------------------------
    const ANOMALY_FIELDS = [
        "pcAnomaly_TypeOfAnomaly",
        "pcAnomaly_Size",
        "pcAnomaly_Count",
        "pcAnomaly_Morphology",
        "pcAnomaly_JNETClassification",
        "pcAnomaly_WASPClassification",
        "pcAnomaly_KudoPitPattern",
        "pcAnomaly_LSTSubtype",
    ];

    // JSON property keys (used in the stored JSON object)
    const ANOMALY_KEYS = [
        "TypeOfAnomaly",
        "Size",
        "Count",
        "Morphology",
        "JNETClassification",
        "WASPClassification",
        "KudoPitPattern",
        "LSTSubtype",
    ];

    // The organ currently being edited in the modal
    let _activeOrgan = null;
    let _anomalyModal = null;

    // -----------------------------------------------------------------
    // Anomaly Modal helpers
    // -----------------------------------------------------------------

    /** Read the modal form and return a JSON object (only non-empty fields). */
    function readModalFields() {
        const obj = {};
        let hasAny = false;
        for (let i = 0; i < ANOMALY_FIELDS.length; i++) {
            const el = q(ANOMALY_FIELDS[i]);
            const v = el ? (el.value || "").trim() : "";
            if (v) {
                obj[ANOMALY_KEYS[i]] = v;
                hasAny = true;
            }
        }
        return hasAny ? obj : null;
    }

    /** Populate the modal form from a JSON object (or clear if null). */
    function writeModalFields(obj) {
        for (let i = 0; i < ANOMALY_FIELDS.length; i++) {
            const el = q(ANOMALY_FIELDS[i]);
            if (!el) continue;
            el.value = (obj && obj[ANOMALY_KEYS[i]]) ? obj[ANOMALY_KEYS[i]] : "";
        }
    }

    /** Clear all modal selects. */
    function clearModalFields() {
        writeModalFields(null);
    }

    /**
     * Safely parse JSON from a hidden input value.
     * Supports both JSON strings and legacy plain-text (returns null for plain text).
     */
    function parseDetailsJson(raw) {
        if (!raw || typeof raw !== "string") return null;
        const trimmed = raw.trim();
        if (!trimmed || trimmed === "null") return null;
        // If it looks like JSON object, parse it
        if (trimmed.startsWith("{")) {
            try { return JSON.parse(trimmed); } catch { return null; }
        }
        // Legacy: plain text from old schema – ignore (treat as no data)
        return null;
    }

    /** Get the hidden input ID for an organ's details. */
    function detailsInputId(organ) {
        return "pc_findings" + organ + "Details";
    }

    /** Get the badge element ID for an organ. */
    function badgeId(organ) {
        return "pc_findings" + organ + "Badge";
    }

    /** Refresh the "Filled" badge visibility for a given organ. */
    function refreshBadge(organ) {
        const badge = q(badgeId(organ));
        if (!badge) return;
        const raw = getVal(detailsInputId(organ));
        const obj = parseDetailsJson(raw);
        if (obj && Object.keys(obj).length > 0) {
            badge.classList.remove("d-none");
            badge.classList.remove("bg-secondary");
            badge.classList.add("bg-success");
            badge.textContent = Object.keys(obj).length + " field(s)";
        } else {
            badge.classList.add("d-none");
            badge.classList.remove("bg-success");
            badge.classList.add("bg-secondary");
            badge.textContent = "Filled";
        }
    }

    /** Open the anomaly modal for a given organ. */
    function openAnomalyModal(organ) {
        _activeOrgan = organ;

        // Set title
        const titleEl = q("pcAnomalyOrganTitle");
        if (titleEl) {
            // Convert camelCase organ key to readable name
            const readableMap = {
                "Anus": "Anus",
                "Rectum": "Rectum",
                "SigmoidColon": "Sigmoid Colon",
                "DescendingColon": "Descending Colon",
                "SplenicFlexure": "Splenic Flexure",
                "TransverseColon": "Transverse Colon",
                "HepaticFlexure": "Hepatic Flexure",
                "AscendingColon": "Ascending Colon",
                "Caecum": "Caecum",
            };
            titleEl.textContent = readableMap[organ] || organ;
        }

        // Load existing JSON data into modal fields
        const raw = getVal(detailsInputId(organ));
        const obj = parseDetailsJson(raw);
        writeModalFields(obj);

        // Show the modal
        if (_anomalyModal) {
            _anomalyModal.show();
        }
    }

    // -----------------------------------------------------------------
    // conditional logic
    // -----------------------------------------------------------------
    const FINDINGS = [
        { sel: "pc_findingsAnus", wrap: "wrap_pc_findingsAnusDetails", organ: "Anus" },
        { sel: "pc_findingsRectum", wrap: "wrap_pc_findingsRectumDetails", organ: "Rectum" },
        { sel: "pc_findingsSigmoidColon", wrap: "wrap_pc_findingsSigmoidColonDetails", organ: "SigmoidColon" },
        { sel: "pc_findingsDescendingColon", wrap: "wrap_pc_findingsDescendingColonDetails", organ: "DescendingColon" },
        { sel: "pc_findingsSplenicFlexure", wrap: "wrap_pc_findingsSplenicFlexureDetails", organ: "SplenicFlexure" },
        { sel: "pc_findingsTransverseColon", wrap: "wrap_pc_findingsTransverseColonDetails", organ: "TransverseColon" },
        { sel: "pc_findingsHepaticFlexure", wrap: "wrap_pc_findingsHepaticFlexureDetails", organ: "HepaticFlexure" },
        { sel: "pc_findingsAscendingColon", wrap: "wrap_pc_findingsAscendingColonDetails", organ: "AscendingColon" },
        { sel: "pc_findingsCaecum", wrap: "wrap_pc_findingsCaecumDetails", organ: "Caecum" },
    ];

    function refreshConditionals() {
        // Colonoscopy status details required only when INCOMPLETE (false)
        const status = parseBitSelect(getVal("pc_colonoscopyStatus"));
        showWrap("wrap_pc_colonoscopyStatusDetails", status === false);

        // Findings detail wrappers shown only when ABNORMAL (false)
        FINDINGS.forEach(function (f) {
            const v = parseBitSelect(getVal(f.sel));
            const isAbnormal = v === false;
            showWrap(f.wrap, isAbnormal);

            // If switching back to NORMAL, clear the stored JSON
            if (!isAbnormal) {
                setVal(detailsInputId(f.organ), "");
                refreshBadge(f.organ);
            }
        });

        // HPE details required only when YES (true)
        const hpe = parseBitSelect(getVal("pc_hpeStatus"));
        showWrap("wrap_pc_hpeDetails", hpe === true);
    }

    // -----------------------------------------------------------------
    // lifecycle
    // -----------------------------------------------------------------
    function init(root) {
        rootEl = root;

        // Initialise Bootstrap modal instance for anomaly modal
        var modalEl = q("pcAnomalyModal");
        if (modalEl && typeof bootstrap !== "undefined" && bootstrap.Modal) {
            _anomalyModal = new bootstrap.Modal(modalEl);
        }

        // Wire conditional toggles
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
        ].forEach(function (id) {
            var el = q(id);
            if (el) el.addEventListener("change", refreshConditionals);
        });

        // Wire anomaly buttons (open modal)
        rootEl.querySelectorAll(".pc-anomaly-btn").forEach(function (btn) {
            btn.addEventListener("click", function () {
                var organ = btn.getAttribute("data-organ");
                if (organ) openAnomalyModal(organ);
            });
        });

        // Wire modal Save button
        var saveBtn = q("pcAnomalySaveBtn");
        if (saveBtn) {
            saveBtn.addEventListener("click", function () {
                if (!_activeOrgan) return;
                var obj = readModalFields();
                var json = obj ? JSON.stringify(obj) : "";
                setVal(detailsInputId(_activeOrgan), json);
                refreshBadge(_activeOrgan);
                if (_anomalyModal) _anomalyModal.hide();
            });
        }

        // Wire modal Clear button
        var clearBtn = q("pcAnomalyClearBtn");
        if (clearBtn) {
            clearBtn.addEventListener("click", function () {
                clearModalFields();
            });
        }

        refreshConditionals();
    }

    function clear() {
        setVal("pc_colonoscopyStatus", "");
        setVal("pc_colonoscopyStatusDetails", "");
        setVal("pc_bowelPreparation", "");

        FINDINGS.forEach(function (f) {
            setVal(f.sel, "");
            setVal(detailsInputId(f.organ), "");
            refreshBadge(f.organ);
        });

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

        // Map of server column name suffix → organ key
        var organMap = {
            "Anus": "Anus",
            "Rectum": "Rectum",
            "SigmoidColon": "SigmoidColon",
            "DescendingColon": "DescendingColon",
            "SplenicFlexure": "SplenicFlexure",
            "TransverseColon": "TransverseColon",
            "HepaticFlexure": "HepaticFlexure",
            "AscendingColon": "AscendingColon",
            "Caecum": "Caecum",
        };

        Object.keys(organMap).forEach(function (key) {
            var organ = organMap[key];
            // Set the Normal/Abnormal select
            setVal("pc_findings" + key, bitSelectValue(data["Findings_" + key]));

            // Set the details hidden input (JSON string from server or legacy text)
            var detailsRaw = data["Findings_" + key + "Details"] ?? "";

            // If it's a string, use as-is (could be JSON or legacy text); if object, stringify
            if (typeof detailsRaw === "object" && detailsRaw !== null) {
                setVal(detailsInputId(organ), JSON.stringify(detailsRaw));
            } else {
                setVal(detailsInputId(organ), detailsRaw);
            }
            refreshBadge(organ);
        });

        setVal("pc_hpeStatus", bitSelectValue(data.HPE_Status));
        setVal("pc_hpeDetails", data.HPE_Details ?? "");

        setVal("pc_complications", data.Complications ?? "");
        setVal("pc_complicationsDetails", data.Complications_Details ?? "");

        setVal("pc_dischargePlan", data.DischargePlan ?? "");

        refreshConditionals();
    }

    function collect() {
        // Mandatorys
        var colonoscopyStatus = requireBitSelect("pc_colonoscopyStatus", "Colonoscopy Status is required.");
        var colonoscopyStatusDetails = getVal("pc_colonoscopyStatusDetails");
        if (colonoscopyStatus === false) {
            colonoscopyStatusDetails = requireNonEmpty("pc_colonoscopyStatusDetails", "Please specify details for INCOMPLETE colonoscopy status.");
        } else {
            colonoscopyStatusDetails = isEmpty(colonoscopyStatusDetails) ? null : colonoscopyStatusDetails;
        }

        var bowelPrep = requireIntSelect("pc_bowelPreparation", "Bowel Preparedness is required.");

        /**
         * Collect finding for an organ.
         * The Details column now stores JSON (or null).
         * ABNORMAL is allowed without filling the anomaly modal (not mandatory).
         */
        function collectFinding(selId, organ, label) {
            var isNormal = requireBitSelect(selId, label + " is required.");
            var detailsRaw = getVal(detailsInputId(organ));
            var details = null;

            if (isNormal === false) {
                // ABNORMAL – details are optional (modal/table is not mandatory)
                if (!isEmpty(detailsRaw)) {
                    // Validate it's valid JSON if it looks like JSON
                    var parsed = parseDetailsJson(detailsRaw);
                    if (parsed) {
                        details = JSON.stringify(parsed);
                    } else if (detailsRaw.trim()) {
                        // Legacy text or invalid – pass through as-is
                        details = detailsRaw.trim();
                    }
                }
            }
            // If NORMAL, details are cleared (null)
            return { isNormal: isNormal, details: details };
        }

        var anus = collectFinding("pc_findingsAnus", "Anus", "Findings: Anus");
        var rectum = collectFinding("pc_findingsRectum", "Rectum", "Findings: Rectum");
        var sigmoid = collectFinding("pc_findingsSigmoidColon", "SigmoidColon", "Findings: Sigmoid Colon");
        var descending = collectFinding("pc_findingsDescendingColon", "DescendingColon", "Findings: Descending Colon");
        var splenic = collectFinding("pc_findingsSplenicFlexure", "SplenicFlexure", "Findings: Splenic Flexure");
        var transverse = collectFinding("pc_findingsTransverseColon", "TransverseColon", "Findings: Transverse Colon");
        var hepatic = collectFinding("pc_findingsHepaticFlexure", "HepaticFlexure", "Findings: Hepatic Flexure");
        var ascending = collectFinding("pc_findingsAscendingColon", "AscendingColon", "Findings: Ascending Colon");
        var caecum = collectFinding("pc_findingsCaecum", "Caecum", "Findings: Caecum");

        var hpeStatus = requireBitSelect("pc_hpeStatus", "HPE Taken is required.");
        var hpeDetails = getVal("pc_hpeDetails");
        if (hpeStatus === true) {
            hpeDetails = requireNonEmpty("pc_hpeDetails", "Please specify HPE details when HPE Taken is YES.");
        } else {
            hpeDetails = isEmpty(hpeDetails) ? null : hpeDetails;
        }

        var complications = requireSelectText("pc_complications", "Procedure Complications is required.");
        var complicationsDetailsRaw = getVal("pc_complicationsDetails");
        var dischargePlan = requireSelectText("pc_dischargePlan", "Discharge Plan is required.");

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
    var api = {
        type: "PATIENT COLONOSCOPY",
        rootSelector: "#tmplPatientColonoscopy",
        init: init,
        clear: clear,
        fill: fill,
        collect: collect,
    };

    window.StaffPatientTemplates["PATIENT COLONOSCOPY"] = api;
})();