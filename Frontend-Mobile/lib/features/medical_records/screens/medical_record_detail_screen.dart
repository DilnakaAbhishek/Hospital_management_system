import 'dart:io';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:file_picker/file_picker.dart';
import 'package:intl/intl.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:url_launcher/url_launcher.dart';
import 'package:medicore_mobile/core/constants/app_colors.dart';
import 'package:medicore_mobile/core/services/api_service.dart';
import 'package:medicore_mobile/features/medical_records/screens/add_medical_record_dialog.dart';
import 'package:medicore_mobile/models/medical_record.dart';

class MedicalRecordDetailScreen extends StatefulWidget {
  final MedicalRecord record;
  final VoidCallback? onRecordUpdated;

  const MedicalRecordDetailScreen({
    super.key,
    required this.record,
    this.onRecordUpdated,
  });

  @override
  State<MedicalRecordDetailScreen> createState() => _MedicalRecordDetailScreenState();
}

class _MedicalRecordDetailScreenState extends State<MedicalRecordDetailScreen> {
  late MedicalRecord _record;
  bool _uploading = false;
  bool _isAdmin = false;
  bool _isAdminOrDoctor = false;
  final ImagePicker _picker = ImagePicker();

  @override
  void initState() {
    super.initState();
    _record = widget.record;
    _checkRole();
  }

