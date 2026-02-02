// @ts-nocheck
(function () {
    const dtMap = {}; // tableId -> DataTable instance

    let msgEl = null;

    let todayMsgEl = null;
    let weekMsgEl = null;
    let monthMsgEl = null;

    let weekRangeLabelEl = null;

    let monthPickerEl = null;
    let monthPrevBtn = null;
    let monthNextBtn = null;

    // ---------------- helpers ----------------

    function setMsg(el, text, cssClass) {
        if (!el) return;
        el.className = "small mb-2 " + (cssClass || "text-muted");
        el.textContent = text || "";
    }

    function escapeHtml(str) {
        return (str ?? "")
            .toString()
            .replace(/[&<>"']/g, (s) => ({
                "&": "&amp;",
                "<": "&lt;",
                ">": "&gt;",
                '"': "&quot;",
                "'": "&#39;",
            }[s]));
    }

    function badgeClassForStatus(status) {
        const v = (status || "").toString().trim().toLowerCase();
        if (v === "attended") return "badge rounded-pill bg-success";
        if (v === "not attended") return "badge rounded-pill bg-danger";
        return "badge rounded-pill bg-warning"; // Scheduled / default
    }

    async function getJson(url) {
        const res = await fetch(url, {
            method: "GET",
            headers: { Accept: "application/json" },
        });
        if (!res.ok) throw new Error("HTTP " + res.status);
        return await res.json();
    }

    function fmtDdMmYyyy(date) {
        const d = date instanceof Date ? date : new Date(date);
        return new Intl.DateTimeFormat("en-GB", { day: "2-digit", month: "2-digit", year: "numeric" }).format(d);
    }

    function addMonths(year, month, delta) {
        const d = new Date(year, month - 1, 1);
        d.setMonth(d.getMonth() + delta);
        return { year: d.getFullYear(), month: d.getMonth() + 1 };
    }

    // ---------------- tables ----------------

    function initOrUpdateDataTable(tableId, rows) {
        const $table = $("#" + tableId);

        if (dtMap[tableId]) {
            dtMap[tableId].clear();
            dtMap[tableId].rows.add(rows);
            dtMap[tableId].draw(false);
            return;
        }

        dtMap[tableId] = $table.DataTable({
            data: rows,
            columns: [
                { data: "patientId", title: "Patient ID" },
                {
                    data: "patientName",
                    title: "Patient Name",
                    render: function (data, type, row) {
                        if (type !== "display") return data || "";
                        const id = row.patientId || "";
                        const name = escapeHtml(data || "");
                        return `<a href="/StaffPatient/Details/${encodeURIComponent(id)}" class="text-decoration-none">${name}</a>`;
                    },
                },
                { data: "appointmentType", title: "Appointment Type" },
                {
                    data: "status",
                    title: "Attendance Status",
                    render: function (data, type) {
                        if (type !== "display") return data || "";
                        const raw = (data || "").toString();
                        const cls = badgeClassForStatus(raw);
                        return `<span class="${cls}">${escapeHtml(raw || "Scheduled")}</span>`;
                    },
                },
                { data: "branchName", title: "Branch" },
                {
                    data: "appointmentDate",
                    title: "Date",
                    render: function (data, type, row) {
                        if (type === "sort" || type === "type") return row.appointmentDateSort || data || "";
                        return data || "";
                    },
                },
                { data: "fromTime", title: "From" },
                { data: "toTime", title: "To" },
            ],
            ordering: true,
            order: [[5, "asc"], [6, "asc"]],
            pageLength: 10,
            lengthChange: true,
            language: { emptyTable: "No appointments found." },
        });
    }

    // ---------------- loaders ----------------

    async function loadTable(url, tableId, msgEl, emptyMsg, labelWhenFound) {
        try {
            setMsg(msgEl, "Loading appointments...", "text-muted");

            const result = await getJson(url);
            if (!result.success) throw new Error(result.message || "Failed loading appointments");

            const rows = result.data || [];
            initOrUpdateDataTable(tableId, rows);

            setMsg(
                msgEl,
                rows.length === 0 ? (emptyMsg || "No appointments found.") : (labelWhenFound ? labelWhenFound(rows.length) : `Found ${rows.length} appointment(s).`),
                "text-muted"
            );
        } catch (err) {
            console.error(err);
            setMsg(msgEl, err.message || "Error loading appointments.", "text-danger");
            if (dtMap[tableId]) dtMap[tableId].clear().draw();
        }
    }

    async function loadTodayAppointments() {
        await loadTable(
            "/StaffDashboard/GetTodayAppointments",
            "todayAppointmentsTable",
            todayMsgEl,
            "No appointments found for today.",
            (n) => `Found ${n} appointment(s) for today.`
        );
    }

    async function loadThisWeekAppointments() {
        // Display range (today to +6 days)
        const start = new Date();
        start.setHours(0, 0, 0, 0);
        const endInclusive = new Date(start);
        endInclusive.setDate(endInclusive.getDate() + 6);

        if (weekRangeLabelEl) weekRangeLabelEl.textContent = `${fmtDdMmYyyy(start)} - ${fmtDdMmYyyy(endInclusive)}`;

        await loadTable(
            "/StaffDashboard/GetThisWeekAppointments",
            "weekAppointmentsTable",
            weekMsgEl,
            "No appointments found in the next 7 days.",
            (n) => `Found ${n} appointment(s) in the next 7 days.`
        );
    }

    function parseMonthPickerValue(val) {
        // val: yyyy-MM
        if (!val || val.length < 7) return null;
        const [yStr, mStr] = val.split("-");
        const y = parseInt(yStr, 10);
        const m = parseInt(mStr, 10);
        if (!y || !m || m < 1 || m > 12) return null;
        return { year: y, month: m };
    }

    function setMonthPickerValue(year, month) {
        if (!monthPickerEl) return;
        const m = String(month).padStart(2, "0");
        monthPickerEl.value = `${year}-${m}`;
    }

    async function loadMonthAppointments(year, month) {
        const url =
            "/StaffDashboard/GetMonthAppointments?year=" +
            encodeURIComponent(year) +
            "&month=" +
            encodeURIComponent(month);

        await loadTable(
            url,
            "monthAppointmentsTable",
            monthMsgEl,
            "No appointments found for this month.",
            (n) => `Found ${n} appointment(s) for this month.`
        );
    }

    // ---------------- init ----------------

    document.addEventListener("DOMContentLoaded", async function () {
        msgEl = document.getElementById("staffDashboardMessage");

        todayMsgEl = document.getElementById("todayAppointmentsMsg");
        weekMsgEl = document.getElementById("weekAppointmentsMsg");
        monthMsgEl = document.getElementById("monthAppointmentsMsg");

        weekRangeLabelEl = document.getElementById("weekRangeLabel");

        monthPickerEl = document.getElementById("monthPicker");
        monthPrevBtn = document.getElementById("monthPrevBtn");
        monthNextBtn = document.getElementById("monthNextBtn");

        setMsg(msgEl, "", "text-muted");

        // Initialize month picker to current month
        const now = new Date();
        const currentYear = now.getFullYear();
        const currentMonth = now.getMonth() + 1;

        setMonthPickerValue(currentYear, currentMonth);

        // Wire month toggler
        if (monthPickerEl) {
            monthPickerEl.addEventListener("change", async function () {
                const v = parseMonthPickerValue(monthPickerEl.value);
                if (!v) return;
                await loadMonthAppointments(v.year, v.month);
            });
        }

        if (monthPrevBtn) {
            monthPrevBtn.addEventListener("click", async function () {
                const v = parseMonthPickerValue(monthPickerEl ? monthPickerEl.value : "");
                const cur = v || { year: currentYear, month: currentMonth };
                const next = addMonths(cur.year, cur.month, -1);
                setMonthPickerValue(next.year, next.month);
                await loadMonthAppointments(next.year, next.month);
            });
        }

        if (monthNextBtn) {
            monthNextBtn.addEventListener("click", async function () {
                const v = parseMonthPickerValue(monthPickerEl ? monthPickerEl.value : "");
                const cur = v || { year: currentYear, month: currentMonth };
                const next = addMonths(cur.year, cur.month, 1);
                setMonthPickerValue(next.year, next.month);
                await loadMonthAppointments(next.year, next.month);
            });
        }

        // Load all 3 cards
        await loadTodayAppointments();
        await loadThisWeekAppointments();
        await loadMonthAppointments(currentYear, currentMonth);
    });
})();