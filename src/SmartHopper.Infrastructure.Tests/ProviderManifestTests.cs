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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Newtonsoft.Json;
using SmartHopper.Infrastructure.AIProviders;
using Xunit;

namespace SmartHopper.Infrastructure.Tests
{
    /// <summary>Regression tests for data-only, bounded pre-consent provider inspection.</summary>
    public sealed class ProviderManifestTests
    {
        /// <summary>Reads real managed resources without invoking a module initializer or loading the DLL.</summary>
        [Theory]
        [InlineData("{\"schemaVersion\":1,\"displayName\":\"Example\",\"modelCount\":12}", true)]
        [InlineData("{\"schemaVersion\":1,\"displayName\":\"Example\",\"modelCount\":null}", true)]
        [InlineData("{\"schemaVersion\":2,\"displayName\":\"Example\"}", false)]
        [InlineData("{\"schemaVersion\":1,\"displayName\":\"Example\",\"modelCount\":-1}", false)]
        [InlineData("{\"schemaVersion\":1,\"displayName\":\"Example\",\"modelCount\":\"12\"}", false)]
        [InlineData("{\"schemaVersion\":1,\"displayName\":\"Example\",\"displayName\":\"Spoof\"}", false)]
        [InlineData("{\"schemaVersion\":1,\"displayName\":\"Bad\\nName\"}", false)]
        [InlineData("not json", false)]
        [InlineData(null, false)]
        public async Task Read_DoesNotExecuteProviderCode(string? manifest, bool valid)
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var name = "UntrustedProvider" + Guid.NewGuid().ToString("N");
                var dll = Path.Combine(directory, name + ".dll");
                var marker = Path.Combine(directory, "executed.txt");
                var source = "public static class Startup { [System.Runtime.CompilerServices.ModuleInitializer] "
                    + "public static void Initialize() { System.IO.File.WriteAllText("
                    + JsonConvert.SerializeObject(marker) + ", \"executed\"); } }";
                var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                    .Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path));
                var compilation = CSharpCompilation.Create(name, new[] { CSharpSyntaxTree.ParseText(source) },
                    references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
                var resources = manifest == null ? null : new[]
                {
                    new ResourceDescription(ProviderManifest.ResourceName,
                        () => new MemoryStream(Encoding.UTF8.GetBytes(manifest)), true),
                };
                var emitted = compilation.Emit(dll, manifestResources: resources);
                Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));

                var result = ProviderManifest.Read(dll);
                Assert.Equal(valid, result != null);
                if (valid)
                {
                    Assert.Equal("Example", result!.DisplayName);
                }

                // Exercise the real trust-dialog candidate path as well as the
                // PE reader. The synthetic catalogue verifier never uses a network.
                var originalHash = ProviderHashVerifier.CalculateFileHash(dll);
                foreach (var matches in new[] { true, false })
                {
                    var verified = false;
                    var candidates = await ProviderManager.BuildNewProviderCandidatesAsync(
                        new List<string> { dll },
                        candidatePath =>
                        {
                            verified = true;
                            var localHash = ProviderHashVerifier.CalculateFileHash(candidatePath);
                            return Task.FromResult(new ProviderVerificationResult
                            {
                                LocalHash = localHash,
                                PublicHash = matches ? originalHash : new string('0', 64),
                                Status = matches ? ProviderVerificationStatus.Match : ProviderVerificationStatus.Mismatch,
                                Success = matches,
                            });
                        }).ConfigureAwait(false);
                    var candidate = Assert.Single(candidates);
                    Assert.True(verified);
                    Assert.Equal(valid ? "Example" : name, candidate.DisplayName);
                    Assert.Equal(result?.ModelCount, candidate.ModelCount);
                    Assert.Equal(matches ? ProviderVerificationStatus.Match : ProviderVerificationStatus.Mismatch, candidate.Verification!.Status);
                }

                Assert.False(File.Exists(marker));
                Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.GetName().Name == name);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>Oversized resources and non-PE files fail closed to the filename preview.</summary>
        [Fact]
        public void Read_InvalidFile_ReturnsNull()
        {
            var file = Path.GetTempFileName();
            try
            {
                File.WriteAllText(file, new string('x', 20000));
                Assert.Null(ProviderManifest.Read(file));
            }
            finally
            {
                File.Delete(file);
            }
        }
    }
}
