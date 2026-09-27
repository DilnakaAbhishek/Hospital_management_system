import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:medicore_mobile/core/services/api_service.dart';
import 'package:medicore_mobile/core/services/secure_token_storage.dart';
import 'package:medicore_mobile/features/assistant/screens/hospital_assistant_screen.dart';

const capabilities = [
  {
    'id': 'patient-help',
    'label': 'Patient Help',
    'enabled': true,
    'prompt': 'I need help with my symptoms'
  },
  {
    'id': 'find-doctor',
    'label': 'Find Doctor',
    'enabled': true,
    'prompt': 'Find a doctor'
  },
  {
    'id': 'appointments',
    'label': 'Appointments',
    'enabled': true,
    'prompt': 'Show my appointments'
  },
  {
    'id': 'medical-reports',
    'label': 'Medical Reports',
    'enabled': false,
    'prompt': ''
  },
];

Map<String, dynamic> conversation(
        {bool pending = false, bool booked = false}) =>
    {
      'conversationId': '11111111-1111-4111-8111-111111111111',
      'title': 'Find a cardiologist',
      'state': pending ? 'WAITING_FOR_HUMAN_APPROVAL' : 'COMPLETED',
      'messages': [
        {
          'id': '1',
          'role': 'user',
          'text': 'Find a cardiologist',
          'progress': []
        },
        {
          'id': '2',
          'role': 'assistant',
          'text':
              booked ? 'Appointment confirmed.' : 'Please review your options.',
          'progress': []
        },
      ],
      'pendingAction': pending
          ? {
              'actionId': '22222222-2222-4222-8222-222222222222',
              'type': 'book',
              'title': 'Confirm an appointment',
              'description': 'Choose a session to confirm.',
              'expiresAt': DateTime.now()
                  .toUtc()
                  .add(const Duration(minutes: 30))
                  .toIso8601String(),
              'slots': [slot()],
              'appointments': [],
            }
          : null,
      'questions': [],
      'slots': [],
      'doctors': [],
      'appointments': booked
          ? [
              {
                ...slot(),
                'appointmentId': 501,
                'appointmentNumber': 9,
                'status': 'Confirmed'
              }
            ]
          : [],
      'capabilities': capabilities,
    };

Map<String, dynamic> slot() => {
      'doctorTimeSlotId': 11,
      'doctorName': 'Dr. Silva',
      'specialty': 'Cardiology',
      'startAt': '2030-10-08T15:00:00+05:30',
      'endAt': '2030-10-08T17:00:00+05:30',
      'appointmentNumber': 8,
      'consultationFee': 2500,
      'location': 'Room A-01',
    };

http.Response jsonResponse(Object value) =>
    http.Response(jsonEncode(value), 200,
        headers: {'content-type': 'application/json'});

