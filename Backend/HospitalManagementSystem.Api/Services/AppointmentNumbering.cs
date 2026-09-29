namespace HospitalManagementSystem.Api.Services;

internal static class AppointmentNumbering
{
    // Zero means full. Both availability previews and locked creation use this rule.
    public static int NextAvailable(int capacity, IEnumerable<int> occupiedNumbers)
    {
        var occupied = occupiedNumbers.ToHashSet();
        for (var number = 1; number <= capacity; number++)
        {
            if (!occupied.Contains(number)) return number;
        }
        return 0;
    }
}
