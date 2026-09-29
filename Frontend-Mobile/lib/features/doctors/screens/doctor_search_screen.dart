import 'dart:async';

import 'package:flutter/material.dart';
import 'package:medicore_mobile/core/constants/app_colors.dart';
import 'package:medicore_mobile/core/services/api_service.dart';
import 'package:medicore_mobile/features/doctors/screens/doctor_profile_screen.dart';
import 'package:medicore_mobile/models/doctor.dart';

class DoctorSearchScreen extends StatefulWidget {
  final ValueChanged<DoctorSearchResult> onBookAppointment;

  const DoctorSearchScreen({
    super.key,
    required this.onBookAppointment,
  });

  @override
  State<DoctorSearchScreen> createState() => _DoctorSearchScreenState();
}

class _DoctorSearchScreenState extends State<DoctorSearchScreen> {
  final _searchController = TextEditingController();
  final _searchFocus = FocusNode();
  Timer? _debounce;
  List<DoctorSearchResult> _results = const [];
  List<DoctorSearchResult> _suggestions = const [];
  bool _loading = true;
  String? _error;
  int _requestVersion = 0;

  @override
  void initState() {
    super.initState();
    _searchFocus.addListener(_refreshSuggestions);
    _search();
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _searchFocus.removeListener(_refreshSuggestions);
    _searchFocus.dispose();
    _searchController.dispose();
    super.dispose();
  }

  void _refreshSuggestions() {
    if (mounted) setState(() {});
  }

  void _onSearchChanged(String value) {
    _debounce?.cancel();
    setState(() {
      if (value.trim().isEmpty) _suggestions = const [];
    });
    _debounce = Timer(const Duration(milliseconds: 300), () => _search(value));
  }

  Future<void> _search([String? value]) async {
    final query = (value ?? _searchController.text).trim();
    final requestVersion = ++_requestVersion;
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final doctors = await ApiService.searchDoctors(query: query);
      if (!mounted || requestVersion != _requestVersion) return;
      setState(() {
        _results = doctors;
        _suggestions = query.isEmpty ? const [] : doctors.take(5).toList();
        _loading = false;
      });
    } catch (_) {
      if (!mounted || requestVersion != _requestVersion) return;
      setState(() {
        _results = const [];
        _suggestions = const [];
        _error = 'Unable to search for doctors. Please try again.';
        _loading = false;
      });
    }
  }

  void _selectSuggestion(DoctorSearchResult doctor) {
    _debounce?.cancel();
    _requestVersion++;
    _searchController.text = doctor.fullName;
    _searchController.selection =
        TextSelection.collapsed(offset: _searchController.text.length);
    _searchFocus.unfocus();
    setState(() {
      _results = [doctor];
      _suggestions = const [];
      _loading = false;
      _error = null;
    });
  }

  void _clearSearch() {
    _debounce?.cancel();
    _searchController.clear();
    _suggestions = const [];
    _searchFocus.requestFocus();
    _search('');
  }

  void _openProfile(DoctorSearchResult doctor) {
    Navigator.push(
      context,
      MaterialPageRoute(
          builder: (_) => DoctorProfileScreen(doctorId: doctor.doctorId)),
    );
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final showSuggestions = _searchFocus.hasFocus &&
        _searchController.text.trim().isNotEmpty &&
        _suggestions.isNotEmpty;

    // This screen is also used directly from navigation routes.  Keeping a
    // Material ancestor here guarantees Material inputs work in every host.
    return Material(
      color: Colors.transparent,
      child: RefreshIndicator(
        onRefresh: _search,
        child: ListView(
          padding: const EdgeInsets.all(20),
          children: [
            TextField(
              key: const Key('doctor-search-field'),
              controller: _searchController,
              focusNode: _searchFocus,
              textInputAction: TextInputAction.search,
              onChanged: _onSearchChanged,
              onSubmitted: (value) {
                _debounce?.cancel();
                _searchFocus.unfocus();
                _search(value);
              },
              decoration: InputDecoration(
                hintText: 'Search by doctor name or specialization',
                prefixIcon: const Icon(Icons.search_rounded),
                suffixIcon: _searchController.text.isEmpty
                    ? null
                    : IconButton(
                        tooltip: 'Clear search',
                        onPressed: _clearSearch,
                        icon: const Icon(Icons.close_rounded),
                      ),
                filled: true,
                fillColor: isDark ? AppColors.surfaceDark : Colors.white,
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(14),
                  borderSide: BorderSide.none,
                ),
              ),
            ),
            if (showSuggestions) ...[
              const SizedBox(height: 4),
              Material(
                key: const Key('doctor-search-suggestions'),
                color: isDark ? AppColors.surfaceDark : Colors.white,
                clipBehavior: Clip.antiAlias,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(12),
                  side: BorderSide(
                      color: isDark
                          ? AppColors.borderDark
                          : AppColors.borderLight),
                ),
                child: Column(
                  children: _suggestions
                      .map((doctor) => ListTile(
                            leading: const Icon(Icons.person_search_rounded,
                                color: AppColors.primary),
                            title: Text('Dr. ${doctor.fullName}'),
                            subtitle: Text(doctor.specialization),
                            onTap: () => _selectSuggestion(doctor),
                          ))
                      .toList(),
                ),
              ),
            ],
            const SizedBox(height: 18),
            if (_loading)
              const Padding(
                padding: EdgeInsets.all(36),
                child: Center(child: CircularProgressIndicator()),
              )
            else if (_error != null)
              _MessageCard(
                icon: Icons.error_outline_rounded,
                message: _error!,
                actionLabel: 'Retry',
                onAction: _search,
              )
            else if (_results.isEmpty)
              const _MessageCard(
                icon: Icons.person_search_outlined,
                message: 'No approved doctors match your search.',
              )
            else ...[
              Text(
                '${_results.length} doctor${_results.length == 1 ? '' : 's'} found',
                style: const TextStyle(fontWeight: FontWeight.w800),
              ),
              const SizedBox(height: 12),
              ..._results.map((doctor) => Padding(
                    padding: const EdgeInsets.only(bottom: 12),
                    child: _DoctorResultCard(
                      doctor: doctor,
                      onViewProfile: () => _openProfile(doctor),
                      onBookAppointment: () => widget.onBookAppointment(doctor),
                    ),
                  )),
            ],
          ],
        ),
      ),
    );
  }
}

