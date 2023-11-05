﻿using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Squirrel
{
    /// <summary>
    /// A hybrid implementation of SemVer that supports semantic versioning as described at http://semver.org while not strictly enforcing it to 
    /// allow older 4-digit versioning schemes to continue working.
    /// </summary>
    [Serializable]
    public sealed class SemanticVersion : IComparable, IComparable<SemanticVersion>, IEquatable<SemanticVersion>
    {
        private const RegexOptions _flags = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture;
        private static readonly Regex _semanticVersionRegex = new Regex(@"^(?<Version>\d+(\s*\.\s*\d+){0,3})(?<Release>-[a-z][0-9a-z-]*)?$", _flags);
        private static readonly Regex _strictSemanticVersionRegex = new Regex(@"^(?<Version>\d+(\.\d+){2})(?<Release>-[a-z][0-9a-z-]*)?$", _flags);
        private static readonly Regex _preReleaseVersionRegex = new Regex(@"(?<PreReleaseString>[a-z]+)(?<PreReleaseNumber>[0-9]+)$", _flags);
        private readonly string _originalString;

        /// <summary> Create a new instance of <see cref="SemanticVersion"/> with the specified version </summary>
        public SemanticVersion(string version)
            : this(Parse(version))
        {
            // The constructor normalizes the version string so that it we do not need to normalize it every time we need to operate on it. 
            // The original string represents the original form in which the version is represented to be used when printing.
            _originalString = version;
        }

        /// <inheritdoc cref="SemanticVersion(string)"/>
        public SemanticVersion(int major, int minor, int build)
            : this(new Version(major, minor, build, 0))
        {
        }

        /// <inheritdoc cref="SemanticVersion(string)"/>
        public SemanticVersion(int major, int minor, int build, int revision)
            : this(new Version(major, minor, build, revision))
        {
        }

        /// <inheritdoc cref="SemanticVersion(string)"/>
        public SemanticVersion(int major, int minor, int build, string specialVersion)
            : this(new Version(major, minor, build), specialVersion)
        {
        }

        /// <inheritdoc cref="SemanticVersion(string)"/>
        public SemanticVersion(Version version)
            : this(version, String.Empty)
        {
        }

        /// <inheritdoc cref="SemanticVersion(string)"/>
        public SemanticVersion(Version version, string specialVersion)
            : this(version, specialVersion, null)
        {
        }

        private SemanticVersion(Version version, string specialVersion, string originalString)
        {
            if (version == null) {
                throw new ArgumentNullException("version");
            }
            Version = NormalizeVersionValue(version);
            SpecialVersion = specialVersion ?? String.Empty;
            _originalString = String.IsNullOrEmpty(originalString) ? version.ToString() + (!String.IsNullOrEmpty(specialVersion) ? '-' + specialVersion : null) : originalString;
        }

        internal SemanticVersion(SemanticVersion semVer)
        {
            _originalString = semVer.ToString();
            Version = semVer.Version;
            SpecialVersion = semVer.SpecialVersion;
        }

        /// <summary> Gets the value of the major component of the version number for the current System.Version object. </summary>
        public int Major => Version.Major;

        /// <summary> Gets the value of the minor component of the version number for the current System.Version object. </summary>
        public int Minor => Version.Minor;

        /// <summary> Gets the value of the build component of the version number for the current System.Version object. </summary>
        public int Build => Version.Build;

        /// <summary> Gets the value of the revision component of the version number for the current System.Version object. </summary>
        public int Revision => Version.Revision;

        /// <summary>
        /// Gets the normalized version portion.
        /// </summary>
        public Version Version {
            get;
            private set;
        }

        /// <summary>
        /// Gets the optional special version.
        /// </summary>
        public string SpecialVersion {
            get;
            private set;
        }

        /// <summary>
        /// Gets the components of the original string used to construct this version instance.
        /// </summary>
        public string[] GetOriginalVersionComponents()
        {
            if (!String.IsNullOrEmpty(_originalString)) {
                string original;

                // search the start of the SpecialVersion part, if any
                int dashIndex = _originalString.IndexOf('-');
                if (dashIndex != -1) {
                    // remove the SpecialVersion part
                    original = _originalString.Substring(0, dashIndex);
                } else {
                    original = _originalString;
                }

                return SplitAndPadVersionString(original);
            } else {
                return SplitAndPadVersionString(Version.ToString());
            }
        }

        private static string[] SplitAndPadVersionString(string version)
        {
            string[] a = version.Split('.');
            if (a.Length == 4) {
                return a;
            } else {
                // if 'a' has less than 4 elements, we pad the '0' at the end 
                // to make it 4.
                var b = new string[4] { "0", "0", "0", "0" };
                Array.Copy(a, 0, b, 0, a.Length);
                return b;
            }
        }

        /// <summary>
        /// Parses a version string using loose semantic versioning rules that allows 2-4 version components followed by an optional special version.
        /// </summary>
        public static SemanticVersion Parse(string version)
        {
            if (String.IsNullOrEmpty(version)) {
                throw new ArgumentException("Argument_Cannot_Be_Null_Or_Empty", "version");
            }

            SemanticVersion semVer;
            if (!TryParse(version, out semVer)) {
                throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, "InvalidVersionString", version), "version");
            }
            return semVer;
        }

        /// <summary>
        /// Parses a version string using loose semantic versioning rules that allows 2-4 version components followed by an optional special version.
        /// </summary>
        public static bool TryParse(string version, out SemanticVersion value)
        {
            return TryParseInternal(version, _semanticVersionRegex, out value);
        }

        /// <summary>
        /// Parses a version string using strict semantic versioning rules that allows exactly 3 components and an optional special version.
        /// </summary>
        public static bool TryParseStrict(string version, out SemanticVersion value)
        {
            return TryParseInternal(version, _strictSemanticVersionRegex, out value);
        }

        private static bool TryParseInternal(string version, Regex regex, out SemanticVersion semVer)
        {
            semVer = null;
            if (String.IsNullOrEmpty(version)) {
                return false;
            }

            var match = regex.Match(version.Trim());
            Version versionValue;
            if (!match.Success || !Version.TryParse(match.Groups["Version"].Value, out versionValue)) {
                return false;
            }

            semVer = new SemanticVersion(NormalizeVersionValue(versionValue), match.Groups["Release"].Value.TrimStart('-'), version.Replace(" ", ""));
            return true;
        }

        /// <summary>
        /// Attempts to parse the version token as a SemanticVersion.
        /// </summary>
        /// <returns>An instance of SemanticVersion if it parses correctly, null otherwise.</returns>
        public static SemanticVersion ParseOptionalVersion(string version)
        {
            SemanticVersion semVer;
            TryParse(version, out semVer);
            return semVer;
        }

        private static Version NormalizeVersionValue(Version version)
        {
            return new Version(version.Major,
                               version.Minor,
                               Math.Max(version.Build, 0),
                               Math.Max(version.Revision, 0));
        }

        /// <inheritdoc/>
        public int CompareTo(object obj)
        {
            if (Object.ReferenceEquals(obj, null)) {
                return 1;
            }
            SemanticVersion other = obj as SemanticVersion;
            if (other == null) {
                throw new ArgumentException("TypeMustBeASemanticVersion", "obj");
            }
            return CompareTo(other);
        }

        /// <inheritdoc/>
        public int CompareTo(SemanticVersion other)
        {
            if (Object.ReferenceEquals(other, null)) {
                return 1;
            }

            int result = Version.CompareTo(other.Version);

            if (result != 0) {
                return result;
            }

            bool empty = String.IsNullOrEmpty(SpecialVersion);
            bool otherEmpty = String.IsNullOrEmpty(other.SpecialVersion);
            if (empty && otherEmpty) {
                return 0;
            } else if (empty) {
                return 1;
            } else if (otherEmpty) {
                return -1;
            }

            // If both versions have a prerelease section with the same prefix
            // and end with digits, compare based on the digits' numeric order
            var match = _preReleaseVersionRegex.Match(SpecialVersion.Trim());
            var otherMatch = _preReleaseVersionRegex.Match(other.SpecialVersion.Trim());
            if (match.Success && otherMatch.Success &&
                string.Equals(
                    match.Groups["PreReleaseString"].Value,
                    otherMatch.Groups["PreReleaseString"].Value,
                    StringComparison.OrdinalIgnoreCase)) {
                int delta =
                    int.Parse(match.Groups["PreReleaseNumber"].Value) -
                    int.Parse(otherMatch.Groups["PreReleaseNumber"].Value);

                return delta != 0 ? delta / Math.Abs(delta) : 0;
            }

            return StringComparer.OrdinalIgnoreCase.Compare(SpecialVersion, other.SpecialVersion);
        }

        /// <inheritdoc/>
        public static bool operator ==(SemanticVersion version1, SemanticVersion version2)
        {
            if (Object.ReferenceEquals(version1, null)) {
                return Object.ReferenceEquals(version2, null);
            }
            return version1.Equals(version2);
        }

        /// <inheritdoc/>
        public static bool operator !=(SemanticVersion version1, SemanticVersion version2)
        {
            return !(version1 == version2);
        }

        /// <inheritdoc/>
        public static bool operator <(SemanticVersion version1, SemanticVersion version2)
        {
            if (version1 == null) {
                throw new ArgumentNullException("version1");
            }
            return version1.CompareTo(version2) < 0;
        }

        /// <inheritdoc/>
        public static bool operator <=(SemanticVersion version1, SemanticVersion version2)
        {
            return (version1 == version2) || (version1 < version2);
        }

        /// <inheritdoc/>
        public static bool operator >(SemanticVersion version1, SemanticVersion version2)
        {
            if (version1 == null) {
                throw new ArgumentNullException("version1");
            }
            return version2 < version1;
        }

        /// <inheritdoc/>
        public static bool operator >=(SemanticVersion version1, SemanticVersion version2)
        {
            return (version1 == version2) || (version1 > version2);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return _originalString;
        }

        /// <inheritdoc/>
        public bool Equals(SemanticVersion other)
        {
            return !Object.ReferenceEquals(null, other) &&
                   Version.Equals(other.Version) &&
                   SpecialVersion.Equals(other.SpecialVersion, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            SemanticVersion semVer = obj as SemanticVersion;
            return !Object.ReferenceEquals(null, semVer) && Equals(semVer);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int hashCode = Version.GetHashCode();
            if (SpecialVersion != null) {
                hashCode = hashCode * 4567 + SpecialVersion.GetHashCode();
            }

            return hashCode;
        }
    }
}
