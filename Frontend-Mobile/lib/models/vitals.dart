class Vitals {
  final int? id;
  final int patientId;
  final int systolicBp;
  final int diastolicBp;
  final int heartRateBpm;
  final double temperatureCelcius;
  final int oxygenSaturationSpo2;
  final DateTime recordedAt;

  Vitals({
    this.id,
    required this.patientId,
    required this.systolicBp,
    required this.diastolicBp,
    required this.heartRateBpm,
    required this.temperatureCelcius,
    required this.oxygenSaturationSpo2,
    DateTime? recordedAt,
  }) : recordedAt = recordedAt ?? DateTime.now();

  String get bpStatus {
    if (systolicBp >= 140 || diastolicBp >= 90) return 'High Blood Pressure';
    if (systolicBp < 90 || diastolicBp < 60) return 'Low Blood Pressure';
    return 'Normal Blood Pressure';
  }

  String get heartRateStatus {
    if (heartRateBpm > 100) return 'Tachycardia (High)';
    if (heartRateBpm < 60) return 'Bradycardia (Low)';
    return 'Normal Heart Rate';
  }

  factory Vitals.fromJson(Map<String, dynamic> json) {
    return Vitals(
      id: json['id'],
      patientId: json['patientId'] ?? 0,
      systolicBp: json['systolicBp'] ?? 120,
      diastolicBp: json['diastolicBp'] ?? 80,
      heartRateBpm: json['heartRateBpm'] ?? 72,
      temperatureCelcius: (json['temperatureCelcius'] as num?)?.toDouble() ?? 36.6,
      oxygenSaturationSpo2: json['oxygenSaturationSpo2'] ?? 98,
      recordedAt: DateTime.tryParse(json['recordedAt'] ?? '') ?? DateTime.now(),
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'patientId': patientId,
      'systolicBp': systolicBp,
      'diastolicBp': diastolicBp,
      'heartRateBpm': heartRateBpm,
      'temperatureCelcius': temperatureCelcius,
      'oxygenSaturationSpo2': oxygenSaturationSpo2,
      'recordedAt': recordedAt.toIso8601String(),
    };
  }
}
