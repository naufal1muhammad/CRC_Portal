// @ts-nocheck
(function() {
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
        if (!el) return; // wrapper optional
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

    // Accept YES/NO, yes/no, 1/0, true/false
    function parseYesNoToBit(raw) {
        const v = (raw ?? "").toString().trim().toLowerCase();
        if (v === "yes" || v === "1" || v === "true") return true;
        if (v === "no" || v === "0" || v === "false") return false;
        return null; // not selected / invalid
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
    function bitSelectValue(raw) {
        if (raw === null || raw === undefined || String(raw).trim() === "") return "";
        return parseAnyToBool(raw) ? "true" : "false";
    }

    function requireNonEmpty(id, message) {
        const v = getVal(id);
        if (isEmpty(v)) {
            focusEl(id);
            throw new Error(message);
        }
        return v;
    }

    function requireYesNoBit(id, message) {
        const raw = getVal(id);
        const b = parseYesNoToBit(raw);
        if (b === null) {
            focusEl(id);
            throw new Error(message);
        }
        return b;
    }

    function requireDietBit(id, message) {
        // Expect values:
        // 0 = VEGETARIAN
        // 1 = NON-VEGETARIAN
        const raw = getVal(id);
        if (isEmpty(raw)) {
            focusEl(id);
            throw new Error(message);
        }
        if (raw !== "0" && raw !== "1") {
            focusEl(id);
            throw new Error(message);
        }
        return raw === "1";
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

    // Date inputs return "YYYY-MM-DD" or ""
    function getDateOrNull(id) {
        const v = getVal(id);
        return isEmpty(v) ? null : v;
    }

    function setDate(id, value) {
        // Accept "YYYY-MM-DD", ISO date, or null
        const el = q(id);
        if (!el) return;

        if (!value) {
            el.value = "";
            return;
        }

        // If already YYYY-MM-DD
        const s = value.toString().trim();
        if (/^\d{4}-\d{2}-\d{2}$/.test(s)) {
            el.value = s;
            return;
        }

        // If it’s a full datetime string, try parse
        const d = new Date(s);
        if (!isNaN(d.getTime())) {
            const yyyy = d.getFullYear();
            const mm = String(d.getMonth() + 1).padStart(2, "0");
            const dd = String(d.getDate()).padStart(2, "0");
            el.value = `${yyyy}-${mm}-${dd}`;
            return;
        }

        // fallback
        el.value = "";
    }

    // -----------------------------
    // Conditional visibility
    // -----------------------------
    function refreshConditionals() {
        const allergyMed = parseYesNoToBit(getVal("pa_allergyMedication"));
        const allergyFood = parseYesNoToBit(getVal("pa_allergyFood"));

        const antiCoag = parseYesNoToBit(getVal("pa_medAnticoagulant"));
        const narc = parseYesNoToBit(getVal("pa_medNarcotics"));
        const ins = parseYesNoToBit(getVal("pa_medInsulin"));
        const antiHyp = parseYesNoToBit(getVal("pa_medAntiHypertensives"));

        showWrap("wrap_pa_allergyMedicationDetails", allergyMed === true);
        showWrap("wrap_pa_allergyFoodDetails", allergyFood === true);

        showWrap("wrap_pa_medAnticoagulantDetails", antiCoag === true);
        showWrap("wrap_pa_medNarcoticsDetails", narc === true);
        showWrap("wrap_pa_medInsulinDetails", ins === true);
        showWrap("wrap_pa_medAntiHypertensivesDetails", antiHyp === true);

        // If switched to NO, clear details so we don't accidentally save old text
        if (allergyMed === false) setVal("pa_allergyMedicationDetails", "");
        if (allergyFood === false) setVal("pa_allergyFoodDetails", "");

        if (antiCoag === false) setVal("pa_medAnticoagulantDetails", "");
        if (narc === false) setVal("pa_medNarcoticsDetails", "");
        if (ins === false) setVal("pa_medInsulinDetails", "");
        if (antiHyp === false) setVal("pa_medAntiHypertensivesDetails", "");
    }

    function wireConditionalEvents() {
        const ids = [
            "pa_allergyMedication",
            "pa_allergyFood",
            "pa_medAnticoagulant",
            "pa_medNarcotics",
            "pa_medInsulin",
            "pa_medAntiHypertensives",
        ];

        ids.forEach((id) => {
            const el = q(id);
            if (!el) return;
            el.addEventListener("change", refreshConditionals);
        });
    }

    // -----------------------------
    // Public API: init / clear / fill / collect
    // -----------------------------
    function init(el) {
        rootEl = el;
        wireConditionalEvents();
        refreshConditionals();
    }

    function clear() {
        if (!rootEl) return;

        // Dates
        setVal("pa_iFOBTPositiveDate", "");
        setVal("pa_previousScopeDate", "");

        // Risks
        setVal("pa_risksSmoking", "");
        setVal("pa_risksAlcohol", "");
        setVal("pa_risksIBD", "");
        setVal("pa_risksDiet", "");
        setVal("pa_risksSedentary", "");

        // Symptoms
        setVal("pa_sympWeightLoss", "");
        setVal("pa_sympAppetiteLoss", "");
        setVal("pa_sympLethargic", "");
        setVal("pa_sympAbdominalPain", "");
        setVal("pa_sympConstipation", "");
        setVal("pa_sympDiarrhea", "");
        setVal("pa_sympRectalBleedingMucous", "");
        setVal("pa_sympRectalBleedingNoMucous", "");
        setVal("pa_sympTenesmus", "");

        // Medical history
        setVal("pa_mhDiabetes", "");
        setVal("pa_mhHypertension", "");
        setVal("pa_mhDyslipidemia", "");
        setVal("pa_mhBleeding", "");
        setVal("pa_mhAsthma", "");

        // Allergy
        setVal("pa_allergyMedication", "");
        setVal("pa_allergyMedicationDetails", "");
        setVal("pa_allergyFood", "");
        setVal("pa_allergyFoodDetails", "");

        // Medication history
        setVal("pa_medAnticoagulant", "");
        setVal("pa_medAnticoagulantDetails", "");
        setVal("pa_medNarcotics", "");
        setVal("pa_medNarcoticsDetails", "");
        setVal("pa_medInsulin", "");
        setVal("pa_medInsulinDetails", "");
        setVal("pa_medAntiHypertensives", "");
        setVal("pa_medAntiHypertensivesDetails", "");

        // Family history
        setVal("pa_fhFirstDegree", "");
        setVal("pa_fhSecondDegree", "");

        // Physical exam
        setVal("pa_physicalExamDetails", "");

        // Investigation
        setVal("pa_invFBC", "");
        setVal("pa_invBUSE", "");
        setVal("pa_invRBS", "");
        setVal("pa_invLFT", "");
        setVal("pa_invCoag", "");

        // Management (checkboxes)
        setChecked("pa_mgmtBowelPrep", false);
        setChecked("pa_mgmtProcedure", false);
        setChecked("pa_mgmtConsent", false);
        setChecked("pa_mgmtAdvise", false);

        refreshConditionals();
    }

    function fill(data) {
        if (!rootEl) return;
        const d = data || {};

        // Dates
        setDate("pa_iFOBTPositiveDate", d.iFOBTPositive_Date ?? d.iFOBTPositiveDate ?? d.ifobtPositiveDate);
        setDate("pa_previousScopeDate", d.PreviousScope_Date ?? d.previousScopeDate);

        // Risks (select values are "true"/"false")
        setVal("pa_risksSmoking", bitSelectValue(d.Risks_Smoking ?? d.risksSmoking));
        setVal("pa_risksAlcohol", bitSelectValue(d.Risks_AlcoholConsumption ?? d.risksAlcoholConsumption));
        setVal("pa_risksIBD", bitSelectValue(d.Risks_InflammatoryBowelDisease ?? d.risksInflammatoryBowelDisease));
        setVal("pa_risksDiet", parseAnyToBool(d.Risks_Diet ?? d.risksDiet) ? "1" : "0"); // 1 = Non-Veg, 0 = Veg
        setVal("pa_risksSedentary", (d.Risks_SedentaryLifestyle ?? d.risksSedentaryLifestyle ?? "").toString());

        // Symptoms
        setVal("pa_sympWeightLoss", bitSelectValue(d.Symptoms_WeightLoss ?? d.symptomsWeightLoss));
        setVal("pa_sympAppetiteLoss", bitSelectValue(d.Symptoms_AppetiteLoss ?? d.symptomsAppetiteLoss));
        setVal("pa_sympLethargic", bitSelectValue(d.Symptoms_Lethargic ?? d.symptomsLethargic));
        setVal("pa_sympAbdominalPain", bitSelectValue(d.Symptoms_AbdominalPain ?? d.symptomsAbdominalPain));
        setVal("pa_sympConstipation", bitSelectValue(d.Symptoms_Constipation ?? d.symptomsConstipation));
        setVal("pa_sympDiarrhea", bitSelectValue(d.Symptoms_Diarrhea ?? d.symptomsDiarrhea));
        setVal("pa_sympRectalBleedingMucous", bitSelectValue(d.Symptoms_RectalBleedingMucous ?? d.symptomsRectalBleedingMucous));
        setVal("pa_sympRectalBleedingNoMucous", bitSelectValue(d.Symptoms_RectalBleedingNoMucous ?? d.symptomsRectalBleedingNoMucous));
        setVal("pa_sympTenesmus", bitSelectValue(d.Symptoms_Tenesmus ?? d.symptomsTenesmus));

        // Medical history
        setVal("pa_mhDiabetes", bitSelectValue(d.MedicalHistory_Diabetes ?? d.medicalHistoryDiabetes));
        setVal("pa_mhHypertension", bitSelectValue(d.MedicalHistory_Hypertension ?? d.medicalHistoryHypertension));
        setVal("pa_mhDyslipidemia", bitSelectValue(d.MedicalHistory_Dyslipidemia ?? d.medicalHistoryDyslipidemia));
        setVal("pa_mhBleeding", bitSelectValue(d.MedicalHistory_Bleeding ?? d.medicalHistoryBleeding));
        setVal("pa_mhAsthma", bitSelectValue(d.MedicalHistory_Asthma ?? d.medicalHistoryAsthma));

        // Allergy
        setVal("pa_allergyMedication", bitSelectValue(d.AllergyHistory_Medication ?? d.allergyHistoryMedication));
        setVal("pa_allergyMedicationDetails", (d.AllergyHistory_MedicationDetails ?? d.allergyHistoryMedicationDetails ?? ""));
        setVal("pa_allergyFood", bitSelectValue(d.AllergyHistory_Food ?? d.allergyHistoryFood));
        setVal("pa_allergyFoodDetails", (d.AllergyHistory_FoodDetails ?? d.allergyHistoryFoodDetails ?? ""));

        // Medication history
        setVal("pa_medAnticoagulant", bitSelectValue(d.MedicationHistory_Anticoagulant ?? d.medicationHistoryAnticoagulant));
        setVal("pa_medAnticoagulantDetails", (d.MedicationHistory_AnticoagulantDetails ?? d.medicationHistoryAnticoagulantDetails ?? ""));
        setVal("pa_medNarcotics", bitSelectValue(d.MedicationHistory_Narcotics ?? d.medicationHistoryNarcotics));
        setVal("pa_medNarcoticsDetails", (d.MedicationHistory_NarcoticsDetails ?? d.medicationHistoryNarcoticsDetails ?? ""));
        setVal("pa_medInsulin", bitSelectValue(d.MedicationHistory_Insulin ?? d.medicationHistoryInsulin));
        setVal("pa_medInsulinDetails", (d.MedicationHistory_InsulinDetails ?? d.medicationHistoryInsulinDetails ?? ""));
        setVal("pa_medAntiHypertensives", bitSelectValue(d.MedicationHistory_AntiHypertensives ?? d.medicationHistoryAntiHypertensives));
        setVal("pa_medAntiHypertensivesDetails", (d.MedicationHistory_AntiHypertensivesDetails ?? d.medicationHistoryAntiHypertensivesDetails ?? ""));

        // Family history
        setVal("pa_fhFirstDegree", bitSelectValue(d.FamilyHistory_FirstDegree ?? d.familyHistoryFirstDegree));
        setVal("pa_fhSecondDegree", bitSelectValue(d.FamilyHistory_SecondDegree ?? d.familyHistorySecondDegree));

        // Physical exam
        setVal("pa_physicalExamDetails", (d.PhysicalExamination_Details ?? d.physicalExaminationDetails ?? ""));

        // Investigation
        setVal("pa_invFBC", bitSelectValue(d.Investigation_FBC ?? d.investigationFBC));
        setVal("pa_invBUSE", bitSelectValue(d.Investigation_BUSE ?? d.investigationBUSE));
        setVal("pa_invRBS", bitSelectValue(d.Investigation_RBS ?? d.investigationRBS));
        setVal("pa_invLFT", bitSelectValue(d.Investigation_LFT ?? d.investigationLFT));
        setVal("pa_invCoag", bitSelectValue(d.Investigation_Coag ?? d.investigationCoag));

        // Management (checkbox)
        setChecked("pa_mgmtBowelPrep", parseAnyToBool(d.Management_BowelPrep ?? d.managementBowelPrep));
        setChecked("pa_mgmtProcedure", parseAnyToBool(d.Management_Procedure ?? d.managementProcedure));
        setChecked("pa_mgmtConsent", parseAnyToBool(d.Management_Consent ?? d.managementConsent));
        setChecked("pa_mgmtAdvise", parseAnyToBool(d.Management_Advise ?? d.managementAdvise));

        refreshConditionals();
    }

    function collect() {
        if (!rootEl) throw new Error("Template not initialized.");

        const iFOBTPositiveDate = requireNonEmpty("pa_iFOBTPositiveDate", "iFOBT Positive Date is required.");

        const Risks_Smoking = requireYesNoBit("pa_risksSmoking", "Smoking is required.");
        const Risks_AlcoholConsumption = requireYesNoBit("pa_risksAlcohol", "Alcohol Consumption is required.");
        const Risks_InflammatoryBowelDisease = requireYesNoBit("pa_risksIBD", "Inflammatory Bowel Disease is required.");
        const Risks_Diet = requireDietBit("pa_risksDiet", "Diet is required.");
        const Risks_SedentaryLifestyle = requireSelectText("pa_risksSedentary", "Sedentary Lifestyle is required.");

        const Symptoms_WeightLoss = requireYesNoBit("pa_sympWeightLoss", "Loss of Weight is required.");
        const Symptoms_AppetiteLoss = requireYesNoBit("pa_sympAppetiteLoss", "Loss of Appetite is required.");
        const Symptoms_Lethargic = requireYesNoBit("pa_sympLethargic", "Lethargy is required.");
        const Symptoms_AbdominalPain = requireYesNoBit("pa_sympAbdominalPain", "Abdominal Pain/Discomfort is required.");
        const Symptoms_Constipation = requireYesNoBit("pa_sympConstipation", "Constipation is required.");
        const Symptoms_Diarrhea = requireYesNoBit("pa_sympDiarrhea", "Diarrhea is required.");
        const Symptoms_RectalBleedingMucous = requireYesNoBit("pa_sympRectalBleedingMucous", "Rectal Bleeding with Mucous is required.");
        const Symptoms_RectalBleedingNoMucous = requireYesNoBit("pa_sympRectalBleedingNoMucous", "Rectal Bleeding without Mucous is required.");
        const Symptoms_Tenesmus = requireYesNoBit("pa_sympTenesmus", "Tenesmus is required.");

        const MedicalHistory_Diabetes = requireYesNoBit("pa_mhDiabetes", "Diabetes Mellitus is required.");
        const MedicalHistory_Hypertension = requireYesNoBit("pa_mhHypertension", "Hypertension is required.");
        const MedicalHistory_Dyslipidemia = requireYesNoBit("pa_mhDyslipidemia", "Dyslipidemia is required.");
        const MedicalHistory_Bleeding = requireYesNoBit("pa_mhBleeding", "Bleeding Disorders is required.");
        const MedicalHistory_Asthma = requireYesNoBit("pa_mhAsthma", "Asthma is required.");

        const AllergyHistory_Medication = requireYesNoBit("pa_allergyMedication", "Allergy (Medication) is required.");
        let AllergyHistory_MedicationDetails = getVal("pa_allergyMedicationDetails");
        if (AllergyHistory_Medication === true && isEmpty(AllergyHistory_MedicationDetails)) {
            focusEl("pa_allergyMedicationDetails");
            throw new Error("Please specify Medication allergy details.");
        }
        if (AllergyHistory_Medication === false) AllergyHistory_MedicationDetails = null;

        const AllergyHistory_Food = requireYesNoBit("pa_allergyFood", "Allergy (Food) is required.");
        let AllergyHistory_FoodDetails = getVal("pa_allergyFoodDetails");
        if (AllergyHistory_Food === true && isEmpty(AllergyHistory_FoodDetails)) {
            focusEl("pa_allergyFoodDetails");
            throw new Error("Please specify Food allergy details.");
        }
        if (AllergyHistory_Food === false) AllergyHistory_FoodDetails = null;

        const MedicationHistory_Anticoagulant = requireYesNoBit("pa_medAnticoagulant", "Anticoagulant/Aspirin is required.");
        let MedicationHistory_AnticoagulantDetails = getVal("pa_medAnticoagulantDetails");
        if (MedicationHistory_Anticoagulant === true && isEmpty(MedicationHistory_AnticoagulantDetails)) {
            focusEl("pa_medAnticoagulantDetails");
            throw new Error("Please specify Anticoagulant/Aspirin details.");
        }
        if (MedicationHistory_Anticoagulant === false) MedicationHistory_AnticoagulantDetails = null;

        const MedicationHistory_Narcotics = requireYesNoBit("pa_medNarcotics", "Chronic Narcotics is required.");
        let MedicationHistory_NarcoticsDetails = getVal("pa_medNarcoticsDetails");
        if (MedicationHistory_Narcotics === true && isEmpty(MedicationHistory_NarcoticsDetails)) {
            focusEl("pa_medNarcoticsDetails");
            throw new Error("Please specify Chronic Narcotics details.");
        }
        if (MedicationHistory_Narcotics === false) MedicationHistory_NarcoticsDetails = null;

        const MedicationHistory_Insulin = requireYesNoBit("pa_medInsulin", "OHA/Insulin is required.");
        let MedicationHistory_InsulinDetails = getVal("pa_medInsulinDetails");
        if (MedicationHistory_Insulin === true && isEmpty(MedicationHistory_InsulinDetails)) {
            focusEl("pa_medInsulinDetails");
            throw new Error("Please specify OHA/Insulin details.");
        }
        if (MedicationHistory_Insulin === false) MedicationHistory_InsulinDetails = null;

        const MedicationHistory_AntiHypertensives = requireYesNoBit("pa_medAntiHypertensives", "Anti-Hypertensives is required.");
        let MedicationHistory_AntiHypertensivesDetails = getVal("pa_medAntiHypertensivesDetails");
        if (MedicationHistory_AntiHypertensives === true && isEmpty(MedicationHistory_AntiHypertensivesDetails)) {
            focusEl("pa_medAntiHypertensivesDetails");
            throw new Error("Please specify Anti-Hypertensives details.");
        }
        if (MedicationHistory_AntiHypertensives === false) MedicationHistory_AntiHypertensivesDetails = null;

        const PreviousScope_Date = getDateOrNull("pa_previousScopeDate");

        const FamilyHistory_FirstDegree = requireYesNoBit("pa_fhFirstDegree", "Family History (First Degree) is required.");
        const FamilyHistory_SecondDegree = requireYesNoBit("pa_fhSecondDegree", "Family History (Second Degree) is required.");

        const PhysicalExamination_Details = (getVal("pa_physicalExamDetails") || "").trim();

        const Investigation_FBC = requireYesNoBit("pa_invFBC", "Investigation (FBC) is required.");
        const Investigation_BUSE = requireYesNoBit("pa_invBUSE", "Investigation (BUSE) is required.");
        const Investigation_RBS = requireYesNoBit("pa_invRBS", "Investigation (RBS) is required.");
        const Investigation_LFT = requireYesNoBit("pa_invLFT", "Investigation (LFT) is required.");
        const Investigation_Coag = requireYesNoBit("pa_invCoag", "Investigation (Coag) is required.");

        const Management_BowelPrep = requireChecked("pa_mgmtBowelPrep", "Management: Bowel Prep Instruction must be checked.");
        const Management_Procedure = requireChecked("pa_mgmtProcedure", "Management: Procedure Explanation must be checked.");
        const Management_Consent = requireChecked("pa_mgmtConsent", "Management: Consent Taken must be checked.");
        const Management_Advise = requireChecked("pa_mgmtAdvise", "Management: Advise for Medication must be checked.");

        return {
            iFOBTPositive_Date: iFOBTPositiveDate,

            Risks_Smoking,
            Risks_AlcoholConsumption,
            Risks_InflammatoryBowelDisease,
            Risks_Diet,
            Risks_SedentaryLifestyle,

            Symptoms_WeightLoss,
            Symptoms_AppetiteLoss,
            Symptoms_Lethargic,
            Symptoms_AbdominalPain,
            Symptoms_Constipation,
            Symptoms_Diarrhea,
            Symptoms_RectalBleedingMucous,
            Symptoms_RectalBleedingNoMucous,
            Symptoms_Tenesmus,

            MedicalHistory_Diabetes,
            MedicalHistory_Hypertension,
            MedicalHistory_Dyslipidemia,
            MedicalHistory_Bleeding,
            MedicalHistory_Asthma,

            AllergyHistory_Medication,
            AllergyHistory_MedicationDetails,
            AllergyHistory_Food,
            AllergyHistory_FoodDetails,

            MedicationHistory_Anticoagulant,
            MedicationHistory_AnticoagulantDetails,
            MedicationHistory_Narcotics,
            MedicationHistory_NarcoticsDetails,
            MedicationHistory_Insulin,
            MedicationHistory_InsulinDetails,
            MedicationHistory_AntiHypertensives,
            MedicationHistory_AntiHypertensivesDetails,

            PreviousScope_Date,

            FamilyHistory_FirstDegree,
            FamilyHistory_SecondDegree,

            PhysicalExamination_Details,

            Investigation_FBC,
            Investigation_BUSE,
            Investigation_RBS,
            Investigation_LFT,
            Investigation_Coag,

            Management_BowelPrep,
            Management_Procedure,
            Management_Consent,
            Management_Advise,
        };
    }

    // -----------------------------
    // ✅ REGISTER TEMPLATE USING THE SAME KEY AS DROPDOWN VALUE
    // -----------------------------
    const api = {
        type: "PATIENT ASSESSMENT",
        rootSelector: "#tmplPatientAssessment",
        init,
        clear,
        fill,
        collect,
    };

    // Correct key (this is what journey.js should look up)
    window.StaffPatientTemplates["PATIENT ASSESSMENT"] = api;

    // Optional alias (won't hurt, but your journey.js should not rely on it)
    window.StaffPatientTemplates.PatientAssessment = api;
})();