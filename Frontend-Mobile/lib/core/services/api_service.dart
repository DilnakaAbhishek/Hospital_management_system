import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;
import 'package:http_parser/http_parser.dart' as http_parser;
import 'package:medicore_mobile/core/services/secure_token_storage.dart';
import 'package:medicore_mobile/models/appointment.dart';
import 'package:medicore_mobile/models/doctor.dart';
import 'package:medicore_mobile/models/medical_record.dart';
import 'package:medicore_mobile/models/patient.dart';
import 'package:medicore_mobile/models/triage_workflow.dart';
import 'package:medicore_mobile/models/hospital_assistant.dart';

class ApiService {
  // Android emulators use 10.0.2.2 to reach the development machine.
  // For a physical device, replace this with your computer's LAN IP address.
  static final String baseUrl = kIsWeb
      ? 'http://localhost:5000/api'
      : defaultTargetPlatform == TargetPlatform.android
          ? 'http://10.0.2.2:5000/api'
          : 'http://localhost:5000/api';

  static http.Client _client = http.Client();

  @visibleForTesting
  static void setHttpClientForTesting(http.Client client) {
    _client = client;
  }

  @visibleForTesting
  static void resetHttpClientForTesting() {
    _client.close();
    _client = http.Client();
  }

  static Future<Map<String, String>> _authHeaders() async {
    final token = await SecureTokenStorage.readToken();

    return {
      'Content-Type': 'application/json',
      if (token != null && token.isNotEmpty)
        'Authorization': 'Bearer $token',
    };
  }

  static Future<List<Map<String, dynamic>>>
      getAppointmentNotifications() async {
    final response = await _client.get(
      Uri.parse('$baseUrl/appointment/notifications'),
      headers: await _authHeaders(),
    );

    if (response.statusCode != 200) {
      throw Exception('Unable to load notifications.');
    }

    return (jsonDecode(response.body) as List<dynamic>)
        .map((item) => item as Map<String, dynamic>)
        .toList();
  }

  static Future<List<Map<String, dynamic>>> getClinicalReviewNotifications() async {
    final response = await _client.get(
      Uri.parse('$baseUrl/triage-workflows/notifications'),
      headers: await _authHeaders(),
    );
    if (response.statusCode != 200) {
      throw Exception('Unable to load clinical-review notifications.');
    }
    return (jsonDecode(response.body) as List<dynamic>)
        .map((item) => item as Map<String, dynamic>)
        .toList();
  }

  static Future<List<Map<String, dynamic>>> getMedicalRecordNotifications() async {
    final response = await _client.get(
      Uri.parse('$baseUrl/medicalrecord/notifications/patient'),
      headers: await _authHeaders(),
    );
    if (response.statusCode != 200) {
      throw Exception('Unable to load medical record notifications.');
    }
    return (jsonDecode(response.body) as List<dynamic>)
        .map((item) => item as Map<String, dynamic>)
        .toList();
  }

  static Future<List<Patient>> getPatients({String search = ''}) async {
    final query = <String, String>{
      'page': '1',
      'pageSize': '50',
      'sortBy': 'name',
      'sortDirection': 'asc',
      if (search.trim().isNotEmpty) 'search': search.trim(),
    };

    final uri = Uri.parse('$baseUrl/patient').replace(
      queryParameters: query,
    );

    final response = await _client.get(
      uri,
      headers: await _authHeaders(),
    );

    if (response.statusCode != 200) {
      throw Exception('Unable to load patient records.');
    }

    final body = jsonDecode(response.body) as Map<String, dynamic>;
    final items = body['data'] as List<dynamic>? ?? [];

    return items
        .map((item) => Patient.fromJson(item as Map<String, dynamic>))
        .toList();
  }

  static Future<Map<String, dynamic>> login({
    required String email,
    required String password,
  }) async {
    final response = await _client.post(
      Uri.parse('$baseUrl/auth/login'),
      headers: {
        'Content-Type': 'application/json',
        'X-Client-Platform': 'mobile',
      },
      body: jsonEncode({
        'email': email.trim(),
        'password': password,
      }),
    );

    if (response.statusCode == 200) {
      final data = jsonDecode(response.body) as Map<String, dynamic>;
      final role = (data['role'] ?? '').toString();
      if (role.toLowerCase() != 'patient') {
        throw Exception(
          'Access restricted: Admin and Doctor accounts are only permitted to log in via the web portal. The mobile application is reserved for patients.',
        );
      }
      return data;
    }

    throw Exception(_errorMessage(response.body));
  }

