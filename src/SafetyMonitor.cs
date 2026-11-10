using System;
using System.Management;
using System.Windows.Forms;

namespace Insominia
{
    internal enum SafetyVerdict
    {
        Fine,
        Warn,
        Stop,
    }

    internal sealed class SafetyReading
    {
        public bool OnCharger;
        public int BatteryPercent = -1;        // -1 = unknown
        public double TemperatureC = double.NaN; // NaN = sensor unavailable
        public string TemperatureNote = "not available";
        public SafetyVerdict Verdict = SafetyVerdict.Fine;
        public string Reason = "";

        public string BatteryText
        {
            get
            {
                if (BatteryPercent < 0) return "unknown";
                return BatteryPercent + "%" + (OnCharger ? " (charging)" : " (on battery)");
            }
        }

        public string TemperatureText
        {
            get
            {
                if (double.IsNaN(TemperatureC)) return "sensor " + TemperatureNote;
                return TemperatureC.ToString("0.0") + " C";
            }
        }
    }

    /// <summary>
    /// A closed laptop running at load cannot breathe, and on battery it drains
    /// fast. This watches both and tells the app when to stop so we never damage
    /// the machine or die mid-task without warning.
    /// </summary>
    internal sealed class SafetyMonitor
    {
        // Thresholds (overridable from settings.ini)
        public int WarnBatteryPercent = 25;
        public int StopBatteryPercent = 15;
        public double WarnTemperatureC = 80;
        public double StopTemperatureC = 90;

        private bool _temperatureSensorWorks = true;

        /// <summary>Why the temperature is unknown, for honest reporting in the UI.</summary>
        public string TemperatureProblem { get; private set; }

        public void LoadThresholds()
        {
            WarnBatteryPercent = AppSettings.GetInt("WarnBatteryPercent", WarnBatteryPercent);
            StopBatteryPercent = AppSettings.GetInt("StopBatteryPercent", StopBatteryPercent);
            WarnTemperatureC = AppSettings.GetInt("WarnTemperatureC", (int)WarnTemperatureC);
            StopTemperatureC = AppSettings.GetInt("StopTemperatureC", (int)StopTemperatureC);
        }

        public SafetyReading Read()
        {
            SafetyReading r = new SafetyReading();

            PowerStatus ps = SystemInformation.PowerStatus;
            r.OnCharger = ps.PowerLineStatus == PowerLineStatus.Online;

            if (ps.BatteryChargeStatus != BatteryChargeStatus.NoSystemBattery &&
                ps.BatteryChargeStatus != BatteryChargeStatus.Unknown &&
                ps.BatteryLifePercent >= 0 && ps.BatteryLifePercent <= 1)
            {
                r.BatteryPercent = (int)Math.Round(ps.BatteryLifePercent * 100f);
            }

            r.TemperatureC = ReadTemperatureC();
            r.TemperatureNote = TemperatureProblem ?? "not available";

            // Battery is the hard limit: running flat mid-task loses the work.
            if (!r.OnCharger && r.BatteryPercent >= 0)
            {
                if (r.BatteryPercent <= StopBatteryPercent)
                {
                    r.Verdict = SafetyVerdict.Stop;
                    r.Reason = "Battery is at " + r.BatteryPercent + "% and not charging.";
                    return r;
                }
                if (r.BatteryPercent <= WarnBatteryPercent)
                {
                    r.Verdict = SafetyVerdict.Warn;
                    r.Reason = "Battery is getting low (" + r.BatteryPercent + "%). Plug in if you can.";
                }
            }

            // Heat is the safety limit: a closed lid traps it.
            if (!double.IsNaN(r.TemperatureC))
            {
                if (r.TemperatureC >= StopTemperatureC)
                {
                    r.Verdict = SafetyVerdict.Stop;
                    r.Reason = "Laptop is too hot (" + r.TemperatureC.ToString("0") + " C).";
                    return r;
                }
                if (r.TemperatureC >= WarnTemperatureC && r.Verdict == SafetyVerdict.Fine)
                {
                    r.Verdict = SafetyVerdict.Warn;
                    r.Reason = "Laptop is running hot (" + r.TemperatureC.ToString("0") + " C).";
                }
            }

            return r;
        }

        /// <summary>
        /// ACPI thermal zone, in tenths of a Kelvin. Many laptops do not expose
        /// this at all - in that case we return NaN and the app is honest about
        /// not knowing, rather than pretending it is safe.
        /// </summary>
        private double ReadTemperatureC()
        {
            if (!_temperatureSensorWorks) return double.NaN;

            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                           @"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature"))
                {
                    double hottest = double.NaN;
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        using (mo)
                        {
                            object raw = mo["CurrentTemperature"];
                            if (raw == null) continue;
                            double celsius = (Convert.ToDouble(raw) - 2732.0) / 10.0;
                            if (celsius < -50 || celsius > 150) continue; // nonsense reading
                            if (double.IsNaN(hottest) || celsius > hottest) hottest = celsius;
                        }
                    }

                    if (double.IsNaN(hottest))
                    {
                        _temperatureSensorWorks = false;
                        TemperatureProblem = "not supported by this laptop";
                        Log.Write("temperature: no thermal zones reported; guard disabled");
                    }
                    return hottest;
                }
            }
            catch (Exception ex)
            {
                bool denied = ex is UnauthorizedAccessException ||
                              ex.Message.IndexOf("denied", StringComparison.OrdinalIgnoreCase) >= 0;

                TemperatureProblem = denied
                    ? "blocked (needs admin on this laptop)"
                    : "not available";

                // Log once, not every 15 seconds.
                if (_temperatureSensorWorks)
                    Log.Write("temperature guard disabled: " + TemperatureProblem + " (" + ex.Message.Trim() + ")");

                _temperatureSensorWorks = false;
                return double.NaN;
            }
        }
    }
}
