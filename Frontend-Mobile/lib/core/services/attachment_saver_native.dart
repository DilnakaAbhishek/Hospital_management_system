import 'dart:io';
import 'dart:typed_data';
import 'package:gal/gal.dart';
import 'package:path_provider/path_provider.dart';
import 'attachment_save_result.dart';

Future<AttachmentSaveResult> saveAttachment({
  required Uint8List bytes,
  required String fileName,
  required bool isImage,
  String? mimeType,
}) async {
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
      await tempFile.writeAsBytes(bytes);

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
      await targetFile.writeAsBytes(bytes);

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