  static Future<List<DoctorSearchResult>> searchDoctors({
    String query = '',
    int limit = 20,
  }) async {
    final uri = Uri.parse('$baseUrl/patient/doctors').replace(
      queryParameters: {
        if (query.trim().isNotEmpty) 'query': query.trim(),
        'limit': limit.clamp(1, 50).toString(),
      },
    );

    final response = await _client.get(
      uri,
      headers: await _authHeaders(),
    );

    if (response.statusCode != 200) {
      throw Exception(_errorMessage(response.body));
    }

    final body = jsonDecode(response.body) as List<dynamic>;

    return body
        .map(
          (item) => DoctorSearchResult.fromJson(
            item as Map<String, dynamic>,
          ),
        )
        .toList();
  }

  static Future<DoctorPublicProfile> getDoctorProfile(
    int doctorId,
  ) async {
    final response = await _client.get(
      Uri.parse('$baseUrl/patient/doctors/$doctorId'),
      headers: await _authHeaders(),
    );

    if (response.statusCode != 200) {
      throw Exception(_errorMessage(response.body));
    }

    return DoctorPublicProfile.fromJson(
      jsonDecode(response.body) as Map<String, dynamic>,
    );
  }

  static Future<void> register(
    Map<String, dynamic> patient,
  ) async {
    final response = await _client.post(
      Uri.parse('$baseUrl/auth/register'),
      headers: {
        'Content-Type': 'application/json',
      },
      body: jsonEncode(patient),
    );

    if (response.statusCode != 201) {
      throw Exception(_errorMessage(response.body));
    }
  }

  static Future<void> changePassword({
    required String email,
    required String currentPassword,
    required String newPassword,
  }) async {
    final response = await _client.post(
      Uri.parse('$baseUrl/auth/change-password'),
      headers: {
        'Content-Type': 'application/json',
      },
      body: jsonEncode({
        'email': email.trim().toLowerCase(),
        'currentPassword': currentPassword,
        'newPassword': newPassword,
      }),
    );

    if (response.statusCode != 200) {
      throw Exception(_errorMessage(response.body));
    }
  }

  static String _errorMessage(String body) {
    try {
      final decoded = jsonDecode(body);

      if (decoded is Map<String, dynamic>) {
        if (decoded.containsKey('message')) {
          return decoded['message'].toString();
        }

        if (decoded.containsKey('title')) {
          return decoded['title'].toString();
        }

        if (decoded.containsKey('errors')) {
          final errors = decoded['errors'];

          if (errors is Map<String, dynamic>) {
            final firstKey = errors.keys.first;
            final firstList = errors[firstKey];

            if (firstList is List && firstList.isNotEmpty) {
              return firstList.first.toString();
            }
          }
        }
      }

      return 'Request failed. Please check your inputs.';
    } catch (_) {
      return body.isNotEmpty ? body : 'Server error occurred.';
    }
  }

  static Future<Patient?> getMyProfile() async {
    try {
      final response = await _client.get(
        Uri.parse('$baseUrl/patient/me'),
        headers: await _authHeaders(),
      );

      if (response.statusCode == 200) {
        return Patient.fromJson(jsonDecode(response.body));
      }
    } catch (e) {
      debugPrint('ApiService getMyProfile Error: $e');
    }

    return null;
  }

  static Future<Patient?> getPatientById(int id) async {
    try {
      final response = await _client.get(
        Uri.parse('$baseUrl/patient/$id'),
        headers: await _authHeaders(),
      );

      if (response.statusCode == 200) {
        return Patient.fromJson(jsonDecode(response.body));
      }
    } catch (e) {
      debugPrint('ApiService Error: $e');
    }

    return null;
  }

  static Future<Patient?> createPatient(
    Map<String, dynamic> data,
  ) async {
    try {
      final response = await _client.post(
        Uri.parse('$baseUrl/patient'),
        headers: await _authHeaders(),
        body: jsonEncode(data),
      );

      if (response.statusCode == 201 || response.statusCode == 200) {
        return Patient.fromJson(jsonDecode(response.body));
      }
    } catch (e) {
      debugPrint('ApiService Create Error: $e');
    }

    return null;
  }

