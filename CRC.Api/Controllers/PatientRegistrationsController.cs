using System.Globalization;
using CRC.Api.Models;
using CRC.Data.Models;
using CRC.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CRC.Api.Controllers;

[ApiController]
[Route("api/v1/patient-registrations")]
public sealed class PatientRegistrationsController : ControllerBase
{
    private readonly PatientBasicRepository _repo;
    private readonly IConfiguration _config;

    public PatientRegistrationsController(PatientBasicRepository repo, IConfiguration config)
    {
        _repo = repo;
        _config = config;
    }

    /// <summary>
    /// Public patient self-registration.
    /// Inserts into dbo.PatientBasic with Source_ID fixed (default 8) and Patient_IsApproved = 0.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register([FromBody] PatientRegistrationRequest req)
    {
        // ApiController handles DataAnnotations automatically, but we keep extra rules here.

        var name = (req.Name ?? string.Empty).Trim();
        var email = (req.Email ?? string.Empty).Trim();
        var phone = (req.Phone ?? string.Empty).Trim();
        var raceId = (req.RaceId ?? string.Empty).Trim();
        var gender = (req.Gender ?? string.Empty).Trim().ToUpperInvariant();
        var religionId = (req.ReligionId ?? string.Empty).Trim();
        var maritalStatusId = (req.MaritalStatusId ?? string.Empty).Trim();
        var occupationId = (req.OccupationId ?? string.Empty).Trim();

        var resState = (req.ResState ?? string.Empty).Trim();
        var resCity = (req.ResCity ?? string.Empty).Trim();
        var resPostcode = (req.ResPostcode ?? string.Empty).Trim();
        var addLine1 = (req.AddressLine1 ?? string.Empty).Trim();
        var addLine2 = (req.AddressLine2 ?? string.Empty).Trim();

        var emergencyName = (req.EmergencyContactName ?? string.Empty).Trim();
        var emergencyRel = (req.EmergencyContactRelationship ?? string.Empty).Trim();
        var emergencyNum = (req.EmergencyContactNumber ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(phone) ||
            string.IsNullOrWhiteSpace(raceId) ||
            string.IsNullOrWhiteSpace(gender) ||
            string.IsNullOrWhiteSpace(religionId) ||
            string.IsNullOrWhiteSpace(maritalStatusId) ||
            string.IsNullOrWhiteSpace(occupationId) ||
            string.IsNullOrWhiteSpace(resState) ||
            string.IsNullOrWhiteSpace(resCity) ||
            string.IsNullOrWhiteSpace(resPostcode) ||
            string.IsNullOrWhiteSpace(addLine1) ||
            string.IsNullOrWhiteSpace(emergencyName) ||
            string.IsNullOrWhiteSpace(emergencyRel) ||
            string.IsNullOrWhiteSpace(emergencyNum))
        {
            return BadRequest(new { success = false, message = "Please fill in all mandatory fields." });
        }

        // NRIC: store digits only.
        var nricDigits = new string((req.NRIC ?? string.Empty).Where(char.IsDigit).ToArray());
        if (nricDigits.Length != 12)
        {
            return BadRequest(new { success = false, message = "NRIC must be exactly 12 digits." });
        }

        if (!TryParseDate(req.BirthDate, out var birthDate))
        {
            return BadRequest(new { success = false, message = "Invalid Birth Date. Use yyyy-MM-dd, dd/MM/yyyy or MM/dd/yyyy." });
        }

        var computedAge = CalculateAge(birthDate);
        var age = req.Age.GetValueOrDefault(computedAge);
        if (age <= 0 || age > 150)
        {
            age = computedAge;
        }
        else
        {
            // Keep data consistent if the client passes a mismatched age.
            if (Math.Abs(age - computedAge) > 1)
                age = computedAge;
        }

        if (gender is not ("MALE" or "FEMALE"))
        {
            return BadRequest(new { success = false, message = "Gender must be MALE or FEMALE." });
        }

        // iFOBT rules (match portal behavior): if not COMPLETE, force-clear completion fields.
        var ifobtStatus = req.IFOBTStatus;
        DateTime? ifobtCompletionDate = null;
        var ifobtResults = req.IFOBTResults;

        if (ifobtStatus == true)
        {
            if (!TryParseDate(req.IFOBTCompletionDate, out var ifobtDate))
            {
                return BadRequest(new { success = false, message = "Invalid iFOBT Completion Date. Use yyyy-MM-dd, dd/MM/yyyy or MM/dd/yyyy." });
            }
            ifobtCompletionDate = ifobtDate.Date;

            if (!ifobtResults.HasValue)
            {
                return BadRequest(new { success = false, message = "iFOBT Results is required when iFOBT Status is COMPLETE." });
            }
        }
        else
        {
            ifobtCompletionDate = null;
            ifobtResults = null;
        }

        // Source_ID fixed (default 8)
        var sourceId = (_config["PatientRegistration:SourceId"] ?? "8").Trim();

        var model = new PatientBasicCreateModel
        {
            PatientName = name.ToUpperInvariant(),
            PatientEmail = email,
            PatientPhone = phone,
            PatientNRIC = nricDigits,
            PatientBirthDate = birthDate,
            PatientAge = age,
            RaceId = raceId,
            SourceId = sourceId,
            PatientGender = gender,
            ReligionId = religionId,
            MaritalStatusId = maritalStatusId,
            OccupationId = occupationId,
            PatientResState = resState,
            PatientResCity = resCity,
            PatientResPostcode = resPostcode,
            PatientAddLine1 = addLine1,
            PatientAddLine2 = string.IsNullOrWhiteSpace(addLine2) ? null : addLine2,
            PatientEmergencyName = emergencyName.ToUpperInvariant(),
            PatientEmergencyRelationship = emergencyRel,
            PatientEmergencyNumber = emergencyNum,
            PatientIFOBTStatus = ifobtStatus,
            PatientIFOBTCompletionDate = ifobtCompletionDate,
            PatientIFOBTResults = ifobtResults,
            PatientIsApproved = false
        };

        try
        {
            var newPatientId = await _repo.InsertAsync(model);
            if (string.IsNullOrWhiteSpace(newPatientId))
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { success = false, message = "Patient was created but no ID was returned." });
            }

            return Ok(new { success = true, patientId = newPatientId, isApproved = false });
        }
        catch
        {
            // Intentionally do not leak DB details.
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { success = false, message = "An unexpected error occurred while creating the patient." });
        }
    }

    private static bool TryParseDate(string? raw, out DateTime dt)
    {
        dt = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var s = raw.Trim();
        var formats = new[]
        {
            "yyyy-MM-dd",
            "dd/MM/yyyy",
            "MM/dd/yyyy",
            "yyyy/MM/dd"
        };

        return DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)
               || DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)
               || DateTime.TryParse(s, out dt);
    }

    private static int CalculateAge(DateTime birthDate)
    {
        var today = DateTime.Today;
        var age = today.Year - birthDate.Year;
        if (birthDate.Date > today.AddYears(-age)) age--;
        return age;
    }
}
