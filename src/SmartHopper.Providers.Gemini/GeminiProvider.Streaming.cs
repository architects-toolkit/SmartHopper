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
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Streaming;

namespace SmartHopper.Providers.Gemini
{
    public sealed partial class GeminiProvider
    {
        /// <inheritdoc/>
        protected override IStreamingAdapter CreateStreamingAdapter()
        {
            return new GeminiStreamingAdapter(this);
        }

        private sealed class GeminiStreamingAdapter : IStreamingAdapter
        {
            private readonly GeminiProvider provider;

            public GeminiStreamingAdapter(GeminiProvider provider)
            {
                this.provider = provider;
            }

            public async IAsyncEnumerable<AIReturn> StreamAsync(
                AIRequestCall request,
                StreamingOptions options,
                CancellationToken cancellationToken = default)
            {
                string endpoint = request.Endpoint;
                string httpMethod = request.HttpMethod;
                string requestBody = request.EncodedRequestBody;
                string contentType = request.ContentType;
                string authentication = request.Authentication;

                if (string.IsNullOrWhiteSpace(endpoint))
                {
                    var error = new AIReturn();
                    error.CreateProviderError("Endpoint cannot be null or empty", request);
                    yield return error;
                    yield break;
                }

                Uri fullUri = this.provider.BuildFullUrl(endpoint);

                Debug.WriteLine($"[{this.provider.Name}] Stream - Method: {httpMethod.ToUpper()}, URL: {fullUri}");

                using (var httpClient = new HttpClient())
                {
                    try
                    {
                        int seconds = request?.TimeoutSeconds > 0 ? request.TimeoutSeconds.Value : TimeoutDefaults.DefaultTimeoutSeconds;
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
                            authError.CreateProviderError($"{this.provider.Name} API key is not configured or is invalid.", request);
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
                            authError.CreateProviderError($"{this.provider.Name} API key is not configured or is invalid.", request);
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
                        authError.CreateProviderError($"Authentication method '{authentication}' is not supported. Supported: 'none', 'bearer', 'x-api-key', 'x-goog-api-key'.", request);
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
                        bodyError.CreateProviderError($"Failed to create request body: {ex.Message}", request);
                    }

                    if (bodyError != null)
                    {
                        yield return bodyError;
                        yield break;
                    }

                    AIReturn sendError = null;
                    AIReturn responseError = null;
                    HttpResponseMessage response = null;
                    try
                    {
                        using (var httpRequest = new HttpRequestMessage(new HttpMethod(httpMethod), fullUri))
                        {
                            httpRequest.Content = content;
                            response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        sendError = new AIReturn();
                        sendError.CreateNetworkError(ex.InnerException?.Message ?? ex.Message, request);
                    }

                    if (responseError != null)
                    {
                        yield return responseError;
                        yield break;
                    }

                    if (sendError != null)
                    {
                        yield return sendError;
                        yield break;
                    }

                    using (response)
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            var errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            var (message, isNetworkLike) = AIProvider.ClassifyHttpError((int)response.StatusCode, response.ReasonPhrase, errorBody, this.provider.Name);
                            responseError = new AIReturn();
                            if (isNetworkLike)
                            {
                                responseError.CreateNetworkError(message, request);
                            }
                            else
                            {
                                responseError.CreateProviderError(message, request);
                            }
                        }

                        if (responseError != null)
                        {
                            yield return responseError;
                            yield break;
                        }

                        using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                        using (var reader = new StreamReader(stream))
                        {
                            string line;
                            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                            {
                                if (string.IsNullOrWhiteSpace(line))
                                {
                                    continue;
                                }

                                AIReturn chunkReturn = null;
                                try
                                {
                                    var chunk = JObject.Parse(line);
                                    var decoded = this.provider.Decode(chunk);
                                    if (decoded != null && decoded.Count > 0)
                                    {
                                        chunkReturn = new AIReturn
                                        {
                                            Request = request,
                                            Status = AICallStatus.Streaming,
                                        };
                                        chunkReturn.CreateSuccess(decoded, request);
                                        chunkReturn.Status = AICallStatus.Streaming;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Error parsing stream chunk: {ex.Message}");
                                }

                                if (chunkReturn != null)
                                {
                                    yield return chunkReturn;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