  static Future<Patient> savePatient(
    Map<String, dynamic> data, {
    int? patientId,
  }) async {
    final uri = Uri.parse(
      patientId == null
          ? '$baseUrl/patient'
          : '$baseUrl/patient/$patientId',
    );

    final response = patientId == null
        ? await _client.post(
            uri,
            headers: await _authHeaders(),
            body: jsonEncode(data),
          )
        : await _client.put(
            uri,
            headers: await _authHeaders(),
            body: jsonEncode(data),
          );

    if (response.statusCode == 200 || response.statusCode == 201) {
      return Patient.fromJson(
        jsonDecode(response.body) as Map<String, dynamic>,
      );
    }

    final message = response.body.isEmpty
        ? 'Unable to save patient.'
        : response.body;

    throw Exception(message);
  }

  static Future<void> deletePatient(int patientId) async {
    final response = await _client.delete(
      Uri.parse('$baseUrl/patient/$patientId'),
      headers: await _authHeaders(),
    );

    if (response.statusCode != 204) {
      throw Exception('Unable to delete patient.');
    }
  }

  static Future<Patient> updateMyProfile(
    Map<String, dynamic> data,
  ) async {
    final response = await _client.put(
      Uri.parse('$baseUrl/patient/me'),
      headers: await _authHeaders(),
      body: jsonEncode(data),
    );

    if (response.statusCode == 200) {
      return Patient.fromJson(
        jsonDecode(response.body) as Map<String, dynamic>,
      );
    }

    throw Exception(_errorMessage(response.body));
  }

  static Future<List<Appointment>> getMyAppointments() async {
    final uri = Uri.parse('$baseUrl/appointment').replace(
      queryParameters: {
        'page': '1',
        'pageSize': '50',
        'sortBy': 'startAt',
        'sortDirection': 'desc',
      },
    );

    final response = await _client.get(
      uri,
      headers: await _authHeaders(),
    );

    if (response.statusCode == 200) {
      final body = jsonDecode(response.body);

      final items = body is Map<String, dynamic>
          ? body['data'] as List<dynamic>? ?? []
          : body as List<dynamic>? ?? [];

      return items
          .map(
            (item) => Appointment.fromJson(
              item as Map<String, dynamic>,
            ),
          )
          .toList();
    }

    throw Exception(_errorMessage(response.body));
  }

  static Future<List<DoctorTimeSlot>>
      getAvailableAppointmentSlots() async {
    final uri = Uri.parse(
      '$baseUrl/appointment/available-slots',
    ).replace(
      queryParameters: {
        'onlyAvailable': 'true',
      },
    );

    final response = await _client.get(
      uri,
      headers: await _authHeaders(),
    );

    if (response.statusCode == 200) {
      final items = jsonDecode(response.body) as List<dynamic>? ?? [];

      return items
          .map(
            (item) => DoctorTimeSlot.fromJson(
              item as Map<String, dynamic>,
            ),
          )
          .where(
            (slot) => slot.isActive && slot.availableCount > 0,
          )
          .toList();
    }

    throw Exception(_errorMessage(response.body));
  }

  static Future<List<DoctorLookup>> getAppointmentDoctors() async {
    final response = await _client.get(
      Uri.parse('$baseUrl/appointment/doctors'),
      headers: await _authHeaders(),
    );

    if (response.statusCode == 200) {
      final items = jsonDecode(response.body) as List<dynamic>? ?? [];

      return items
          .map(
            (item) => DoctorLookup.fromJson(
              item as Map<String, dynamic>,
            ),
          )
          .toList();
    }

    throw Exception(_errorMessage(response.body));
  }

  static Future<List<String>>
      getAppointmentSpecializations() async {
    final response = await _client.get(
      Uri.parse('$baseUrl/appointment/specializations'),
      headers: await _authHeaders(),
    );

    if (response.statusCode == 404) {
      return [];
    }

    if (response.statusCode == 200) {
      final items = jsonDecode(response.body) as List<dynamic>? ?? [];

      return items
          .map((item) => item.toString().trim())
          .where((item) => item.isNotEmpty)
          .toSet()
          .toList()
        ..sort();
    }

    throw Exception(_errorMessage(response.body));
  }

