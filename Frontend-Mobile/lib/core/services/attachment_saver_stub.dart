import 'dart:typed_data';
import 'attachment_save_result.dart';

Future<AttachmentSaveResult> saveAttachment({
  Uint8List? bytes,
  String? downloadUrl,
  required String fileName,
  required bool isImage,
  String? mimeType,
}) async {
  throw UnsupportedError('Saving attachments is not supported on this platform.');
}

Future<void> openGalleryApp() async {}
