using System.Text.RegularExpressions;

namespace SuperPutty.Utils
{
    /// <summary>Remote directory helpers for uploads: the shell's directory from the window title, pscp paths</summary>
    public static class RemotePath
    {
        /// <summary>
        /// "user@host: /path", "user@host:~/dir" (the default xterm titles of Debian/Ubuntu and Fedora/RHEL bash,
        /// and many zsh setups)
        /// </summary>
        private static readonly Regex UserHostPathTitle = new Regex(@"^\S+@[^\s:]+:\s*(?<path>[~/].*?)\s*$", RegexOptions.Compiled);

        /// <summary>
        /// The shell's current directory if the remote shell puts it into the window title (e.g. "~/app" or
        /// "/var/log"), otherwise null. PuTTY's own title ("host - PuTTY") yields null.
        /// </summary>
        public static string FromWindowTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }
            title = title.Trim();
            Match match = UserHostPathTitle.Match(title);
            if (match.Success)
            {
                return match.Groups["path"].Value;
            }
            // a bare path as title
            if ((title.StartsWith("/") || title == "~" || title.StartsWith("~/")) && title.IndexOf(' ') < 0)
            {
                return title;
            }
            return null;
        }

        /// <summary>
        /// The path as pscp expects it: SFTP doesn't expand "~", but relative paths start in the home directory,
        /// so "~" becomes "." and "~/dir" becomes "dir"
        /// </summary>
        public static string ToPscpPath(string path)
        {
            path = string.IsNullOrWhiteSpace(path) ? "~" : path.Trim();
            if (path == "~" || path == "~/")
            {
                return ".";
            }
            if (path.StartsWith("~/"))
            {
                return path.Substring(2);
            }
            return path;
        }
    }
}
