namespace CRC.Data.Models
{
    // Grid 2 of spStaff_GetPerformance: how many hours this clinician has spent per appointment type.
    //
    // It is computed from dbo.PatientAppointment, NOT from the journey rows grid 1 counts, and it counts
    // only appointments whose PatientAppointment_Status is exactly 'Attended' — a booked-but-not-attended
    // hour contributes nothing. The value is
    // SUM(DATEDIFF(MINUTE, start, end)) / 60.0 CAST to DECIMAL(10, 2), grouped by type and ordered by
    // PjAppType_Name; the caller must not re-sort.
    //
    // PjAppType_Name is a LEFT JOIN onto LU_PJ_APP_TYPE, so it is null for an appointment whose type code
    // no longer matches a lookup row — nothing prevents that, since none of these ids is a foreign key.
    // All three are nullable so the endpoint's existing "" / 0 coercions stay reproducible rather than
    // becoming an exception (see BranchDetail for the same argument).
    public class StaffPerformanceHours
    {
        public string? PjAppType_ID { get; set; }
        public string? PjAppType_Name { get; set; }
        public decimal? TotalHours { get; set; }
    }
}
