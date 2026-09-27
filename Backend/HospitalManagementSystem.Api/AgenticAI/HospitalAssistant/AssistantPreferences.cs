using System.Globalization;
using System.Text.RegularExpressions;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.AppointmentProposal;

namespace HospitalManagementSystem.Api.AgenticAI.HospitalAssistant;

/// Bounded intent/constraint parsing. Unknown wording asks for clarification; it never invents facts.
public static class AssistantPreferences
{
    public static bool Has(string text, string pattern) => Regex.IsMatch(text, pattern,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public static string? Query(string text, IReadOnlyList<AgentDoctor> doctors)
    {
        // Pronouns refer to the previously selected doctor/specialty and must be
        // resolved by the conversation coordinator. Do not parse "doctor tomorrow"
        // from "book that doctor tomorrow" as a doctor named Tomorrow.
        if (Has(text, @"\b(that|this|selected)\s+(doctor|specialist|one)\b")) return null;

        // Full-name match: "Dr. Neranjani Perera" or "Neranjani Perera"
        var named = doctors.Where(doctor =>
            text.Contains(doctor.Name, StringComparison.OrdinalIgnoreCase) ||
            text.Contains(doctor.Name.Replace("Dr. ", "", StringComparison.OrdinalIgnoreCase), StringComparison.OrdinalIgnoreCase)).ToArray();
        if (named.Length == 1) return named[0].Name;

        // Patients commonly omit the space after the title (for example, "Dr.Silva" or "dr neranjani").
        var drMatch = Regex.Match(text, @"\bdr\.?\s*([\p{L}]+(?:\s+[\p{L}]+)?)", RegexOptions.IgnoreCase);
        if (drMatch.Success)
        {
            var extracted = drMatch.Groups[1].Value.Split(' ')[0];
            // Try to resolve to a known doctor first name before returning raw text
            var byFirstName = doctors.FirstOrDefault(d =>
                d.Name.Replace("Dr. ", "", StringComparison.OrdinalIgnoreCase)
                      .Split(' ')[0]
                      .Equals(extracted, StringComparison.OrdinalIgnoreCase));
            return byFirstName?.Name ?? extracted;
        }

        // "book doctor neranjani" / "i want doctor perera" - extract the word after "doctor"
        var doctorWordMatch = Regex.Match(text, @"\b(?:doctor|doc)\s+([\p{L}]+(?:\s+[\p{L}]+)?)", RegexOptions.IgnoreCase);
        if (doctorWordMatch.Success)
        {
            var candidate = doctorWordMatch.Groups[1].Value.Trim();
            // Match against first name (ignoring "Dr." prefix) of any known doctor
            var byFirstName = doctors.FirstOrDefault(d =>
                d.Name.Replace("Dr. ", "", StringComparison.OrdinalIgnoreCase)
                      .Split(' ')[0]
                      .Equals(candidate.Split(' ')[0], StringComparison.OrdinalIgnoreCase));
            if (byFirstName != null) return byFirstName.Name;
            // No exact first-name match — return the candidate as a free-text search term
            if (candidate.Length >= 3) return candidate;
        }

        foreach (var specialty in doctors.Select(d => d.Specialty).Distinct().OrderByDescending(x => x.Length))
            if (text.Contains(specialty, StringComparison.OrdinalIgnoreCase)) return specialty;
        (string Pattern, string Query)[] aliases = [
            (@"\b(cardiologists?|cardiology|heart doctors?)\b", "Cardiology"),
            (@"\b(eye doctors?|eye specialists?|ophthalmologists?|ophthalmology)\b", "Ophthalmology"),
            (@"\b(dermatologists?|skin doctors?|dermatology)\b", "Dermatology"),
            (@"\b(pediatricians?|paediatricians?|child specialists?)\b", "Pediatrics"),
            (@"\b(general doctors?|general medicine|general practitioners?)\b", "General Medicine"),
            (@"\b(neurologists?|neurology)\b", "Neurology")
        ];
        foreach (var (pattern, query) in aliases)
            if (Has(text, pattern))
            {
                if (query == "Ophthalmology" && !doctors.Any(d => d.Specialty.Contains(query, StringComparison.OrdinalIgnoreCase)))
                    return doctors.Select(d => d.Specialty).FirstOrDefault(s => s.Contains("eye", StringComparison.OrdinalIgnoreCase)) ?? query;
                return query;
            }
        return null;
    }

    public static string? ApplyDates(string text, AssistantState state, DateOnly today)
    {
        if (Has(text, @"\b(any date|earliest|any day|first available)\b"))
        { state.PreferredDate = null; state.ThroughDate = null; }
        var iso = Regex.Match(text, @"\b\d{4}-\d{2}-\d{2}\b");
        DateOnly? date = null;
        if (iso.Success)
        {
            if (!DateOnly.TryParseExact(iso.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return "Please provide a valid date as YYYY-MM-DD.";
            date = parsed;
        }
        else if (Has(text, @"\bday after tomorrow\b")) date = today.AddDays(2);
        else if (Has(text, @"\btomorrow\b")) date = today.AddDays(1);
        else if (Has(text, @"\btoday\b")) date = today;
        else
        {
            foreach (var day in Enum.GetValues<DayOfWeek>())
            {
                if (!Has(text, $@"\b{day}\b")) continue;
                var distance = ((int)day - (int)today.DayOfWeek + 7) % 7;
                date = today.AddDays(distance == 0 ? 7 : distance);
                break;
            }
        }
        if (date.HasValue)
        {
            state.PreferredDate = date;
            state.ThroughDate = null;
        }
        else if (Has(text, @"\bnext week\b"))
        {
            var distance = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
            state.PreferredDate = today.AddDays(distance == 0 ? 7 : distance);
            state.ThroughDate = state.PreferredDate.Value.AddDays(6);
        }
        else if (Has(text, @"\b(january|february|march|april|may|june|july|august|september|october|november|december)\s+\d|\b\d{1,2}[/\-]\d{1,2}\b"))
            return "Please give that date as YYYY-MM-DD so I can check the correct day.";
        if (state.PreferredDate < today) return "Please choose today or a future date.";
        if (Has(text, @"\b(any time|anytime)\b")) state.Period = null;
        else foreach (var period in new[] { "morning", "afternoon", "evening" })
            if (Has(text, $@"\b{period}\b")) state.Period = period;
        if (Has(text, @"\b\d{1,2}(?::\d{2})?\s*(am|pm)\b|\b\d{1,2}:\d{2}\b"))
            return "I can filter by morning, afternoon, or evening. Which would you prefer? The options show each session's exact time.";
        return null;
    }
}
