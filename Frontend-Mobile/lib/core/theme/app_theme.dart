import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:medicore_mobile/core/constants/app_colors.dart';

/// AppTheme mirrors the web app's design system.
/// Light theme = web :root {}  /  Dark theme = web body.dark {}
class AppTheme {
  AppTheme._();

  // ── Typography (Inter-inspired sizes matching web rem scale) ─────────────────
  static const _fontFamily = 'Inter'; // load via pubspec.yaml or Google Fonts

  // ─── Light Theme ─────────────────────────────────────────────────────────────
  static ThemeData get lightTheme {
    const colorScheme = ColorScheme(
      brightness: Brightness.light,
      primary:          AppColors.primary,
      onPrimary:        Colors.white,
      primaryContainer: AppColors.primaryLight,
      onPrimaryContainer: AppColors.primaryDark,
      secondary:        AppColors.accent,
      onSecondary:      Colors.white,
      secondaryContainer: Color(0xFFEEF2FF), // indigo-50
      onSecondaryContainer: AppColors.accentDark,
      surface:          AppColors.bgLightCard,  // --bg-card / --bg-dark
      onSurface:        AppColors.textPrimaryLight,
      onSurfaceVariant: AppColors.textSecondaryLight,
      error:            AppColors.danger,
      onError:          Colors.white,
      outline:          AppColors.borderLight,
      outlineVariant:   AppColors.surfaceLight,
      shadow:           AppColors.shadowColorLight,
      scrim:            Colors.black,
      inverseSurface:   AppColors.bgDark,
      onInverseSurface: Colors.white,
      inversePrimary:   AppColors.primaryLight,
    );

    return ThemeData(
      useMaterial3: true,
      brightness: Brightness.light,
      colorScheme: colorScheme,
      fontFamily: _fontFamily,

      // Scaffold = --bg-base (#f1f5f9)
      scaffoldBackgroundColor: AppColors.bgLight,

      // StatusBar
      appBarTheme: const AppBarTheme(
        backgroundColor: AppColors.bgLightCard, // --bg-card
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        shadowColor: Colors.transparent,
        centerTitle: false,
        iconTheme: IconThemeData(color: AppColors.textPrimaryLight),
        titleTextStyle: TextStyle(
          color: AppColors.textPrimaryLight,
          fontSize: 18,
          fontWeight: FontWeight.w700,
          fontFamily: _fontFamily,
        ),
        systemOverlayStyle: SystemUiOverlayStyle(
          statusBarColor: Colors.transparent,
          statusBarIconBrightness: Brightness.dark,
          statusBarBrightness: Brightness.light,
        ),
      ),

      // Cards = --bg-card + --shadow-sm + --border-default
      cardTheme: CardThemeData(
        color: AppColors.bgLightCard,
        elevation: 0,
        shadowColor: AppColors.shadowColorLight.withValues(alpha: 0.05),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(14), // --radius-lg
          side: const BorderSide(color: AppColors.borderLight),
        ),
        margin: const EdgeInsets.all(0),
      ),

      // Dividers = --border-default
      dividerTheme: const DividerThemeData(
        color: AppColors.borderLight,
        space: 1,
        thickness: 1,
      ),

      // Inputs = --bg-card fill + --border-default + focus --border-focus
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: AppColors.bgLightCard,
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        hintStyle: const TextStyle(
          color: AppColors.textMutedLight,
          fontSize: 14,
          fontFamily: _fontFamily,
        ),
        labelStyle: const TextStyle(
          color: AppColors.textSecondaryLight,
          fontSize: 14,
          fontFamily: _fontFamily,
        ),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10), // --radius-md
          borderSide: const BorderSide(color: AppColors.borderLight),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10),
          borderSide: const BorderSide(color: AppColors.borderLight),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10),
          borderSide: const BorderSide(color: AppColors.borderFocusLight, width: 2),
        ),
        errorBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10),
          borderSide: const BorderSide(color: AppColors.danger),
        ),
        focusedErrorBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10),
          borderSide: const BorderSide(color: AppColors.danger, width: 2),
        ),
      ),

      // Elevated buttons = primary fill
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: AppColors.primary,
          foregroundColor: Colors.white,
          elevation: 2,
          shadowColor: AppColors.primary.withValues(alpha: 0.2),
          padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 14),
          minimumSize: const Size(0, 48),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(10),
          ),
          textStyle: const TextStyle(
            fontWeight: FontWeight.w700,
            fontSize: 15,
            fontFamily: _fontFamily,
          ),
        ),
      ),

      // Outlined buttons
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: AppColors.primary,
          side: const BorderSide(color: AppColors.borderLight),
          padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 14),
          minimumSize: const Size(0, 48),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(10),
          ),
          textStyle: const TextStyle(
            fontWeight: FontWeight.w600,
            fontSize: 15,
            fontFamily: _fontFamily,
          ),
        ),
      ),

      // Text buttons
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: AppColors.primary,
          textStyle: const TextStyle(
            fontWeight: FontWeight.w600,
            fontSize: 14,
            fontFamily: _fontFamily,
          ),
        ),
      ),

      // Chips
      chipTheme: ChipThemeData(
        backgroundColor: AppColors.surfaceLight,
        labelStyle: const TextStyle(
          color: AppColors.textSecondaryLight,
          fontSize: 13,
          fontFamily: _fontFamily,
        ),
        side: const BorderSide(color: AppColors.borderLight),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
      ),

      // ListTile
      listTileTheme: const ListTileThemeData(
        iconColor: AppColors.textMutedLight,
        titleTextStyle: TextStyle(
          color: AppColors.textPrimaryLight,
          fontSize: 15,
          fontWeight: FontWeight.w500,
          fontFamily: _fontFamily,
        ),
        subtitleTextStyle: TextStyle(
          color: AppColors.textMutedLight,
          fontSize: 13,
          fontFamily: _fontFamily,
        ),
      ),

      // Bottom Navigation
      bottomNavigationBarTheme: const BottomNavigationBarThemeData(
        backgroundColor: AppColors.bgLightCard,
        selectedItemColor: AppColors.primary,
        unselectedItemColor: AppColors.textMutedLight,
        elevation: 8,
        type: BottomNavigationBarType.fixed,
        selectedLabelStyle: TextStyle(fontWeight: FontWeight.w600, fontSize: 12, fontFamily: _fontFamily),
        unselectedLabelStyle: TextStyle(fontWeight: FontWeight.w500, fontSize: 12, fontFamily: _fontFamily),
      ),

      // Navigation Bar (Material 3)
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: AppColors.bgLightCard,
        indicatorColor: AppColors.primary.withValues(alpha: 0.12),
        iconTheme: WidgetStateProperty.resolveWith((states) {
          if (states.contains(WidgetState.selected)) {
            return const IconThemeData(color: AppColors.primary);
          }
          return const IconThemeData(color: AppColors.textMutedLight);
        }),
        labelTextStyle: WidgetStateProperty.resolveWith((states) {
          if (states.contains(WidgetState.selected)) {
            return const TextStyle(color: AppColors.primary, fontWeight: FontWeight.w600, fontSize: 12, fontFamily: _fontFamily);
          }
          return const TextStyle(color: AppColors.textMutedLight, fontSize: 12, fontFamily: _fontFamily);
        }),
      ),

      // Drawer
      drawerTheme: const DrawerThemeData(
        backgroundColor: AppColors.bgLightCard,
        elevation: 0,
        shadowColor: Colors.transparent,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.zero),
      ),

      // Switches / Checkboxes / Radios
      switchTheme: SwitchThemeData(
        thumbColor: WidgetStateProperty.resolveWith((s) =>
            s.contains(WidgetState.selected) ? AppColors.primary : AppColors.textMutedLight),
        trackColor: WidgetStateProperty.resolveWith((s) =>
            s.contains(WidgetState.selected)
                ? AppColors.primary.withValues(alpha: 0.3)
                : AppColors.surfaceLight),
      ),
      checkboxTheme: CheckboxThemeData(
        fillColor: WidgetStateProperty.resolveWith((s) =>
            s.contains(WidgetState.selected) ? AppColors.primary : Colors.transparent),
        side: const BorderSide(color: AppColors.borderLight, width: 1.5),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(4)),
      ),

      // Text Theme
      textTheme: const TextTheme(
        displayLarge:  TextStyle(fontSize: 32, fontWeight: FontWeight.w800, color: AppColors.textPrimaryLight, fontFamily: _fontFamily, height: 1.2),
        displayMedium: TextStyle(fontSize: 24, fontWeight: FontWeight.w700, color: AppColors.textPrimaryLight, fontFamily: _fontFamily, height: 1.3),
        displaySmall:  TextStyle(fontSize: 20, fontWeight: FontWeight.w600, color: AppColors.textPrimaryLight, fontFamily: _fontFamily, height: 1.4),
        headlineLarge: TextStyle(fontSize: 28, fontWeight: FontWeight.w700, color: AppColors.textPrimaryLight, fontFamily: _fontFamily),
        headlineMedium:TextStyle(fontSize: 22, fontWeight: FontWeight.w600, color: AppColors.textPrimaryLight, fontFamily: _fontFamily),
        headlineSmall: TextStyle(fontSize: 18, fontWeight: FontWeight.w600, color: AppColors.textPrimaryLight, fontFamily: _fontFamily),
        titleLarge:    TextStyle(fontSize: 18, fontWeight: FontWeight.w700, color: AppColors.textPrimaryLight, fontFamily: _fontFamily),
        titleMedium:   TextStyle(fontSize: 16, fontWeight: FontWeight.w600, color: AppColors.textPrimaryLight, fontFamily: _fontFamily),
        titleSmall:    TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.textPrimaryLight, fontFamily: _fontFamily),
        bodyLarge:     TextStyle(fontSize: 16, color: AppColors.textPrimaryLight, fontFamily: _fontFamily, height: 1.6),
        bodyMedium:    TextStyle(fontSize: 14, color: AppColors.textSecondaryLight, fontFamily: _fontFamily, height: 1.6),
        bodySmall:     TextStyle(fontSize: 12, color: AppColors.textMutedLight, fontFamily: _fontFamily, height: 1.5),
        labelLarge:    TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.textPrimaryLight, fontFamily: _fontFamily),
        labelMedium:   TextStyle(fontSize: 12, fontWeight: FontWeight.w500, color: AppColors.textSecondaryLight, fontFamily: _fontFamily),
        labelSmall:    TextStyle(fontSize: 11, fontWeight: FontWeight.w500, color: AppColors.textMutedLight, fontFamily: _fontFamily, letterSpacing: 0.5),
      ),

      // Tooltip
      tooltipTheme: TooltipThemeData(
        decoration: BoxDecoration(
          color: AppColors.textPrimaryLight.withValues(alpha: 0.9),
          borderRadius: BorderRadius.circular(6),
        ),
        textStyle: const TextStyle(color: Colors.white, fontSize: 13, fontFamily: _fontFamily),
      ),

      // SnackBar
      snackBarTheme: SnackBarThemeData(
        backgroundColor: AppColors.textPrimaryLight,
        contentTextStyle: const TextStyle(color: Colors.white, fontSize: 14, fontFamily: _fontFamily),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
        behavior: SnackBarBehavior.floating,
      ),
    );
  }

  // ─── Dark Theme ──────────────────────────────────────────────────────────────
  static ThemeData get darkTheme {
    const colorScheme = ColorScheme(
      brightness: Brightness.dark,
      primary:           AppColors.primaryDarkTheme, // web --clr-primary
      onPrimary:         Colors.white,
      primaryContainer:  AppColors.primaryDark,
      onPrimaryContainer: AppColors.primaryLight,
      secondary:         Color(0xFF6366F1),         // web --clr-accent
      onSecondary:       Colors.white,
      secondaryContainer:AppColors.accentDark,
      onSecondaryContainer: Colors.white,
      surface:           AppColors.bgDarkCard,      // --bg-card (dark)
      onSurface:         AppColors.textPrimaryDark,
      onSurfaceVariant:  AppColors.textSecondaryDark,
      error:             Color(0xFFEF4444),
      onError:           Colors.white,
      outline:           AppColors.borderDark,
      outlineVariant:    AppColors.surfaceDark,
      shadow:            AppColors.shadowColorDark,
      scrim:             Colors.black,
      inverseSurface:    AppColors.bgLightCard,
      onInverseSurface:  AppColors.textPrimaryLight,
      inversePrimary:    AppColors.primary,
    );

    return ThemeData(
      useMaterial3: true,
      brightness: Brightness.dark,
      colorScheme: colorScheme,
      fontFamily: _fontFamily,
      scaffoldBackgroundColor: AppColors.bgDark,

      appBarTheme: const AppBarTheme(
        backgroundColor: AppColors.surfaceDark,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        shadowColor: Colors.transparent,
        centerTitle: false,
        iconTheme: IconThemeData(color: AppColors.textPrimaryDark),
        titleTextStyle: TextStyle(
          color: AppColors.textPrimaryDark,
          fontSize: 18,
          fontWeight: FontWeight.w700,
          fontFamily: _fontFamily,
        ),
        systemOverlayStyle: SystemUiOverlayStyle(
          statusBarColor: Colors.transparent,
          statusBarIconBrightness: Brightness.light,
          statusBarBrightness: Brightness.dark,
        ),
      ),

      cardTheme: CardThemeData(
        color: AppColors.bgDarkCard,
        elevation: 0,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(14),
          side: const BorderSide(color: AppColors.borderDark),
        ),
        margin: const EdgeInsets.all(0),
      ),

      dividerTheme: const DividerThemeData(
        color: AppColors.borderDark,
        space: 1,
        thickness: 1,
      ),

      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: AppColors.surfaceDarkSecondary,
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        hintStyle: const TextStyle(color: AppColors.textMutedDark, fontSize: 14, fontFamily: _fontFamily),
        labelStyle: const TextStyle(color: AppColors.textSecondaryDark, fontSize: 14, fontFamily: _fontFamily),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10),
          borderSide: const BorderSide(color: AppColors.borderDark),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10),
          borderSide: const BorderSide(color: AppColors.borderDark),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10),
          borderSide: const BorderSide(color: AppColors.borderFocusDark, width: 2),
        ),
        errorBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10),
          borderSide: const BorderSide(color: AppColors.danger),
        ),
        focusedErrorBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10),
          borderSide: const BorderSide(color: AppColors.danger, width: 2),
        ),
      ),

      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: AppColors.primaryDarkTheme,
          foregroundColor: Colors.white,
          elevation: 0,
          padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 14),
          minimumSize: const Size(0, 48),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
          textStyle: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15, fontFamily: _fontFamily),
        ),
      ),

      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: AppColors.primaryDarkTheme,
          side: const BorderSide(color: AppColors.borderDark),
          padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 14),
          minimumSize: const Size(0, 48),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
          textStyle: const TextStyle(fontWeight: FontWeight.w600, fontSize: 15, fontFamily: _fontFamily),
        ),
      ),

      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: AppColors.primaryDarkTheme,
          textStyle: const TextStyle(fontWeight: FontWeight.w600, fontSize: 14, fontFamily: _fontFamily),
        ),
      ),

      chipTheme: ChipThemeData(
        backgroundColor: AppColors.surfaceDark,
        labelStyle: const TextStyle(color: AppColors.textSecondaryDark, fontSize: 13, fontFamily: _fontFamily),
        side: const BorderSide(color: AppColors.borderDark),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
      ),

      listTileTheme: const ListTileThemeData(
        iconColor: AppColors.textMutedDark,
        titleTextStyle: TextStyle(color: AppColors.textPrimaryDark, fontSize: 15, fontWeight: FontWeight.w500, fontFamily: _fontFamily),
        subtitleTextStyle: TextStyle(color: AppColors.textMutedDark, fontSize: 13, fontFamily: _fontFamily),
      ),

      bottomNavigationBarTheme: const BottomNavigationBarThemeData(
        backgroundColor: AppColors.surfaceDark,
        selectedItemColor: AppColors.primaryLight,
        unselectedItemColor: AppColors.textMutedDark,
        elevation: 0,
        type: BottomNavigationBarType.fixed,
        selectedLabelStyle: TextStyle(fontWeight: FontWeight.w600, fontSize: 12, fontFamily: _fontFamily),
        unselectedLabelStyle: TextStyle(fontWeight: FontWeight.w500, fontSize: 12, fontFamily: _fontFamily),
      ),

      drawerTheme: const DrawerThemeData(
        backgroundColor: AppColors.surfaceDark,
        elevation: 0,
        shadowColor: Colors.transparent,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.zero),
      ),

      switchTheme: SwitchThemeData(
        thumbColor: WidgetStateProperty.resolveWith((s) =>
            s.contains(WidgetState.selected) ? AppColors.primaryLight : AppColors.textMutedDark),
        trackColor: WidgetStateProperty.resolveWith((s) =>
            s.contains(WidgetState.selected)
                ? AppColors.primaryLight.withValues(alpha: 0.3)
                : AppColors.surfaceDark),
      ),

      snackBarTheme: SnackBarThemeData(
        backgroundColor: AppColors.bgDarkCard,
        contentTextStyle: const TextStyle(color: AppColors.textPrimaryDark, fontSize: 14, fontFamily: _fontFamily),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
        behavior: SnackBarBehavior.floating,
      ),

      textTheme: const TextTheme(
        displayLarge:  TextStyle(fontSize: 32, fontWeight: FontWeight.w800, color: AppColors.textPrimaryDark, fontFamily: _fontFamily, height: 1.2),
        displayMedium: TextStyle(fontSize: 24, fontWeight: FontWeight.w700, color: AppColors.textPrimaryDark, fontFamily: _fontFamily, height: 1.3),
        displaySmall:  TextStyle(fontSize: 20, fontWeight: FontWeight.w600, color: AppColors.textPrimaryDark, fontFamily: _fontFamily, height: 1.4),
        headlineLarge: TextStyle(fontSize: 28, fontWeight: FontWeight.w700, color: AppColors.textPrimaryDark, fontFamily: _fontFamily),
        headlineMedium:TextStyle(fontSize: 22, fontWeight: FontWeight.w600, color: AppColors.textPrimaryDark, fontFamily: _fontFamily),
        headlineSmall: TextStyle(fontSize: 18, fontWeight: FontWeight.w600, color: AppColors.textPrimaryDark, fontFamily: _fontFamily),
        titleLarge:    TextStyle(fontSize: 18, fontWeight: FontWeight.w700, color: AppColors.textPrimaryDark, fontFamily: _fontFamily),
        titleMedium:   TextStyle(fontSize: 16, fontWeight: FontWeight.w600, color: AppColors.textPrimaryDark, fontFamily: _fontFamily),
        titleSmall:    TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.textPrimaryDark, fontFamily: _fontFamily),
        bodyLarge:     TextStyle(fontSize: 16, color: AppColors.textPrimaryDark, fontFamily: _fontFamily, height: 1.6),
        bodyMedium:    TextStyle(fontSize: 14, color: AppColors.textSecondaryDark, fontFamily: _fontFamily, height: 1.6),
        bodySmall:     TextStyle(fontSize: 12, color: AppColors.textMutedDark, fontFamily: _fontFamily, height: 1.5),
        labelLarge:    TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.textPrimaryDark, fontFamily: _fontFamily),
        labelMedium:   TextStyle(fontSize: 12, fontWeight: FontWeight.w500, color: AppColors.textSecondaryDark, fontFamily: _fontFamily),
        labelSmall:    TextStyle(fontSize: 11, fontWeight: FontWeight.w500, color: AppColors.textMutedDark, fontFamily: _fontFamily, letterSpacing: 0.5),
      ),
    );
  }
}
