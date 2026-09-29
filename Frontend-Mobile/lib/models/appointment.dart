class Appointment {
  final int appointmentId;
  final int doctorTimeSlotId;
  final int? doctorId;
  final int? patientId;
  final int appointmentNumber;
  final String patientName;
  final String patientPhone;
  final String patientEmail;
  final String doctorName;
  final String specialty;
  final int? roomId;
  final String roomNumber;
  final String roomName;
  final String floor;
  final DateTime startAt;
  final DateTime endAt;
  final double consultationFee;
  final String appointmentType;
  final String reason;
  final String status;
  final String cancellationReason;
  final String notes;
  final DateTime createdAt;
  final DateTime updatedAt;

  Appointment({
    required this.appointmentId,
    required this.doctorTimeSlotId,
    this.doctorId,
    this.patientId,
    required this.appointmentNumber,
    required this.patientName,
    required this.patientPhone,
    required this.patientEmail,
    required this.doctorName,
    required this.specialty,
    this.roomId,
    required this.roomNumber,
    required this.roomName,
    required this.floor,
    required this.startAt,
    required this.endAt,
    required this.consultationFee,
    required this.appointmentType,
    required this.reason,
    required this.status,
    required this.cancellationReason,
    required this.notes,
    required this.createdAt,
    required this.updatedAt,
  });

  factory Appointment.fromJson(Map<String, dynamic> json) {
    return Appointment(
      appointmentId: json['appointmentId'] ?? 0,
      doctorTimeSlotId: json['doctorTimeSlotId'] ?? 0,
      doctorId: json['doctorId'],
      patientId: json['patientId'],
      appointmentNumber: json['appointmentNumber'] ?? 0,
      patientName: json['patientName'] ?? '',
      patientPhone: json['patientPhone'] ?? '',
      patientEmail: json['patientEmail'] ?? '',
      doctorName: json['doctorName'] ?? '',
      specialty: json['specialty'] ?? '',
      roomId: json['roomId'],
      roomNumber: json['roomNumber'] ?? '',
      roomName: json['roomName'] ?? '',
      floor: json['floor'] ?? '',
      startAt: DateTime.tryParse(json['startAt'] ?? '') ?? DateTime.now(),
      endAt: DateTime.tryParse(json['endAt'] ?? '') ?? DateTime.now(),
      consultationFee: (json['consultationFee'] as num?)?.toDouble() ?? 0,
      appointmentType: json['appointmentType'] ?? '',
      reason: json['reason'] ?? '',
      status: json['status'] ?? '',
      cancellationReason: json['cancellationReason'] ?? '',
      notes: json['notes'] ?? '',
      createdAt: DateTime.tryParse(json['createdAt'] ?? '') ?? DateTime.now(),
      updatedAt: DateTime.tryParse(json['updatedAt'] ?? '') ?? DateTime.now(),
    );
  }
}

class DoctorTimeSlot {
  final int doctorTimeSlotId;
  final int? doctorId;
  final String doctorName;
  final String specialty;
  final int? roomId;
  final String roomNumber;
  final String roomName;
  final String floor;
  final DateTime startAt;
  final DateTime endAt;
  final int capacity;
  final int bookedCount;
  final List<int> bookedAppointmentNumbers;
  final int nextAppointmentNumber;
  final bool isActive;
  final double consultationFee;

  DoctorTimeSlot({
    required this.doctorTimeSlotId,
    this.doctorId,
    required this.doctorName,
    required this.specialty,
    this.roomId,
    required this.roomNumber,
    required this.roomName,
    required this.floor,
    required this.startAt,
    required this.endAt,
    required this.capacity,
    required this.bookedCount,
    required this.bookedAppointmentNumbers,
    required this.nextAppointmentNumber,
    required this.isActive,
    required this.consultationFee,
  });

  int get availableCount =>
      capacity - bookedCount < 0 ? 0 : capacity - bookedCount;

  factory DoctorTimeSlot.fromJson(Map<String, dynamic> json) {
    return DoctorTimeSlot(
      doctorTimeSlotId: json['doctorTimeSlotId'] ?? 0,
      doctorId: json['doctorId'],
      doctorName: json['doctorName'] ?? '',
      specialty: json['specialty'] ?? '',
      roomId: json['roomId'],
      roomNumber: json['roomNumber'] ?? '',
      roomName: json['roomName'] ?? '',
      floor: json['floor'] ?? '',
      startAt: DateTime.tryParse(json['startAt'] ?? '') ?? DateTime.now(),
      endAt: DateTime.tryParse(json['endAt'] ?? '') ?? DateTime.now(),
      capacity: json['capacity'] ?? 0,
      bookedCount: json['bookedCount'] ?? 0,
      bookedAppointmentNumbers:
          (json['bookedAppointmentNumbers'] as List<dynamic>? ?? [])
              .map((value) => (value as num).toInt())
              .toList(),
      nextAppointmentNumber: json['nextAppointmentNumber'] ?? 0,
      isActive: json['isActive'] ?? false,
      consultationFee: (json['consultationFee'] as num?)?.toDouble() ?? 0,
    );
  }
}

class DoctorLookup {
  final int doctorId;
  final String doctorName;
  final String specialty;

  DoctorLookup({
    required this.doctorId,
    required this.doctorName,
    required this.specialty,
  });

  factory DoctorLookup.fromJson(Map<String, dynamic> json) {
    return DoctorLookup(
      doctorId: json['doctorId'] ?? 0,
      doctorName: json['doctorName'] ?? '',
      specialty: json['specialty'] ?? '',
    );
  }
}
