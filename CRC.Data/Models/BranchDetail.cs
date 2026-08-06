namespace CRC.Data.Models
{
    // A full dbo.Branch row. ONE model serves TWO procedures — spBranch_ListAll and spBranch_GetById
    // select the same seven columns in the same order, so a separate BranchListItem would have been two
    // names for one shape. (spBranch_ListActive is the odd one out: three columns only, hence BranchOption.)
    //
    // Property names mirror the column names because Dapper maps by name and Prompt 1 may not alias the
    // .sql; see LocationLookupItem for the longer version of that argument.
    //
    // TWO NULLABILITY SURPRISES, both of which the Web layer must keep coercing:
    //
    //   • Organization_ID and Organization_Name are NULL-able on dbo.Branch even though spBranch_Insert
    //     hard-refuses a blank Organization_ID with RAISERROR. So the schema permits a state the only
    //     supported write path cannot produce — but a row inserted by hand, or by an older build, can and
    //     does have them NULL. The old DataTable code returned "" for those (DBNull.ToString() is ""), and
    //     BranchController still maps `?? string.Empty` to keep that JSON identical.
    //
    //   • Branch_Status is BIT NOT NULL in the table, yet the controller has always coerced DBNull to
    //     false. It is `bool?` here so that coercion stays reproducible rather than becoming an exception:
    //     Dapper throws when it maps a NULL onto a non-nullable bool, which would turn a harmless "false"
    //     into a 500.
    public class BranchDetail
    {
        public string Branch_ID { get; set; } = string.Empty;
        public string Branch_Name { get; set; } = string.Empty;
        public string Branch_Location { get; set; } = string.Empty;
        public string Branch_State { get; set; } = string.Empty;
        public bool? Branch_Status { get; set; }
        public string? Organization_ID { get; set; }
        public string? Organization_Name { get; set; }
    }
}