  static Future<Appointment> bookAppointment({
    required int doctorTimeSlotId,
    required String patientName,
    required String patientPhone,
    String? patientEmail,
    required String appointmentType,
  }) async {
    final response = await _client.post(
      Uri.parse('$baseUrl/appointment'),
      headers: await _authHeaders(),
      body: jsonEncode({
        'doctorTimeSlotId': doctorTimeSlotId,
        'patientName': patientName.trim(),
        'patientPhone': patientPhone.trim(),
        if (patientEmail != null &&
            patientEmail.trim().isNotEmpty)
          'patientEmail': patientEmail.trim().toLowerCase(),
        'appointmentType': appointmentType.trim(),
      }),
    );

    if (response.statusCode == 201 || response.statusCode == 200) {
      return Appointment.fromJson(jsonDecode(response.body));
    }

    throw Exception(_errorMessage(response.body));
  }

  static Future<Appointment> rescheduleAppointment({
    required int appointmentId,
    required int doctorTimeSlotId,
  }) async {
    final response = await _client.post(
      Uri.parse(
        '$baseUrl/appointment/$appointmentId/reschedule',
      ),
      headers: await _authHeaders(),
      body: jsonEncode({
        'doctorTimeSlotId': doctorTimeSlotId,
      }),
    );

    if (response.statusCode == 200) {
      return Appointment.fromJson(jsonDecode(response.body));
    }

    throw Exception(_errorMessage(response.body));
  }
  static Future<Appointment> cancelAppointment({
    required int appointmentId,
    required String reason,
  }) async {
    final response = await _client.post(
      Uri.parse(
        '$baseUrl/appointment/$appointmentId/cancel',
      ),
      headers: await _authHeaders(),
      body: jsonEncode({
        'reason': reason.trim(),
      }),
    );

    if (response.statusCode == 200) {
      return Appointment.fromJson(jsonDecode(response.body));
    }

    throw Exception(_errorMessage(response.body));
  }

  static Future<TriageWorkflow> startTriageWorkflow({
    required String symptoms,
    Map<String, dynamic>? vitals,
    bool isFollowUp = false,
  }) async {
    final response = await http
        .post(
          Uri.parse('$baseUrl/triage-workflows'),
          headers: await _authHeaders(),
          body: jsonEncode({
            'symptoms': symptoms.trim(),
            'isFollowUp': isFollowUp,
            if (vitals != null) 'vitals': vitals,
          }),
        )
        .timeout(const Duration(seconds: 85));

    if (response.statusCode == 201) {
      return TriageWorkflow.fromJson(
        jsonDecode(response.body),
      );
    }

    throw Exception(_errorMessage(response.body));
  }

  static Future<TriageWorkflow> getTriageWorkflow(
    int workflowId,
  ) async {
    final response = await _client.get(
      Uri.parse('$baseUrl/triage-workflows/$workflowId'),
      headers: await _authHeaders(),
    );

    if (response.statusCode == 200) {
      return TriageWorkflow.fromJson(
        jsonDecode(response.body),
      );
    }

    throw Exception(_errorMessage(response.body));
  }

  static Future<List<TriageWorkflow>>
      getTriageWorkflowHistory() async {
    final response = await _client.get(
      Uri.parse('$baseUrl/triage-workflows/history'),
      headers: await _authHeaders(),
    );

    if (response.statusCode == 200) {
      return (jsonDecode(response.body) as List<dynamic>)
          .map(
            (item) => TriageWorkflow.fromJson(
              item as Map<String, dynamic>,
            ),
          )
          .toList();
    }

    throw Exception(_errorMessage(response.body));
  }

  static Future<TriageWorkflow> continueTriageWorkflow({
    required int workflowId,
    required List<Map<String, dynamic>> answers,
  }) async {
    final response = await http
        .post(
          Uri.parse(
            '$baseUrl/triage-workflows/$workflowId/continue',
          ),
          headers: await _authHeaders(),
          body: jsonEncode({
            'answers': answers,
          }),
        )
        .timeout(const Duration(seconds: 85));

    if (response.statusCode == 200) {
      return TriageWorkflow.fromJson(
        jsonDecode(response.body),
      );
    }

    throw Exception(_errorMessage(response.body));
  }

  // ---------- Shared Hospital AI Assistant ----------

  static Future<List<AssistantCapability>> getAssistantCapabilities() async {
    final response = await _client.get(
      Uri.parse('$baseUrl/hospital-assistant/capabilities'),
      headers: await _authHeaders(),
    );
    if (response.statusCode != 200) {
      throw Exception(_errorMessage(response.body));
    }
    return assistantObjects(jsonDecode(response.body))
        .map(AssistantCapability.fromJson).toList();
  }

