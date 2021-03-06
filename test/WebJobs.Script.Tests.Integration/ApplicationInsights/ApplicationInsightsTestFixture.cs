﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.WebJobs.Script.Tests;

namespace Microsoft.Azure.WebJobs.Script.Tests.ApplicationInsights
{
    public abstract class ApplicationInsightsTestFixture : IDisposable
    {
        public const string ApplicationInsightsKey = "some_key";

        public ApplicationInsightsTestFixture(string scriptRoot, string testId)
        {
            WebHostOptions = new ScriptApplicationHostOptions
            {
                IsSelfHost = true,
                ScriptPath = Path.Combine(Environment.CurrentDirectory, scriptRoot),
                LogPath = Path.Combine(Path.GetTempPath(), @"Functions"),
                SecretsPath = Environment.CurrentDirectory // not used
            };

            TestHost = new TestFunctionHost(Path.Combine(Environment.CurrentDirectory, scriptRoot),
                jobHostBuilder =>
                {
                    jobHostBuilder.Services.AddSingleton<ITelemetryChannel>(_ => Channel);

                    jobHostBuilder.Services.Configure<ScriptJobHostOptions>(o =>
                    {
                        o.Functions = new[]
                        {
                            "Scenarios",
                            "HttpTrigger-Scenarios"
                        };
                    });
                },
                configurationBuilder =>
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        [EnvironmentSettingNames.AppInsightsInstrumentationKey] = ApplicationInsightsKey
                    });
                });

            HttpClient = TestHost.HttpClient;

            TestHelpers.WaitForWebHost(HttpClient);
        }

        public TestTelemetryChannel Channel { get; private set; } = new TestTelemetryChannel();

        public TestFunctionHost TestHost { get; }

        public ScriptApplicationHostOptions WebHostOptions { get; private set; }

        public HttpClient HttpClient { get; private set; }

        public void Dispose()
        {
            TestHost?.Dispose();
            HttpClient?.Dispose();
        }

        private class ScriptHostBuilder : IConfigureBuilder<IWebJobsBuilder>
        {
            public void Configure(IWebJobsBuilder builder)
            {
                builder.Services.Configure<ScriptJobHostOptions>(o =>
                {
                    o.Functions = new[] { "Scenarios", "HttpTrigger-Scenarios" };
                });
            }
        }
    }
}