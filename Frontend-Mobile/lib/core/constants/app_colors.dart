import 'package:flutter/material.dart';

/// Color palette that mirrors the web app's CSS design tokens exactly.
/// Light theme maps to :root {} and dark theme maps to body.dark {}
class AppColors {
  AppColors._();

  // ── Primary ─────────────────────────────────────────────────────────────────
  /// Web: --clr-primary (light)
  static const Color primaryDarkTheme = Color(0xFF0EA5E9);
  static const Color primary = Color(0xFF0284C7);
  /// Web: --clr-primary-dark
  static const Color primaryDark = Color(0xFF0369A1);
  /// Web: --clr-primary-light
  static const Color primaryLight = Color(0xFF38BDF8);

  // ── Accent ───────────────────────────────────────────────────────────────────
  /// Web: --clr-accent
  static const Color accent = Color(0xFF4F46E5);
  /// Web: --clr-accent-dark
  static const Color accentDark = Color(0xFF4338CA);

  // ── Semantic ─────────────────────────────────────────────────────────────────
  /// Web: --clr-success
  static const Color success = Color(0xFF059669);
  /// Web: --clr-warning
  static const Color warning = Color(0xFFD97706);
  /// Web: --clr-danger
  static const Color danger = Color(0xFFDC2626);
  /// Web: --clr-info
  static const Color info = Color(0xFF0891B2);

  // ── Light Theme Backgrounds ──────────────────────────────────────────────────
  /// Web: --bg-base  (#f1f5f9)
  static const Color bgLight = Color(0xFFF1F5F9);
  /// Web: --bg-dark / --bg-card  (#f8fafc)
  static const Color bgLightCard = Color(0xFFF8FAFC);
  /// Web: --bg-surface  (#e2e8f0)
  static const Color surfaceLight = Color(0xFFE2E8F0);
  /// Web: --bg-surface-2  (#d1dce8)
  static const Color surfaceLight2 = Color(0xFFD1DCE8);
  /// Web: --bg-glass
  static const Color glassLight = Color(0xE6FFFFFF); // rgba(255,255,255,0.9)

  // ── Light Theme Text ────────────────────────────────────────────────────────
  /// Web: --text-primary  (#0f172a)
  static const Color textPrimaryLight = Color(0xFF0F172A);
  /// Web: --text-secondary  (#475569)
  static const Color textSecondaryLight = Color(0xFF475569);
  /// Web: --text-muted  (#64748b)
  static const Color textMutedLight = Color(0xFF64748B);
  /// Web: --text-accent  (#0284c7)
  static const Color textAccentLight = Color(0xFF0284C7);

  // ── Light Theme Borders ─────────────────────────────────────────────────────
  /// Web: --border-default  (#cbd5e1)
  static const Color borderLight = Color(0xFFCBD5E1);
  /// Web: --border-focus  (#0284c7)
  static const Color borderFocusLight = Color(0xFF0284C7);

  // ── Dark Theme Backgrounds ───────────────────────────────────────────────────
  /// Web: --bg-base (dark)  (#242424)
  static const Color bgDark = Color(0xFF242424);
  /// Web: --bg-dark  (#161616)
  static const Color bgDarkDeep = Color(0xFF161616);
  /// Web: --bg-surface (dark)  (#2b2b2b)
  static const Color surfaceDark = Color(0xFF2B2B2B);
  /// Web: --bg-surface-2 (dark)  (#353535)
  static const Color surfaceDarkSecondary = Color(0xFF353535);
  /// Web: --bg-card (dark)  (#303030)
  static const Color bgDarkCard = Color(0xFF303030);

  // ── Dark Theme Text ──────────────────────────────────────────────────────────
  static const Color textPrimaryDark = Color(0xFFF5F5F5);
  static const Color textSecondaryDark = Color(0xFFD4D4D4);
  static const Color textMutedDark = Color(0xFFA3A3A3);

  // ── Dark Theme Borders ───────────────────────────────────────────────────────
  static const Color borderDark = Color(0x1FD4D4D4); // rgba(212,212,212,0.12)
  static const Color borderFocusDark = Color(0x800EA5E9); // rgba(14,165,233,0.5)

  // ── Shadows (pre-computed for Flutter BoxShadow) ────────────────────────────
  static const Color shadowColorLight = Color(0xFF334155); // slate-700 base
  static const Color shadowColorDark = Color(0xFF000000);
}