  static Future<List<AssistantJson>> getAssistantHistory() async {
    final response = await _client.get(
      Uri.parse('$baseUrl/hospital-assistant/conversations'),
      headers: await _authHeaders(),
    );
    if (response.statusCode != 200) {
      throw Exception(_errorMessage(response.body));
    }
    return assistantObjects(jsonDecode(response.body));
  }

  static Future<AssistantConversation> getAssistantConversation(String id) async {
    final response = await _client.get(
      Uri.parse('$baseUrl/hospital-assistant/conversations/${Uri.encodeComponent(id)}'),
      headers: await _authHeaders(),
    );
    if (response.statusCode != 200) {
      throw Exception(_errorMessage(response.body));
    }
    return AssistantConversation.fromJson(jsonDecode(response.body));
  }

  static Future<AssistantConversation> sendAssistantMessage({
    String? conversationId,
    required String message,
    required String requestId,
    String? requirementId,
    String? requirementState,
  }) async {
    final response = await _client.post(
      Uri.parse('$baseUrl/hospital-assistant/messages'),
      headers: await _authHeaders(),
      body: jsonEncode({
        if (conversationId != null) 'conversationId': conversationId,
        'message': message,
        if (requirementId != null) 'requirementId': requirementId,
        if (requirementState != null) 'requirementState': requirementState,
        'requestId': requestId,
      }),
    ).timeout(const Duration(seconds: 180));
    if (response.statusCode != 200 && response.statusCode != 201) {
      throw Exception(_errorMessage(response.body));
    }
    return AssistantConversation.fromJson(jsonDecode(response.body));
  }

  static Future<AssistantConversation> decideAssistantAction({
    required String conversationId,
    required String actionId,
    required String decision,
    required String requestId,
    int? doctorTimeSlotId,
    int? appointmentId,
  }) async {
    final response = await _client.post(
      Uri.parse('$baseUrl/hospital-assistant/conversations/${Uri.encodeComponent(conversationId)}/actions'),
      headers: await _authHeaders(),
      body: jsonEncode({
        'actionId': actionId,
        'decision': decision,
        'requestId': requestId,
        if (doctorTimeSlotId != null) 'doctorTimeSlotId': doctorTimeSlotId,
        if (appointmentId != null) 'appointmentId': appointmentId,
      }),
    ).timeout(const Duration(seconds: 90));
    if (response.statusCode != 200) {
      throw Exception(_errorMessage(response.body));
    }
    return AssistantConversation.fromJson(jsonDecode(response.body));
  }

  // ---------- Legacy Patient Care APIs (retained for compatibility) ----------

  static Future<Map<String, dynamic>>
      createTriageAppointmentProposal({
    required String symptoms,
    String? specialty,
    bool requestAppointmentProposal = false,
    Map<String, dynamic>? vitals,
  }) async {
    final response = await _client
        .post(
          Uri.parse(
            '$baseUrl/patient-care/triage-appointment-proposal',
          ),
          headers: await _authHeaders(),
          body: jsonEncode({
            'symptoms': symptoms.trim(),
            'requestAppointmentProposal':
                requestAppointmentProposal,
            if (specialty != null &&
                specialty.trim().isNotEmpty)
              'specialty': specialty.trim(),
            if (vitals != null) 'vitals': vitals,
          }),
        )
        .timeout(const Duration(seconds: 85));

    if (response.statusCode == 200) {
      return jsonDecode(response.body)
          as Map<String, dynamic>;
    }

    throw Exception(_errorMessage(response.body));
  }

  static Future<Map<String, dynamic>>
      confirmAppointmentProposal({
    required int proposalId,
    required int doctorTimeSlotId,
  }) async {
    final response = await _client.post(
      Uri.parse(
        '$baseUrl/appointment-proposals/$proposalId/confirm',
      ),
      headers: await _authHeaders(),
      body: jsonEncode({
        'doctorTimeSlotId': doctorTimeSlotId,
      }),
    );

    if (response.statusCode == 200) {
      return jsonDecode(response.body)
          as Map<String, dynamic>;
    }

    throw Exception(_errorMessage(response.body));
  }

