using System;

namespace SuperPutty.Utils
{
    /// <summary>
    /// Where Help → Check for Updates looks. The answer has the format of GitHub's "latest release" API
    /// ({"tag_name": "v1.5.0", "html_url": "https://…"}), so a GitHub/Gitea release page or a static JSON file works.
    /// </summary>
    public static class UpdateCheck
    {
        /// <summary>Release information of this edition (placeholder until the real location exists)</summary>
        public const string ReleasesUrl = "https://updates.example.com/superputty/releases/latest";

        /// <summary>True if the release tag (e.g. "v1.5.1" or "1.5.1") is newer than the running version</summary>
        public static bool IsNewer(string tag, string currentVersion)
        {
            Version latest, current;
            if (!TryParse(tag, out latest) || !TryParse(currentVersion, out current))
            {
                return false;
            }
            return Normalize(latest).CompareTo(Normalize(current)) > 0;
        }

        /// <summary>A version from a release tag: leading "v" and suffixes like "-beta" are ignored</summary>
        public static bool TryParse(string tag, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }
            string text = tag.Trim().TrimStart('v', 'V');
            int suffix = text.IndexOfAny(new[] { '-', '+', ' ' });
            if (suffix > 0)
            {
                text = text.Substring(0, suffix);
            }
            return Version.TryParse(text, out version);
        }

        /// <summary>Missing parts count as 0, so "1.5" equals "1.5.0.0"</summary>
        private static Version Normalize(Version v)
        {
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));
        }
    }
}
