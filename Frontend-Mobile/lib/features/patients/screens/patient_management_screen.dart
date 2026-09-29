import 'package:flutter/material.dart';
import 'package:medicore_mobile/core/constants/app_colors.dart';
import 'package:medicore_mobile/core/services/api_service.dart';
import 'package:medicore_mobile/models/patient.dart';

class PatientManagementScreen extends StatefulWidget {
  const PatientManagementScreen({super.key});

  @override
  State<PatientManagementScreen> createState() =>
      _PatientManagementScreenState();
}

class _PatientManagementScreenState extends State<PatientManagementScreen> {
  final _searchController = TextEditingController();
  List<Patient> _patients = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadPatients();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _loadPatients() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final patients =
          await ApiService.getPatients(search: _searchController.text);
      if (mounted) {
        setState(() => _patients = patients);
      }
    } catch (_) {
      if (mounted) {
        setState(() =>
            _error = 'Could not load patients. Check that the API is running.');
      }
    } finally {
      if (mounted) {
        setState(() => _loading = false);
      }
    }
  }

  Future<void> _confirmDelete(Patient patient) async {
    final delete = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Delete patient?'),
        content: Text('This will permanently remove ${patient.fullName}.'),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: const Text('Cancel')),
          FilledButton.tonal(
            onPressed: () => Navigator.pop(context, true),
            style: FilledButton.styleFrom(foregroundColor: AppColors.danger),
            child: const Text('Delete'),
          ),
        ],
      ),
    );
    if (delete != true) return;
    try {
      await ApiService.deletePatient(patient.patientId);
      await _loadPatients();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('Could not delete the patient.')));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return RefreshIndicator(
      onRefresh: _loadPatients,
      child: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          Row(children: [
            Expanded(
              child: TextField(
                controller: _searchController,
                onSubmitted: (_) => _loadPatients(),
                decoration: InputDecoration(
                  hintText: 'Search name, NIC, email or phone',
                  prefixIcon: const Icon(Icons.search_rounded),
                  suffixIcon: IconButton(
                      icon: const Icon(Icons.refresh_rounded),
                      onPressed: _loadPatients),
                ),
              ),
            ),
            const SizedBox(width: 12),
            FilledButton.icon(
              onPressed: () => _openEditor(),
              icon: const Icon(Icons.person_add_alt_1_rounded),
              label: const Text('Add'),
            ),
          ]),
          const SizedBox(height: 20),
          if (_loading)
            const Padding(
                padding: EdgeInsets.all(48),
                child: Center(child: CircularProgressIndicator()))
          else if (_error != null)
            _MessageCard(
                icon: Icons.cloud_off_rounded,
                message: _error!,
                action: _loadPatients)
          else if (_patients.isEmpty)
            const _MessageCard(
                icon: Icons.groups_outlined, message: 'No patients found.')
          else ...[
            Text('${_patients.length} patient records',
                style: Theme.of(context)
                    .textTheme
                    .labelLarge
                    ?.copyWith(color: AppColors.textSecondaryLight)),
            const SizedBox(height: 10),
            ..._patients.map((patient) => _PatientCard(
                  patient: patient,
                  onEdit: () => _openEditor(patient),
                  onDelete: () => _confirmDelete(patient),
                )),
          ],
        ],
      ),
    );
  }

  Future<void> _openEditor([Patient? patient]) async {
    final changed = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (_) => _PatientEditor(patient: patient),
    );
    if (changed == true) _loadPatients();
  }
}

class _PatientCard extends StatelessWidget {
  final Patient patient;
  final VoidCallback onEdit;
  final VoidCallback onDelete;
  const _PatientCard(
      {required this.patient, required this.onEdit, required this.onDelete});

  @override
  Widget build(BuildContext context) {
    final initials =
        '${patient.firstName.isNotEmpty ? patient.firstName[0] : ''}${patient.lastName.isNotEmpty ? patient.lastName[0] : ''}';
    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      child: ListTile(
        contentPadding: const EdgeInsets.fromLTRB(16, 12, 8, 12),
        leading: CircleAvatar(
            backgroundColor: AppColors.primary.withValues(alpha: .14),
            foregroundColor: AppColors.primary,
            child: Text(initials)),
        title: Text(patient.fullName,
            style: const TextStyle(fontWeight: FontWeight.w700)),
        subtitle: Padding(
          padding: const EdgeInsets.only(top: 4),
          child: Text(
              '${patient.gender}  •  ${patient.bloodGroup}  •  ${patient.phoneNumber}\nNIC: ${patient.nic}'),
        ),
        isThreeLine: true,
        trailing: PopupMenuButton<String>(
          onSelected: (value) => value == 'edit' ? onEdit() : onDelete(),
          itemBuilder: (_) => const [
            PopupMenuItem(
                value: 'edit',
                child: ListTile(
                    leading: Icon(Icons.edit_outlined), title: Text('Edit'))),
            PopupMenuItem(
                value: 'delete',
                child: ListTile(
                    leading:
                        Icon(Icons.delete_outline, color: AppColors.danger),
                    title: Text('Delete'))),
          ],
        ),
      ),
    );
  }
}

class _MessageCard extends StatelessWidget {
  final IconData icon;
  final String message;
  final VoidCallback? action;
  const _MessageCard({required this.icon, required this.message, this.action});
  @override
  Widget build(BuildContext context) => Card(
          child: Padding(
        padding: const EdgeInsets.all(28),
        child: Column(children: [
          Icon(icon, size: 38, color: AppColors.textSecondaryLight),
          const SizedBox(height: 10),
          Text(message, textAlign: TextAlign.center),
          if (action != null)
            TextButton(onPressed: action, child: const Text('Try again'))
        ]),
      ));
}