class _DoctorResultCard extends StatelessWidget {
  final DoctorSearchResult doctor;
  final VoidCallback onViewProfile;
  final VoidCallback onBookAppointment;

  const _DoctorResultCard({
    required this.doctor,
    required this.onViewProfile,
    required this.onBookAppointment,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Container(
      key: Key('doctor-card-${doctor.doctorId}'),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: isDark ? AppColors.surfaceDark : Colors.white,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(
            color: isDark ? AppColors.borderDark : AppColors.borderLight),
        boxShadow: isDark
            ? []
            : [
                BoxShadow(
                  color: Colors.black.withValues(alpha: 0.04),
                  blurRadius: 12,
                  offset: const Offset(0, 4),
                )
              ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              CircleAvatar(
                radius: 26,
                backgroundColor: AppColors.primary.withValues(alpha: 0.10),
                child: const Icon(Icons.medical_services_outlined,
                    color: AppColors.primary, size: 24),
              ),
              const SizedBox(width: 13),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Dr. ${doctor.fullName}',
                        style: TextStyle(
                            fontSize: 15,
                            fontWeight: FontWeight.w800,
                            color: isDark
                                ? AppColors.textPrimaryDark
                                : AppColors.textPrimaryLight)),
                    const SizedBox(height: 4),
                    Container(
                      padding: const EdgeInsets.symmetric(
                          horizontal: 9, vertical: 3),
                      decoration: BoxDecoration(
                        color: AppColors.primary.withValues(alpha: 0.10),
                        borderRadius: BorderRadius.circular(999),
                      ),
                      child: Text(
                        doctor.specialization,
                        style: const TextStyle(
                            color: AppColors.primary,
                            fontSize: 11,
                            fontWeight: FontWeight.w700),
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 14),
          Row(
            children: [
              Expanded(
                child: OutlinedButton.icon(
                  key: Key('view-doctor-${doctor.doctorId}'),
                  onPressed: onViewProfile,
                  icon: const Icon(Icons.person_outline_rounded, size: 16),
                  label: const Text('View Profile'),
                  style: OutlinedButton.styleFrom(
                    foregroundColor: AppColors.primary,
                    side: BorderSide(
                        color: AppColors.primary.withValues(alpha: 0.5)),
                    shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(12)),
                    padding: const EdgeInsets.symmetric(vertical: 10),
                    textStyle: const TextStyle(
                        fontSize: 13, fontWeight: FontWeight.w700),
                  ),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: FilledButton.icon(
                  key: Key('book-doctor-${doctor.doctorId}'),
                  onPressed: onBookAppointment,
                  icon: const Icon(Icons.calendar_month_rounded, size: 16),
                  label: const Text('Book'),
                  style: FilledButton.styleFrom(
                    backgroundColor: AppColors.primary,
                    shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(12)),
                    padding: const EdgeInsets.symmetric(vertical: 10),
                    textStyle: const TextStyle(
                        fontSize: 13, fontWeight: FontWeight.w700),
                  ),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _MessageCard extends StatelessWidget {
  final IconData icon;
  final String message;
  final String? actionLabel;
  final Future<void> Function()? onAction;

  const _MessageCard(
      {required this.icon,
      required this.message,
      this.actionLabel,
      this.onAction});

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.all(28),
        alignment: Alignment.center,
        child: Column(
          children: [
            Icon(icon, size: 42, color: AppColors.textMutedLight),
            const SizedBox(height: 10),
            Text(message, textAlign: TextAlign.center),
            if (actionLabel != null && onAction != null) ...[
              const SizedBox(height: 14),
              OutlinedButton(onPressed: onAction, child: Text(actionLabel!)),
            ],
          ],
        ),
      );
}
