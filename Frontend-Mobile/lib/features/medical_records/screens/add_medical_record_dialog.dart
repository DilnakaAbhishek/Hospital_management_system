import 'dart:async';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:file_picker/file_picker.dart';
import 'package:intl/intl.dart';
import 'package:medicore_mobile/core/constants/app_colors.dart';
import 'package:medicore_mobile/core/services/api_service.dart';
import 'package:medicore_mobile/models/doctor.dart';
import 'package:medicore_mobile/models/patient.dart';
import 'package:medicore_mobile/models/medical_record.dart';

class AddMedicalRecordDialog extends StatefulWidget {
  final VoidCallback onRecordCreated;
  final int? initialPatientId;
  final String? initialPatientName;
  final MedicalRecord? recordToEdit;

  const AddMedicalRecordDialog({
    super.key,
    required this.onRecordCreated,
    this.initialPatientId,
    this.initialPatientName,
    this.recordToEdit,
  });

  @override
  State<AddMedicalRecordDialog> createState() => _AddMedicalRecordDialogState();
}

class _AddMedicalRecordDialogState extends State<AddMedicalRecordDialog> {
  final _formKey = GlobalKey<FormState>();

  final _diagnosisController = TextEditingController();
  final _symptomsController = TextEditingController();
  final _treatmentPlanController = TextEditingController();
  final _prescriptionController = TextEditingController();
  final _labNotesController = TextEditingController();
  final _patientSearchController = TextEditingController();
  final _doctorSearchController = TextEditingController();
  final _manualIdController = TextEditingController();

  DateTime _recordDate = DateTime.now();
  DateTime? _followUpDate;
  String _selectedRecordType = 'Consultation';
  String _selectedStatus = 'Finalized';

  int? _selectedPatientId;
  String? _selectedPatientName;
  int? _selectedDoctorId;
  String? _selectedDoctorName;

  List<Patient> _patients = [];
  List<DoctorSearchResult> _doctors = [];
  bool _loadingDependencies = true;
  bool _loadingPatients = true;
  bool _manualIdEntry = false;
  Timer? _patientSearchDebounce;
  bool _submitting = false;
  String? _errorMessage;

  XFile? _selectedAttachment;
  int? _selectedAttachmentSize;
  final ImagePicker _picker = ImagePicker();

  final List<String> _recordTypes = [
    'Consultation',
    'LabReport',
    'Prescription',
    'DischargeSummary',
    'GeneralNote',
  ];

  final List<String> _statuses = [
    'Finalized',
    'Draft',
    'Archived',
  ];

  @override
  void initState() {
    super.initState();
    if (widget.recordToEdit != null) {
      final r = widget.recordToEdit!;
      _diagnosisController.text = r.diagnosis;
      _symptomsController.text = r.symptoms;
      _treatmentPlanController.text = r.treatmentPlan;
      _prescriptionController.text = r.prescriptionNotes ?? '';
      _labNotesController.text = r.labNotes ?? '';
      _recordDate = r.recordDate;
      _followUpDate = r.followUpDate;
      _selectedRecordType = r.recordType;
      _selectedStatus = r.status;
      _selectedPatientId = r.patientId;
      _selectedPatientName = r.patientName;
      _selectedDoctorId = r.doctorId;
      _selectedDoctorName = r.doctorName;
    } else {
      _selectedPatientId = widget.initialPatientId;
      _selectedPatientName = widget.initialPatientName;
    }
    _loadDependencies();
  }

  @override
  void dispose() {
    _diagnosisController.dispose();
    _symptomsController.dispose();
    _treatmentPlanController.dispose();
    _prescriptionController.dispose();
    _labNotesController.dispose();
    _patientSearchController.dispose();
    _doctorSearchController.dispose();
    _manualIdController.dispose();
    _patientSearchDebounce?.cancel();
    super.dispose();
  }

  Future<void> _loadDependencies() async {
    setState(() {
      _loadingPatients = true;
      _loadingDependencies = true;
    });

    try {
      final patients = await ApiService.getAllPatients(pageSize: 200);
      if (mounted) {
        setState(() {
          _patients = patients;
          if (_selectedPatientId != null && _selectedPatientName == null) {
            final found = _patients.where((p) => p.patientId == _selectedPatientId);
            if (found.isNotEmpty) {
              _selectedPatientName = found.first.fullName;
            }
          }
          _loadingPatients = false;
        });
      }
    } catch (e) {
      debugPrint('Error loading patients: $e');
      if (mounted) {
        setState(() => _loadingPatients = false);
      }
    }

    try {
      final doctors = await ApiService.searchDoctors();
      if (mounted) {
        setState(() {
          _doctors = doctors;
          if (_selectedDoctorId != null && _selectedDoctorName == null) {
            final found = _doctors.where((d) => d.doctorId == _selectedDoctorId);
            if (found.isNotEmpty) {
              _selectedDoctorName = found.first.fullName;
            }
          }
        });
      }
    } catch (e) {
      debugPrint('Error loading doctors: $e');
    }

    if (mounted) {
      setState(() => _loadingDependencies = false);
    }
  }

  List<DoctorSearchResult> get _filteredDoctors {
    final query = _doctorSearchController.text.trim().toLowerCase();
    if (query.isEmpty) return _doctors;
    return _doctors.where((d) {
      final name = d.fullName.toLowerCase();
      final spec = d.specialization.toLowerCase();
      final id = d.doctorId.toString();
      return name.contains(query) || spec.contains(query) || id.contains(query);
    }).toList();
  }

  void _onPatientSearchChanged(String query) {
    setState(() {});
    _patientSearchDebounce?.cancel();
    final trimmed = query.trim();
    if (trimmed.length >= 2) {
      _patientSearchDebounce = Timer(const Duration(milliseconds: 400), () async {
        try {
          final serverPatients = await ApiService.getAllPatients(search: trimmed, pageSize: 50);
          if (!mounted) return;
          setState(() {
            final existingIds = _patients.map((p) => p.patientId).toSet();
            for (final sp in serverPatients) {
              if (!existingIds.contains(sp.patientId)) {
                _patients.add(sp);
              }
            }
          });
        } catch (_) {}
      });
    }
  }

  List<Patient> get _filteredPatients {
    final query = _patientSearchController.text.trim().toLowerCase();
    if (query.isEmpty) return _patients;
    return _patients.where((p) {
      final name = p.fullName.toLowerCase();
      final nic = p.nic.toLowerCase();
      final phone = p.phoneNumber.toLowerCase();
      final id = p.patientId.toString();
      final email = (p.email ?? '').toLowerCase();
      return name.contains(query) ||
          nic.contains(query) ||
          phone.contains(query) ||
          id.contains(query) ||
          email.contains(query);
    }).toList();
  }