void main() {
  tearDown(() {
    ApiService.resetHttpClientForTesting();
    SecureTokenStorage.resetTokenForTesting();
  });

  Future<void> pump(WidgetTester tester,
      Future<http.Response> Function(http.Request) handler) async {
    await tester.binding.setSurfaceSize(const Size(420, 1000));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    SecureTokenStorage.setTokenForTesting('patient-token');
    ApiService.setHttpClientForTesting(MockClient((request) async {
      expect(request.headers['Authorization'], 'Bearer patient-token');
      if (request.url.path.endsWith('/capabilities')) {
        return jsonResponse(capabilities);
      }
      return handler(request);
    }));
    await tester.pumpWidget(const MaterialApp(home: HospitalAssistantScreen()));
    await tester.pumpAndSettle();
  }

  testWidgets('decline sends a structured action without a medical answer', (tester) async {
    var requests = 0;
    await pump(tester, (request) async {
      requests++;
      final body = jsonDecode(request.body) as Map<String, dynamic>;
      final response = conversation();
      if (requests == 1) {
        response['state'] = 'GATHERING_INFORMATION';
      response['assessmentInputActive'] = true;
        response['questions'] = [
          {'id': 'severity_score', 'prompt': 'How severe is it?', 'type': 'shortText'}
        ];
      } else {
        expect(body['requirementId'], 'severity_score');
        expect(body['requirementState'], 'Declined');
        expect(body['message'], '');
        response['messages'] = [
          {'id': '3', 'role': 'assistant', 'text': 'Your assessment has been sent for clinical review.', 'progress': []}
        ];
      }
      return jsonResponse(response);
    });
    await tester.enterText(find.byType(TextField), 'I have a cough');
    await tester.tap(find.byTooltip('Send message'));
    await tester.pumpAndSettle();
    expect(find.text('How severe is it?'), findsOneWidget);
    await tester.tap(find.text('Prefer not to answer'));
    await tester.pumpAndSettle();
    expect(requests, 2);
    expect(find.text('Prefer not to answer'), findsNothing);
    expect(find.text('Your assessment has been sent for clinical review.'), findsOneWidget);
  });

  testWidgets('persisted result cards remain visible across replies and reviews are separate', (tester) async {
    await pump(tester, (request) async {
      final response = conversation();
      response['messages'] = [
        {'id': '1', 'role': 'user', 'text': 'Earlier doctor question', 'progress': []},
        {'id': '2', 'role': 'assistant', 'text': 'Earlier doctor answer', 'progress': ['Doctor directory checked'],
          'doctors': [{'name': 'Dr. History', 'specialty': 'General Medicine'}]},
        {'id': '3', 'role': 'user', 'text': 'Latest question', 'progress': []},
        {'id': '4', 'role': 'assistant', 'text': 'Latest answer', 'progress': []},
      ];
      response['assessmentInputActive'] = true;
      response['clinicalReviews'] = [{'workflowId': 3, 'status': 'FailedSafely',
        'approvalStatus': 'Pending', 'message': 'Assessment requires review'}];
      return jsonResponse(response);
    });
    await tester.enterText(find.byType(TextField), 'Show my appointments');
    await tester.tap(find.byTooltip('Send message'));
    await tester.pumpAndSettle();
    await tester.scrollUntilVisible(find.text('Earlier doctor question'), -300,
        scrollable: find.byType(Scrollable).first);
    expect(find.text('Earlier doctor answer'), findsOneWidget);
    expect(find.text('Dr. History'), findsOneWidget);
    expect(find.text('Latest answer'), findsOneWidget);
    expect(find.text('Pending clinical reviews / assessment input'), findsOneWidget);
    expect(tester.widget<TextField>(find.byType(TextField)).enabled, isTrue);
    expect(find.text('Doctor directory checked'), findsNothing);
  });

  testWidgets('follow-up question shows saved options and guidance', (tester) async {
    await pump(tester, (request) async {
      final response = conversation();
      response['state'] = 'GATHERING_INFORMATION';
      response['assessmentInputActive'] = true;
      response['questions'] = [
        {
          'id': 'headache_onset',
          'prompt': 'How quickly did the headache reach its peak intensity?',
          'type': 'singleChoice',
          'options': ['Suddenly', 'Gradually'],
          'hint': 'Choose the closest description.',
        }
      ];
      return jsonResponse(response);
    });
    await tester.enterText(find.byType(TextField), 'I have a headache');
    await tester.tap(find.byTooltip('Send message'));
    await tester.pumpAndSettle();
    expect(find.textContaining('Options: Suddenly; Gradually'), findsOneWidget);
    expect(find.textContaining('Choose the closest description.'), findsOneWidget);
  });

  testWidgets('dismissal removes appointment controls and duplicate doctor headings', (tester) async {
    var dismissed = false;
    await pump(tester, (request) async {
      if (request.url.path.endsWith('/actions')) {
        expect(jsonDecode(request.body)['decision'], 'cancel');
        dismissed = true;
      }
      final response = conversation(pending: !dismissed);
      final proposal = conversation(pending: true)['pendingAction'] as Map<String, dynamic>;
      proposal['status'] = dismissed ? 'Dismissed' : 'Pending';
      response['messages'] = [
        {'id': 'proposal', 'role': 'assistant', 'text': 'Choose a time.',
          'doctors': [{'name': 'Dr. Silva', 'specialty': 'Cardiology'}],
          'slots': [slot()], 'proposedAction': proposal},
        if (dismissed) {'id': 'dismissal', 'role': 'assistant', 'text': "Okay, I've cancelled this appointment request."},
      ];
      return jsonResponse(response);
    });
    await tester.enterText(find.byType(TextField), 'Book a cardiologist');
    await tester.tap(find.byTooltip('Send message'));
    await tester.pumpAndSettle();
    expect(find.text('Dr. Silva'), findsOneWidget);
    expect(find.textContaining('Expected appointment number'), findsNothing);
    await tester.ensureVisible(find.text('Cancel request'));
    await tester.tap(find.text('Cancel request'));
    await tester.pumpAndSettle();
    expect(dismissed, isTrue);
    expect(find.byKey(const ValueKey('assistant-option-11')), findsNothing);
    expect(find.text('Confirm appointment'), findsNothing);
    expect(find.text('Dr. Silva'), findsNothing);
    expect(tester.takeException(), isNull);
  });

  for (final status in ['Answered', 'Declined', 'Unknown', 'NotApplicable']) {
    testWidgets('completed $status follow-up retains context with one next question', (tester) async {
      var sent = 0;
      const prompt = 'Has the pain been getting better, worse, or staying the same?';
      const nextPrompt = 'When did it begin?';
      final answer = switch (status) {
        'Declined' => 'Prefer not to answer',
        'Unknown' => "I don't know",
        'NotApplicable' => 'That does not apply to me',
        _ => "It hasn't really changed.",
      };
      await pump(tester, (request) async {
        sent++;
        final response = conversation();
        response['assessmentInputActive'] = true;
        response['questions'] = [{'id': sent == 1 ? 'progression' : 'onset',
          'prompt': sent == 1 ? prompt : nextPrompt, 'type': 'shortText'}];
        response['clinicalReviews'] = [{'workflowId': 1, 'status': 'PendingPatientInput', 'message': nextPrompt}];
        response['messages'] = [
          if (sent > 1) {'id': 'answer', 'role': 'user', 'text': answer,
            'followUpQuestion': {'id': 'progression', 'prompt': prompt, 'type': 'shortText'},
            'followUpState': status},
        ];
        return jsonResponse(response);
      });
      await tester.enterText(find.byType(TextField), 'I have a headache');
      await tester.tap(find.byTooltip('Send message'));
      await tester.pumpAndSettle();
      expect(find.text(prompt), findsOneWidget);
      if (status == 'Declined') {
        await tester.tap(find.text('Prefer not to answer'));
      } else {
        await tester.enterText(find.byType(TextField), answer);
        await tester.tap(find.byTooltip('Send message'));
      }
      await tester.pumpAndSettle();
      expect(find.text(prompt), findsOneWidget);
      expect(find.text(answer), findsWidgets);
      expect(find.text(nextPrompt), findsOneWidget);
      expect(find.widgetWithText(TextButton, 'Prefer not to answer'), findsOneWidget);
      expect(find.byType(TextField), findsOneWidget);
      await tester.tap(find.text('Pending clinical reviews / assessment input'));
      await tester.pumpAndSettle();
      expect(find.text(nextPrompt), findsOneWidget);
      expect(tester.takeException(), isNull);
    });
  }

  testWidgets('connection failure keeps draft and explains server is unreachable', (tester) async {
    await pump(tester, (request) async {
      if (request.method == 'POST') throw http.ClientException('Failed to fetch');
      return jsonResponse([]);
    });
    await tester.enterText(find.byType(TextField), 'What does this mean?');
    await tester.tap(find.byTooltip('Send message'));
    await tester.pumpAndSettle();
    expect(find.textContaining('Cannot reach the hospital server'), findsOneWidget);
    expect(tester.widget<TextField>(find.byType(TextField)).controller!.text,
        'What does this mean?');
  });

  testWidgets(
      'single option is selected automatically; explicit confirmation displays final number',
      (tester) async {
    final posts = <http.Request>[];
    await pump(tester, (request) async {
      if (request.method == 'GET') return jsonResponse([]);
      posts.add(request);
      if (request.url.path.endsWith('/messages')) {
        return jsonResponse(conversation(pending: true));
      }
      return jsonResponse(conversation(booked: true));
    });
    await tester.enterText(find.byType(TextField), 'Find a cardiologist');
    await tester.tap(find.byTooltip('Send message'));
    await tester.pumpAndSettle();
    expect(posts.length, 1);
    expect(posts.single.url.path, '/api/hospital-assistant/messages');
    expect(find.text('Next available booking No: 8'), findsOneWidget);
    expect(tester.widget<FilledButton>(
        find.widgetWithText(FilledButton, 'Confirm appointment')).onPressed,
        isNotNull);
    expect(posts.length, 1);
    await tester.ensureVisible(find.text('Confirm appointment'));
    await tester.tap(find.text('Confirm appointment'));
    await tester.pumpAndSettle();
    expect(posts.length, 2);
    final action = jsonDecode(posts.last.body) as Map<String, dynamic>;
    expect(action['decision'], 'confirm');
    expect(action['doctorTimeSlotId'], 11);
    expect(action.containsKey('appointmentNumber'), isFalse);
    expect(action.containsKey('patientId'), isFalse);
    expect(find.text('Appointment No: 9'), findsOneWidget);
    expect(find.text('Confirm appointment'), findsNothing);
    expect(tester.takeException(), isNull);
  });

  testWidgets(
      'multiple options require a selection before confirmation',
      (tester) async {
    final posts = <http.Request>[];
    await pump(tester, (request) async {
      posts.add(request);
      final response = conversation(pending: true);
      (response['pendingAction'] as Map)['slots'] = [
        slot(), {...slot(), 'doctorTimeSlotId': 12}
      ];
      return jsonResponse(response);
    });
    await tester.enterText(find.byType(TextField), 'Find a cardiologist');
    await tester.tap(find.byTooltip('Send message'));
    await tester.pumpAndSettle();
    final confirm = find.widgetWithText(FilledButton, 'Confirm appointment');
    expect(tester.widget<FilledButton>(confirm).onPressed, isNull);
    await tester.ensureVisible(find.byKey(const ValueKey('assistant-option-12')));
    await tester.tap(find.byKey(const ValueKey('assistant-option-12')));
    await tester.pumpAndSettle();
    expect(tester.widget<FilledButton>(confirm).onPressed, isNotNull);
    expect(posts.length, 1);
  });

  testWidgets(
      'restores pending server approval; future capability stays disabled',
      (tester) async {
    var writes = 0;
    await pump(tester, (request) async {
      if (request.method != 'GET') writes++;
      return jsonResponse(request.url.path.endsWith('/conversations')
          ? [
              {
                'conversationId': conversation()['conversationId'],
                'title': 'Find a cardiologist'
              }
            ]
          : conversation(pending: true));
    });
    await tester.tap(find.byTooltip('Conversation history'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Find a cardiologist'));
    await tester.pumpAndSettle();
    expect(find.text('Confirm appointment'), findsOneWidget);
    final chip = tester.widget<ActionChip>(
        find.widgetWithText(ActionChip, 'Medical Reports · Coming soon'));
    expect(chip.onPressed, isNull);
    expect(writes, 0);
    expect(tester.takeException(), isNull);
  });

  testWidgets(
      'uncertain confirmation is not retried and refresh restores actual result',
      (tester) async {
    var actionWrites = 0;
    await pump(tester, (request) async {
      if (request.url.path.endsWith('/conversations')) {
        return jsonResponse([
          {
            'conversationId': conversation()['conversationId'],
            'title': 'Find a cardiologist'
          }
        ]);
      }
      if (request.method == 'POST') {
        actionWrites++;
        throw http.ClientException('Connection lost after submission');
      }
      return jsonResponse(
          conversation(pending: actionWrites == 0, booked: actionWrites > 0));
    });
    await tester.tap(find.byTooltip('Conversation history'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Find a cardiologist'));
    await tester.pumpAndSettle();
    await tester
        .ensureVisible(find.byKey(const ValueKey('assistant-option-11')));
    await tester.tap(find.byKey(const ValueKey('assistant-option-11')));
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('Confirm appointment'));
    await tester.tap(find.text('Confirm appointment'));
    await tester.pumpAndSettle();
    expect(actionWrites, 1);
    expect(
        tester
            .widget<FilledButton>(
                find.widgetWithText(FilledButton, 'Confirm appointment'))
            .onPressed,
        isNull);
    await tester.tap(find.byTooltip('Refresh conversation'));
    await tester.pumpAndSettle();
    expect(actionWrites, 1);
    expect(find.text('Appointment No: 9'), findsOneWidget);
  });

  testWidgets('sending a prompt is separate from tapping a quick action',
      (tester) async {
    final posts = <http.Request>[];
    await pump(tester, (request) async {
      if (request.method == 'POST') posts.add(request);
      return jsonResponse([]);
    });
    await tester.tap(find.widgetWithText(ActionChip, 'Find Doctor'));
    await tester.pumpAndSettle();
    expect(tester.widget<TextField>(find.byType(TextField)).controller!.text,
        'Find a doctor');
    expect(posts, isEmpty);
  });

  testWidgets('medical reports capability when enabled populates summarize prompt',
      (tester) async {
    final posts = <http.Request>[];
    await tester.binding.setSurfaceSize(const Size(420, 1000));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    SecureTokenStorage.setTokenForTesting('patient-token');
    ApiService.setHttpClientForTesting(MockClient((request) async {
      if (request.url.path.endsWith('/capabilities')) {
        return jsonResponse([
          {
            'id': 'medical-reports',
            'label': 'Medical Reports',
            'enabled': true,
            'prompt': 'Summarize my medical reports'
          }
        ]);
      }
      if (request.method == 'POST') posts.add(request);
      return jsonResponse([]);
    }));
    await tester.pumpWidget(const MaterialApp(home: HospitalAssistantScreen()));
    await tester.pumpAndSettle();

    final chipFinder = find.widgetWithText(ActionChip, 'Medical Reports');
    expect(chipFinder, findsOneWidget);
    await tester.tap(chipFinder);
    await tester.pumpAndSettle();

    expect(
      tester.widget<TextField>(find.byType(TextField)).controller!.text,
      'Summarize my medical reports',
    );
    expect(posts, isEmpty);
  });
}
