namespace CRC.Data.Models
{
    // 🔴 WHY THIS IS AN ENUM AND NOT A STRING — THE MOST REUSABLE CONVENTION IN THE DATA LAYER.
    //
    // SaveAppointmentAsync validates the slots a booking is trying to consume, and it HAS to: the check
    // runs against rows read inside its own transaction, under the lock that stops two administrators
    // taking the same hour (CoreFlow.md §6.6). So the data layer is the only place that can know WHAT
    // failed.
    //
    // It is not, however, the place that knows WHAT THE USER IS TOLD. A user-facing message is a
    // presentation decision: it is worded for one screen, it is what a JavaScript file matches on, and
    // changing it is a product change, not a data change. Putting one in CRC.Data would mean a controller
    // could no longer alter its own copy without editing the data layer, and a second screen calling the
    // same method would be stuck with the first screen's wording.
    //
    //     THE DATA LAYER DECIDES WHAT FAILED. THE CONTROLLER DECIDES WHAT THE USER IS TOLD.
    //
    // PatientController.SaveAppointment maps every value below to the exact string it showed before the
    // Dapper migration — character for character, because 59 JavaScript files and every user of the
    // Appointment tab depend on those strings. That mapping is a `switch` in the controller, in the open,
    // and it is the only place any of those sentences appears.
    //
    // Copy this shape for the next flow that needs to fail for several distinct reasons. The alternatives
    // are all worse: a bool loses the reason, an exception per reason turns expected validation into
    // control flow through `catch`, and a message string moves the product's voice into CRC.Data.
    public enum AppointmentSaveFailure
    {
        // The transaction committed. Every other value means it was ROLLED BACK and nothing was written —
        // no appointment row, no slot change, no dbo.AuditTrails row.
        Ok = 0,

        // At least one requested StaffSlot_ID was not among the slots the in-transaction read returned.
        //
        // 🔴 THIS IS THE BROADEST OF THE FOUR AND IT SWALLOWS THE NEXT TWO IN PRACTICE. The read is
        // spStaffSlots_List filtered by @Staff_ID AND @FromDate = @ToDate = the appointment's date, so a
        // slot belonging to another clinician, or to another day, never comes back at all and is simply
        // "missing" from the count. Measured against the running site before and after this migration: a
        // non-existent id, another staff member's slot and another date's slot all produce THIS reason.
        SlotNotFound,

        // A returned slot does not belong to the requested clinician.
        //
        // 🔴 RESERVED AND CURRENTLY UNREACHABLE, AND IT WAS UNREACHABLE BEFORE THE MIGRATION TOO.
        // spStaffSlots_List filters `WHERE Staff_ID = @Staff_ID` and does not project Staff_ID, so
        // another clinician's slot never reaches the validation and is refused by SlotNotFound instead.
        // The pre-migration controller did write the check, comparing a field it had just populated from
        // the request against that same request value — a tautology, not a test. This value is kept, and
        // the controller still maps it to the exact sentence it always did, so the reason is there the
        // day the read projects Staff_ID or stops filtering on it.
        SlotWrongStaff,

        // A returned slot's SlotDate is not the appointment's date. Also unreachable, because the read is
        // bounded by @FromDate = @ToDate = that date — but unlike SlotWrongStaff it IS checked in code,
        // because spStaffSlots_List projects SlotDate and so there is a real value to assert.
        SlotWrongDate,

        // 🔴 THE ONE THAT MATTERS UNDER CONCURRENCY, AND THE WHOLE REASON THE READ IS IN THE TRANSACTION.
        // A requested slot already carries a PatientAppointment_ID belonging to a DIFFERENT appointment.
        //
        // A slot carrying THIS appointment's own id is allowed, and that permission is what makes an edit
        // work: re-saving an appointment over some of the hours it already holds must not be refused as a
        // double booking.
        SlotTaken,

        // The requested hours are not a contiguous run — sorted by start time, some slot does not begin
        // exactly one hour after the one before it. An appointment is one unbroken block, which is why
        // only its first start and last end are stored on dbo.PatientAppointment.
        SlotsNotConsecutive,

        // spPatientAppointment_Insert returned no usable identity through @NewPatientAppointment_ID. It
        // has no path that can do that, so this is defensive — but the alternative is assigning slots to
        // appointment 0, so it rolls back rather than continuing.
        InsertFailed
    }
}
