enum AttachmentSaveType {
  galleryImage,
  downloadedFile,
}

class AttachmentSaveResult {
  final bool success;
  final AttachmentSaveType type;
  final String message;
  final String? savedPath;

  const AttachmentSaveResult({
    required this.success,
    required this.type,
    required this.message,
    this.savedPath,
  });
}
