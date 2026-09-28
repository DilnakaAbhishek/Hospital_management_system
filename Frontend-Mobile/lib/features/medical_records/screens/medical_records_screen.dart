import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:file_picker/file_picker.dart';
import 'package:intl/intl.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:medicore_mobile/core/constants/app_colors.dart';
import 'package:medicore_mobile/core/services/api_service.dart';
import 'package:medicore_mobile/features/medical_records/screens/add_medical_record_dialog.dart';
import 'package:medicore_mobile/features/medical_records/screens/medical_record_detail_screen.dart';
import 'package:medicore_mobile/models/doctor.dart';
import 'package:medicore_mobile/models/medical_record.dart';

class MedicalRecordsScreen extends StatefulWidget {
  final bool embedded;

  const MedicalRecordsScreen({super.key, this.embedded = false});

  @override
  State<MedicalRecordsScreen> createState() => _MedicalRecordsScreenState();
}

class _MedicalRecordsScreenState extends State<MedicalRecordsScreen> {
  List<MedicalRecord> _records = [];
  bool _loading = true;
  String? _error;
  String _searchQuery = '';
  String _selectedType = 'All';
  String _selectedStatus = 'All';
  DateTimeRange? _dateRange;
  bool _isAdmin = false;
  bool _isAdminOrDoctor = false;
  String _currentPatientEmail = '';
  int _currentPatientId = 0;
  MedicalRecordSummary? _summary;
  final ImagePicker _picker = ImagePicker();
  final TextEditingController _searchController = TextEditingController();

  final List<String> _types = [
    'All',
    'Consultation',
    'LabReport',
    'Prescription',
    'DischargeSummary',
    'GeneralNote',
  ];

  final List<String> _statuses = [
    'All',
    'Finalized',
    'Draft',
    'Archived',
  ];

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  @override
  void initState() {
    super.initState();
    _checkRoleAndFetch();
  }

  Future<void> _checkRoleAndFetch() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final role = prefs.getString('user_role');
      final email = (prefs.getString('patient_email') ?? '').toLowerCase().trim();
      final patientId = prefs.getInt('patient_id') ?? prefs.getInt('patient_user_id') ?? 0;

      final isAdmin = role == 'Admin' || email.contains('admin');
      final isDoctor = role == 'Doctor' || email.contains('doctor');
      final isAdminOrDoctor = isAdmin || isDoctor;

