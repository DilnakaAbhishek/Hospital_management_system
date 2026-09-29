class DoctorSearchResult {
  final int doctorId;
  final String fullName;
  final String specialization;

  const DoctorSearchResult({
    required this.doctorId,
    required this.fullName,
    required this.specialization,
  });

  factory DoctorSearchResult.fromJson(Map<String, dynamic> json) =>
      DoctorSearchResult(
        doctorId: json['doctorId'] as int,
        fullName: json['fullName']?.toString() ?? '',
        specialization: json['specialization']?.toString() ?? '',
      );
}

class DoctorPublicProfile {
  final int doctorId;
  final String fullName;
  final String email;
  final String slmcLicenseNumber;
  final String specialization;

  const DoctorPublicProfile({
    required this.doctorId,
    required this.fullName,
    required this.email,
    required this.slmcLicenseNumber,
    required this.specialization,
  });

  factory DoctorPublicProfile.fromJson(Map<String, dynamic> json) =>
      DoctorPublicProfile(
        doctorId: json['doctorId'] as int,
        fullName: json['fullName']?.toString() ?? '',
        email: json['email']?.toString() ?? '',
        slmcLicenseNumber: json['slmcLicenseNumber']?.toString() ?? '',
        specialization: json['specialization']?.toString() ?? '',
      );
}
