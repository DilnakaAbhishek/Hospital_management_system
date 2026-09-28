import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Stores credentials separately from non-sensitive UI preferences.
class SecureTokenStorage {
  static const _tokenKey = 'hms_token';
  static const FlutterSecureStorage _storage = FlutterSecureStorage();
  static bool _hasTokenOverride = false;
  static String? _tokenOverride;

  static Future<String?> readToken() => _hasTokenOverride
      ? Future.value(_tokenOverride)
      : _storage.read(key: _tokenKey);
  static Future<void> saveToken(String token) =>
      _storage.write(key: _tokenKey, value: token);
  static Future<void> clearToken() => _storage.delete(key: _tokenKey);

  /// Returns true only when a stored JWT is present, has not expired, and belongs to a Patient.
  /// Expired, non-patient, or malformed tokens are removed so the app returns to Login.
  static Future<bool> hasValidSession() async {
    final token = await readToken();
    final expiresAt = getTokenExpiry(token);
    if (expiresAt == null || !expiresAt.isAfter(DateTime.now().toUtc())) {
      await clearToken();
      return false;
    }
    final role = getTokenRole(token);
    if (role == null || role.toLowerCase() != 'patient') {
      await clearToken();
      return false;
    }
    return true;
  }

  static String? getTokenRole(String? token) {
    if (token == null || token.isEmpty) return null;
    try {
      final parts = token.split('.');
      if (parts.length != 3) return null;
      final payload = jsonDecode(
        utf8.decode(base64Url.decode(base64Url.normalize(parts[1]))),
      ) as Map<String, dynamic>;
      final role = payload['role'] ??
          payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
      return role?.toString();
    } catch (_) {
      return null;
    }
  }

  static DateTime? getTokenExpiry(String? token) {
    if (token == null || token.isEmpty) return null;
    try {
      final parts = token.split('.');
      if (parts.length != 3) return null;
      final payload = jsonDecode(
        utf8.decode(base64Url.decode(base64Url.normalize(parts[1]))),
      ) as Map<String, dynamic>;
      final expiresAtSeconds = payload['exp'];
      if (expiresAtSeconds is! num) return null;
      return DateTime.fromMillisecondsSinceEpoch(
        expiresAtSeconds.toInt() * 1000,
        isUtc: true,
      );
    } catch (_) {
      return null;
    }
  }

  @visibleForTesting
  static void setTokenForTesting(String? token) {
    _hasTokenOverride = true;
    _tokenOverride = token;
  }

  @visibleForTesting
  static void resetTokenForTesting() {
    _hasTokenOverride = false;
    _tokenOverride = null;
  }
}