  static Future<List<Map<String, dynamic>>>
      getPatientCareHistory() async {
    final response = await _client.get(
      Uri.parse('$baseUrl/patient-care/history'),
      headers: await _authHeaders(),
    );

    if (response.statusCode == 200) {
      return (jsonDecode(response.body) as List<dynamic>)
          .map((item) => item as Map<String, dynamic>)
          .toList();
    }

    throw Exception(_errorMessage(response.body));
  }

  // ---------- Medical Records Endpoints ----------

  static Future<List<MedicalRecord>> getMedicalRecords({
    int page = 1,
    int pageSize = 100,
    String? search,
    String? recordType,
    String? status,
    DateTime? fromDate,
    DateTime? toDate,
    int? doctorId,
  }) async {
    try {
      final queryParams = <String, String>{
        'page': page.toString(),
        'pageSize': pageSize.toString(),
        if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
        if (recordType != null && recordType != 'All' && recordType.trim().isNotEmpty)
          'recordType': recordType.trim(),
        if (status != null && status != 'All' && status.trim().isNotEmpty)
          'status': status.trim(),
        if (fromDate != null)
          'fromDate': fromDate.toIso8601String().split('T').first,
        if (toDate != null)
          'toDate': toDate.toIso8601String().split('T').first,
        if (doctorId != null)
          'doctorId': doctorId.toString(),
      };

      final uri = Uri.parse('$baseUrl/medicalrecord').replace(queryParameters: queryParams);
      final response = await _client.get(
        uri,
        headers: await _authHeaders(),
      );

      if (response.statusCode == 200) {
        final decoded = jsonDecode(response.body);
        if (decoded is Map && decoded.containsKey('data')) {
          final items = decoded['data'] as List<dynamic>;
          return items.map((item) => MedicalRecord.fromJson(item as Map<String, dynamic>)).toList();
        } else if (decoded is List) {
          return decoded.map((item) => MedicalRecord.fromJson(item as Map<String, dynamic>)).toList();
        }
      }
    } catch (_) {}

    // Fallback to getMyMedicalRecords
    return getMyMedicalRecords();
  }

  static Future<List<MedicalRecord>> getMyMedicalRecords() async {
    final response = await _client.get(
      Uri.parse('$baseUrl/medicalrecord/me'),
      headers: await _authHeaders(),
    );

    if (response.statusCode == 200) {
      final list = jsonDecode(response.body) as List<dynamic>;
      return list
          .map((item) => MedicalRecord.fromJson(item as Map<String, dynamic>))
          .toList();
    }

    // Secondary fallback to /medicalrecord if /me is unauthorized or not a patient
    final fallback = await _client.get(
      Uri.parse('$baseUrl/medicalrecord?pageSize=100'),
      headers: await _authHeaders(),
    );

    if (fallback.statusCode == 200) {
      final decoded = jsonDecode(fallback.body);
      final List<dynamic> items = decoded is Map && decoded.containsKey('data')
          ? decoded['data'] as List<dynamic>
          : decoded is List
              ? decoded
              : [];
      return items.map((item) => MedicalRecord.fromJson(item as Map<String, dynamic>)).toList();
    }

    throw Exception(_errorMessage(response.body));
  }

  static Future<MedicalRecord> createMedicalRecord(Map<String, dynamic> data) async {
    final response = await _client.post(
      Uri.parse('$baseUrl/medicalrecord'),
      headers: await _authHeaders(),
      body: jsonEncode(data),
    );

    if (response.statusCode == 201 || response.statusCode == 200) {
      return MedicalRecord.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
    }

    throw Exception(_errorMessage(response.body));
  }

  static Future<MedicalRecord> updateMedicalRecord(int id, Map<String, dynamic> data) async {
    final response = await _client.put(
      Uri.parse('$baseUrl/medicalrecord/$id'),
      headers: await _authHeaders(),
      body: jsonEncode(data),
    );

    if (response.statusCode == 200) {
      return MedicalRecord.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
    }

    throw Exception(_errorMessage(response.body));
  }

  static Future<bool> deleteMedicalRecord(int id) async {
    final response = await _client.delete(
      Uri.parse('$baseUrl/medicalrecord/$id'),
      headers: await _authHeaders(),
    );

    if (response.statusCode == 204 || response.statusCode == 200) {
      return true;
    }

    throw Exception(_errorMessage(response.body));
  }

