// @ts-nocheck
(function() {
    let chkDischarged,
        txtDate,
        selType,
        txtRemarks,
        msg;

    function setFieldsEnabled(enabled) {
        if (txtDate) {
            txtDate.disabled = !enabled;
            if (!enabled) txtDate.value = '';
        }
        if (selType) {
            selType.disabled = !enabled;
            if (!enabled) selType.value = '';
        }
        if (txtRemarks) {
            txtRemarks.disabled = !enabled;
            if (!enabled) txtRemarks.value = '';
        }
        if (msg) {
            msg.textContent = '';
            msg.classList.remove('text-danger', 'text-success');
        }
    }

async function loadDischargeTypesIntoTab() {
    if (!selType) {
        selType = document.getElementById('PatientDischargeType');
    }
    if (!selType) return;

    selType.innerHTML = '<option value="">Loading...</option>';

    try {
        const response = await fetch('/Settings/GetDischargeTypes', {
            method: 'GET',
            headers: { 'Accept': 'application/json' }
        });

        if (!response.ok) {
            selType.innerHTML = '<option value="">Error loading discharge types</option>';
            return;
        }

        const result = await response.json();
        if (!result.success) {
            selType.innerHTML = '<option value="">Error loading discharge types</option>';
            return;
        }

        const list = result.data || [];
        selType.innerHTML = '<option value="">-- Select Discharge Type --</option>';

        list.forEach(t => {
            const opt = document.createElement('option');
            opt.value = t.dischargeTypeId;
            opt.textContent = t.dischargeTypeName;
            opt.setAttribute('data-name', t.dischargeTypeName || '');
            selType.appendChild(opt);
        });
    } catch (err) {
        console.error('Error loading discharge types', err);
        selType.innerHTML = '<option value="">Error loading discharge types</option>';
    }
}

    // Called by main edit.js when a patient is loaded
    function initDischargeTab(initial) {
        // initial = { isDischarged, dischargeTypeId, dischargeTypeName, dischargeDate, dischargeRemarks }

        const hasDischarge = initial && initial.dischargeTypeName;

        if (chkDischarged) {
            chkDischarged.checked = !!hasDischarge;
        }

        // Load dropdown options, then apply initial selection
        loadDischargeTypesIntoTab().then(() => {
            if (hasDischarge && selType) {
                // find option with matching name or id
                let matched = false;
                if (initial.dischargeTypeId) {
                    for (let i = 0; i < selType.options.length; i++) {
                        if (selType.options[i].value === initial.dischargeTypeId) {
                            selType.selectedIndex = i;
                            matched = true;
                            break;
                        }
                    }
                }
                if (!matched && initial.dischargeTypeName) {
                    for (let i = 0; i < selType.options.length; i++) {
                        if (selType.options[i].text === initial.dischargeTypeName) {
                            selType.selectedIndex = i;
                            break;
                        }
                    }
                }
            }

            if (hasDischarge && txtDate) {
                txtDate.value = initial.dischargeDate || '';
            }

            if (hasDischarge && txtRemarks) {
                txtRemarks.value = initial.dischargeRemarks || '';
            }

            setFieldsEnabled(!!hasDischarge);
        });
    }

    function getDischargePayload() {
        if (!chkDischarged) {
            return {
                isDischarged: false,
                dischargeTypeId: null,
                dischargeTypeName: null,
                dischargeDate: null,
                dischargeRemarks: null
            };
        }

        const isDischarged = chkDischarged.checked;

        if (!isDischarged) {
            return {
                isDischarged: false,
                dischargeTypeId: null,
                dischargeTypeName: null,
                dischargeDate: null,
                dischargeRemarks: null
            };
        }

        const dateVal = txtDate ? txtDate.value : '';
        const typeVal = selType ? selType.value : '';
        const typeName = (selType && selType.selectedIndex >= 0)
            ? (selType.options[selType.selectedIndex].getAttribute('data-name')
                || selType.options[selType.selectedIndex].text)
            : '';

        const remarksVal = txtRemarks ? txtRemarks.value.trim() : '';

        // Basic front-end validation (Discharge-specific)
        if (!dateVal || !typeVal) {
            if (msg) {
                msg.textContent = 'Please fill in Discharge Date and Discharge Type.';
                msg.classList.remove('text-success');
                msg.classList.add('text-danger');
            }
            return null; // signal invalid
        }

        if (msg) {
            msg.textContent = '';
            msg.classList.remove('text-danger', 'text-success');
        }

        return {
            isDischarged: true,
            dischargeTypeId: typeVal,
            dischargeTypeName: typeName,
            dischargeDate: dateVal,
            dischargeRemarks: remarksVal
        };
    }

    // Expose helper to global scope so edit-basic.js can call it
    window.PatientDischargeTab = {
        init: initDischargeTab,
        getPayload: getDischargePayload
    };

    document.addEventListener('DOMContentLoaded', function() {
        chkDischarged = document.getElementById('PatientIsDischarged');
        txtDate = document.getElementById('PatientDischargeDate');
        selType = document.getElementById('PatientDischargeType');
        txtRemarks = document.getElementById('PatientDischargeRemarks');
        msg = document.getElementById('dischargeTabMessage');

        // default state: disabled
        setFieldsEnabled(false);

        if (chkDischarged) {
            chkDischarged.addEventListener('change', function() {
                const enabled = chkDischarged.checked;
                setFieldsEnabled(enabled);
            });
        }
    });
})();