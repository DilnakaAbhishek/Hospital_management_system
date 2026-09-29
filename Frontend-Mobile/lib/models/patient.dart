class Patient {
  final int patientId;
  final String firstName;
  final String lastName;
  final DateTime dateOfBirth;
  final String gender;
  final String nic;
  final String phoneNumber;
  final String? email;
  final String? address;
  final String bloodGroup;
  final String? emergencyContactName;
  final String? emergencyContactPhone;
  final String? profileImageUrl;

  Patient({
    required this.patientId,
    required this.firstName,
    required this.lastName,
    required this.dateOfBirth,
    required this.gender,
    required this.nic,
    required this.phoneNumber,
    this.email,
    this.address,
    required this.bloodGroup,
    this.emergencyContactName,
    this.emergencyContactPhone,
    this.profileImageUrl,
  });

  String get fullName => '$firstName $lastName';

  factory Patient.fromJson(Map<String, dynamic> json) {
    return Patient(
      patientId: json['patientId'] ?? 0,
      firstName: json['firstName'] ?? '',
      lastName: json['lastName'] ?? '',
      dateOfBirth: DateTime.tryParse(json['dateOfBirth'] ?? '') ?? DateTime.now(),
      gender: json['gender'] ?? 'Other',
      nic: json['nic'] ?? '',
      phoneNumber: json['phoneNumber'] ?? '',
      email: json['email'],
      address: json['address'],
      bloodGroup: json['bloodGroup'] ?? 'A+',
      emergencyContactName: json['emergencyContactName'],
      emergencyContactPhone: json['emergencyContactPhone'],
      profileImageUrl: json['profileImageUrl'],
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'patientId': patientId,
      'firstName': firstName,
      'lastName': lastName,
      'dateOfBirth': dateOfBirth.toIso8601String(),
      'gender': gender,
      'nic': nic,
      'phoneNumber': phoneNumber,
      'email': email,
      'address': address,
      'bloodGroup': bloodGroup,
      'emergencyContactName': emergencyContactName,
      'emergencyContactPhone': emergencyContactPhone,
      'profileImageUrl': profileImageUrl,
    };
  }
}
