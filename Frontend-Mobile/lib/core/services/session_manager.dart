import 'dart:async';
import 'package:flutter/material.dart';
import 'package:medicore_mobile/core/services/secure_token_storage.dart';
import 'package:medicore_mobile/features/auth/screens/login_screen.dart';
import 'package:shared_preferences/shared_preferences.dart';

class SessionManager {
  static final GlobalKey<NavigatorState> navigatorKey = GlobalKey<NavigatorState>();
  static Timer? _sessionTimer;

  static Future<void> startSession() async {
    _sessionTimer?.cancel();
    
    final token = await SecureTokenStorage.readToken();
    final expiresAt = SecureTokenStorage.getTokenExpiry(token);
    
    if (expiresAt != null) {
      final timeUntilExpiry = expiresAt.difference(DateTime.now().toUtc());
      if (timeUntilExpiry.isNegative) {
        await logout();
      } else {
        _sessionTimer = Timer(timeUntilExpiry, () {
          logout();
        });
      }
    }
  }

  static Future<void> logout() async {
    _sessionTimer?.cancel();
    await SecureTokenStorage.clearToken();
    
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove('patient_user_id');
    await prefs.remove('patient_full_name');
    await prefs.remove('user_role');
    
    if (navigatorKey.currentContext != null) {
      Navigator.of(navigatorKey.currentContext!).pushAndRemoveUntil(
        MaterialPageRoute(builder: (context) => const LoginScreen()),
        (Route<dynamic> route) => false,
      );
    }
  }
}
