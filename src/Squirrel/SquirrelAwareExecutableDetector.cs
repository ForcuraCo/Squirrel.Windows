using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Threading;
using System.Xml.Linq;
using Mono.Cecil;
using Squirrel;
using Squirrel.NuGet;

namespace Squirrel
{
#if NET5_0_OR_GREATER
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
#endif
    internal static class SquirrelAwareExecutableDetector
    {
        const string SQUIRREL_AWARE_KEY = "SquirrelAwareVersion";

        public static List<string> GetAllSquirrelAwareApps(string directory, int minimumVersion = 1)
        {
            var di = new DirectoryInfo(directory);

            return di.EnumerateFiles()
                .Where(x => x.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                .Select(x => x.FullName)
                .Where(x => (GetSquirrelAwareVersion(x) ?? -1) >= minimumVersion)
                .ToList();
        }

        public static int? GetSquirrelAwareVersion(string exePath)
        {
            if (!File.Exists(exePath)) return null;
            var fullname = Path.GetFullPath(exePath);

            // ways to search for SquirrelAwareVersion, ordered by precedence
            // search exe-embedded values first, and if not found, move on to sidecar files
            var detectors = new Func<string, int?>[] {
                GetPESquirrelAwareVersion,
                GetEmbeddedManifestSquirrelAwareValue,
                GetVersionBlockSquirrelAwareValue,
                GetSidecarSquirrelAwareValue,
                GetSideBySideManifestSquirrelAwareValue,
                GetSideBySideDllManifestSquirrelAwareValue,
            };

            for (int i = 0; i < 3; i++) {
                bool error = false;
                foreach (var fn in detectors) {
                    try {
                        var v = fn(exePath);
                        if (v != null) return v;
                    } catch {
                        error = true;
                        // do not throw, otherwise other detectors will not run
                    }
                }

                if (!error) {
                    // we tried all the detectors and none of them threw, so we don't need to retry
                    break;
                }

                // retry 3 times with 100ms delay
                Thread.Sleep(100);
            }

            return null;
        }

        static int? GetVersionBlockSquirrelAwareValue(string executable)
        {
            return StringFileInfo.ReadVersionInfo(executable, out var vi)
                .Where(i => i.Key == SQUIRREL_AWARE_KEY)
                .Where(i => int.TryParse(i.Value, out var _))
                .Select(i => (int?) int.Parse(i.Value))
                .FirstOrDefault(i => i > 0);
        }

        static int? GetSidecarSquirrelAwareValue(string executable)
        {
            // Looks for a "MyApp.exe.squirrel" sidecar file
            // the file should contain just the integer version (eg. "1")
            var sidecarPath = executable + ".squirrel";
            if (File.Exists(sidecarPath)) {
                var txt = File.ReadAllText(sidecarPath);
                if (int.TryParse(txt, out var pv)) {
                    return pv;
                }
            }
            return null;
        }

        static int? GetSideBySideManifestSquirrelAwareValue(string executable)
        {
            // Looks for an external application manifest eg. "MyApp.exe.manifest"
            var manifestPath = executable + ".manifest";
            if (File.Exists(manifestPath)) {
                return ParseManifestAwareValue(File.ReadAllBytes(manifestPath));
            }
            return null;
        }

        static int? GetSideBySideDllManifestSquirrelAwareValue(string executable)
        {
            // Looks for an external application DLL manifest eg. "MyApp.dll.manifest"
            var manifestPath = Path.Combine(
                Path.GetDirectoryName(executable),
                Path.GetFileNameWithoutExtension(executable) + ".dll.manifest");
            if (File.Exists(manifestPath)) {
                return ParseManifestAwareValue(File.ReadAllBytes(manifestPath));
            }
            return null;
        }

        static int? GetEmbeddedManifestSquirrelAwareValue(string executable)
        {
            // Looks for an embedded application manifest
            byte[] buffer = null;
            using (var rr = new ResourceReader(executable))
                buffer = rr.ReadAssemblyManifest();
            return ParseManifestAwareValue(buffer);
        }

        static int? ParseManifestAwareValue(byte[] buffer)
        {
            if (buffer == null)
                return null;

            var document = XDocument.Load(new MemoryStream(buffer));
            var aware = document.Root.ElementsNoNamespace(SQUIRREL_AWARE_KEY).FirstOrDefault();
            if (aware != null && int.TryParse(aware.Value, out var pv)) {
                return pv;
            }

            return null;
        }

        static int? GetPESquirrelAwareVersion(string executable)
        {
            if (!File.Exists(executable)) return null;
            var fullname = Path.GetFullPath(executable);

            return Utility.Retry<int?>(() =>
                GetAssemblySquirrelAwareVersion(fullname) ?? GetPEVersionBlockSquirrelAwareValue(fullname));
        }

        static int? GetAssemblySquirrelAwareVersion(string executable)
        {
            try {
                using (var assembly = AssemblyDefinition.ReadAssembly(executable)) {
                    if (!assembly.HasCustomAttributes) return null;

                    var attrs = assembly.CustomAttributes;
                    var attribute = attrs.FirstOrDefault(x => {
                        if (x.AttributeType.FullName != typeof(AssemblyMetadataAttribute).FullName) return false;
                        if (x.ConstructorArguments.Count != 2) return false;
                        return x.ConstructorArguments[0].Value.ToString() == "SquirrelAwareVersion";
                    });

                    if (attribute == null) return null;

                    if (!Int32.TryParse(attribute.ConstructorArguments[1].Value.ToString(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var result)) {
                        return null;
                    }

                    return result;
                }
            } catch (FileLoadException) { return null; } catch (BadImageFormatException) { return null; }
        }

        static int? GetPEVersionBlockSquirrelAwareValue(string executable)
        {
            int size = NativeMethods.GetFileVersionInfoSize(executable, IntPtr.Zero);

            // Nice try, buffer overflow
            if (size <= 0 || size > 4096) return null;

            var buf = new byte[size];
            if (!NativeMethods.GetFileVersionInfo(executable, 0, size, buf)) return null;

            const string englishUS = "040904B0";
            const string neutral = "000004B0";
            var supportedLanguageCodes = new[] { englishUS, neutral };

            if (!supportedLanguageCodes.Any(
                languageCode =>
                    NativeMethods.VerQueryValue(
                        buf,
                        $"\\StringFileInfo\\{languageCode}\\SquirrelAwareVersion",
                        out var result, out var resultSize
                    )
            )) {
                return null;
            }

            // NB: I have **no** idea why, but Atom.exe won't return the version
            // number "1" despite it being in the resource file and being 100% 
            // identical to the version block that actually works. I've got stuff
            // to ship, so we're just going to return '1' if we find the name in 
            // the block at all. I hate myself for this.
            return 1;

#if __NOT__DEFINED_EVAR__
            int ret;
            string resultData = Marshal.PtrToStringAnsi(result, resultSize-1 /* Subtract one for null terminator */);
if (!Int32.TryParse(resultData, NumberStyles.Integer, CultureInfo.CurrentCulture, out ret)) return null;

return ret;
#endif
        }
    }
}



//public static List<string> GetAllSquirrelAwareApps(string directory, int minimumVersion = 1)
//{
//    var di = new DirectoryInfo(directory);

//    return di.EnumerateFiles()
//        .Where(x => x.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
//        .Select(x => x.FullName)
//        .Where(x => (GetPESquirrelAwareVersion(x) ?? -1) >= minimumVersion)
//        .ToList();
//}

