using System;
using System.Linq;

namespace SuperPutty.Data
{
    /// <summary>The environment a session belongs to</summary>
    public enum EnvironmentKind
    {
        None,
        Dev,
        Qual,
        Prod
    }

    /// <summary>
    /// Finds a session's environment: from its icon ("prod-…", "qual-…", "dev-…", see tools/session-icons), otherwise
    /// from a folder of the session tree named like an environment ("Prod", "Production", "QA", "Staging", "Dev"…)
    /// </summary>
    public static class SessionEnvironment
    {
        public static EnvironmentKind Of(SessionData session)
        {
            return session == null ? EnvironmentKind.None : Of(session.ImageKey, session.ScopeSessionId);
        }

        public static EnvironmentKind Of(string imageKey, string sessionId)
        {
            EnvironmentKind fromIcon = FromName(imageKey == null ? null : imageKey.Split('-')[0], false);
            if (fromIcon != EnvironmentKind.None)
            {
                return fromIcon;
            }
            // the closest folder that names an environment wins
            foreach (string folder in CommandLibrary.FolderChain(sessionId))
            {
                EnvironmentKind fromFolder = FromName(folder.Split('/').Last(), true);
                if (fromFolder != EnvironmentKind.None)
                {
                    return fromFolder;
                }
            }
            return EnvironmentKind.None;
        }

        /// <summary>An environment name: icon prefixes are exact, folder names also accept common spellings</summary>
        private static EnvironmentKind FromName(string name, bool folder)
        {
            switch ((name ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "prod": return EnvironmentKind.Prod;
                case "qual": return EnvironmentKind.Qual;
                case "dev": return EnvironmentKind.Dev;
            }
            if (!folder)
            {
                return EnvironmentKind.None;
            }
            switch ((name ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "production": case "live": case "prd": return EnvironmentKind.Prod;
                case "qa": case "staging": case "stage": case "test": case "int": case "integration": return EnvironmentKind.Qual;
                case "development": case "develop": return EnvironmentKind.Dev;
                default: return EnvironmentKind.None;
            }
        }
    }
}
