import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

class ThemeController {
  ThemeController._();

  static const _storageKey = 'patient_theme_mode';
  static final ValueNotifier<ThemeMode> mode = ValueNotifier<ThemeMode>(ThemeMode.light);

  static Future<void> load() async {
    final prefs = await SharedPreferences.getInstance();
    mode.value = _modeFromName(prefs.getString(_storageKey));
  }

  static Future<void> setMode(ThemeMode nextMode) async {
    mode.value = nextMode;
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_storageKey, nextMode.name);
  }

  static Future<void> toggleDarkMode(bool enabled) {
    return setMode(enabled ? ThemeMode.dark : ThemeMode.light);
  }

  static ThemeMode _modeFromName(String? name) {
    return switch (name) {
      'light' => ThemeMode.light,
      'dark' => ThemeMode.dark,
      _ => ThemeMode.light,
    };
  }
}
