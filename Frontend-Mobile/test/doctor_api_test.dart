import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:medicore_mobile/core/services/api_service.dart';
import 'package:medicore_mobile/core/services/secure_token_storage.dart';
import 'package:medicore_mobile/features/doctors/screens/doctor_search_screen.dart';
import 'package:medicore_mobile/layouts/dashboard_layout.dart';

void main() {
  tearDown(() {
    ApiService.resetHttpClientForTesting();
    SecureTokenStorage.resetTokenForTesting();
  });

  test('searchDoctors sends query and parses approved doctor results',
      () async {
    SecureTokenStorage.setTokenForTesting('patient-token');
    ApiService.setHttpClientForTesting(MockClient((request) async {
      expect(request.url.path, '/api/patient/doctors');
      expect(request.url.queryParameters['query'], 'card');
      expect(request.headers['Authorization'], 'Bearer patient-token');
      return http.Response(
          jsonEncode([
            {
              'doctorId': 7,
              'fullName': 'Nimal Perera',
              'specialization': 'Cardiology'
            }
          ]),
          200,
          headers: {'content-type': 'application/json'});
    }));

    final doctors = await ApiService.searchDoctors(query: 'card');

    expect(doctors, hasLength(1));
    expect(doctors.single.doctorId, 7);
    expect(doctors.single.fullName, 'Nimal Perera');
    expect(doctors.single.specialization, 'Cardiology');
  });

  test('getDoctorProfile parses the public doctor profile', () async {
    SecureTokenStorage.setTokenForTesting('patient-token');
    ApiService.setHttpClientForTesting(MockClient((request) async {
      expect(request.url.path, '/api/patient/doctors/7');
      return http.Response(
          jsonEncode({
            'doctorId': 7,
            'fullName': 'Nimal Perera',
            'email': 'doctor@hospital.lk',
            'slmcLicenseNumber': 'SLMC-100',
            'specialization': 'Cardiology'
          }),
          200,
          headers: {'content-type': 'application/json'});
    }));

    final doctor = await ApiService.getDoctorProfile(7);

    expect(doctor.email, 'doctor@hospital.lk');
    expect(doctor.slmcLicenseNumber, 'SLMC-100');
  });

  testWidgets('typing shows suggestions and profile details', (tester) async {
    int? bookedDoctorId;
    SecureTokenStorage.setTokenForTesting('patient-token');
    ApiService.setHttpClientForTesting(MockClient((request) async {
      if (request.url.path == '/api/patient/doctors/7') {
        return http.Response(
            jsonEncode({
              'doctorId': 7,
              'fullName': 'Nimal Perera',
              'email': 'doctor@hospital.lk',
              'slmcLicenseNumber': 'SLMC-100',
              'specialization': 'Cardiology'
            }),
            200);
      }
      return http.Response(
          jsonEncode([
            {
              'doctorId': 7,
              'fullName': 'Nimal Perera',
              'specialization': 'Cardiology'
            }
          ]),
          200);
    }));

    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: DoctorSearchScreen(
          onBookAppointment: (doctor) => bookedDoctorId = doctor.doctorId,
        ),
      ),
    ));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('doctor-search-field')), 'car');
    await tester.pump(const Duration(milliseconds: 350));
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('doctor-search-suggestions')), findsOneWidget);
    expect(find.byKey(const Key('doctor-card-7')), findsOneWidget);

    await tester.tap(find.byKey(const Key('book-doctor-7')));
    expect(bookedDoctorId, 7);

    await tester.tap(find.byKey(const Key('view-doctor-7')));
    await tester.pumpAndSettle();

    expect(find.text('Doctor Profile'), findsOneWidget);
    expect(find.text('doctor@hospital.lk'), findsOneWidget);
    expect(find.text('SLMC-100'), findsOneWidget);
    expect(find.text('Cardiology'), findsWidgets);
  });

  testWidgets('Book opens the appointment form with doctor preselected',
      (tester) async {
    SharedPreferences.setMockInitialValues({
      'patient_full_name': 'Amal Perera',
      'patient_email': 'amal@example.com',
    });
    SecureTokenStorage.setTokenForTesting('patient-token');
    final futureStart = DateTime.now().toUtc().add(const Duration(days: 2));
    final futureEnd = futureStart.add(const Duration(hours: 1));

    ApiService.setHttpClientForTesting(MockClient((request) async {
      switch (request.url.path) {
        case '/api/patient/doctors':
          return http.Response(
              jsonEncode([
                {
                  'doctorId': 7,
                  'fullName': 'Nimal Perera',
                  'specialization': 'Cardiology'
                }
              ]),
              200);
        case '/api/patient/me':
          return http.Response(
              jsonEncode({
                'patientId': 1,
                'firstName': 'Amal',
                'lastName': 'Perera',
                'dateOfBirth': '1990-01-01T00:00:00Z',
                'gender': 'Male',
                'nic': '900010001V',
                'phoneNumber': '0771234567',
                'email': 'amal@example.com',
                'address': 'Colombo'
              }),
              200);
        case '/api/appointment':
          return http.Response(jsonEncode({'data': []}), 200);
        case '/api/appointment/available-slots':
          return http.Response(
              jsonEncode([
                {
                  'doctorTimeSlotId': 15,
                  'doctorId': 7,
                  'doctorName': 'Dr. Nimal Perera',
                  'specialty': 'Cardiology',
                  'startAt': futureStart.toIso8601String(),
                  'endAt': futureEnd.toIso8601String(),
                  'capacity': 5,
                  'bookedCount': 0,
                  'bookedAppointmentNumbers': [],
                  'nextAppointmentNumber': 1,
                  'isActive': true,
                  'consultationFee': 2500
                }
              ]),
              200);
        case '/api/appointment/doctors':
          return http.Response(
              jsonEncode([
                {
                  'doctorId': 7,
                  'doctorName': 'Dr. Nimal Perera',
                  'specialty': 'Cardiology'
                }
              ]),
              200);
        case '/api/appointment/specializations':
          return http.Response(jsonEncode(['Cardiology']), 200);
        default:
          return http.Response(jsonEncode({'message': 'Not found'}), 404);
      }
    }));

    await tester
        .pumpWidget(const MaterialApp(home: DashboardLayout(title: 'Doctors')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('book-doctor-7')));
    await tester.pumpAndSettle();

    expect(find.text('Book appointment'), findsOneWidget);
    expect(find.text('Cardiology'), findsWidgets);
    expect(find.text('Dr. Nimal Perera'), findsWidgets);
    final fields = tester.widgetList<DropdownButtonFormField<String>>(
      find.byType(DropdownButtonFormField<String>)).toList();
    expect(fields[0].initialValue, 'Cardiology');
    expect(tester.widget<TextFormField>(find.widgetWithText(
      TextFormField, 'Search or Select Doctor')).controller!.text, 'Dr. Nimal Perera');
    expect(find.text('Appointment date and time'), findsOneWidget);
    expect(find.text('Select appointment no'), findsNothing);
    final sessionField = tester.widget<DropdownButton<int>>(find.descendant(
      of: find.byType(DropdownButtonFormField<int>),
      matching: find.byType(DropdownButton<int>)));
    expect(sessionField.items!.map((item) => item.value), [15]);
  });
}
