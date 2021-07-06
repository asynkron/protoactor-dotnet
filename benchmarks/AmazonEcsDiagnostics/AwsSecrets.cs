// -----------------------------------------------------------------------
// <copyright file="AwsSecrets.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.IO;
using System.Threading.Tasks;
using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Newtonsoft.Json;

namespace EcsDiagnostics
{
    public static class AwsSecretsManager
    {
        public static async Task<AwsSecrets> GetSecret(string secretName, string region = "eu-north-1" )
        {
            var client = new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));

            var response = await client.GetSecretValueAsync(new GetSecretValueRequest
            {
                SecretId = secretName,
            });

            var json = response.SecretString ?? throw new Exception("Unknown secret type");
            var secrets = JsonConvert.DeserializeObject<AwsSecrets>(json);

            return secrets;
        }
    }
    
    public class AwsSecrets
    {
        [JsonProperty("api-key")]
        public string ApiKey { get; set; }

        [JsonProperty("api-secret")]
        public string ApiSecret { get; set; }
    }
}