using Serilog;
using Serilog.Events;

namespace CRC.Web.Infrastructure
{
    /// Dedicated audit log channel. Writes to a separate "audit-*.log" file via a
    /// Serilog sub-logger configured in Program.cs. Use for security-relevant
    /// events: login attempts, viewing/downloading sensitive resources, etc.
    public static class AuditLog
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext("AuditChannel", true);

        public static void LoginSucceeded(HttpContext context, string username, int? userId, string userType)
        {
            _logger.Write(LogEventLevel.Information,
                "AUDIT Login succeeded. Username={Username} UserId={UserId} UserType={UserType}",
                username, userId, userType);
        }

        public static void LoginFailed(HttpContext context, string username, string reason)
        {
            _logger.Write(LogEventLevel.Warning,
                "AUDIT Login failed. Username={Username} Reason={Reason}",
                username, reason);
        }

        public static void LoginLockoutTriggered(HttpContext context, string username, int failedCount, DateTime? lockoutEndUtc)
        {
            _logger.Write(LogEventLevel.Warning,
                "AUDIT Login lockout triggered. Username={Username} FailedCount={FailedCount} LockoutEndUtc={LockoutEndUtc}",
                username, failedCount, lockoutEndUtc);
        }

        public static void LoginAttemptWhileLocked(HttpContext context, string username, DateTime lockoutEndUtc)
        {
            _logger.Write(LogEventLevel.Warning,
                "AUDIT Login attempt while account locked. Username={Username} LockoutEndUtc={LockoutEndUtc}",
                username, lockoutEndUtc);
        }

        public static void LoginRateLimited(HttpContext context, string remoteIp)
        {
            _logger.Write(LogEventLevel.Warning,
                "AUDIT Login rate limited. RemoteIp={RemoteIp}",
                remoteIp);
        }

        public static void AccountUnlocked(HttpContext context, int targetUserId, string targetUsername, string actorUsername)
        {
            _logger.Write(LogEventLevel.Information,
                "AUDIT Account unlocked. TargetUserId={TargetUserId} TargetUsername={TargetUsername} ActorUsername={ActorUsername}",
                targetUserId, targetUsername, actorUsername);
        }

        public static void Logout(HttpContext context, string username)
        {
            _logger.Write(LogEventLevel.Information,
                "AUDIT Logout. Username={Username}", username);
        }

        public static void DocumentSearched(HttpContext context, string mode, string? individualName, string? documentType, int resultCount)
        {
            _logger.Write(LogEventLevel.Information,
                "AUDIT Documents searched. Mode={Mode} Individual={Individual} DocType={DocType} Results={ResultCount}",
                mode, individualName ?? "", documentType ?? "", resultCount);
        }

        public static void AppointmentCreated(HttpContext context, int appointmentId, string patientId, string staffId,
            DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime, string appointmentTypeId, string branchId, string status)
        {
            _logger.Write(LogEventLevel.Information,
                "AUDIT Appointment created. AppointmentId={AppointmentId} PatientId={PatientId} StaffId={StaffId} Date={Date} From={StartTime} To={EndTime} TypeId={TypeId} BranchId={BranchId} Status={Status}",
                appointmentId, patientId, staffId, appointmentDate.ToString("yyyy-MM-dd"),
                startTime.ToString(@"hh\:mm"), endTime.ToString(@"hh\:mm"), appointmentTypeId, branchId, status);
        }

        public static void AppointmentUpdated(HttpContext context, int appointmentId, string patientId, string staffId,
            DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime, string appointmentTypeId, string branchId, string status)
        {
            _logger.Write(LogEventLevel.Information,
                "AUDIT Appointment updated. AppointmentId={AppointmentId} PatientId={PatientId} StaffId={StaffId} Date={Date} From={StartTime} To={EndTime} TypeId={TypeId} BranchId={BranchId} Status={Status}",
                appointmentId, patientId, staffId, appointmentDate.ToString("yyyy-MM-dd"),
                startTime.ToString(@"hh\:mm"), endTime.ToString(@"hh\:mm"), appointmentTypeId, branchId, status);
        }

