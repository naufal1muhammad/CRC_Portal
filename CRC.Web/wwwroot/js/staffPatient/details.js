// @ts-nocheck
(function() {
    // ---------------------------
    // Shared context for all tabs
    // ---------------------------
    const ENDPOINTS = {
        getBasic: "/StaffPatient/GetBasic",
    };

    function el(id) {
        return document.getElementById(id);
    }

    function setText(node, text) {
        if (!node) return;
        node.textContent = text ?? "";
    }

    function setValue(id, val) {
        const x = el(id);
        if (!x) return;
        x.value = val ?? "";
    }

    function setMsg(idOrEl, text, cssClass) {
        const node =
            typeof idOrEl === "string" ? el(idOrEl) : (idOrEl || null);
        if (!node) return;
        node.className = (cssClass || "text-muted") + " small";
        node.textContent = text || "";
    }

    async function getJson(url) {
        const res = await fetch(url, {
            method: "GET",
            headers: { Accept: "application/json" },
        });
        if (!res.ok) throw new Error("HTTP " + res.status);
        return await res.json();
    }

    function getPatientIdFromDom() {
        return (el("patientId")?.value || "").trim();
    }

    async function loadBasicDetails() {
        const patientId = getPatientIdFromDom();
        if (!patientId) {
            setMsg("spBasicMsg", "Invalid patient (missing patientId).", "text-danger");
            return;
        }

        try {
            setMsg("spBasicMsg", "Loading patient details...", "text-muted");

            const url = ENDPOINTS.getBasic + "?patientId=" + encodeURIComponent(patientId);
            const result = await getJson(url);

            if (!result.success) {
                throw new Error(result.message || "Failed loading patient details.");
            }

            const p = result.patient || null;
            if (!p) {
                setMsg("spBasicMsg", "Patient details not found.", "text-danger");
                return;
            }

            // Header
            setText(el("spPatientHeaderName"), p.name ? p.name : "Patient");
            setText(el("spPatientHeaderMeta"), "ID: " + (p.patientId || patientId));

            // Fill inputs
            setValue("sp_name", p.name);
            setValue("sp_nric", p.nric);
            setValue("sp_gender", p.gender);

            setValue("sp_email", p.email);
            setValue("sp_phone", p.phone);
            setValue("sp_branchName", p.branchName);

            setValue("sp_admittedOn", p.admittedOn);
            setValue("sp_birthDate", p.birthDate);
            setValue("sp_raceName", p.raceName);
            setValue("sp_religionName", p.religionName);

            setValue("sp_maritalStatusName", p.maritalStatusName);
            setValue("sp_occupationName", p.occupationName);
            setValue("sp_sourceName", p.sourceName);

            setValue("sp_address", p.address);

            setValue("sp_emergencyName", p.emergencyName);
            setValue("sp_emergencyRelationship", p.emergencyRelationship);
            setValue("sp_emergencyNumber", p.emergencyNumber);

            // optional fields (your endpoint may or may not return them)
            setValue("sp_dischargeTypeName", p.dischargeTypeName || "");
            setValue("sp_dischargeDate", p.dischargeDate || "");
            setValue("sp_dischargeRemarks", p.dischargeRemarks || "");

            setMsg("spBasicMsg", "", "text-muted");
        } catch (err) {
            console.error(err);
            setMsg("spBasicMsg", err.message || "Error loading patient details.", "text-danger");
        }
    }

    // ------------------------------------------------
    // Expose shared context used by journey.js + templates
    // ------------------------------------------------
    window.StaffPatient = window.StaffPatient || {};
    window.StaffPatient.endpoints = ENDPOINTS;
    window.StaffPatient.helpers = { el, setText, setValue, setMsg, getJson, getPatientIdFromDom };

    // Run only the Basic tab logic here
    document.addEventListener("DOMContentLoaded", async function() {
        console.log("staffPatient/details.js loaded ✅");
        window.StaffPatient.patientId = getPatientIdFromDom();
        await loadBasicDetails();
    });
})();