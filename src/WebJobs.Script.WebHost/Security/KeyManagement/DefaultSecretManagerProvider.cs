﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Script.WebHost
{
    public sealed class DefaultSecretManagerProvider : ISecretManagerProvider
    {
        private const string FileStorage = "Files";
        private readonly ILogger _logger;
        private readonly IOptionsMonitor<ScriptApplicationHostOptions> _options;
        private readonly IHostIdProvider _hostIdProvider;
        private readonly IConfiguration _configuration;
        private readonly IEnvironment _environment;
        private Lazy<ISecretManager> _secretManagerLazy;

        public DefaultSecretManagerProvider(IOptionsMonitor<ScriptApplicationHostOptions> options, IHostIdProvider hostIdProvider,
            IConfiguration configuration, IEnvironment environment, ILoggerFactory loggerFactory)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _options = options ?? throw new ArgumentNullException(nameof(options));
            _hostIdProvider = hostIdProvider ?? throw new ArgumentNullException(nameof(hostIdProvider));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));

            _logger = loggerFactory.CreateLogger(ScriptConstants.LogCategoryHostGeneral);
            _secretManagerLazy = new Lazy<ISecretManager>(Create);

            // When these options change (due to specialization), we need to reset the secret manager.
            options.OnChange(_ => ResetSecretManager());
        }

        public ISecretManager Current => _secretManagerLazy.Value;

        private void ResetSecretManager() => Interlocked.Exchange(ref _secretManagerLazy, new Lazy<ISecretManager>(Create));

        private ISecretManager Create() => new SecretManager(CreateSecretsRepository(), _logger);

        internal ISecretsRepository CreateSecretsRepository()
        {
            string secretStorageType = Environment.GetEnvironmentVariable(EnvironmentSettingNames.AzureWebJobsSecretStorageType);
            string storageString = _configuration.GetWebJobsConnectionString(ConnectionStringNames.Storage);
            if (secretStorageType != null && secretStorageType.Equals(FileStorage, StringComparison.OrdinalIgnoreCase))
            {
                return new FileSystemSecretsRepository(_options.CurrentValue.SecretsPath);
            }
            else if (storageString == null)
            {
                throw new InvalidOperationException($"Secret initialization from Blob storage failed due to a missing Azure Storage connection string. If you intend to use files for secrets, add an App Setting key '{EnvironmentSettingNames.AzureWebJobsSecretStorageType}' with value '{FileStorage}'.");
            }
            else
            {
                string siteSlotName = _environment.GetAzureWebsiteUniqueSlotName() ?? _hostIdProvider.GetHostIdAsync(CancellationToken.None).GetAwaiter().GetResult();

                return new BlobStorageSecretsMigrationRepository(Path.Combine(_options.CurrentValue.SecretsPath, "Sentinels"), storageString, siteSlotName, _logger);
            }
        }
    }
}