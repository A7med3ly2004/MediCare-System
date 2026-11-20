namespace MediCare.Services.MedicalRecords;

public class InvalidVisitStateException : Exception
{
    public InvalidVisitStateException(string message) : base(message)
    {
    }
}

public class MedicalRecordNotFoundException : Exception
{
    public MedicalRecordNotFoundException(string message = "Medical record not found.") : base(message)
    {
    }
}
