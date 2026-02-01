// @ts-nocheck
(function () {
    let dt = null;

    function getStaffId() {
        const root = document.querySelector('[data-staff-id]');
        return root ? (root.getAttribute('data-staff-id') || '') : '';
    }

    function qs(id) {
        return document.getElementById(id);
    }

    function escapeHtml(str) {
        return (str ?? '').toString().replace(/[&<>"']/g, s => ({
            '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
        }[s]));
    }

    function showInfoForNewStaff(isNew) {
        const info = qs('staffScheduleInfo');
        const btnCreate = qs('btnCreateStaffSlots');
        const btnFilter = qs('btnFilterSlots');
        const btnClear = qs('btnClearFilterSlots');

        if (info) {
            info.classList.toggle('d-none', !isNew);
        }

        if (btnCreate) btnCreate.disabled = !!isNew;
        if (btnFilter) btnFilter.disabled = !!isNew;
        if (btnClear) btnClear.disabled = !!isNew;
    }

    function setMessage(text, isSuccess) {
        const el = qs('staffScheduleMessage');
        if (!el) return;

        el.textContent = text || '';
        el.classList.remove('text-danger', 'text-success');

        if (!text) return;

        el.classList.add(isSuccess ? 'text-success' : 'text-danger');
    }

    function isTimeOnTheHour(t) {
        // accepts 'HH:mm' or 'HH:mm:ss'
        if (!t) return false;
        const parts = t.split(':');
        if (parts.length < 2) return false;
        const mm = parseInt(parts[1], 10);
        return !isNaN(mm) && mm === 0;
    }

    function todayIso() {
        const d = new Date();
        const yyyy = d.getFullYear();
        const mm = String(d.getMonth() + 1).padStart(2, '0');
        const dd = String(d.getDate()).padStart(2, '0');
        return `${yyyy}-${mm}-${dd}`;
    }

    function buildListUrl(staffId, fromDate, toDate) {
        const url = new URL('/StaffSchedule/List', window.location.origin);
        url.searchParams.set('staffId', staffId);

        if (fromDate) url.searchParams.set('fromDate', fromDate);
        if (toDate) url.searchParams.set('toDate', toDate);

        return url.toString();
    }

    function normalizeRows(rows) {
        return (rows || []).map(r => ({
            staffSlotId: r.staffSlotId,
            slotDate: r.slotDate || '',
            slotStartTime: r.slotStartTime || '',
            slotEndTime: r.slotEndTime || '',
            patientAppointmentId: r.patientAppointmentId
        }));
    }

    function initOrUpdateDataTable(rows) {
        const $table = $('#staffSlotsTable');

        // moment sort (if plugin exists)
        if ($.fn?.dataTable?.moment) {
            $.fn.dataTable.moment('YYYY-MM-DD');
        }

        if (dt) {
            dt.clear();
            dt.rows.add(rows);
            dt.draw();
            return;
        }

        dt = $table.DataTable({
            data: rows,
            columns: [
                {
                    data: 'slotDate',
                    title: 'Date',
                    render: function (data, type) {
                        if (type !== 'display') return data || '';
                        return `<span class="text-nowrap">${escapeHtml(data || '')}</span>`;
                    }
                },
                {
                    data: 'slotStartTime',
                    title: 'Start Time',
                    render: function (data, type) {
                        if (type !== 'display') return data || '';
                        return `<span class="text-nowrap">${escapeHtml(data || '')}</span>`;
                    }
                },
                {
                    data: 'slotEndTime',
                    title: 'End Time',
                    render: function (data, type) {
                        if (type !== 'display') return data || '';
                        return `<span class="text-nowrap">${escapeHtml(data || '')}</span>`;
                    }
                },
                {
                    data: 'patientAppointmentId',
                    title: 'Status',
                    orderable: true,
                    render: function (data, type) {
                        const isTaken = data !== null && data !== undefined && data !== '';
                        if (type !== 'display') return isTaken ? 'Taken' : 'Available';

                        const badgeClass = isTaken ? 'badge rounded-pill bg-danger' : 'badge rounded-pill bg-success';
                        const txt = isTaken ? 'Taken' : 'Available';
                        return `<span class="${badgeClass}">${txt}</span>`;
                    }
                },
                {
                    data: null,
                    title: 'Actions',
                    orderable: false,
                    className: 'text-center text-nowrap',
                    render: function (data, type, row) {
                        if (type !== 'display') return '';

                        const id = row.staffSlotId || 0;
                        const isTaken = row.patientAppointmentId !== null && row.patientAppointmentId !== undefined && row.patientAppointmentId !== '';

                        if (isTaken) {
                            return `
                                <button type="button" class="btn btn-sm btn-secondary" disabled title="Cannot delete a taken slot">
                                    <i class="fas fa-trash"></i>
                                </button>`;
                        }

                        return `
                            <button type="button"
                                    class="btn btn-sm btn-danger js-delete-slot"
                                    data-slot-id="${id}"
                                    title="Delete">
                                <i class="fas fa-trash"></i>
                            </button>`;
                    }
                }
            ],
            ordering: true,
            pageLength: 10,
            lengthChange: true,
            language: {
                emptyTable: 'No slots found.'
            }
        });
    }

    async function loadSlots() {
        const staffId = getStaffId();

        showInfoForNewStaff(!staffId);

        if (!staffId) {
            setMessage('', false);
            initOrUpdateDataTable([]);
            return;
        }

        const filterFrom = qs('ScheduleFilterFrom')?.value || '';
        const filterTo = qs('ScheduleFilterTo')?.value || '';

        try {
            const response = await fetch(buildListUrl(staffId, filterFrom, filterTo), {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                setMessage('Error loading staff slots.', false);
                initOrUpdateDataTable([]);
                return;
            }

            const result = await response.json();

            if (!result.success) {
                setMessage(result.message || 'Error loading staff slots.', false);
                initOrUpdateDataTable([]);
                return;
            }

            initOrUpdateDataTable(normalizeRows(result.data));
            setMessage('', false);
        } catch (err) {
            console.error(err);
            setMessage('Error loading staff slots.', false);
            initOrUpdateDataTable([]);
        }
    }

    function validateCreateInputs() {
        const fromDate = qs('ScheduleFromDate')?.value || '';
        const toDate = qs('ScheduleToDate')?.value || '';
        const startTime = qs('ScheduleStartTime')?.value || '';
        const endTime = qs('ScheduleEndTime')?.value || '';

        if (!fromDate || !toDate || !startTime || !endTime) {
            return { ok: false, message: 'Please fill in From Date, To Date, Start Time and End Time.' };
        }

        if (toDate < fromDate) {
            return { ok: false, message: 'To Date must be on or after From Date.' };
        }

        // date range max 31 days inclusive
        const d1 = new Date(fromDate);
        const d2 = new Date(toDate);
        const diffDays = Math.floor((d2 - d1) / (1000 * 60 * 60 * 24)) + 1;
        if (diffDays > 31) {
            return { ok: false, message: 'Date range cannot exceed 31 days.' };
        }

        if (!isTimeOnTheHour(startTime) || !isTimeOnTheHour(endTime)) {
            return { ok: false, message: 'Slot times must be on the hour (minutes must be 00).' };
        }

        if (endTime <= startTime) {
            return { ok: false, message: 'End Time must be later than Start Time.' };
        }

        return { ok: true, fromDate, toDate, startTime, endTime };
    }

    async function createSlots() {
        const staffId = getStaffId();
        if (!staffId) {
            setMessage('Please save the staff first before managing schedule slots.', false);
            return;
        }

        const v = validateCreateInputs();
        if (!v.ok) {
            setMessage(v.message, false);
            return;
        }

        const btn = qs('btnCreateStaffSlots');
        if (btn) btn.disabled = true;

        setMessage('Creating slots...', true);

        try {
            const response = await fetch('/StaffSchedule/CreateRange', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
                body: JSON.stringify({
                    staffId: staffId,
                    fromDate: v.fromDate,
                    toDate: v.toDate,
                    startTime: v.startTime,
                    endTime: v.endTime
                })
            });

            if (!response.ok) {
                setMessage('Server error while creating slots.', false);
                return;
            }

            const result = await response.json();

            if (!result.success) {
                setMessage(result.message || 'Failed to create slots.', false);
                return;
            }

            const created = result.createdCount ?? 0;
            const skipped = result.skippedExistingCount ?? 0;

            setMessage(`Slots created: ${created}. Skipped existing: ${skipped}.`, true);

            // reload grid
            await loadSlots();
        } catch (err) {
            console.error(err);
            setMessage('An unexpected error occurred.', false);
        } finally {
            if (btn) btn.disabled = false;
        }
    }

    async function deleteSlot(staffSlotId) {
        if (!staffSlotId) return;

        if (!confirm('Delete this slot?')) return;

        try {
            const response = await fetch('/StaffSchedule/Delete', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
                body: JSON.stringify({ staffSlotId: staffSlotId })
            });

            if (!response.ok) {
                setMessage('Server error while deleting slot.', false);
                return;
            }

            const result = await response.json();

            if (!result.success) {
                setMessage(result.message || 'Failed to delete slot.', false);
                return;
            }

            setMessage('Slot deleted successfully.', true);
            await loadSlots();
        } catch (err) {
            console.error(err);
            setMessage('An unexpected error occurred.', false);
        }
    }

    function pad2(n) {
        return String(n).padStart(2, '0');
    }

    function hourValue(h) {
        return `${pad2(h)}:00`;
    }

    function findFirstExistingId(ids) {
        for (const id of ids) {
            const el = document.getElementById(id);
            if (el) return el;
        }
        return null;
    }

    function buildHourSelectReplacingInput(inputEl, hours) {
        const sel = document.createElement('select');

        // Keep styling & identity
        sel.id = inputEl.id;
        sel.name = inputEl.name || inputEl.id;
        sel.className = inputEl.className;          // preserve Bootstrap classes (form-control etc)
        sel.required = inputEl.required;
        sel.disabled = inputEl.disabled;

        // Copy data-* attributes (if any)
        for (const attr of inputEl.attributes) {
            if (attr.name.startsWith('data-')) {
                sel.setAttribute(attr.name, attr.value);
            }
        }

        // Add options
        hours.forEach(h => {
            const opt = document.createElement('option');
            opt.value = hourValue(h);
            opt.textContent = hourValue(h);
            sel.appendChild(opt);
        });

        // Set initial value (if valid), otherwise first option
        const current = (inputEl.value || '').slice(0, 5);
        if (current && Array.from(sel.options).some(o => o.value === current)) {
            sel.value = current;
        }

        // Replace in DOM
        inputEl.parentNode.replaceChild(sel, inputEl);

        return sel;
    }

    function initHourOnlyTimeSelectors() {
        // Your JS uses these IDs:
        // ScheduleStartTime / ScheduleEndTime
        // But I also support startTime / endTime just in case your cshtml uses those.
        const startEl = findFirstExistingId(['ScheduleStartTime', 'startTime']);
        const endEl = findFirstExistingId(['ScheduleEndTime', 'endTime']);

        if (!startEl || !endEl) return;

        // Start can be 00:00 .. 22:00 (23:00 would have no valid 1-hour slot ending <= 23:59)
        const startHours = [];
        for (let h = 0; h <= 22; h++) startHours.push(h);

        // End can be 01:00 .. 23:00
        const endHours = [];
        for (let h = 1; h <= 23; h++) endHours.push(h);

        const startSel = buildHourSelectReplacingInput(startEl, startHours);
        const endSel = buildHourSelectReplacingInput(endEl, endHours);

        function refreshEndOptions() {
            const sh = parseInt(startSel.value.substring(0, 2), 10);
            const minEndHour = sh + 1;

            // Disable end options that are <= start
            for (const opt of endSel.options) {
                const eh = parseInt(opt.value.substring(0, 2), 10);
                opt.disabled = eh < minEndHour;
            }

            // If current end is invalid, set to minEndHour
            const currentEh = parseInt(endSel.value.substring(0, 2), 10);
            if (isNaN(currentEh) || currentEh < minEndHour) {
                endSel.value = hourValue(minEndHour);
            }
        }

        startSel.addEventListener('change', refreshEndOptions);
        refreshEndOptions();
    }

    function attachHandlers() {
        const btnCreate = qs('btnCreateStaffSlots');
        const btnFilter = qs('btnFilterSlots');
        const btnClear = qs('btnClearFilterSlots');

        if (btnCreate) {
            btnCreate.addEventListener('click', function () {
                createSlots();
            });
        }

        if (btnFilter) {
            btnFilter.addEventListener('click', function () {
                loadSlots();
            });
        }

        if (btnClear) {
            btnClear.addEventListener('click', function () {
                const from = qs('ScheduleFilterFrom');
                const to = qs('ScheduleFilterTo');
                if (from) from.value = '';
                if (to) to.value = '';
                loadSlots();
            });
        }

        // Delete button handler (DataTables / responsive friendly)
        $('#staffSlotsTable').off('click', '.js-delete-slot');
        $('#staffSlotsTable').on('click', '.js-delete-slot', function (e) {
            e.preventDefault();
            e.stopPropagation();

            if (!dt) return;

            let $tr = $(this).closest('tr');
            if ($tr.hasClass('child')) $tr = $tr.prev();

            const rowApi = dt.row($tr);
            const rowData = rowApi.data();
            const slotId = rowData?.staffSlotId || $(this).data('slot-id') || 0;

            deleteSlot(slotId);
        });

        // When Schedule tab becomes visible, adjust columns
        const scheduleTabBtn = document.getElementById('tab-schedule');
        if (scheduleTabBtn) {
            scheduleTabBtn.addEventListener('shown.bs.tab', function () {
                if (dt) {
                    dt.columns.adjust().draw(false);
                }
                // lazy load when tab opened
                loadSlots();
            });
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        // defaults
        const d = todayIso();
        const from = qs('ScheduleFromDate');
        const to = qs('ScheduleToDate');
        if (from && !from.value) from.value = d;
        if (to && !to.value) to.value = d;

        initHourOnlyTimeSelectors();
        attachHandlers();
        loadSlots();
    });
})();