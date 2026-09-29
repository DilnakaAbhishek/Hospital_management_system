typedef AssistantJson = Map<String, dynamic>;

List<AssistantJson> assistantObjects(dynamic value) => (value as List? ?? [])
    .whereType<Map>()
    .map((item) => Map<String, dynamic>.from(item))
    .toList();

class AssistantCapability {
  final String id;
  final String label;
  final bool enabled;
  final String prompt;

  AssistantCapability.fromJson(AssistantJson json)
      : id = json['id'] as String,
        label = json['label'] as String,
        enabled = json['enabled'] == true,
        prompt = json['prompt'] as String? ?? '';
}

class AssistantMessage {
  final String id;
  final String role;
  final String text;
  final List<String> progress;
  final List<AssistantJson> appointments;
  final List<AssistantJson> slots;
  final List<AssistantJson> doctors;
  final AssistantAction? proposedAction;
  final bool availabilityChecked;
  final AssistantJson? followUpQuestion;
  final String? followUpState;

  AssistantMessage.fromJson(AssistantJson json)
      : id = json['id'].toString(),
        role = json['role'] as String,
        text = json['text'] as String? ?? '',
        progress = List<String>.from(json['progress'] ?? []),
        appointments = assistantObjects(json['appointments']),
        slots = assistantObjects(json['slots']),
        doctors = assistantObjects(json['doctors']),
        proposedAction = json['proposedAction'] is Map
            ? AssistantAction.fromJson(Map<String, dynamic>.from(json['proposedAction'])) : null,
        availabilityChecked = json['availabilityChecked'] as bool? ?? false,
        followUpQuestion = json['followUpQuestion'] is Map
            ? Map<String, dynamic>.from(json['followUpQuestion']) : null,
        followUpState = json['followUpState'] as String?;
}

class AssistantAction {
  final String id;
  final String type;
  final String title;
  final String description;
  final DateTime? expiresAt;
  final List<AssistantJson> slots;
  final List<AssistantJson> appointments;
  final String status;

  AssistantAction.fromJson(AssistantJson json)
      : id = json['actionId'] as String,
        type = json['type'] as String,
        title = json['title'] as String? ?? 'Confirm your request',
        description = json['description'] as String? ?? '',
        expiresAt = DateTime.tryParse(json['expiresAt']?.toString() ?? ''),
        slots = assistantObjects(json['slots']),
        appointments = assistantObjects(json['appointments']),
        status = json['status'] as String? ?? 'Pending';
}

class AssistantConversation {
  final String id;
  final String title;
  final String state;
  final List<AssistantMessage> messages;
  final AssistantAction? pendingAction;
  final List<AssistantJson> questions;
  final List<AssistantJson> appointments;
  final List<AssistantJson> slots;
  final List<AssistantJson> doctors;
  final List<AssistantCapability> capabilities;
  final List<AssistantJson> clinicalReviews;
  final bool availabilityChecked;
  final bool assessmentInputActive;

  AssistantConversation.fromJson(AssistantJson json)
      : id = json['conversationId'] as String,
        title = json['title'] as String? ?? 'Conversation',
        state = json['state'] as String? ?? '',
        messages = assistantObjects(json['messages'])
            .map(AssistantMessage.fromJson)
            .toList(),
        pendingAction = json['pendingAction'] is Map
            ? AssistantAction.fromJson(
                Map<String, dynamic>.from(json['pendingAction']))
            : null,
        questions = assistantObjects(json['questions']),
        appointments = assistantObjects(json['appointments']),
        slots = assistantObjects(json['slots']),
        doctors = assistantObjects(json['doctors']),
        clinicalReviews = assistantObjects(json['clinicalReviews']),
        availabilityChecked = json['availabilityChecked'] as bool? ?? false,
        assessmentInputActive = json['assessmentInputActive'] as bool? ?? false,
        capabilities = assistantObjects(json['capabilities'])
            .map(AssistantCapability.fromJson)
            .toList();
}
