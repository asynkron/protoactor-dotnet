// -----------------------------------------------------------------------
// <copyright file="AwsHttpClient.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.IO;
using System.Net;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Proto.Cluster.AmazonECS
{
    public class AwsEcsContainerMetadataHttpClient
    {
        private readonly ILogger _logger = Log.CreateLogger<AwsEcsContainerMetadataHttpClient>();

        public ContainerMetadata GetContainerMetadata()
        {
            try
            {
                var str = Environment.GetEnvironmentVariable("ECS_CONTAINER_METADATA_URI_V4");
                if (Uri.TryCreate(str, UriKind.Absolute, out var containerMetadataUri))
                {
                    var json = GetResponseString(containerMetadataUri);
                    _logger.LogInformation("[AwsEcsContainerMetadataHttpClient] got metadata for container {Metadata}", json);
                    return JsonConvert.DeserializeObject<ContainerMetadata>(json);
                }

                _logger.LogError("[AwsEcsContainerMetadataHttpClient] failed to get Metadata");

            }
            catch(Exception x)
            {
                _logger.LogError(x, "[AwsEcsContainerMetadataHttpClient] failed to get Metadata");
            }

            return null;
        }
        
        public TaskMetadata GetTaskMetadata()
        {
            try
            {
                var str = Environment.GetEnvironmentVariable("ECS_CONTAINER_METADATA_URI_V4") + "/taskWithTags";
                if (Uri.TryCreate(str, UriKind.Absolute, out var containerMetadataUri))
                {
                    var json = GetResponseString(containerMetadataUri);
                    _logger.LogInformation("[AwsEcsContainerMetadataHttpClient] got metadata for task {Metadata}", json);
                    return JsonConvert.DeserializeObject<TaskMetadata>(json);
                }

                _logger.LogError("[AwsEcsContainerMetadataHttpClient] failed to get Metadata");

            }
            catch(Exception x)
            {
                _logger.LogError(x, "[AwsEcsContainerMetadataHttpClient] failed to get Metadata");
            }

            return null;
        }
        
        
        //
        // public string GetHostPrivateIPv4Address() => GetResponseString(new Uri("http://169.254.169.254/latest/meta-data/local-ipv4"));
        //
        // public string GetHostPublicIPv4Address() => GetResponseString(new Uri("http://169.254.169.254/latest/meta-data/public-ipv4"));

        private string GetResponseString(Uri requestUri)
        {
            try
            {
                var request = WebRequest.Create(requestUri);

                using var response = (HttpWebResponse)request.GetResponse();

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logger.LogError("Failed to execute HTTP request. Request URI: {RequestUri}, Status code: {StatusCode}", requestUri, response.StatusCode);

                    return default;
                }

                using var stream = response.GetResponseStream();
                using var reader = new StreamReader(stream!);

                return reader.ReadToEnd();
            }
            catch (WebException ex) when (ex.Status == WebExceptionStatus.UnknownError)
            {
                _logger.LogError(ex, "Network is unreachable");
                // Network is unreachable
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get AWS metadata response");
            }

            return default;
        }
    }
}