using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using CRC.Web.Data;

namespace CRC.Web.Controllers.DocumentManagement
{
    [Authorize]
    public class DocumentManagementController : Controller
    {
        private readonly DatabaseHelper _db;

        public DocumentManagementController(DatabaseHelper db)
        {
            _db = db;
        }

        // GET: /DocumentManagement
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetPatientDocuments()
        {
            // No parameters → @Patient_ID will be NULL inside the SP
            var dt = await _db.ExecuteDataTableAsync("spPatientDocument_List");

            var list = new List<object>();

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new
                {
                    documentId = row["PatientDocument_ID"]?.ToString() ?? "",
                    patientId = row["Patient_ID"]?.ToString() ?? "",
                    patientName = row["Patient_Name"]?.ToString() ?? "",
                    fileName = row["FileName"]?.ToString() ?? "",
                    filePath = row["FilePath"]?.ToString() ?? "",
                    uploadedOn = row["UploadedOn"] is DateTime dtUploaded
                        ? dtUploaded.ToString("yyyy-MM-dd HH:mm")
                        : ""
                });
            }

            return Ok(list);
        }

        [HttpGet]
        public async Task<IActionResult> GetStaffDocuments()
        {
            // No parameters → @Staff_ID will be NULL inside the SP
            var dt = await _db.ExecuteDataTableAsync("spStaffDocument_List");

            var list = new List<object>();

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new
                {
                    documentId = row["StaffDocument_ID"]?.ToString() ?? "",
                    staffId = row["Staff_ID"]?.ToString() ?? "",
                    staffName = row["Staff_Name"]?.ToString() ?? "",
                    fileName = row["FileName"]?.ToString() ?? "",
                    filePath = row["FilePath"]?.ToString() ?? "",
                    uploadedOn = row["UploadedOn"] is DateTime dtUploaded
                        ? dtUploaded.ToString("yyyy-MM-dd HH:mm")
                        : ""
                });
            }

            return Ok(list);
        }

    }
}