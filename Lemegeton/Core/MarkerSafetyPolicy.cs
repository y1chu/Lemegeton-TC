using System;

namespace Lemegeton.Core
{
    internal static class MarkerSafetyPolicy
    {
        // Keep this gate stable when settings are reloaded or imported.
        internal static readonly object Sync = new object();

        internal static bool IsSharedMarkerCommand(string commandText)
        {
            if (String.IsNullOrWhiteSpace(commandText))
                return false;

            // Timeline commands can contain multiple lines.
            foreach (string line in commandText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string command = line.TrimStart();
                int end = 0;
                while (end < command.Length && !Char.IsWhiteSpace(command[end]))
                    end++;
                string token = command.Substring(0, end);
                if (token.Equals("/mk", StringComparison.OrdinalIgnoreCase)
                    || token.Equals("/mark", StringComparison.OrdinalIgnoreCase)
                    || token.Equals("/marking", StringComparison.OrdinalIgnoreCase)
                    || token.Equals("/標記", StringComparison.Ordinal)
                    || token.Equals("/标记", StringComparison.Ordinal)
                    || token.Equals("/マーキング", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }
}
