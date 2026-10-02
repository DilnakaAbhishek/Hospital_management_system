// ignore: avoid_web_libraries_in_flutter, deprecated_member_use
import 'dart:html' as html;
import 'dart:typed_data';
import 'attachment_save_result.dart';

Future<AttachmentSaveResult> saveAttachment({
  Uint8List? bytes,
  String? downloadUrl,
  required String fileName,
  required bool isImage,
  String? mimeType,
}) async {
  try {
    String? targetUrl;
    bool revokeNeeded = false;

    if (downloadUrl != null && downloadUrl.isNotEmpty) {
      targetUrl = downloadUrl;
    } else if (bytes != null && bytes.isNotEmpty) {
      final effectiveMime = (mimeType != null && mimeType.isNotEmpty)
          ? mimeType
          : (isImage ? 'image/jpeg' : 'application/octet-stream');
      final blob = html.Blob([bytes], effectiveMime);
      targetUrl = html.Url.createObjectUrlFromBlob(blob);
      revokeNeeded = true;
    } else {
      throw Exception('No download URL or file data provided.');
    }

    final anchor = html.AnchorElement(href: targetUrl)
      ..setAttribute('download', fileName)
      ..style.display = 'none';

    html.document.body?.children.add(anchor);
    anchor.click();
    anchor.remove();

    if (revokeNeeded) {
      Future.delayed(const Duration(seconds: 10), () {
        html.Url.revokeObjectUrl(targetUrl!);
      });
    }

    return AttachmentSaveResult(
      success: true,
      type: isImage ? AttachmentSaveType.galleryImage : AttachmentSaveType.downloadedFile,
      message: isImage
          ? 'Image downloaded ($fileName)'
          : 'File downloaded ($fileName)',
    );
  } catch (e) {
    return AttachmentSaveResult(
      success: false,
      type: isImage ? AttachmentSaveType.galleryImage : AttachmentSaveType.downloadedFile,
      message: 'Failed to download: $e',
    );
  }
}

Future<void> openGalleryApp() async {}
