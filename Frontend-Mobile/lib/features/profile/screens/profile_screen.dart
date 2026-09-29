import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:medicore_mobile/core/constants/app_colors.dart';
import 'package:medicore_mobile/core/services/api_service.dart';
import 'package:medicore_mobile/models/patient.dart';

class ProfileScreen extends StatefulWidget {
  const ProfileScreen({super.key});

  @override
  State<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends State<ProfileScreen> {
  Patient? _patient;
  bool _isLoading = true;

  String _userName = 'Patient';
  String _userEmail = '';
  String _userInitials = 'P';

  @override
  void initState() {
    super.initState();
    _loadProfileData();
  }

  Future<void> _loadProfileData() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final name = prefs.getString('patient_full_name');
      final email = prefs.getString('patient_email');

      // Derive initials & display name from session
      if (name != null && name.trim().isNotEmpty) {
        final parts = name.trim().split(' ');
        String initials = parts.first[0].toUpperCase();
        if (parts.length > 1) {
          initials += parts.last[0].toUpperCase();
        }
        setState(() {
          _userName = name;
          _userEmail = email ?? '';
          _userInitials = initials;
        });
      }

      final fetched = await ApiService.getMyProfile();
      if (fetched != null && mounted) {
        final parts =
            '${fetched.firstName} ${fetched.lastName}'.trim().split(' ');
        String initials = parts.first[0].toUpperCase();
        if (parts.length > 1) {
          initials += parts.last[0].toUpperCase();
        }
        setState(() {
          _patient = fetched;
          _userName = '${fetched.firstName} ${fetched.lastName}';
          _userInitials = initials;
        });
      }
    } catch (_) {
    } finally {
      if (mounted) {
        setState(() => _isLoading = false);
      }
    }
  }

  void _showEditProfileSheet() {
    if (_patient == null) return;

    final messenger = ScaffoldMessenger.of(context);
    final phoneController = TextEditingController(text: _patient!.phoneNumber);
    final addressController =
        TextEditingController(text: _patient!.address ?? '');
    final emergencyNameController =
        TextEditingController(text: _patient!.emergencyContactName ?? '');
    final emergencyPhoneController =
        TextEditingController(text: _patient!.emergencyContactPhone ?? '');
    final formKey = GlobalKey<FormState>();
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
            }) {
              return InputDecoration(
                labelText: label,
                labelStyle: TextStyle(
                    color: isDark
                        ? AppColors.textMutedDark
                        : AppColors.textSecondaryLight),
                prefixIcon: Icon(prefixIcon, color: AppColors.primary),
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
                child: SingleChildScrollView(
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
                        'Edit Profile Details',
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
                        controller: phoneController,
                        style: textStyle,
                        decoration: customInputDecoration(
                          label: 'Phone Number',
                          prefixIcon: Icons.phone_android_rounded,
                        ),
                        validator: (v) {
                          if (v == null || v.isEmpty) {
                            return 'Please enter phone number';
                          }
                          final regExp = RegExp(r'^\+?[0-9]{9,15}$');
                          if (!regExp.hasMatch(v.replaceAll(' ', ''))) {
                            return 'Enter a valid phone number (e.g., +94771234567)';
                          }
                          return null;
                        },
                      ),
                      const SizedBox(height: 16),
                      TextFormField(
                        controller: addressController,
                        style: textStyle,
                        decoration: customInputDecoration(
                          label: 'Address',
                          prefixIcon: Icons.home_outlined,
                        ),
                        validator: (v) {
                          if (v == null || v.isEmpty) {
                            return 'Please enter address';
                          }
                          if (v.trim().length < 5) {
                            return 'Address must be at least 5 characters long';
                          }
                          return null;
                        },
                      ),
                      const SizedBox(height: 16),
                      TextFormField(
                        controller: emergencyNameController,
                        style: textStyle,
                        decoration: customInputDecoration(
                          label: 'Emergency Contact Person',
                          prefixIcon: Icons.person_outline_rounded,
                        ),
                        validator: (v) {
                          if (v == null || v.isEmpty) {
                            return 'Please enter emergency contact name';
                          }
                          if (v.trim().length < 3) {
                            return 'Name must be at least 3 characters long';
                          }
                          final regExp = RegExp(r'^[a-zA-Z\s\.]+$');
                          if (!regExp.hasMatch(v.trim())) {
                            return 'Name must contain letters only';
                          }
                          return null;
                        },
                      ),
                      const SizedBox(height: 16),
                      TextFormField(
                        controller: emergencyPhoneController,
                        style: textStyle,
                        decoration: customInputDecoration(
                          label: 'Emergency Phone',
                          prefixIcon: Icons.phone_in_talk_rounded,
                        ),
                        validator: (v) {
                          if (v == null || v.isEmpty) {
                            return 'Please enter emergency phone number';
                          }
                          final regExp = RegExp(r'^\+?[0-9]{9,15}$');
                          if (!regExp.hasMatch(v.replaceAll(' ', ''))) {
                            return 'Enter a valid phone number (e.g., +94712345678)';
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
                                  if (formKey.currentState?.validate() ==
                                      true) {
                                    setSheetState(() => isSaving = true);
                                    try {
                                      final data = {
                                        'patientId': _patient!.patientId,
                                        'firstName': _patient!.firstName,
                                        'lastName': _patient!.lastName,
                                        'dateOfBirth': _patient!.dateOfBirth
                                            .toIso8601String(),
                                        'gender': _patient!.gender,
                                        'nic': _patient!.nic,
                                        'bloodGroup': _patient!.bloodGroup,
                                        'email': _patient!.email,
                                        'phoneNumber':
                                            phoneController.text.trim(),
                                        'address':
                                            addressController.text.trim(),
                                        'emergencyContactName':
                                            emergencyNameController.text.trim(),
                                        'emergencyContactPhone':
                                            emergencyPhoneController.text
                                                .trim(),
                                        'profileImageUrl':
                                            _patient!.profileImageUrl,
                                      };
                                      await ApiService.updateMyProfile(data);
                                      if (!context.mounted || !mounted) {
                                        return;
                                      }
                                      Navigator.pop(context);
                                      _loadProfileData();
                                      messenger.showSnackBar(
                                        SnackBar(
                                          content: const Row(
                                            children: [
                                              Icon(Icons.check_circle_rounded,
                                                  color: Colors.white),
                                              SizedBox(width: 10),
                                              Text(
                                                  'Profile details updated successfully!'),
                                            ],
                                          ),
                                          backgroundColor: AppColors.success,
                                          behavior: SnackBarBehavior.floating,
                                          shape: RoundedRectangleBorder(
                                              borderRadius:
                                                  BorderRadius.circular(10)),
                                        ),
                                      );
                                    } catch (e) {
                                      messenger.showSnackBar(
                                        SnackBar(
                                          content: Text('Failed to update: $e'),
                                          backgroundColor: AppColors.danger,
                                          behavior: SnackBarBehavior.floating,
                                          shape: RoundedRectangleBorder(
                                              borderRadius:
                                                  BorderRadius.circular(10)),
                                        ),
                                      );
                                    } finally {
                                      if (context.mounted) {
                                        setSheetState(() => isSaving = false);
                                      }
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
                                  'Save Changes',
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
              ),
            );
          },
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Scaffold(
      body: _isLoading
          ? const Center(
              child: CircularProgressIndicator(color: AppColors.primary))
          : SingleChildScrollView(
              physics: const BouncingScrollPhysics(),
              padding: const EdgeInsets.all(20.0),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.center,
                children: [
                  // Profile Header Avatar Box
                  Container(
                    padding: const EdgeInsets.all(4),
                    decoration: BoxDecoration(
                      shape: BoxShape.circle,
                      border: Border.all(color: AppColors.primary, width: 3),
                    ),
                    child: CircleAvatar(
                      radius: 46,
                      backgroundColor: AppColors.primary,
                      child: Text(
                        _userInitials,
                        style: const TextStyle(
                          fontSize: 32,
                          fontWeight: FontWeight.bold,
                          color: Colors.white,
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(height: 12),
                  Text(
                    _patient != null
                        ? '${_patient!.firstName} ${_patient!.lastName}'
                        : _userName,
                    style: TextStyle(
                      fontSize: 22,
                      fontWeight: FontWeight.bold,
                      color: isDark
                          ? AppColors.textPrimaryDark
                          : AppColors.textPrimaryLight,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    _userEmail.isNotEmpty ? _userEmail : 'Patient Portal User',
                    style: TextStyle(
                        color: isDark
                            ? AppColors.textSecondaryDark
                            : AppColors.textSecondaryLight,
                        fontSize: 13),
                  ),
                  const SizedBox(height: 12),

                  // Pill Badges for NIC & Blood Group
                  Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Container(
                        padding: const EdgeInsets.symmetric(
                            horizontal: 12, vertical: 6),
                        decoration: BoxDecoration(
                          color: AppColors.primary.withValues(alpha: 0.12),
                          borderRadius: BorderRadius.circular(20),
                        ),
                        child: Row(
                          children: [
                            const Icon(Icons.badge_outlined,
                                size: 16, color: AppColors.primary),
                            const SizedBox(width: 6),
                            Text(
                              'NIC: ${_patient?.nic ?? '199512345678'}',
                              style: const TextStyle(
                                color: AppColors.primary,
                                fontWeight: FontWeight.bold,
                                fontSize: 12,
                              ),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(width: 8),
                      Container(
                        padding: const EdgeInsets.symmetric(
                            horizontal: 12, vertical: 6),
                        decoration: BoxDecoration(
                          color: AppColors.danger.withValues(alpha: 0.12),
                          borderRadius: BorderRadius.circular(20),
                        ),
                        child: Row(
                          children: [
                            const Icon(Icons.water_drop_rounded,
                                size: 16, color: AppColors.danger),
                            const SizedBox(width: 6),
                            Text(
                              'Blood: ${_patient?.bloodGroup ?? 'O+'}',
                              style: const TextStyle(
                                color: AppColors.danger,
                                fontWeight: FontWeight.bold,
                                fontSize: 12,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 24),

                  // Personal Medical Records Card
                  Card(
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(16),
                      side: BorderSide(
                          color: isDark
                              ? AppColors.borderDark
                              : AppColors.borderLight),
                    ),
                    child: Padding(
                      padding: const EdgeInsets.all(16.0),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Row(
                            children: [
                              Icon(Icons.folder_shared_outlined,
                                  color: AppColors.primary, size: 20),
                              SizedBox(width: 8),
                              Text(
                                'Personal Identity & Details',
                                style: TextStyle(
                                    fontWeight: FontWeight.bold, fontSize: 15),
                              ),
                            ],
                          ),
                          const Divider(height: 24),
                          _buildProfileRow(
                              'Phone Number:',
                              _patient?.phoneNumber ?? '+94 77 123 4567',
                              isDark),
                          const SizedBox(height: 10),
                          _buildProfileRow(
                              'Gender:', _patient?.gender ?? 'Male', isDark),
                          const SizedBox(height: 10),
                          _buildProfileRow(
                              'Address:',
                              _patient?.address ?? '45 Galle Rd, Colombo 03',
                              isDark),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: 16),

                  // Emergency Contact Card
                  Card(
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(16),
                      side: BorderSide(
                          color: isDark
                              ? AppColors.borderDark
                              : AppColors.borderLight),
                    ),
                    child: Padding(
                      padding: const EdgeInsets.all(16.0),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Row(
                            children: [
                              Icon(Icons.phone_in_talk_rounded,
                                  color: AppColors.danger, size: 20),
                              SizedBox(width: 8),
                              Text(
                                'Emergency Contact',
                                style: TextStyle(
                                    fontWeight: FontWeight.bold, fontSize: 15),
                              ),
                            ],
                          ),
                          const Divider(height: 24),
                          _buildProfileRow(
                            'Contact Person:',
                            (_patient?.emergencyContactName != null &&
                                    _patient!.emergencyContactName!.isNotEmpty)
                                ? _patient!.emergencyContactName!
                                : 'Primary Emergency Contact',
                            isDark,
                          ),
                          const SizedBox(height: 10),
                          _buildProfileRow(
                            'Phone:',
                            (_patient?.emergencyContactPhone != null &&
                                    _patient!.emergencyContactPhone!.isNotEmpty)
                                ? _patient!.emergencyContactPhone!
                                : '+94 71 234 5678',
                            isDark,
                            isPrimaryText: true,
                          ),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: 16),

                  if (_patient != null)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 24),
                      child: SizedBox(
                        width: double.infinity,
                        height: 50,
                        child: ElevatedButton.icon(
                          style: ElevatedButton.styleFrom(
                            backgroundColor: AppColors.primary,
                            shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(12)),
                          ),
                          onPressed: _showEditProfileSheet,
                          icon: const Icon(Icons.edit_rounded,
                              color: Colors.white),
                          label: const Text(
                            'Edit Profile Details',
                            style: TextStyle(
                                color: Colors.white,
                                fontSize: 15,
                                fontWeight: FontWeight.bold),
                          ),
                        ),
                      ),
                    ),
                ],
              ),
            ),
    );
  }

  Widget _buildProfileRow(String label, String value, bool isDark,
      {bool isPrimaryText = false}) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: TextStyle(
              color: isDark
                  ? AppColors.textSecondaryDark
                  : AppColors.textSecondaryLight,
              fontSize: 13),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Text(
            value,
            textAlign: TextAlign.right,
            style: TextStyle(
              fontWeight: FontWeight.w600,
              fontSize: 13,
              color: isPrimaryText
                  ? AppColors.primary
                  : (isDark
                      ? AppColors.textPrimaryDark
                      : AppColors.textPrimaryLight),
            ),
          ),
        ),
      ],
    );
  }
}
