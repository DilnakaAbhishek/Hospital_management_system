import 'package:flutter_test/flutter_test.dart';
import 'package:medicore_mobile/models/medical_record.dart';

void main() {
  group('MedicalRecordAttachment tests', () {
    final testDate = DateTime(2026, 1, 1);

    test('cleanFileName strips 32-character hex GUID prefix', () {
      final att = MedicalRecordAttachment(
        attachmentId: 1,
        medicalRecordId: 10,
        fileName: '6bfb4c06f363402bbda19b6264d1f237_blood_report.pdf',
        fileType: 'application/pdf',
        fileUrl: 'https://r2.example.com/attachments/1',
        fileSize: 1024,
        uploadedAt: testDate,
      );

      expect(att.cleanFileName, equals('blood_report.pdf'));
      expect(att.isPdf, isTrue);
      expect(att.isImage, isFalse);
    });

    test('cleanFileName strips 36-character hyphenated GUID prefix', () {
      final att = MedicalRecordAttachment(
        attachmentId: 2,
        medicalRecordId: 10,
        fileName: '12345678-1234-1234-1234-123456789abc_chest_xray.png',
        fileType: 'image/png',
        fileUrl: 'https://r2.example.com/attachments/2',
        fileSize: 2048,
        uploadedAt: testDate,
      );

      expect(att.cleanFileName, equals('chest_xray.png'));
      expect(att.isImage, isTrue);
      expect(att.isPdf, isFalse);
    });

    test('cleanFileName preserves names without GUID prefix', () {
      final att = MedicalRecordAttachment(
        attachmentId: 3,
        medicalRecordId: 10,
        fileName: 'mri_scan_spine.jpg',
        fileType: 'image/jpeg',
        fileUrl: 'https://r2.example.com/attachments/3',
        fileSize: 4096,
        uploadedAt: testDate,
      );

      expect(att.cleanFileName, equals('mri_scan_spine.jpg'));
      expect(att.isImage, isTrue);
      expect(att.isPdf, isFalse);
    });

    test('detects images by MIME type or extension', () {
      final attWebp = MedicalRecordAttachment(
        attachmentId: 4,
        medicalRecordId: 10,
        fileName: 'scan.webp',
        fileType: 'image/webp',
        fileUrl: 'url',
        fileSize: 500,
        uploadedAt: testDate,
      );

      expect(attWebp.isImage, isTrue);

      final attUnknownName = MedicalRecordAttachment(
        attachmentId: 5,
        medicalRecordId: 10,
        fileName: 'file_without_ext',
        fileType: 'image/png',
        fileUrl: 'url',
        fileSize: 500,
        uploadedAt: testDate,
      );

      expect(attUnknownName.isImage, isTrue);
    });
  });
}
