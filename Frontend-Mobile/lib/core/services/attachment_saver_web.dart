// ignore: avoid_web_libraries_in_flutter, deprecated_member_use
import 'dart:html' as html;
import 'dart:typed_data';
import 'attachment_save_result.dart';

Future<AttachmentSaveResult> saveAttachment({
  required Uint8List bytes,
  required String fileName,
  required bool isImage,
  String? mimeType,
}) async {
  try {
    final effectiveMime = (mimeType != null && mimeType.isNotEmpty)
        ? mimeType
        : (isImage ? 'image/jpeg' : 'application/octet-stream');
    final blob = html.Blob([bytes], effectiveMime);
    final url = html.Url.createObjectUrlFromBlob(blob);
    final anchor = html.AnchorElement(href: url)
      ..setAttribute('download', fileName)
      ..style.display = 'none';

    html.document.body?.children.add(anchor);
    anchor.click();
    anchor.remove();
    html.Url.revokeObjectUrl(url);

    return AttachmentSaveResult(
      success: true,
      type: isImage ? AttachmentSaveType.galleryImage : AttachmentSaveType.downloadedFile,
      message: isImage
          ? 'Image downloaded directly ($fileName)'
          : 'File downloaded directly ($fileName)',
    );
  } catch (e) {
    return AttachmentSaveResult(
      success: false,
      type: isImage ? AttachmentSaveType.galleryImage : AttachmentSaveType.downloadedFile,
      message: 'Failed to download file on web: $e',
    );
  }
}

Future<void> openGalleryApp() async {}
