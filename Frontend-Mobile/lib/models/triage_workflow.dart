class TriageWorkflow {
  final int workflowId;
  final String status;
  final String approvalStatus;
  final String triageLevel;
  final String uncertaintyState;
  final bool requiresHumanReview;
  final String patientMessage;
  final String patientReportedSymptoms;
  final TriageGuidance? guidance;
  final List<String> redFlags;
  final List<String> urgentFlags;
  final List<String> clinicalReviewFlags;
  final List<String> missingInformation;
  final List<String> decisionBasis;
  final List<TriagePlanStep> plan;
  final DateTime? createdAt;
  final DateTime? updatedAt;

  const TriageWorkflow({
    required this.workflowId,
    required this.status,
    required this.approvalStatus,
    required this.triageLevel,
    required this.uncertaintyState,
    required this.requiresHumanReview,
    required this.patientMessage,
    required this.patientReportedSymptoms,
    required this.guidance,
    required this.redFlags,
    required this.urgentFlags,
    required this.clinicalReviewFlags,
    required this.missingInformation,
    required this.decisionBasis,
    required this.plan,
    this.createdAt,
    this.updatedAt,
  });

  factory TriageWorkflow.fromJson(Map<String, dynamic> json) => TriageWorkflow(
        workflowId: json['workflowId'] as int,
        status: json['status']?.toString() ?? 'Unknown',
        approvalStatus: json['approvalStatus']?.toString() ?? 'Unknown',
        triageLevel:
            json['triageLevel']?.toString() ?? 'InsufficientInformation',
        uncertaintyState:
            json['uncertaintyState']?.toString() ?? 'LimitedInformation',
        requiresHumanReview: json['requiresHumanReview'] == true,
        patientMessage: json['patientMessage']?.toString() ??
            'The system cannot safely assess this situation.',
        patientReportedSymptoms:
            json['patientReportedSymptoms']?.toString() ?? '',
        guidance: json['guidance'] is Map<String, dynamic>
            ? TriageGuidance.fromJson(json['guidance'] as Map<String, dynamic>)
            : null,
        redFlags: List<String>.from(json['redFlags'] ?? const []),
        urgentFlags: List<String>.from(json['urgentFlags'] ?? const []),
        clinicalReviewFlags:
            List<String>.from(json['clinicalReviewFlags'] ?? const []),
        missingInformation:
            List<String>.from(json['missingInformation'] ?? const []),
        decisionBasis: List<String>.from(json['decisionBasis'] ?? const []),
        plan: (json['plan'] as List<dynamic>? ?? const [])
            .map(
                (item) => TriagePlanStep.fromJson(item as Map<String, dynamic>))
            .toList(),
        createdAt: DateTime.tryParse(json['createdAt']?.toString() ?? ''),
        updatedAt: DateTime.tryParse(json['updatedAt']?.toString() ?? ''),
      );
}

class TriageGuidance {
  final String heading;
  final String summary;
  final List<String> actions;
  final List<String> seekHelpIf;
  final List<String> followUpQuestions;
  final List<TriageFollowUpQuestion> followUpItems;
  final String evidenceSource;

  const TriageGuidance(
      {required this.heading,
      required this.summary,
      required this.actions,
      required this.seekHelpIf,
      required this.followUpQuestions,
      required this.followUpItems,
      required this.evidenceSource});

  factory TriageGuidance.fromJson(Map<String, dynamic> json) => TriageGuidance(
        heading: json['heading']?.toString() ?? 'General guidance',
        summary: json['summary']?.toString() ?? '',
        actions: List<String>.from(json['actions'] ?? const []),
        seekHelpIf: List<String>.from(json['seekHelpIf'] ?? const []),
        followUpQuestions:
            List<String>.from(json['followUpQuestions'] ?? const []),
        followUpItems: (json['followUpItems'] as List<dynamic>? ?? const [])
            .map((item) =>
                TriageFollowUpQuestion.fromJson(item as Map<String, dynamic>))
            .toList(),
        evidenceSource: json['evidenceSource']?.toString() ?? '',
      );
}

class TriageFollowUpQuestion {
  final String id;
  final String prompt;
  final String type;
  final bool required;
  final List<String> options;
  final String? unit;
  final double? minimum;
  final double? maximum;

  const TriageFollowUpQuestion({
    required this.id,
    required this.prompt,
    required this.type,
    required this.required,
    required this.options,
    this.unit,
    this.minimum,
    this.maximum,
  });

  factory TriageFollowUpQuestion.fromJson(Map<String, dynamic> json) =>
      TriageFollowUpQuestion(
        id: json['id']?.toString() ?? '',
        prompt: json['prompt']?.toString() ?? '',
        type: json['type']?.toString() ?? 'shortText',
        required: json['required'] == true,
        options: List<String>.from(json['options'] ?? const []),
        unit: json['unit']?.toString(),
        minimum: (json['minimum'] as num?)?.toDouble(),
        maximum: (json['maximum'] as num?)?.toDouble(),
      );
}

class TriagePlanStep {
  final String agent;
  final String status;
  final String purpose;
  const TriagePlanStep(
      {required this.agent, required this.status, required this.purpose});
  factory TriagePlanStep.fromJson(Map<String, dynamic> json) => TriagePlanStep(
        agent: json['agent']?.toString() ?? 'Workflow agent',
        status: json['status']?.toString() ?? 'Unknown',
        purpose: json['purpose']?.toString() ?? '',
      );
}
