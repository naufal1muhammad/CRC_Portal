using System.Data;
using CRC.Data.Database;
using CRC.Data.Models;
using Microsoft.Data.SqlClient;

namespace CRC.Data.Repositories;

public sealed class PatientBasicRepository
{
    private readonly DatabaseHelper _db;

    public PatientBasicRepository(DatabaseHelper db)
    {
        _db = db;
    }

    /// <summary>
    /// Inserts a new record into dbo.PatientBasic using dbo.spPatientBasic_Insert.
    /// Returns the newly generated Patient_ID (e.g., PAT-000001).
    /// </summary>
    public async Task<string> InsertAsync(PatientBasicCreateModel model, CancellationToken cancellationToken = default)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        var parameters = new List<SqlParameter>
        {
            new("@Patient_Name", model.PatientName),
            new("@Patient_Email", model.PatientEmail),
            new("@Patient_Phone", model.PatientPhone),
            new("@Patient_NRIC", model.PatientNRIC),
            new("@Patient_BirthDate", model.PatientBirthDate),
            new("@Patient_Age", model.PatientAge),
            new("@Race_ID", model.RaceId),
            new("@Source_ID", model.SourceId),
            new("@Patient_Gender", model.PatientGender),
            new("@Religion_ID", model.ReligionId),
            new("@MaritalStatus_ID", model.MaritalStatusId),
            new("@Occupation_ID", model.OccupationId),

            new("@Patient_ResState", model.PatientResState),
            new("@Patient_ResCity", model.PatientResCity),
            new("@Patient_ResPostcode", model.PatientResPostcode),
            new("@Patient_AddLine1", model.PatientAddLine1),
            new SqlParameter("@Patient_AddLine2", SqlDbType.VarChar, -1)
            {
                Value = string.IsNullOrWhiteSpace(model.PatientAddLine2)
                    ? (object)DBNull.Value
                    : model.PatientAddLine2!
            },

            new("@Patient_EmergencyName", model.PatientEmergencyName),
            new("@Patient_EmergencyRelationship", model.PatientEmergencyRelationship),
            new("@Patient_EmergencyNumber", model.PatientEmergencyNumber),

            new SqlParameter("@Patient_iFOBTStatus", SqlDbType.Bit) { Value = model.PatientIFOBTStatus.HasValue ? (object)model.PatientIFOBTStatus.Value : DBNull.Value },
            new SqlParameter("@Patient_iFOBTCompletionDate", SqlDbType.Date) { Value = model.PatientIFOBTCompletionDate.HasValue ? (object)model.PatientIFOBTCompletionDate.Value.Date : DBNull.Value },
            new SqlParameter("@Patient_iFOBTResults", SqlDbType.Bit) { Value = model.PatientIFOBTResults.HasValue ? (object)model.PatientIFOBTResults.Value : DBNull.Value },

            new SqlParameter("@Patient_IsApproved", SqlDbType.Bit) { Value = model.PatientIsApproved }
        };

        var outParam = new SqlParameter("@NewPatient_ID", SqlDbType.VarChar, 100)
        {
            Direction = ParameterDirection.Output
        };
        parameters.Add(outParam);

        // DatabaseHelper does not accept CancellationToken currently.
        await _db.ExecuteNonQueryAsync("spPatientBasic_Insert", parameters.ToArray());

        var newId = outParam.Value?.ToString() ?? string.Empty;
        return newId;
    }
}
