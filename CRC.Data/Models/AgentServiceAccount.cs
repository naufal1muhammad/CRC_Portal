namespace CRC.Data.Models
{
    // The one dbo.Users row the Agent API performs its work as — Username = 'AGENT_SERVICE', seeded by
    // CRC.Database/Scripts/Seed_Users.sql. Maps the four columns spAgentUsers_GetServiceAccount selects
    // and no others: NOT Password_Hash and NOT User_Email. The caller needs an identity to build a
    // ClaimsPrincipal from, not a credential to check — this account never logs in and its password is a
    // random secret that was generated once and thrown away.
    //
    // User_ID is the property everything else here exists for: it becomes the ClaimTypes.NameIdentifier
    // claim, which is what DatabaseHelper.CurrentUserId reads and SqlData passes as @User_ID to the 19
    // audit-actor procedures (CoreFlow.md §0.1, §13.3). It is INT IDENTITY, so its value differs between
    // a local CRC_DB and Azure SQL — which is why it is resolved per request by username and never
    // stored in configuration.
    public class AgentServiceAccount
    {
        public int User_ID { get; set; }
        public string Username { get; set; } = string.Empty;
        public string User_Name { get; set; } = string.Empty;
        public int User_Type { get; set; }
    }
}
