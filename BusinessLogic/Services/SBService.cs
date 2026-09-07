using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using BusinessLogic.Interfaces;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Services
{
    public class SBService : ISBService
    {
        private readonly string _url = "";
        private readonly ISessionHelper _sessionHelper;

        public SBService(IConfiguration configuration, ISessionHelper sessionHelper)
        {
            _url = configuration["CustomSettings:ServiceUrl"].ToString();
            _sessionHelper = sessionHelper;
        }

        public async Task<string> PostWithoutTokenAsync(string service, object? body)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_url}{service}");
            request.Headers.Add("Accept-Encoding", "gzip, deflate, br");

            var content = new StringContent(JsonConvert.SerializeObject(body), null, "application/json");
            request.Content = content;
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var encoding = response.Content.Headers.ContentEncoding.FirstOrDefault();
            string responseContent;

            using (var responseStream = await response.Content.ReadAsStreamAsync())
            {
                if (encoding == "gzip")
                {
                    using (var decompressedStream = new GZipStream(responseStream, CompressionMode.Decompress))
                    using (var reader = new StreamReader(decompressedStream))
                    {
                        responseContent = await reader.ReadToEndAsync();
                    }
                }
                else if (encoding == "br")
                {
                    using (var decompressedStream = new BrotliStream(responseStream, CompressionMode.Decompress))
                    using (var reader = new StreamReader(decompressedStream))
                    {
                        responseContent = await reader.ReadToEndAsync();
                    }
                }
                else if (encoding == "deflate")
                {
                    using (var decompressedStream = new DeflateStream(responseStream, CompressionMode.Decompress))
                    using (var reader = new StreamReader(decompressedStream))
                    {
                        responseContent = await reader.ReadToEndAsync();
                    }
                }
                else
                {
                    // If the response is not compressed
                    responseContent = await response.Content.ReadAsStringAsync();
                }
            }

            return responseContent;
        }

        public async Task<string> PostAsync(string service, object? body)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_url}{service}");
            request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
            string bearer = "Bearer " + _sessionHelper.Token;
            request.Headers.Add("Authorization", bearer);

            var content = new StringContent(JsonConvert.SerializeObject(body), null, "application/json");
            request.Content = content;
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var encoding = response.Content.Headers.ContentEncoding.FirstOrDefault();
            string responseContent;

            using (var responseStream = await response.Content.ReadAsStreamAsync())
            {
                if (encoding == "gzip")
                {
                    using (var decompressedStream = new GZipStream(responseStream, CompressionMode.Decompress))
                    using (var reader = new StreamReader(decompressedStream))
                    {
                        responseContent = await reader.ReadToEndAsync();
                    }
                }
                else if (encoding == "br")
                {
                    using (var decompressedStream = new BrotliStream(responseStream, CompressionMode.Decompress))
                    using (var reader = new StreamReader(decompressedStream))
                    {
                        responseContent = await reader.ReadToEndAsync();
                    }
                }
                else if (encoding == "deflate")
                {
                    using (var decompressedStream = new DeflateStream(responseStream, CompressionMode.Decompress))
                    using (var reader = new StreamReader(decompressedStream))
                    {
                        responseContent = await reader.ReadToEndAsync();
                    }
                }
                else
                {
                    // If the response is not compressed
                    responseContent = await response.Content.ReadAsStringAsync();
                }
            }

            return responseContent;
        }
    }
}