  Future<void> _pickAttachment(ImageSource source) async {
    try {
      final picked = await _picker.pickImage(
        source: source,
        imageQuality: 85,
        maxWidth: 1600,
      );
      if (picked != null && mounted) {
        final length = await picked.length();
        setState(() {
          _selectedAttachment = picked;
          _selectedAttachmentSize = length;
        });
      }
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Could not pick image: $e'),
          backgroundColor: AppColors.danger,
        ),
      );
    }
  }

  Future<void> _pickPdfAttachment() async {
    try {
      final result = await FilePicker.pickFiles(
        type: FileType.custom,
        allowedExtensions: ['pdf'],
        withData: true,
      );
      if (result != null && result.files.isNotEmpty && mounted) {
        final file = result.files.single;
        XFile xFile;
        if (file.bytes != null) {
          xFile = XFile.fromData(file.bytes!, name: file.name, mimeType: 'application/pdf');
        } else if (file.path != null && file.path!.isNotEmpty) {
          xFile = XFile(file.path!, name: file.name, mimeType: 'application/pdf');
        } else {
          return;
        }
        setState(() {
          _selectedAttachment = xFile;
          _selectedAttachmentSize = file.size;
        });
      }
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Could not pick PDF file: $e'),
          backgroundColor: AppColors.danger,
        ),
      );
    }
  }

  void _showAttachmentOptions() {
    showModalBottomSheet(
      context: context,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (ctx) => SafeArea(
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 16.0, horizontal: 20.0),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'Attach Medical Document / Image',
                style: TextStyle(fontSize: 17, fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 6),
              const Text(
                'Choose a capture method for your medical report or prescription',
                style: TextStyle(color: Colors.grey, fontSize: 13),
              ),
              const SizedBox(height: 16),
              ListTile(
                leading: Container(
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    color: AppColors.primary.withValues(alpha: 0.1),
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(Icons.camera_alt_rounded, color: AppColors.primary),
                ),
                title: const Text('Take Photo with Camera', style: TextStyle(fontWeight: FontWeight.w600)),
                subtitle: const Text('Capture report or scan directly using camera'),
                onTap: () {
                  Navigator.pop(ctx);
                  _pickAttachment(ImageSource.camera);
                },
              ),
              ListTile(
                leading: Container(
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    color: AppColors.accent.withValues(alpha: 0.1),
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(Icons.photo_library_rounded, color: AppColors.accent),
                ),
                title: const Text('Choose Photos from Gallery', style: TextStyle(fontWeight: FontWeight.w600)),
                subtitle: const Text('Select saved medical image from device'),
                onTap: () {
                  Navigator.pop(ctx);
                  _pickAttachment(ImageSource.gallery);
                },
              ),
              ListTile(
                leading: Container(
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    color: Colors.redAccent.withValues(alpha: 0.1),
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(Icons.picture_as_pdf_rounded, color: Colors.redAccent),
                ),
                title: const Text('Choose PDF Document', style: TextStyle(fontWeight: FontWeight.w600)),
                subtitle: const Text('Select digital lab report or clinical prescription PDF'),
                onTap: () {
                  Navigator.pop(ctx);
                  _pickPdfAttachment();
                },
              ),
            ],
          ),
        ),
      ),
    );
  }

  Future<void> _pickDate({required bool isFollowUp}) async {
    final initialDate = isFollowUp
        ? (_followUpDate ?? DateTime.now().add(const Duration(days: 7)))
        : _recordDate;

    final picked = await showDatePicker(
      context: context,
      initialDate: initialDate,
      firstDate: DateTime(2020),
      lastDate: DateTime(2035),
    );

    if (picked != null && mounted) {
      setState(() {
        if (isFollowUp) {
          _followUpDate = picked;
        } else {
          _recordDate = picked;
        }
      });
    }
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    if (_selectedPatientId == null) {
      setState(() => _errorMessage = 'Please select a patient.');
      return;
    }

    setState(() {
      _submitting = true;
      _errorMessage = null;
    });

    try {
      // Determine field fallbacks based on record type to maintain database integrity
      String finalDiagnosis = _diagnosisController.text.trim();
      String finalSymptoms = _symptomsController.text.trim();
      String finalTreatmentPlan = _treatmentPlanController.text.trim();
      String? finalPrescriptionNotes = _prescriptionController.text.trim();
      String? finalLabNotes = _labNotesController.text.trim();

      if (_selectedRecordType == 'LabReport') {
        if (finalSymptoms.isEmpty) {
          finalSymptoms = 'Laboratory test results and measured findings recorded.';
        }
        if (finalTreatmentPlan.isEmpty) {
          finalTreatmentPlan = 'Parameters compared against reference ranges. Follow clinical correlation.';
        }
      } else if (_selectedRecordType == 'Prescription') {
        if (finalSymptoms.isEmpty) {
          finalSymptoms = 'Prescription issued for management of clinical condition.';
        }
        if (finalTreatmentPlan.isEmpty) {
          finalTreatmentPlan = finalPrescriptionNotes.isNotEmpty
              ? 'Take medications exactly as prescribed: $finalPrescriptionNotes'
              : 'Take medications according to physician dosage instructions.';
        }
      } else if (_selectedRecordType == 'DischargeSummary') {
        if (finalSymptoms.isEmpty) {
          finalSymptoms = 'Patient course of stay and clinical evaluations summarized upon discharge.';
        }
      } else if (_selectedRecordType == 'GeneralNote') {
        if (finalSymptoms.isEmpty) {
          finalSymptoms = 'General clinical observation note.';
        }
        if (finalTreatmentPlan.isEmpty) {
          finalTreatmentPlan = 'Follow routine health advice and report if symptoms change.';
        }
      }

      final data = {
        'patientId': _selectedPatientId,
        if (_selectedDoctorId != null) 'doctorId': _selectedDoctorId,
        'recordDate': _recordDate.toIso8601String(),
        'recordType': _selectedRecordType,
        'diagnosis': finalDiagnosis,
        'symptoms': finalSymptoms,
        'treatmentPlan': finalTreatmentPlan,
        if (finalPrescriptionNotes.isNotEmpty) 'prescriptionNotes': finalPrescriptionNotes,
        if (finalLabNotes.isNotEmpty) 'labNotes': finalLabNotes,
        if (_followUpDate != null) 'followUpDate': _followUpDate!.toIso8601String(),
        'status': _selectedStatus,
      };

      if (widget.recordToEdit != null) {
        final updateData = {
          'recordType': _selectedRecordType,
          'diagnosis': finalDiagnosis,
          'symptoms': finalSymptoms,
          'treatmentPlan': finalTreatmentPlan,
          if (finalPrescriptionNotes.isNotEmpty) 'prescriptionNotes': finalPrescriptionNotes,
          if (finalLabNotes.isNotEmpty) 'labNotes': finalLabNotes,
          if (_followUpDate != null) 'followUpDate': _followUpDate!.toIso8601String(),
          'status': _selectedStatus,
        };

        await ApiService.updateMedicalRecord(widget.recordToEdit!.medicalRecordId, updateData);

        if (_selectedAttachment != null) {
          try {
            final fileName = _selectedAttachment!.name.isNotEmpty
                ? _selectedAttachment!.name
                : 'attachment_${DateTime.now().millisecondsSinceEpoch}.jpg';
            final fileBytes = await _selectedAttachment!.readAsBytes();

            await ApiService.uploadMedicalRecordAttachment(
              widget.recordToEdit!.medicalRecordId,
              fileBytes: fileBytes,
              fileName: fileName,
              fileType: _selectedAttachment!.mimeType ?? (fileName.toLowerCase().endsWith('.pdf') ? 'application/pdf' : 'image/jpeg'),
            );
          } catch (_) {}
        }

        if (!mounted) return;
        Navigator.of(context).pop();
        widget.onRecordCreated();

        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Medical record updated successfully!'),
            backgroundColor: AppColors.success,
            behavior: SnackBarBehavior.floating,
          ),
        );
        return;
      }

      final created = await ApiService.createMedicalRecord(data);

      // If user selected an attachment (PDF or image), upload it immediately
      if (_selectedAttachment != null) {
        try {
          final fileName = _selectedAttachment!.name.isNotEmpty
              ? _selectedAttachment!.name
              : 'attachment_${DateTime.now().millisecondsSinceEpoch}.jpg';
          final fileBytes = await _selectedAttachment!.readAsBytes();

          await ApiService.uploadMedicalRecordAttachment(
            created.medicalRecordId,
            fileBytes: fileBytes,
            fileName: fileName,
            fileType: _selectedAttachment!.mimeType ?? (fileName.toLowerCase().endsWith('.pdf') ? 'application/pdf' : 'image/jpeg'),
          );
        } catch (_) {
          // Record created, attachment upload issue shouldn't block modal dismissal
        }
      }

      if (!mounted) return;
      Navigator.of(context).pop();
      widget.onRecordCreated();

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Medical record created successfully!'),
          backgroundColor: AppColors.success,
          behavior: SnackBarBehavior.floating,
        ),
      );
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _submitting = false;
        _errorMessage = e.toString().replaceAll('Exception: ', '');
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final size = MediaQuery.of(context).size;
    final isDesktop = size.width >= 700;
    final isNarrow = size.width < 620;

    return Dialog(
      backgroundColor: isDark ? AppColors.surfaceDark : Colors.white,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(18)),
      insetPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 24),
      child: Container(
        width: isDesktop ? 680 : size.width,
        constraints: BoxConstraints(maxHeight: size.height * 0.90),
        child: Column(
          children: [
            // Header
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
              decoration: BoxDecoration(
                border: Border(
                  bottom: BorderSide(
                    color: isDark ? AppColors.borderDark : AppColors.borderLight,
                  ),
                ),
              ),
              child: Row(
                children: [
                  Container(
                    padding: const EdgeInsets.all(8),
                    decoration: BoxDecoration(
                      color: AppColors.primary.withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: const Icon(Icons.note_add_rounded, color: AppColors.primary, size: 22),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          widget.recordToEdit != null
                              ? 'Edit Medical Record #${widget.recordToEdit!.medicalRecordId}'
                              : 'Add Medical Record',
                          style: TextStyle(
                            fontSize: 18,
                            fontWeight: FontWeight.bold,
                            color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
                          ),
                        ),
                        const SizedBox(height: 2),
                        Text(
                          widget.recordToEdit != null
                              ? 'Update ${_getTypeSubtitle()}'
                              : 'Create ${_getTypeSubtitle()}',
                          style: const TextStyle(fontSize: 12, color: Colors.grey),
                        ),
                      ],
                    ),
                  ),
                  IconButton(
                    onPressed: () => Navigator.of(context).pop(),
                    icon: const Icon(Icons.close_rounded, size: 20),
                    tooltip: 'Close',
                  ),
                ],
              ),
            ),

            // Form Body
            Expanded(
              child: _loadingDependencies
                  ? const Center(child: CircularProgressIndicator())
                  : SingleChildScrollView(
                      padding: const EdgeInsets.all(20),
                      child: Form(
                        key: _formKey,
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            if (_errorMessage != null) ...[
                              Container(
                                padding: const EdgeInsets.all(12),
                                margin: const EdgeInsets.only(bottom: 16),
                                decoration: BoxDecoration(
                                  color: AppColors.danger.withValues(alpha: 0.1),
                                  borderRadius: BorderRadius.circular(10),
                                  border: Border.all(color: AppColors.danger.withValues(alpha: 0.3)),
                                ),
                                child: Row(
                                  children: [
                                    const Icon(Icons.error_outline_rounded, color: AppColors.danger, size: 20),
                                    const SizedBox(width: 10),
                                    Expanded(
                                      child: Text(
                                        _errorMessage!,
                                        style: const TextStyle(color: AppColors.danger, fontSize: 13),
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            ],

                            // 1. Patient Selection with Search
                            _buildSectionLabel('Select Patient *', isDark),
                            _buildPatientSelector(isDark),
                            const SizedBox(height: 16),

                            // 2. Record Type & Doctor (Responsive layout)
                            if (isNarrow) ...[
                              _buildSectionLabel('Doctor (Optional)', isDark),
                              _buildDoctorSelector(isDark),
                              const SizedBox(height: 14),

                              _buildSectionLabel('Record Type *', isDark),
                              DropdownButtonFormField<String>(
                                isExpanded: true,
                                initialValue: _selectedRecordType,
                                decoration: _inputDecoration(isDark),
                                items: _recordTypes.map((type) {
                                  return DropdownMenuItem<String>(
                                    value: type,
                                    child: Text(
                                      type == 'LabReport'
                                          ? 'Lab Report'
                                          : type == 'DischargeSummary'
                                              ? 'Discharge Summary'
                                              : type == 'GeneralNote'
                                                  ? 'General Note'
                                                  : type,
                                      overflow: TextOverflow.ellipsis,
                                      style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600),
                                    ),
                                  );
                                }).toList(),
                                onChanged: (val) {
                                  if (val != null) {
                                    setState(() => _selectedRecordType = val);
                                  }
                                },
                              ),
                              const SizedBox(height: 14),
                              _buildSectionLabel('Record Date', isDark),
                              InkWell(
                                onTap: () => _pickDate(isFollowUp: false),
                                borderRadius: BorderRadius.circular(10),
                                child: Container(
                                  padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
                                  decoration: BoxDecoration(
                                    color: isDark ? AppColors.surfaceDark : const Color(0xFFF8FAFC),
                                    borderRadius: BorderRadius.circular(10),
                                    border: Border.all(
                                      color: isDark ? AppColors.borderDark : AppColors.borderLight,
                                    ),
                                  ),
                                  child: Row(
                                    children: [
                                      const Icon(Icons.calendar_today_rounded, size: 16, color: AppColors.primary),
                                      const SizedBox(width: 8),
                                      Text(
                                        DateFormat('MMM dd, yyyy').format(_recordDate),
                                        style: const TextStyle(fontSize: 13),
                                      ),
                                    ],
                                  ),
                                ),
                              ),
                              const SizedBox(height: 14),
                              _buildSectionLabel('Status', isDark),
                              DropdownButtonFormField<String>(
                                isExpanded: true,
                                initialValue: _selectedStatus,
                                decoration: _inputDecoration(isDark),
                                items: _statuses.map((s) {
                                  return DropdownMenuItem<String>(
                                    value: s,
                                    child: Text(s, style: const TextStyle(fontSize: 13), overflow: TextOverflow.ellipsis),
                                  );
                                }).toList(),
                                onChanged: (val) {
                                  if (val != null) setState(() => _selectedStatus = val);
                                },
                              ),
                            ] else ...[
                              _buildSectionLabel('Doctor (Optional)', isDark),
                              _buildDoctorSelector(isDark),
                              const SizedBox(height: 16),

                              Row(
                                children: [
                                  // Record Type
                                  Expanded(
                                    child: Column(
                                      crossAxisAlignment: CrossAxisAlignment.start,
                                      children: [
                                        _buildSectionLabel('Record Type *', isDark),
                                        DropdownButtonFormField<String>(
                                          isExpanded: true,
                                          initialValue: _selectedRecordType,
                                          decoration: _inputDecoration(isDark),
                                          items: _recordTypes.map((type) {
                                            return DropdownMenuItem<String>(
                                              value: type,
                                              child: Text(
                                                type == 'LabReport'
                                                    ? 'Lab Report'
                                                    : type == 'DischargeSummary'
                                                        ? 'Discharge Summary'
                                                        : type == 'GeneralNote'
                                                            ? 'General Note'
                                                            : type,
                                                overflow: TextOverflow.ellipsis,
                                                style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600),
                                              ),
                                            );
                                          }).toList(),
                                          onChanged: (val) {
                                            if (val != null) {
                                              setState(() => _selectedRecordType = val);
                                            }
                                          },
                                        ),
                                      ],
                                    ),
                                  ),
                                  const SizedBox(width: 14),
                                  Expanded(
                                    child: Column(
                                      crossAxisAlignment: CrossAxisAlignment.start,
                                      children: [
                                        _buildSectionLabel('Record Date', isDark),
                                        InkWell(
                                          onTap: () => _pickDate(isFollowUp: false),
                                          borderRadius: BorderRadius.circular(10),
                                          child: Container(
                                            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
                                            decoration: BoxDecoration(
                                              color: isDark ? AppColors.surfaceDark : const Color(0xFFF8FAFC),
                                              borderRadius: BorderRadius.circular(10),
                                              border: Border.all(
                                                color: isDark ? AppColors.borderDark : AppColors.borderLight,
                                              ),
                                            ),
                                            child: Row(
                                              children: [
                                                const Icon(Icons.calendar_today_rounded, size: 16, color: AppColors.primary),
                                                const SizedBox(width: 8),
                                                Text(
                                                  DateFormat('MMM dd, yyyy').format(_recordDate),
                                                  style: const TextStyle(fontSize: 13),
                                                ),
                                              ],
                                            ),
                                          ),
                                        ),
                                      ],
                                    ),
                                  ),
                                  const SizedBox(width: 14),
                                  Expanded(
                                    child: Column(
                                      crossAxisAlignment: CrossAxisAlignment.start,
                                      children: [
                                        _buildSectionLabel('Status', isDark),
                                        DropdownButtonFormField<String>(
                                          isExpanded: true,
                                          initialValue: _selectedStatus,
                                          decoration: _inputDecoration(isDark),
                                          items: _statuses.map((s) {
                                            return DropdownMenuItem<String>(
                                              value: s,
                                              child: Text(s, style: const TextStyle(fontSize: 13), overflow: TextOverflow.ellipsis),
                                            );
                                          }).toList(),
                                          onChanged: (val) {
                                            if (val != null) setState(() => _selectedStatus = val);
                                          },
                                        ),
                                      ],
                                    ),
                                  ),
                                ],
                              ),
                            ],
                            const SizedBox(height: 18),

                            // DYNAMIC FIELDS BASED ON RECORD TYPE
                            _buildDynamicFields(isDark),

                            const SizedBox(height: 18),

                            // ATTACHMENT SECTION (CAMERA / GALLERY)
                            _buildAttachmentSection(isDark),

                            const SizedBox(height: 16),

                            // Follow-up Date Row
                            if (_selectedRecordType != 'GeneralNote') ...[
                              _buildSectionLabel(_getFollowUpLabel(), isDark),
                              InkWell(
                                onTap: () => _pickDate(isFollowUp: true),
                                borderRadius: BorderRadius.circular(10),
                                child: Container(
                                  padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
                                  decoration: BoxDecoration(
                                    color: isDark ? AppColors.surfaceDark : const Color(0xFFF8FAFC),
                                    borderRadius: BorderRadius.circular(10),
                                    border: Border.all(
                                      color: isDark ? AppColors.borderDark : AppColors.borderLight,
                                    ),
                                  ),
                                  child: Row(
                                    children: [
                                      const Icon(Icons.event_repeat_rounded, size: 16, color: AppColors.primary),
                                      const SizedBox(width: 8),
                                      Expanded(
                                        child: Text(
                                          _followUpDate != null
                                              ? DateFormat('MMMM dd, yyyy').format(_followUpDate!)
                                              : 'Set recommended follow-up date (optional)',
                                          overflow: TextOverflow.ellipsis,
                                          style: TextStyle(
                                            fontSize: 13,
                                            color: _followUpDate != null ? null : Colors.grey,
                                          ),
                                        ),
                                      ),
                                      if (_followUpDate != null)
                                        GestureDetector(
                                          onTap: () => setState(() => _followUpDate = null),
                                          child: const Icon(Icons.clear, size: 16, color: Colors.grey),
                                        ),
                                    ],
                                  ),
                                ),
                              ),
                            ],
                          ],
                        ),
                      ),
                    ),
            ),

            // Actions Footer
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 14),
              decoration: BoxDecoration(
                border: Border(
                  top: BorderSide(
                    color: isDark ? AppColors.borderDark : AppColors.borderLight,
                  ),
                ),
              ),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.end,
                children: [
                  OutlinedButton(
                    onPressed: _submitting ? null : () => Navigator.of(context).pop(),
                    style: OutlinedButton.styleFrom(
                      padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 12),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                    ),
                    child: const Text('Cancel'),
                  ),
                  const SizedBox(width: 12),
                  ElevatedButton.icon(
                    onPressed: _submitting ? null : _submit,
                    style: ElevatedButton.styleFrom(
                      backgroundColor: AppColors.primary,
                      foregroundColor: Colors.white,
                      padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                    ),
                    icon: _submitting
                        ? const SizedBox(
                            width: 16,
                            height: 16,
                            child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2),
                          )
                        : const Icon(Icons.check_circle_outline_rounded, size: 18),
                    label: Text(
                      _submitting
                          ? 'Saving...'
                          : widget.recordToEdit != null
                              ? 'Update Record'
                              : 'Save Record',
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  String _getTypeSubtitle() {
    switch (_selectedRecordType) {
      case 'LabReport':
        return 'diagnostic lab investigation & report scan';
      case 'Prescription':
        return 'prescription, medication instructions & dosages';
      case 'DischargeSummary':
        return 'inpatient discharge summary & post-care instructions';
      case 'GeneralNote':
        return 'clinical note or patient progress observation';
      default:
        return 'clinical consultation note & diagnosis';
    }
  }

  String _getFollowUpLabel() {
    switch (_selectedRecordType) {
      case 'Prescription':
        return 'Refill / Medication Review Date (Optional)';
      case 'DischargeSummary':
        return 'Follow-up Clinic Review Date (Optional)';
      case 'LabReport':
        return 'Repeat Test / Review Date (Optional)';
      default:
        return 'Next Follow-up Date (Optional)';
    }
  }

  // --- Dynamic Form Fields According to Record Type ---
  Widget _buildDynamicFields(bool isDark) {
    switch (_selectedRecordType) {
      case 'LabReport':
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _buildSectionLabel('Investigation / Test Name *', isDark),
            TextFormField(
              controller: _diagnosisController,
              decoration: _inputDecoration(
                isDark,
                hint: 'e.g. Complete Blood Count (CBC), Lipid Profile, Liver Function Test, ECG...',
                prefixIcon: Icons.science_outlined,
              ),
              validator: (val) => (val == null || val.trim().isEmpty) ? 'Please enter the test or investigation name' : null,
            ),
            const SizedBox(height: 14),

            _buildSectionLabel('Test Findings & Measured Values *', isDark),
            TextFormField(
              controller: _symptomsController,
              maxLines: 3,
              decoration: _inputDecoration(
                isDark,
                hint: 'e.g. Hemoglobin: 13.8 g/dL, WBC: 7.2 x10^3/uL, Platelets: 240,000 /uL, Fasting Blood Sugar: 95 mg/dL...',
              ),
              validator: (val) => (val == null || val.trim().isEmpty) ? 'Please enter test findings or values' : null,
            ),
            const SizedBox(height: 14),

            _buildSectionLabel('Reference Ranges & Clinician Remarks (Optional)', isDark),
            TextFormField(
              controller: _treatmentPlanController,
              maxLines: 2,
              decoration: _inputDecoration(
                isDark,
                hint: 'e.g. Normal reference parameters observed. No signs of infection or acute inflammation.',
              ),
            ),
            const SizedBox(height: 14),

            _buildSectionLabel('Laboratory / Diagnostic Center Details (Optional)', isDark),
            TextFormField(
              controller: _labNotesController,
              decoration: _inputDecoration(
                isDark,
                hint: 'e.g. Central Pathology Laboratory, Sample: Venous Blood, Ref ID: LAB-9021',
                prefixIcon: Icons.domain_rounded,
              ),
            ),
          ],
        );

      case 'Prescription':
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _buildSectionLabel('Medical Condition / Indication *', isDark),
            TextFormField(
              controller: _diagnosisController,
              decoration: _inputDecoration(
                isDark,
                hint: 'e.g. Essential Hypertension, Type 2 Diabetes, Bacterial Sinusitis...',
                prefixIcon: Icons.healing_outlined,
              ),
              validator: (val) => (val == null || val.trim().isEmpty) ? 'Please enter the medical condition' : null,
            ),
            const SizedBox(height: 14),

            _buildSectionLabel('Prescribed Medications & Dosages *', isDark),
            TextFormField(
              controller: _prescriptionController,
              maxLines: 4,
              decoration: _inputDecoration(
                isDark,
                hint: 'e.g.\n1. Metformin 500mg - 1 tablet twice daily with meals\n2. Atorvastatin 20mg - 1 tablet at night\n3. Paracetamol 500mg - 1-2 tablets PRN',
              ),
              validator: (val) => (val == null || val.trim().isEmpty) ? 'Please enter medication details & dosages' : null,
            ),
            const SizedBox(height: 14),

            _buildSectionLabel('Special Instructions, Diet & Precautions (Optional)', isDark),
            TextFormField(
              controller: _treatmentPlanController,
              maxLines: 2,
              decoration: _inputDecoration(
                isDark,
                hint: 'e.g. Take after food. Avoid consuming alcohol. Report any dizziness or skin reactions immediately.',
              ),
            ),
          ],
        );

      case 'DischargeSummary':
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _buildSectionLabel('Final Discharge Diagnosis *', isDark),
            TextFormField(
              controller: _diagnosisController,
              decoration: _inputDecoration(
                isDark,
                hint: 'e.g. Acute Appendicitis (Post-Op Laparoscopic Appendectomy), Recovered',
                prefixIcon: Icons.local_hospital_outlined,
              ),
              validator: (val) => (val == null || val.trim().isEmpty) ? 'Please enter the discharge diagnosis' : null,
            ),
            const SizedBox(height: 14),

            _buildSectionLabel('Hospital Course & Summary of Inpatient Care *', isDark),
            TextFormField(
              controller: _symptomsController,
              maxLines: 3,
              decoration: _inputDecoration(
                isDark,
                hint: 'e.g. Patient admitted via ER with acute RLQ abdominal pain. Underwent laparoscopic surgery on Day 1. Afebrile, vitals stable, surgical site clean and healing.',
              ),
              validator: (val) => (val == null || val.trim().isEmpty) ? 'Please summarize hospital course' : null,
            ),
            const SizedBox(height: 14),

            _buildSectionLabel('Discharge Advice & Post-Discharge Medications *', isDark),
            TextFormField(
              controller: _treatmentPlanController,
              maxLines: 3,
              decoration: _inputDecoration(
                isDark,
                hint: 'e.g. Oral Cefuroxime 500mg BD for 5 days. Daily wound dressing. Avoid heavy lifting for 2 weeks. Suture removal in 7 days.',
              ),
              validator: (val) => (val == null || val.trim().isEmpty) ? 'Please enter discharge advice and medications' : null,
            ),
          ],
        );

      case 'GeneralNote':
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _buildSectionLabel('Note Title / Subject *', isDark),
            TextFormField(
              controller: _diagnosisController,
              decoration: _inputDecoration(
                isDark,
                hint: 'e.g. Dietary Counseling & Lifestyle Plan, Routine Health Review...',
                prefixIcon: Icons.notes_rounded,
              ),
              validator: (val) => (val == null || val.trim().isEmpty) ? 'Please enter a title or subject' : null,
            ),
            const SizedBox(height: 14),

            _buildSectionLabel('Clinical Observations & Note Details *', isDark),
            TextFormField(
              controller: _symptomsController,
              maxLines: 4,
              decoration: _inputDecoration(
                isDark,
                hint: 'e.g. Reviewed blood sugar logs with patient. Advised on low-glycemic dietary options and daily 30-min walking. Patient shows good compliance and motivation.',
              ),
              validator: (val) => (val == null || val.trim().isEmpty) ? 'Please enter note content' : null,
            ),
            const SizedBox(height: 14),

            _buildSectionLabel('Action Plan / Recommendations (Optional)', isDark),
            TextFormField(
              controller: _treatmentPlanController,
              maxLines: 2,
              decoration: _inputDecoration(
                isDark,
                hint: 'e.g. Repeat HbA1c in 3 months. Continue present lifestyle regimen.',
              ),
            ),
          ],
        );

      case 'Consultation':
      default:
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _buildSectionLabel('Clinical Diagnosis *', isDark),
            TextFormField(
              controller: _diagnosisController,
              decoration: _inputDecoration(
                isDark,
                hint: 'e.g. Acute Bronchitis, Type 2 Diabetes Mellitus, Essential Hypertension...',
                prefixIcon: Icons.medical_services_outlined,
              ),
              validator: (val) => (val == null || val.trim().isEmpty) ? 'Please enter a clinical diagnosis' : null,
            ),
            const SizedBox(height: 14),

            _buildSectionLabel('Chief Complaints & Symptoms *', isDark),
            TextFormField(
              controller: _symptomsController,
              maxLines: 2,
              decoration: _inputDecoration(
                isDark,
                hint: 'e.g. Productive cough for 5 days, mild chest tightness, low-grade fever...',
              ),
              validator: (val) => (val == null || val.trim().isEmpty) ? 'Please describe symptoms' : null,
            ),
            const SizedBox(height: 14),

            _buildSectionLabel('Treatment Plan & Clinical Advice *', isDark),
            TextFormField(
              controller: _treatmentPlanController,
              maxLines: 2,
              decoration: _inputDecoration(
                isDark,
                hint: 'e.g. Prescribed bronchodilator inhaler, hydration, warm fluids, steam inhalation...',
              ),
              validator: (val) => (val == null || val.trim().isEmpty) ? 'Please enter a treatment plan' : null,
            ),
            const SizedBox(height: 14),

            _buildSectionLabel('Prescription Notes (Optional)', isDark),
            TextFormField(
              controller: _prescriptionController,
              maxLines: 2,
              decoration: _inputDecoration(
                isDark,
                hint: 'e.g. Salbutamol 100mcg 2 puffs TDS, Paracetamol 500mg PRN...',
              ),
            ),
            const SizedBox(height: 14),

            _buildSectionLabel('Lab / Diagnostic Notes (Optional)', isDark),
            TextFormField(
              controller: _labNotesController,
              maxLines: 2,
              decoration: _inputDecoration(
                isDark,
                hint: 'e.g. Chest X-ray clear, inflammatory markers normal...',
              ),
            ),
          ],
        );
    }
  }

  // --- Patient Selector with Real-time Search ---
  Widget _buildPatientSelector(bool isDark) {
    if (_loadingPatients && _patients.isEmpty) {
      return Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: isDark ? AppColors.surfaceDark : const Color(0xFFF8FAFC),
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: isDark ? AppColors.borderDark : AppColors.borderLight),
        ),
        child: const Row(
          children: [
            SizedBox(
              width: 18,
              height: 18,
              child: CircularProgressIndicator(strokeWidth: 2, color: AppColors.primary),
            ),
            SizedBox(width: 12),
            Text('Loading registered patients...', style: TextStyle(fontSize: 13, color: Colors.grey)),
          ],
        ),
      );
    }

    final selectedPatient = _patients.where((p) => p.patientId == _selectedPatientId).firstOrNull;

    // If manual ID entry mode is toggled
    if (_manualIdEntry) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          TextFormField(
            controller: _manualIdController,
            keyboardType: TextInputType.number,
            decoration: _inputDecoration(
              isDark,
              hint: 'Enter Patient ID number (e.g. 1)',
              prefixIcon: Icons.badge_outlined,
            ),
            onChanged: (val) {
              final id = int.tryParse(val.trim());
              setState(() {
                _selectedPatientId = id;
                final found = _patients.where((p) => p.patientId == id).firstOrNull;
                _selectedPatientName = found?.fullName ?? (id != null ? 'Patient #$id' : null);
              });
            },
          ),
          const SizedBox(height: 6),
          Align(
            alignment: Alignment.centerRight,
            child: TextButton.icon(
              onPressed: () => setState(() => _manualIdEntry = false),
              icon: const Icon(Icons.search_rounded, size: 15),
              label: const Text('Back to patient search', style: TextStyle(fontSize: 12)),
            ),
          ),
        ],
      );
    }

    // If a patient is selected and not searching
    if (_selectedPatientId != null && (_patientSearchController.text.isEmpty || selectedPatient != null)) {
      final name = selectedPatient?.fullName ?? _selectedPatientName ?? 'Patient #$_selectedPatientId';
      final nic = selectedPatient?.nic ?? '';
      final phone = selectedPatient?.phoneNumber ?? '';

      return Container(
        decoration: BoxDecoration(
          color: AppColors.primary.withValues(alpha: 0.08),
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: AppColors.primary.withValues(alpha: 0.35)),
        ),
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
        child: Row(
          children: [
            CircleAvatar(
              radius: 20,
              backgroundColor: AppColors.primary,
              child: Text(
                name.isNotEmpty ? name[0].toUpperCase() : 'P',
                style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold, fontSize: 15),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    name,
                    style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 14),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    'ID: #$_selectedPatientId${nic.isNotEmpty ? ' • NIC: $nic' : ''}${phone.isNotEmpty ? ' • $phone' : ''}',
                    style: TextStyle(
                      color: isDark ? Colors.white70 : const Color(0xFF64748B),
                      fontSize: 12,
                    ),
                  ),
                ],
              ),
            ),
            if (widget.recordToEdit == null)
              OutlinedButton.icon(
                onPressed: () {
                  setState(() {
                    _selectedPatientId = null;
                    _selectedPatientName = null;
                    _patientSearchController.clear();
                  });
                },
                icon: const Icon(Icons.swap_horiz_rounded, size: 16),
                label: const Text('Change', style: TextStyle(fontSize: 12)),
                style: OutlinedButton.styleFrom(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                  visualDensity: VisualDensity.compact,
                ),
              )
            else
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                decoration: BoxDecoration(
                  color: Colors.grey.withValues(alpha: 0.15),
                  borderRadius: BorderRadius.circular(6),
                ),
                child: const Text('Locked', style: TextStyle(fontSize: 11, color: Colors.grey)),
              ),
          ],
        ),
      );
    }

    // Patient Search Box & Interactive List
    return Container(
      decoration: BoxDecoration(
        color: isDark ? AppColors.surfaceDark : const Color(0xFFF8FAFC),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(
          color: _selectedPatientId == null && _errorMessage != null
              ? AppColors.danger
              : (isDark ? AppColors.borderDark : AppColors.borderLight),
        ),
      ),
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Search Field
          TextField(
            controller: _patientSearchController,
            decoration: InputDecoration(
              hintText: 'Search patient by name, NIC, phone, or ID...',
              hintStyle: const TextStyle(color: Colors.grey, fontSize: 13),
              prefixIcon: const Icon(Icons.search_rounded, size: 20, color: AppColors.primary),
              suffixIcon: _patientSearchController.text.isNotEmpty
                  ? IconButton(
                      icon: const Icon(Icons.clear, size: 18),
                      onPressed: () {
                        _patientSearchController.clear();
                        setState(() {});
                      },
                    )
                  : null,
              filled: true,
              fillColor: isDark ? const Color(0xFF1E293B) : Colors.white,
              isDense: true,
              contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(10),
                borderSide: BorderSide(color: isDark ? AppColors.borderDark : AppColors.borderLight),
              ),
              enabledBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(10),
                borderSide: BorderSide(color: isDark ? AppColors.borderDark : AppColors.borderLight),
              ),
              focusedBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(10),
                borderSide: const BorderSide(color: AppColors.primary, width: 1.5),
              ),
            ),
            onChanged: _onPatientSearchChanged,
          ),
          const SizedBox(height: 10),

          // Patients Results List
          if (_filteredPatients.isNotEmpty) ...[
            Text(
              'Select Patient (${_filteredPatients.length} available):',
              style: TextStyle(
                fontSize: 11,
                fontWeight: FontWeight.w600,
                color: isDark ? Colors.white60 : Colors.grey[600],
              ),
            ),
            const SizedBox(height: 6),
            ConstrainedBox(
              constraints: const BoxConstraints(maxHeight: 180),
              child: ClipRRect(
                borderRadius: BorderRadius.circular(8),
                child: ListView.separated(
                  shrinkWrap: true,
                  itemCount: _filteredPatients.length > 15 ? 15 : _filteredPatients.length,
                  separatorBuilder: (_, __) => Divider(
                    height: 1,
                    color: isDark ? AppColors.borderDark : AppColors.borderLight,
                  ),
                  itemBuilder: (context, index) {
                    final p = _filteredPatients[index];
                    final isSelected = p.patientId == _selectedPatientId;
                    return Material(
                      color: isSelected
                          ? AppColors.primary.withValues(alpha: 0.12)
                          : Colors.transparent,
                      child: InkWell(
                        onTap: () {
                          setState(() {
                            _selectedPatientId = p.patientId;
                            _selectedPatientName = p.fullName;
                            _errorMessage = null;
                          });
                        },
                        child: Padding(
                          padding: const EdgeInsets.symmetric(vertical: 8, horizontal: 10),
                          child: Row(
                            children: [
                              CircleAvatar(
                                radius: 14,
                                backgroundColor: isSelected
                                    ? AppColors.primary
                                    : (isDark ? Colors.white12 : const Color(0xFFE2E8F0)),
                                child: Text(
                                  p.firstName.isNotEmpty ? p.firstName[0].toUpperCase() : 'P',
                                  style: TextStyle(
                                    fontSize: 12,
                                    fontWeight: FontWeight.bold,
                                    color: isSelected ? Colors.white : (isDark ? Colors.white70 : Colors.black87),
                                  ),
                                ),
                              ),
                              const SizedBox(width: 10),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      p.fullName,
                                      style: TextStyle(
                                        fontSize: 13,
                                        fontWeight: FontWeight.bold,
                                        color: isSelected ? AppColors.primary : (isDark ? Colors.white : Colors.black87),
                                      ),
                                    ),
                                    Text(
                                      'ID: #${p.patientId} • NIC: ${p.nic}${p.phoneNumber.isNotEmpty ? ' • ${p.phoneNumber}' : ''}',
                                      style: const TextStyle(color: Colors.grey, fontSize: 11),
                                    ),
                                  ],
                                ),
                              ),
                              if (isSelected)
                                const Icon(Icons.check_circle_rounded, color: AppColors.primary, size: 18)
                              else
                                const Icon(Icons.chevron_right_rounded, color: Colors.grey, size: 18),
                            ],
                          ),
                        ),
                      ),
                    );
                  },
                ),
              ),
            ),
          ] else ...[
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 14),
              child: Center(
                child: Column(
                  children: [
                    const Icon(Icons.person_search_rounded, size: 28, color: Colors.grey),
                    const SizedBox(height: 6),
                    Text(
                      _patientSearchController.text.trim().isEmpty
                          ? 'No registered patients available'
                          : 'No patients found matching "${_patientSearchController.text.trim()}"',
                      style: const TextStyle(fontSize: 12, color: Colors.grey),
                    ),
                  ],
                ),
              ),
            ),
          ],

          // Footer link for manual ID entry
          Row(
            mainAxisAlignment: MainAxisAlignment.end,
            children: [
              TextButton(
                onPressed: () => setState(() => _manualIdEntry = true),
                child: const Text('Enter ID manually instead', style: TextStyle(fontSize: 11)),
              ),
            ],
          ),
        ],
      ),
    );
  }

  // --- Doctor Selector with Real-time Search ---
  Widget _buildDoctorSelector(bool isDark) {
    final selectedDoctor = _doctors.where((d) => d.doctorId == _selectedDoctorId).firstOrNull;

    // If a doctor is selected
    if (_selectedDoctorId != null) {
      final name = selectedDoctor?.fullName ?? _selectedDoctorName ?? 'Doctor #$_selectedDoctorId';
      final spec = selectedDoctor?.specialization ?? 'Assigned Clinician';

      return Container(
        decoration: BoxDecoration(
          color: AppColors.primary.withValues(alpha: 0.08),
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: AppColors.primary.withValues(alpha: 0.35)),
        ),
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
        child: Row(
          children: [
            const CircleAvatar(
              radius: 18,
              backgroundColor: AppColors.primary,
              child: Icon(Icons.medical_services_rounded, color: Colors.white, size: 18),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    name,
                    style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 13),
                    overflow: TextOverflow.ellipsis,
                  ),
                  const SizedBox(height: 2),
                  Text(
                    '$spec • ID: #$_selectedDoctorId',
                    style: TextStyle(
                      color: isDark ? Colors.white70 : const Color(0xFF64748B),
                      fontSize: 11,
                    ),
                    overflow: TextOverflow.ellipsis,
                  ),
                ],
              ),
            ),
            TextButton.icon(
              onPressed: () {
                setState(() {
                  _selectedDoctorId = null;
                  _selectedDoctorName = null;
                  _doctorSearchController.clear();
                });
              },
              icon: const Icon(Icons.swap_horiz_rounded, size: 16),
              label: const Text('Change', style: TextStyle(fontSize: 12)),
              style: TextButton.styleFrom(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                visualDensity: VisualDensity.compact,
              ),
            ),
          ],
        ),
      );
    }

    // Doctor Search Box & Interactive List
    return Container(
      decoration: BoxDecoration(
        color: isDark ? AppColors.surfaceDark : const Color(0xFFF8FAFC),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: isDark ? AppColors.borderDark : AppColors.borderLight),
      ),
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          TextField(
            controller: _doctorSearchController,
            decoration: InputDecoration(
              hintText: 'Search doctor by name or specialty...',
              hintStyle: const TextStyle(color: Colors.grey, fontSize: 13),
              prefixIcon: const Icon(Icons.search_rounded, size: 20, color: AppColors.primary),
              suffixIcon: _doctorSearchController.text.isNotEmpty
                  ? IconButton(
                      icon: const Icon(Icons.clear, size: 18),
                      onPressed: () {
                        _doctorSearchController.clear();
                        setState(() {});
                      },
                    )
                  : null,
              filled: true,
              fillColor: isDark ? const Color(0xFF1E293B) : Colors.white,
              isDense: true,
              contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(10),
                borderSide: BorderSide(color: isDark ? AppColors.borderDark : AppColors.borderLight),
              ),
              enabledBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(10),
                borderSide: BorderSide(color: isDark ? AppColors.borderDark : AppColors.borderLight),
              ),
              focusedBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(10),
                borderSide: const BorderSide(color: AppColors.primary, width: 1.5),
              ),
            ),
            onChanged: (val) => setState(() {}),
          ),
          const SizedBox(height: 8),

          // Unassigned / Clinician Option
          Material(
            color: Colors.transparent,
            child: InkWell(
              borderRadius: BorderRadius.circular(8),
              onTap: () {
                setState(() {
                  _selectedDoctorId = null;
                  _selectedDoctorName = null;
                });
              },
              child: Padding(
                padding: const EdgeInsets.symmetric(vertical: 8, horizontal: 10),
                child: Row(
                  children: [
                    CircleAvatar(
                      radius: 14,
                      backgroundColor: isDark ? Colors.white12 : const Color(0xFFE2E8F0),
                      child: Icon(Icons.domain_rounded, size: 14, color: isDark ? Colors.white70 : Colors.black54),
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Text(
                            'Unassigned / General Hospital Record',
                            style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600),
                          ),
                          Text(
                            'Visible to administrators and patient',
                            style: TextStyle(fontSize: 10, color: isDark ? Colors.white54 : Colors.grey[600]),
                          ),
                        ],
                      ),
                    ),
                    if (_selectedDoctorId == null)
                      const Icon(Icons.check_circle_rounded, size: 16, color: AppColors.primary),
                  ],
                ),
              ),
            ),
          ),

          if (_filteredDoctors.isNotEmpty) ...[
            Divider(height: 12, color: isDark ? AppColors.borderDark : AppColors.borderLight),
            Text(
              'Select Doctor (${_filteredDoctors.length} available):',
              style: TextStyle(
                fontSize: 11,
                fontWeight: FontWeight.w600,
                color: isDark ? Colors.white60 : Colors.grey[600],
              ),
            ),
            const SizedBox(height: 6),
            ConstrainedBox(
              constraints: const BoxConstraints(maxHeight: 180),
              child: ClipRRect(
                borderRadius: BorderRadius.circular(8),
                child: ListView.separated(
                  shrinkWrap: true,
                  itemCount: _filteredDoctors.length > 15 ? 15 : _filteredDoctors.length,
                  separatorBuilder: (_, __) => Divider(
                    height: 1,
                    color: isDark ? AppColors.borderDark : AppColors.borderLight,
                  ),
                  itemBuilder: (context, index) {
                    final d = _filteredDoctors[index];
                    final isSelected = d.doctorId == _selectedDoctorId;
                    return Material(
                      color: isSelected
                          ? AppColors.primary.withValues(alpha: 0.12)
                          : Colors.transparent,
                      child: InkWell(
                        onTap: () {
                          setState(() {
                            _selectedDoctorId = d.doctorId;
                            _selectedDoctorName = d.fullName;
                          });
                        },
                        child: Padding(
                          padding: const EdgeInsets.symmetric(vertical: 8, horizontal: 10),
                          child: Row(
                            children: [
                              CircleAvatar(
                                radius: 14,
                                backgroundColor: isSelected
                                    ? AppColors.primary
                                    : (isDark ? Colors.white12 : const Color(0xFFE2E8F0)),
                                child: Text(
                                  d.fullName.isNotEmpty ? d.fullName[0].toUpperCase() : 'D',
                                  style: TextStyle(
                                    color: isSelected ? Colors.white : (isDark ? Colors.white70 : Colors.black87),
                                    fontSize: 11,
                                    fontWeight: FontWeight.bold,
                                  ),
                                ),
                              ),
                              const SizedBox(width: 10),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      d.fullName,
                                      style: TextStyle(
                                        fontSize: 12,
                                        fontWeight: isSelected ? FontWeight.bold : FontWeight.w500,
                                        color: isSelected ? AppColors.primary : null,
                                      ),
                                    ),
                                    Text(
                                      '${d.specialization} • ID: #${d.doctorId}',
                                      style: TextStyle(
                                        fontSize: 11,
                                        color: isDark ? Colors.white54 : Colors.grey[600],
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                              if (isSelected)
                                const Icon(Icons.check_circle_rounded, size: 16, color: AppColors.primary),
                            ],
                          ),
                        ),
                      ),
                    );
                  },
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }

  // --- Attachment Section (Camera or Gallery) ---
  Widget _buildAttachmentSection(bool isDark) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: isDark ? AppColors.surfaceDark : const Color(0xFFF8FAFC),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: isDark ? AppColors.borderDark : AppColors.borderLight),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Row(
                children: [
                  const Icon(Icons.attach_file_rounded, size: 18, color: AppColors.primary),
                  const SizedBox(width: 6),
                  Text(
                    'Attach Medical Scan / Image',
                    style: TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.bold,
                      color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
                    ),
                  ),
                ],
              ),
              OutlinedButton.icon(
                onPressed: _showAttachmentOptions,
                icon: const Icon(Icons.camera_alt_rounded, size: 15),
                label: Text(
                  _selectedAttachment == null ? 'Scan / Browse' : 'Change',
                  style: const TextStyle(fontSize: 12),
                ),
                style: OutlinedButton.styleFrom(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                  visualDensity: VisualDensity.compact,
                ),
              ),
            ],
          ),
          const SizedBox(height: 6),
          const Text(
            'Capture document with camera, choose an image, or upload a PDF report (PDF, JPEG, PNG)',
            style: TextStyle(color: Colors.grey, fontSize: 11),
          ),
          if (_selectedAttachment != null) ...[
            Builder(
              builder: (ctx) {
                final isPdf = _selectedAttachment!.name.toLowerCase().endsWith('.pdf');
                return Padding(
                  padding: const EdgeInsets.only(top: 10),
                  child: Container(
                    padding: const EdgeInsets.all(10),
                    decoration: BoxDecoration(
                      color: isDark ? Colors.black26 : Colors.white,
                      borderRadius: BorderRadius.circular(8),
                      border: Border.all(color: (isPdf ? Colors.redAccent : AppColors.primary).withValues(alpha: 0.3)),
                    ),
                    child: Row(
                      children: [
                        Container(
                          padding: const EdgeInsets.all(8),
                          decoration: BoxDecoration(
                            color: (isPdf ? Colors.redAccent : AppColors.primary).withValues(alpha: 0.1),
                            borderRadius: BorderRadius.circular(6),
                          ),
                          child: Icon(
                            isPdf ? Icons.picture_as_pdf_rounded : Icons.image_outlined,
                            color: isPdf ? Colors.redAccent : AppColors.primary,
                            size: 20,
                          ),
                        ),
                        const SizedBox(width: 10),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                _selectedAttachment!.name,
                                style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 12),
                                overflow: TextOverflow.ellipsis,
                              ),
                              if (_selectedAttachmentSize != null)
                                Text(
                                  '${(_selectedAttachmentSize! / 1024).toStringAsFixed(1)} KB • ${isPdf ? "PDF Document • Ready to upload" : "Ready to upload"}',
                                  style: const TextStyle(color: AppColors.success, fontSize: 11, fontWeight: FontWeight.bold),
                                ),
                            ],
                          ),
                        ),
                        IconButton(
                          icon: const Icon(Icons.delete_outline_rounded, color: AppColors.danger, size: 18),
                          onPressed: () {
                            setState(() {
                              _selectedAttachment = null;
                              _selectedAttachmentSize = null;
                            });
                          },
                          tooltip: 'Remove Attachment',
                        ),
                      ],
                    ),
                  ),
                );
              },
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildSectionLabel(String label, bool isDark) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Text(
        label,
        style: TextStyle(
          fontSize: 12,
          fontWeight: FontWeight.w700,
          color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
        ),
      ),
    );
  }

  InputDecoration _inputDecoration(bool isDark, {String? hint, IconData? prefixIcon}) {
    return InputDecoration(
      hintText: hint,
      hintStyle: const TextStyle(color: Colors.grey, fontSize: 13),
      prefixIcon: prefixIcon != null ? Icon(prefixIcon, size: 18, color: Colors.grey) : null,
      filled: true,
      fillColor: isDark ? AppColors.surfaceDark : const Color(0xFFF8FAFC),
      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      border: OutlineInputBorder(
        borderRadius: BorderRadius.circular(10),
        borderSide: BorderSide(
          color: isDark ? AppColors.borderDark : AppColors.borderLight,
        ),
      ),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(10),
        borderSide: BorderSide(
          color: isDark ? AppColors.borderDark : AppColors.borderLight,
        ),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(10),
        borderSide: const BorderSide(color: AppColors.primary, width: 1.5),
      ),
    );
  }
}
