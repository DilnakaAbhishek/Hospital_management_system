import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:medicore_mobile/core/services/api_service.dart';
import 'package:medicore_mobile/core/services/secure_token_storage.dart';
import 'package:medicore_mobile/layouts/dashboard_layout.dart';

void main() {
  tearDown(() {
    ApiService.resetHttpClientForTesting();
    SecureTokenStorage.resetTokenForTesting();
  });

  for (final size in [const Size(800, 600), const Size(390, 844)]) {
  testWidgets('patient sees saved schedule changes in notifications at $size', (tester) async {
    tester.view.physicalSize = size;
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    SharedPreferences.setMockInitialValues({});
    SecureTokenStorage.setTokenForTesting('patient-token');
    ApiService.setHttpClientForTesting(MockClient((request) async {
      if (request.url.path == '/api/appointment/notifications') {
        expect(request.headers['Authorization'], 'Bearer patient-token');
        return jsonResponse([{
          'appointmentNotificationId': 1,
          'message': 'Your appointment time has changed to 11:00 AM.',
          'createdAt': '2026-09-07T08:00:00Z',
        }]);
      }
      if (request.url.path == '/api/triage-workflows/notifications' ||
          request.url.path == '/api/medicalrecord/notifications/patient' ||
          request.url.path == '/api/medicalrecord/notifications') {
        return jsonResponse([]);
      }
      return jsonResponse({'data': []});
    }));
    await tester.pumpWidget(const MaterialApp(home: DashboardLayout()));
    await tester.pumpAndSettle();
    expect(find.text('Your appointment time has changed to 11:00 AM.'), findsNothing);
    await tester.tap(find.byTooltip('Notifications'));
    await tester.pumpAndSettle();
    expect(find.text('Appointment Update'), findsOneWidget);
    expect(find.text('Your appointment time has changed to 11:00 AM.'), findsOneWidget);
    expect(find.text('Your cardiology visit has been confirmed.'), findsNothing);
    await tester.tapAt(const Offset(5, 550));
    await tester.pumpAndSettle();
    expect(find.text('Your appointment time has changed to 11:00 AM.'), findsNothing);
    expect(find.text('Home'), findsWidgets);
    await tester.tap(find.byTooltip('Notifications'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Clear all'));
    await tester.pumpAndSettle();
    expect(find.text('No notifications'), findsOneWidget);
    await tester.tap(find.byTooltip('Close notifications'));
    await tester.pumpAndSettle();
    expect(find.text('No notifications'), findsNothing);
  });
  }

  testWidgets('doctor Book opens the form with that doctor preselected',
      (tester) async {
    final requests = <http.Request>[];
    await pumpAppointmentsDashboard(tester, requests: requests, title: 'Doctors');
    await tester.tap(find.text('Book').first);
    await tester.pumpAndSettle();

    final fields = tester.widgetList<DropdownButtonFormField<String>>(
      find.byType(DropdownButtonFormField<String>)).toList();
    expect(fields[0].initialValue, 'Cardiology');
    expect(tester.widget<TextFormField>(find.widgetWithText(
      TextFormField, 'Search or Select Doctor')).controller!.text, 'Dr. Ada Lovelace');
    expect(find.text('Appointment date and time'), findsOneWidget);
    expect(find.text('Select appointment no'), findsNothing);
    expect(requests.any((request) => request.method == 'POST'), isFalse);

    await selectBookingSession(tester);
    expect(find.text('Next available appointment number: #1\nThis number may change if someone books before you confirm.'), findsOneWidget);
    await tester.ensureVisible(find.text('Confirm'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Confirm'));
    await tester.pumpAndSettle();
    final booking = requests.singleWhere((request) =>
        request.method == 'POST' && request.url.path == '/api/appointment');
    expect(jsonDecode(booking.body)['doctorTimeSlotId'], 11);
    expect((jsonDecode(booking.body) as Map).containsKey('appointmentNumber'), isFalse);
    expect(find.text('Appointment booked successfully.'), findsOneWidget);
  });

  testWidgets('patient can complete the appointment booking form',
      (tester) async {
    final requests = <http.Request>[];
    await pumpAppointmentsDashboard(tester, requests: requests);

    await tester.tap(find.text('Book Appointment'));
    await tester.pumpAndSettle();
    await selectDropdownValue(tester, 0, 'Cardiology');
    final doctorField = find.widgetWithText(TextFormField, 'Search or Select Doctor');
    await tester.ensureVisible(doctorField);
    await tester.pumpAndSettle();
    await tester.enterText(doctorField, 'Ada');
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(ListTile, 'Dr. Ada Lovelace'));
    await tester.pumpAndSettle();
    await selectBookingSession(tester);
    await tester.ensureVisible(find.text('Confirm'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Confirm'));
    await tester.pumpAndSettle();

    expect(
      requests.any((request) =>
          request.method == 'POST' && request.url.path == '/api/appointment'),
      isTrue,
    );
    expect(find.text('Appointment booked successfully.'), findsOneWidget);
  });

  testWidgets('patient can switch between upcoming appointments and history',
      (tester) async {
    await pumpAppointmentsDashboard(tester);

    expect(find.text('Upcoming appointments'), findsOneWidget);
    expect(find.text('Dr. Ada Lovelace'), findsWidgets);

    await tester.tap(find.text('View History'));
    await tester.pumpAndSettle();

    expect(find.text('Appointment history'), findsOneWidget);
    expect(find.text('Dr. Grace Hopper'), findsWidgets);
  });

  testWidgets('patient can submit a cancellation reason', (tester) async {
    final requests = <http.Request>[];
    await pumpAppointmentsDashboard(tester, requests: requests);

    await tester.ensureVisible(find.text('Cancel'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Cancel').first);
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextFormField).last, 'Patient unavailable');
    await tester.tap(find.widgetWithText(FilledButton, 'Cancel appointment'));
    await tester.pumpAndSettle();

    expect(
      requests.any((request) =>
          request.method == 'POST' &&
          request.url.path == '/api/appointment/10/cancel' &&
          request.body.contains('Patient unavailable')),
      isTrue,
    );
    expect(find.text('Appointment cancelled.'), findsOneWidget);
  });

  testWidgets('appointment card offers rescheduling for future appointments', (tester) async {
    await pumpAppointmentsDashboard(tester, rescheduleFixture: true);
    expect(find.text('Estimated time'), findsNothing);
    expect(find.text('Reschedule'), findsOneWidget);
    expect(find.text('Upcoming'), findsNothing);
    expect(find.text('Cancel'), findsWidgets);
  });

  testWidgets('rescheduling keeps the doctor and posts the selected session', (tester) async {
    final requests = <http.Request>[];
    await pumpAppointmentsDashboard(tester,
        requests: requests, rescheduleFixture: true);
    await tester.ensureVisible(find.text('Reschedule'));
    await tester.tap(find.text('Reschedule'));
    await tester.pumpAndSettle();
    expect(find.text('Dr. Ada Lovelace · Cardiology'), findsOneWidget);
    expect(tester.widget<FilledButton>(find.widgetWithText(
        FilledButton, 'Confirm reschedule')).onPressed, isNull);
    final dateFinder = find.byType(DropdownButtonFormField<String>);
    final dateField = tester.widget<DropdownButton<String>>(find.descendant(
        of: dateFinder, matching: find.byType(DropdownButton<String>)));
    final dateLabel = (dateField.items!.single.child as Text).data!;
    await tester.tap(dateFinder);
    await tester.pumpAndSettle();
    await tester.tap(find.text(dateLabel).last);
    await tester.pumpAndSettle();
    final sessionFinder = find.byType(DropdownButtonFormField<int>);
    final sessionField = tester.widget<DropdownButton<int>>(find.descendant(
        of: sessionFinder, matching: find.byType(DropdownButton<int>)));
    expect(sessionField.items!.map((item) => item.value), [13]);
    final sessionLabel = (sessionField.items!.single.child as Text).data!;
    await tester.tap(sessionFinder);
    await tester.pumpAndSettle();
    await tester.tap(find.text(sessionLabel).last);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Confirm reschedule'));
    await tester.pumpAndSettle();
    final request = requests.singleWhere((request) =>
        request.method == 'POST' && request.url.path.endsWith('/10/reschedule'));
    expect(jsonDecode(request.body), {'doctorTimeSlotId': 13});
    expect(find.text('Appointment rescheduled successfully.'), findsOneWidget);
  });

  testWidgets('appointment API failures show a retryable error state',
      (tester) async {
    await pumpAppointmentsDashboard(tester, failAppointments: true);

    expect(find.text('Unable to load appointments.'), findsOneWidget);
    expect(find.text('Retry'), findsOneWidget);
  });
}

Future<void> pumpAppointmentsDashboard(
  WidgetTester tester, {
  List<http.Request>? requests,
  bool failAppointments = false,
  String title = 'Appointments',
  bool rescheduleFixture = false,
}) async {
  final future = DateTime.now().toUtc().add(const Duration(days: 30));
  final appointments = appointmentJson();
  final slots = slotJson();
  // Keep upcoming fixtures in the future regardless of when the suite runs.
  appointments.first['startAt'] = future.toIso8601String();
  appointments.first['endAt'] = future.add(const Duration(hours: 1)).toIso8601String();
  for (final slot in slots) {
    slot['startAt'] = future.toIso8601String();
    slot['endAt'] = future.add(const Duration(hours: 1)).toIso8601String();
  }
  if (rescheduleFixture) {
    slots.add({...slots.first, 'doctorTimeSlotId': 13});
  }
  SharedPreferences.setMockInitialValues({
    'patient_full_name': 'Amal Perera',
    'patient_email': 'amal.perera@email.com',
  });
  SecureTokenStorage.setTokenForTesting('test-token');
  ApiService.setHttpClientForTesting(MockClient((request) async {
    requests?.add(request);
    if (request.method == 'GET' && request.url.path == '/api/patient/doctors') {
      return jsonResponse([
        {'doctorId': 1, 'fullName': 'Ada Lovelace', 'specialization': 'Cardiology'},
        {'doctorId': 2, 'fullName': 'Alan Turing', 'specialization': 'Cardiology'},
      ]);
    }
    if (failAppointments && request.url.path == '/api/appointment') {
      return jsonResponse({'message': 'Unable to load appointments.'}, 500);
    }

    if (request.method == 'GET' && request.url.path == '/api/patient/me') {
      return jsonResponse(patientJson());
    }

    if (request.method == 'GET' && request.url.path == '/api/appointment') {
      return jsonResponse({'data': appointments});
    }

    if (request.method == 'GET' &&
        request.url.path == '/api/appointment/available-slots') {
      return jsonResponse(slots);
    }

    if (request.method == 'GET' &&
        request.url.path == '/api/appointment/doctors') {
      return jsonResponse([
        {'doctorId': 1, 'doctorName': 'Dr. Ada Lovelace', 'specialty': 'Cardiology'},
        {'doctorId': 2, 'doctorName': 'Dr. Alan Turing', 'specialty': 'Cardiology'},
      ]);
    }

    if (request.method == 'GET' &&
        request.url.path == '/api/appointment/specializations') {
      return jsonResponse(['Cardiology']);
    }

    if (request.method == 'GET' &&
        (request.url.path == '/api/medicalrecord/notifications/patient' ||
         request.url.path == '/api/medicalrecord/notifications' ||
         request.url.path == '/api/appointment/notifications' ||
         request.url.path == '/api/triage-workflows/notifications')) {
      return jsonResponse([]);
    }

    if (request.method == 'POST' && request.url.path == '/api/appointment') {
      return jsonResponse({...appointmentJson().first, 'appointmentId': 99}, 200);
    }

    if (request.method == 'POST' &&
        request.url.path == '/api/appointment/10/reschedule') {
      return jsonResponse({
        ...appointmentJson().first,
        'doctorTimeSlotId': jsonDecode(request.body)['doctorTimeSlotId'],
        'status': 'Confirmed',
      });
    }

    if (request.method == 'POST' &&
        request.url.path == '/api/appointment/10/cancel') {
      return jsonResponse({
        ...appointmentJson().first,
        'status': 'Cancelled',
        'cancellationReason': 'Patient unavailable',
      });
    }

    return jsonResponse({'message': 'Not found'}, 404);
  }));

  await tester.pumpWidget(MaterialApp(
    home: DashboardLayout(title: title),
  ));
  await tester.pumpAndSettle();
}

Future<void> selectBookingSession(WidgetTester tester) async {
  final sessionField = find.byType(DropdownButtonFormField<int>);
  final field = tester.widget<DropdownButton<int>>(find.descendant(
    of: sessionField, matching: find.byType(DropdownButton<int>)));
  expect(field.items!.map((item) => item.value), [11]);
  final label = ((field.items!.first.child as Column).children.first as Text).data!;
  await tester.ensureVisible(sessionField);
  await tester.pumpAndSettle();
  await tester.tap(sessionField);
  await tester.pumpAndSettle();
  await tester.tap(find.text(label).last);
  await tester.pumpAndSettle();
}

Future<void> selectDropdownValue(
    WidgetTester tester, int dropdownIndex, String value) async {
  final dropdown = find.byType(DropdownButtonFormField<String>).at(dropdownIndex);
  await tester.ensureVisible(dropdown);
  await tester.pumpAndSettle();
  await tester.tap(dropdown);
  await tester.pumpAndSettle();
  await tester.tap(find.text(value).last);
  await tester.pumpAndSettle();
}

http.Response jsonResponse(Object body, [int status = 200]) => http.Response(
      jsonEncode(body),
      status,
      headers: {'content-type': 'application/json'},
    );

Map<String, dynamic> patientJson() => {
      'patientId': 1,
      'firstName': 'Amal',
      'lastName': 'Perera',
      'dateOfBirth': '1985-03-14T00:00:00Z',
      'gender': 'Male',
      'nic': '850314123V',
      'phoneNumber': '0771234567',
      'email': 'amal.perera@email.com',
      'address': 'Colombo',
    };

List<Map<String, dynamic>> appointmentJson() => [
      {
        'appointmentId': 10,
        'doctorTimeSlotId': 11,
        'doctorId': 1,
        'patientId': 1,
        'appointmentNumber': 1,
        'patientName': 'Amal Perera',
        'patientPhone': '0771234567',
        'patientEmail': 'amal.perera@email.com',
        'doctorName': 'Dr. Ada Lovelace',
        'specialty': 'Cardiology',
        'roomId': 3,
        'roomNumber': 'C-101',
        'roomName': 'Consultation Room',
        'floor': 'First',
        'startAt': '2026-09-10T08:00:00Z',
        'endAt': '2026-09-10T09:00:00Z',
        'consultationFee': 4500,
        'appointmentType': 'Consultation',
        'reason': 'Checkup',
        'status': 'Confirmed',
        'cancellationReason': '',
        'notes': '',
        'createdAt': '2026-09-01T08:00:00Z',
        'updatedAt': '2026-09-01T08:00:00Z',
      },
      {
        'appointmentId': 20,
        'doctorTimeSlotId': 21,
        'doctorId': 3,
        'patientId': 1,
        'appointmentNumber': 2,
        'patientName': 'Amal Perera',
        'patientPhone': '0771234567',
        'patientEmail': 'amal.perera@email.com',
        'doctorName': 'Dr. Grace Hopper',
        'specialty': 'Neurology',
        'roomId': 4,
        'roomNumber': 'N-101',
        'roomName': 'Neuro Room',
        'floor': 'Second',
        'startAt': '2026-08-01T08:00:00Z',
        'endAt': '2026-08-01T09:00:00Z',
        'consultationFee': 5000,
        'appointmentType': 'Review',
        'reason': 'Review',
        'status': 'Completed',
        'cancellationReason': '',
        'notes': '',
        'createdAt': '2026-07-20T08:00:00Z',
        'updatedAt': '2026-08-01T09:00:00Z',
      },
    ];

List<Map<String, dynamic>> slotJson() => [
      {
        'doctorTimeSlotId': 11,
        'doctorId': 1,
        'doctorName': 'Dr. Ada Lovelace',
        'specialty': 'Cardiology',
        'roomId': 3,
        'roomNumber': 'C-101',
        'roomName': 'Consultation Room',
        'floor': 'First',
        'startAt': '2026-09-10T08:00:00Z',
        'endAt': '2026-09-10T09:00:00Z',
        'capacity': 2,
        'bookedCount': 0,
        'bookedAppointmentNumbers': [],
        'nextAppointmentNumber': 1,
        'isActive': true,
        'consultationFee': 4500,
      },
      {
        'doctorTimeSlotId': 12,
        'doctorId': 2,
        'doctorName': 'Dr. Alan Turing',
        'specialty': 'Cardiology',
        'roomId': 5,
        'roomNumber': 'C-102',
        'roomName': 'Consultation Room 2',
        'floor': 'First',
        'startAt': '2026-09-11T08:00:00Z',
        'endAt': '2026-09-11T09:00:00Z',
        'capacity': 2,
        'bookedCount': 0,
        'bookedAppointmentNumbers': [],
        'nextAppointmentNumber': 1,
        'isActive': true,
        'consultationFee': 4500,
      },
    ];
