﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ACMESharp;
using ACMESharp.ACME;
using System.Configuration;
using System.IO;
using LetsEncrypt.Azure.Core.Models;
using System.Diagnostics;

namespace LetsEncrypt.Azure.Core.Services
{
    public class KuduFileSystemAuthorizationChallengeProvider : BaseHttpAuthorizationChallengeProvider
    {
        
        private readonly IAuthorizationChallengeProviderConfig config;
        private readonly IAzureWebAppEnvironment azureEnvironment;

        public KuduFileSystemAuthorizationChallengeProvider(IAzureWebAppEnvironment azureEnvironment, IAuthorizationChallengeProviderConfig config)
        {
            this.config = config;
            
            this.azureEnvironment = azureEnvironment;
        }

        public override Task CleanupChallengeFile(HttpChallenge challenge)
        {
            return Task.CompletedTask;
        }

        public override async Task EnsureWebConfig()
        {
            if (config.DisableWebConfigUpdate)
            {
                Trace.TraceInformation($"Disabled updating web.config at {WebRootPath() }");
                return;
            }
            await WriteFile(WebRootPath() + "/.well-known/acme-challenge/web.config", webConfig);
        }

        public override async Task PersistsChallengeFile(HttpChallenge challenge)
        {
            var answerPath = GetAnswerPath(challenge);
            await WriteFile(answerPath, challenge.FileContent);
        }

        private async Task WriteFile(string answerPath, string content)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                var streamwriter = new StreamWriter(ms);
                streamwriter.Write(content);
                streamwriter.Flush();
                await (await GetKuduRestClient()).PutFile(answerPath, ms);          
            }
        }

        private string GetAnswerPath(HttpChallenge httpChallenge)
        {
            // We need to strip off any leading '/' in the path
            var filePath = httpChallenge.FilePath;
            if (filePath.StartsWith("/", StringComparison.OrdinalIgnoreCase))
                filePath = filePath.Substring(1);
            var answerPath = WebRootPath() + "/" +filePath;
            return answerPath;
        }

        private string WebRootPath()
        {
            
            if (string.IsNullOrEmpty(azureEnvironment.WebRootPath))
                return "site/wwwroot";
            //Ensure this is a backwards compatible with the LocalFileSystemProvider that was the only option before
            return azureEnvironment.WebRootPath.Replace(Environment.ExpandEnvironmentVariables("%HOME%"), "").Replace('\\', '/');
        }
        private async Task<KuduRestClient> GetKuduRestClient()
        {
            var website = await ArmHelper.GetWebSiteManagementClient(azureEnvironment);
            return KuduHelper.GetKuduClient(website, azureEnvironment);
        }
    }
}
