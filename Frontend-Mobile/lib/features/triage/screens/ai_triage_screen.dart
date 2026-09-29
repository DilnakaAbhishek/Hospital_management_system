import 'package:flutter/material.dart';
import 'package:medicore_mobile/features/assistant/screens/hospital_assistant_screen.dart';

/// Compatibility entry point; all patient AI requests now use one conversation.
@Deprecated('Use HospitalAssistantScreen')
class AiTriageScreen extends StatelessWidget {
  final bool embedded;
  const AiTriageScreen({super.key, this.embedded = false});

  @override
  Widget build(BuildContext context) => HospitalAssistantScreen(embedded: embedded);
}
