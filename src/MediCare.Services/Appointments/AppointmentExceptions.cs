using MediCare.Data.Entities;

namespace MediCare.Services.Appointments;

public class SlotUnavailableException : Exception
{
    public SlotUnavailableException(string message = "The selected appointment slot is no longer available.")
        : base(message)
    {
    }

    public SlotUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public class InvalidStateTransitionException : Exception
{
    public AppointmentStatus CurrentStatus { get; }
    public AppointmentStatus AttemptedStatus { get; }

    public InvalidStateTransitionException(
        AppointmentStatus currentStatus,
        AppointmentStatus attemptedStatus,
        string message)
        : base(message)
    {
        CurrentStatus = currentStatus;
        AttemptedStatus = attemptedStatus;
    }

    public InvalidStateTransitionException(string message)
        : base(message)
    {
    }
}

public class LeadTimeViolationException : Exception
{
    public int RequiredLeadHours { get; }

    public LeadTimeViolationException(int requiredLeadHours, string message = "Appointments can only be cancelled or rescheduled at least 2 hours prior to the start time.")
        : base(message)
    {
        RequiredLeadHours = requiredLeadHours;
    }
}
