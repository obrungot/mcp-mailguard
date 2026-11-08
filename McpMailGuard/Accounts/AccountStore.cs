// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using System.Text.RegularExpressions;

namespace McpMailGuard.Accounts;

/// <summary>
/// Owns the on-disk folder layout for per-account sign-in state:
/// <c>&lt;DataDirectory&gt;/accounts/&lt;name&gt;/</c>, readable only by the server's user.
/// </summary>
public sealed partial class AccountStore
{
    public AccountStore(string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
            throw new ArgumentException("A data directory is required.", nameof(dataDirectory));

        DataDirectory = Path.GetFullPath(dataDirectory);
    }

    public string DataDirectory { get; }

    /// <summary>
    /// Returns the account's folder, creating it (owner-only on Linux/macOS) if needed.
    /// </summary>
    public string GetAccountDirectory(string accountName)
    {
        ValidateName(accountName);
        var path = Path.Combine(DataDirectory, "accounts", accountName.ToLowerInvariant());
        CreatePrivateDirectory(Path.Combine(DataDirectory, "accounts"));
        CreatePrivateDirectory(path);
        return path;
    }

    /// <summary>
    /// Restricts a file to its owner (no-op on Windows, where the user profile is already private).
    /// </summary>
    public static void RestrictToOwner(string filePath)
    {
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    /// <summary>
    /// Account names become folder names, so only letters, digits, '-' and '_' are allowed.
    /// </summary>
    public static void ValidateName(string accountName)
    {
        if (string.IsNullOrWhiteSpace(accountName) || !NamePattern().IsMatch(accountName))
            throw new ArgumentException(
                $"Invalid account name '{accountName}'. Use letters, digits, '-' or '_' only.", nameof(accountName));
    }

    private static void CreatePrivateDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
            return;
        }

        Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex NamePattern();
}
