namespace CRC.Web.Models
{
    public class PasswordPolicyOptions
    {
        public bool RequireDigit { get; set; } = true;
        public bool RequireLowercase { get; set; } = true;
        public bool RequireNonAlphanumeric { get; set; } = true;
        public bool RequireUppercase { get; set; } = true;
        public int RequiredLength { get; set; } = 12;
        public int RequiredUniqueChars { get; set; } = 2;
    }
}