      if (mounted) {
        setState(() {
          _isAdmin = isAdmin;
          _isAdminOrDoctor = isAdminOrDoctor;
          _currentPatientEmail = email;
          _currentPatientId = patientId;
        });
      }
    } catch (_) {}

    await _fetchRecords();
  }

  Future<void> _fetchRecords() async {
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final List<MedicalRecord> data;
      if (!_isAdminOrDoctor) {
        // Patient dashboard: strictly fetch their own records
        data = await ApiService.getMyMedicalRecords();
      } else {
        // Doctor / Admin: fetch all medical records
        data = await ApiService.getMedicalRecords();
        ApiService.getMedicalRecordSummary().then((sum) {
          if (mounted) setState(() => _summary = sum);
        }).catchError((_) {});
      }

      if (!mounted) return;
      setState(() {
        _records = data;
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = 'Unable to load medical records. Please pull down or click try again.';
        _loading = false;
      });
    }
  }

  void _openAddRecordDialog() {
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => AddMedicalRecordDialog(
        onRecordCreated: _fetchRecords,
      ),
    );
  }

  void _openEditRecordDialog(MedicalRecord record) {
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => AddMedicalRecordDialog(
        recordToEdit: record,
        onRecordCreated: _fetchRecords,
      ),
    );
  }

  void _confirmDeleteRecord(MedicalRecord record) {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('Delete Record #${record.medicalRecordId}?'),
        content: Text(
          'Are you sure you want to permanently delete this medical record for ${record.patientName.isNotEmpty ? record.patientName : "this patient"}? This action cannot be undone.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('Cancel'),
          ),
          ElevatedButton(
            style: ElevatedButton.styleFrom(
              backgroundColor: AppColors.danger,
              foregroundColor: Colors.white,
            ),
            onPressed: () async {
              Navigator.pop(ctx);
              try {
                await ApiService.deleteMedicalRecord(record.medicalRecordId);
                _fetchRecords();
                if (!mounted) return;
                ScaffoldMessenger.of(context).showSnackBar(
                  const SnackBar(
                    content: Text('Record deleted successfully.'),
                    backgroundColor: AppColors.success,
                    behavior: SnackBarBehavior.floating,
                  ),
                );
              } catch (e) {
                if (!mounted) return;
                ScaffoldMessenger.of(context).showSnackBar(
                  SnackBar(
                    content: Text('Failed to delete record: $e'),
                    backgroundColor: AppColors.danger,
                    behavior: SnackBarBehavior.floating,
                  ),
                );
              }
            },
            child: const Text('Delete'),
          ),
        ],
      ),
    );
  }

  bool get _hasActiveFilters =>
      _searchQuery.isNotEmpty ||
      _selectedType != 'All' ||
      _selectedStatus != 'All' ||
      _dateRange != null;

  void _clearFilters() {
    _searchController.clear();
    setState(() {
      _searchQuery = '';
      _selectedType = 'All';
      _selectedStatus = 'All';
      _dateRange = null;
    });
  }

  void _showPatientUploadOptions() {
    showModalBottomSheet(
      context: context,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(22)),
      ),
      builder: (ctx) => SafeArea(
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 20.0, horizontal: 20.0),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Container(
                    padding: const EdgeInsets.all(8),
                    decoration: BoxDecoration(
                      color: AppColors.primary.withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: const Icon(Icons.document_scanner_rounded, color: AppColors.primary, size: 22),
                  ),
                  const SizedBox(width: 12),
                  const Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Upload Medical Report or Scan',
                          style: TextStyle(fontSize: 17, fontWeight: FontWeight.bold),
                        ),
                        SizedBox(height: 2),
                        Text(
                          'Select capture method to add paper report or scan',
                          style: TextStyle(color: Colors.grey, fontSize: 12),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 18),
              ListTile(
                contentPadding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                leading: Container(
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: AppColors.primary.withValues(alpha: 0.1),
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(Icons.camera_alt_rounded, color: AppColors.primary, size: 22),
                ),
                title: const Text('Take Photo with Camera', style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14)),
                subtitle: const Text('Directly scan paper report, prescription or diagnostic test'),
                onTap: () {
                  Navigator.pop(ctx);
                  _handlePatientUpload(ImageSource.camera);
                },
              ),
              const Divider(height: 1),
              ListTile(
                contentPadding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                leading: Container(
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: const Color(0xFF0891B2).withValues(alpha: 0.1),
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(Icons.photo_library_rounded, color: Color(0xFF0891B2), size: 22),
                ),
                title: const Text('Choose Photos from Gallery', style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14)),
                subtitle: const Text('Pick existing image, diagnostic photo, or report from phone'),
                onTap: () {
                  Navigator.pop(ctx);
                  _handlePatientUpload(ImageSource.gallery);
                },
              ),
              const Divider(height: 1),
              ListTile(
                contentPadding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                leading: Container(
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: Colors.redAccent.withValues(alpha: 0.1),
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(Icons.picture_as_pdf_rounded, color: Colors.redAccent, size: 22),
                ),
                title: const Text('Upload PDF Document / Lab Report', style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14)),
                subtitle: const Text('Select downloaded or scanned PDF files from device storage'),
                onTap: () {
                  Navigator.pop(ctx);
                  _handlePatientPdfUpload();
                },
              ),
            ],
          ),
        ),
      ),
    );
  }

  Future<void> _handlePatientPdfUpload() async {
    try {
      final result = await FilePicker.pickFiles(
        type: FileType.custom,
        allowedExtensions: ['pdf'],
        withData: true,
      );
      if (result == null || result.files.isEmpty || !mounted) return;
      final file = result.files.single;
      XFile xFile;
      if (file.bytes != null) {
        xFile = XFile.fromData(file.bytes!, name: file.name, mimeType: 'application/pdf');
      } else if (file.path != null && file.path!.isNotEmpty) {
        xFile = XFile(file.path!, name: file.name, mimeType: 'application/pdf');
      } else {
        return;
      }
      final fileSize = file.size;
      _showQuickUploadSheet(xFile, fileSize, isPdf: true);
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Could not access PDF file: $e'),
          backgroundColor: AppColors.danger,
        ),
      );
    }
  }

  Future<void> _handlePatientUpload(ImageSource source) async {
    try {
      final picked = await _picker.pickImage(
        source: source,
        imageQuality: 85,
        maxWidth: 1600,
      );
      if (picked == null || !mounted) return;

      final fileSize = await picked.length();
      _showQuickUploadSheet(picked, fileSize, isPdf: false);
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Could not access image: $e'),
          backgroundColor: AppColors.danger,
        ),
      );
    }
  }

  void _showQuickUploadSheet(XFile pickedFile, int fileSize, {bool isPdf = false}) {
    final isPdfFile = isPdf || pickedFile.name.toLowerCase().endsWith('.pdf');
    final defaultTitle = isPdfFile
        ? (pickedFile.name.replaceAll(RegExp(r'\.pdf$', caseSensitive: false), ''))
        : 'Medical Scan Report';
    final titleController = TextEditingController(text: defaultTitle);
    final notesController = TextEditingController();
    final doctorSearchController = TextEditingController();
    String uploadType = isPdfFile ? 'LabReport' : 'GeneralNote';
    bool isSaving = false;
    int? selectedDoctorId;
    String? selectedDoctorName;
    List<DoctorSearchResult> doctorsList = [];
    bool loadingDoctors = true;

    ApiService.searchDoctors().then((docs) {
      doctorsList = docs;
      loadingDoctors = false;
    }).catchError((_) {
      loadingDoctors = false;
    });

    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: Theme.of(context).brightness == Brightness.dark ? AppColors.surfaceDark : Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(22)),
      ),
      builder: (modalCtx) => StatefulBuilder(
        builder: (ctx, setModalState) {
          final isDark = Theme.of(ctx).brightness == Brightness.dark;

          return Padding(
            padding: EdgeInsets.only(
              bottom: MediaQuery.of(ctx).viewInsets.bottom + 20,
              left: 20,
              right: 20,
              top: 20,
            ),
            child: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      const Text(
                        'Upload Medical Record Scan',
                        style: TextStyle(fontSize: 17, fontWeight: FontWeight.bold),
                      ),
                      IconButton(
                        icon: const Icon(Icons.close, size: 20),
                        onPressed: () => Navigator.pop(modalCtx),
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),

                  // Picked File Card
                  Container(
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: (isPdfFile ? Colors.redAccent : AppColors.primary).withValues(alpha: 0.08),
                      borderRadius: BorderRadius.circular(12),
                      border: Border.all(color: (isPdfFile ? Colors.redAccent : AppColors.primary).withValues(alpha: 0.25)),
                    ),
                    child: Row(
                      children: [
                        Container(
                          padding: const EdgeInsets.all(8),
                          decoration: BoxDecoration(
                            color: (isPdfFile ? Colors.redAccent : AppColors.primary).withValues(alpha: 0.15),
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: Icon(
                            isPdfFile ? Icons.picture_as_pdf_rounded : Icons.image_outlined,
                            color: isPdfFile ? Colors.redAccent : AppColors.primary,
                            size: 24,
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                pickedFile.name,
                                style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 13),
                                overflow: TextOverflow.ellipsis,
                              ),
                              Text(
                                '${(fileSize / 1024).toStringAsFixed(1)} KB • ${isPdfFile ? "PDF Document" : "Image captured"}',
                                style: const TextStyle(color: Colors.grey, fontSize: 11),
                              ),
                            ],
                          ),
                        ),
                        const Icon(Icons.check_circle, color: AppColors.success, size: 20),
                      ],
                    ),
                  ),
                  const SizedBox(height: 16),

                  // Record Type
                  const Text('Record Type', style: TextStyle(fontSize: 12, fontWeight: FontWeight.bold)),
                  const SizedBox(height: 6),
                  DropdownButtonFormField<String>(
                    isExpanded: true,
                    initialValue: uploadType,
                    decoration: InputDecoration(
                      filled: true,
                      fillColor: isDark ? Colors.black26 : const Color(0xFFF8FAFC),
                      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
                      border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                    ),
                    items: const [
                      DropdownMenuItem(value: 'LabReport', child: Text('Lab Report / Test Results', overflow: TextOverflow.ellipsis)),
                      DropdownMenuItem(value: 'GeneralNote', child: Text('Medical Scan / General Note', overflow: TextOverflow.ellipsis)),
                      DropdownMenuItem(value: 'Prescription', child: Text('Prescription / Medication', overflow: TextOverflow.ellipsis)),
                      DropdownMenuItem(value: 'Consultation', child: Text('Consultation Summary', overflow: TextOverflow.ellipsis)),
                    ],
                    onChanged: (val) {
                      if (val != null) {
                        setModalState(() => uploadType = val);
                      }
                    },
                  ),
                  const SizedBox(height: 14),

                  // Doctor / Clinician Selector (Search & Select)
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      const Text(
                        'Assigned Doctor (Optional)',
                        style: TextStyle(fontSize: 12, fontWeight: FontWeight.bold),
                      ),
                      if (selectedDoctorId != null)
                        GestureDetector(
                          onTap: () {
                            setModalState(() {
                              selectedDoctorId = null;
                              selectedDoctorName = null;
                              doctorSearchController.clear();
                            });
                          },
                          child: const Text(
                            'Clear',
                            style: TextStyle(fontSize: 12, color: AppColors.primary, fontWeight: FontWeight.w600),
                          ),
                        ),
                    ],
                  ),
                  const SizedBox(height: 6),
                  if (selectedDoctorId != null) ...[
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                      decoration: BoxDecoration(
                        color: AppColors.primary.withValues(alpha: 0.08),
                        borderRadius: BorderRadius.circular(10),
                        border: Border.all(color: AppColors.primary.withValues(alpha: 0.3)),
                      ),
                      child: Row(
                        children: [
                          const CircleAvatar(
                            radius: 16,
                            backgroundColor: AppColors.primary,
                            child: Icon(Icons.medical_services_rounded, color: Colors.white, size: 16),
                          ),
                          const SizedBox(width: 10),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  selectedDoctorName ?? 'Doctor #$selectedDoctorId',
                                  style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 13),
                                  overflow: TextOverflow.ellipsis,
                                ),
                                Text(
                                  'Only visible in this clinician\'s dashboard',
                                  style: TextStyle(color: isDark ? Colors.white70 : const Color(0xFF64748B), fontSize: 11),
                                ),
                              ],
                            ),
                          ),
                          TextButton(
                            onPressed: () {
                              setModalState(() {
                                selectedDoctorId = null;
                                selectedDoctorName = null;
                                doctorSearchController.clear();
                              });
                            },
                            child: const Text('Change', style: TextStyle(fontSize: 12)),
                          ),
                        ],
                      ),
                    ),
                  ] else ...[
                    Container(
                      padding: const EdgeInsets.all(10),
                      decoration: BoxDecoration(
                        color: isDark ? const Color(0xFF1E293B) : const Color(0xFFF8FAFC),
                        borderRadius: BorderRadius.circular(10),
                        border: Border.all(color: isDark ? AppColors.borderDark : AppColors.borderLight),
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          TextField(
                            controller: doctorSearchController,
                            decoration: InputDecoration(
                              hintText: 'Search doctor by name or specialty...',
                              hintStyle: const TextStyle(color: Colors.grey, fontSize: 12),
                              prefixIcon: const Icon(Icons.search_rounded, size: 18, color: AppColors.primary),
                              suffixIcon: doctorSearchController.text.isNotEmpty
                                  ? IconButton(
                                      icon: const Icon(Icons.clear, size: 16),
                                      onPressed: () {
                                        doctorSearchController.clear();
                                        setModalState(() {});
                                      },
                                    )
                                  : null,
                              filled: true,
                              fillColor: isDark ? Colors.black26 : Colors.white,
                              isDense: true,
                              contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                              border: OutlineInputBorder(borderRadius: BorderRadius.circular(8)),
                            ),
                            onChanged: (_) => setModalState(() {}),
                          ),
                          const SizedBox(height: 6),
                          InkWell(
                            onTap: () {
                              setModalState(() {
                                selectedDoctorId = null;
                                selectedDoctorName = null;
                              });
                            },
                            borderRadius: BorderRadius.circular(6),
                            child: Padding(
                              padding: const EdgeInsets.symmetric(vertical: 6, horizontal: 8),
                              child: Row(
                                children: [
                                  CircleAvatar(
                                    radius: 12,
                                    backgroundColor: isDark ? Colors.white12 : const Color(0xFFE2E8F0),
                                    child: Icon(Icons.domain_rounded, size: 13, color: isDark ? Colors.white70 : Colors.black54),
                                  ),
                                  const SizedBox(width: 8),
                                  Expanded(
                                    child: Text(
                                      'Unassigned / General Hospital Record',
                                      style: TextStyle(
                                        fontSize: 12,
                                        fontWeight: selectedDoctorId == null ? FontWeight.bold : FontWeight.normal,
                                        color: selectedDoctorId == null ? AppColors.primary : null,
                                      ),
                                    ),
                                  ),
                                  if (selectedDoctorId == null)
                                    const Icon(Icons.check_circle_rounded, size: 16, color: AppColors.primary),
                                ],
                              ),
                            ),
                          ),
                          Builder(builder: (_) {
                            final q = doctorSearchController.text.trim().toLowerCase();
                            final filtered = q.isEmpty
                                ? doctorsList
                                : doctorsList.where((d) {
                                    final name = d.fullName.toLowerCase();
                                    final spec = d.specialization.toLowerCase();
                                    final id = d.doctorId.toString();
                                    return name.contains(q) || spec.contains(q) || id.contains(q);
                                  }).toList();

                            if (filtered.isEmpty) {
                              return Padding(
                                padding: const EdgeInsets.symmetric(vertical: 8),
                                child: Center(
                                  child: Text(
                                    loadingDoctors
                                        ? 'Loading doctors...'
                                        : 'No doctors found matching "${doctorSearchController.text}"',
                                    style: const TextStyle(fontSize: 11, color: Colors.grey),
                                  ),
                                ),
                              );
                            }

                            return ConstrainedBox(
                              constraints: const BoxConstraints(maxHeight: 140),
                              child: ClipRRect(
                                borderRadius: BorderRadius.circular(6),
                                child: ListView.separated(
                                  shrinkWrap: true,
                                  itemCount: filtered.length > 10 ? 10 : filtered.length,
                                  separatorBuilder: (_, __) => Divider(height: 1, color: isDark ? AppColors.borderDark : AppColors.borderLight),
                                  itemBuilder: (ctx, i) {
                                    final d = filtered[i];
                                    final isSelected = d.doctorId == selectedDoctorId;
                                    return InkWell(
                                      onTap: () {
                                        setModalState(() {
                                          selectedDoctorId = d.doctorId;
                                          selectedDoctorName = '${d.fullName} (${d.specialization})';
                                        });
                                      },
                                      child: Padding(
                                        padding: const EdgeInsets.symmetric(vertical: 6, horizontal: 8),
                                        child: Row(
                                          children: [
                                            CircleAvatar(
                                              radius: 12,
                                              backgroundColor: isSelected ? AppColors.primary : (isDark ? Colors.white12 : const Color(0xFFE2E8F0)),
                                              child: Text(
                                                d.fullName.isNotEmpty ? d.fullName[0].toUpperCase() : 'D',
                                                style: TextStyle(
                                                  color: isSelected ? Colors.white : (isDark ? Colors.white70 : Colors.black87),
                                                  fontSize: 10,
                                                  fontWeight: FontWeight.bold,
                                                ),
                                              ),
                                            ),
                                            const SizedBox(width: 8),
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
                                                    style: TextStyle(fontSize: 10, color: isDark ? Colors.white54 : Colors.grey[600]),
                                                  ),
                                                ],
                                              ),
                                            ),
                                            if (isSelected)
                                              const Icon(Icons.check_circle_rounded, size: 15, color: AppColors.primary),
                                          ],
                                        ),
                                      ),
                                    );
                                  },
                                ),
                              ),
                            );
                          }),
                        ],
                      ),
                    ),
                  ],
                  const SizedBox(height: 14),

                  // Document / Test Title
                  const Text('Report / Document Title *', style: TextStyle(fontSize: 12, fontWeight: FontWeight.bold)),
                  const SizedBox(height: 6),
                  TextField(
                    controller: titleController,
                    decoration: InputDecoration(
                      hintText: 'e.g. CBC Blood Test, Chest X-Ray, Clinic Prescription...',
                      filled: true,
                      fillColor: isDark ? Colors.black26 : const Color(0xFFF8FAFC),
                      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
                      border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                    ),
                  ),
                  const SizedBox(height: 14),

                  // Notes / Findings
                  const Text('Notes / Description (Optional)', style: TextStyle(fontSize: 12, fontWeight: FontWeight.bold)),
                  const SizedBox(height: 6),
                  TextField(
                    controller: notesController,
                    maxLines: 2,
                    decoration: InputDecoration(
                      hintText: 'e.g. Scanned copy of blood report from laboratory...',
                      filled: true,
                      fillColor: isDark ? Colors.black26 : const Color(0xFFF8FAFC),
                      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
                      border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                    ),
                  ),
                  const SizedBox(height: 20),

                  // Submit Button
                  SizedBox(
                    width: double.infinity,
                    height: 48,
                    child: ElevatedButton.icon(
                      onPressed: isSaving
                          ? null
                          : () async {
                              final title = titleController.text.trim();
                              if (title.isEmpty) {
                                ScaffoldMessenger.of(context).showSnackBar(
                                  const SnackBar(content: Text('Please enter a document title.')),
                                );
                                return;
                              }

                              setModalState(() => isSaving = true);

                              try {
                                int resolvedPatientId = _currentPatientId;
                                try {
                                  final profile = await ApiService.getMyPatientProfile();
                                  if (profile.patientId > 0) {
                                    resolvedPatientId = profile.patientId;
                                  }
                                } catch (_) {}

                                final data = {
                                  'patientId': resolvedPatientId,
                                  if (selectedDoctorId != null) 'doctorId': selectedDoctorId,
                                  'recordDate': DateTime.now().toIso8601String(),
                                  'recordType': uploadType,
                                  'diagnosis': title,
                                  'symptoms': notesController.text.trim().isNotEmpty
                                      ? notesController.text.trim()
                                      : 'Medical scan report uploaded by patient.',
                                  'treatmentPlan': 'Uploaded for clinical review and archival.',
                                  'status': 'Finalized',
                                };

                                final created = await ApiService.createMedicalRecord(data);

                                final fileBytes = await pickedFile.readAsBytes();
                                await ApiService.uploadMedicalRecordAttachment(
                                  created.medicalRecordId,
                                  fileBytes: fileBytes,
                                  fileName: pickedFile.name,
                                  fileType: isPdfFile ? 'application/pdf' : (pickedFile.mimeType ?? 'image/jpeg'),
                                );

                                if (!mounted || !modalCtx.mounted) return;
                                Navigator.pop(modalCtx);
                                _fetchRecords();

                                ScaffoldMessenger.of(context).showSnackBar(
                                  const SnackBar(
                                    content: Text('Report scan uploaded and saved successfully!'),
                                    backgroundColor: AppColors.success,
                                    behavior: SnackBarBehavior.floating,
                                  ),
                                );
                              } catch (err) {
                                setModalState(() => isSaving = false);
                                if (!mounted) return;
                                ScaffoldMessenger.of(context).showSnackBar(
                                  SnackBar(
                                    content: Text('Failed to save record: $err'),
                                    backgroundColor: AppColors.danger,
                                  ),
                                );
                              }
                            },
                      style: ElevatedButton.styleFrom(
                        backgroundColor: AppColors.primary,
                        foregroundColor: Colors.white,
                        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                      ),
                      icon: isSaving
                          ? const SizedBox(
                              width: 18,
                              height: 18,
                              child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2),
                            )
                          : const Icon(Icons.cloud_upload_rounded),
                      label: Text(isSaving ? 'Uploading & Saving...' : 'Save & Attach to Records'),
                    ),
                  ),
                ],
              ),
            ),
          );
        },
      ),
    );
  }

  List<MedicalRecord> get _filteredRecords {
    return _records.where((rec) {
      // Defense-in-depth: if on patient dashboard, strictly scope to current patient
      if (!_isAdminOrDoctor && _currentPatientEmail.isNotEmpty) {
        if (rec.patientEmail.isNotEmpty &&
            rec.patientEmail.toLowerCase().trim() != _currentPatientEmail) {
          return false;
        }
      }

      // Filter by Type
      if (_selectedType != 'All' && rec.recordType.toLowerCase() != _selectedType.toLowerCase()) {
        return false;
      }

      // Filter by Status
      if (_selectedStatus != 'All' && rec.status.toLowerCase() != _selectedStatus.toLowerCase()) {
        return false;
      }

      // Filter by Date Range
      if (_dateRange != null) {
        final start = DateTime(_dateRange!.start.year, _dateRange!.start.month, _dateRange!.start.day);
        final end = DateTime(_dateRange!.end.year, _dateRange!.end.month, _dateRange!.end.day, 23, 59, 59);
        if (rec.recordDate.isBefore(start) || rec.recordDate.isAfter(end)) {
          return false;
        }
      }

      // Filter by Search Query
      final query = _searchQuery.trim().toLowerCase();
      if (query.isNotEmpty) {
        final idStr = query.startsWith('#') ? query.substring(1) : query;
        final matchesId = rec.medicalRecordId.toString() == idStr || '#${rec.medicalRecordId}' == query;
        final matchesText = rec.diagnosis.toLowerCase().contains(query) ||
            rec.symptoms.toLowerCase().contains(query) ||
            rec.treatmentPlan.toLowerCase().contains(query) ||
            (rec.prescriptionNotes != null && rec.prescriptionNotes!.toLowerCase().contains(query)) ||
            (rec.labNotes != null && rec.labNotes!.toLowerCase().contains(query)) ||
            rec.patientName.toLowerCase().contains(query) ||
            rec.patientEmail.toLowerCase().contains(query) ||
            (rec.doctorName != null && rec.doctorName!.toLowerCase().contains(query)) ||
            rec.recordType.toLowerCase().contains(query) ||
            rec.status.toLowerCase().contains(query);

        if (!matchesId && !matchesText) return false;
      }

      return true;
    }).toList()
      ..sort((a, b) {
        final aDate = DateTime(a.recordDate.year, a.recordDate.month, a.recordDate.day);
        final bDate = DateTime(b.recordDate.year, b.recordDate.month, b.recordDate.day);
        final dateCmp = bDate.compareTo(aDate);
        if (dateCmp != 0) return dateCmp;
        final createdCmp = b.createdAt.compareTo(a.createdAt);
        if (createdCmp != 0) return createdCmp;
        return b.medicalRecordId.compareTo(a.medicalRecordId);
      });
  }

  Color _getTypeColor(String type) {
    switch (type) {
      case 'Consultation':
        return AppColors.primary;
      case 'LabReport':
        return const Color(0xFF0891B2);
      case 'DischargeSummary':
        return const Color(0xFFD97706);
      case 'Prescription':
        return const Color(0xFF4F46E5);
      case 'GeneralNote':
        return const Color(0xFF10B981);
      default:
        return Colors.grey;
    }
  }

  IconData _getTypeIcon(String type) {
    switch (type) {
      case 'LabReport':
        return Icons.science_outlined;
      case 'Prescription':
        return Icons.medication_outlined;
      case 'DischargeSummary':
        return Icons.local_hospital_outlined;
      case 'GeneralNote':
        return Icons.note_alt_outlined;
      case 'Consultation':
      default:
        return Icons.medical_services_outlined;
    }
  }

  Widget _buildKpiOverview(bool isDark) {
    final total = _summary?.totalRecords ?? _records.length;
    final consultations = _summary?.consultationCount ??
        _records.where((r) => r.recordType == 'Consultation').length;
    final labs = _summary?.labReportCount ??
        _records.where((r) => r.recordType == 'LabReport').length;
    final prescriptions = _summary?.prescriptionCount ??
        _records.where((r) => r.recordType == 'Prescription').length;
    final finalized = _summary?.finalizedCount ??
        _records.where((r) => r.status == 'Finalized').length;
    final drafts = _summary?.draftCount ??
        _records.where((r) => r.status == 'Draft').length;

    return Container(
      margin: const EdgeInsets.only(bottom: 16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                'Clinical Records Overview',
                style: TextStyle(
                  fontSize: 15,
                  fontWeight: FontWeight.bold,
                  color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
                ),
              ),
              Text(
                'Audit & Analytics',
                style: TextStyle(
                  fontSize: 12,
                  color: isDark ? AppColors.textMutedDark : AppColors.textMutedLight,
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          SizedBox(
            height: 96,
            child: ListView(
              scrollDirection: Axis.horizontal,
              children: [
                _buildKpiCard(
                  title: 'Total Records',
                  value: total.toString(),
                  sub: '+${_summary?.addedThisMonth ?? 0} this month',
                  icon: Icons.assignment_outlined,
                  colors: [const Color(0xFF1E40AF), const Color(0xFF3B82F6)],
                ),
                const SizedBox(width: 10),
                _buildKpiCard(
                  title: 'Consultations',
                  value: consultations.toString(),
                  sub: 'Clinical visits',
                  icon: Icons.medical_services_outlined,
                  colors: [const Color(0xFF0369A1), const Color(0xFF0EA5E9)],
                ),
                const SizedBox(width: 10),
                _buildKpiCard(
                  title: 'Lab Reports',
                  value: labs.toString(),
                  sub: 'Diagnostic scans',
                  icon: Icons.science_outlined,
                  colors: [const Color(0xFF0F766E), const Color(0xFF14B8A6)],
                ),
                const SizedBox(width: 10),
                _buildKpiCard(
                  title: 'Prescriptions',
                  value: prescriptions.toString(),
                  sub: 'Medication plans',
                  icon: Icons.medication_outlined,
                  colors: [const Color(0xFF6D28D9), const Color(0xFF8B5CF6)],
                ),
                const SizedBox(width: 10),
                _buildKpiCard(
                  title: 'Finalized',
                  value: finalized.toString(),
                  sub: '$drafts draft(s) pending',
                  icon: Icons.check_circle_outline_rounded,
                  colors: [const Color(0xFF047857), const Color(0xFF10B981)],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildKpiCard({
    required String title,
    required String value,
    required String sub,
    required IconData icon,
    required List<Color> colors,
  }) {
    return Container(
      width: 154,
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: colors,
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(14),
        boxShadow: [
          BoxShadow(
            color: colors.first.withValues(alpha: 0.35),
            blurRadius: 8,
            offset: const Offset(0, 3),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                title,
                style: const TextStyle(
                  color: Colors.white70,
                  fontSize: 11,
                  fontWeight: FontWeight.w600,
                ),
              ),
              Icon(icon, color: Colors.white, size: 16),
            ],
          ),
          Text(
            value,
            style: const TextStyle(
              color: Colors.white,
              fontSize: 22,
              fontWeight: FontWeight.w900,
              height: 1.1,
            ),
          ),
          Text(
            sub,
            style: const TextStyle(
              color: Colors.white70,
              fontSize: 10,
              fontWeight: FontWeight.w500,
            ),
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;

    final content = RefreshIndicator(
      onRefresh: _fetchRecords,
      color: AppColors.primary,
      child: SingleChildScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.symmetric(horizontal: 16.0, vertical: 12.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [


            // Admin & Doctor KPI Summary Overview Cards
            if (_isAdminOrDoctor) _buildKpiOverview(isDark),

            // Top Search & Action Bar
            Row(
              children: [
                Expanded(
                  child: TextField(
                    controller: _searchController,
                    decoration: InputDecoration(
                      hintText: _isAdminOrDoctor
                          ? 'Search diagnosis, patient, #ID, plan...'
                          : 'Search your medical reports & diagnoses...',
                      prefixIcon: const Icon(Icons.search, size: 20),
                      suffixIcon: _searchQuery.isNotEmpty
                          ? IconButton(
                              icon: const Icon(Icons.clear, size: 18),
                              onPressed: () {
                                _searchController.clear();
                                setState(() => _searchQuery = '');
                              },
                            )
                          : null,
                      contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                      filled: true,
                      fillColor: isDark ? AppColors.surfaceDark : const Color(0xFFF1F5F9),
                      border: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(12),
                        borderSide: BorderSide.none,
                      ),
                    ),
                    onChanged: (val) => setState(() => _searchQuery = val),
                  ),
                ),
                if (_isAdminOrDoctor) ...[
                  const SizedBox(width: 10),
                  ElevatedButton.icon(
                    onPressed: _openAddRecordDialog,
                    icon: const Icon(Icons.add_rounded, size: 18),
                    label: const Text('New'),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: AppColors.primary,
                      foregroundColor: Colors.white,
                      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                      elevation: 1,
                    ),
                  ),
                ],
              ],
            ),
            const SizedBox(height: 10),

            // Type Filter Chips
            SizedBox(
              height: 36,
              child: ListView.separated(
                scrollDirection: Axis.horizontal,
                itemCount: _types.length,
                separatorBuilder: (_, __) => const SizedBox(width: 6),
                itemBuilder: (context, i) {
                  final t = _types[i];
                  final isSelected = _selectedType == t;
                  return ChoiceChip(
                    label: Text(
                      t == 'LabReport'
                          ? 'Lab Reports'
                          : t == 'DischargeSummary'
                              ? 'Discharge'
                              : t == 'GeneralNote'
                                  ? 'Notes'
                                  : t,
                    ),
                    selected: isSelected,
                    selectedColor: AppColors.primary,
                    labelStyle: TextStyle(
                      color: isSelected ? Colors.white : (isDark ? Colors.white70 : Colors.black87),
                      fontSize: 11,
                      fontWeight: isSelected ? FontWeight.bold : FontWeight.normal,
                    ),
                    backgroundColor: isDark ? AppColors.surfaceDark : const Color(0xFFE2E8F0),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(18)),
                    onSelected: (_) => setState(() => _selectedType = t),
                  );
                },
              ),
            ),
            const SizedBox(height: 8),

            // Secondary Filters: Status + Date Range + Clear
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Row(
                children: [
                  // Status Filter Dropdown / Menu
                  Container(
                    height: 32,
                    padding: const EdgeInsets.symmetric(horizontal: 10),
                    decoration: BoxDecoration(
                      color: isDark ? AppColors.surfaceDark : const Color(0xFFE2E8F0),
                      borderRadius: BorderRadius.circular(16),
                      border: Border.all(
                        color: _selectedStatus != 'All' ? AppColors.primary : Colors.transparent,
                        width: 1.5,
                      ),
                    ),
                    child: DropdownButtonHideUnderline(
                      child: DropdownButton<String>(
                        value: _selectedStatus,
                        icon: const Icon(Icons.keyboard_arrow_down_rounded, size: 16),
                        style: TextStyle(
                          color: _selectedStatus != 'All'
                              ? AppColors.primary
                              : (isDark ? Colors.white70 : Colors.black87),
                          fontSize: 11,
                          fontWeight: _selectedStatus != 'All' ? FontWeight.bold : FontWeight.normal,
                        ),
                        dropdownColor: isDark ? AppColors.surfaceDark : Colors.white,
                        items: _statuses.map((s) {
                          return DropdownMenuItem(
                            value: s,
                            child: Text(s == 'All' ? 'All Statuses' : s),
                          );
                        }).toList(),
                        onChanged: (val) {
                          if (val != null) setState(() => _selectedStatus = val);
                        },
                      ),
                    ),
                  ),
                  const SizedBox(width: 8),

                  // Date Range Filter Button
                  InkWell(
                    borderRadius: BorderRadius.circular(16),
                    onTap: () async {
                      final picked = await showDateRangePicker(
                        context: context,
                        firstDate: DateTime(2020),
                        lastDate: DateTime(2035),
                        initialDateRange: _dateRange,
                      );
                      if (picked != null) {
                        setState(() => _dateRange = picked);
                      }
                    },
                    child: Container(
                      height: 32,
                      padding: const EdgeInsets.symmetric(horizontal: 10),
                      decoration: BoxDecoration(
                        color: isDark ? AppColors.surfaceDark : const Color(0xFFE2E8F0),
                        borderRadius: BorderRadius.circular(16),
                        border: Border.all(
                          color: _dateRange != null ? AppColors.primary : Colors.transparent,
                          width: 1.5,
                        ),
                      ),
                      child: Row(
                        children: [
                          Icon(
                            Icons.calendar_today_rounded,
                            size: 13,
                            color: _dateRange != null ? AppColors.primary : Colors.grey,
                          ),
                          const SizedBox(width: 6),
                          Text(
                            _dateRange != null
                                ? '${DateFormat('MM/dd').format(_dateRange!.start)} - ${DateFormat('MM/dd').format(_dateRange!.end)}'
                                : 'Date Range',
                            style: TextStyle(
                              color: _dateRange != null
                                  ? AppColors.primary
                                  : (isDark ? Colors.white70 : Colors.black87),
                              fontSize: 11,
                              fontWeight: _dateRange != null ? FontWeight.bold : FontWeight.normal,
                            ),
                          ),
                          if (_dateRange != null) ...[
                            const SizedBox(width: 4),
                            GestureDetector(
                              onTap: () => setState(() => _dateRange = null),
                              child: const Icon(Icons.close, size: 14, color: AppColors.primary),
                            ),
                          ],
                        ],
                      ),
                    ),
                  ),

                  // Clear Filters
                  if (_hasActiveFilters) ...[
                    const SizedBox(width: 8),
                    InkWell(
                      borderRadius: BorderRadius.circular(16),
                      onTap: _clearFilters,
                      child: Container(
                        height: 32,
                        padding: const EdgeInsets.symmetric(horizontal: 10),
                        decoration: BoxDecoration(
                          color: AppColors.danger.withValues(alpha: 0.12),
                          borderRadius: BorderRadius.circular(16),
                        ),
                        child: const Row(
                          children: [
                            Icon(Icons.filter_alt_off_rounded, size: 13, color: AppColors.danger),
                            SizedBox(width: 4),
                            Text(
                              'Clear Filters',
                              style: TextStyle(
                                color: AppColors.danger,
                                fontSize: 11,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ],
                ],
              ),
            ),
            const SizedBox(height: 12),

            // Body State Rendering
            if (_loading) ...[
              const Center(
                child: Padding(
                  padding: EdgeInsets.symmetric(vertical: 40.0),
                  child: CircularProgressIndicator(),
                ),
              ),
            ] else if (_error != null) ...[
              Center(
                child: Padding(
                  padding: const EdgeInsets.symmetric(vertical: 40.0, horizontal: 20.0),
                  child: Column(
                    children: [
                      const Icon(Icons.error_outline_rounded, size: 40, color: AppColors.danger),
                      const SizedBox(height: 12),
                      Text(
                        _error!,
                        textAlign: TextAlign.center,
                        style: const TextStyle(color: Colors.grey, fontSize: 13),
                      ),
                      const SizedBox(height: 16),
                      ElevatedButton.icon(
                        onPressed: _fetchRecords,
                        icon: const Icon(Icons.refresh, size: 16),
                        label: const Text('Try Again'),
                      ),
                    ],
                  ),
                ),
              ),
            ] else if (_filteredRecords.isEmpty) ...[
              Center(
                child: Padding(
                  padding: const EdgeInsets.symmetric(vertical: 60.0),
                  child: Column(
                    children: [
                      Icon(Icons.folder_open_rounded, size: 48, color: Colors.grey.withValues(alpha: 0.5)),
                      const SizedBox(height: 12),
                      const Text(
                        'No medical records found',
                        style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        _searchQuery.isNotEmpty
                            ? 'No matches for "$_searchQuery"'
                            : 'No medical records have been recorded yet.',
                        style: const TextStyle(color: Colors.grey, fontSize: 13),
                      ),
                      const SizedBox(height: 16),
                      if (_isAdminOrDoctor)
                        ElevatedButton.icon(
                          onPressed: _openAddRecordDialog,
                          icon: const Icon(Icons.add_rounded, size: 18),
                          label: const Text('Create First Record'),
                          style: ElevatedButton.styleFrom(
                            backgroundColor: AppColors.primary,
                            foregroundColor: Colors.white,
                            padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 12),
                            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                          ),
                        )
                      else
                        ElevatedButton.icon(
                          onPressed: _showPatientUploadOptions,
                          icon: const Icon(Icons.camera_alt_rounded, size: 18),
                          label: const Text('Upload Your First Report'),
                          style: ElevatedButton.styleFrom(
                            backgroundColor: AppColors.primary,
                            foregroundColor: Colors.white,
                            padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 12),
                            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                          ),
                        ),
                    ],
                  ),
                ),
              ),
            ] else ...[
              // Summary counter
              Text(
                '${_filteredRecords.length} ${_filteredRecords.length == 1 ? 'Record' : 'Records'} Available',
                style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: Colors.grey),
              ),
              const SizedBox(height: 8),

              // Cards List
              ListView.separated(
                shrinkWrap: true,
                physics: const NeverScrollableScrollPhysics(),
                itemCount: _filteredRecords.length,
                separatorBuilder: (_, __) => const SizedBox(height: 12),
                itemBuilder: (context, index) {
                  final rec = _filteredRecords[index];
                  final typeColor = _getTypeColor(rec.recordType);
                  final typeIcon = _getTypeIcon(rec.recordType);

                  return InkWell(
                    borderRadius: BorderRadius.circular(14),
                    onTap: () {
                      Navigator.push(
                        context,
                        MaterialPageRoute(
                          builder: (_) => MedicalRecordDetailScreen(
                            record: rec,
                            onRecordUpdated: _fetchRecords,
                          ),
                        ),
                      );
                    },
                    child: Container(
                      padding: const EdgeInsets.all(16),
                      decoration: BoxDecoration(
                        color: isDark ? AppColors.surfaceDark : Colors.white,
                        borderRadius: BorderRadius.circular(14),
                        border: Border.all(
                          color: isDark ? AppColors.borderDark : AppColors.borderLight,
                        ),
                        boxShadow: [
                          BoxShadow(
                            color: Colors.black.withValues(alpha: 0.03),
                            blurRadius: 8,
                            offset: const Offset(0, 3),
                          ),
                        ],
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            mainAxisAlignment: MainAxisAlignment.spaceBetween,
                            children: [
                              Container(
                                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                                decoration: BoxDecoration(
                                  color: typeColor.withValues(alpha: 0.12),
                                  borderRadius: BorderRadius.circular(8),
                                ),
                                child: Row(
                                  mainAxisSize: MainAxisSize.min,
                                  children: [
                                    Icon(typeIcon, size: 13, color: typeColor),
                                    const SizedBox(width: 5),
                                    Text(
                                      rec.recordType == 'LabReport'
                                          ? 'Lab Report'
                                          : rec.recordType == 'DischargeSummary'
                                              ? 'Discharge Summary'
                                              : rec.recordType == 'GeneralNote'
                                                  ? 'General Note'
                                                  : rec.recordType,
                                      style: TextStyle(
                                        color: typeColor,
                                        fontSize: 11,
                                        fontWeight: FontWeight.bold,
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                              Row(
                                children: [
                                  Text(
                                    DateFormat('MMM dd, yyyy').format(rec.recordDate),
                                    style: const TextStyle(color: Colors.grey, fontSize: 12),
                                  ),
                                  const SizedBox(width: 8),
                                  Container(
                                    padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                                    decoration: BoxDecoration(
                                      color: rec.status == 'Finalized'
                                          ? AppColors.success.withValues(alpha: 0.12)
                                          : Colors.grey.withValues(alpha: 0.15),
                                      borderRadius: BorderRadius.circular(6),
                                    ),
                                    child: Text(
                                      rec.status,
                                      style: TextStyle(
                                        color: rec.status == 'Finalized' ? AppColors.success : Colors.grey,
                                        fontSize: 10,
                                        fontWeight: FontWeight.w700,
                                      ),
                                    ),
                                  ),
                                ],
                              ),
                            ],
                          ),
                          const SizedBox(height: 10),

                          if (_isAdminOrDoctor && rec.patientName.isNotEmpty) ...[
                            Row(
                              children: [
                                const Icon(Icons.person_rounded, size: 14, color: AppColors.primary),
                                const SizedBox(width: 4),
                                Text(
                                  rec.patientName,
                                  style: const TextStyle(
                                    fontSize: 13,
                                    fontWeight: FontWeight.bold,
                                    color: AppColors.primary,
                                  ),
                                ),
                              ],
                            ),
                            const SizedBox(height: 4),
                          ],

                          // Diagnosis / Investigation Title
                          Text(
                            rec.diagnosis,
                            style: TextStyle(
                              fontSize: 15,
                              fontWeight: FontWeight.w800,
                              color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
                            ),
                          ),
                          const SizedBox(height: 6),

                          // Symptoms / Findings snippet
                          Text(
                            rec.symptoms,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: TextStyle(
                              fontSize: 13,
                              color: isDark ? AppColors.textMutedDark : AppColors.textMutedLight,
                              height: 1.3,
                            ),
                          ),
                          const SizedBox(height: 12),

                          // Footer: Clinician info, Attachments & Admin/Doctor Actions
                          Row(
                            mainAxisAlignment: MainAxisAlignment.spaceBetween,
                            children: [
                              Row(
                                children: [
                                  const Icon(Icons.medical_services_outlined, size: 14, color: Colors.grey),
                                  const SizedBox(width: 4),
                                  Text(
                                    rec.doctorName ?? 'Hospital Clinician',
                                    style: const TextStyle(fontSize: 12, color: Colors.grey),
                                  ),
                                  if (rec.attachments.isNotEmpty) ...[
                                    const SizedBox(width: 8),
                                    Container(
                                      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                                      decoration: BoxDecoration(
                                        color: AppColors.primary.withValues(alpha: 0.1),
                                        borderRadius: BorderRadius.circular(6),
                                      ),
                                      child: Row(
                                        children: [
                                          const Icon(Icons.attach_file_rounded, size: 13, color: AppColors.primary),
                                          const SizedBox(width: 3),
                                          Text(
                                            '${rec.attachments.length} ${rec.attachments.length == 1 ? 'file' : 'files'}',
                                            style: const TextStyle(
                                              fontSize: 11,
                                              fontWeight: FontWeight.bold,
                                              color: AppColors.primary,
                                            ),
                                          ),
                                        ],
                                      ),
                                    ),
                                  ],
                                ],
                              ),
                              if (_isAdminOrDoctor)
                                Row(
                                  mainAxisSize: MainAxisSize.min,
                                  children: [
                                    IconButton(
                                      icon: const Icon(Icons.edit_outlined, size: 18),
                                      color: AppColors.primary,
                                      padding: EdgeInsets.zero,
                                      constraints: const BoxConstraints(minWidth: 32, minHeight: 32),
                                      tooltip: 'Edit Record',
                                      onPressed: () => _openEditRecordDialog(rec),
                                    ),
                                    if (_isAdmin)
                                      IconButton(
                                        icon: const Icon(Icons.delete_outline_rounded, size: 18),
                                        color: AppColors.danger,
                                        padding: EdgeInsets.zero,
                                        constraints: const BoxConstraints(minWidth: 32, minHeight: 32),
                                        tooltip: 'Delete Record',
                                        onPressed: () => _confirmDeleteRecord(rec),
                                      ),
                                  ],
                                ),
                            ],
                          ),
                        ],
                      ),
                    ),
                  );
                },
              ),
            ],
            const SizedBox(height: 80),
          ],
        ),
      ),
    );

    return Scaffold(
      backgroundColor: Colors.transparent,
      appBar: widget.embedded
          ? null
          : AppBar(
              title: const Text('Medical Records'),
            ),
      body: content,
      floatingActionButton: _isAdminOrDoctor
          ? FloatingActionButton.extended(
              onPressed: _openAddRecordDialog,
              backgroundColor: AppColors.primary,
              icon: const Icon(Icons.add_rounded, color: Colors.white),
              label: const Text(
                'Add Record',
                style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold),
              ),
            )
          : FloatingActionButton.extended(
              onPressed: _showPatientUploadOptions,
              backgroundColor: AppColors.primary,
              icon: const Icon(Icons.camera_alt_rounded, color: Colors.white),
              label: const Text(
                'Upload Report',
                style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold),
              ),
            ),
    );
  }
}