  static Future<MedicalRecordSummary> getMedicalRecordSummary() async {
    final response = await _client.get(
      Uri.parse('$baseUrl/medicalrecord/summary'),
      headers: await _authHeaders(),
    );

    if (response.statusCode == 200) {
      return MedicalRecordSummary.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
    }

    return const MedicalRecordSummary();
  }

  static Future<bool> deleteMedicalRecordAttachment(int recordId, int attachmentId) async {
    final response = await _client.delete(
      Uri.parse('$baseUrl/medicalrecord/$recordId/attachments/$attachmentId'),
      headers: await _authHeaders(),
    );

    if (response.statusCode == 204 || response.statusCode == 200) {
      return true;
    }

    throw Exception(_errorMessage(response.body));
  }

  static Future<List<Patient>> getAllPatients({int pageSize = 100, String? search}) async {
    final queryParams = <String, String>{
      'pageSize': pageSize.toString(),
    };
    if (search != null && search.trim().isNotEmpty) {
      queryParams['search'] = search.trim();
    }
    final uri = Uri.parse('$baseUrl/patient').replace(queryParameters: queryParams);
    final response = await _client.get(
      uri,
      headers: await _authHeaders(),
    );

    if (response.statusCode == 200) {
      final decoded = jsonDecode(response.body);
      final List<dynamic> items = decoded is Map && decoded.containsKey('data')
          ? decoded['data'] as List<dynamic>
          : decoded is List
              ? decoded
              : [];
      return items.map((item) => Patient.fromJson(item as Map<String, dynamic>)).toList();
    }

    return [];
  }

  static Future<Patient> getMyPatientProfile() async {
    final response = await _client.get(
      Uri.parse('$baseUrl/patient/me'),
      headers: await _authHeaders(),
    );

    if (response.statusCode == 200) {
      return Patient.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
    }

    throw Exception(_errorMessage(response.body));
  }

  static Future<MedicalRecord> getMedicalRecordById(
    int id,
  ) async {
    final response = await _client.get(
      Uri.parse('$baseUrl/medicalrecord/$id'),
      headers: await _authHeaders(),
    );

    if (response.statusCode == 200) {
      return MedicalRecord.fromJson(
        jsonDecode(response.body),
      );
    }

    throw Exception(_errorMessage(response.body));
  }

  static Future<MedicalRecordAttachment> addMedicalRecordAttachment(
    int recordId, {
    required String fileName,
    required String fileType,
    required String fileUrl,
    required int fileSize,
  }) async {
    final response = await _client.post(
      Uri.parse(
        '$baseUrl/medicalrecord/$recordId/attachments',
      ),
      headers: await _authHeaders(),
      body: jsonEncode({
        'fileName': fileName,
        'fileType': fileType,
        'fileUrl': fileUrl,
        'fileSize': fileSize,
      }),
    );

    if (response.statusCode == 201) {
      return MedicalRecordAttachment.fromJson(
        jsonDecode(response.body),
      );
    }

    throw Exception(_errorMessage(response.body));
  }

  static Future<MedicalRecordAttachment> uploadMedicalRecordAttachment(
    int recordId, {
    required List<int> fileBytes,
    required String fileName,
    String? fileType,
  }) async {
    final headers = await _authHeaders();
    final uri = Uri.parse('$baseUrl/medicalrecord/$recordId/upload-attachment');
    final request = http.MultipartRequest('POST', uri);

    request.headers.addAll(headers);
    request.headers.remove('content-type');
    request.headers.remove('Content-Type');

    final mediaType = (fileType != null && fileType.contains('/'))
        ? http_parser.MediaType.parse(fileType)
        : (fileName.toLowerCase().endsWith('.png')
            ? http_parser.MediaType('image', 'png')
            : fileName.toLowerCase().endsWith('.pdf')
                ? http_parser.MediaType('application', 'pdf')
                : http_parser.MediaType('image', 'jpeg'));

    request.files.add(
      http.MultipartFile.fromBytes(
        'file',
        fileBytes,
        filename: fileName,
        contentType: mediaType,
      ),
    );

    final streamed = await request.send();
    final response = await http.Response.fromStream(streamed);

    if (response.statusCode == 201) {
      return MedicalRecordAttachment.fromJson(
        jsonDecode(response.body),
      );
    }

    throw Exception(_errorMessage(response.body));
  }
}
