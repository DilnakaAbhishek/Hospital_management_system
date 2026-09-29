import 'package:flutter/material.dart';
import 'package:medicore_mobile/core/theme/app_theme.dart';
import 'package:medicore_mobile/core/theme/theme_controller.dart';
import 'package:medicore_mobile/core/services/secure_token_storage.dart';
import 'package:medicore_mobile/features/auth/screens/login_screen.dart';
import 'package:medicore_mobile/layouts/dashboard_layout.dart';
import 'package:medicore_mobile/core/services/session_manager.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await ThemeController.load();
  final hasValidSession = await SecureTokenStorage.hasValidSession();
  if (hasValidSession) {
    SessionManager.startSession();
  }
  runApp(MediCoreMobileApp(
    initialHome:
        hasValidSession ? const DashboardLayout() : const LoginScreen(),
  ));
}

class MediCoreMobileApp extends StatelessWidget {
  final Widget? initialHome;

  const MediCoreMobileApp({super.key, this.initialHome});

  @override
  Widget build(BuildContext context) {
    return ValueListenableBuilder<ThemeMode>(
      valueListenable: ThemeController.mode,
      builder: (context, themeMode, _) {
        return MaterialApp(
          navigatorKey: SessionManager.navigatorKey,
          title: 'MediCore',
          debugShowCheckedModeBanner: false,
          theme: AppTheme.lightTheme,
          darkTheme: AppTheme.darkTheme,
          themeMode: themeMode,
          home: initialHome ?? const LoginScreen(),
        );
      },
    );
  }
}
