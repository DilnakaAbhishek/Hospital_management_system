using HospitalManagementSystem.Api.AgenticAI.PatientCare.AppointmentProposal;
using HospitalManagementSystem.Api.DTOs;
using static HospitalManagementSystem.Api.AgenticAI.HospitalAssistant.AssistantPreferences;

using HospitalManagementSystem.Api.AgenticAI.HospitalAssistant;

namespace HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator;

public sealed partial class PlanningCoordinatorAgent
{
    private async Task<bool> TryReadAsync(PatientDto patient, AssistantState state, string text, CancellationToken token)
    {
        // Do not intercept active conversational answers or clinical choices
        if (state.Awaiting is "clinical-review-choice" or "clinical-answer" or "cancellation-reason") return false;

        // Mutation verbs remain in the existing proposal/approval route.
        if (Has(text, @"\b(book|booking|reserve|schedule|reschedule|cancel|create|confirm|proceed)\b")) return false;
        if (Has(text, @"^(thanks|thank you|thanks a lot|thank you very much|appreciate it)( for (the help|your help|that))?[.! ]*$"))
        {
            Reply(state, "You're welcome.", "COMPLETED");
            return true;
        }
        if (Has(text, @"^(bye|goodbye|bye bye|see you|see you later|thanks[, ]+bye|thank you[, ]+bye)[.! ]*$"))
        {
            Reply(state, "Goodbye. You can return whenever you need help.",
                state.PendingAction != null ? "WAITING_FOR_HUMAN_APPROVAL" : state.Awaiting != null ? "GATHERING_INFORMATION" : "COMPLETED");
            return true;
        }
        if (state.Awaiting == null && state.PendingAction == null && !state.WantsAppointment &&
            Has(text, @"^(ok|okay|fine|alright|all right|got it|understood|sounds good)[.! ]*$"))
        {
            Reply(state, "Okay. Let me know if you need anything else.", "COMPLETED");
            return true;
        }
        if (Has(text, @"^(nothing|never mind|nevermind|no thanks|that's all|that is all)[.! ]*$"))
        {
            Reply(state, "No problem. Let me know if you need anything else.", "COMPLETED");
            return true;
        }
        if (Has(text, @"^(yes|okay|ok|go ahead|do it|no need|not needed|nothing else|no thanks|that's all|that is all)[.! ]*$")) return false;
        if (Has(text, @"^(hello|hi|help|what can you do)[?!. ]*$"))
        {
            Reply(state, "I can show your appointments, find approved doctors, check availability, and prepare appointment requests for your confirmation. You can keep using these information services while an assessment is pending.", "COMPLETED");
            return true;
        }
        var allSpecialties = Has(text, @"\b(all|every|show|list)\b.*\b(specialt(y|ies)|specialit(y|ies))\b|\b(specialt(y|ies)|specialit(y|ies))\b.*\b(all|every|show|list)\b");
        var allDoctors = Has(text, @"\b(all|every|show|list|who are)\b.*\b(doctors?|specialists?)\b|\b(doctors?|specialists?)\b.*\b(working|work)\b.*\b(here|hospital)\b");
        if (allSpecialties || allDoctors)
        {
            var directory = await tools.FindDoctorsAsync("");
            state.ActiveTask = allSpecialties ? "specialty-list" : "doctor-list";
            state.ReadSearchMode = "doctors";
            state.SearchQuery = null;
            state.Slots = [];
            state.AvailabilityChecked = false;
            if (allSpecialties)
            {
                state.Doctors = [];
                var specialties = directory.Select(d => d.Specialty).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToArray();
                Reply(state, specialties.Length == 0 ? "No approved specialties are currently available."
                    : "Available specialties: " + string.Join(", ", specialties) + ".", "COMPLETED", ["The approved doctor directory was checked."]);
            }
            else
            {
                state.Doctors = directory;
                Reply(state, directory.Count == 0 ? "No approved doctors are currently listed."
                    : $"Here are {directory.Count} approved doctors working in this hospital.", "COMPLETED", ["The approved doctor directory was checked."]);
            }
            return true;
        }
        // Doctor directory / specialty searches take precedence over extensions unless
        // the user is explicitly requesting their medical records/reports/prescriptions/diagnoses/symptoms.
        var medicalRecordAgent = registry.AdditionalAgents.FirstOrDefault(agent => agent.Capability.Enabled && agent.Capability.Id == "medical-reports");
        var isExplicitMedicalRecordQuery = (medicalRecordAgent != null && medicalRecordAgent.CanHandle(text))
            || (Has(text, @"\b(my\s+records?|my\s+reports?|medical\s+records?|medical\s+reports?|lab\s+reports?|lab\s+results?|prescriptions?|prescribed|medications?|medicines?|diagnos(is|es)|discharge\s+summary|doctor\s+notes?)\b")
                && !Has(text, @"\b(general\s+medicine\s+doctor|doctor\s+in\s+general\s+medicine|general\s+medicine)\b"));
        var isDoctorOrSpecialtySearch = Has(text, @"\b(doctors?|specialists?|general medicine|cardiolog(y|ists?)|ophthalmolog(y|ists?)|dermatolog(y|ists?)|pediatric(s|ians?)|neurolog(y|ists?))\b");

        if (!isDoctorOrSpecialtySearch || isExplicitMedicalRecordQuery)
        {
            var extension = registry.AdditionalAgents.FirstOrDefault(agent => agent.Capability.Enabled && agent.CanHandle(text));
            if (extension != null)
            {
                if (extension.Capability.Id == "medical-reports")
                    await DispatchAsync(state, PlanningWorkflowType.MedicalRecords, "Medical Records", token);
                Reply(state, await extension.ReadAsync(text, patient, token), "COMPLETED");
                return true;
            }
        }
        if (Has(text, @"\bappointments?\b") && Has(text, @"\b(my|next|existing|have|history|all|cancelled|canceled|show|list|check|view)\b"))
        {
            var all = await MyAppointmentsAsync(patient);
            var next = Has(text, @"\bnext\b");
            var cancelled = Has(text, @"\b(cancelled|canceled)\b");
            var history = Has(text, @"\bhistory\b");
            var includeAll = Has(text, @"\ball\b");
            var records = all.AsEnumerable();
            if (next || (!cancelled && !history && !includeAll))
                records = records.Where(a => a.Status == "Confirmed" && a.StartAt > DateTime.UtcNow);
            else if (cancelled) records = records.Where(a => a.Status == "Cancelled");
            else if (history) records = records.Where(a => a.Status is "Cancelled" or "Completed" || a.EndAt <= DateTime.UtcNow);
            var filters = new AssistantState();
            // History is not constrained by the future-only booking date parser.
            if (!history && !cancelled && !includeAll)
            {
                var error = ApplyDates(text, filters, Today());
                if (error != null) { Reply(state, error, "GATHERING_INFORMATION"); return true; }
                records = FilterAppointments(records, filters);
            }
            state.Appointments = records.OrderBy(a => a.StartAt).Take(next ? 1 : 30).ToArray();
            var first = state.Appointments.FirstOrDefault();
            var reply = first == null ? "No appointments matched that request."
                : next ? $"Your next appointment is with {first.DoctorName} on {Local(first.StartAt):d MMMM yyyy} at {Local(first.StartAt):h:mm tt}. This is the session start time."
                : $"Here are {state.Appointments.Count} matching appointments{(cancelled ? " (cancelled)" : history ? " from your history" : "")}.";
            Reply(state, reply, "COMPLETED", ["Your appointment records were retrieved from the hospital."]);
            return true;
        }

        var doctorRequest = Has(text, @"\b(doctors?|specialists?|cardiologists?|cardiology|general medicine|ophthalmologists?|dermatologists?|neurologists?)\b");
        var followup = state.SearchQuery != null && Has(text, @"\b(which one|which of them|available|availability|tomorrow|morning|afternoon|evening)\b");
        var preferenceReply = state.ActiveTask == "availability" && state.ReadSearchMode != null && state.PendingAction == null;
        if (!doctorRequest && !followup && !preferenceReply)
        {
            var fallbackExtension = registry.AdditionalAgents.FirstOrDefault(agent => agent.Capability.Enabled && agent.CanHandle(text));
            if (fallbackExtension != null)
            {
                Reply(state, await fallbackExtension.ReadAsync(text, patient, token), "COMPLETED");
                return true;
            }
            return false;
        }
        // A date-only reply to an explicit booking stays in the booking flow.
        if (!doctorRequest && state.WantsAppointment && state.ReadSearchMode == null) return false;
        var doctors = await tools.FindDoctorsAsync("");
        if (!doctorRequest && !followup && Query(text, doctors) == null) return false;
        var continuesAvailability = followup || preferenceReply;
        var priorSearchQuery = state.SearchQuery;
        if (!continuesAvailability)
        {
            // A new directory/availability request starts fresh; only a recognised
            // follow-up can inherit the previous doctor, date, and daypart.
            state.SearchQuery = null;
            state.PreferredDate = null;
            state.ThroughDate = null;
            state.Period = null;
            state.Slots = [];
            state.AvailabilityChecked = false;
        }
        // A blocked booking can still ask for an independent availability read;
        // retain its explicit doctor/specialty only when the new wording does not
        // contain another recognised query.
        var query = Query(text, doctors) ?? (continuesAvailability || state.ActiveTask == "booking" ? priorSearchQuery : null);
        var availability = Has(text, @"\b(available|availability|tomorrow|today|monday|tuesday|wednesday|thursday|friday|saturday|sunday|morning|afternoon|evening)\b") ||
            Has(text, @"\b\d{4}-\d{2}-\d{2}\b") || state.ReadSearchMode == "availability";
        state.ReadSearchMode = availability ? "availability" : "doctors";
        state.ActiveTask = availability ? "availability" : "doctor-information";
        if (query == null)
        {
            if (Has(text, @"\b(suggest|recommend|who\s+should|which\s+doctor|help\s+me\s+choose|suitable\s+doctor)\b"))
            {
                var gmDoctors = await tools.FindDoctorsAsync("General Medicine");
                if (gmDoctors.Count > 0)
                {
                    state.SearchQuery = "General Medicine";
                    state.Doctors = gmDoctors;
                    var docNames = string.Join(", ", gmDoctors.Select(d => d.Name.StartsWith("Dr") ? d.Name : $"Dr. {d.Name}"));
                    Reply(state, $"Based on your symptoms, a consultation in General Medicine is recommended. Approved doctors: {docNames}. You can ask about their availability or request a booking.",
                        "COMPLETED", ["The approved doctor directory was checked."]);
                    return true;
                }
            }

            Reply(state, "Which doctor or specialty would you like me to look up?", "GATHERING_INFORMATION", ["The approved doctor directory was checked."]);
            return true;
        }
        state.SearchQuery = query;
        state.Doctors = await tools.FindDoctorsAsync(query);
        if (!availability)
        {
            Reply(state, state.Doctors.Count == 0 ? "No approved doctors matched that request."
                : $"These approved doctors match {query}. You can ask about their availability or request a booking.",
                "COMPLETED", ["The approved doctor directory was checked."]);
            return true;
        }
        // Continue only the active availability task; a new request gets an
        // isolated preference object and therefore cannot inherit stale filters.
        var preferences = continuesAvailability ? state : new AssistantState();
        var dateError = ApplyDates(text, preferences, Today());
        if (dateError != null) { Reply(state, dateError, "GATHERING_INFORMATION"); return true; }
        var slots = new List<AgentSlot>();
        foreach (var doctor in state.Doctors.Take(5))
        {
            token.ThrowIfCancellationRequested();
            if (preferences.ThroughDate.HasValue)
                for (var date = preferences.PreferredDate!.Value; date <= preferences.ThroughDate.Value; date = date.AddDays(1))
                    slots.AddRange(await tools.FindSlotsAsync(doctor, date));
            else slots.AddRange(await tools.FindSlotsAsync(doctor, preferences.PreferredDate));
        }
        state.Slots = slots.Where(s => preferences.Period == null || preferences.Period switch {
            "morning" => s.StartAt.Hour < 12, "afternoon" => s.StartAt.Hour is >= 12 and < 17,
            "evening" => s.StartAt.Hour >= 17, _ => false
        }).DistinctBy(s => s.DoctorTimeSlotId).OrderBy(s => s.StartAt).Take(5).ToArray();
        state.PreferredDate = preferences.PreferredDate;
        state.ThroughDate = preferences.ThroughDate;
        state.Period = preferences.Period;
        state.AvailabilityChecked = true;
        Reply(state, state.Slots.Count == 0
            ? $"{(state.Doctors.Count == 0 ? "No approved doctors matched that request." : $"{string.Join(", ", state.Doctors.Select(d => d.Name))} works in {query}, but no matching sessions are available{AvailabilityConstraint(state)}.")}" + " Would you like me to check another date?"
            : "Here are available sessions. Nothing is reserved. Ask me to book an option if you want a confirmation proposal.",
            "COMPLETED", state.Doctors.Count == 0 ? ["The approved doctor directory was checked."]
                : ["The approved doctor directory was checked.", "Doctor availability was checked using hospital sessions."]);
        return true;
    }

    private static string AvailabilityConstraint(AssistantState state)
    {
        var date = state.PreferredDate.HasValue ? $" on {state.PreferredDate:dddd, d MMMM}" : "";
        return state.Period == null ? date : date + $" in the {state.Period}";
    }
}
