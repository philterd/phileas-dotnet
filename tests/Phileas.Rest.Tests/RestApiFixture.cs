/*
 * Copyright 2026 Philterd, LLC
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.MongoDb;
using Xunit;

namespace Phileas.Rest.Tests;

/// <summary>
///     Boots real MongoDB and Valkey containers and hosts the Phileas.Rest app in-process against them,
///     exposing an <see cref="HttpClient" /> for end-to-end tests. When Docker is not available the fixture
///     starts nothing and reports <see cref="DockerAvailable" /> = <see langword="false" />, so the tests skip
///     rather than fail (keeping <c>dotnet test</c> green on machines/CI without Docker).
/// </summary>
public sealed class RestApiFixture : IAsyncLifetime
{
    private MongoDbContainer? _mongo;
    private IContainer? _valkey;
    private WebApplicationFactory<Program>? _factory;
    private Dictionary<string, string?>? _settings;

    public bool DockerAvailable { get; private set; }

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        try
        {
            // Build and start inside the try: resolving the Docker endpoint (which can fail when Docker is
            // absent) happens during Build()/StartAsync, so both must be guarded to let the tests skip.
            _mongo = new MongoDbBuilder().WithImage("mongo:7").Build();
            _valkey = new ContainerBuilder()
                .WithImage("valkey/valkey:8")
                .WithPortBinding(6379, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6379))
                .Build();

            await _mongo.StartAsync();
            await _valkey.StartAsync();
        }
        catch (Exception)
        {
            // Docker isn't available (or the daemon isn't reachable) — leave DockerAvailable false so tests skip.
            DockerAvailable = false;
            return;
        }

        var valkeyEndpoint = $"{_valkey.Hostname}:{_valkey.GetMappedPublicPort(6379)}";

        // Program.cs binds PhileasRestOptions from configuration *before* the host is built and the DI
        // registrations close over that bound instance, so config added via ConfigureAppConfiguration
        // would be applied too late to be seen (the app would fall back to the localhost defaults and
        // fail to reach the containers). Environment variables are read by WebApplication.CreateBuilder
        // up front, so they reach that binding. The "Phileas__" prefix maps each value onto the section.
        _settings = new Dictionary<string, string?>
        {
            ["Phileas__MongoConnectionString"] = _mongo.GetConnectionString(),
            ["Phileas__MongoDatabase"] = "phileas_test",
            ["Phileas__ValkeyConnectionString"] = valkeyEndpoint,
            ["Phileas__ContextCacheTtlSeconds"] = "3600",
            // No local GLiNER model in tests; the policies used here don't need PhEye.
            ["Phileas__PhEyeModelPath"] = ""
        };
        foreach (var (key, value) in _settings)
            Environment.SetEnvironmentVariable(key, value);

        _factory = new WebApplicationFactory<Program>();

        // Building the client starts the host (creating Mongo indexes, connecting to Valkey).
        Client = _factory.CreateClient();
        DockerAvailable = true;
    }

    public async Task DisposeAsync()
    {
        // Clear the process-wide environment variables set for the host so they don't leak to other tests.
        if (_settings != null)
            foreach (var key in _settings.Keys)
                Environment.SetEnvironmentVariable(key, null);

        if (_factory != null)
            await _factory.DisposeAsync();
        if (_valkey != null)
            await _valkey.DisposeAsync();
        if (_mongo != null)
            await _mongo.DisposeAsync();
    }
}

/// <summary>Shares one <see cref="RestApiFixture" /> (one set of containers) across the whole test class group.</summary>
[CollectionDefinition(Name)]
public sealed class RestApiCollection : ICollectionFixture<RestApiFixture>
{
    public const string Name = "rest-api";
}
