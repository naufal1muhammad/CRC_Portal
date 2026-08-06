namespace CRC.Data.Models
{
    // An active branch, reduced to what a dropdown needs. spBranch_ListActive selects THREE columns —
    // Branch_ID, Branch_Name, Branch_State — where spBranch_ListAll selects seven, so it gets its own
    // model rather than a BranchDetail with four properties silently left at their defaults. A model
    // should describe the result set it maps, or it stops being documentation.
    //
    // Nothing in Prompt 1 calls this: BranchController's own list endpoint shows every branch, active or
    // not. It is wrapped now because spBranch_ListActive is the branch dropdown behind StaffController
    // (Prompt 3) and PatientController (Prompt 5), and the plan's rule is that the prompt reaching a
    // procedure first creates its method and later prompts reuse it.
    public class BranchOption
    {
        public string Branch_ID { get; set; } = string.Empty;
        public string Branch_Name { get; set; } = string.Empty;
        public string Branch_State { get; set; } = string.Empty;
    }
}
