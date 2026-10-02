import 'dart:io';
import 'dart:typed_data';
import 'package:gal/gal.dart';
import 'package:http/http.dart' as http;
import 'package:path_provider/path_provider.dart';
import 'attachment_save_result.dart';

Future<AttachmentSaveResult> saveAttachment({
  Uint8List? bytes,
  String? downloadUrl,
  required String fileName,
  required bool isImage,
  String? mimeType,
}) async {
  Uint8List? fileBytes = bytes;

  if ((fileBytes == null || fileBytes.isEmpty) && downloadUrl != null && downloadUrl.isNotEmpty) {
    try {
      final response = await http.get(Uri.parse(downloadUrl));
      if (response.statusCode >= 200 && response.statusCode < 300) {
        fileBytes = response.bodyBytes;
      }
    } catch (_) {}
  }

  if (fileBytes == null || fileBytes.isEmpty) {
    return const AttachmentSaveResult(
      success: false,
      type: AttachmentSaveType.downloadedFile,
      message: 'Could not obtain attachment data to save.',
    );
  }

  if (isImage) {
    try {
      final hasAccess = await Gal.hasAccess();
      if (!hasAccess) {
        final granted = await Gal.requestAccess();
        if (!granted) {
          return const AttachmentSaveResult(
            success: false,
            type: AttachmentSaveType.galleryImage,
            message: 'Gallery permission was denied.',
          );
        }
      }

      final tempDir = await getTemporaryDirectory();
      final tempFile = File('${tempDir.path}/$fileName');
      await tempFile.writeAsBytes(fileBytes);

      await Gal.putImage(tempFile.path, album: 'MediCore');
      try {
        await tempFile.delete();
      } catch (_) {}

      return AttachmentSaveResult(
        success: true,
        type: AttachmentSaveType.galleryImage,
        message: 'Image saved to photo gallery: $fileName',
      );
    } catch (e) {
      return AttachmentSaveResult(
        success: false,
        type: AttachmentSaveType.galleryImage,
        message: 'Could not save image to gallery: $e',
      );
    }
  } else {
    try {
      Directory? targetDir;
      if (Platform.isAndroid) {
        final publicDownload = Directory('/storage/emulated/0/Download');
        if (await publicDownload.exists()) {
          targetDir = publicDownload;
        }
      }
      targetDir ??= await getDownloadsDirectory() ?? await getApplicationDocumentsDirectory();

      if (!await targetDir.exists()) {
        await targetDir.create(recursive: true);
      }

      final targetFile = File('${targetDir.path}/$fileName');
      await targetFile.writeAsBytes(fileBytes);

      return AttachmentSaveResult(
        success: true,
        type: AttachmentSaveType.downloadedFile,
        message: 'Saved to Downloads: $fileName',
        savedPath: targetFile.path,
      );
    } catch (e) {
      return AttachmentSaveResult(
        success: false,
        type: AttachmentSaveType.downloadedFile,
        message: 'Could not save document: $e',
      );
    }
  }
}

Future<void> openGalleryApp() async {
  try {
    await Gal.open();
  } catch (_) {}
}
