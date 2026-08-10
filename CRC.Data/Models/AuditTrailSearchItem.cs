namespace CRC.Data.Models
{
    // One row of the SUPERUSER audit page, from spAuditTrails_Search — a row of dbo.AuditTrails with the
    // actor's name joined on and the timestamp shifted into local time. This is the human-readable face of
    // the `@User_ID` actor parameter that nineteen stored procedures write (CoreFlow.md §0.1, §9).
    //
    // 🔴 AuditTrail_EventMYT IS NOT THE STORED VALUE. dbo.AuditTrails.AuditTrail_EventUTC is UTC; the
    // procedure returns `DATEADD(HOUR, 8, …)` under a different name because the portal is Malaysian and
    // Malaysia has no daylight saving. The two date FILTERS are applied to the same shifted expression, so
    // filtering and display agree — but the ORDER BY is on the raw UTC column, which is the same ordering
    // by construction. Nothing here should ever compare this value against SYSUTCDATETIME().
    //
    // User_ID is `ISNULL(a.User_Id, 0)` and User_Name is `ISNULL(u.User_Name, '')`, so neither is null even
    // when the join misses — and the join CAN miss, in two different ways that look identical on screen:
    // an audit row whose actor parameter was dropped (User_Id = 0, the silent failure §0.1 warns about),
    // and an audit row whose user has since been deleted from dbo.Users. Both render as a blank name. The
    // id tells them apart: 0 is the first, anything else is the second.
    //
    // Both are typed nullable anyway, because the endpoint's defensive `== DBNull` coercions predate the
    // ISNULLs and are preserved verbatim.
    public class AuditTrailSearchItem
    {
        public int? User_ID { get; set; }
        public string? User_Name { get; set; }
        public DateTime? AuditTrail_EventMYT { get; set; }
        public string? AuditTrail_Action { get; set; }
        public string? AuditTrail_Category { get; set; }
        public string? AuditTrail_Summary { get; set; }
    }
}
