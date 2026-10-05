import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:medicore_mobile/features/auth/screens/register_screen.dart';

void main() {
  testWidgets('RegisterScreen shows 12-digit NIC placeholder and enforces 12-digit validation',
      (WidgetTester tester) async {
    tester.view.physicalSize = const Size(800, 1200);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(const MaterialApp(
      home: RegisterScreen(),
    ));
    await tester.pumpAndSettle();

    // Verify NIC field placeholder
    final nicFieldFinder = find.byWidgetPredicate(
      (widget) =>
          widget is TextField &&
          widget.decoration?.hintText == 'e.g. 199012345678 (12 digits)',
    );
    expect(nicFieldFinder, findsOneWidget);

    // Enter invalid NIC (old format 9 digits + letter)
    await tester.enterText(find.byType(TextFormField).at(0), 'John');
    await tester.enterText(find.byType(TextFormField).at(1), 'Doe');
    await tester.enterText(find.byType(TextFormField).at(2), '951234567V');

    // Tap Continue
    await tester.tap(find.text('Continue'));
    await tester.pumpAndSettle();

    // Verify error message
    expect(find.text('NIC must be a 12-digit number'), findsOneWidget);

    // Enter valid 12-digit NIC
    await tester.enterText(find.byType(TextFormField).at(2), '199512345678');
    await tester.pumpAndSettle();

    // Select Date of Birth by tapping the DoB card
    await tester.tap(find.text('Select Date of Birth'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('OK'));
    await tester.pumpAndSettle();

    // Continue to step 2
    await tester.tap(find.text('Continue'));
    await tester.pumpAndSettle();

    // Verify Phone field placeholder
    final phoneFieldFinder = find.byWidgetPredicate(
      (widget) =>
          widget is TextField &&
          widget.decoration?.hintText == 'e.g. 0771234567 (10 digits)',
    );
    expect(phoneFieldFinder, findsOneWidget);

    // Enter invalid phone (not 10 digits)
    await tester.enterText(find.byType(TextFormField).at(0), '077123'); // 6 digits
    await tester.enterText(find.byType(TextFormField).at(1), 'john@example.com');
    await tester.enterText(find.byType(TextFormField).at(2), 'Colombo, Sri Lanka');

    await tester.tap(find.text('Continue'));
    await tester.pumpAndSettle();

    // Verify phone validation error
    expect(find.text('Phone number must be a 10-digit number'), findsOneWidget);

    // Enter valid 10-digit phone
    await tester.enterText(find.byType(TextFormField).at(0), '0771234567');
    await tester.tap(find.text('Continue'));
    await tester.pumpAndSettle();

    // Now on step 3 (Security)
    expect(find.text('Account Security'), findsOneWidget);
  });
}
