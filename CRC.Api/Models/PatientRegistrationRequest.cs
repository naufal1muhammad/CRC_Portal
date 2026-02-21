using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CRC.Api.JsonConverters;

namespace CRC.Api.Models;

public sealed class PatientRegistrationRequest
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Phone { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string NRIC { get; set; } = string.Empty;

    /// <summary>
    /// Accepts: yyyy-MM-dd, dd/MM/yyyy, MM/dd/yyyy
    /// </summary>
    [Required]
    public string BirthDate { get; set; } = string.Empty;

    /// <summary>
    /// Optional. If not provided, API will compute from BirthDate.
    /// </summary>
    public int? Age { get; set; }

    [Required, StringLength(100)]
    public string RaceId { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Gender { get; set; } = string.Empty; // MALE / FEMALE

    [Required, StringLength(100)]
    public string ReligionId { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string MaritalStatusId { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string OccupationId { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string ResState { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string ResCity { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string ResPostcode { get; set; } = string.Empty;

    [Required]
    public string AddressLine1 { get; set; } = string.Empty;

    public string? AddressLine2 { get; set; }

    [Required, StringLength(100)]
    public string EmergencyContactName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string EmergencyContactRelationship { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string EmergencyContactNumber { get; set; } = string.Empty;

    // iFOBT (optional)

    /// <summary>
    /// Accepts: true/false, 1/0, COMPLETE/INCOMPLETE
    /// </summary>
    [JsonConverter(typeof(IfobtStatusJsonConverter))]
    public bool? IFOBTStatus { get; set; }

    /// <summary>
    /// Required when IFOBTStatus is COMPLETE/true.
    /// Accepts: yyyy-MM-dd, dd/MM/yyyy, MM/dd/yyyy
    /// </summary>
    public string? IFOBTCompletionDate { get; set; }

    /// <summary>
    /// Accepts: true/false, 1/0, POSITIVE/NEGATIVE
    /// </summary>
    [JsonConverter(typeof(IfobtResultsJsonConverter))]
    public bool? IFOBTResults { get; set; }
}
