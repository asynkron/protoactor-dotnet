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

namespace EcsDiagnostics
{
    public static class AwsSecrets
    {
        public static async Task<string> GetSecret(string secretName, string region = "eu-north-1" )
        {
            var client = new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));

            var response = await client.GetSecretValueAsync(new GetSecretValueRequest
            {
                SecretId = secretName,
            });

            var secret = response.SecretString ?? throw new Exception("Unknown secret type");
            return secret;
        }
    }
}