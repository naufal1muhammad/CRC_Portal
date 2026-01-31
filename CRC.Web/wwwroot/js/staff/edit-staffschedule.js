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

        attachHandlers();
        loadSlots();
    });
})();