        public static void AppointmentDeleted(HttpContext context, int appointmentId)
        {
            _logger.Write(LogEventLevel.Warning,
                "AUDIT Appointment deleted. AppointmentId={AppointmentId}",
                appointmentId);
        }

        public static void StaffSlotRangeCreated(HttpContext context, string staffId,
            DateTime fromDate, DateTime toDate, TimeSpan startTime, TimeSpan endTime,
            int createdCount, int skippedExistingCount)
        {
            _logger.Write(LogEventLevel.Information,
                "AUDIT Staff slot range created. StaffId={StaffId} FromDate={FromDate} ToDate={ToDate} StartTime={StartTime} EndTime={EndTime} Created={CreatedCount} SkippedExisting={SkippedExistingCount}",
                staffId, fromDate.ToString("yyyy-MM-dd"), toDate.ToString("yyyy-MM-dd"),
                startTime.ToString(@"hh\:mm"), endTime.ToString(@"hh\:mm"),
                createdCount, skippedExistingCount);
        }

        public static void StaffSlotDeleted(HttpContext context, int staffSlotId)
        {
            _logger.Write(LogEventLevel.Warning,
                "AUDIT Staff slot deleted. StaffSlotId={StaffSlotId}",
                staffSlotId);
        }

        public static void StaffCreated(HttpContext context, string staffId, string name, string nric,
            string phone, string email, string staffTypeId, string staffBase)
        {
            _logger.Write(LogEventLevel.Information,
                "AUDIT Staff created. StaffId={StaffId} Name={Name} NRIC={NRIC} Phone={Phone} Email={Email} StaffTypeId={StaffTypeId} StaffBase={StaffBase}",
                staffId, name, nric, phone, email, staffTypeId, staffBase);
        }

        public static void StaffUpdated(HttpContext context, string staffId, string name, string nric,
            string phone, string email, string staffTypeId, string staffBase)
        {
            _logger.Write(LogEventLevel.Information,
                "AUDIT Staff updated. StaffId={StaffId} Name={Name} NRIC={NRIC} Phone={Phone} Email={Email} StaffTypeId={StaffTypeId} StaffBase={StaffBase}",
                staffId, name, nric, phone, email, staffTypeId, staffBase);
        }

        public static void StaffDeleted(HttpContext context, string staffId)
        {
            _logger.Write(LogEventLevel.Warning,
                "AUDIT Staff deleted. StaffId={StaffId}",
                staffId);
        }

        public static void PatientDocumentUploaded(HttpContext context, string patientId, int documentId,
            string docTypeId, string blobName, string fileName, long sizeBytes)
        {
            _logger.Write(LogEventLevel.Information,
                "AUDIT Patient document uploaded. PatientId={PatientId} DocumentId={DocumentId} DocTypeId={DocTypeId} BlobName={BlobName} FileName={FileName} SizeBytes={SizeBytes}",
                patientId, documentId, docTypeId, blobName, fileName, sizeBytes);
        }

        // A download is really the minting of a short-lived read SAS for one document — the bytes are then
        // fetched from storage directly, where the application can no longer see them. This line is the only
        // record that someone was handed access to that patient's file.
        public static void PatientDocumentDownloaded(HttpContext context, string patientId, int documentId, string fileName)
        {
            _logger.Write(LogEventLevel.Information,
                "AUDIT Patient document downloaded. PatientId={PatientId} DocumentId={DocumentId} FileName={FileName}",
                patientId, documentId, fileName);
        }

        public static void PatientDocumentDeleted(HttpContext context, string patientId, int documentId, string blobName)
        {
            _logger.Write(LogEventLevel.Warning,
                "AUDIT Patient document deleted. PatientId={PatientId} DocumentId={DocumentId} BlobName={BlobName}",
                patientId, documentId, blobName);
        }

        // Written by the patient cascade delete: every document belonging to the patient was removed from the
        // private container along with the patient record.
        public static void PatientDocumentsPurged(HttpContext context, string patientId, int blobCount)
        {
            _logger.Write(LogEventLevel.Warning,
                "AUDIT Patient documents purged. PatientId={PatientId} BlobCount={BlobCount}",
                patientId, blobCount);
        }
    }
}
