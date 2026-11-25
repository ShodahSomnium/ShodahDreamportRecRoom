using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rr
{
    /// <summary>
    /// Finds the player's own Rec Room and the part of it this world reads.
    ///
    /// This world ships nothing from the game. It reads the player's installation at runtime, and
    /// of that installation only the Addressables bundles under
    /// &lt;Data&gt;\StreamingAssets\aa\StandaloneWindows64: plain UnityFS bundles with type trees,
    /// holding the props, tools, weapons, enemies, sounds, effects, sprites and fonts. The level
    /// files, sharedassets and resources are encrypted by the game and are left alone - the rooms
    /// are rebuilt, not read.
    ///
    /// The data folder is "RecRoom_Data" in the final build; older builds called it "Recroom_Release_Data" (Rec Room's
    /// own Player.log of 2025), so any "*_Data" folder holding the bundles counts.
    ///
    /// Where it looks, cheapest first (<see cref="Locate"/>): the world's install hint and the player's saved path
    /// (install.txt, written after every successful find); the path Rec Room's own Player.log names; Steam (its folder
    /// from the registry and the usual places, every library its libraryfolders.vdf lists, the app manifest's install
    /// folder name, the usual library names on every drive); the Meta (Oculus) PC store's libraries (registry and the
    /// usual places). When several copies exist the newest wins. If none is found, <see cref="Search"/> walks every
    /// fixed drive a few folders deep (on the worker: it can take a while), so a library in any folder is found once
    /// and remembered.
    /// </summary>
    public static class RrInstall
    {
        public const string SteamAppId = "471710";
        public const string DataFolder = "RecRoom_Data";

        /// <summary>The folder of bundles inside the data folder.</summary>
        public static readonly string AaFolder = Path.Combine("StreamingAssets", "aa", "StandaloneWindows64");

        /// <summary>The folder of bundles, relative to the install root, for the final build's data folder name.</summary>
        public static readonly string BundleFolder = Path.Combine(DataFolder, AaFolder);

        /// <summary>The Addressables catalog hash: changes whenever the bundles change.</summary>
        public static readonly string CatalogHash = Path.Combine(DataFolder, "StreamingAssets", "aa", "catalog.hash");

        /// <summary>Data folder names tried first (then any "*_Data").</summary>
        private static readonly string[] DataFolders = { "RecRoom_Data", "Recroom_Release_Data", "RecRoom_Release_Data" };

        private static readonly string[] SteamFolders = { "RecRoom", "Rec Room" };
        private static readonly string[] OculusFolders = { "against-gravity-rec-room" };

        /// <summary>Folders the drive search never enters.</summary>
        private static readonly string[] SkipFolders =
        {
            "Windows", "$Recycle.Bin", "System Volume Information", "ProgramData", "Recovery", "$WinREAgent", "PerfLogs",
            "AppData", "node_modules", ".git", "Library", "Temp", "WindowsApps", "Microsoft", "Intel", "NVIDIA", "AMD"
        };

        /// <summary>What the last <see cref="Locate"/> or <see cref="Search"/> did, for the log.</summary>
        public static string LastReport { get; private set; } = "";

        public static bool IsInstall(string root, out string missing)
        {
            missing = null;
            if (string.IsNullOrEmpty(root) || !SafeExists(root))
            {
                missing = "the folder does not exist";
                return false;
            }

            if (DataFolderOf(root) == null)
            {
                missing = BundleFolder;
                return false;
            }

            return true;
        }

        /// <summary>The install's data folder name that holds the bundles ("RecRoom_Data", an older name, or any "*_Data"), or null.</summary>
        public static string DataFolderOf(string root)
        {
            if (string.IsNullOrEmpty(root))
            {
                return null;
            }

            foreach (string name in DataFolders)
            {
                if (HasBundles(Path.Combine(root, name, AaFolder)))
                {
                    return name;
                }
            }

            try
            {
                foreach (string dir in Directory.GetDirectories(root, "*_Data"))
                {
                    if (HasBundles(Path.Combine(dir, AaFolder)))
                    {
                        return Path.GetFileName(dir);
                    }
                }
            }
            catch (Exception)
            {
                // An unreadable folder is not an install.
            }

            return null;
        }

        /// <summary>The cheap search (no drive walk): the newest valid install among every known place, or null.</summary>
        public static string Locate(string hint = null, IEnumerable<string> savedPaths = null)
        {
            var report = new StringBuilder();
            var tried = new List<string>();
            var found = new List<string>();

            if (!string.IsNullOrEmpty(hint))
            {
                Try(hint, "world hint", tried, found, report);
            }

            if (savedPaths != null)
            {
                foreach (string p in savedPaths)
                {
                    Try(p, "saved path", tried, found, report);
                }
            }

            foreach (string p in FromRecRoomLogs())
            {
                Try(p, "Rec Room's log", tried, found, report);
            }

            foreach (string library in Libraries())
            {
                foreach (string folder in SteamFoldersFor(library))
                {
                    Try(Path.Combine(library, "steamapps", "common", folder), "Steam library " + library, tried, found, report);
                }
            }

            foreach (string library in OculusLibraries())
            {
                foreach (string folder in OculusFolders)
                {
                    Try(Path.Combine(library, "Software", folder), "Meta library " + library, tried, found, report);
                    Try(Path.Combine(library, folder), "Meta library " + library, tried, found, report);
                }
            }

            string best = Newest(found);
            report.Insert(0, "install search: " + tried.Count + " places tried, " + found.Count + " install(s) found" + (best != null ? ", using " + best : "") + "\n");
            LastReport = report.ToString();
            return best;
        }

        /// <summary>
        /// The slow search: every fixed drive, up to <paramref name="depth"/> folders deep (system folders skipped), for a
        /// Steam library ("steamapps"), a Rec Room folder or a Meta library holding Rec Room. Stops after
        /// <paramref name="seconds"/>. Pure (no Unity calls): run it on the worker.
        /// </summary>
        public static string Search(Action<string> progress = null, int depth = 5, double seconds = 90.0)
        {
            var watch = Stopwatch.StartNew();
            var found = new List<string>();
            var tried = new List<string>();
            var report = new StringBuilder();
            int visited = 0;
            foreach (string drive in FixedDrives())
            {
                if (progress != null)
                {
                    progress("searching " + drive);
                }

                var level = new List<string> { drive };
                for (int d = 0; d <= depth && level.Count > 0 && watch.Elapsed.TotalSeconds < seconds; d++)
                {
                    var next = new List<string>();
                    foreach (string dir in level)
                    {
                        if (watch.Elapsed.TotalSeconds >= seconds)
                        {
                            break;
                        }

                        string[] subs;
                        try
                        {
                            subs = Directory.GetDirectories(dir);
                        }
                        catch (Exception)
                        {
                            continue;
                        }

                        foreach (string sub in subs)
                        {
                            visited++;
                            string name = Path.GetFileName(sub);
                            if (string.Equals(name, "steamapps", StringComparison.OrdinalIgnoreCase))
                            {
                                string library = Path.GetDirectoryName(sub);
                                foreach (string folder in SteamFoldersFor(library))
                                {
                                    Try(Path.Combine(sub, "common", folder), "drive search", tried, found, report);
                                }

                                continue;
                            }

                            if (IsRecRoomName(name))
                            {
                                Try(sub, "drive search", tried, found, report);
                            }

                            if (Skip(name))
                            {
                                continue;
                            }

                            next.Add(sub);
                        }
                    }

                    level = next;
                }
            }

            string best = Newest(found);
            LastReport = "drive search: " + visited + " folders in " + watch.Elapsed.TotalSeconds.ToString("0.0") + " s, " + found.Count + " install(s) found"
                + (best != null ? ", using " + best : "") + "\n" + report;
            return best;
        }

        public static string BundleDirectory(string root)
        {
            if (string.IsNullOrEmpty(root))
            {
                return null;
            }

            string data = DataFolderOf(root) ?? DataFolder;
            return Path.Combine(root, data, AaFolder);
        }

        /// <summary>Steam libraries: Steam's folders, those libraryfolders.vdf lists, and the usual names on every drive.</summary>
        public static List<string> Libraries()
        {
            var libraries = new List<string>();
            foreach (string steam in SteamRoots())
            {
                Add(libraries, steam);
                foreach (string manifest in new[] { Path.Combine(steam, "steamapps", "libraryfolders.vdf"), Path.Combine(steam, "config", "libraryfolders.vdf") })
                {
                    foreach (string line in ReadLines(manifest))
                    {
                        Add(libraries, VdfValue(line, "path"));
                    }
                }
            }

            foreach (string drive in FixedDrives())
            {
                foreach (string name in new[] { "SteamLibrary", "Steam", "Steam Library", "SteamGames", "Steam Games", "Games", "Games\\Steam", "Games\\SteamLibrary", "Program Files (x86)\\Steam", "Program Files\\Steam" })
                {
                    Add(libraries, Path.Combine(drive, name));
                }
            }

            return libraries;
        }

        // ------------------------------------------------------------------ the places

        private static void Try(string candidate, string source, List<string> tried, List<string> found, StringBuilder report)
        {
            if (string.IsNullOrEmpty(candidate))
            {
                return;
            }

            string full;
            try
            {
                full = Path.GetFullPath(candidate.Trim().Trim('"')).TrimEnd('\\', '/');
            }
            catch (Exception)
            {
                return;
            }

            foreach (string t in tried)
            {
                if (string.Equals(t, full, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            tried.Add(full);
            if (DataFolderOf(full) != null)
            {
                found.Add(full);
                report.Append("  found ").Append(full).Append(" (").Append(source).Append(", data ").Append(DataFolderOf(full)).Append(")\n");
            }
        }

        /// <summary>The copy whose bundles changed most recently (an old leftover copy loses to the up-to-date one).</summary>
        private static string Newest(List<string> found)
        {
            string best = null;
            DateTime bestAt = DateTime.MinValue;
            foreach (string root in found)
            {
                DateTime at = DateTime.MinValue;
                try
                {
                    string aa = BundleDirectory(root);
                    at = Directory.GetLastWriteTimeUtc(aa);
                    string hash = Path.Combine(Path.GetDirectoryName(aa), "catalog.hash");
                    if (File.Exists(hash) && File.GetLastWriteTimeUtc(hash) > at)
                    {
                        at = File.GetLastWriteTimeUtc(hash);
                    }
                }
                catch (Exception)
                {
                    // Unknown age: it still counts, as the oldest.
                }

                if (best == null || at > bestAt)
                {
                    best = root;
                    bestAt = at;
                }
            }

            return best;
        }

        /// <summary>The Rec Room folder names to try in a library: the app manifest's installdir first.</summary>
        private static IEnumerable<string> SteamFoldersFor(string library)
        {
            var names = new List<string>();
            string manifest = Path.Combine(library, "steamapps", "appmanifest_" + SteamAppId + ".acf");
            if (string.Equals(Path.GetFileName(library.TrimEnd('\\', '/')), "steamapps", StringComparison.OrdinalIgnoreCase))
            {
                manifest = Path.Combine(library, "appmanifest_" + SteamAppId + ".acf");
            }

            foreach (string line in ReadLines(manifest))
            {
                string dir = VdfValue(line, "installdir");
                if (!string.IsNullOrEmpty(dir))
                {
                    names.Add(dir);
                }
            }

            names.AddRange(SteamFolders);
            return names;
        }

        /// <summary>
        /// The install folder Rec Room's own Player.log names (LocalLow\Against Gravity\Rec Room): Unity writes
        /// "Discovering subsystems at path X:/.../RecRoom_Data/UnitySubsystems" at every start.
        /// </summary>
        private static IEnumerable<string> FromRecRoomLogs()
        {
            var roots = new List<string>();
            foreach (string dir in RecRoomLocalLow())
            {
                foreach (string log in new[] { "Player.log", "Player-prev.log" })
                {
                    foreach (string line in ReadLines(Path.Combine(dir, log), 400))
                    {
                        int at = line.IndexOf("_Data", StringComparison.OrdinalIgnoreCase);
                        int path = line.IndexOf(":/", StringComparison.Ordinal);
                        if (path < 0)
                        {
                            path = line.IndexOf(":\\", StringComparison.Ordinal);
                        }

                        if (at < 0 || path < 1 || path > at)
                        {
                            continue;
                        }

                        string full = line.Substring(path - 1, at + 5 - (path - 1)).Replace('/', '\\');
                        string root = Path.GetDirectoryName(full);
                        if (!string.IsNullOrEmpty(root) && !roots.Contains(root))
                        {
                            roots.Add(root);
                        }
                    }
                }
            }

            return roots;
        }

        private static IEnumerable<string> RecRoomLocalLow()
        {
            var dirs = new List<string>();
            try
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(local))
                {
                    dirs.Add(Path.Combine(Path.GetDirectoryName(local) ?? local, "LocalLow", "Against Gravity", "Rec Room"));
                }

                string profile = Environment.GetEnvironmentVariable("USERPROFILE");
                if (!string.IsNullOrEmpty(profile))
                {
                    dirs.Add(Path.Combine(profile, "AppData", "LocalLow", "Against Gravity", "Rec Room"));
                }
            }
            catch (Exception)
            {
                // No profile folder: the other places remain.
            }

            return dirs;
        }

        private static IEnumerable<string> SteamRoots()
        {
            var roots = new List<string>();
            try
            {
                string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (!string.IsNullOrEmpty(programFilesX86))
                {
                    roots.Add(Path.Combine(programFilesX86, "Steam"));
                }

                if (!string.IsNullOrEmpty(programFiles))
                {
                    roots.Add(Path.Combine(programFiles, "Steam"));
                }
            }
            catch (Exception)
            {
                // No special folders: the registry and the drives remain.
            }

            roots.Add(RegistryValue("HKEY_CURRENT_USER\\Software\\Valve\\Steam", "SteamPath"));
            roots.Add(RegistryValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Valve\\Steam", "InstallPath"));
            roots.Add(RegistryValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Valve\\Steam", "InstallPath"));
            string uninstall = RegistryValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Steam App " + SteamAppId, "InstallLocation");
            if (!string.IsNullOrEmpty(uninstall))
            {
                // ...\steamapps\common\RecRoom -> the library root.
                try
                {
                    var common = Directory.GetParent(uninstall);
                    var steamapps = common != null ? common.Parent : null;
                    if (steamapps != null && steamapps.Parent != null)
                    {
                        roots.Add(steamapps.Parent.FullName);
                    }
                }
                catch (Exception)
                {
                    // A malformed value is skipped.
                }
            }

            var found = new List<string>();
            foreach (string r in roots)
            {
                if (string.IsNullOrEmpty(r))
                {
                    continue;
                }

                string root = r.Replace('/', '\\');
                if (SafeExists(Path.Combine(root, "steamapps")))
                {
                    Add(found, root);
                }
            }

            return found;
        }

        private static List<string> OculusLibraries()
        {
            var libraries = new List<string>();
            foreach (string path in OculusRegistryLibraries())
            {
                Add(libraries, path);
            }

            foreach (string drive in FixedDrives())
            {
                Add(libraries, Path.Combine(drive, "Program Files", "Oculus"));
                Add(libraries, Path.Combine(drive, "Program Files", "Oculus", "Software"));
                Add(libraries, Path.Combine(drive, "Oculus Apps"));
                Add(libraries, Path.Combine(drive, "Meta Apps"));
            }

            return libraries;
        }

        private static bool IsRecRoomName(string name)
        {
            foreach (string n in SteamFolders)
            {
                if (string.Equals(name, n, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            foreach (string n in OculusFolders)
            {
                if (string.Equals(name, n, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Skip(string name)
        {
            if (string.IsNullOrEmpty(name) || name.StartsWith(".", StringComparison.Ordinal) || name.StartsWith("$", StringComparison.Ordinal))
            {
                return true;
            }

            foreach (string s in SkipFolders)
            {
                if (string.Equals(name, s, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------ the Windows registry (optional)

        private static Type _registry;
        private static bool _registryChecked;

        /// <summary>
        /// A registry value, or null. Microsoft.Win32.Registry is not in every Unity player profile, so it is reached by
        /// reflection: where it is missing this simply returns null and the other places are searched.
        /// </summary>
        private static string RegistryValue(string key, string name)
        {
            try
            {
                var t = RegistryType();
                if (t == null)
                {
                    return null;
                }

                var get = t.GetMethod("GetValue", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(string), typeof(object) }, null);
                var value = get != null ? get.Invoke(null, new object[] { key, name, null }) : null;
                return value as string;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The Meta (Oculus) app libraries the user added: HKCU\Software\Oculus VR, LLC\Oculus\Libraries\*\OriginalPath.</summary>
        private static IEnumerable<string> OculusRegistryLibraries()
        {
            var paths = new List<string>();
            try
            {
                var t = RegistryType();
                if (t == null)
                {
                    return paths;
                }

                var hkcu = t.GetField("CurrentUser", BindingFlags.Public | BindingFlags.Static);
                object user = hkcu != null ? hkcu.GetValue(null) : null;
                object libs = Call(user, "OpenSubKey", "Software\\Oculus VR, LLC\\Oculus\\Libraries");
                var names = Call(libs, "GetSubKeyNames") as string[];
                if (names == null)
                {
                    return paths;
                }

                foreach (string n in names)
                {
                    object lib = Call(libs, "OpenSubKey", n);
                    string original = Call(lib, "GetValue", "OriginalPath") as string;
                    if (!string.IsNullOrEmpty(original))
                    {
                        paths.Add(original);
                    }
                }
            }
            catch (Exception)
            {
                // No registry: the usual folders remain.
            }

            return paths;
        }

        private static Type RegistryType()
        {
            if (!_registryChecked)
            {
                _registryChecked = true;
                try
                {
                    _registry = Type.GetType("Microsoft.Win32.Registry, mscorlib", false);
                }
                catch (Exception)
                {
                    _registry = null;
                }
            }

            return _registry;
        }

        private static object Call(object target, string method, params object[] args)
        {
            if (target == null)
            {
                return null;
            }

            var types = new Type[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                types[i] = args[i] != null ? args[i].GetType() : typeof(object);
            }

            var m = target.GetType().GetMethod(method, types);
            return m != null ? m.Invoke(target, args) : null;
        }

        // ------------------------------------------------------------------ helpers

        private static bool HasBundles(string aa)
        {
            try
            {
                if (!Directory.Exists(aa))
                {
                    return false;
                }

                using (var e = Directory.EnumerateFiles(aa, "*.bundle").GetEnumerator())
                {
                    return e.MoveNext();
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool SafeExists(string path)
        {
            try
            {
                return Directory.Exists(path);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void Add(List<string> list, string path)
        {
            if (string.IsNullOrEmpty(path) || !SafeExists(path))
            {
                return;
            }

            foreach (string existing in list)
            {
                if (string.Equals(existing.TrimEnd('\\', '/'), path.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            list.Add(path);
        }

        private static IEnumerable<string> FixedDrives()
        {
            var drives = new List<string>();
            try
            {
                foreach (var d in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (d.DriveType == DriveType.Fixed && d.IsReady)
                        {
                            drives.Add(d.RootDirectory.FullName);
                        }
                    }
                    catch (Exception)
                    {
                        // A drive that cannot be asked is skipped.
                    }
                }
            }
            catch (Exception)
            {
                // No drive list: the Steam manifest is all there is.
            }

            return drives;
        }

        private static string[] ReadLines(string path, int max = int.MaxValue)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return new string[0];
                }

                // Shared read: Rec Room's log may be open elsewhere.
                var lines = new List<string>();
                using (var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var r = new StreamReader(s))
                {
                    string line;
                    while (lines.Count < max && (line = r.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }
                }

                return lines.ToArray();
            }
            catch (Exception)
            {
                return new string[0];
            }
        }

        /// <summary>The value of a "key" "value" line of a Valve KeyValues file (libraryfolders.vdf, appmanifest_*.acf).</summary>
        private static string VdfValue(string line, string key)
        {
            if (line == null)
            {
                return null;
            }

            int k = line.IndexOf("\"" + key + "\"", StringComparison.OrdinalIgnoreCase);
            if (k < 0)
            {
                return null;
            }

            int open = line.IndexOf('"', k + key.Length + 2);
            if (open < 0)
            {
                return null;
            }

            int close = line.IndexOf('"', open + 1);
            if (close < 0)
            {
                return null;
            }

            return line.Substring(open + 1, close - open - 1).Replace("\\\\", "\\");
        }

        /// <summary>What was found, in words a player can act on.</summary>
        public static string Describe(string root, string savedFile = null)
        {
            var report = new StringBuilder();
            if (string.IsNullOrEmpty(root))
            {
                report.AppendLine("Rec Room was not found on this computer.");
                report.AppendLine();
                report.AppendLine("This world plays your own copy of Rec Room - it does not include it.");
                report.AppendLine("Install Rec Room (free on Steam), start it once, then come back.");
                if (!string.IsNullOrEmpty(savedFile))
                {
                    report.AppendLine();
                    report.AppendLine("Installed somewhere unusual? Put its folder path (the one with RecRoom.exe) in:");
                    report.AppendLine(savedFile);
                }

                return report.ToString();
            }

            report.AppendLine("Rec Room: " + root);
            string missing;
            if (!IsInstall(root, out missing))
            {
                report.AppendLine("Incomplete - missing " + missing + ".");
            }

            return report.ToString();
        }

        /// <summary>RrMain shutdown: nothing cached survives the world.</summary>
        public static void Reset()
        {
            LastReport = "";
            _registry = null;
            _registryChecked = false;
        }
    }
}
