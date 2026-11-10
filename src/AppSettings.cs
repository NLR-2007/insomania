using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Insominia
{
    /// <summary>Tiny key=value settings file in %APPDATA%\Insominia\settings.ini</summary>
    internal static class AppSettings
    {
        private static readonly Dictionary<string, string> Values =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static string Dir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Insominia");
            }
        }

        public static string FilePath { get { return Path.Combine(Dir, "settings.ini"); } }
        public static string LogPath  { get { return Path.Combine(Dir, "insominia.log"); } }

        public static void Load()
        {
            Values.Clear();
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string raw in File.ReadAllLines(FilePath))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    Values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }
            catch (Exception ex) { Log.Write("settings load failed: " + ex.Message); }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var lines = new List<string> { "# Insominia settings - edited by the app" };
                foreach (var kv in Values) lines.Add(kv.Key + "=" + kv.Value);
                File.WriteAllLines(FilePath, lines);
            }
            catch (Exception ex) { Log.Write("settings save failed: " + ex.Message); }
        }

        public static string GetString(string key, string fallback)
        {
            string v;
            return Values.TryGetValue(key, out v) ? v : fallback;
        }

        public static int GetInt(string key, int fallback)
        {
            string v; int n;
            if (Values.TryGetValue(key, out v) &&
                int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) return n;
            return fallback;
        }

        public static bool GetBool(string key, bool fallback)
        {
            return GetInt(key, fallback ? 1 : 0) != 0;
        }

        public static void Set(string key, string value) { Values[key] = value; }
        public static void Set(string key, int value) { Values[key] = value.ToString(CultureInfo.InvariantCulture); }
        public static void Set(string key, bool value) { Values[key] = value ? "1" : "0"; }
        public static bool Has(string key) { return Values.ContainsKey(key); }
    }

    internal static class Log
    {
        private static readonly object Gate = new object();

        public static void Write(string message)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(AppSettings.Dir);
                    File.AppendAllText(AppSettings.LogPath,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
                }
            }
            catch { /* logging must never crash the app */ }
        }
    }
}
