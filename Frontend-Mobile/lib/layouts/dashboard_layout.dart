import 'dart:async';
import 'dart:io' show Platform;
import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:medicore_mobile/core/constants/app_colors.dart';
import 'package:medicore_mobile/core/services/api_service.dart';
import 'package:medicore_mobile/core/services/session_manager.dart';
import 'package:medicore_mobile/core/theme/theme_controller.dart';
import 'package:medicore_mobile/core/widgets/medicore_logo.dart';
import 'package:medicore_mobile/features/doctors/screens/doctor_search_screen.dart';
import 'package:medicore_mobile/features/medical_records/screens/medical_records_screen.dart';
import 'package:medicore_mobile/features/profile/screens/profile_screen.dart';
import 'package:medicore_mobile/features/assistant/screens/hospital_assistant_screen.dart';
import 'package:medicore_mobile/models/appointment.dart';
import 'package:medicore_mobile/models/doctor.dart';
import 'package:medicore_mobile/models/patient.dart';

enum _PatientSection {
  home,
  profile,
  appointments,
  doctors,
  records,
  assistant,
  settings,
  support,
}

enum _AppointmentListMode { upcoming, history }

class DashboardLayout extends StatefulWidget {
  final Widget? body;
  final String? title;
  final String? subtitle;

  const DashboardLayout({
    super.key,
    this.body,
    this.title,
    this.subtitle,
  });

  @override
  State<DashboardLayout> createState() => _DashboardLayoutState();
}

class _DashboardLayoutState extends State<DashboardLayout> {
  _PatientSection _activeSection = _PatientSection.home;
  String _userName = 'Patient User';
  String _userEmail = 'patient@medicore.lk';
  String _userInitials = 'PU';
  String? _appointmentAction;
  int? _appointmentDoctorId;
  int _appointmentActionVersion = 0;
  Appointment? _nextAppointment;
  bool _loadingNextAppointment = true;
  String? _nextAppointmentError;

  Timer? _notificationTimer;
  List<Map<String, dynamic>> _notifications = [];
  Set<String> _readNotificationIds = {};
  bool _loadingNotifications = false;
  final MenuController _notificationMenu = MenuController();
  String? _notificationError;

  bool get _isTesting {
    if (kIsWeb) return false;
    return Platform.environment.containsKey('FLUTTER_TEST');
  }

  @override
  void initState() {
    super.initState();
    _activeSection = _sectionFromTitle(widget.title);
    _loadUserSession();
    _loadNextAppointment();
    _loadNotifications();
    if (!_isTesting) {
      _notificationTimer = Timer.periodic(
        const Duration(seconds: 2),
        (_) => _loadNotifications(background: true),
      );
    }
  }

  @override
  void dispose() {
    _notificationTimer?.cancel();
    super.dispose();
  }

  Future<void> _loadUserSession() async {
    final prefs = await SharedPreferences.getInstance();
    final name = prefs.getString('patient_full_name');
    final email = prefs.getString('patient_email');
    if (!mounted) return;

    final resolvedName =
        name?.trim().isNotEmpty == true ? name!.trim() : _userName;
    setState(() {
      _userName = resolvedName;
      _userEmail =
          email?.trim().isNotEmpty == true ? email!.trim() : _userEmail;
      _userInitials = _initialsFor(resolvedName);
    });
  }

  _PatientSection _sectionFromTitle(String? title) {
    switch (title) {
      case 'My Profile':
      case 'Patient Profile':
        return _PatientSection.profile;
      case 'Appointments':
        return _PatientSection.appointments;
      case 'Doctors':
        return _PatientSection.doctors;
      case 'Medical Records':
        return _PatientSection.records;
      case 'Hospital AI Assistant':
      case 'AI Health Assistant':
      case 'AI Smart Triage':
        return _PatientSection.assistant;
      case 'Settings':
        return _PatientSection.settings;
      case 'Help & Support':
        return _PatientSection.support;
      default:
        return _PatientSection.home;
    }
  }

  String get _title => switch (_activeSection) {
        _PatientSection.home => 'Home',
        _PatientSection.profile => 'My Profile',
        _PatientSection.appointments => 'Appointments',
        _PatientSection.doctors => 'Doctors',
        _PatientSection.records => 'Medical Records',
        _PatientSection.assistant => 'Hospital AI Assistant',
        _PatientSection.settings => 'Settings',
        _PatientSection.support => 'Help & Support',
      };

  String get _subtitle => switch (_activeSection) {
        _PatientSection.home => 'Your patient dashboard',
        _PatientSection.profile => 'Personal and emergency information',
        _PatientSection.appointments => 'Book, cancel, and view visits',
        _PatientSection.doctors => 'Find specialists and available schedules',
        _PatientSection.records => 'History, reports, prescriptions, and notes',
        _PatientSection.assistant =>
          'Patient guidance and appointment assistance',
        _PatientSection.settings => 'Account, privacy, language, and security',
        _PatientSection.support => 'FAQs, hospital contact, and technical help',
      };

  Widget get _content => switch (_activeSection) {
        _PatientSection.home => _HomeSection(
            userName: _userName,
            nextAppointmentText: _nextAppointmentText,
            onBookAppointment: () => _openAppointments('book'),
            onViewAppointments: () => _openAppointments('upcoming'),
            onOpenAssistant: () => _selectSection(_PatientSection.assistant),
            onOpenDoctors: () => _selectSection(_PatientSection.doctors),
          ),
        _PatientSection.profile => const ProfileScreen(),
        _PatientSection.appointments => _AppointmentsSection(
            action: _appointmentAction,
            actionVersion: _appointmentActionVersion,
            preferredDoctorId: _appointmentDoctorId,
            onAppointmentsLoaded: _syncNextAppointment,
          ),
        _PatientSection.doctors => DoctorSearchScreen(
            onBookAppointment: _openDoctorBooking,
          ),
        _PatientSection.records => const MedicalRecordsScreen(embedded: true),
        _PatientSection.assistant =>
          const HospitalAssistantScreen(embedded: true),
        _PatientSection.settings => const _SettingsSection(),
        _PatientSection.support => const _SupportSection(),
      };

  void _selectSection(_PatientSection section, {bool closeDrawer = false}) {
    setState(() => _activeSection = section);
    if (closeDrawer) {
      Navigator.maybePop(context);
    }
  }

  void _openAppointments(String action) {
    setState(() {
      _activeSection = _PatientSection.appointments;
      _appointmentAction = action;
      _appointmentDoctorId = null;
      _appointmentActionVersion++;
    });
  }

  void _openDoctorBooking(DoctorSearchResult doctor) {
    setState(() {
      _activeSection = _PatientSection.appointments;
      _appointmentAction = 'book';
      _appointmentDoctorId = doctor.doctorId;
      _appointmentActionVersion++;
    });
  }

  int get _unreadNotificationCount => _notifications
      .where((item) => !_readNotificationIds.contains(item['_notificationId']))
      .length;

  Future<String> _notificationReadKey() async {
    final prefs = await SharedPreferences.getInstance();
    return 'patient_read_notifications_${prefs.getInt('patient_user_id') ?? 'current'}';
  }

  Future<String> _notificationDismissedKey() async {
    final prefs = await SharedPreferences.getInstance();
    return 'patient_dismissed_notifications_${prefs.getInt('patient_user_id') ?? 'current'}';
  }

