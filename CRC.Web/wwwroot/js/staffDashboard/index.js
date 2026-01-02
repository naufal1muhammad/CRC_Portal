// @ts-nocheck
(function() {
    let dt = null;
    let msgEl = null;
    let todayMsgEl = null;

    // Calendar
    let weekCalendar = null;

    // Legend + modal
    let legendEl = null;
    let apptModal = null;
    let mdPatient, mdType, mdBranch, mdWhen, mdOpenPatient;

    // appointmentType -> color
    const typeColorMap = {};

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

    // Deterministic distinct-ish color per type (no hardcoding)
    function colorForType(typeName) {
        const key = (typeName || "Unknown").toString().trim();
        if (typeColorMap[key]) return typeColorMap[key];

        let hash = 0;
        for (let i = 0; i < key.length; i++) {
            hash = (hash << 5) - hash + key.charCodeAt(i);
            hash |= 0;
        }

        const hue = Math.abs(hash) % 360;
        const color = `hsl(${hue}, 70%, 50%)`;
        typeColorMap[key] = color;
        return color;
    }

    function renderLegend(typeNames) {
        if (!legendEl) return;

        legendEl.innerHTML = "";
        const types = (typeNames || []).filter(Boolean).map((x) => x.toString().trim());
        const unique = Array.from(new Set(types)).sort((a, b) => a.localeCompare(b));

        if (unique.length === 0) {
            const t = document.createElement("span");
            t.className = "text-muted small";
            t.textContent = "No appointment types to display.";
            legendEl.appendChild(t);
            return;
        }

        unique.forEach((type) => {
            const color = colorForType(type);

            const item = document.createElement("div");
            item.className = "d-flex align-items-center gap-2 small border rounded px-2 py-1 bg-white";

            const dot = document.createElement("span");
            dot.className = "rounded-circle";
            dot.style.width = "10px";
            dot.style.height = "10px";
            dot.style.display = "inline-block";
            dot.style.backgroundColor = color;

            const label = document.createElement("span");
            label.textContent = type;

            item.appendChild(dot);
            item.appendChild(label);
            legendEl.appendChild(item);
        });
    }

    function showApptModal(ep) {
        if (!apptModal) return;

        const patientName = (ep.patientName || "").toString();
        const appointmentType = (ep.appointmentType || "").toString();
        const branchName = (ep.branchName || "").toString();
        const when = (ep.whenDisplay || ep.startDisplay || "").toString();
        const patientId = (ep.patientId || "").toString();

        if (mdPatient) mdPatient.textContent = patientName || "-";
        if (mdType) mdType.textContent = appointmentType || "-";
        if (mdBranch) mdBranch.textContent = branchName || "-";
        if (mdWhen) mdWhen.textContent = when || "-";

        if (mdOpenPatient) {
            if (patientId) {
                mdOpenPatient.href = "/StaffPatient/Details/" + encodeURIComponent(patientId);
                mdOpenPatient.classList.remove("disabled");
                mdOpenPatient.setAttribute("aria-disabled", "false");
            } else {
                mdOpenPatient.href = "#";
                mdOpenPatient.classList.add("disabled");
                mdOpenPatient.setAttribute("aria-disabled", "true");
            }
        }

        apptModal.show();
    }

    // ---------------- Row 1: Today appointments ----------------

    async function loadTodayAppointments() {
        try {
            setMsg(todayMsgEl, "Loading today’s appointments...", "text-muted");

            const result = await getJson("/StaffDashboard/GetTodayAppointments");
            if (!result.success) throw new Error(result.message || "Failed loading appointments");

            const rows = result.data || [];
            initOrUpdateDataTable(rows);

            setMsg(
                todayMsgEl,
                rows.length === 0
                    ? "No appointments found for today."
                    : `Found ${rows.length} appointment(s) for today.`,
                "text-muted"
            );
        } catch (err) {
            console.error(err);
            setMsg(todayMsgEl, err.message || "Error loading today’s appointments.", "text-danger");
            if (dt) dt.clear().draw();
        }
    }

    function initOrUpdateDataTable(rows) {
        const $table = $("#todayAppointmentsTable");

        if (dt) {
            dt.clear();
            dt.rows.add(rows);
            dt.draw(false);
            return;
        }

        dt = $table.DataTable({
            data: rows,
            columns: [
                { data: "patientId", title: "Patient ID" },
                {
                    data: "patientName",
                    title: "Patient Name",
                    render: function(data, type, row) {
                        if (type !== "display") return data || "";
                        const id = row.patientId || "";
                        const name = escapeHtml(data || "");
                        return `<a href="/StaffPatient/Details/${encodeURIComponent(id)}" class="text-decoration-none">${name}</a>`;
                    },
                },
                { data: "patientPhone", title: "Phone" },
                { data: "patientEmail", title: "Email" },
                { data: "appointmentType", title: "Appointment Type" },
                {
                    data: "status",
                    title: "Attendance Status",
                    render: function(data, type) {
                        if (type !== "display") return data || "";
                        const raw = (data || "").toString();
                        const cls = badgeClassForStatus(raw);
                        return `<span class="${cls}">${escapeHtml(raw || "Scheduled")}</span>`;
                    },
                },
                { data: "staffName", title: "Assigned Staff" },
                { data: "branchName", title: "Branch" },
                { data: "appointmentDateTime", title: "Date & Time" },
            ],
            ordering: true,
            pageLength: 10,
            lengthChange: true,
            language: { emptyTable: "No appointments found." },
        });
    }

    // ---------------- Row 2: Week calendar ----------------

    async function fetchWeekEvents(fetchInfo) {
        // Reset mapping each week-view load so legend shows only relevant types
        // (remove this block if you want the legend to accumulate forever)
        for (const k of Object.keys(typeColorMap)) delete typeColorMap[k];

        const url =
            "/StaffDashboard/GetWeekAppointments?start=" +
            encodeURIComponent(fetchInfo.startStr) +
            "&end=" +
            encodeURIComponent(fetchInfo.endStr);

        const result = await getJson(url);
        if (!result.success) throw new Error(result.message || "Failed loading calendar");

        const events = result.data || [];

        // Build legend types from events
        const typesInView = [];

        // Ensure colors exist + keep types list
        events.forEach((e) => {
            const ep = e.extendedProps || e.extendedprops || {};
            const type = (ep.appointmentType || e.appointmentType || "Unknown").toString();
            typesInView.push(type);

            // set colors on the event object too (may still get overridden by CSS)
            const c = colorForType(type);
            e.backgroundColor = c;
            e.borderColor = c;
            e.textColor = "#fff";

            // store display string for modal
            // (if your API already provides this, this is harmless)
            if (ep && !ep.whenDisplay && e.start) {
                ep.whenDisplay = ""; // controller can fill; eventClick will still work without
            }
            e.extendedProps = ep;
        });

        renderLegend(typesInView);
        return events;
    }

    function initWeekCalendar() {
        const el = document.getElementById("staffWeekCalendar");
        if (!el) return;

        if (!window.FullCalendar || !window.FullCalendar.Calendar) {
            console.warn("FullCalendar not found. Did you include main.min.js?");
            return;
        }

        if (weekCalendar) {
            weekCalendar.destroy();
            weekCalendar = null;
        }

        weekCalendar = new FullCalendar.Calendar(el, {
            initialView: "timeGridWeek",
            nowIndicator: true,
            height: "auto",
            expandRows: true,
            allDaySlot: false,
            slotMinTime: "07:00:00",
            slotMaxTime: "21:00:00",

            headerToolbar: {
                left: "prev,next today",
                center: "title",
                right: "timeGridWeek,timeGridDay",
            },

            events: async function(fetchInfo, successCallback, failureCallback) {
                try {
                    const events = await fetchWeekEvents(fetchInfo);
                    successCallback(events);
                } catch (err) {
                    console.error(err);
                    failureCallback(err);
                }
            },

            // Force colors here (fixes “everything is green” due to CSS override)
            eventDidMount: function(info) {
                const ep = info.event.extendedProps || {};
                const type = (ep.appointmentType || "Unknown").toString();
                const c = colorForType(type);

                // force with !important
                info.el.style.setProperty("background-color", c, "important");
                info.el.style.setProperty("border-color", c, "important");

                const main = info.el.querySelector(".fc-event-main");
                if (main) main.style.setProperty("color", "#fff", "important");
            },

            // Make the card show patient + type + branch (better than hover)
            eventContent: function(arg) {
                const ep = arg.event.extendedProps || {};
                const patient = (ep.patientName || arg.event.title || "").toString();
                const type = (ep.appointmentType || "").toString();
                const branch = (ep.branchName || "").toString();

                const wrap = document.createElement("div");
                wrap.style.lineHeight = "1.1";

                const l1 = document.createElement("div");
                l1.className = "fw-semibold";
                l1.style.fontSize = "0.85rem";
                l1.textContent = patient || "Appointment";

                const l2 = document.createElement("div");
                l2.className = "small";
                l2.style.fontSize = "0.75rem";
                l2.textContent = [type, branch].filter(Boolean).join(" • ");

                wrap.appendChild(l1);
                wrap.appendChild(l2);

                return { domNodes: [wrap] };
            },

            eventClick: function(info) {
                // show modal instead of navigating
                info.jsEvent.preventDefault();

                const ep = info.event.extendedProps || {};
                const start = info.event.start;

                // Build a nice display for the modal
                const whenDisplay =
                    start
                        ? new Intl.DateTimeFormat("en-GB", {
                            day: "2-digit",
                            month: "2-digit",
                            year: "numeric",
                            hour: "2-digit",
                            minute: "2-digit",
                            hour12: false,
                        }).format(start)
                        : "";

                showApptModal({
                    patientId: ep.patientId || "",
                    patientName: ep.patientName || info.event.title || "",
                    appointmentType: ep.appointmentType || "",
                    branchName: ep.branchName || "",
                    whenDisplay: whenDisplay,
                });
            },

            eventTimeFormat: { hour: "2-digit", minute: "2-digit", hour12: false },
            dayHeaderFormat: { weekday: "short", day: "2-digit", month: "short" },
        });

        weekCalendar.render();
    }

    // ---------------- init ----------------

    document.addEventListener("DOMContentLoaded", async function() {
        msgEl = document.getElementById("staffDashboardMessage");
        todayMsgEl = document.getElementById("todayAppointmentsMsg");
        legendEl = document.getElementById("calendarLegend");

        // Modal elements
        const modalEl = document.getElementById("staffApptModal");
        mdPatient = document.getElementById("mdPatient");
        mdType = document.getElementById("mdType");
        mdBranch = document.getElementById("mdBranch");
        mdWhen = document.getElementById("mdWhen");
        mdOpenPatient = document.getElementById("mdOpenPatient");

        if (modalEl && window.bootstrap && window.bootstrap.Modal) {
            apptModal = new bootstrap.Modal(modalEl);
        }

        setMsg(msgEl, "", "text-muted");

        // Row 1
        await loadTodayAppointments();

        // Row 2
        initWeekCalendar();
    });
})();