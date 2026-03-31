/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Providers.Google
{
    public partial class GoogleProvider
    {
        /// <inheritdoc/>
        protected override IStreamingAdapter CreateStreamingAdapter(AIRequestCall request)
        {
            return new GoogleStreamingAdapter(this, request);
        }

        private sealed class GoogleStreamingAdapter : IStreamingAdapter
        {
            private readonly GoogleProvider provider;
            private readonly AIRequestCall request;

            public GoogleStreamingAdapter(GoogleProvider provider, AIRequestCall request)
            {
                this.provider = provider;
                this.request = request;
            }

            public async IAsyncEnumerable<AIReturn> StreamAsync()
            {
                string endpoint = this.request.Endpoint;
                string httpMethod = this.request.HttpMethod;
                string requestBody = this.request.EncodedRequestBody;
                string contentType = this.request.ContentType;
                string authentication = this.request.Authentication;

                if (string.IsNullOrWhiteSpace(endpoint))
                {
                    var error = new AIReturn();
                    error.AddError("Endpoint cannot be null or empty");
                    yield return error;
                    yield break;
                }

                Uri fullUri = this.provider.BuildFullUrl(endpoint);

                Debug.WriteLine($"[{this.provider.Name}] Stream - Method: {httpMethod.ToUpper()}, URL: {fullUri}");

                using (var httpClient = new HttpClient())
                {
                    try
                    {
                        int seconds = this.request?.TimeoutSeconds > 0 ? this.request.TimeoutSeconds : 120;
                        httpClient.Timeout = TimeSpan.FromSeconds(seconds);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[{this.provider.Name}] Warning: could not set HttpClient timeout: {ex.Message}");
                    }

                    var auth = authentication?.Trim().ToLowerInvariant();
                    var apiKey = this.provider.GetSetting<string>("ApiKey");

                    AIReturn authError = null;
                    if (string.IsNullOrWhiteSpace(auth) || auth == "none")
                    {
                    }
                    else if (auth == "bearer")
                    {
                        if (string.IsNullOrWhiteSpace(apiKey))
                        {
                            authError = new AIReturn();
                            authError.AddError($"{this.provider.Name} API key is not configured or is invalid.");
                        }
                        else
                        {
                            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                        }
                    }
                    else if (auth == "x-goog-api-key")
                    {
                        if (string.IsNullOrWhiteSpace(apiKey))
                        {
                            authError = new AIReturn();
                            authError.AddError($"{this.provider.Name} API key is not configured or is invalid.");
                        }
                        else
                        {
                            httpClient.DefaultRequestHeaders.Remove("x-goog-api-key");
                            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-goog-api-key", apiKey);
                        }
                    }
                    else if (auth != "x-api-key")
                    {
                        authError = new AIReturn();
                        authError.AddError($"Authentication method '{authentication}' is not supported. Supported: 'none', 'bearer', 'x-api-key', 'x-goog-api-key'.");
                    }

                    if (authError != null)
                    {
                        yield return authError;
                        yield break;
                    }

                    httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    AIReturn bodyError = null;
                    HttpContent content = null;
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(requestBody))
                        {
                            content = new StringContent(requestBody, Encoding.UTF8, contentType ?? "application/json");
                        }
                    }
                    catch (Exception ex)
                    {
                        bodyError = new AIReturn();
                        bodyError.AddError($"Failed to create request body: {ex.Message}");
                    }

                    if (bodyError != null)
                    {
                        yield return bodyError;
                        yield break;
                    }

                    AIReturn sendError = null;
                    try
                    {
                        using (var request = new HttpRequestMessage(new HttpMethod(httpMethod), fullUri))
                        {
                            request.Content = content;
                            using (var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                            {
                                if (!response.IsSuccessStatusCode)
                                {
                                    var errorBody = await response.Content.ReadAsStringAsync();
                                    sendError = new AIReturn();
                                    sendError.AddError($"HTTP {(int)response.StatusCode}: {errorBody}");
                                    yield return sendError;
                                    yield break;
                                }

                                using (var stream = await response.Content.ReadAsStreamAsync())
                                using (var reader = new StreamReader(stream))
                                {
                                    string line;
                                    while ((line = await reader.ReadLineAsync()) != null)
                                    {
                                        if (string.IsNullOrWhiteSpace(line))
                                        {
                                            continue;
                                        }

                                        try
                                        {
                                            var chunk = JObject.Parse(line);
                                            var decoded = this.provider.Decode(chunk);
                                            if (decoded != null)
                                            {
                                                yield return decoded;
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine($"Error parsing stream chunk: {ex.Message}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        sendError = new AIReturn();
                        sendError.AddError($"Stream request failed: {ex.Message}");
                        yield return sendError;
                    }
                }
            }
        }
    }
}