  Future<void> _loadNotifications({bool background = false}) async {
    if (!background && mounted) setState(() => _loadingNotifications = true);
    try {
      final results = await Future.wait([
        ApiService.getAppointmentNotifications(),
        ApiService.getClinicalReviewNotifications(),
      ]);
      final items = <Map<String, dynamic>>[
        ...results[0].map((item) => <String, dynamic>{
              ...item,
              '_notificationId':
                  'appointment:${item['appointmentNotificationId']}',
              '_title': 'Appointment Update',
              '_icon': Icons.event_available_outlined,
            }),
        ...results[1].map((item) {
              final isEmergency = item['isEmergency'] == true ||
                  item['priorityLevel'] == 'Critical' ||
                  (item['message']?.toString().contains('🚨') ?? false);
              return <String, dynamic>{
                ...item,
                '_notificationId':
                    'clinical:${item['triageWorkflowId']}',
                '_title': isEmergency ? '🚨 CRITICAL EMERGENCY ALERT' : 'Clinical Review Update',
                '_icon': isEmergency ? Icons.warning_amber_rounded : Icons.medical_information_outlined,
                '_isEmergency': isEmergency,
              };
            }),
      ];

      items.sort((a, b) {
        final dateA = DateTime.tryParse(a['createdAt']?.toString() ?? '') ??
            DateTime.now();
        final dateB = DateTime.tryParse(b['createdAt']?.toString() ?? '') ??
            DateTime.now();
        return dateB.compareTo(dateA);
      });

      final prefs = await SharedPreferences.getInstance();
      final key = await _notificationReadKey();
      final bool isFirstLoad = !prefs.containsKey(key);
      List<String> readList = prefs.getStringList(key) ?? [];
      
      final dismissedKey = await _notificationDismissedKey();
      final dismissedIds = prefs.getStringList(dismissedKey)?.toSet() ?? {};

      if (!mounted) return;
      
      final filteredItems = items
          .where((item) => !dismissedIds.contains(item['_notificationId']))
          .toList();

      if (isFirstLoad && filteredItems.isNotEmpty) {
        readList = filteredItems.map((item) => item['_notificationId'] as String).toList();
        await prefs.setStringList(key, readList);
      }

      setState(() {
        _notifications = filteredItems;
        _readNotificationIds = readList.toSet();
        _loadingNotifications = false;
        _notificationError = null;
      });
      if (_notificationMenu.isOpen) await _markNotificationsRead();
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _notificationError = 'Unable to load notifications right now.';
        _loadingNotifications = false;
      });
    }
  }

  Future<void> _markNotificationsRead() async {
    if (_notifications.isEmpty) return;
    final ids =
        _notifications.map((n) => n['_notificationId'] as String).toList();
    final prefs = await SharedPreferences.getInstance();
    final key = await _notificationReadKey();
    await prefs.setStringList(key, ids);
    if (!mounted) return;
    setState(() {
      _readNotificationIds = ids.toSet();
    });
  }

  Future<void> _clearNotifications() async {
    if (_notifications.isEmpty) return;

    final prefs = await SharedPreferences.getInstance();
    final key = await _notificationDismissedKey();
    final dismissedIds = (prefs.getStringList(key) ?? []).toSet()
      ..addAll(_notifications.map((item) => item['_notificationId'] as String));
    await prefs.setStringList(key, dismissedIds.toList());

    if (!mounted) return;
    setState(() {
      _notifications = [];
      _readNotificationIds = {};
    });
  }

  Future<void> _loadNextAppointment() async {
    try {
      final appointments = await ApiService.getMyAppointments();
      if (!mounted) return;
      setState(() {
        _nextAppointment = _findNextAppointment(appointments);
        _nextAppointmentError = null;
        _loadingNextAppointment = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _nextAppointmentError =
            'Unable to load your next appointment right now.';
        _loadingNextAppointment = false;
      });
    }
  }

  void _syncNextAppointment(List<Appointment> appointments) {
    setState(() {
      _nextAppointment = _findNextAppointment(appointments);
      _nextAppointmentError = null;
      _loadingNextAppointment = false;
    });
  }

  String get _nextAppointmentText {
    if (_loadingNextAppointment) {
      return 'Checking your upcoming appointments...';
    }

    if (_nextAppointmentError != null) {
      return _nextAppointmentError!;
    }

    final appointment = _nextAppointment;
    if (appointment == null) {
      return 'You have no upcoming appointments. Book a visit when you are ready.';
    }

    final doctorName = appointment.doctorName.trim().isEmpty
        ? 'your doctor'
        : appointment.doctorName.trim();
    final startAt = appointment.startAt.toLocal();
    final date = DateFormat('MMM d').format(startAt);
    final time = DateFormat('h:mm a').format(startAt);
    return 'Your next appointment is $doctorName on $date at $time.';
  }

  Future<void> _logout() async {
    await SessionManager.logout();
  }

  @override
  Widget build(BuildContext context) {
    final size = MediaQuery.of(context).size;
    final isDesktop = size.width >= 900;
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Scaffold(
      backgroundColor: isDark ? AppColors.bgDark : AppColors.bgLight,
      drawer: isDesktop
          ? null
          : Drawer(width: 286, child: _sidebar(isDesktop: false)),
      body: SafeArea(
        child: Row(
          children: [
            if (isDesktop) _sidebar(isDesktop: true),
            Expanded(
              child: Column(
                children: [
                  _topBar(isDesktop: isDesktop),
                  Expanded(child: _content),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _topBar({required bool isDesktop}) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Container(
      height: 76,
      padding: const EdgeInsets.symmetric(horizontal: 16),
      decoration: BoxDecoration(
        color: isDark ? AppColors.surfaceDark : AppColors.bgLightCard,
        border: Border(
          bottom: BorderSide(
              color: isDark ? AppColors.borderDark : AppColors.borderLight),
        ),
      ),
      child: Row(
        children: [
          if (!isDesktop)
            Builder(
              builder: (context) => IconButton.filledTonal(
                tooltip: 'Open menu',
                onPressed: () => Scaffold.of(context).openDrawer(),
                icon: const Icon(Icons.menu_rounded),
              ),
            ),
          if (!isDesktop) const SizedBox(width: 10),
          Expanded(
            child: Row(
              children: [
                if (!isDesktop) ...[
                  const MediCoreLogo(
                    size: 32,
                    borderRadius: 8,
                    showText: false,
                  ),
                  const SizedBox(width: 10),
                ],
                Expanded(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        _title,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(
                          color: isDark
                              ? AppColors.textPrimaryDark
                              : AppColors.textPrimaryLight,
                          fontSize: 18,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        _subtitle,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(
                          color: isDark
                              ? AppColors.textMutedDark
                              : AppColors.textMutedLight,
                          fontSize: 12,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
          MenuAnchor(
            controller: _notificationMenu,
            consumeOutsideTap: true,
            onOpen: _markNotificationsRead,
            alignmentOffset: const Offset(0, 10),
            style: MenuStyle(
              alignment: Alignment.bottomRight,
              padding: const WidgetStatePropertyAll(EdgeInsets.zero),
              backgroundColor: WidgetStatePropertyAll(
                isDark ? AppColors.surfaceDark : Colors.white,
              ),
              surfaceTintColor: const WidgetStatePropertyAll(Colors.transparent),
              elevation: const WidgetStatePropertyAll(8),
              shadowColor: const WidgetStatePropertyAll(Color(0x240F172A)),
              shape: WidgetStatePropertyAll(RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(20),
                side: BorderSide(
                  color: isDark ? AppColors.borderDark : AppColors.borderLight,
                ),
              )),
            ),
            menuChildren: [
              SizedBox(
                width: (MediaQuery.sizeOf(context).width - 32)
                    .clamp(0.0, 380.0),
                child: ConstrainedBox(
                  constraints: BoxConstraints(
                    maxHeight: MediaQuery.sizeOf(context).height * 0.6,
                  ),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Padding(
                        padding: const EdgeInsets.fromLTRB(18, 8, 6, 4),
                        child: Row(
                          children: [
                            const Expanded(
                              child: Text('Notifications',
                                  style: TextStyle(fontSize: 16, fontWeight: FontWeight.w800)),
                            ),
                            IconButton(
                              tooltip: 'Refresh notifications',
                              onPressed: _loadingNotifications
                                  ? null : _loadNotifications,
                              icon: const Icon(Icons.refresh_rounded, size: 20),
                            ),
                            IconButton(
                              tooltip: 'Close notifications',
                              onPressed: _notificationMenu.close,
                              icon: const Icon(Icons.close_rounded, size: 20),
                            ),
                          ],
                        ),
                      ),
                      if (_notifications.isNotEmpty)
                        Padding(
                          padding: const EdgeInsets.fromLTRB(18, 0, 12, 6),
                          child: Row(
                            children: [
                              Expanded(
                                child: Text(
                                  '${_notifications.length} ${_notifications.length == 1 ? 'update' : 'updates'}',
                                  style: TextStyle(
                                    fontSize: 12,
                                    color: isDark ? AppColors.textMutedDark : AppColors.textMutedLight,
                                  ),
                                ),
                              ),
                              TextButton(
                                onPressed: _clearNotifications,
                                child: const Text('Clear all'),
                              ),
                            ],
                          ),
                        ),
                      Flexible(
                        child: _NotificationsSection(
                          notifications: _notifications,
                          loading: _loadingNotifications,
                          error: _notificationError,
                          onRefresh: _loadNotifications,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ],
            builder: (context, controller, child) => Tooltip(
              message: 'Notifications',
              child: _HeaderIconButton(
                icon: Icons.notifications_none_rounded,
                badgeCount: _unreadNotificationCount,
                onTap: () => controller.isOpen
                    ? controller.close() : controller.open(),
              ),
            ),
          ),
          const SizedBox(width: 8),
          CircleAvatar(
            radius: 18,
            backgroundColor: AppColors.primary,
            child: Text(
              _userInitials,
              style: const TextStyle(
                  color: Colors.white,
                  fontSize: 12,
                  fontWeight: FontWeight.w800),
            ),
          ),
        ],
      ),
    );
  }

  Widget _sidebar({required bool isDesktop}) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final headerPadding = isDesktop
        ? const EdgeInsets.fromLTRB(18, 18, 18, 20)
        : const EdgeInsets.fromLTRB(14, 12, 12, 12);
    final listPadding = isDesktop
        ? const EdgeInsets.fromLTRB(14, 18, 14, 18)
        : const EdgeInsets.fromLTRB(10, 10, 10, 10);
    final footerPadding = isDesktop
        ? const EdgeInsets.fromLTRB(14, 12, 14, 14)
        : const EdgeInsets.fromLTRB(10, 8, 10, 10);
    return Container(
      width: isDesktop ? 300 : null,
      color: isDark ? AppColors.surfaceDark : const Color(0xFFEAF1F8),
      child: SafeArea(
        child: Column(
          children: [
            Padding(
              padding: headerPadding,
              child: Row(
                children: [
                  MediCoreLogo(
                    size: isDesktop ? 48 : 40,
                    borderRadius: isDesktop ? 12 : 10,
                    showText: true,
                    fontSize: isDesktop ? 23 : 20,
                  ),
                  const Spacer(),
                  if (!isDesktop)
                    IconButton(
                      visualDensity: VisualDensity.compact,
                      onPressed: () => Navigator.pop(context),
                      icon: const Icon(Icons.close_rounded, size: 22),
                    ),
                ],
              ),
            ),
            Divider(
                color: isDark ? AppColors.borderDark : AppColors.borderLight),
            Expanded(
              child: ListView(
                physics: const BouncingScrollPhysics(),
                padding: listPadding,
                children: [
                  _sectionLabel('Main Menu'),
                  _navItem(Icons.home_rounded, 'Home', _PatientSection.home),
                  _navItem(Icons.person_outline_rounded, 'My Profile',
                      _PatientSection.profile),
                  _navItem(Icons.calendar_month_outlined, 'Appointments',
                      _PatientSection.appointments),
                  _navItem(Icons.medical_services_outlined, 'Doctors',
                      _PatientSection.doctors),
                  _navItem(Icons.folder_copy_outlined, 'Medical Records',
                      _PatientSection.records),
                  _navItem(Icons.psychology_alt_outlined,
                      'Hospital AI Assistant', _PatientSection.assistant),
                  const SizedBox(height: 12),
                  _sectionLabel('Account'),
                  _navItem(Icons.settings_outlined, 'Settings',
                      _PatientSection.settings),
                  _navItem(Icons.help_outline_rounded, 'Help & Support',
                      _PatientSection.support),
                ],
              ),
            ),
            Divider(
                color: isDark ? AppColors.borderDark : AppColors.borderLight),
            Padding(
              padding: footerPadding,
              child: Column(
                children: [
                  _logoutItem(),
                  SizedBox(height: isDesktop ? 12 : 8),
                  Container(
                    padding: EdgeInsets.all(isDesktop ? 12 : 10),
                    decoration: BoxDecoration(
                      color: isDark
                          ? AppColors.surfaceDarkSecondary
                          : AppColors.bgLightCard,
                      borderRadius: BorderRadius.circular(14),
                      border: Border.all(
                          color: isDark
                              ? AppColors.borderDark
                              : AppColors.borderLight),
                    ),
                    child: Row(
                      children: [
                        CircleAvatar(
                          radius: isDesktop ? 20 : 17,
                          backgroundColor: AppColors.primary,
                          child: Text(
                            _userInitials,
                            style: TextStyle(
                              color: Colors.white,
                              fontSize: isDesktop ? 12 : 11,
                              fontWeight: FontWeight.w800,
                            ),
                          ),
                        ),
                        SizedBox(width: isDesktop ? 12 : 10),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                _userName,
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: TextStyle(
                                  color: isDark
                                      ? AppColors.textPrimaryDark
                                      : AppColors.textPrimaryLight,
                                  fontSize: isDesktop ? 13 : 12,
                                  fontWeight: FontWeight.w800,
                                ),
                              ),
                              const SizedBox(height: 2),
                              Text(
                                _userEmail,
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: TextStyle(
                                  color: isDark
                                      ? AppColors.textMutedDark
                                      : AppColors.textMutedLight,
                                  fontSize: isDesktop ? 11 : 10,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _sectionLabel(String label) {
    final isDesktop = MediaQuery.of(context).size.width >= 900;
    return Padding(
      padding:
          EdgeInsets.fromLTRB(isDesktop ? 14 : 12, 0, 12, isDesktop ? 8 : 5),
      child: Text(
        label.toUpperCase(),
        style: TextStyle(
          color: AppColors.textMutedLight,
          fontSize: isDesktop ? 11 : 10,
          fontWeight: FontWeight.w800,
          letterSpacing: 1.2,
        ),
      ),
    );
  }

  Widget _navItem(IconData icon, String label, _PatientSection section) {
    final isSelected = _activeSection == section;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final isDesktop = MediaQuery.of(context).size.width >= 900;
    final color = isSelected
        ? AppColors.primary
        : (isDark ? AppColors.textSecondaryDark : AppColors.textSecondaryLight);
    return Padding(
      padding: EdgeInsets.only(bottom: isDesktop ? 6 : 3),
      child: Material(
        color: isSelected
            ? AppColors.primary.withValues(alpha: isDark ? 0.18 : 0.12)
            : Colors.transparent,
        borderRadius: BorderRadius.circular(isDesktop ? 12 : 10),
        child: InkWell(
          borderRadius: BorderRadius.circular(isDesktop ? 12 : 10),
          onTap: () => _selectSection(section, closeDrawer: true),
          child: Container(
            constraints: BoxConstraints(minHeight: isDesktop ? 52 : 42),
            padding: EdgeInsets.symmetric(
                horizontal: isDesktop ? 14 : 12, vertical: isDesktop ? 10 : 7),
            decoration: BoxDecoration(
              border: isSelected
                  ? Border(
                      left: BorderSide(
                          color: AppColors.primary, width: isDesktop ? 4 : 3))
                  : null,
            ),
            child: Row(
              children: [
                Icon(icon, color: color, size: isDesktop ? 22 : 19),
                SizedBox(width: isDesktop ? 16 : 12),
                Expanded(
                  child: Text(
                    label,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      color: isSelected
                          ? AppColors.primary
                          : (isDark
                              ? AppColors.textPrimaryDark
                              : AppColors.textPrimaryLight),
                      fontSize: isDesktop ? 15 : 13.5,
                      fontWeight:
                          isSelected ? FontWeight.w800 : FontWeight.w600,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _logoutItem() {
    final isDesktop = MediaQuery.of(context).size.width >= 900;
    return Material(
      color: Colors.transparent,
      borderRadius: BorderRadius.circular(isDesktop ? 12 : 10),
      child: InkWell(
        borderRadius: BorderRadius.circular(isDesktop ? 12 : 10),
        onTap: _logout,
        child: Container(
          constraints: BoxConstraints(minHeight: isDesktop ? 52 : 42),
          padding: EdgeInsets.symmetric(
              horizontal: isDesktop ? 14 : 12, vertical: isDesktop ? 10 : 7),
          child: Row(
            children: [
              Icon(Icons.logout_rounded,
                  color: AppColors.danger, size: isDesktop ? 22 : 19),
              SizedBox(width: isDesktop ? 16 : 12),
              Expanded(
                child: Text(
                  'Logout',
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    color: AppColors.danger,
                    fontSize: isDesktop ? 15 : 13.5,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _HomeSection extends StatelessWidget {
  final String userName;
  final String nextAppointmentText;
  final VoidCallback onBookAppointment;
  final VoidCallback onViewAppointments;
  final VoidCallback onOpenAssistant;
  final VoidCallback onOpenDoctors;

  const _HomeSection({
    required this.userName,
    required this.nextAppointmentText,
    required this.onBookAppointment,
    required this.onViewAppointments,
    required this.onOpenAssistant,
    required this.onOpenDoctors,
  });

  @override
  Widget build(BuildContext context) {
    final firstName = userName.trim().split(' ').first;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final textPrimary =
        isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight;
    final textMuted =
        isDark ? AppColors.textMutedDark : AppColors.textMutedLight;
    final surfaceColor = isDark ? AppColors.surfaceDark : AppColors.bgLightCard;
    final borderColor = isDark ? AppColors.borderDark : AppColors.borderLight;

    return CustomScrollView(
      physics: const BouncingScrollPhysics(),
      slivers: [
        SliverPadding(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 32),
          sliver: SliverList(
            delegate: SliverChildListDelegate([
              // ── Hero greeting card ──────────────────────────────────
              Container(
                padding: const EdgeInsets.all(22),
                decoration: BoxDecoration(
                  gradient: const LinearGradient(
                    colors: [
                      Color(0xFF0369A1),
                      Color(0xFF0284C7),
                      Color(0xFF4F46E5)
                    ],
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                  ),
                  borderRadius: BorderRadius.circular(24),
                  boxShadow: [
                    BoxShadow(
                      color: AppColors.primary.withValues(alpha: 0.28),
                      blurRadius: 28,
                      offset: const Offset(0, 12),
                    ),
                  ],
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        const Icon(Icons.waving_hand_rounded,
                            color: Colors.amber, size: 22),
                        const SizedBox(width: 8),
                        Text(
                          'Welcome back,',
                          style: TextStyle(
                              color: Colors.white.withValues(alpha: 0.8),
                              fontSize: 14,
                              fontWeight: FontWeight.w500),
                        ),
                      ],
                    ),
                    const SizedBox(height: 4),
                    Text(
                      firstName,
                      style: const TextStyle(
                          color: Colors.white,
                          fontSize: 28,
                          fontWeight: FontWeight.w800,
                          letterSpacing: -0.5),
                    ),
                    const SizedBox(height: 16),
                    Container(
                      padding: const EdgeInsets.symmetric(
                          horizontal: 14, vertical: 10),
                      decoration: BoxDecoration(
                        color: Colors.white.withValues(alpha: 0.14),
                        borderRadius: BorderRadius.circular(14),
                        border: Border.all(
                            color: Colors.white.withValues(alpha: 0.18)),
                      ),
                      child: Row(
                        children: [
                          const Icon(Icons.calendar_today_rounded,
                              color: Colors.white, size: 16),
                          const SizedBox(width: 10),
                          Expanded(
                            child: Text(
                              nextAppointmentText,
                              style: const TextStyle(
                                  color: Colors.white,
                                  fontSize: 13,
                                  fontWeight: FontWeight.w600),
                            ),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(height: 14),
                    Row(
                      children: [
                        Expanded(
                          child: _HeroActionButton(
                            icon: Icons.event_note_rounded,
                            label: 'View',
                            onTap: onViewAppointments,
                          ),
                        ),
                        const SizedBox(width: 10),
                        Expanded(
                          child: _HeroActionButton(
                            icon: Icons.add_circle_outline_rounded,
                            label: 'Book',
                            onTap: onBookAppointment,
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),

              const SizedBox(height: 26),

              // ── Services ─────────────────────────────────────────────
              Text('Hospital Services',
                  style: TextStyle(
                      color: textPrimary,
                      fontSize: 17,
                      fontWeight: FontWeight.w800)),
              const SizedBox(height: 12),

              _ServiceCard(
                icon: Icons.smart_toy_outlined,
                title: 'Hospital AI Assistant',
                subtitle:
                    'Describe symptoms, get triage guidance, and book appointments with AI support.',
                actionLabel: 'Open Assistant',
                color: const Color(0xFF4F46E5),
                isDark: isDark,
                surfaceColor: surfaceColor,
                borderColor: borderColor,
                onTap: onOpenAssistant,
              ),
              const SizedBox(height: 10),
              _ServiceCard(
                icon: Icons.medical_information_outlined,
                title: 'My Medical Records',
                subtitle:
                    'Access your full medical history, lab results, prescriptions, and clinical notes.',
                actionLabel: 'View Records',
                color: const Color(0xFF059669),
                isDark: isDark,
                surfaceColor: surfaceColor,
                borderColor: borderColor,
                onTap: () => Navigator.push(
                  context,
                  MaterialPageRoute(
                      builder: (_) => const MedicalRecordsScreen()),
                ),
              ),
              const SizedBox(height: 10),
              _ServiceCard(
                icon: Icons.add_task_rounded,
                title: 'Book an Appointment',
                subtitle:
                    'Schedule consultations with specialists and find available time slots.',
                actionLabel: 'Book Now',
                color: const Color(0xFF0284C7),
                isDark: isDark,
                surfaceColor: surfaceColor,
                borderColor: borderColor,
                onTap: onBookAppointment,
              ),
              const SizedBox(height: 10),
              _ServiceCard(
                icon: Icons.person_search_outlined,
                title: 'Find Doctor',
                subtitle:
                    'Find specialists and explore available consultation schedules.',
                actionLabel: 'Find Doctor',
                color: const Color(0xFFD97706),
                isDark: isDark,
                surfaceColor: surfaceColor,
                borderColor: borderColor,
                onTap: onOpenDoctors,
              ),

              const SizedBox(height: 24),

              // ── Disclaimer ───────────────────────────────────────────
              Container(
                padding:
                    const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
                decoration: BoxDecoration(
                  color: isDark
                      ? Colors.white.withValues(alpha: 0.04)
                      : Colors.black.withValues(alpha: 0.03),
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: borderColor),
                ),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Icon(Icons.info_outline_rounded,
                        size: 15, color: textMuted),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        'AI guidance is informational only and does not replace professional medical advice.',
                        style: TextStyle(
                            color: textMuted, fontSize: 12, height: 1.4),
                      ),
                    ),
                  ],
                ),
              ),
            ]),
          ),
        ),
      ],
    );
  }
}

class _HeroActionButton extends StatelessWidget {
  final IconData icon;
  final String label;
  final VoidCallback onTap;

  const _HeroActionButton({
    required this.icon,
    required this.label,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.white.withValues(alpha: 0.18),
      borderRadius: BorderRadius.circular(13),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(13),
        splashColor: Colors.white.withValues(alpha: 0.1),
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 11),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(icon, color: Colors.white, size: 17),
              const SizedBox(width: 7),
              Text(label,
                  style: const TextStyle(
                      color: Colors.white,
                      fontWeight: FontWeight.w700,
                      fontSize: 14)),
            ],
          ),
        ),
      ),
    );
  }
}

class _ServiceCard extends StatelessWidget {
  final IconData icon;
  final String title;
  final String subtitle;
  final String actionLabel;
  final Color color;
  final bool isDark;
  final Color surfaceColor;
  final Color borderColor;
  final VoidCallback? onTap;

  const _ServiceCard({
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.actionLabel,
    required this.color,
    required this.isDark,
    required this.surfaceColor,
    required this.borderColor,
    this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(18),
        child: Container(
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: surfaceColor,
            borderRadius: BorderRadius.circular(18),
            border: Border.all(color: borderColor),
          ),
          child: Row(
            children: [
              Container(
                width: 46,
                height: 46,
                decoration: BoxDecoration(
                  color: color.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(14),
                ),
                child: Icon(icon, color: color, size: 24),
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      title,
                      style: TextStyle(
                        color: isDark
                            ? AppColors.textPrimaryDark
                            : AppColors.textPrimaryLight,
                        fontSize: 14,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    const SizedBox(height: 3),
                    Text(
                      subtitle,
                      style: TextStyle(
                        color: isDark
                            ? AppColors.textMutedDark
                            : AppColors.textMutedLight,
                        fontSize: 12,
                        height: 1.3,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 10),
              Container(
                padding:
                    const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                decoration: BoxDecoration(
                  color: color.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(999),
                ),
                child: Text(
                  actionLabel,
                  style: TextStyle(
                      color: color, fontSize: 11, fontWeight: FontWeight.w700),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _AppointmentsSection extends StatefulWidget {
  final String? action;
  final int actionVersion;
  final int? preferredDoctorId;

  final ValueChanged<List<Appointment>>? onAppointmentsLoaded;

  const _AppointmentsSection({
    this.action,
    this.actionVersion = 0,
    this.preferredDoctorId,
    this.onAppointmentsLoaded,
  });

  @override
  State<_AppointmentsSection> createState() => _AppointmentsSectionState();
}

class _AppointmentsSectionState extends State<_AppointmentsSection> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _phoneController = TextEditingController();
  final _emailController = TextEditingController();
  final _ageController = TextEditingController();
  String _appointmentType = 'Consultation';
  String? _selectedSpecialty;
  int? _selectedDoctorId;
  String? _selectedDoctorName;
  int? _selectedSlotId;
  bool _showBookingForm = false;
  _AppointmentListMode _listMode = _AppointmentListMode.upcoming;
  bool _loading = true;
  bool _saving = false;
  String? _error;
  List<Appointment> _appointments = [];
  List<DoctorTimeSlot> _slots = [];
  List<DoctorLookup> _doctors = [];
  List<String> _specializations = [];

  @override
  void initState() {
    super.initState();
    _loadAppointments(preselectDoctor: true);
  }

  @override
  void dispose() {
    _nameController.dispose();
    _phoneController.dispose();
    _emailController.dispose();
    _ageController.dispose();
    super.dispose();
  }

  @override
  void didUpdateWidget(covariant _AppointmentsSection oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.actionVersion != oldWidget.actionVersion ||
        widget.preferredDoctorId != oldWidget.preferredDoctorId) {
      _applyPreferredDoctor(widget.preferredDoctorId);
      _applyAction(widget.action);
    }
  }

  void _applyAction(String? action) {
    if (action == 'book') {
      setState(() {
        _showBookingForm = true;
        _listMode = _AppointmentListMode.upcoming;
      });
    } else if (action == 'upcoming') {
      setState(() {
        _showBookingForm = false;
        _listMode = _AppointmentListMode.upcoming;
      });
    } else if (action == 'history') {
      setState(() {
        _showBookingForm = false;
        _listMode = _AppointmentListMode.history;
      });
    }
  }

  Future<void> _loadAppointments({bool preselectDoctor = false}) async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([
        ApiService.getMyProfile(),
        ApiService.getMyAppointments(),
        ApiService.getAvailableAppointmentSlots(),
        ApiService.getAppointmentDoctors(),
        ApiService.getAppointmentSpecializations(),
      ]);
      final profile = results[0] as Patient?;
      final appointments = results[1] as List<Appointment>;
      final slots = results[2] as List<DoctorTimeSlot>;
      final doctors = results[3] as List<DoctorLookup>;
      final specializations = _specialties(results[4] as List<String>, doctors);
      final futureSlots = slots
          .where((slot) =>
              _isFutureSlot(slot) && slot.isActive && slot.availableCount > 0)
          .toList();
      if (!mounted) return;
      setState(() {
        _appointments = appointments;
        _slots = futureSlots;
        _doctors = doctors;
        _specializations = specializations;
        _selectedSpecialty = specializations.contains(_selectedSpecialty)
            ? _selectedSpecialty
            : null;
        _selectedDoctorName =
            _doctorNamesForSpecialty(doctors, _selectedSpecialty)
                    .contains(_selectedDoctorName)
                ? _selectedDoctorName
                : null;
        _selectedDoctorId = doctors.any((doctor) =>
                doctor.doctorId == _selectedDoctorId &&
                _sameText(doctor.specialty, _selectedSpecialty))
            ? _selectedDoctorId
            : null;
        _selectedSlotId = futureSlots.any((slot) =>
                slot.doctorTimeSlotId == _selectedSlotId &&
                slot.doctorName == _selectedDoctorName &&
                _sameText(slot.specialty, _selectedSpecialty))
            ? _selectedSlotId
            : null;
      });
      widget.onAppointmentsLoaded?.call(appointments);
      _prefillProfile(profile);
      if (preselectDoctor) _applyPreferredDoctor(widget.preferredDoctorId);
      if (widget.action != null) _applyAction(widget.action);
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = _friendlyError(e));
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  void _prefillProfile(Patient? profile) {
    if (profile == null) return;
    if (_nameController.text.trim().isEmpty) {
      _nameController.text = profile.fullName;
    }
    if (_phoneController.text.trim().isEmpty) {
      _phoneController.text = profile.phoneNumber;
    }
    if (_emailController.text.trim().isEmpty) {
      _emailController.text = profile.email ?? '';
    }
    if (_ageController.text.trim().isEmpty) {
      _ageController.text = _ageFromDateOfBirth(profile.dateOfBirth).toString();
    }
  }

  Future<void> _bookAppointment() async {
    if (!_formKey.currentState!.validate()) return;
    final slot = _selectedSlot;
    if (slot == null ||
        !slot.isActive ||
        slot.availableCount <= 0 ||
        !_isFutureSlot(slot)) {
      setState(() => _error = 'Select an upcoming appointment slot.');
      return;
    }

    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      await ApiService.bookAppointment(
        doctorTimeSlotId: _selectedSlotId!,
        patientName: _nameController.text,
        patientPhone: _phoneController.text,
        patientEmail: _emailController.text,
        appointmentType: _appointmentType,
      );
      if (!mounted) return;
      _selectedSpecialty = null;
      _selectedDoctorId = null;
      _selectedSlotId = null;
      _selectedDoctorName = null;
      _showBookingForm = false;
      _listMode = _AppointmentListMode.upcoming;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Appointment booked successfully.')),
      );
      await _loadAppointments();
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = _friendlyError(e));
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<void> _reschedule(Appointment appointment) async {
    if (_saving) return;
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      final available = await ApiService.getAvailableAppointmentSlots();
      if (!mounted) return;
      final slots = available.where((slot) =>
          (appointment.doctorId != null
              ? slot.doctorId == appointment.doctorId
              : slot.doctorName == appointment.doctorName) &&
          _sameText(slot.specialty, appointment.specialty) &&
          slot.doctorTimeSlotId != appointment.doctorTimeSlotId &&
          slot.isActive && slot.availableCount > 0 &&
          _isFutureSlot(slot)).toList()
        ..sort((a, b) => a.startAt.compareTo(b.startAt));
      String dayOf(DoctorTimeSlot slot) =>
          DateFormat('yyyy-MM-dd').format(slot.startAt.toLocal());
      final dates = slots.map(dayOf).toSet().toList();
      String? selectedDate;
      int? selectedSlotId;
      final slotId = await showDialog<int>(
        context: context,
        builder: (dialogContext) => StatefulBuilder(
          builder: (context, updateDialog) => AlertDialog(
            title: const Text('Reschedule appointment'),
            content: SizedBox(
              width: double.maxFinite,
              child: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(appointment.patientName),
                    Text('${appointment.doctorName} · ${appointment.specialty}'),
                    const SizedBox(height: 12),
                    const Text('Patient and doctor details are locked. Select another available date and session.'),
                    const SizedBox(height: 16),
                    if (slots.isEmpty)
                      const Text('No other available sessions for this doctor.')
                    else ...[
                      DropdownButtonFormField<String>(
                        initialValue: selectedDate,
                        isExpanded: true,
                        decoration: const InputDecoration(labelText: 'Appointment date'),
                        hint: const Text('Select appointment date'),
                        items: dates.map((date) => DropdownMenuItem(
                          value: date,
                          child: Text(DateFormat('MMM d, yyyy').format(DateTime.parse(date))),
                        )).toList(),
                        onChanged: (value) => updateDialog(() {
                          selectedDate = value;
                          selectedSlotId = null;
                        }),
                      ),
                      const SizedBox(height: 12),
                      DropdownButtonFormField<int>(
                        key: ValueKey(selectedDate),
                        initialValue: selectedSlotId,
                        isExpanded: true,
                        decoration: const InputDecoration(labelText: 'Appointment session'),
                        hint: const Text('Select appointment session'),
                        items: slots.where((slot) => dayOf(slot) == selectedDate)
                            .map((slot) => DropdownMenuItem(
                              value: slot.doctorTimeSlotId,
                              child: Text('${DateFormat('h:mm a').format(slot.startAt.toLocal())} – ${DateFormat('h:mm a').format(slot.endAt.toLocal())}'),
                            )).toList(),
                        onChanged: selectedDate == null ? null : (value) =>
                            updateDialog(() => selectedSlotId = value),
                      ),
                    ],
                  ],
                ),
              ),
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.pop(dialogContext),
                child: const Text('Keep appointment'),
              ),
              FilledButton(
                onPressed: selectedSlotId == null ? null : () =>
                    Navigator.pop(dialogContext, selectedSlotId),
                child: const Text('Confirm reschedule'),
              ),
            ],
          ),
        ),
      );
      if (slotId == null || !mounted) return;
      await ApiService.rescheduleAppointment(
        appointmentId: appointment.appointmentId,
        doctorTimeSlotId: slotId,
      );
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Appointment rescheduled successfully.')),
      );
      await _loadAppointments();
    } catch (e) {
      if (mounted) setState(() => _error = _friendlyError(e));
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<void> _cancel(Appointment appointment) async {
    final reason = await _cancelReason();
    if (reason == null) return;
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      await ApiService.cancelAppointment(
        appointmentId: appointment.appointmentId,
        reason: reason,
      );
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Appointment cancelled.')),
      );
      await _loadAppointments();
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = _friendlyError(e));
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<String?> _cancelReason() async {
    var reason = '';
    final formKey = GlobalKey<FormState>();
    return showDialog<String>(
      context: context,
      builder: (context) {
        return AlertDialog(
          title: const Text('Cancel appointment'),
          content: Form(
            key: formKey,
            child: TextFormField(
              // Let the field own its controller for the entire route lifetime,
              // including the dialog's closing animation.
              onChanged: (value) => reason = value,
              minLines: 2,
              maxLines: 4,
              decoration: const InputDecoration(
                labelText: 'Reason',
                hintText: 'Tell us why you need to cancel',
              ),
              validator: (value) => value == null || value.trim().length < 3
                  ? 'Enter a cancellation reason.'
                  : null,
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context),
              child: const Text('Keep'),
            ),
            FilledButton(
              onPressed: () {
                if (formKey.currentState!.validate()) {
                  Navigator.pop(context, reason.trim());
                }
              },
              child: const Text('Cancel appointment'),
            ),
          ],
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final upcomingAppointments =
        _appointments.where(_isUpcomingAppointment).toList()
          ..sort((a, b) {
            final dateComparison = a.startAt.compareTo(b.startAt);
            if (dateComparison != 0) return dateComparison;
            return a.appointmentNumber.compareTo(b.appointmentNumber);
          });
    final historicalAppointments =
        _appointments.where(_isHistoricalAppointment).toList()
          ..sort((a, b) {
            final dateComparison = b.startAt.compareTo(a.startAt);
            if (dateComparison != 0) return dateComparison;
            return b.appointmentNumber.compareTo(a.appointmentNumber);
          });
    final showingHistory = _listMode == _AppointmentListMode.history;
    final visibleAppointments =
        showingHistory ? historicalAppointments : upcomingAppointments;

    return _PageScaffold(
      children: [
        _HeroCard(
          title: 'Book a doctor visit',
          subtitle:
              'Select Doctor, choose Date, pick an Available Time, then Confirm Appointment.',
          icon: Icons.calendar_month_outlined,
          actions: const [
            'Book Appointment',
            'View Appointment',
            'View History'
          ],
          onAction: (label) {
            setState(() {
              _showBookingForm = label == 'Book Appointment';
              _listMode = label == 'View History'
                  ? _AppointmentListMode.history
                  : _AppointmentListMode.upcoming;
            });
          },
        ),
        const SizedBox(height: 16),
        if (_error != null) ...[
          _ErrorPanel(message: _error!, onRetry: _loadAppointments),
          const SizedBox(height: 16),
        ],
        if (_loading)
          const _AppointmentLoadingState()
        else ...[
          if (_showBookingForm) ...[
            _BookingFormCard(
              formKey: _formKey,
              slots: _slots,
              doctors: _doctors,
              specializations: _specializations,
              selectedSpecialty: _selectedSpecialty,
              selectedDoctorId: _selectedDoctorId,
              selectedDoctorName: _selectedDoctorName,
              selectedSlotId: _selectedSlotId,
              appointmentType: _appointmentType,
              nameController: _nameController,
              phoneController: _phoneController,
              emailController: _emailController,
              ageController: _ageController,
              saving: _saving,
              onSpecialtyChanged: _selectSpecialty,
              onDoctorChanged: _selectDoctor,
              onSlotChanged: (value) => setState(() => _selectedSlotId = value),
              onTypeChanged: (value) =>
                  setState(() => _appointmentType = value ?? 'Consultation'),
              onClose: () => setState(() => _showBookingForm = false),
              onSubmit: _bookAppointment,
            ),
            const SizedBox(height: 16),
          ],
          _SectionTitle(
              showingHistory ? 'Appointment history' : 'Upcoming appointments'),
          const SizedBox(height: 12),
          if (visibleAppointments.isEmpty)
            _EmptyStateCard(
              icon: showingHistory
                  ? Icons.history_rounded
                  : Icons.event_available_outlined,
              title: showingHistory
                  ? 'No completed or cancelled appointments'
                  : 'No upcoming appointments',
              subtitle: showingHistory
                  ? 'Completed and cancelled appointments will appear here.'
                  : 'Booked upcoming appointments will appear here.',
            )
          else ...[
            if (showingHistory) ...[
              const _SectionLabel('Completed and cancelled'),
              const SizedBox(height: 8),
            ],
            ...visibleAppointments.map((appointment) => Padding(
                  padding: const EdgeInsets.only(bottom: 12),
                  child: _AppointmentCard(
                    appointment: appointment,
                    compact: showingHistory,
                    onReschedule: showingHistory || _saving ||
                            !appointment.startAt.isAfter(DateTime.now())
                        ? null
                        : () => _reschedule(appointment),
                    onCancel: showingHistory || _saving
                        ? null
                        : () => _cancel(appointment),
                  ),
                )),
          ],
        ],
      ],
    );
  }

  bool _isUpcomingAppointment(Appointment appointment) {
    if (_isHistoricalAppointment(appointment)) {
      return false;
    }

    return appointment.endAt.isAfter(DateTime.now());
  }

  bool _isHistoricalAppointment(Appointment appointment) =>
      appointment.status == 'Cancelled' || appointment.status == 'Completed';

  String _friendlyError(Object error) {
    final message = error.toString().replaceFirst('Exception: ', '');
    return message.isEmpty
        ? 'Unable to load appointment information.'
        : message;
  }

  DoctorTimeSlot? get _selectedSlot {
    for (final slot in _slots) {
      if (slot.doctorTimeSlotId == _selectedSlotId) return slot;
    }
    return null;
  }

  void _selectSpecialty(String? specialty) {
    setState(() {
      _selectedSpecialty = specialty;
      _selectedDoctorId = null;
      _selectedDoctorName = null;
      _selectedSlotId = null;
    });
  }

  void _selectDoctor(String? doctorName) {
    setState(() {
      _selectedDoctorName = doctorName;
      final matchedDoctor = _doctors
          .where((doctor) => doctor.doctorName == doctorName)
          .firstOrNull;
      _selectedDoctorId = matchedDoctor?.doctorId;
      if (matchedDoctor != null && matchedDoctor.specialty.trim().isNotEmpty) {
        _selectedSpecialty = matchedDoctor.specialty;
      }
      _selectedSlotId = null;
    });
  }

  void _applyPreferredDoctor(int? doctorId) {
    if (doctorId == null || _doctors.isEmpty) return;

    DoctorLookup? preferredDoctor;
    for (final doctor in _doctors) {
      if (doctor.doctorId == doctorId) {
        preferredDoctor = doctor;
        break;
      }
    }
    if (preferredDoctor == null) return;

    setState(() {
      _selectedSpecialty = preferredDoctor!.specialty;
      _selectedDoctorId = preferredDoctor.doctorId;
      _selectedDoctorName = preferredDoctor.doctorName;
      _selectedSlotId = null;
    });
  }

  List<String> _doctorNamesForSpecialty(
      List<DoctorLookup> doctors, String? specialty) {
    final names = <String>{};
    for (final doctor in doctors) {
      if (doctor.doctorName.trim().isNotEmpty &&
          _sameText(doctor.specialty, specialty)) {
        names.add(doctor.doctorName);
      }
    }
    return names.toList()..sort();
  }

  List<String> _specialties(
      List<String> loadedSpecializations, List<DoctorLookup> doctors) {
    final values = <String>{};
    for (final specialty in loadedSpecializations) {
      if (specialty.trim().isNotEmpty) values.add(specialty.trim());
    }
    if (values.isEmpty) {
      for (final doctor in doctors) {
        if (doctor.specialty.trim().isNotEmpty) {
          values.add(doctor.specialty.trim());
        }
      }
    }
    return values.toList()..sort();
  }

  int _ageFromDateOfBirth(DateTime dateOfBirth) {
    final today = DateTime.now();
    var age = today.year - dateOfBirth.year;
    final birthdayThisYear =
        DateTime(today.year, dateOfBirth.month, dateOfBirth.day);
    if (birthdayThisYear.isAfter(today)) age--;
    return age < 0 ? 0 : age;
  }

  bool _isFutureSlot(DoctorTimeSlot slot) =>
      slot.startAt.isAfter(DateTime.now());
}

class _SearchableDoctorDropdown extends StatefulWidget {
  final List<DoctorLookup> doctors;
  final String? selectedSpecialty;
  final String? selectedDoctorName;
  final bool enabled;
  final ValueChanged<String?> onDoctorChanged;

  const _SearchableDoctorDropdown({
    super.key,
    required this.doctors,
    required this.selectedSpecialty,
    required this.selectedDoctorName,
    required this.enabled,
    required this.onDoctorChanged,
  });

  @override
  State<_SearchableDoctorDropdown> createState() =>
      _SearchableDoctorDropdownState();
}

class _SearchableDoctorDropdownState extends State<_SearchableDoctorDropdown> {
  late TextEditingController _controller;
  late FocusNode _focusNode;

  @override
  void initState() {
    super.initState();
    _controller =
        TextEditingController(text: widget.selectedDoctorName ?? '');
    _focusNode = FocusNode();
  }

  @override
  void didUpdateWidget(covariant _SearchableDoctorDropdown oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.selectedDoctorName != oldWidget.selectedDoctorName) {
      final newText = widget.selectedDoctorName ?? '';
      if (_controller.text != newText) {
        _controller.text = newText;
      }
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    _focusNode.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final availableDoctors = widget.selectedSpecialty == null
        ? widget.doctors
        : widget.doctors
            .where((d) => _sameText(d.specialty, widget.selectedSpecialty))
            .toList();

    return LayoutBuilder(
      builder: (context, constraints) {
        return RawAutocomplete<DoctorLookup>(
          textEditingController: _controller,
          focusNode: _focusNode,
          displayStringForOption: (doctor) => doctor.doctorName,
          optionsBuilder: (TextEditingValue textEditingValue) {
            final query = textEditingValue.text.trim().toLowerCase();
            if (query.isEmpty) {
              return availableDoctors;
            }
            return availableDoctors.where((doctor) {
              return doctor.doctorName.toLowerCase().contains(query) ||
                  doctor.specialty.toLowerCase().contains(query);
            });
          },
          onSelected: (doctor) {
            _focusNode.unfocus();
            widget.onDoctorChanged(doctor.doctorName);
          },
          fieldViewBuilder:
              (context, textController, focusNode, onFieldSubmitted) {
            return TextFormField(
              controller: textController,
              focusNode: focusNode,
              enabled: widget.enabled,
              decoration: InputDecoration(
                labelText: 'Search or Select Doctor',
                hintText: 'Type doctor name or specialty...',
                prefixIcon: const Icon(Icons.search_rounded),
                suffixIcon: textController.text.isNotEmpty && widget.enabled
                    ? IconButton(
                        icon: const Icon(Icons.clear_rounded, size: 18),
                        onPressed: () {
                          textController.clear();
                          widget.onDoctorChanged(null);
                        },
                      )
                    : const Icon(Icons.arrow_drop_down_rounded, size: 28),
              ),
              validator: (value) {
                if (widget.selectedDoctorName == null ||
                    widget.selectedDoctorName!.trim().isEmpty) {
                  return 'Choose a doctor.';
                }
                return null;
              },
            );
          },
          optionsViewBuilder: (context, onSelected, options) {
            return Align(
              alignment: Alignment.topLeft,
              child: Material(
                elevation: 6,
                borderRadius: BorderRadius.circular(12),
                color: isDark ? const Color(0xFF1E293B) : Colors.white,
                child: ConstrainedBox(
                  constraints: BoxConstraints(
                    maxHeight: 220,
                    maxWidth: constraints.maxWidth,
                  ),
                  child: options.isEmpty
                      ? const Padding(
                          padding: EdgeInsets.all(12),
                          child: Text(
                            'No matching doctors found.',
                            style: TextStyle(fontSize: 13, color: Colors.grey),
                          ),
                        )
                      : ListView.separated(
                          padding: const EdgeInsets.symmetric(vertical: 4),
                          shrinkWrap: true,
                          itemCount: options.length,
                          separatorBuilder: (_, __) => Divider(
                            height: 1,
                            color: isDark
                                ? AppColors.borderDark
                                : AppColors.borderLight,
                          ),
                          itemBuilder: (context, index) {
                            final doctor = options.elementAt(index);
                            final isSelected =
                                doctor.doctorName == widget.selectedDoctorName;
                            return ListTile(
                              dense: true,
                              visualDensity: VisualDensity.compact,
                              leading: CircleAvatar(
                                radius: 14,
                                backgroundColor: isSelected
                                    ? AppColors.primary
                                    : (isDark
                                        ? Colors.white12
                                        : const Color(0xFFE2E8F0)),
                                child: Text(
                                  doctor.doctorName.isNotEmpty
                                      ? doctor.doctorName
                                          .replaceAll('Dr. ', '')
                                          .trim()
                                          .substring(0, 1)
                                          .toUpperCase()
                                      : 'D',
                                  style: TextStyle(
                                    fontSize: 11,
                                    fontWeight: FontWeight.bold,
                                    color: isSelected
                                        ? Colors.white
                                        : (isDark
                                            ? Colors.white70
                                            : Colors.black87),
                                  ),
                                ),
                              ),
                              title: Text(
                                doctor.doctorName,
                                style: TextStyle(
                                  fontWeight: isSelected
                                      ? FontWeight.bold
                                      : FontWeight.w600,
                                  fontSize: 13,
                                ),
                              ),
                              subtitle: Text(
                                doctor.specialty,
                                style: TextStyle(
                                  fontSize: 11,
                                  color: isDark
                                      ? Colors.white60
                                      : Colors.black54,
                                ),
                              ),
                              trailing: isSelected
                                  ? const Icon(Icons.check_rounded,
                                      color: AppColors.primary, size: 18)
                                  : null,
                              onTap: () => onSelected(doctor),
                            );
                          },
                        ),
                ),
              ),
            );
          },
        );
      },
    );
  }
}

class _BookingFormCard extends StatelessWidget {
  final GlobalKey<FormState> formKey;
  final List<DoctorTimeSlot> slots;
  final List<DoctorLookup> doctors;
  final List<String> specializations;
  final String? selectedSpecialty;
  final int? selectedDoctorId;
  final String? selectedDoctorName;
  final int? selectedSlotId;
  final String appointmentType;
  final TextEditingController nameController;
  final TextEditingController phoneController;
  final TextEditingController emailController;
  final TextEditingController ageController;
  final bool saving;
  final ValueChanged<String?> onSpecialtyChanged;
  final ValueChanged<String?> onDoctorChanged;
  final ValueChanged<int?> onSlotChanged;
  final ValueChanged<String?> onTypeChanged;
  final VoidCallback onClose;
  final VoidCallback onSubmit;

  const _BookingFormCard({
    required this.formKey,
    required this.slots,
    required this.doctors,
    required this.specializations,
    required this.selectedSpecialty,
    required this.selectedDoctorId,
    required this.selectedDoctorName,
    required this.selectedSlotId,
    required this.appointmentType,
    required this.nameController,
    required this.phoneController,
    required this.emailController,
    required this.ageController,
    required this.saving,
    required this.onSpecialtyChanged,
    required this.onDoctorChanged,
    required this.onSlotChanged,
    required this.onTypeChanged,
    required this.onClose,
    required this.onSubmit,
  });

  @override
  Widget build(BuildContext context) {
    final filteredDoctors = selectedSpecialty == null
        ? <DoctorLookup>[]
        : doctors
            .where((doctor) => _sameText(doctor.specialty, selectedSpecialty))
            .toList();
    final doctorSlots = selectedDoctorId == null && selectedDoctorName == null
        ? <DoctorTimeSlot>[]
        : slots
            .where((slot) =>
                (slot.doctorId == selectedDoctorId ||
                    (slot.doctorId == null &&
                        slot.doctorName == selectedDoctorName)) &&
                slot.isActive &&
                slot.availableCount > 0 &&
                slot.startAt.isAfter(DateTime.now()))
            .toList();
    DoctorTimeSlot? selectedSlot;
    for (final slot in doctorSlots) {
      if (slot.doctorTimeSlotId == selectedSlotId) {
        selectedSlot = slot;
        break;
      }
    }
    final hasNoUpcomingSlots = selectedDoctorName != null && doctorSlots.isEmpty;
    return _SurfaceCard(
      child: Form(
        key: formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(Icons.add_circle_outline, color: AppColors.primary),
                const SizedBox(width: 8),
                Expanded(
                  child: Text('Book appointment',
                      style: Theme.of(context)
                          .textTheme
                          .titleMedium
                          ?.copyWith(fontWeight: FontWeight.w900)),
                ),
                IconButton(
                  onPressed: saving ? null : onClose,
                  icon: const Icon(Icons.close_rounded),
                  tooltip: 'Close',
                ),
              ],
            ),
            const SizedBox(height: 14),
            if (specializations.isEmpty)
              const _InlineNotice(
                icon: Icons.medical_information_outlined,
                message: 'No approved doctor specializations are available.',
              )
            else
              DropdownButtonFormField<String>(
                key: ValueKey('specialization-$selectedSpecialty'),
                initialValue: selectedSpecialty,
                isExpanded: true,
                hint: const Text('Select specialization'),
                decoration: const InputDecoration(
                  labelText: 'Specialization',
                  floatingLabelBehavior: FloatingLabelBehavior.always,
                ),
                items: [
                  const DropdownMenuItem<String>(
                    value: '',
                    child: Text('Select specialization'),
                  ),
                  ...specializations.map((specialty) => DropdownMenuItem(
                        value: specialty,
                        child: Text(specialty, overflow: TextOverflow.ellipsis),
                      )),
                ],
                validator: (value) =>
                    value == null || value.isEmpty
                        ? 'Choose a specialization.'
                        : null,
                onChanged: saving
                    ? null
                    : (value) => onSpecialtyChanged(
                        value == null || value.isEmpty ? null : value),
              ),
            const SizedBox(height: 12),
            if (selectedSpecialty != null && filteredDoctors.isEmpty)
              const _InlineNotice(
                icon: Icons.person_search_outlined,
                message:
                    'No approved doctors are available for this specialization.',
              )
            else
              _SearchableDoctorDropdown(
                key: ValueKey(
                    'doctor-picker-$selectedSpecialty-$selectedDoctorId-$selectedDoctorName'),
                doctors: doctors,
                selectedSpecialty: selectedSpecialty,
                selectedDoctorName: selectedDoctorName,
                enabled: !saving,
                onDoctorChanged: onDoctorChanged,
              ),
            const SizedBox(height: 12),
            DropdownButtonFormField<int>(
              key: ValueKey(
                  'slot-picker-$selectedSpecialty-$selectedDoctorId-$selectedSlotId'),
              initialValue: doctorSlots
                      .any((slot) => slot.doctorTimeSlotId == selectedSlotId)
                  ? selectedSlotId
                  : null,
              isExpanded: true,
              decoration: InputDecoration(
                labelText: hasNoUpcomingSlots
                    ? 'No upcoming appointment slots'
                    : 'Appointment date and time',
              ),
              itemHeight: 64,
              isDense: false,
              items: doctorSlots
                  .map((slot) => DropdownMenuItem(
                        value: slot.doctorTimeSlotId,
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              DateFormat('EEE, d MMM yyyy')
                                  .format(slot.startAt.toLocal()),
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: const TextStyle(
                                  fontSize: 14, fontWeight: FontWeight.w700),
                            ),
                            const SizedBox(height: 4),
                            Text(
                              '${DateFormat('h:mm a').format(slot.startAt.toLocal())} – ${DateFormat('h:mm a').format(slot.endAt.toLocal())}',
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: TextStyle(
                                  fontSize: 12,
                                  color: Theme.of(context)
                                      .colorScheme
                                      .onSurfaceVariant),
                            ),
                          ],
                        ),
                      ))
                  .toList(),
              validator: (value) =>
                  value == null ? 'Choose an appointment slot.' : null,
              onChanged:
                  saving || hasNoUpcomingSlots ? null : onSlotChanged,
            ),
            if (hasNoUpcomingSlots) ...[
              const SizedBox(height: 10),
              const _InlineNotice(
                icon: Icons.event_busy_outlined,
                message:
                    'No upcoming appointment slots are available for this doctor.',
              ),
            ],
            const SizedBox(height: 10),
            _InlineNotice(
              icon: Icons.info_outline,
              message: selectedSlot == null
                  ? 'Select an appointment date and time to see the next available appointment number.'
                  : selectedSlot.nextAppointmentNumber > 0
                      ? 'Next available appointment number: #${selectedSlot.nextAppointmentNumber}\nThis number may change if someone books before you confirm.'
                      : 'No appointment numbers are available for this session.',
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: nameController,
              enabled: !saving && !hasNoUpcomingSlots,
              decoration: InputDecoration(
                labelText: 'Patient name',
                hintText: hasNoUpcomingSlots
                    ? 'Doctor has no upcoming sessions'
                    : null,
              ),
              textInputAction: TextInputAction.next,
              validator: (value) => value == null || value.trim().length < 2
                  ? 'Enter the patient name.'
                  : null,
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: ageController,
              enabled: !saving && !hasNoUpcomingSlots,
              decoration: const InputDecoration(labelText: 'Patient age'),
              keyboardType: TextInputType.number,
              textInputAction: TextInputAction.next,
              validator: (value) {
                final age = int.tryParse(value?.trim() ?? '');
                if (age == null || age < 0 || age > 130) {
                  return 'Enter a valid age.';
                }
                return null;
              },
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: phoneController,
              enabled: !saving && !hasNoUpcomingSlots,
              decoration: const InputDecoration(labelText: 'Phone number'),
              keyboardType: TextInputType.phone,
              textInputAction: TextInputAction.next,
              validator: (value) => value == null || value.trim().length < 6
                  ? 'Enter a valid phone number.'
                  : null,
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: emailController,
              enabled: !saving && !hasNoUpcomingSlots,
              decoration: const InputDecoration(labelText: 'Email'),
              keyboardType: TextInputType.emailAddress,
              textInputAction: TextInputAction.next,
              validator: (value) {
                final text = value?.trim() ?? '';
                if (text.isEmpty) return null;
                return text.contains('@') ? null : 'Enter a valid email.';
              },
            ),
            const SizedBox(height: 12),
            DropdownButtonFormField<String>(
              initialValue: appointmentType,
              decoration: const InputDecoration(labelText: 'Appointment type'),
              items: const [
                DropdownMenuItem(
                    value: 'Consultation', child: Text('Consultation')),
                DropdownMenuItem(value: 'Follow-up', child: Text('Follow-up')),
                DropdownMenuItem(value: 'Review', child: Text('Review')),
              ],
              onChanged:
                  saving || hasNoUpcomingSlots ? null : onTypeChanged,
            ),
            const SizedBox(height: 14),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: saving ? null : onClose,
                    child: const Text('Close'),
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: FilledButton.icon(
                    onPressed:
                        saving || selectedSlotId == null || hasNoUpcomingSlots
                            ? null
                            : onSubmit,
                    icon: saving
                        ? const SizedBox(
                            width: 16,
                            height: 16,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : const Icon(Icons.check_rounded),
                    label: Text(saving ? 'Booking...' : 'Confirm'),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _AppointmentLoadingState extends StatelessWidget {
  const _AppointmentLoadingState();

  @override
  Widget build(BuildContext context) {
    return const _SurfaceCard(
      child: Padding(
        padding: EdgeInsets.symmetric(vertical: 18),
        child: Row(
          children: [
            SizedBox(
              width: 22,
              height: 22,
              child: CircularProgressIndicator(strokeWidth: 2.4),
            ),
            SizedBox(width: 12),
            Expanded(
              child: Text(
                'Loading appointments and available time slots...',
                style: TextStyle(fontWeight: FontWeight.w700),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ErrorPanel extends StatelessWidget {
  final String message;
  final VoidCallback onRetry;

  const _ErrorPanel({required this.message, required this.onRetry});

  @override
  Widget build(BuildContext context) {
    return _SurfaceCard(
      child: Row(
        children: [
          const CircleAvatar(
            backgroundColor: Color(0xFFFEE2E2),
            child: Icon(Icons.error_outline_rounded, color: AppColors.danger),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Text(
              message,
              style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w700),
            ),
          ),
          TextButton.icon(
            onPressed: onRetry,
            icon: const Icon(Icons.refresh_rounded),
            label: const Text('Retry'),
          ),
        ],
      ),
    );
  }
}

class _EmptyStateCard extends StatelessWidget {
  final IconData icon;
  final String title;
  final String subtitle;

  const _EmptyStateCard({
    required this.icon,
    required this.title,
    required this.subtitle,
  });

  @override
  Widget build(BuildContext context) {
    return _SurfaceCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          CircleAvatar(
            backgroundColor: AppColors.primary.withValues(alpha: 0.12),
            child: Icon(icon, color: AppColors.primary),
          ),
          const SizedBox(height: 12),
          Text(title,
              style:
                  const TextStyle(fontSize: 15, fontWeight: FontWeight.w900)),
          const SizedBox(height: 4),
          Text(subtitle,
              style: const TextStyle(
                  color: AppColors.textMutedLight, fontSize: 12, height: 1.35)),
        ],
      ),
    );
  }
}

class _InlineNotice extends StatelessWidget {
  final IconData icon;
  final String message;

  const _InlineNotice({required this.icon, required this.message});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: AppColors.warning.withValues(alpha: 0.10),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: AppColors.warning.withValues(alpha: 0.24)),
      ),
      child: Row(
        children: [
          Icon(icon, color: AppColors.warning),
          const SizedBox(width: 10),
          Expanded(
            child: Text(message,
                style:
                    const TextStyle(fontSize: 12, fontWeight: FontWeight.w700)),
          ),
        ],
      ),
    );
  }
}

class _SectionLabel extends StatelessWidget {
  final String label;

  const _SectionLabel(this.label);

  @override
  Widget build(BuildContext context) {
    return Text(
      label,
      style: const TextStyle(
        color: AppColors.textMutedLight,
        fontSize: 12,
        fontWeight: FontWeight.w900,
      ),
    );
  }
}

String _formatAppointmentLocation(Appointment appointment) {
  return [appointment.roomNumber, appointment.roomName, appointment.floor]
      .map((value) => value.trim())
      .where((value) => value.isNotEmpty)
      .join(', ');
}

Appointment? _findNextAppointment(List<Appointment> appointments) {
  final now = DateTime.now();
  final upcoming = appointments.where((appointment) {
    final status = appointment.status.trim().toLowerCase();
    return !_isClosedAppointmentStatus(status) &&
        status != 'no-show' &&
        appointment.endAt.toLocal().isAfter(now);
  }).toList()
    ..sort((a, b) => a.startAt.compareTo(b.startAt));

  return upcoming.isEmpty ? null : upcoming.first;
}

bool _isClosedAppointmentStatus(String status) =>
    status == 'cancelled' || status == 'completed';

String _formatAppointmentDate(Appointment appointment) =>
    DateFormat('MMM d, yyyy').format(appointment.startAt.toLocal());

String _formatAppointmentTime(Appointment appointment) {
  final start = DateFormat('h:mm a').format(appointment.startAt.toLocal());
  final end = DateFormat('h:mm a').format(appointment.endAt.toLocal());
  return '$start - $end';
}

String _formatFee(double value) {
  if (value <= 0) return 'Fee pending';
  return 'LKR ${NumberFormat('#,##0.00').format(value)}';
}

bool _sameText(String? a, String? b) =>
    (a ?? '').trim().toLowerCase() == (b ?? '').trim().toLowerCase();

class _NotificationsSection extends StatelessWidget {
  final List<Map<String, dynamic>> notifications;
  final bool loading;
  final String? error;
  final VoidCallback onRefresh;

  const _NotificationsSection({
    required this.notifications,
    required this.loading,
    this.error,
    required this.onRefresh,
  });

  @override
  Widget build(BuildContext context) {
    if (loading && notifications.isEmpty) {
      return const Padding(
        padding: EdgeInsets.all(24),
        child: Center(child: CircularProgressIndicator()),
      );
    }

    if (error != null && notifications.isEmpty) {
      return Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Text(error!),
            const SizedBox(height: 16),
            ElevatedButton(
              onPressed: onRefresh,
              child: const Text('Try Again'),
            ),
          ],
        ),
      );
    }

    return SingleChildScrollView(
      primary: false,
      padding: const EdgeInsets.all(12),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (notifications.isEmpty)
            const _NotificationTile(
                Icons.notifications_none_rounded,
                'No notifications',
                'You have no new updates from the hospital.',
                ''),
          ...notifications.map((item) {
            final date = DateTime.tryParse(item['createdAt']?.toString() ?? '') ??
                DateTime.now();
            final formattedDate =
                DateFormat('MMM d, h:mm a').format(date.toLocal());
            return _NotificationTile(
              item['_icon'] as IconData? ?? Icons.notifications_none_rounded,
              item['_title'] as String? ?? 'Notification',
              item['message'] as String? ?? '',
              formattedDate,
            );
          }),
        ],
        ),
    );
  }
}

class _SettingsSection extends StatefulWidget {
  const _SettingsSection();

  @override
  State<_SettingsSection> createState() => _SettingsSectionState();
}

class _SettingsSectionState extends State<_SettingsSection> {
  String _userEmail = '';
  bool _notifyAppointments = true;
  bool _notifyLabReports = true;
  bool _notifyAnnouncements = false;

  @override
  void initState() {
    super.initState();
    _loadSettings();
  }

  Future<void> _loadSettings() async {
    final prefs = await SharedPreferences.getInstance();
    setState(() {
      _userEmail = prefs.getString('patient_email') ?? 'patient@medicore.lk';
      _notifyAppointments =
          prefs.getBool('patient_notify_appointments') ?? true;
      _notifyLabReports = prefs.getBool('patient_notify_lab_reports') ?? true;
      _notifyAnnouncements =
          prefs.getBool('patient_notify_announcements') ?? false;
    });
  }

  Future<void> _updateSetting(String key, dynamic value) async {
    final prefs = await SharedPreferences.getInstance();
    if (value is bool) {
      await prefs.setBool(key, value);
    } else if (value is String) {
      await prefs.setString(key, value);
    }
    _loadSettings();
  }

  void _showSnackBar(String message, Color bgColor) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Row(
          children: [
            Icon(
              bgColor == AppColors.success
                  ? Icons.check_circle_rounded
                  : Icons.error_outline_rounded,
              color: Colors.white,
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Text(
                message,
                style: const TextStyle(
                    color: Colors.white, fontWeight: FontWeight.w600),
              ),
            ),
          ],
        ),
        backgroundColor: bgColor,
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
        margin: const EdgeInsets.all(16),
      ),
    );
  }

  void _showChangePasswordSheet() {
    final currentPasswordController = TextEditingController();
    final newPasswordController = TextEditingController();
    final confirmPasswordController = TextEditingController();
    final formKey = GlobalKey<FormState>();
    bool obscureCurrent = true;
    bool obscureNew = true;
    bool obscureConfirm = true;
    bool isSaving = false;

    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: Theme.of(context).brightness == Brightness.dark
          ? AppColors.surfaceDark
          : Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      builder: (context) {
        return StatefulBuilder(
          builder: (context, setSheetState) {
            final isDark = Theme.of(context).brightness == Brightness.dark;
            final textStyle = TextStyle(
              color: isDark
                  ? AppColors.textPrimaryDark
                  : AppColors.textPrimaryLight,
            );

            InputDecoration customInputDecoration({
              required String label,
              required IconData prefixIcon,
              Widget? suffixIcon,
            }) {
              return InputDecoration(
                labelText: label,
                labelStyle: TextStyle(
                    color: isDark
                        ? AppColors.textMutedDark
                        : AppColors.textSecondaryLight),
                prefixIcon: Icon(prefixIcon, color: AppColors.primary),
                suffixIcon: suffixIcon,
                enabledBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                  borderSide: BorderSide(
                      color: isDark
                          ? AppColors.borderDark
                          : AppColors.borderLight),
                ),
                focusedBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                  borderSide: BorderSide(
                      color:
                          isDark ? AppColors.primaryLight : AppColors.primary),
                ),
                errorBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                  borderSide: const BorderSide(color: AppColors.danger),
                ),
                focusedErrorBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                  borderSide:
                      const BorderSide(color: AppColors.danger, width: 2),
                ),
              );
            }

            return Padding(
              padding: EdgeInsets.only(
                left: 20,
                right: 20,
                top: 10,
                bottom: MediaQuery.of(context).viewInsets.bottom + 24,
              ),
              child: Form(
                key: formKey,
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Container(
                      width: 40,
                      height: 4,
                      margin: const EdgeInsets.symmetric(vertical: 10),
                      decoration: BoxDecoration(
                        color: isDark
                            ? Colors.grey.shade700
                            : Colors.grey.shade300,
                        borderRadius: BorderRadius.circular(2),
                      ),
                    ),
                    const SizedBox(height: 10),
                    Text(
                      'Change Password',
                      style: TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.bold,
                        color: isDark
                            ? AppColors.textPrimaryDark
                            : AppColors.textPrimaryLight,
                      ),
                    ),
                    const SizedBox(height: 20),
                    TextFormField(
                      controller: currentPasswordController,
                      obscureText: obscureCurrent,
                      style: textStyle,
                      decoration: customInputDecoration(
                        label: 'Current Password',
                        prefixIcon: Icons.lock_outline_rounded,
                        suffixIcon: IconButton(
                          icon: Icon(
                            obscureCurrent
                                ? Icons.visibility_off_outlined
                                : Icons.visibility_outlined,
                            color: isDark
                                ? AppColors.textMutedDark
                                : AppColors.textSecondaryLight,
                          ),
                          onPressed: () => setSheetState(
                              () => obscureCurrent = !obscureCurrent),
                        ),
                      ),
                      validator: (v) => v == null || v.isEmpty
                          ? 'Please enter current password'
                          : null,
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: newPasswordController,
                      obscureText: obscureNew,
                      style: textStyle,
                      decoration: customInputDecoration(
                        label: 'New Password',
                        prefixIcon: Icons.lock_reset_rounded,
                        suffixIcon: IconButton(
                          icon: Icon(
                            obscureNew
                                ? Icons.visibility_off_outlined
                                : Icons.visibility_outlined,
                            color: isDark
                                ? AppColors.textMutedDark
                                : AppColors.textSecondaryLight,
                          ),
                          onPressed: () =>
                              setSheetState(() => obscureNew = !obscureNew),
                        ),
                      ),
                      validator: (v) {
                        if (v == null || v.isEmpty) {
                          return 'Please enter new password';
                        }
                        if (v.length < 8) {
                          return 'Password must be at least 8 characters';
                        }
                        return null;
                      },
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: confirmPasswordController,
                      obscureText: obscureConfirm,
                      style: textStyle,
                      decoration: customInputDecoration(
                        label: 'Confirm New Password',
                        prefixIcon: Icons.lock_clock_outlined,
                        suffixIcon: IconButton(
                          icon: Icon(
                            obscureConfirm
                                ? Icons.visibility_off_outlined
                                : Icons.visibility_outlined,
                            color: isDark
                                ? AppColors.textMutedDark
                                : AppColors.textSecondaryLight,
                          ),
                          onPressed: () => setSheetState(
                              () => obscureConfirm = !obscureConfirm),
                        ),
                      ),
                      validator: (v) {
                        if (v == null || v.isEmpty) {
                          return 'Please confirm new password';
                        }
                        if (v != newPasswordController.text) {
                          return 'Passwords do not match';
                        }
                        return null;
                      },
                    ),
                    const SizedBox(height: 24),
                    SizedBox(
                      width: double.infinity,
                      height: 50,
                      child: ElevatedButton(
                        style: ElevatedButton.styleFrom(
                          backgroundColor: AppColors.primary,
                          shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(12)),
                        ),
                        onPressed: isSaving
                            ? null
                            : () async {
                                if (formKey.currentState?.validate() == true) {
                                  setSheetState(() => isSaving = true);
                                  try {
                                    await ApiService.changePassword(
                                      email: _userEmail,
                                      currentPassword:
                                          currentPasswordController.text,
                                      newPassword: newPasswordController.text,
                                    );
                                    if (context.mounted) {
                                      Navigator.pop(context);
                                    }
                                    _showSnackBar(
                                        'Password changed successfully!',
                                        AppColors.success);
                                  } catch (e) {
                                    _showSnackBar(
                                        e
                                            .toString()
                                            .replaceAll('Exception: ', ''),
                                        AppColors.danger);
                                  } finally {
                                    setSheetState(() => isSaving = false);
                                  }
                                }
                              },
                        child: isSaving
                            ? const SizedBox(
                                width: 24,
                                height: 24,
                                child: CircularProgressIndicator(
                                    color: Colors.white, strokeWidth: 2),
                              )
                            : const Text(
                                'Update Password',
                                style: TextStyle(
                                    color: Colors.white,
                                    fontSize: 15,
                                    fontWeight: FontWeight.bold),
                              ),
                      ),
                    ),
                  ],
                ),
              ),
            );
          },
        );
      },
    );
  }

  void _showNotificationSettingsSheet() {
    showModalBottomSheet(
      context: context,
      backgroundColor: Theme.of(context).brightness == Brightness.dark
          ? AppColors.surfaceDark
          : Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      builder: (context) {
        return StatefulBuilder(
          builder: (context, setSheetState) {
            final isDark = Theme.of(context).brightness == Brightness.dark;
            final textStyle = TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.w600,
              color: isDark
                  ? AppColors.textPrimaryDark
                  : AppColors.textPrimaryLight,
            );
            final subtitleStyle = TextStyle(
              fontSize: 12,
              color: isDark
                  ? AppColors.textMutedDark
                  : AppColors.textSecondaryLight,
            );

            return Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Container(
                    width: 40,
                    height: 4,
                    margin: const EdgeInsets.only(bottom: 16),
                    decoration: BoxDecoration(
                      color:
                          isDark ? Colors.grey.shade700 : Colors.grey.shade300,
                      borderRadius: BorderRadius.circular(2),
                    ),
                  ),
                  Text(
                    'Notification Settings',
                    style: TextStyle(
                      fontSize: 18,
                      fontWeight: FontWeight.bold,
                      color: isDark
                          ? AppColors.textPrimaryDark
                          : AppColors.textPrimaryLight,
                    ),
                  ),
                  const SizedBox(height: 16),
                  SwitchListTile(
                    activeThumbColor: AppColors.primary,
                    title: Text('Appointment Reminders', style: textStyle),
                    subtitle: Text(
                        'Receive alerts for upcoming consultations and schedule updates',
                        style: subtitleStyle),
                    value: _notifyAppointments,
                    onChanged: (val) {
                      setSheetState(() => _notifyAppointments = val);
                      _updateSetting('patient_notify_appointments', val);
                    },
                  ),
                  const Divider(),
                  SwitchListTile(
                    activeThumbColor: AppColors.primary,
                    title: Text('Lab Reports Alert', style: textStyle),
                    subtitle: Text(
                        'Get notified as soon as diagnostics/reports are published',
                        style: subtitleStyle),
                    value: _notifyLabReports,
                    onChanged: (val) {
                      setSheetState(() => _notifyLabReports = val);
                      _updateSetting('patient_notify_lab_reports', val);
                    },
                  ),
                  const Divider(),
                  SwitchListTile(
                    activeThumbColor: AppColors.primary,
                    title: Text('Hospital Announcements', style: textStyle),
                    subtitle: Text(
                        'Stay informed about holiday closures, camps, and clinic details',
                        style: subtitleStyle),
                    value: _notifyAnnouncements,
                    onChanged: (val) {
                      setSheetState(() => _notifyAnnouncements = val);
                      _updateSetting('patient_notify_announcements', val);
                    },
                  ),
                  const SizedBox(height: 20),
                ],
              ),
            );
          },
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    return _PageScaffold(
      children: [
        _SettingsTile(
          Icons.lock_outline_rounded,
          'Change password',
          'Update your account password',
          onTap: _showChangePasswordSheet,
        ),
        _SettingsTile(
          Icons.notifications_none_rounded,
          'Notification settings',
          'Appointment, report, and system alerts',
          onTap: _showNotificationSettingsSheet,
        ),
        ValueListenableBuilder<ThemeMode>(
          valueListenable: ThemeController.mode,
          builder: (context, themeMode, _) {
            final isDarkMode = themeMode == ThemeMode.dark;
            return _ThemeModeTile(isDarkMode: isDarkMode);
          },
        ),
      ],
    );
  }
}

class _SupportSection extends StatelessWidget {
  const _SupportSection();

  @override
  Widget build(BuildContext context) {
    return const _PageScaffold(
      children: [
        _HeroCard(
          title: 'Help & Support',
          subtitle:
              'Get help with the app, hospital services, emergencies, and account access.',
          icon: Icons.help_outline_rounded,
          actions: ['Contact Hospital', 'Report Problem'],
        ),
        SizedBox(height: 16),
        _SettingsTile(Icons.question_answer_outlined, 'FAQs',
            'Common patient portal questions'),
        _SettingsTile(Icons.support_agent_rounded, 'Technical support',
            'App issues and login problems'),
        _SettingsTile(Icons.emergency_outlined, 'Emergency information',
            'Urgent contact and hospital details'),
        _SettingsTile(Icons.info_outline_rounded, 'About application',
            'MediCore Patient Portal'),
      ],
    );
  }
}

class _PageScaffold extends StatelessWidget {
  final List<Widget> children;

  const _PageScaffold({required this.children});

  @override
  Widget build(BuildContext context) {
    return ListView(
      physics: const BouncingScrollPhysics(),
      padding: const EdgeInsets.fromLTRB(18, 18, 18, 28),
      children: children,
    );
  }
}

class _HeroCard extends StatelessWidget {
  final String title;
  final String subtitle;
  final IconData icon;
  final List<String> actions;
  final ValueChanged<String>? onAction;

  const _HeroCard({
    required this.title,
    required this.subtitle,
    required this.icon,
    required this.actions,
    this.onAction,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: [Color(0xFF0369A1), Color(0xFF0284C7), Color(0xFF4F46E5)],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(24),
        boxShadow: [
          BoxShadow(
            color: AppColors.primary.withValues(alpha: 0.24),
            blurRadius: 26,
            offset: const Offset(0, 14),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          CircleAvatar(
            backgroundColor: Colors.white.withValues(alpha: 0.16),
            child: Icon(icon, color: Colors.white),
          ),
          const SizedBox(height: 16),
          Text(
            title,
            style: const TextStyle(
                color: Colors.white,
                fontSize: 24,
                fontWeight: FontWeight.w900,
                height: 1.1),
          ),
          const SizedBox(height: 8),
          Text(
            subtitle,
            style: const TextStyle(
                color: Colors.white70,
                fontSize: 13,
                fontWeight: FontWeight.w600,
                height: 1.35),
          ),
          const SizedBox(height: 16),
          Wrap(
            spacing: 10,
            runSpacing: 10,
            children: actions
                .map((label) => _WhitePill(
                      label: label,
                      onTap: onAction == null ? null : () => onAction!(label),
                    ))
                .toList(),
          ),
        ],
      ),
    );
  }
}

class _WhitePill extends StatelessWidget {
  final String label;
  final VoidCallback? onTap;

  const _WhitePill({required this.label, this.onTap});

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.white,
      borderRadius: BorderRadius.circular(12),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(12),
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
          child: Text(label,
              style: const TextStyle(
                  color: AppColors.primary,
                  fontSize: 12,
                  fontWeight: FontWeight.w800)),
        ),
      ),
    );
  }
}

class _SectionTitle extends StatelessWidget {
  final String title;

  const _SectionTitle(this.title);

  @override
  Widget build(BuildContext context) {
    return Text(title,
        style: Theme.of(context)
            .textTheme
            .titleMedium
            ?.copyWith(fontWeight: FontWeight.w900));
  }
}

class _SurfaceCard extends StatelessWidget {
  final Widget child;

  const _SurfaceCard({required this.child});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: isDark ? AppColors.surfaceDark : AppColors.bgLightCard,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(
            color: isDark ? AppColors.borderDark : AppColors.borderLight),
      ),
      child: child,
    );
  }
}

class _InfoPanel extends StatelessWidget {
  final IconData icon;
  final String title;
  final String subtitle;
  final String trailing;

  const _InfoPanel({
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.trailing,
  });

  @override
  Widget build(BuildContext context) {
    return _SurfaceCard(
      child: Row(
        children: [
          CircleAvatar(
              backgroundColor: AppColors.primary.withValues(alpha: 0.12),
              child: Icon(icon, color: AppColors.primary)),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title,
                    style: const TextStyle(
                        fontSize: 14, fontWeight: FontWeight.w900)),
                const SizedBox(height: 3),
                Text(subtitle,
                    style: const TextStyle(
                        color: AppColors.textMutedLight,
                        fontSize: 12,
                        height: 1.35)),
              ],
            ),
          ),
          const SizedBox(width: 8),
          Text(trailing,
              style: const TextStyle(
                  color: AppColors.primary,
                  fontSize: 11,
                  fontWeight: FontWeight.w900)),
        ],
      ),
    );
  }
}

class _AppointmentCard extends StatelessWidget {
  final Appointment appointment;
  final bool compact;
  final VoidCallback? onCancel;
  final VoidCallback? onReschedule;

  const _AppointmentCard({
    required this.appointment,
    this.compact = false,
    this.onCancel,
    this.onReschedule,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final reason = appointment.reason.trim();
    final cancellationReason = appointment.cancellationReason.trim();
    final location = _formatAppointmentLocation(appointment);
    return _SurfaceCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const CircleAvatar(
                  backgroundColor: Color(0xFFE0F2FE),
                  child: Icon(Icons.medical_services_outlined,
                      color: AppColors.primary)),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      appointment.doctorName,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        fontWeight: FontWeight.w900,
                        color: isDark
                            ? AppColors.textPrimaryDark
                            : AppColors.textPrimaryLight,
                      ),
                    ),
                    Text(appointment.specialty,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(
                            color: isDark
                                ? AppColors.textMutedDark
                                : AppColors.textMutedLight,
                            fontSize: 12)),
                  ],
                ),
              ),
              _StatusBadge(label: appointment.status),
            ],
          ),
          const SizedBox(height: 8),
          Text(
            'Appointment No: ${appointment.appointmentNumber}',
            style: const TextStyle(
              color: AppColors.primary,
              fontSize: 12,
              fontWeight: FontWeight.w800,
            ),
          ),
          if (location.isNotEmpty) ...[
            const SizedBox(height: 8),
            Row(
              children: [
                const Icon(Icons.meeting_room_outlined,
                    size: 15, color: AppColors.textMutedLight),
                const SizedBox(width: 6),
                Expanded(
                  child: Text(
                    location,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                        color: AppColors.textMutedLight,
                        fontSize: 12,
                        fontWeight: FontWeight.w700),
                  ),
                ),
              ],
            ),
          ],
          if (reason.isNotEmpty || cancellationReason.isNotEmpty) ...[
            const SizedBox(height: 10),
            Text(
              cancellationReason.isNotEmpty ? cancellationReason : reason,
              maxLines: compact ? 1 : 2,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                color: AppColors.textMutedLight,
                fontSize: 12,
                height: 1.35,
              ),
            ),
          ],
          const SizedBox(height: 14),
          Row(
            children: [
              Expanded(
                  child: _MiniMetric(
                      label: 'Date',
                      value: _formatAppointmentDate(appointment))),
              const SizedBox(width: 8),
              Expanded(
                  child: _MiniMetric(
                      label: 'Time',
                      value: _formatAppointmentTime(appointment))),
              const SizedBox(width: 8),
              Expanded(
                  child: _MiniMetric(
                      label: 'Fee',
                      value: _formatFee(appointment.consultationFee))),
            ],
          ),
          if (onReschedule != null || onCancel != null) ...[
            const SizedBox(height: 12),
            Row(
              children: [
                if (onReschedule != null)
                  Expanded(
                    child: _OutlineAction(
                      label: 'Reschedule',
                      onTap: onReschedule,
                    ),
                  ),
                if (onReschedule != null && onCancel != null)
                  const SizedBox(width: 12),
                if (onCancel != null)
                  Expanded(
                    child: _OutlineAction(
                      label: 'Cancel',
                      danger: true,
                      onTap: onCancel,
                    ),
                  ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}

class _MiniMetric extends StatelessWidget {
  final String label;
  final String value;

  const _MiniMetric({required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Container(
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(
        color: isDark ? AppColors.surfaceDarkSecondary : AppColors.bgLight,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            label,
            style: TextStyle(
              color:
                  isDark ? AppColors.textMutedDark : AppColors.textMutedLight,
              fontSize: 10,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 2),
          Text(
            value,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w800,
              color: isDark
                  ? AppColors.textPrimaryDark
                  : AppColors.textPrimaryLight,
            ),
          ),
        ],
      ),
    );
  }
}

class _StatusBadge extends StatelessWidget {
  final String label;

  const _StatusBadge({required this.label});

  @override
  Widget build(BuildContext context) {
    final normalized = label.toLowerCase();
    final color = normalized == 'cancelled'
        ? AppColors.danger
        : normalized == 'requested'
            ? AppColors.warning
            : normalized == 'completed'
                ? AppColors.accent
                : AppColors.success;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 5),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(label,
          style: TextStyle(
              color: color, fontSize: 11, fontWeight: FontWeight.w900)),
    );
  }
}

class _OutlineAction extends StatelessWidget {
  final String label;
  final bool danger;
  final VoidCallback? onTap;

  const _OutlineAction({required this.label, this.danger = false, this.onTap});

  @override
  Widget build(BuildContext context) {
    final enabled = onTap != null;
    final color = enabled
        ? (danger ? AppColors.danger : AppColors.primary)
        : AppColors.textMutedLight;
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(12),
        child: Container(
          alignment: Alignment.center,
          padding: const EdgeInsets.symmetric(vertical: 10),
          decoration: BoxDecoration(
            border: Border.all(color: color.withValues(alpha: 0.32)),
            borderRadius: BorderRadius.circular(12),
          ),
          child: Text(label,
              style: TextStyle(
                  color: color, fontSize: 12, fontWeight: FontWeight.w900)),
        ),
      ),
    );
  }
}

class _NotificationTile extends StatelessWidget {
  final IconData icon;
  final String title;
  final String message;
  final String tag;

  const _NotificationTile(this.icon, this.title, this.message, this.tag);

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final muted = isDark ? AppColors.textMutedDark : AppColors.textMutedLight;
    return Container(
      margin: const EdgeInsets.only(bottom: 6),
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: isDark
            ? AppColors.surfaceDarkSecondary
            : const Color(0xFFF4F8FC),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 36,
            height: 36,
            decoration: BoxDecoration(
              color: AppColors.primary.withValues(alpha: 0.1),
              borderRadius: BorderRadius.circular(11),
            ),
            child: Icon(icon, color: AppColors.primary, size: 20),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title,
                    style: TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w700,
                      color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
                    )),
                const SizedBox(height: 5),
                Text(message,
                    style: TextStyle(fontSize: 12, height: 1.45, color: muted)),
                if (tag.isNotEmpty) ...[
                  const SizedBox(height: 9),
                  Text(tag,
                      style: TextStyle(fontSize: 11, color: muted)),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _ThemeModeTile extends StatelessWidget {
  final bool isDarkMode;

  const _ThemeModeTile({required this.isDarkMode});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: _SurfaceCard(
        child: Row(
          children: [
            CircleAvatar(
              backgroundColor: AppColors.accent.withValues(alpha: 0.12),
              child: Icon(
                isDarkMode ? Icons.dark_mode_rounded : Icons.light_mode_rounded,
                color: AppColors.accent,
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'App appearance',
                    style: TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w900,
                      color: isDarkMode
                          ? AppColors.textPrimaryDark
                          : AppColors.textPrimaryLight,
                    ),
                  ),
                  Text(
                    'Choose the mode you want',
                    style: TextStyle(
                      color: isDarkMode
                          ? AppColors.textMutedDark
                          : AppColors.textMutedLight,
                      fontSize: 12,
                    ),
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      Expanded(
                        child: _ThemeChoiceButton(
                          icon: Icons.light_mode_rounded,
                          label: 'Light',
                          selected: !isDarkMode,
                          onTap: () => ThemeController.setMode(ThemeMode.light),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: _ThemeChoiceButton(
                          icon: Icons.dark_mode_rounded,
                          label: 'Dark',
                          selected: isDarkMode,
                          onTap: () => ThemeController.setMode(ThemeMode.dark),
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ThemeChoiceButton extends StatelessWidget {
  final IconData icon;
  final String label;
  final bool selected;
  final VoidCallback onTap;

  const _ThemeChoiceButton({
    required this.icon,
    required this.label,
    required this.selected,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Material(
      color: selected
          ? AppColors.primary.withValues(alpha: isDark ? 0.22 : 0.12)
          : (isDark ? AppColors.surfaceDarkSecondary : AppColors.bgLight),
      borderRadius: BorderRadius.circular(12),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(12),
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 10),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(12),
            border: Border.all(
              color: selected
                  ? AppColors.primary
                  : (isDark ? AppColors.borderDark : AppColors.borderLight),
            ),
          ),
          child: Center(
            child: FittedBox(
              fit: BoxFit.scaleDown,
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(
                    icon,
                    color: selected
                        ? AppColors.primary
                        : (isDark
                            ? AppColors.textMutedDark
                            : AppColors.textMutedLight),
                    size: 16,
                  ),
                  const SizedBox(width: 5),
                  Text(
                    label,
                    maxLines: 1,
                    style: TextStyle(
                      color: selected
                          ? AppColors.primary
                          : (isDark
                              ? AppColors.textSecondaryDark
                              : AppColors.textSecondaryLight),
                      fontSize: 12,
                      fontWeight: FontWeight.w900,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _SettingsTile extends StatelessWidget {
  final IconData icon;
  final String title;
  final String subtitle;
  final VoidCallback? onTap;

  const _SettingsTile(this.icon, this.title, this.subtitle, {this.onTap});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Material(
        color: isDark ? AppColors.surfaceDark : AppColors.bgLightCard,
        borderRadius: BorderRadius.circular(18),
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: onTap,
          child: Container(
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(18),
              border: Border.all(
                  color: isDark ? AppColors.borderDark : AppColors.borderLight),
            ),
            child: Row(
              children: [
                CircleAvatar(
                  backgroundColor: AppColors.primary.withValues(alpha: 0.12),
                  child: Icon(icon, color: AppColors.primary),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        title,
                        style: TextStyle(
                          fontSize: 14,
                          fontWeight: FontWeight.w900,
                          color: isDark
                              ? AppColors.textPrimaryDark
                              : AppColors.textPrimaryLight,
                        ),
                      ),
                      Text(
                        subtitle,
                        style: TextStyle(
                          color: isDark
                              ? AppColors.textMutedDark
                              : AppColors.textMutedLight,
                          fontSize: 12,
                        ),
                      ),
                    ],
                  ),
                ),
                Icon(
                  Icons.chevron_right_rounded,
                  color: isDark
                      ? AppColors.textMutedDark
                      : AppColors.textMutedLight,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _HeaderIconButton extends StatelessWidget {
  final IconData icon;
  final int badgeCount;
  final VoidCallback onTap;

  const _HeaderIconButton({
    required this.icon,
    this.badgeCount = 0,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(12),
      child: Container(
        width: 40,
        height: 40,
        decoration: BoxDecoration(
          color: AppColors.primary.withValues(alpha: 0.1),
          borderRadius: BorderRadius.circular(12),
        ),
        child: Stack(
          alignment: Alignment.center,
          children: [
            Icon(icon, color: AppColors.primary, size: 21),
            if (badgeCount > 0)
              Positioned(
                right: 6,
                top: 6,
                child: Container(
                  padding: const EdgeInsets.all(2),
                  decoration: BoxDecoration(
                    color: AppColors.danger,
                    shape: BoxShape.circle,
                    border: Border.all(color: Colors.white, width: 1.5),
                  ),
                  constraints:
                      const BoxConstraints(minWidth: 10, minHeight: 10),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

String _initialsFor(String name) {
  final parts = name
      .trim()
      .split(RegExp(r'\s+'))
      .where((part) => part.isNotEmpty)
      .toList();
  if (parts.isEmpty) return 'PU';
  final first = parts.first[0].toUpperCase();
  final second = parts.length > 1 ? parts.last[0].toUpperCase() : '';
  return '$first$second';
}
