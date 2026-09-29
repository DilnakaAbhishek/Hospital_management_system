class MedicalRecordAttachment {
  final int attachmentId;
  final int medicalRecordId;
  final String fileName;
  final String fileType;
  final String fileUrl;
  final int fileSize;
  final DateTime uploadedAt;

  const MedicalRecordAttachment({
    required this.attachmentId,
    required this.medicalRecordId,
    required this.fileName,
    required this.fileType,
    required this.fileUrl,
    required this.fileSize,
    required this.uploadedAt,
  });

  factory MedicalRecordAttachment.fromJson(Map<String, dynamic> json) {
    return MedicalRecordAttachment(
      attachmentId: json['attachmentId'] as int? ?? 0,
      medicalRecordId: json['medicalRecordId'] as int? ?? 0,
      fileName: json['fileName'] as String? ?? 'attachment',
      fileType: json['fileType'] as String? ?? 'application/pdf',
      fileUrl: json['fileUrl'] as String? ?? '',
      fileSize: json['fileSize'] as int? ?? 0,
      uploadedAt: json['uploadedAt'] != null
          ? DateTime.parse(json['uploadedAt'] as String)
          : DateTime.now(),
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'attachmentId': attachmentId,
      'medicalRecordId': medicalRecordId,
      'fileName': fileName,
      'fileType': fileType,
      'fileUrl': fileUrl,
      'fileSize': fileSize,
      'uploadedAt': uploadedAt.toIso8601String(),
    };
  }
}

class MedicalRecord {
  final int medicalRecordId;
  final int patientId;
  final String patientName;
  final String patientEmail;
  final int? doctorId;
  final String? doctorName;
  final String? doctorSpecialization;
  final int? appointmentId;
  final DateTime recordDate;
  final String recordType;
  final String diagnosis;
  final String symptoms;
  final String treatmentPlan;
  final String? prescriptionNotes;
  final String? labNotes;
  final DateTime? followUpDate;
  final String status;
  final DateTime createdAt;
  final DateTime updatedAt;
  final List<MedicalRecordAttachment> attachments;

  const MedicalRecord({
    required this.medicalRecordId,
    required this.patientId,
    required this.patientName,
    required this.patientEmail,
    this.doctorId,
    this.doctorName,
    this.doctorSpecialization,
    this.appointmentId,
    required this.recordDate,
    required this.recordType,
    required this.diagnosis,
    required this.symptoms,
    required this.treatmentPlan,
    this.prescriptionNotes,
    this.labNotes,
    this.followUpDate,
    required this.status,
    required this.createdAt,
    required this.updatedAt,
    required this.attachments,
  });

  factory MedicalRecord.fromJson(Map<String, dynamic> json) {
    final rawAttachments = json['attachments'] as List<dynamic>? ?? [];
    return MedicalRecord(
      medicalRecordId: json['medicalRecordId'] as int? ?? 0,
      patientId: json['patientId'] as int? ?? 0,
      patientName: json['patientName'] as String? ?? '',
      patientEmail: json['patientEmail'] as String? ?? '',
      doctorId: json['doctorId'] as int?,
      doctorName: json['doctorName'] as String?,
      doctorSpecialization: json['doctorSpecialization'] as String?,
      appointmentId: json['appointmentId'] as int?,
      recordDate: json['recordDate'] != null
          ? DateTime.parse(json['recordDate'] as String)
          : DateTime.now(),
      recordType: json['recordType'] as String? ?? 'Consultation',
      diagnosis: json['diagnosis'] as String? ?? '',
      symptoms: json['symptoms'] as String? ?? '',
      treatmentPlan: json['treatmentPlan'] as String? ?? '',
      prescriptionNotes: json['prescriptionNotes'] as String?,
      labNotes: json['labNotes'] as String?,
      followUpDate: json['followUpDate'] != null
          ? DateTime.parse(json['followUpDate'] as String)
          : null,
      status: json['status'] as String? ?? 'Finalized',
      createdAt: json['createdAt'] != null
          ? DateTime.parse(json['createdAt'] as String)
          : DateTime.now(),
      updatedAt: json['updatedAt'] != null
          ? DateTime.parse(json['updatedAt'] as String)
          : DateTime.now(),
      attachments: rawAttachments
          .map((a) => MedicalRecordAttachment.fromJson(a as Map<String, dynamic>))
          .toList(),
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'medicalRecordId': medicalRecordId,
      'patientId': patientId,
      'patientName': patientName,
      'patientEmail': patientEmail,
      'doctorId': doctorId,
      'doctorName': doctorName,
      'doctorSpecialization': doctorSpecialization,
      'appointmentId': appointmentId,
      'recordDate': recordDate.toIso8601String(),
      'recordType': recordType,
      'diagnosis': diagnosis,
      'symptoms': symptoms,
      'treatmentPlan': treatmentPlan,
      'prescriptionNotes': prescriptionNotes,
      'labNotes': labNotes,
      'followUpDate': followUpDate?.toIso8601String(),
      'status': status,
      'createdAt': createdAt.toIso8601String(),
      'updatedAt': updatedAt.toIso8601String(),
      'attachments': attachments.map((a) => a.toJson()).toList(),
    };
  }
}

class MedicalRecordSummary {
  final int totalRecords;
  final int consultationCount;
  final int labReportCount;
  final int dischargeSummaryCount;
  final int prescriptionCount;
  final int generalNoteCount;
  final int draftCount;
  final int finalizedCount;
  final int addedThisMonth;

  const MedicalRecordSummary({
    this.totalRecords = 0,
    this.consultationCount = 0,
    this.labReportCount = 0,
    this.dischargeSummaryCount = 0,
    this.prescriptionCount = 0,
    this.generalNoteCount = 0,
    this.draftCount = 0,
    this.finalizedCount = 0,
    this.addedThisMonth = 0,
  });

  factory MedicalRecordSummary.fromJson(Map<String, dynamic> json) {
    return MedicalRecordSummary(
      totalRecords: json['totalRecords'] as int? ?? 0,
      consultationCount: json['consultationCount'] as int? ?? 0,
      labReportCount: json['labReportCount'] as int? ?? 0,
      dischargeSummaryCount: json['dischargeSummaryCount'] as int? ?? 0,
      prescriptionCount: json['prescriptionCount'] as int? ?? 0,
      generalNoteCount: json['generalNoteCount'] as int? ?? 0,
      draftCount: json['draftCount'] as int? ?? 0,
      finalizedCount: json['finalizedCount'] as int? ?? 0,
      addedThisMonth: json['addedThisMonth'] as int? ?? 0,
    );
  }
}
