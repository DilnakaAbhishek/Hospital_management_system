import 'package:flutter/material.dart';
import 'package:medicore_mobile/core/constants/app_colors.dart';

class ActivityPulsePainter extends CustomPainter {
  final Color color;
  final double strokeWidth;

  ActivityPulsePainter({required this.color, this.strokeWidth = 2.5});

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = color
      ..style = PaintingStyle.stroke
      ..strokeWidth = strokeWidth
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round;

    final path = Path();
    final w = size.width;
    final h = size.height;

    // Matches lucide-react Activity icon: "22 12 18 12 15 21 9 3 6 12 2 12"
    path.moveTo(w * (2 / 24), h * (12 / 24));
    path.lineTo(w * (6 / 24), h * (12 / 24));
    path.lineTo(w * (9 / 24), h * (3 / 24));
    path.lineTo(w * (15 / 24), h * (21 / 24));
    path.lineTo(w * (18 / 24), h * (12 / 24));
    path.lineTo(w * (22 / 24), h * (12 / 24));

    canvas.drawPath(path, paint);
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}

class MediCoreLogo extends StatelessWidget {
  final double size;
  final double borderRadius;
  final bool showText;
  final double fontSize;

  const MediCoreLogo({
    super.key,
    this.size = 46.0,
    this.borderRadius = 12.0,
    this.showText = false,
    this.fontSize = 24.0,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;

    Widget logoBox = Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: [AppColors.primary, AppColors.accent],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(borderRadius),
        boxShadow: [
          BoxShadow(
            color: AppColors.primary.withValues(alpha: 0.35),
            blurRadius: size * 0.3,
            offset: Offset(0, size * 0.1),
          )
        ],
      ),
      child: Center(
        child: CustomPaint(
          size: Size(size * 0.55, size * 0.55),
          painter: ActivityPulsePainter(
            color: Colors.white,
            strokeWidth: size * 0.065,
          ),
        ),
      ),
    );

    if (!showText) return logoBox;

    return Row(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.center,
      children: [
        logoBox,
        const SizedBox(width: 12),
        RichText(
          text: TextSpan(
            style: TextStyle(
              fontSize: fontSize,
              fontWeight: FontWeight.w800,
              color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
              letterSpacing: -0.5,
            ),
            children: const [
              TextSpan(text: 'Medi'),
              TextSpan(
                text: 'Core',
                style: TextStyle(color: AppColors.primary),
              ),
            ],
          ),
        ),
      ],
    );
  }
}
