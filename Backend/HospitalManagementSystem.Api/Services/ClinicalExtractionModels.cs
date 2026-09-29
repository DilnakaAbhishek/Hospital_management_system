namespace HospitalManagementSystem.Api.Services;


public sealed record ClinicalExtractionResult(
    IReadOnlyList<string> Symptoms,
    IReadOnlyList<string> MissingInformation,
    PatientGuidance? Guidance,
    string Status,
    string? ErrorCode = null,
    IReadOnlyList<string>? Concepts = null,
    ClinicalFactSet? Facts = null,
    IReadOnlyList<HospitalManagementSystem.Api.AgenticAI.SafeTriage.SafeTriageRequirement>? Requirements = null);

public sealed class ClinicalFactSet
{
    public string? PrimaryConcept { get; init; }
    public bool? CurrentlyActive { get; init; }
    public decimal? DurationMinutes { get; init; }
    public decimal? DurationDays { get; init; }
    public decimal? SeverityScore { get; init; }
    public decimal? TemperatureCelsius { get; init; }
    public string? Progression { get; init; }
    public IReadOnlyList<string> WarningSigns { get; init; } = [];
    public IReadOnlyList<string> NegatedWarningSigns { get; init; } = [];
    public IReadOnlyList<string> RiskContexts { get; init; } = [];
    public IReadOnlyList<ClinicalFactEvidence> Evidence { get; init; } = [];
}

public sealed record ClinicalFactEvidence(string Field, string Quote, string? Value = null);
public sealed record PatientGuidance(string Summary, IReadOnlyList<string> GeneralActions, IReadOnlyList<string> SafetyNetting, IReadOnlyList<string> FollowUpQuestions);

