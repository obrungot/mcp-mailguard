// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Olaf Brungot

using McpMailGuard.Abstractions;
using Microsoft.Extensions.Configuration;

namespace McpMailGuard.Accounts;

/// <summary>
/// A configured account: its name, provider type, and the mail/calendar sources it offers.
/// </summary>
public sealed record MailAccount(
    string Name,
    string ProviderType,
    IEmailProvider? Email,
    ICalendarProvider? Calendar);

/// <summary>
/// Creates accounts for one provider type (e.g. "Microsoft", later "Google") and performs that
/// provider's interactive sign-in. Register implementations in DI; <see cref="AccountRegistry"/>
/// picks the factory whose <see cref="ProviderType"/> matches the account's <c>Provider</c> setting.
/// </summary>
public interface IAccountProviderFactory
{
    /// <summary>
    /// The value of <c>Accounts:&lt;name&gt;:Provider</c> this factory handles (case-insensitive).
    /// </summary>
    string ProviderType { get; }

    /// <summary>
    /// Builds the account's providers from its configuration section. Must not contact the network
    /// or prompt: it runs at startup, and sign-in state is read lazily on the first request.
    /// </summary>
    MailAccount Create(string accountName, IConfigurationSection section, AccountStore store, IServiceProvider services);

    /// <summary>
    /// Runs the provider's interactive sign-in from the command line and saves the result in the
    /// account's folder, so the server can later use the account without prompting.
    /// </summary>
    Task LoginAsync(
        string accountName,
        IConfigurationSection section,
        AccountStore store,
        TextWriter output,
        CancellationToken cancellationToken);
}
