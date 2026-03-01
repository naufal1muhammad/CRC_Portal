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

    function parseAnyToBool(raw) {
        if (raw === true || raw === false) return raw;
        if (raw === 1 || raw === 0) return raw === 1;
        const v = (raw ?? "").toString().trim().toLowerCase();
        if (v === "1" || v === "true" || v === "yes") return true;
        if (v === "0" || v === "false" || v === "no") return false;
        return false;
    }

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
        var v = parseBitSelect(getVal(id));
        if (v === null) {
            focusEl(id);
            throw new Error(message);
        }
        return v;
    }

    function requireIntSelect(id, message) {
        var raw = getVal(id);
        if (raw === "" || raw === null || raw === undefined) {
            focusEl(id);
            throw new Error(message);
        }
        var n = parseInt(raw, 10);
        if (isNaN(n)) {
            focusEl(id);
            throw new Error(message);
        }
        return n;
    }

    function requireSelectText(id, message) {
        var v = getVal(id);
        if (isEmpty(v)) {
            focusEl(id);
            throw new Error(message);
        }
        return v;
    }

    function requireNonEmpty(id, message) {
        var v = getVal(id);
        if (isEmpty(v)) {
            focusEl(id);
            throw new Error(message);
        }
        return v;
    }

    // -----------------------------------------------------------------
    // ANOMALY INLINE FIELDS (findings details stored as JSON)
    // -----------------------------------------------------------------
    const ANOMALY_KEYS = ["TypeOfAnomaly", "Size", "Count", "Morphology", "Intervention", "Remarks"];

    function inlineFieldId(organ, fieldKey) {
        return "pcAnomaly_" + organ + "_" + fieldKey;
    }

    function readInlineFields(organ) {
        var obj = {};
        var hasAny = false;
        for (var i = 0; i < ANOMALY_KEYS.length; i++) {
            var el = q(inlineFieldId(organ, ANOMALY_KEYS[i]));
            var v = el ? (el.value || "").trim() : "";
            if (v) {
                obj[ANOMALY_KEYS[i]] = v;
                hasAny = true;
            }
        }
        return hasAny ? obj : null;
    }

    function writeInlineFields(organ, obj) {
        for (var i = 0; i < ANOMALY_KEYS.length; i++) {
            var el = q(inlineFieldId(organ, ANOMALY_KEYS[i]));
            if (!el) continue;
            el.value = (obj && obj[ANOMALY_KEYS[i]]) ? obj[ANOMALY_KEYS[i]] : "";
        }
    }

    function clearInlineFields(organ) {
        writeInlineFields(organ, null);
    }

    function parseDetailsJson(raw) {
        if (!raw || typeof raw !== "string") return null;
        var trimmed = raw.trim();
        if (!trimmed || trimmed === "null") return null;
        if (trimmed.startsWith("{")) {
            try { return JSON.parse(trimmed); } catch (e) { return null; }
        }
        return null;
    }

    function parseJsonArray(raw) {
        if (!raw || typeof raw !== "string") return null;
        var trimmed = raw.trim();
        if (!trimmed || trimmed === "null") return null;
        if (trimmed.startsWith("[")) {
            try { return JSON.parse(trimmed); } catch (e) { return null; }
        }
        return null;
    }

    // -----------------------------------------------------------------
    // MEDICATION TABLE LOGIC
    // -----------------------------------------------------------------
    var MEDICATION_OPTIONS = [
        "Midazolam",
        "Fentanyl",
        "Propofol",
        "Meperidine",
        "Diphenhydramine",
        "Glucagon"
    ];

    var medicationRowCounter = 0;

    function buildMedicationSelectHtml(selectedValue) {
        var html = '<select class="form-select form-select-sm pc-med-name">';
        html += '<option value="">-- Select --</option>';
        for (var i = 0; i < MEDICATION_OPTIONS.length; i++) {
            var opt = MEDICATION_OPTIONS[i];
            var sel = (selectedValue === opt) ? ' selected' : '';
            html += '<option value="' + opt + '"' + sel + '>' + opt + '</option>';
        }
        html += '</select>';
        return html;
    }

    function addMedicationRow(medication, remarks) {
        var tbody = q("pc_medicationTableBody");
        if (!tbody) return;

        medicationRowCounter++;
        var rowId = "pc_medRow_" + medicationRowCounter;

        var tr = document.createElement("tr");
        tr.id = rowId;

        // Medication dropdown cell
        var tdMed = document.createElement("td");
        tdMed.innerHTML = buildMedicationSelectHtml(medication || "");
        tr.appendChild(tdMed);

        // Remarks text cell
        var tdRemarks = document.createElement("td");
        tdRemarks.innerHTML = '<input type="text" class="form-control form-control-sm pc-med-remarks" maxlength="500" value="' +
            ((remarks || "").replace(/"/g, "&quot;")) + '" />';
        tr.appendChild(tdRemarks);

        // Actions cell (delete button)
        var tdActions = document.createElement("td");
        tdActions.className = "text-center";
        tdActions.innerHTML =
            '<button type="button" class="btn btn-sm btn-outline-danger pc-med-delete" title="Delete row">' +
            '<i class="fas fa-trash-alt"></i>' +
            '</button>';
        tr.appendChild(tdActions);

        tbody.appendChild(tr);
    }

    function clearMedicationTable() {
        var tbody = q("pc_medicationTableBody");
        if (!tbody) return;
        tbody.innerHTML = "";
        medicationRowCounter = 0;
    }

    function collectMedicationData() {
        var tbody = q("pc_medicationTableBody");
        if (!tbody) return null;

        var rows = tbody.querySelectorAll("tr");
        if (!rows || rows.length === 0) return null;

        var data = [];
        for (var i = 0; i < rows.length; i++) {
            var row = rows[i];
            var medSelect = row.querySelector(".pc-med-name");
            var remarksInput = row.querySelector(".pc-med-remarks");

            var medVal = medSelect ? (medSelect.value || "").trim() : "";
            var remarksVal = remarksInput ? (remarksInput.value || "").trim() : "";

            // Only include rows where medication is selected
            if (medVal) {
                data.push({
                    Medication: medVal,
                    Remarks: remarksVal
                });
            }
        }

        return data.length > 0 ? JSON.stringify(data) : null;
    }

    function fillMedicationTable(raw) {
        clearMedicationTable();

        var arr = null;
        if (typeof raw === "string" && raw.trim()) {
            arr = parseJsonArray(raw);
        } else if (Array.isArray(raw)) {
            arr = raw;
        }

        if (!arr || !Array.isArray(arr)) return;

        for (var i = 0; i < arr.length; i++) {
            var item = arr[i];
            addMedicationRow(
                item.Medication || item.medication || "",
                item.Remarks || item.remarks || ""
            );
        }
    }

    function initMedicationEvents() {
        // Add row button
        var addBtn = q("pc_addMedicationRow");
        if (addBtn) {
            addBtn.addEventListener("click", function () {
                addMedicationRow("", "");
            });
        }

        // Delegated delete handler on the table body
        var tbody = q("pc_medicationTableBody");
        if (tbody) {
            tbody.addEventListener("click", function (e) {
                var deleteBtn = e.target.closest(".pc-med-delete");
                if (deleteBtn) {
                    var row = deleteBtn.closest("tr");
                    if (row) {
                        row.remove();
                    }
                }
            });
        }
    }

    // -----------------------------------------------------------------
    // conditional logic
    // -----------------------------------------------------------------
    const FINDINGS = [
        { sel: "pc_findingsAnus", wrap: "wrap_pc_findingsAnusDetails", organ: "Anus", label: "Anus" },
        { sel: "pc_findingsRectum", wrap: "wrap_pc_findingsRectumDetails", organ: "Rectum", label: "Rectum" },
        { sel: "pc_findingsSigmoidColon", wrap: "wrap_pc_findingsSigmoidColonDetails", organ: "SigmoidColon", label: "Sigmoid Colon" },
        { sel: "pc_findingsDescendingColon", wrap: "wrap_pc_findingsDescendingColonDetails", organ: "DescendingColon", label: "Descending Colon" },
        { sel: "pc_findingsSplenicFlexure", wrap: "wrap_pc_findingsSplenicFlexureDetails", organ: "SplenicFlexure", label: "Splenic Flexure" },
        { sel: "pc_findingsTransverseColon", wrap: "wrap_pc_findingsTransverseColonDetails", organ: "TransverseColon", label: "Transverse Colon" },
        { sel: "pc_findingsHepaticFlexure", wrap: "wrap_pc_findingsHepaticFlexureDetails", organ: "HepaticFlexure", label: "Hepatic Flexure" },
        { sel: "pc_findingsAscendingColon", wrap: "wrap_pc_findingsAscendingColonDetails", organ: "AscendingColon", label: "Ascending Colon" },
        { sel: "pc_findingsCaecum", wrap: "wrap_pc_findingsCaecumDetails", organ: "Caecum", label: "Caecum" },
    ];

    function stampAnomalyTemplates() {
        var tmpl = rootEl.querySelector("#pcAnomalyTemplate");
        if (!tmpl) return;

        FINDINGS.forEach(function (f) {
            var wrapper = q(f.wrap);
            if (!wrapper) return;

            var clone = tmpl.content.cloneNode(true);

            var labelEl = clone.querySelector("[data-anomaly-label]");
            if (labelEl) labelEl.textContent = f.label;

            clone.querySelectorAll("[data-anomaly-field]").forEach(function (sel) {
                sel.id = "pcAnomaly_" + f.organ + "_" + sel.getAttribute("data-anomaly-field");
                sel.removeAttribute("data-anomaly-field");
            });

            wrapper.appendChild(clone);
        });
    }

    function refreshConditionals() {
        var status = parseBitSelect(getVal("pc_colonoscopyStatus"));
        showWrap("wrap_pc_colonoscopyStatusDetails", status === false);

        FINDINGS.forEach(function (f) {
            var v = parseBitSelect(getVal(f.sel));
            var isAbnormal = v === false;
            showWrap(f.wrap, isAbnormal);

            if (!isAbnormal) {
                clearInlineFields(f.organ);
            }
        });

        var hpe = parseBitSelect(getVal("pc_hpeStatus"));
        showWrap("wrap_pc_hpeDetails", hpe === true);
    }

    // -----------------------------------------------------------------
    // lifecycle
    // -----------------------------------------------------------------
    function init(root) {
        rootEl = root;

        stampAnomalyTemplates();

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

        // Initialize medication table events
        initMedicationEvents();

        refreshConditionals();
    }

    function clear() {
        setVal("pc_colonoscopyStatus", "");
        setVal("pc_colonoscopyStatusDetails", "");
        setVal("pc_bowelPreparation", "");

        FINDINGS.forEach(function (f) {
            setVal(f.sel, "");
            clearInlineFields(f.organ);
        });

        setVal("pc_hpeStatus", "");
        setVal("pc_hpeDetails", "");

        setVal("pc_complications", "");
        setVal("pc_complicationsDetails", "");

        // Clear medication table
        clearMedicationTable();

        setVal("pc_dischargePlan", "");

        refreshConditionals();
    }

    function fill(data) {
        if (!data) return;

        setVal("pc_colonoscopyStatus", bitSelectValue(data.ColonoscopyStatus));
        setVal("pc_colonoscopyStatusDetails", data.ColonoscopyStatus_Details ?? "");
        setVal("pc_bowelPreparation", data.BowelPreparation ?? "");

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
            setVal("pc_findings" + key, bitSelectValue(data["Findings_" + key]));

            var detailsRaw = data["Findings_" + key + "Details"] ?? "";
            var obj = null;

            if (typeof detailsRaw === "object" && detailsRaw !== null) {
                obj = detailsRaw;
            } else if (typeof detailsRaw === "string" && detailsRaw.trim()) {
                obj = parseDetailsJson(detailsRaw);
            }

            writeInlineFields(organ, obj);
        });

        setVal("pc_hpeStatus", bitSelectValue(data.HPE_Status));
        setVal("pc_hpeDetails", data.HPE_Details ?? "");

        setVal("pc_complications", data.Complications ?? "");
        setVal("pc_complicationsDetails", data.Complications_Details ?? "");

        // Fill medication table from JSON
        fillMedicationTable(data.Medication_Details ?? data.medicationDetails ?? "");

        setVal("pc_dischargePlan", data.DischargePlan ?? "");

        refreshConditionals();
    }

    function collect() {
        var colonoscopyStatus = requireBitSelect("pc_colonoscopyStatus", "Colonoscopy Status is required.");
        var colonoscopyStatusDetails = getVal("pc_colonoscopyStatusDetails");
        if (colonoscopyStatus === false) {
            colonoscopyStatusDetails = requireNonEmpty("pc_colonoscopyStatusDetails", "Please specify details for INCOMPLETE colonoscopy status.");
        } else {
            colonoscopyStatusDetails = isEmpty(colonoscopyStatusDetails) ? null : colonoscopyStatusDetails;
        }

        var bowelPrep = requireIntSelect("pc_bowelPreparation", "Bowel Preparedness is required.");

        function collectFinding(selId, organ, label) {
            var isNormal = requireBitSelect(selId, label + " is required.");
            var details = null;

            if (isNormal === false) {
                var obj = readInlineFields(organ);
                if (obj) {
                    details = JSON.stringify(obj);
                }
            }
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

        // Collect medication data (optional – not mandatory)
        var medicationDetails = collectMedicationData();

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

            Medication_Details: medicationDetails,

            DischargePlan: dischargePlan,
        };
    }

    // -----------------------------
    // REGISTER TEMPLATE
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