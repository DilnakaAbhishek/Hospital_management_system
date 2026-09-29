import 'package:flutter/material.dart';
import 'package:medicore_mobile/core/constants/app_colors.dart';
import 'package:medicore_mobile/models/vitals.dart';

class VitalsLoggerScreen extends StatefulWidget {
  const VitalsLoggerScreen({super.key});

  @override
  State<VitalsLoggerScreen> createState() => _VitalsLoggerScreenState();
}

class _VitalsLoggerScreenState extends State<VitalsLoggerScreen> {
  final _formKey = GlobalKey<FormState>();
  final _systolicController = TextEditingController(text: '120');
  final _diastolicController = TextEditingController(text: '80');
  final _heartRateController = TextEditingController(text: '72');
  final _tempController = TextEditingController(text: '36.6');
  final _spo2Controller = TextEditingController(text: '98');

  bool _isSaving = false;
  Vitals? _latestVitals;

  void _saveVitals() async {
    if (_formKey.currentState!.validate()) {
      setState(() => _isSaving = true);
      await Future.delayed(const Duration(milliseconds: 600));

      final vitals = Vitals(
        patientId: 1,
        systolicBp: int.parse(_systolicController.text),
        diastolicBp: int.parse(_diastolicController.text),
        heartRateBpm: int.parse(_heartRateController.text),
        temperatureCelcius: double.parse(_tempController.text),
        oxygenSaturationSpo2: int.parse(_spo2Controller.text),
      );

      setState(() {
        _isSaving = false;
        _latestVitals = vitals;
      });

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Vitals logged successfully! Synced with Health Record.'),
            backgroundColor: AppColors.success,
          ),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Daily Vitals Logger'),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(20.0),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                'Record Today\'s Health Vitals',
                style: TextStyle(
                  fontSize: 20,
                  fontWeight: FontWeight.bold,
                  color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
                ),
              ),
              const SizedBox(height: 4),
              const Text(
                'Log your daily measurements to assist AI triage and clinical tracking',
                style: TextStyle(color: AppColors.textSecondaryLight, fontSize: 13),
              ),
              const SizedBox(height: 24),

              Row(
                children: [
                  Expanded(
                    child: TextFormField(
                      controller: _systolicController,
                      keyboardType: TextInputType.number,
                      decoration: const InputDecoration(
                        labelText: 'Systolic (mmHg)',
                        prefixIcon: Icon(Icons.speed),
                      ),
                      validator: (v) => v == null || v.isEmpty ? 'Required' : null,
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: TextFormField(
                      controller: _diastolicController,
                      keyboardType: TextInputType.number,
                      decoration: const InputDecoration(
                        labelText: 'Diastolic (mmHg)',
                        prefixIcon: Icon(Icons.speed_outlined),
                      ),
                      validator: (v) => v == null || v.isEmpty ? 'Required' : null,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 16),

              TextFormField(
                controller: _heartRateController,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(
                  labelText: 'Heart Rate (BPM)',
                  prefixIcon: Icon(Icons.favorite_rounded, color: AppColors.danger),
                ),
                validator: (v) => v == null || v.isEmpty ? 'Required' : null,
              ),
              const SizedBox(height: 16),

              Row(
                children: [
                  Expanded(
                    child: TextFormField(
                      controller: _tempController,
                      keyboardType: const TextInputType.numberWithOptions(decimal: true),
                      decoration: const InputDecoration(
                        labelText: 'Temp (°C)',
                        prefixIcon: Icon(Icons.thermostat),
                      ),
                      validator: (value) {
                        final temperature = double.tryParse(value?.trim() ?? '');
                        if (temperature == null || !temperature.isFinite) {
                          return 'Enter a temperature in Celsius';
                        }
                        if (temperature < 25 || temperature > 45) {
                          return 'Enter 25–45°C; check the units';
                        }
                        return null;
                      },
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: TextFormField(
                      controller: _spo2Controller,
                      keyboardType: TextInputType.number,
                      decoration: const InputDecoration(
                        labelText: 'Oxygen SpO2 (%)',
                        prefixIcon: Icon(Icons.air),
                      ),
                      validator: (v) => v == null || v.isEmpty ? 'Required' : null,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 28),

              SizedBox(
                height: 50,
                child: ElevatedButton.icon(
                  onPressed: _isSaving ? null : _saveVitals,
                  icon: const Icon(Icons.save_rounded),
                  label: _isSaving
                      ? const SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2),
                        )
                      : const Text('Save Vitals Entry'),
                ),
              ),

              if (_latestVitals != null) ...[
                const SizedBox(height: 32),
                Text(
                  'Latest Vitals Summary',
                  style: TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.bold,
                    color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
                  ),
                ),
                const SizedBox(height: 12),
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(16.0),
                    child: Column(
                      children: [
                        ListTile(
                          leading: const Icon(Icons.favorite, color: AppColors.danger),
                          title: Text('Blood Pressure: ${_latestVitals!.systolicBp}/${_latestVitals!.diastolicBp} mmHg'),
                          subtitle: Text(_latestVitals!.bpStatus),
                        ),
                        const Divider(),
                        ListTile(
                          leading: const Icon(Icons.monitor_heart, color: AppColors.primary),
                          title: Text('Heart Rate: ${_latestVitals!.heartRateBpm} BPM'),
                          subtitle: Text(_latestVitals!.heartRateStatus),
                        ),
                        const Divider(),
                        ListTile(
                          leading: const Icon(Icons.thermostat, color: AppColors.warning),
                          title: Text('Temperature: ${_latestVitals!.temperatureCelcius}°C | SpO2: ${_latestVitals!.oxygenSaturationSpo2}%'),
                        ),
                      ],
                    ),
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
