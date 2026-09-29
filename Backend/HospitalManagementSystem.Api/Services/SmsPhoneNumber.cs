using System.Text.RegularExpressions;

namespace HospitalManagementSystem.Api.Services;

public static class SmsPhoneNumber
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var number = Regex.Replace(value.Trim(), @"[\s()\-]", "");
        if (number.StartsWith('+')) number = number[1..];
        else if (number.StartsWith("00")) number = number[2..];
        if (Regex.IsMatch(number, @"^0[1-9][0-9]{8}$")) number = "94" + number[1..];
        return Regex.IsMatch(number, @"^94[1-9][0-9]{8}$") ? number : null;
    }
}
