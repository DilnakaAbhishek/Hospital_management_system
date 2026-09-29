enum TriageRiskLevel { critical, urgent, routine }

class TriageResult {
  final int patientId;
  final String symptomsText;
  final TriageRiskLevel riskLevel;
  final int urgencyScore;
  final String recommendedDepartment;
  final String aiSummary;
  final List<String> recommendedActions;
  final DateTime assessedAt;

  TriageResult({
    required this.patientId,
    required this.symptomsText,
    required this.riskLevel,
    required this.urgencyScore,
    required this.recommendedDepartment,
    required this.aiSummary,
    required this.recommendedActions,
    DateTime? assessedAt,
  }) : assessedAt = assessedAt ?? DateTime.now();

  String get riskLabel {
    switch (riskLevel) {
      case TriageRiskLevel.critical:
        return 'CRITICAL (Red)';
      case TriageRiskLevel.urgent:
        return 'URGENT (Yellow)';
      case TriageRiskLevel.routine:
        return 'ROUTINE (Green)';
    }
  }

  factory TriageResult.fromJson(Map<String, dynamic> json) {
    TriageRiskLevel parseLevel(String level) {
      switch (level.toLowerCase()) {
        case 'critical':
        case 'red':
          return TriageRiskLevel.critical;
        case 'urgent':
        case 'yellow':
          return TriageRiskLevel.urgent;
        default:
          return TriageRiskLevel.routine;
      }
    }

    return TriageResult(
      patientId: json['patientId'] ?? 0,
      symptomsText: json['symptomsText'] ?? '',
      riskLevel: parseLevel(json['riskLevel'] ?? 'routine'),
      urgencyScore: json['urgencyScore'] ?? 1,
      recommendedDepartment: json['recommendedDepartment'] ?? 'General OPD',
      aiSummary: json['aiSummary'] ?? '',
      recommendedActions: List<String>.from(json['recommendedActions'] ?? []),
      assessedAt: DateTime.tryParse(json['assessedAt'] ?? '') ?? DateTime.now(),
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'patientId': patientId,
      'symptomsText': symptomsText,
      'riskLevel': riskLevel.name,
      'urgencyScore': urgencyScore,
      'recommendedDepartment': recommendedDepartment,
      'aiSummary': aiSummary,
      'recommendedActions': recommendedActions,
      'assessedAt': assessedAt.toIso8601String(),
    };
  }
}