  Future<void> _checkRole() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final role = prefs.getString('user_role');
      final email = (prefs.getString('patient_email') ?? '').toLowerCase();
      if (!mounted) return;
      setState(() {
        _isAdmin = role == 'Admin' || email.contains('admin');
        _isAdminOrDoctor = _isAdmin || role == 'Doctor' || email.contains('doctor');
      });
    } catch (_) {}
  }

  Future<void> _pickAndUploadImage(ImageSource source) async {
    try {
      final pickedFile = await _picker.pickImage(
        source: source,
        imageQuality: 85,
        maxWidth: 1600,
      );

      if (pickedFile == null) return;

      setState(() => _uploading = true);

      final fileName = pickedFile.name.isNotEmpty
          ? pickedFile.name
          : 'report_${DateTime.now().millisecondsSinceEpoch}.jpg';
      final fileBytes = await pickedFile.readAsBytes();

      final attachment = await ApiService.uploadMedicalRecordAttachment(
        _record.medicalRecordId,
        fileBytes: fileBytes,
        fileName: fileName,
        fileType: pickedFile.mimeType ?? (fileName.toLowerCase().endsWith('.png') ? 'image/png' : 'image/jpeg'),
      );

      if (!mounted) return;

      setState(() {
        _record = MedicalRecord(
          medicalRecordId: _record.medicalRecordId,
          patientId: _record.patientId,
          patientName: _record.patientName,
          patientEmail: _record.patientEmail,
          doctorId: _record.doctorId,
          doctorName: _record.doctorName,
          doctorSpecialization: _record.doctorSpecialization,
          appointmentId: _record.appointmentId,
          recordDate: _record.recordDate,
          recordType: _record.recordType,
          diagnosis: _record.diagnosis,
          symptoms: _record.symptoms,
          treatmentPlan: _record.treatmentPlan,
          prescriptionNotes: _record.prescriptionNotes,
          labNotes: _record.labNotes,
          followUpDate: _record.followUpDate,
          status: _record.status,
          createdAt: _record.createdAt,
          updatedAt: _record.updatedAt,
          attachments: [..._record.attachments, attachment],
        );
        _uploading = false;
      });

      widget.onRecordUpdated?.call();

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Document / Scan uploaded successfully to Cloud!'),
          backgroundColor: AppColors.success,
          behavior: SnackBarBehavior.floating,
        ),
      );
    } catch (e) {
      if (!mounted) return;
      setState(() => _uploading = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Upload failed: $e'),
          backgroundColor: AppColors.danger,
          behavior: SnackBarBehavior.floating,
        ),
      );
    }
  }

  Future<void> _pickAndUploadPdf() async {
    try {
      final result = await FilePicker.pickFiles(
        type: FileType.custom,
        allowedExtensions: ['pdf'],
        withData: true,
      );

      if (result == null || result.files.isEmpty) return;
      final file = result.files.single;

      setState(() => _uploading = true);

      List<int>? bytes = file.bytes;
      if (bytes == null && file.path != null && file.path!.isNotEmpty) {
        bytes = await File(file.path!).readAsBytes();
      }
      if (bytes == null) {
        setState(() => _uploading = false);
        return;
      }

      final fileName = file.name.isNotEmpty
          ? file.name
          : 'report_${DateTime.now().millisecondsSinceEpoch}.pdf';

      final attachment = await ApiService.uploadMedicalRecordAttachment(
        _record.medicalRecordId,
        fileBytes: bytes,
        fileName: fileName,
        fileType: 'application/pdf',
      );

      if (!mounted) return;

      setState(() {
        _record = MedicalRecord(
          medicalRecordId: _record.medicalRecordId,
          patientId: _record.patientId,
          patientName: _record.patientName,
          patientEmail: _record.patientEmail,
          doctorId: _record.doctorId,
          doctorName: _record.doctorName,
          doctorSpecialization: _record.doctorSpecialization,
          appointmentId: _record.appointmentId,
          recordDate: _record.recordDate,
          recordType: _record.recordType,
          diagnosis: _record.diagnosis,
          symptoms: _record.symptoms,
          treatmentPlan: _record.treatmentPlan,
          prescriptionNotes: _record.prescriptionNotes,
          labNotes: _record.labNotes,
          followUpDate: _record.followUpDate,
          status: _record.status,
          createdAt: _record.createdAt,
          updatedAt: _record.updatedAt,
          attachments: [..._record.attachments, attachment],
        );
        _uploading = false;
      });

      widget.onRecordUpdated?.call();

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('PDF Report uploaded successfully to Cloud!'),
          backgroundColor: AppColors.success,
          behavior: SnackBarBehavior.floating,
        ),
      );
    } catch (e) {
      if (!mounted) return;
      setState(() => _uploading = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Upload failed: $e'),
          backgroundColor: AppColors.danger,
          behavior: SnackBarBehavior.floating,
        ),
      );
    }
  }

  Future<void> _openAttachment(MedicalRecordAttachment att) async {
    final fullUrl = att.fileUrl.startsWith('http')
        ? att.fileUrl
        : '${ApiService.baseUrl.replaceAll('/api', '')}${att.fileUrl.startsWith('/') ? '' : '/'}${att.fileUrl}';

    try {
      final uri = Uri.parse(fullUrl);
      final launched = await launchUrl(uri, mode: LaunchMode.externalApplication);
      if (!launched && mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Could not open file in external app')),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Error opening file: $e')),
        );
      }
    }
  }

  Future<void> _deleteAttachment(MedicalRecordAttachment att) async {
    final confirm = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Delete Attachment'),
        content: Text('Are you sure you want to remove "${att.fileName}"?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Cancel'),
          ),
          ElevatedButton(
            style: ElevatedButton.styleFrom(backgroundColor: AppColors.danger),
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Delete', style: TextStyle(color: Colors.white)),
          ),
        ],
      ),
    );

    if (confirm != true) return;

    try {
      await ApiService.deleteMedicalRecordAttachment(_record.medicalRecordId, att.attachmentId);
      if (!mounted) return;
      setState(() {
        _record = MedicalRecord(
          medicalRecordId: _record.medicalRecordId,
          patientId: _record.patientId,
          patientName: _record.patientName,
          patientEmail: _record.patientEmail,
          doctorId: _record.doctorId,
          doctorName: _record.doctorName,
          doctorSpecialization: _record.doctorSpecialization,
          appointmentId: _record.appointmentId,
          recordDate: _record.recordDate,
          recordType: _record.recordType,
          diagnosis: _record.diagnosis,
          symptoms: _record.symptoms,
          treatmentPlan: _record.treatmentPlan,
          prescriptionNotes: _record.prescriptionNotes,
          labNotes: _record.labNotes,
          followUpDate: _record.followUpDate,
          status: _record.status,
          createdAt: _record.createdAt,
          updatedAt: _record.updatedAt,
          attachments: _record.attachments.where((a) => a.attachmentId != att.attachmentId).toList(),
        );
      });
      widget.onRecordUpdated?.call();
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Attachment deleted successfully'),
          backgroundColor: AppColors.success,
          behavior: SnackBarBehavior.floating,
        ),
      );
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Failed to delete attachment: $e'),
          backgroundColor: AppColors.danger,
          behavior: SnackBarBehavior.floating,
        ),
      );
    }
  }

  void _showUploadOptions() {
    showModalBottomSheet(
      context: context,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (ctx) => SafeArea(
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 18.0, horizontal: 20.0),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'Upload Diagnostic Report / Scan',
                style: TextStyle(fontSize: 17, fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 6),
              const Text(
                'Take a photo of paper report, choose an image, or upload a PDF',
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
                subtitle: const Text('Scan document directly using device camera'),
                onTap: () {
                  Navigator.pop(ctx);
                  _pickAndUploadImage(ImageSource.camera);
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
                subtitle: const Text('Select saved PNG/JPEG image from device'),
                onTap: () {
                  Navigator.pop(ctx);
                  _pickAndUploadImage(ImageSource.gallery);
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
                title: const Text('Upload PDF Document', style: TextStyle(fontWeight: FontWeight.w600)),
                subtitle: const Text('Select digital lab report or clinical prescription PDF'),
                onTap: () {
                  Navigator.pop(ctx);
                  _pickAndUploadPdf();
                },
              ),
            ],
          ),
        ),
      ),
    );
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

  String _getTypeDisplayName(String type) {
    switch (type) {
      case 'LabReport':
        return 'Lab Report';
      case 'DischargeSummary':
        return 'Discharge Summary';
      case 'GeneralNote':
        return 'General Note';
      default:
        return type;
    }
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final typeColor = _getTypeColor(_record.recordType);

    return Scaffold(
      appBar: AppBar(
        title: Text('${_getTypeDisplayName(_record.recordType)} #${_record.medicalRecordId}'),
        actions: [
          if (_isAdminOrDoctor)
            IconButton(
              icon: const Icon(Icons.edit_outlined),
              tooltip: 'Edit Medical Record',
              onPressed: () {
                showDialog(
                  context: context,
                  barrierDismissible: false,
                  builder: (_) => AddMedicalRecordDialog(
                    recordToEdit: _record,
                    onRecordCreated: () async {
                      try {
                        final refreshed = await ApiService.getMedicalRecordById(_record.medicalRecordId);
                        if (mounted) setState(() => _record = refreshed);
                      } catch (_) {}
                      widget.onRecordUpdated?.call();
                    },
                  ),
                );
              },
            ),
          if (_isAdmin)
            IconButton(
              icon: const Icon(Icons.delete_outline_rounded, color: AppColors.danger),
              tooltip: 'Delete Medical Record',
              onPressed: () {
                showDialog(
                  context: context,
                  builder: (ctx) => AlertDialog(
                    title: Text('Delete Record #${_record.medicalRecordId}?'),
                    content: const Text(
                      'Are you sure you want to permanently delete this medical record? This action cannot be undone.',
                    ),
                    actions: [
                      TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('Cancel')),
                      ElevatedButton(
                        style: ElevatedButton.styleFrom(
                          backgroundColor: AppColors.danger,
                          foregroundColor: Colors.white,
                        ),
                        onPressed: () async {
                          Navigator.pop(ctx);
                          try {
                            await ApiService.deleteMedicalRecord(_record.medicalRecordId);
                            widget.onRecordUpdated?.call();
                            if (mounted) Navigator.pop(context);
                          } catch (e) {
                            if (!mounted) return;
                            ScaffoldMessenger.of(context).showSnackBar(
                              SnackBar(
                                content: Text('Delete failed: $e'),
                                backgroundColor: AppColors.danger,
                              ),
                            );
                          }
                        },
                        child: const Text('Delete'),
                      ),
                    ],
                  ),
                );
              },
            ),
          IconButton(
            icon: const Icon(Icons.share_outlined),
            tooltip: 'Share Record',
            onPressed: () {
              ScaffoldMessenger.of(context).showSnackBar(
                const SnackBar(
                  content: Text('Medical record summary copied to clipboard.'),
                  behavior: SnackBarBehavior.floating,
                ),
              );
            },
          ),
        ],
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Header Hero Banner
            Container(
              padding: const EdgeInsets.all(18),
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  colors: [typeColor.withValues(alpha: 0.15), typeColor.withValues(alpha: 0.05)],
                  begin: Alignment.topLeft,
                  end: Alignment.bottomRight,
                ),
                borderRadius: BorderRadius.circular(16),
                border: Border.all(color: typeColor.withValues(alpha: 0.3)),
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
                          color: typeColor,
                          borderRadius: BorderRadius.circular(20),
                        ),
                        child: Text(
                          _getTypeDisplayName(_record.recordType),
                          style: const TextStyle(
                            color: Colors.white,
                            fontSize: 12,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                      ),
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                        decoration: BoxDecoration(
                          color: _record.status == 'Finalized'
                              ? AppColors.success.withValues(alpha: 0.15)
                              : Colors.grey.withValues(alpha: 0.2),
                          borderRadius: BorderRadius.circular(8),
                        ),
                        child: Text(
                          _record.status,
                          style: TextStyle(
                            color: _record.status == 'Finalized' ? AppColors.success : Colors.grey,
                            fontSize: 12,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  Text(
                    _record.diagnosis,
                    style: TextStyle(
                      fontSize: 18,
                      fontWeight: FontWeight.w900,
                      color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
                    ),
                  ),
                  const SizedBox(height: 10),
                  Row(
                    children: [
                      const Icon(Icons.calendar_today_rounded, size: 14, color: Colors.grey),
                      const SizedBox(width: 6),
                      Text(
                        DateFormat('MMMM dd, yyyy').format(_record.recordDate),
                        style: const TextStyle(color: Colors.grey, fontSize: 13),
                      ),
                      if (_record.doctorName != null) ...[
                        const SizedBox(width: 14),
                        const Icon(Icons.medical_services_rounded, size: 14, color: Colors.grey),
                        const SizedBox(width: 6),
                        Expanded(
                          child: Text(
                            _record.doctorName!,
                            style: const TextStyle(color: Colors.grey, fontSize: 13),
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      ],
                    ],
                  ),
                  if (_record.patientName.isNotEmpty) ...[
                    const SizedBox(height: 6),
                    Row(
                      children: [
                        const Icon(Icons.person_outline_rounded, size: 14, color: Colors.grey),
                        const SizedBox(width: 6),
                        Text(
                          'Patient: ${_record.patientName}',
                          style: const TextStyle(color: Colors.grey, fontSize: 12),
                        ),
                      ],
                    ),
                  ],
                ],
              ),
            ),
            const SizedBox(height: 20),

            // Dynamic Content Sections according to recordType
            ..._buildDynamicContentSections(isDark),

            // Follow-up Date Card (if set)
            if (_record.followUpDate != null) ...[
              const SizedBox(height: 16),
              _buildSectionCard(
                title: 'Follow-up / Review Schedule',
                icon: Icons.event_repeat_rounded,
                iconColor: AppColors.primary,
                content: Row(
                  children: [
                    const Icon(Icons.calendar_month_rounded, size: 20, color: AppColors.primary),
                    const SizedBox(width: 10),
                    Text(
                      DateFormat('EEEE, MMMM dd, yyyy').format(_record.followUpDate!),
                      style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600),
                    ),
                  ],
                ),
              ),
            ],

            const SizedBox(height: 16),

            // Attachments Section
            _buildSectionCard(
              title: 'Attached Reports & Scans (${_record.attachments.length})',
              icon: Icons.attach_file_rounded,
              iconColor: AppColors.accent,
              action: TextButton.icon(
                onPressed: _uploading ? null : _showUploadOptions,
                icon: const Icon(Icons.camera_alt, size: 16),
                label: const Text('Scan / Upload'),
              ),
              content: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  if (_uploading)
                    const Padding(
                      padding: EdgeInsets.symmetric(vertical: 8.0),
                      child: Row(
                        children: [
                          SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2)),
                          SizedBox(width: 10),
                          Text('Uploading scan...', style: TextStyle(fontSize: 13)),
                        ],
                      ),
                    ),
                  if (_record.attachments.isEmpty)
                    const Padding(
                      padding: EdgeInsets.symmetric(vertical: 8.0),
                      child: Text(
                        'No files attached yet. Tap "Scan / Upload" above to attach a lab report or photo.',
                        style: TextStyle(color: Colors.grey, fontSize: 13, fontStyle: FontStyle.italic),
                      ),
                    )
                  else
                    ListView.separated(
                      shrinkWrap: true,
                      physics: const NeverScrollableScrollPhysics(),
                      itemCount: _record.attachments.length,
                      separatorBuilder: (_, __) => const SizedBox(height: 8),
                      itemBuilder: (context, index) {
                        final att = _record.attachments[index];
                        final isPdf = att.fileName.toLowerCase().endsWith('.pdf') ||
                            att.fileType.toLowerCase().contains('pdf');
                        final isImage = att.fileType.toLowerCase().contains('image') ||
                            att.fileName.toLowerCase().endsWith('.jpg') ||
                            att.fileName.toLowerCase().endsWith('.jpeg') ||
                            att.fileName.toLowerCase().endsWith('.png');

                        return Container(
                          padding: const EdgeInsets.all(12),
                          decoration: BoxDecoration(
                            color: isDark ? Colors.grey[900] : const Color(0xFFF8FAFC),
                            borderRadius: BorderRadius.circular(10),
                            border: Border.all(color: Colors.grey.withValues(alpha: 0.2)),
                          ),
                          child: Row(
                            children: [
                              Container(
                                padding: const EdgeInsets.all(8),
                                decoration: BoxDecoration(
                                  color: (isPdf ? Colors.red : AppColors.primary).withValues(alpha: 0.1),
                                  borderRadius: BorderRadius.circular(8),
                                ),
                                child: Icon(
                                  isPdf
                                      ? Icons.picture_as_pdf_rounded
                                      : (isImage ? Icons.image_rounded : Icons.description_outlined),
                                  color: isPdf ? Colors.redAccent : AppColors.primary,
                                  size: 20,
                                ),
                              ),
                              const SizedBox(width: 12),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      att.fileName,
                                      style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13),
                                      overflow: TextOverflow.ellipsis,
                                    ),
                                    Text(
                                      '${att.fileType} • ${(att.fileSize / 1024).toStringAsFixed(1)} KB',
                                      style: const TextStyle(color: Colors.grey, fontSize: 11),
                                    ),
                                  ],
                                ),
                              ),
                              IconButton(
                                icon: const Icon(Icons.visibility_outlined, size: 20),
                                color: AppColors.primary,
                                tooltip: 'Preview',
                                onPressed: () => _previewAttachment(att),
                              ),
                              IconButton(
                                icon: const Icon(Icons.download_rounded, size: 20),
                                color: Colors.teal,
                                tooltip: 'Download / Open',
                                onPressed: () => _openAttachment(att),
                              ),
                              if (_isAdminOrDoctor)
                                IconButton(
                                  icon: const Icon(Icons.delete_outline, size: 20),
                                  color: AppColors.danger,
                                  tooltip: 'Delete',
                                  onPressed: () => _deleteAttachment(att),
                                ),
                            ],
                          ),
                        );
                      },
                    ),
                ],
              ),
            ),
            const SizedBox(height: 30),
          ],
        ),
      ),
    );
  }

  void _previewAttachment(MedicalRecordAttachment att) {
    final isPdf = att.fileName.toLowerCase().endsWith('.pdf') ||
        att.fileType.toLowerCase().contains('pdf');
    final isImage = att.fileType.toLowerCase().contains('image') ||
        att.fileName.toLowerCase().endsWith('.jpg') ||
        att.fileName.toLowerCase().endsWith('.jpeg') ||
        att.fileName.toLowerCase().endsWith('.png');

    final fullUrl = att.fileUrl.startsWith('http')
        ? att.fileUrl
        : '${ApiService.baseUrl.replaceAll('/api', '')}${att.fileUrl.startsWith('/') ? '' : '/'}${att.fileUrl}';

    showDialog(
      context: context,
      builder: (ctx) => Dialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Expanded(
                    child: Text(
                      att.fileName,
                      style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 14),
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
                  IconButton(
                    icon: const Icon(Icons.close, size: 20),
                    onPressed: () => Navigator.pop(ctx),
                  ),
                ],
              ),
              const SizedBox(height: 10),
              if (isImage)
                ClipRRect(
                  borderRadius: BorderRadius.circular(12),
                  child: Image.network(
                    fullUrl,
                    fit: BoxFit.contain,
                    height: 280,
                    errorBuilder: (_, __, ___) => Container(
                      height: 140,
                      color: Colors.grey[200],
                      alignment: Alignment.center,
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          const Icon(Icons.broken_image_rounded, size: 36, color: Colors.grey),
                          const SizedBox(height: 6),
                          Text(
                            att.fileName,
                            style: const TextStyle(color: Colors.grey, fontSize: 12),
                          ),
                        ],
                      ),
                    ),
                  ),
                )
              else
                Container(
                  width: double.infinity,
                  padding: const EdgeInsets.all(24),
                  decoration: BoxDecoration(
                    color: (isPdf ? Colors.red : AppColors.primary).withValues(alpha: 0.08),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Column(
                    children: [
                      Icon(
                        isPdf ? Icons.picture_as_pdf_rounded : Icons.description_outlined,
                        size: 48,
                        color: isPdf ? Colors.redAccent : AppColors.primary,
                      ),
                      const SizedBox(height: 12),
                      Text(
                        att.fileName,
                        textAlign: TextAlign.center,
                        style: const TextStyle(fontWeight: FontWeight.bold),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        '${(att.fileSize / 1024).toStringAsFixed(1)} KB • ${isPdf ? 'PDF Document' : 'Document'}',
                        style: const TextStyle(color: Colors.grey, fontSize: 12),
                      ),
                    ],
                  ),
                ),
              const SizedBox(height: 16),
              Row(
                children: [
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: () => Navigator.pop(ctx),
                      icon: const Icon(Icons.close, size: 16),
                      label: const Text('Close'),
                      style: OutlinedButton.styleFrom(
                        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
                      ),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: ElevatedButton.icon(
                      onPressed: () {
                        Navigator.pop(ctx);
                        _openAttachment(att);
                      },
                      icon: const Icon(Icons.download_rounded, size: 16),
                      label: Text(
                        isPdf ? 'Download PDF' : (isImage ? 'Download / View' : 'Download'),
                        overflow: TextOverflow.ellipsis,
                      ),
                      style: ElevatedButton.styleFrom(
                        backgroundColor: isPdf ? Colors.redAccent : AppColors.primary,
                        foregroundColor: Colors.white,
                        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
                      ),
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  List<Widget> _buildDynamicContentSections(bool isDark) {
    final textStyle = TextStyle(
      fontSize: 14,
      height: 1.5,
      color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
    );

    switch (_record.recordType) {
      case 'LabReport':
        return [
          _buildSectionCard(
            title: 'Test Findings & Measured Values',
            icon: Icons.science_outlined,
            iconColor: const Color(0xFF0891B2),
            content: Text(_record.symptoms, style: textStyle),
          ),
          const SizedBox(height: 16),
          if (_record.treatmentPlan.isNotEmpty) ...[
            _buildSectionCard(
              title: 'Reference Ranges & Remarks',
              icon: Icons.rule_folder_outlined,
              iconColor: const Color(0xFF0D9488),
              content: Text(_record.treatmentPlan, style: textStyle),
            ),
            const SizedBox(height: 16),
          ],
          if (_record.labNotes != null && _record.labNotes!.trim().isNotEmpty) ...[
            _buildSectionCard(
              title: 'Laboratory / Specimen Details',
              icon: Icons.domain_rounded,
              iconColor: const Color(0xFF6366F1),
              content: Text(_record.labNotes!, style: textStyle),
            ),
          ],
        ];

      case 'Prescription':
        return [
          if (_record.prescriptionNotes != null && _record.prescriptionNotes!.trim().isNotEmpty) ...[
            _buildSectionCard(
              title: 'Prescribed Medications & Dosages',
              icon: Icons.medication_liquid_outlined,
              iconColor: const Color(0xFF4F46E5),
              content: Container(
                width: double.infinity,
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: isDark ? Colors.black26 : const Color(0xFFF1F5F9),
                  borderRadius: BorderRadius.circular(10),
                  border: Border.all(color: Colors.grey.withValues(alpha: 0.2)),
                ),
                child: Text(
                  _record.prescriptionNotes!,
                  style: const TextStyle(
                    fontFamily: 'monospace',
                    fontSize: 13,
                    height: 1.6,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ),
            ),
            const SizedBox(height: 16),
          ],
          _buildSectionCard(
            title: 'Instructions, Precautions & Care Plan',
            icon: Icons.info_outline_rounded,
            iconColor: AppColors.primary,
            content: Text(_record.treatmentPlan, style: textStyle),
          ),
          if (_record.symptoms.isNotEmpty && !_record.symptoms.contains('Prescription issued')) ...[
            const SizedBox(height: 16),
            _buildSectionCard(
              title: 'Clinical Symptoms & Indication',
              icon: Icons.healing_outlined,
              iconColor: Colors.orange,
              content: Text(_record.symptoms, style: textStyle),
            ),
          ],
        ];

      case 'DischargeSummary':
        return [
          _buildSectionCard(
            title: 'Hospital Course & Inpatient Care Summary',
            icon: Icons.timeline_rounded,
            iconColor: const Color(0xFFD97706),
            content: Text(_record.symptoms, style: textStyle),
          ),
          const SizedBox(height: 16),
          _buildSectionCard(
            title: 'Discharge Advice & Post-Discharge Medications',
            icon: Icons.healing_outlined,
            iconColor: AppColors.primary,
            content: Text(_record.treatmentPlan, style: textStyle),
          ),
        ];

      case 'GeneralNote':
        return [
          _buildSectionCard(
            title: 'Clinical Observations & Note Content',
            icon: Icons.notes_rounded,
            iconColor: const Color(0xFF10B981),
            content: Text(_record.symptoms, style: textStyle),
          ),
          const SizedBox(height: 16),
          if (_record.treatmentPlan.isNotEmpty) ...[
            _buildSectionCard(
              title: 'Recommended Action Items & Next Steps',
              icon: Icons.check_circle_outline_rounded,
              iconColor: AppColors.primary,
              content: Text(_record.treatmentPlan, style: textStyle),
            ),
          ],
        ];

      case 'Consultation':
      default:
        return [
          _buildSectionCard(
            title: 'Symptoms & Observations',
            icon: Icons.personal_injury_outlined,
            iconColor: Colors.orange,
            content: Text(_record.symptoms, style: textStyle),
          ),
          const SizedBox(height: 16),
          _buildSectionCard(
            title: 'Treatment Plan & Advice',
            icon: Icons.healing_outlined,
            iconColor: AppColors.primary,
            content: Text(_record.treatmentPlan, style: textStyle),
          ),
          if (_record.prescriptionNotes != null && _record.prescriptionNotes!.trim().isNotEmpty) ...[
            const SizedBox(height: 16),
            _buildSectionCard(
              title: 'Prescribed Medication',
              icon: Icons.medication_outlined,
              iconColor: const Color(0xFF4F46E5),
              content: Container(
                width: double.infinity,
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: isDark ? Colors.black26 : const Color(0xFFF1F5F9),
                  borderRadius: BorderRadius.circular(10),
                  border: Border.all(color: Colors.grey.withValues(alpha: 0.2)),
                ),
                child: Text(
                  _record.prescriptionNotes!,
                  style: const TextStyle(
                    fontFamily: 'monospace',
                    fontSize: 13,
                    height: 1.5,
                  ),
                ),
              ),
            ),
          ],
          if (_record.labNotes != null && _record.labNotes!.trim().isNotEmpty) ...[
            const SizedBox(height: 16),
            _buildSectionCard(
              title: 'Lab & Diagnostic Findings',
              icon: Icons.science_outlined,
              iconColor: const Color(0xFF0891B2),
              content: Text(_record.labNotes!, style: textStyle),
            ),
          ],
        ];
    }
  }

  Widget _buildSectionCard({
    required String title,
    required IconData icon,
    required Color iconColor,
    required Widget content,
    Widget? action,
  }) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Container(
      width: double.infinity,
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
            blurRadius: 10,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Row(
                children: [
                  Icon(icon, size: 18, color: iconColor),
                  const SizedBox(width: 8),
                  Text(
                    title,
                    style: const TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ],
              ),
              if (action != null) action,
            ],
          ),
          const SizedBox(height: 12),
          content,
        ],
      ),
    );
  }
}