public static class ClinicalHeuristicExtractor
{
    public static ClinicalExtractionResult Extract(string text, bool includeFollowUpQuestions = true)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 2)
        {
            return new ClinicalExtractionResult([], ["Input symptoms were incomplete or unreadable."], null, "FailedSafely", "ExtractionUnavailable");
        }

        var lower = text.ToLowerInvariant();
        var symptoms = new List<string>();
        var concepts = new List<string>();

        if (lower.Contains("headache") || lower.Contains("migraine") || lower.Contains("head pain"))
        {
            symptoms.Add("Head pain / headache");
            concepts.Add("headache");
        }
        if (lower.Contains("fever") || lower.Contains("temperature") || lower.Contains("chills") || lower.Contains("feverish"))
        {
            symptoms.Add("Elevated temperature / fever");
            concepts.Add("fever");
        }
        if (lower.Contains("cough") || lower.Contains("coughing"))
        {
            symptoms.Add("Coughing");
            concepts.Add("cough");
        }
        if (lower.Contains("throat") || lower.Contains("sore throat"))
        {
            symptoms.Add("Sore or painful throat");
            concepts.Add("sore throat");
        }
        if (lower.Contains("stomach") || lower.Contains("abdomen") || lower.Contains("abdominal") || lower.Contains("belly") || lower.Contains("gut"))
        {
            symptoms.Add("Abdominal / stomach discomfort");
            concepts.Add("stomach pain");
        }
        if (lower.Contains("nausea") || lower.Contains("nauseous") || lower.Contains("vomit") || lower.Contains("throwing up"))
        {
            symptoms.Add("Nausea or vomiting");
            concepts.Add("nausea");
        }
        if (lower.Contains("diarrhea") || lower.Contains("diarrhoea") || lower.Contains("loose stool"))
        {
            symptoms.Add("Diarrhea / loose stools");
            concepts.Add("diarrhea");
        }
        if (lower.Contains("back pain") || lower.Contains("backache") || lower.Contains("spine"))
        {
            symptoms.Add("Back discomfort / pain");
            concepts.Add("back pain");
        }
        if (lower.Contains("joint") || lower.Contains("knee") || lower.Contains("elbow") || lower.Contains("shoulder") || lower.Contains("arthritis"))
        {
            symptoms.Add("Joint pain or stiffness");
            concepts.Add("joint pain");
        }
        if (lower.Contains("rash") || lower.Contains("itching") || lower.Contains("skin") || lower.Contains("hives") || lower.Contains("spots"))
        {
            symptoms.Add("Skin rash or irritation");
            concepts.Add("skin rash");
        }
        if (lower.Contains("earache") || lower.Contains("ear pain") || lower.Contains("ear infection") || System.Text.RegularExpressions.Regex.IsMatch(lower, @"\bear(s)?\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            symptoms.Add("Ear pain / discomfort");
            concepts.Add("earache");
        }
        if (lower.Contains("dizzy") || lower.Contains("dizziness") || lower.Contains("lightheaded"))
        {
            symptoms.Add("Dizziness or lightheadedness");
            concepts.Add("dizziness");
        }
        if (lower.Contains("fatigue") || lower.Contains("tired") || lower.Contains("exhausted") || lower.Contains("weakness"))
        {
            symptoms.Add("Fatigue / tiredness");
            concepts.Add("fatigue");
        }
        if (lower.Contains("eye") || lower.Contains("vision") || lower.Contains("blurry") || lower.Contains("blurred"))
        {
            symptoms.Add("Eye or vision symptom");
            concepts.Add("eye symptom");
        }
        if (lower.Contains("nosebleed") || lower.Contains("nose bleed") || lower.Contains("bleeding nose") ||
            lower.Contains("blood from nostril") || lower.Contains("blood from my nostril"))
        {
            symptoms.Add("Nasal bleeding");
            concepts.Add("nosebleed");
        }
        if (lower.Contains("runny nose") || lower.Contains("blocked nose") || lower.Contains("congestion") || lower.Contains("cold"))
        {
            symptoms.Add("Nasal congestion / cold symptoms");
            concepts.Add("nasal congestion");
        }

        if (symptoms.Count == 0)
        {
            symptoms.Add(text.Trim());
            concepts.Add("general symptom complaint");
        }

        var mainConcept = concepts.FirstOrDefault() ?? "general symptoms";
        var summary = $"Reported {mainConcept} and associated patient symptoms.";
        var generalActions = new List<string>
        {
            "Rest, ensure adequate hydration, and monitor symptom changes.",
            "Consult a pharmacist or healthcare provider for suitable non-prescription relief options."
        };
        var safetyNetting = new List<string>
        {
            "Seek urgent or emergency medical evaluation if symptoms rapidly deteriorate, or if severe pain or breathing trouble occurs.",
            "Contact your healthcare provider if symptoms persist beyond 5-7 days or interfere with daily activities."
        };
        var followUpQuestions = new List<string>();

        var guidance = new PatientGuidance(summary, generalActions, safetyNetting, followUpQuestions);
        return new ClinicalExtractionResult(symptoms, [], guidance, "Completed", Concepts: concepts,
            Facts: ExtractFacts(text, concepts.FirstOrDefault()));
    }

    private static ClinicalFactSet ExtractFacts(string text, string? primaryConcept)
    {
        var lower = text.ToLowerInvariant();
        var evidence = new List<ClinicalFactEvidence>();
        decimal? durationMinutes = null;
        decimal? durationDays = null;
        var duration = System.Text.RegularExpressions.Regex.Match(lower,
            @"\b(?<value>\d+(?:\.\d+)?)\s*(?<unit>minutes?|mins?|hours?|hrs?|days?|weeks?)\b");
        if (duration.Success && decimal.TryParse(duration.Groups["value"].Value,
                System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            var unit = duration.Groups["unit"].Value;
            if (unit.StartsWith("min")) durationMinutes = value;
            else if (unit.StartsWith("hour") || unit.StartsWith("hr")) durationMinutes = value * 60;
            else if (unit.StartsWith("day")) durationDays = value;
            else if (unit.StartsWith("week")) durationDays = value * 7;
            evidence.Add(new ClinicalFactEvidence(durationMinutes.HasValue ? "durationMinutes" : "durationDays",
                duration.Value, value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        bool? active = primaryConcept == "nosebleed"
            ? !System.Text.RegularExpressions.Regex.IsMatch(lower, @"\b(stopped|has stopped|no longer bleeding)\b") &&
              System.Text.RegularExpressions.Regex.IsMatch(lower, @"\b(bleeding|nosebleed|nose bleed|blood (?:is )?(?:coming )?from (?:my )?(?:nose|nostril))\b")
            : null;
        if (active.HasValue)
        {
            var activeQuote = System.Text.RegularExpressions.Regex.Match(lower, @"\b(stopped|has stopped|no longer bleeding|bleeding|nosebleed|nose bleed|blood (?:is )?(?:coming )?from (?:my )?(?:nose|nostril))\b");
            if (activeQuote.Success) evidence.Add(new ClinicalFactEvidence("currentlyActive", activeQuote.Value, active.Value.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(primaryConcept))
        {
            var conceptQuote = FindConceptEvidence(lower, primaryConcept);
            if (conceptQuote is not null) evidence.Add(new ClinicalFactEvidence("primaryConcept", conceptQuote, primaryConcept));
        }

        var warningSigns = ExtractNonNegatedPhrases(lower,
            ["difficulty breathing", "severe chest pain", "severe bleeding", "weak", "dizzy", "fainting", "seizure", "persistent vomiting"]);
        foreach (var warning in warningSigns) evidence.Add(new ClinicalFactEvidence("warningSigns", warning, warning));
        var negatedWarningSigns = ExtractNegatedPhrases(lower,
            ["difficulty breathing", "severe chest pain", "severe bleeding", "weak", "dizzy", "fainting", "seizure", "persistent vomiting"]);
        foreach (var warning in negatedWarningSigns) evidence.Add(new ClinicalFactEvidence("negatedWarningSigns", warning, warning));
        var riskContexts = ExtractNonNegatedPhrases(lower,
            ["pregnant", "postpartum", "cancer treatment", "chemotherapy", "weakened immune system", "organ transplant", "recent surgery", "blood-thinning medicine", "bleeding or clotting condition"]);
        foreach (var risk in riskContexts) evidence.Add(new ClinicalFactEvidence("riskContexts", risk, risk));

        decimal? severity = null;
        var severityMatch = System.Text.RegularExpressions.Regex.Match(lower, @"\b(?<value>[0-9]|10)\s*(?:/\s*10|out of 10)\b");
        if (severityMatch.Success && decimal.TryParse(severityMatch.Groups["value"].Value, out var severityValue))
        {
            severity = severityValue;
            evidence.Add(new ClinicalFactEvidence("severityScore", severityMatch.Value,
                severityValue.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        decimal? temperature = null;
        var temperatureMatch = System.Text.RegularExpressions.Regex.Match(lower, @"\b(?<value>\d{2}(?:\.\d+)?)\s*(?:°\s*)?c(?:elsius)?\b");
        if (temperatureMatch.Success && decimal.TryParse(temperatureMatch.Groups["value"].Value,
                System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var temperatureValue))
        {
            temperature = temperatureValue;
            evidence.Add(new ClinicalFactEvidence("temperatureCelsius", temperatureMatch.Value,
                temperatureValue.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        var progression = new[] { "suddenly much worse", "getting worse", "worsening", "unchanged", "improving", "getting better" }
            .FirstOrDefault(item => lower.Contains(item, StringComparison.OrdinalIgnoreCase));
        if (progression is not null) evidence.Add(new ClinicalFactEvidence("progression", progression, progression));

        return new ClinicalFactSet
        {
            PrimaryConcept = primaryConcept,
            CurrentlyActive = active,
            DurationMinutes = durationMinutes,
            DurationDays = durationDays,
            SeverityScore = severity,
            TemperatureCelsius = temperature,
            Progression = progression,
            WarningSigns = warningSigns,
            NegatedWarningSigns = negatedWarningSigns,
            RiskContexts = riskContexts,
            Evidence = evidence
        };
    }

    private static string? FindConceptEvidence(string text, string concept)
    {
        var candidates = concept switch
        {
            "nosebleed" => new[] { "nosebleed", "nose bleed", "bleeding nose", "blood from nostril", "blood from my nostril" },
            "nasal congestion" => new[] { "runny nose", "blocked nose", "congestion", "cold" },
            "eye symptom" => new[] { "eye", "vision", "blurry", "blurred" },
            "dizziness" => new[] { "dizzy", "dizziness", "lightheaded" },
            _ => new[] { concept }
        };
        return candidates.FirstOrDefault(candidate => text.Contains(candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> ExtractNonNegatedPhrases(string text, IReadOnlyList<string> phrases) =>
        phrases.Where(phrase => IsPresentAndNotNegated(text, phrase)).ToList();

    private static List<string> ExtractNegatedPhrases(string text, IReadOnlyList<string> phrases) =>
        phrases.Where(phrase => text.Contains(phrase, StringComparison.OrdinalIgnoreCase) && !IsPresentAndNotNegated(text, phrase)).ToList();

    private static bool IsPresentAndNotNegated(string text, string phrase)
    {
        var index = text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return false;
        var prefix = text[..index];
        var nearby = prefix[Math.Max(0, prefix.Length - 45)..];
        return !System.Text.RegularExpressions.Regex.IsMatch(nearby,
            @"\b(no|not|without|deny|denies|never|don't|do not)\b(?:\W+\w+){0,4}\W*$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}


