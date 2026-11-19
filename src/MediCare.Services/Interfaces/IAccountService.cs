using MediCare.Services.Account;

namespace MediCare.Services.Interfaces;

public interface IAccountService
{
    Task<ServiceResult> RegisterPatientAsync(RegisterPatientDto dto);
    Task<ServiceResult> RegisterDoctorAsync(RegisterDoctorDto dto);
    Task<LoginResult> LoginAsync(string email, string password);
}
