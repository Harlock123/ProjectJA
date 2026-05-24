// SPDX-License-Identifier: BUSL-1.1
using System.Text.RegularExpressions;

namespace ProjectJA.SharedKernel.Notifications;

/// <summary>Shared parsing helpers for @-mention tokens in comment bodies.
/// Deliberately conservative regex — accented characters, emoji, and trailing
/// punctuation don't make it into the captured token. The same regex is used
/// by the notification fan-out (resolves tokens to users) and the "Mentioned"
/// filter on the My Issues page (checks "does this body mention me").</summary>
public static class MentionTokens
{
    public static readonly Regex Pattern = new(@"@([A-Za-z0-9._-]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Distinct lowercase tokens (the part after the @, exclusive)
    /// found in the body. Empty for a null/blank/no-mention body.</summary>
    public static IEnumerable<string> Extract(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) yield break;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in Pattern.Matches(body))
        {
            var token = m.Groups[1].Value;
            if (seen.Add(token)) yield return token.ToLowerInvariant();
        }
    }

    /// <summary>True iff <paramref name="body"/> contains a properly-formed
    /// mention of <paramref name="prefix"/>. Avoids the substring-false-positive
    /// of a plain <c>Contains("@alice")</c> matching <c>@alice123</c>.</summary>
    public static bool ContainsMention(string? body, string? prefix)
    {
        if (string.IsNullOrWhiteSpace(body) || string.IsNullOrWhiteSpace(prefix))
            return false;
        foreach (Match m in Pattern.Matches(body))
        {
            if (m.Groups[1].Value.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