class _PatientEditor extends StatefulWidget {
  final Patient? patient;
  const _PatientEditor({this.patient});
  @override
  State<_PatientEditor> createState() => _PatientEditorState();
}

class _PatientEditorState extends State<_PatientEditor> {
  final _formKey = GlobalKey<FormState>();
  late final Map<String, TextEditingController> _fields;
  late String _gender;
  late String _bloodGroup;
  late DateTime _dob;
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    final p = widget.patient;
    _gender = p?.gender ?? 'Male';
    _bloodGroup = p?.bloodGroup ?? 'O+';
    _dob = p?.dateOfBirth ?? DateTime(2000, 1, 1);
    _fields = {
      for (final entry in {
        'First name': p?.firstName ?? '',
        'Last name': p?.lastName ?? '',
        'NIC': p?.nic ?? '',
        'Phone': p?.phoneNumber ?? '',
        'Email': p?.email ?? '',
        'Address': p?.address ?? '',
        'Emergency contact name': p?.emergencyContactName ?? '',
        'Emergency contact phone': p?.emergencyContactPhone ?? ''
      }.entries)
        entry.key: TextEditingController(text: entry.value)
    };
  }

  @override
  void dispose() {
    for (final c in _fields.values) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _saving = true);
    final data = {
      'firstName': _fields['First name']!.text.trim(),
      'lastName': _fields['Last name']!.text.trim(),
      'dateOfBirth': _dob.toIso8601String(),
      'gender': _gender,
      'nic': _fields['NIC']!.text.trim(),
      'phoneNumber': _fields['Phone']!.text.trim(),
      'email': _fields['Email']!.text.trim(),
      'address': _fields['Address']!.text.trim(),
      'bloodGroup': _bloodGroup,
      'emergencyContactName': _fields['Emergency contact name']!.text.trim(),
      'emergencyContactPhone': _fields['Emergency contact phone']!.text.trim(),
    };
    try {
      await ApiService.savePatient(data, patientId: widget.patient?.patientId);
      if (mounted) {
        Navigator.pop(context, true);
      }
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
            content: Text(
                'Could not save patient. Check required fields and API connection.')));
      }
    } finally {
      if (mounted) {
        setState(() => _saving = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) => Padding(
        padding:
            EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom),
        child: DraggableScrollableSheet(
          expand: false,
          initialChildSize: .9,
          maxChildSize: .96,
          builder: (_, controller) => Material(
            borderRadius: const BorderRadius.vertical(top: Radius.circular(24)),
            child: ListView(
                controller: controller,
                padding: const EdgeInsets.all(20),
                children: [
                  Text(
                      widget.patient == null
                          ? 'Register patient'
                          : 'Edit patient',
                      style: Theme.of(context)
                          .textTheme
                          .headlineSmall
                          ?.copyWith(fontWeight: FontWeight.bold)),
                  const SizedBox(height: 18),
                  Form(
                      key: _formKey,
                      child: Column(children: [
                        _field('First name', required: true),
                        _field('Last name', required: true),
                        _field('NIC', required: true),
                        _field('Phone',
                            required: true, type: TextInputType.phone),
                        _field('Email', type: TextInputType.emailAddress),
                        _field('Address'),
                        ListTile(
                            contentPadding: EdgeInsets.zero,
                            title: const Text('Date of birth'),
                            subtitle: Text(
                                '${_dob.year}-${_dob.month.toString().padLeft(2, '0')}-${_dob.day.toString().padLeft(2, '0')}'),
                            trailing: const Icon(Icons.calendar_today_outlined),
                            onTap: () async {
                              final picked = await showDatePicker(
                                  context: context,
                                  initialDate: _dob,
                                  firstDate: DateTime(1900),
                                  lastDate: DateTime.now());
                              if (picked != null) setState(() => _dob = picked);
                            }),
                        Row(children: [
                          Expanded(
                              child: _dropdown(
                                  'Gender',
                                  _gender,
                                  const ['Male', 'Female', 'Other'],
                                  (v) => setState(() => _gender = v!))),
                          const SizedBox(width: 12),
                          Expanded(
                              child: _dropdown(
                                  'Blood group',
                                  _bloodGroup,
                                  const [
                                    'A+',
                                    'A-',
                                    'B+',
                                    'B-',
                                    'AB+',
                                    'AB-',
                                    'O+',
                                    'O-'
                                  ],
                                  (v) => setState(() => _bloodGroup = v!)))
                        ]),
                        _field('Emergency contact name'),
                        _field('Emergency contact phone',
                            type: TextInputType.phone),
                        const SizedBox(height: 20),
                        SizedBox(
                            width: double.infinity,
                            child: FilledButton(
                                onPressed: _saving ? null : _save,
                                child: Padding(
                                    padding: const EdgeInsets.all(13),
                                    child: _saving
                                        ? const CircularProgressIndicator()
                                        : Text(widget.patient == null
                                            ? 'Register patient'
                                            : 'Save changes')))),
                      ])),
                ]),
          ),
        ),
      );
  Widget _field(String key, {bool required = false, TextInputType? type}) =>
      Padding(
          padding: const EdgeInsets.only(bottom: 12),
          child: TextFormField(
              controller: _fields[key],
              keyboardType: type,
              decoration: InputDecoration(labelText: key),
              validator: required
                  ? (value) => value == null || value.trim().isEmpty
                      ? '$key is required'
                      : null
                  : null));
  Widget _dropdown(String label, String value, List<String> items,
          ValueChanged<String?> change) =>
      DropdownButtonFormField<String>(
          initialValue: value,
          decoration: InputDecoration(labelText: label),
          items: items
              .map((e) => DropdownMenuItem(value: e, child: Text(e)))
              .toList(),
          onChanged: change);
}
