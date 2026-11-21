namespace MediCare.Web.Helpers;

/// <summary>
/// Utility to format doctor names consistently and prevent duplicate prefixes (e.g. "Dr. Dr. Ahmed").
/// </summary>
public static class DoctorNameFormatter
{
    /// <summary>
    /// Normalizes a doctor name so that "Dr." is prefixed exactly once.
    /// Handles null, empty, "Dr. Dr. Ahmed", "Dr Ahmed", "Doctor Ahmed", or plain "Ahmed".
    /// </summary>
    public static string Format(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Dr.";

        var trimmed = name.Trim();

        // Repeatedly strip duplicate/redundant title prefixes
        bool stripped;
        do
        {
            stripped = false;
            if (trimmed.StartsWith("Doctor ", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(7).Trim();
                stripped = true;
            }
            else if (trimmed.StartsWith("Dr.", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(3).Trim();
                stripped = true;
            }
            else if (trimmed.StartsWith("Dr ", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(3).Trim();
                stripped = true;
            }
        } while (stripped);

        return string.IsNullOrWhiteSpace(trimmed) ? "Dr." : $"Dr. {trimmed}";
    }
}
