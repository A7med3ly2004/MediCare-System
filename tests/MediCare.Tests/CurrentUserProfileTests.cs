using MediCare.Web.Services;
using Xunit;

namespace MediCare.Tests;

public class CurrentUserProfileTests
{
    [Theory]
    [InlineData("Dr. Mariam Ahmed", "M")]
    [InlineData("Dr. Dr. Ahmed Mostafa", "A")]
    [InlineData("Ahmed Tarek", "A")]
    [InlineData("Doctor Omar Hassan", "O")]
    [InlineData("Clinic Administrator", "C")]
    [InlineData("Heba El Sayed", "H")]
    [InlineData("", "?")]
    [InlineData(null, "?")]
    public void GetInitial_ExtractsCorrectInitial(string? name, string expected)
    {
        var result = CurrentUserProfileService.GetInitial(name!);
        Assert.Equal(expected, result);
    }
}
