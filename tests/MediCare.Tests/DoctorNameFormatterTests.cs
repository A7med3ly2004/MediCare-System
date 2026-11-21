using MediCare.Web.Helpers;
using Xunit;

namespace MediCare.Tests;

public class DoctorNameFormatterTests
{
    [Theory]
    [InlineData("Dr. Mariam Ahmed", "Dr. Mariam Ahmed")]
    [InlineData("Dr. Dr. Ahmed Mostafa", "Dr. Ahmed Mostafa")]
    [InlineData("Dr. Dr. Dr. Salma Mahmoud", "Dr. Salma Mahmoud")]
    [InlineData("Dr Mariam Ahmed", "Dr. Mariam Ahmed")]
    [InlineData("Doctor Omar Hassan", "Dr. Omar Hassan")]
    [InlineData("Ahmed Mostafa", "Dr. Ahmed Mostafa")]
    [InlineData("dr. hana ibrahim", "Dr. hana ibrahim")]
    [InlineData("  Dr.   Nour El Din Ali  ", "Dr. Nour El Din Ali")]
    [InlineData("", "Dr.")]
    [InlineData("   ", "Dr.")]
    [InlineData(null, "Dr.")]
    public void Format_NormalizesDoctorNamesProperly(string? input, string expected)
    {
        var result = DoctorNameFormatter.Format(input);
        Assert.Equal(expected, result);
    }
}
