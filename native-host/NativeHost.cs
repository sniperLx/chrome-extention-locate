using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace ExtensionFolderLocator
{
    internal static class Program
    {
        private const string HostVersion = "1.0.0";
        private const int MaximumMessageSize = 1024 * 1024;
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer
        {
            MaxJsonLength = MaximumMessageSize
        };

        [STAThread]
        private static int Main(string[] args)
        {
            Console.OutputEncoding = new UTF8Encoding(false);

            if (args.Length > 0 && args[0] == "--version")
            {
                Console.WriteLine(HostVersion);
                return 0;
            }

            if (args.Length > 1 && args[0] == "--locate")
            {
                return RunLocateCommand(args[1]);
            }

            if (args.Length > 0 && args[0] == "--roots")
            {
                return RunRootsCommand();
            }

            try
            {
                using (Stream input = Console.OpenStandardInput())
                using (Stream output = Console.OpenStandardOutput())
                {
                    while (true)
                    {
                        string requestJson = ReadMessage(input);
                        if (requestJson == null)
                        {
                            return 0;
                        }

                        Dictionary<string, object> response;
                        try
                        {
                            Dictionary<string, object> request = DeserializeRequest(requestJson);
                            response = HandleRequest(request);
                        }
                        catch (Exception exception)
                        {
                            response = ErrorResponse("助手处理请求时出错：" + exception.Message);
                        }

                        WriteMessage(output, Serializer.Serialize(response));
                    }
                }
            }
            catch (Exception exception)
            {
                try
                {
                    using (Stream output = Console.OpenStandardOutput())
                    {
                        WriteMessage(output, Serializer.Serialize(ErrorResponse(exception.Message)));
                    }
                }
                catch
                {
                    // Native messaging has no secondary error channel when stdout is unavailable.
                }
                return 1;
            }
        }

        private static int RunLocateCommand(string extensionId)
        {
            Dictionary<string, object> response = LocateExtension(extensionId);
            Console.WriteLine(Serializer.Serialize(response));
            return Convert.ToBoolean(response["ok"]) ? 0 : 1;
        }

        private static int RunRootsCommand()
        {
            List<Dictionary<string, object>> roots = ExtensionLocator.GetBrowserRoots()
                .Select(root => new Dictionary<string, object>
                {
                    { "browser", root.Browser },
                    { "path", root.Path },
                    { "exists", Directory.Exists(root.Path) }
                })
                .ToList();

            Console.WriteLine(Serializer.Serialize(new Dictionary<string, object>
            {
                { "ok", true },
                { "hostVersion", HostVersion },
                { "roots", roots }
            }));
            return 0;
        }

        private static Dictionary<string, object> HandleRequest(Dictionary<string, object> request)
        {
            string action = GetString(request, "action").ToLowerInvariant();

            if (action == "ping")
            {
                return new Dictionary<string, object>
                {
                    { "ok", true },
                    { "host", "ExtensionFolderLocator" },
                    { "version", HostVersion }
                };
            }

            if (action != "locate" && action != "open")
            {
                return ErrorResponse("不支持的操作：" + action);
            }

            string extensionId = GetString(request, "extensionId");
            string browserHint = GetString(request, "browserHint");
            Dictionary<string, object> response = LocateExtension(extensionId, browserHint);
            if (!Convert.ToBoolean(response["ok"]) || !Convert.ToBoolean(response["found"]) || action != "open")
            {
                return response;
            }

            string path = Convert.ToString(response["path"], CultureInfo.InvariantCulture);
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = QuoteArgument(path),
                    UseShellExecute = false
                });
                response["opened"] = true;
            }
            catch (Exception exception)
            {
                return ErrorResponse("找到文件夹，但无法启动资源管理器：" + exception.Message);
            }

            return response;
        }

        private static Dictionary<string, object> LocateExtension(string extensionId, string browserHint = "")
        {
            if (string.IsNullOrWhiteSpace(extensionId) ||
                !Regex.IsMatch(extensionId, "^[a-p]{32}$", RegexOptions.IgnoreCase))
            {
                return ErrorResponse("扩展 ID 格式不正确");
            }

            List<ExtensionMatch> matches;
            try
            {
                matches = ExtensionLocator.Find(extensionId, browserHint);
            }
            catch (Exception exception)
            {
                return ErrorResponse("读取浏览器配置失败：" + exception.Message);
            }

            if (matches.Count == 0)
            {
                return new Dictionary<string, object>
                {
                    { "ok", true },
                    { "found", false },
                    { "message", "浏览器配置中没有找到该插件的有效安装目录。企业策略、便携版浏览器或自定义用户数据目录可能不在自动扫描范围内。" }
                };
            }

            ExtensionMatch best = matches
                .OrderByDescending(match => match.HasManifest)
                .ThenByDescending(match => match.IsPreferredLocation)
                .ThenByDescending(match => !string.IsNullOrEmpty(browserHint) &&
                    string.Equals(match.Browser, browserHint, StringComparison.OrdinalIgnoreCase))
                .ThenBy(match => match.Browser, StringComparer.OrdinalIgnoreCase)
                .ThenBy(match => match.Profile, StringComparer.OrdinalIgnoreCase)
                .First();

            List<Dictionary<string, object>> alternatives = matches
                .Where(match => !PathEquals(match.Path, best.Path))
                .Select(match => new Dictionary<string, object>
                {
                    { "path", match.Path },
                    { "browser", match.Browser },
                    { "profile", match.Profile }
                })
                .ToList();

            return new Dictionary<string, object>
            {
                { "ok", true },
                { "found", true },
                { "opened", false },
                { "path", best.Path },
                { "browser", best.Browser },
                { "profile", best.Profile },
                { "manifestName", best.ManifestName },
                { "manifestVersion", best.ManifestVersion },
                { "alternatives", alternatives }
            };
        }

        private static Dictionary<string, object> DeserializeRequest(string requestJson)
        {
            Dictionary<string, object> request =
                Serializer.Deserialize<Dictionary<string, object>>(requestJson);
            if (request == null)
            {
                throw new InvalidDataException("请求内容为空");
            }
            return request;
        }

        private static string GetString(Dictionary<string, object> dictionary, string key)
        {
            object value;
            if (dictionary == null || !dictionary.TryGetValue(key, out value) || value == null)
            {
                return string.Empty;
            }
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static Dictionary<string, object> ErrorResponse(string message)
        {
            return new Dictionary<string, object>
            {
                { "ok", false },
                { "found", false },
                { "error", message }
            };
        }

        private static string ReadMessage(Stream input)
        {
            byte[] lengthBuffer = new byte[4];
            if (!ReadExactly(input, lengthBuffer, 4))
            {
                return null;
            }

            int length = BitConverter.ToInt32(lengthBuffer, 0);
            if (length < 0 || length > MaximumMessageSize)
            {
                throw new InvalidDataException("本机消息长度无效");
            }

            byte[] messageBuffer = new byte[length];
            if (!ReadExactly(input, messageBuffer, length))
            {
                throw new EndOfStreamException("本机消息不完整");
            }

            return Encoding.UTF8.GetString(messageBuffer);
        }

        private static bool ReadExactly(Stream input, byte[] buffer, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int bytesRead = input.Read(buffer, offset, count - offset);
                if (bytesRead == 0)
                {
                    return false;
                }
                offset += bytesRead;
            }
            return true;
        }

        private static void WriteMessage(Stream output, string message)
        {
            byte[] payload = Encoding.UTF8.GetBytes(message);
            byte[] length = BitConverter.GetBytes(payload.Length);
            output.Write(length, 0, length.Length);
            output.Write(payload, 0, payload.Length);
            output.Flush();
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static bool PathEquals(string left, string right)
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class BrowserRoot
    {
        public BrowserRoot(string browser, string path)
        {
            Browser = browser;
            Path = path;
        }

        public string Browser { get; private set; }

        public string Path { get; private set; }
    }

    internal sealed class ExtensionMatch
    {
        public string Path { get; set; }

        public string Browser { get; set; }

        public string Profile { get; set; }

        public bool HasManifest { get; set; }

        public bool IsPreferredLocation { get; set; }

        public string ManifestName { get; set; }

        public string ManifestVersion { get; set; }
    }

    internal static class ExtensionLocator
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer
        {
            MaxJsonLength = int.MaxValue
        };

        public static List<BrowserRoot> GetBrowserRoots()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return new List<BrowserRoot>
            {
                new BrowserRoot("Chrome", Path.Combine(localAppData, "Google", "Chrome", "User Data")),
                new BrowserRoot("Chrome Beta", Path.Combine(localAppData, "Google", "Chrome Beta", "User Data")),
                new BrowserRoot("Chrome Dev", Path.Combine(localAppData, "Google", "Chrome Dev", "User Data")),
                new BrowserRoot("Chrome Canary", Path.Combine(localAppData, "Google", "Chrome SxS", "User Data")),
                new BrowserRoot("Edge", Path.Combine(localAppData, "Microsoft", "Edge", "User Data")),
                new BrowserRoot("Brave", Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data")),
                new BrowserRoot("Chromium", Path.Combine(localAppData, "Chromium", "User Data"))
            };
        }

        public static List<ExtensionMatch> Find(string extensionId, string browserHint = "")
        {
            List<BrowserRoot> roots = GetBrowserRoots();
            if (!string.IsNullOrEmpty(browserHint))
            {
                List<ExtensionMatch> preferredMatches = FindInRoots(
                    extensionId,
                    roots.Where(root => IsBrowserHintMatch(root, browserHint)));
                if (preferredMatches.Count > 0)
                {
                    return preferredMatches;
                }
            }

            return FindInRoots(extensionId, roots);
        }

        private static bool IsBrowserHintMatch(BrowserRoot root, string browserHint)
        {
            if (string.Equals(browserHint, "Chrome", StringComparison.OrdinalIgnoreCase))
            {
                return root.Browser.StartsWith("Chrome", StringComparison.OrdinalIgnoreCase);
            }
            return string.Equals(root.Browser, browserHint, StringComparison.OrdinalIgnoreCase);
        }

        private static List<ExtensionMatch> FindInRoots(
            string extensionId,
            IEnumerable<BrowserRoot> roots)
        {
            Dictionary<string, ExtensionMatch> matches =
                new Dictionary<string, ExtensionMatch>(StringComparer.OrdinalIgnoreCase);

            foreach (BrowserRoot root in roots)
            {
                if (!Directory.Exists(root.Path))
                {
                    continue;
                }

                IEnumerable<string> profileDirectories;
                try
                {
                    profileDirectories = Directory.EnumerateDirectories(root.Path)
                        .Where(IsProfileDirectory)
                        .ToList();
                }
                catch
                {
                    continue;
                }

                foreach (string profileDirectory in profileDirectories)
                {
                    string profileName = Path.GetFileName(profileDirectory);
                    ExtensionSetting setting = ReadExtensionSetting(profileDirectory, extensionId);
                    ExtensionMatch match = null;

                    if (setting != null && !string.IsNullOrWhiteSpace(setting.Path))
                    {
                        match = ResolvePreferencePath(root, profileDirectory, extensionId, setting);
                    }

                    if (match == null)
                    {
                        match = FindVersionDirectory(root, profileDirectory, extensionId);
                    }

                    if (match == null)
                    {
                        continue;
                    }

                    match.Browser = root.Browser;
                    match.Profile = profileName;

                    if (!matches.ContainsKey(match.Path))
                    {
                        matches[match.Path] = match;
                    }
                }
            }

            return matches.Values.ToList();
        }

        private static bool IsProfileDirectory(string directory)
        {
            return File.Exists(Path.Combine(directory, "Preferences")) ||
                   File.Exists(Path.Combine(directory, "Secure Preferences"));
        }

        private static ExtensionSetting ReadExtensionSetting(string profileDirectory, string extensionId)
        {
            string[] preferenceFiles = { "Secure Preferences", "Preferences" };
            foreach (string fileName in preferenceFiles)
            {
                string preferencePath = Path.Combine(profileDirectory, fileName);
                if (!File.Exists(preferencePath))
                {
                    continue;
                }

                try
                {
                    using (FileStream stream = new FileStream(
                        preferencePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete))
                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
                    {
                        Dictionary<string, object> preferences =
                            Serializer.Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
                        Dictionary<string, object> root = GetDictionary(preferences, "extensions");
                        Dictionary<string, object> settings = GetDictionary(root, "settings");
                        Dictionary<string, object> setting = GetDictionary(settings, extensionId);
                        if (setting == null)
                        {
                            continue;
                        }

                        return new ExtensionSetting
                        {
                            Path = GetString(setting, "path"),
                            Location = GetString(setting, "location"),
                            Manifest = GetDictionary(setting, "manifest")
                        };
                    }
                }
                catch
                {
                    // A browser may rewrite its preferences while this request is processing.
                }
            }

            return null;
        }

        private static ExtensionMatch ResolvePreferencePath(
            BrowserRoot root,
            string profileDirectory,
            string extensionId,
            ExtensionSetting setting)
        {
            string expandedPath = Environment.ExpandEnvironmentVariables(setting.Path);
            List<string> candidates = new List<string>();

            if (Path.IsPathRooted(expandedPath))
            {
                candidates.Add(expandedPath);
            }
            else
            {
                candidates.Add(Path.Combine(profileDirectory, "Extensions", expandedPath));
                candidates.Add(Path.Combine(profileDirectory, expandedPath));
                candidates.Add(Path.Combine(root.Path, expandedPath));
            }

            foreach (string candidate in candidates)
            {
                string normalized;
                try
                {
                    normalized = Path.GetFullPath(candidate);
                }
                catch
                {
                    continue;
                }

                if (!Directory.Exists(normalized))
                {
                    continue;
                }

                return CreateMatch(extensionId, normalized, setting, true);
            }

            return null;
        }

        private static ExtensionMatch FindVersionDirectory(
            BrowserRoot root,
            string profileDirectory,
            string extensionId)
        {
            string extensionDirectory = Path.Combine(profileDirectory, "Extensions", extensionId);
            if (!Directory.Exists(extensionDirectory))
            {
                return null;
            }

            List<string> versions;
            try
            {
                versions = Directory.EnumerateDirectories(extensionDirectory).ToList();
            }
            catch
            {
                versions = new List<string>();
            }

            versions = versions
                .Where(Directory.Exists)
                .OrderByDescending(path => ReadManifest(path).Version, new VersionTextComparer())
                .ToList();

            string selectedPath = versions.FirstOrDefault(path =>
                File.Exists(Path.Combine(path, "manifest.json"))) ?? versions.FirstOrDefault();

            if (selectedPath == null && File.Exists(Path.Combine(extensionDirectory, "manifest.json")))
            {
                selectedPath = extensionDirectory;
            }

            return selectedPath == null
                ? null
                : CreateMatch(extensionId, selectedPath, null, false);
        }

        private static ExtensionMatch CreateMatch(
            string extensionId,
            string path,
            ExtensionSetting setting,
            bool preferred)
        {
            ManifestData manifest = ReadManifest(path);
            return new ExtensionMatch
            {
                Path = path,
                HasManifest = manifest.Exists,
                IsPreferredLocation = preferred,
                ManifestName = manifest.Name,
                ManifestVersion = manifest.Version,
                Browser = string.Empty,
                Profile = string.Empty
            };
        }

        private static ManifestData ReadManifest(string extensionDirectory)
        {
            string manifestPath = Path.Combine(extensionDirectory, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                return new ManifestData();
            }

            try
            {
                using (FileStream stream = new FileStream(
                    manifestPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    Dictionary<string, object> manifest =
                        Serializer.Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
                    return new ManifestData
                    {
                        Exists = true,
                        Name = GetString(manifest, "name"),
                        Version = GetString(manifest, "version")
                    };
                }
            }
            catch
            {
                return new ManifestData { Exists = true };
            }
        }

        private static Dictionary<string, object> GetDictionary(
            Dictionary<string, object> dictionary,
            string key)
        {
            object value;
            if (dictionary == null || !dictionary.TryGetValue(key, out value))
            {
                return null;
            }
            return value as Dictionary<string, object>;
        }

        private static string GetString(Dictionary<string, object> dictionary, string key)
        {
            object value;
            if (dictionary == null || !dictionary.TryGetValue(key, out value) || value == null)
            {
                return string.Empty;
            }
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }

    internal sealed class ExtensionSetting
    {
        public string Path { get; set; }

        public string Location { get; set; }

        public Dictionary<string, object> Manifest { get; set; }
    }

    internal sealed class ManifestData
    {
        public bool Exists { get; set; }

        public string Name { get; set; }

        public string Version { get; set; }
    }

    internal sealed class VersionTextComparer : IComparer<string>
    {
        public int Compare(string left, string right)
        {
            Version leftVersion;
            Version rightVersion;
            bool leftValid = Version.TryParse(NormalizeVersion(left), out leftVersion);
            bool rightValid = Version.TryParse(NormalizeVersion(right), out rightVersion);

            if (leftValid && rightValid)
            {
                return leftVersion.CompareTo(rightVersion);
            }
            if (leftValid)
            {
                return 1;
            }
            if (rightValid)
            {
                return -1;
            }
            return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "0.0.0";
            }

            string normalized = value.Split('_')[0];
            int componentCount = normalized.Split('.').Length;
            while (componentCount < 4)
            {
                normalized += ".0";
                componentCount += 1;
            }
            return normalized;
        }
    }
}
