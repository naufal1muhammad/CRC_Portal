namespace CRC.Data.Models;

public sealed class PatientBasicCreateModel
{
    public string PatientName { get; set; } = string.Empty;
    public string PatientEmail { get; set; } = string.Empty;
    public string PatientPhone { get; set; } = string.Empty;
    public string PatientNRIC { get; set; } = string.Empty;
    public DateTime PatientBirthDate { get; set; }
    public int PatientAge { get; set; }
    public string RaceId { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string PatientGender { get; set; } = string.Empty;
    public string ReligionId { get; set; } = string.Empty;
    public string MaritalStatusId { get; set; } = string.Empty;
    public string OccupationId { get; set; } = string.Empty;

    public string PatientResState { get; set; } = string.Empty;
    public string PatientResCity { get; set; } = string.Empty;
    public string PatientResPostcode { get; set; } = string.Empty;
    public string PatientAddLine1 { get; set; } = string.Empty;
    public string? PatientAddLine2 { get; set; }

    public string PatientEmergencyName { get; set; } = string.Empty;
    public string PatientEmergencyRelationship { get; set; } = string.Empty;
    public string PatientEmergencyNumber { get; set; } = string.Empty;

    public bool? PatientIFOBTStatus { get; set; }
    public DateTime? PatientIFOBTCompletionDate { get; set; }
    public bool? PatientIFOBTResults { get; set; }
}
