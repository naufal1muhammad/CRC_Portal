namespace CRC.Data.Models
{
    // One row of the appointment search, from spPatientAppointment_Search — the procedure behind BOTH the
    // /Appointment search page and the /AdminDashboard "today's appointments" panel. Ten columns, ordered
    // by date DESC, then start time ASC, then id DESC.
    //
    // IT IS NOT A PatientAppointmentItem, even though both describe an appointment. The two procedures
    // select genuinely different columns: this one joins dbo.PatientBasic for the patient's name, phone
    // and email and does NOT return Patient_ID's date/time as separate fields, while ListByPatient returns
    // the ids and the two time strings and no patient contact details. Sharing one model would hand every
    // caller of one of them a set of properties silently left at their defaults — the failure mode §5.3
    // describes for spUsers_GetById. Reuse the shape, never the name.
    //
    // 🔴 PatientAppointment_Date IS A DATETIME HERE AND A DATE EVERYWHERE ELSE. The procedure composes it:
    //
    //     DATEADD(SECOND, DATEDIFF(SECOND, 0, pa.PatientAppointment_StartTime),
    //             CAST(pa.PatientAppointment_Date AS DATETIME)) AS [PatientAppointment_Date]
    //
    // — the appointment's DATE column with its START TIME folded in, reusing the column's own name. Its
    // header comment says why: "Keep legacy column name used by existing controllers/JS (start datetime)".
    // That is what makes both endpoints able to render "01/09/2026 08:00" from a single field, and it is
    // why this model has no separate time property to go looking for.
    //
    // FIVE OF THE TEN COLUMNS COME FROM LEFT JOINs — dbo.PatientBasic (three), dbo.Staff and dbo.Branch —
    // and none of those relationships is a foreign key, so all five are genuinely nullable. Both endpoints
    // coerce every one to "": the DataTable code they replace called DBNull.Value.ToString(), which is "",
    // so a null here would newly render as the word "null" in two tables.
    public class AppointmentSearchItem
    {
        public int PatientAppointment_ID { get; set; }
        public string Patient_ID { get; set; } = string.Empty;

        public string? Patient_Name { get; set; }
        public string? Patient_Phone { get; set; }
        public string? Patient_Email { get; set; }

        // COALESCE(t.PjAppType_Name, pa.PjAppType_ID) — the lookup's name, falling back to the raw id when
        // the LEFT JOIN misses, so a retired appointment type still shows something. pa.PjAppType_ID is
        // NOT NULL, which makes the COALESCE total; it stays nullable here to match the endpoints, which
        // coerce it like the other four.
        public string? PjAppType_Name { get; set; }

        public string PatientAppointment_Status { get; set; } = string.Empty;

        public string? Staff_Name { get; set; }
        public string? Branch_Name { get; set; }

        // The composed start DATETIME described above — not the bare DATE column. Nullable for the same
        // reason as PatientAppointmentItem's: the code this replaces tested `== DBNull.Value` and rendered
        // "" for one, and Dapper throws mapping a NULL onto a non-nullable DateTime.
        public DateTime? PatientAppointment_Date { get; set; }
    }
}
