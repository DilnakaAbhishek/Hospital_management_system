import 'dart:typed_data';
import 'attachment_save_result.dart';

Future<AttachmentSaveResult> saveAttachment({
  required Uint8List bytes,
  required String fileName,
  required bool isImage,
  String? mimeType,
}) async {
  throw UnsupportedError('Saving attachments is not supported on this platform.');
}

Future<void> openGalleryApp() async {}
