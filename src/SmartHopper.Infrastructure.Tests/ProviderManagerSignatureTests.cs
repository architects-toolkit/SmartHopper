/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

namespace SmartHopper.Infrastructure.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Security;
    using System.Security.Cryptography;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using SmartHopper.Infrastructure.AIProviders;
    using Xunit;
    using Xunit.Abstractions;
    using Xunit.Sdk;

    public class ProviderManagerSignatureTests
    {
        private readonly ITestOutputHelper output;

        public ProviderManagerSignatureTests(ITestOutputHelper output) => this.output = output;

#if NET7_WINDOWS
        [Fact(DisplayName = "VerifySignature_UnsignedDll_ThrowsCryptographicException [Windows]")]
#else
        [Fact(DisplayName = "VerifySignature_UnsignedDll_ThrowsCryptographicException [Core]")]
#endif
        public void VerifySignature_UnsignedDll_ThrowsCryptographicException()
        {
            // Arrange: create an unsigned dummy assembly file
            var manager = ProviderManager.Instance;
            var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".dll");
            File.WriteAllText(tempFile, "dummy content");

            try
            {
                // Use reflection to access the private VerifySignature method
                var verifyMethod = typeof(ProviderManager)
                    .GetMethod("VerifySignature", BindingFlags.NonPublic | BindingFlags.Instance);

                // Act & Assert: invoking on unsigned file should throw TargetInvocationException
                var ex = Assert.Throws<TargetInvocationException>(() =>
                    verifyMethod.Invoke(manager, new object[] { tempFile }));

                // Inner exception must be a CryptographicException
                Assert.IsType<CryptographicException>(ex.InnerException);
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "VerifySignature_AuthenticodeSigned_NoStrongName_FailsVerification [Windows]")]
#else
        [Fact(DisplayName = "VerifySignature_AuthenticodeSigned_NoStrongName_FailsVerification [Core]")]
#endif
        /// <summary>
        /// Test that an Authenticode-signed but non-strong-named assembly fails verification.
        /// The verification may fail with either SecurityException or CryptographicException
        /// depending on which validation check runs first.
        /// </summary>
        public void VerifySignature_AuthenticodeSignedNoStrongName_FailsVerification()
        {
            // Skip on macOS where code signing is different
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                this.output.WriteLine("Skipping code signing test on macOS");
                return;
            }

            // Skip if certificate creation is not supported (e.g., in CI environments)
            if (!this.IsCertificateCreationSupported())
            {
                this.output.WriteLine("Skipping certificate creation test - not supported in current environment");
                return;
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new SkipException("Authenticode signature tests require Windows");
            }

            const string password = "testpw";
            var manager = ProviderManager.Instance;

            // Create a temporary DLL via Roslyn
            var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".dll");
            try
            {
                var syntaxTree = CSharpSyntaxTree.ParseText("public class Dummy {};");
                var refs = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
                var compilation = CSharpCompilation.Create(
                    Path.GetFileNameWithoutExtension(tempFile),
                    new[] { syntaxTree },
                    refs,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
                Assert.True(compilation.Emit(tempFile).Success, "Failed to compile dummy assembly");

                // Locate Sign-Authenticode script
                var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var solutionDir = Path.GetFullPath(Path.Combine(assemblyDir, "../../../../.."));
                var script = Path.Combine(solutionDir, "tools", "Sign-Authenticode.ps1");

                // Prepare PFX path
                var pfxFile = Path.ChangeExtension(tempFile, ".pfx");

                // Authenticode signing
                EnsureWindowsKitsOnPath();
                this.GeneratePfx(script, pfxFile, password);
                this.SignAuthenticode(tempFile, pfxFile, password);

                // Act & Assert: verify throws an exception for invalid signing
                var verify = typeof(ProviderManager).GetMethod("VerifySignature", BindingFlags.NonPublic | BindingFlags.Instance);
                var ex = Assert.Throws<TargetInvocationException>(() => verify.Invoke(manager, new object[] { tempFile }));

                // The exception could be either SecurityException (strong-name failure) or
                // CryptographicException (general signing validation failure) - both are acceptable
                Assert.True(
                    ex.InnerException is SecurityException || ex.InnerException is CryptographicException,
                    $"Expected SecurityException or CryptographicException, got {ex.InnerException?.GetType().Name}");

                this.output.WriteLine($"Validation correctly failed with: {ex.InnerException?.GetType().Name}");
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }

                var cleanupPfx = Path.ChangeExtension(tempFile, ".pfx");
                if (File.Exists(cleanupPfx))
                {
                    File.Delete(cleanupPfx);
                }
            }
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "VerifySignature_AuthenticodeUnsigned_StrongNameCorrect_ThrowsCryptographicException [Windows]")]
#else
        [Fact(DisplayName = "VerifySignature_AuthenticodeUnsigned_StrongNameCorrect_ThrowsCryptographicException [Core]")]
#endif
        public void VerifySignature_AuthenticodeUnsigned_StrongNameCorrect_ThrowsCryptographicException()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new SkipException("Strong-name tests require Windows");
            }

            var manager = ProviderManager.Instance;

            // Locate scripts relative to test assembly
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var solutionDir = Path.GetFullPath(Path.Combine(assemblyDir, "../../../../.."));
            var snkScriptPath = Path.Combine(solutionDir, "tools", "Sign-StrongNames.ps1");

            // Configure environment for both local VS and GitHub Actions
            var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var pathsToAdd = new List<string>();

            if (!string.IsNullOrEmpty(programFilesX86))
            {
                // 1. Check for .NET Framework SDK (contains sn.exe)
                var netfxPaths = new[]
                {
                    Path.Combine(programFilesX86, "Microsoft SDKs", "Windows", "v10.0A", "bin", "NETFX 4.8 Tools"),
                    Path.Combine(programFilesX86, "Microsoft SDKs", "Windows", "v10.0A", "bin"),
                    Path.Combine(programFilesX86, "Microsoft SDKs", "Windows", "v8.1A", "bin", "NETFX 4.8 Tools"),
                };

                // 2. Check Windows 10/11 SDK locations
                var windowsKitRoot = Path.Combine(programFilesX86, "Windows Kits", "10", "bin");
                if (Directory.Exists(windowsKitRoot))
                {
                    var sdkDirs = Directory.GetDirectories(windowsKitRoot)
                        .OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase);

                    foreach (var sdkDir in sdkDirs)
                    {
                        pathsToAdd.Add(Path.Combine(sdkDir, "x64"));
                        pathsToAdd.Add(Path.Combine(sdkDir, "x86"));
                    }
                }

                // Add .NET Framework SDK paths
                pathsToAdd.AddRange(netfxPaths.Where(Directory.Exists));
            }

            // Add Visual Studio MSBuild paths if running locally
            var vsInstallDir = Environment.GetEnvironmentVariable("VSINSTALLDIR");
            if (!string.IsNullOrEmpty(vsInstallDir))
            {
                var msbuildPath = Path.Combine(vsInstallDir, "MSBuild", "Current", "Bin");
                if (Directory.Exists(msbuildPath))
                {
                    pathsToAdd.Add(msbuildPath);
                }
            }

            // Update PATH with found directories
            var newPaths = string.Join(";", pathsToAdd.Distinct()
                .Where(p => !path.Contains(p, StringComparison.OrdinalIgnoreCase)));

            if (!string.IsNullOrEmpty(newPaths))
            {
                Environment.SetEnvironmentVariable("PATH", $"{path};{newPaths}");
                this.output.WriteLine($"Updated PATH with: {newPaths}");
            }

            // Generate temp SNK file for strong-name signing
            var tempDir = Path.GetTempPath();
            var snkPath = Path.Combine(tempDir, "signing.snk");

            // Try to find sn.exe in the updated PATH
            var snExe = FindExecutable("sn.exe");
            if (!string.IsNullOrEmpty(snExe))
            {
                this.output.WriteLine($"Using sn.exe at: {snExe}");
                try
                {
                    var snProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = snExe,
                        Arguments = "-k \"" + snkPath + "\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                    });
                    snProcess.WaitForExit();
                    this.output.WriteLine("SNK generation output: " + snProcess.StandardOutput.ReadToEnd());
                    this.output.WriteLine("SNK generation error: " + snProcess.StandardError.ReadToEnd());

                    if (snProcess.ExitCode == 0 && File.Exists(snkPath))
                    {
                        File.Copy(snkPath, Path.Combine(solutionDir, "signing.snk"), true);
                        this.output.WriteLine("Successfully generated SNK using sn.exe");
                        goto SnkGenerated;
                    }
                }
                catch (Exception exception)
                {
                    this.output.WriteLine($"Error using sn.exe: {exception.Message}");
                }
            }

            // Fall back to PowerShell script if sn.exe failed or wasn't found
            this.output.WriteLine("Falling back to PowerShell script for SNK generation");
            var psi = new ProcessStartInfo("pwsh", $"-NoProfile -ExecutionPolicy Bypass -File \"{snkScriptPath}\" -Generate")
            {
                WorkingDirectory = tempDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            var proc = Process.Start(psi);
            proc.WaitForExit();
            var stdout = proc.StandardOutput.ReadToEnd();
            var stderr = proc.StandardError.ReadToEnd();
            this.output.WriteLine("Generate SNK STDOUT:\n" + stdout);
            this.output.WriteLine("Generate SNK STDERR:\n" + stderr);

            if (proc.ExitCode != 0)
            {
                this.output.WriteLine($"SNK generation failed: {stderr}. Falling back to existing signing.snk.");
            }

        // If we get here, we used the PowerShell script, so mark that we've generated the SNK
        SnkGenerated:

            // Copy SNK to assembly directory to ensure same strong-name for tests
            var sourceSnk = Path.Combine(solutionDir, "signing.snk");
            var targetSnk = Path.Combine(assemblyDir, "signing.snk");
            if (File.Exists(sourceSnk))
            {
                File.Copy(sourceSnk, targetSnk, true);
            }
            else if (File.Exists(snkPath))
            {
                File.Copy(snkPath, targetSnk, true);
            }
            else
            {
                throw new FileNotFoundException("Failed to generate or locate the SNK file");
            }

            // Build strong-named dummy via MSBuild
            var builtDll = this.BuildStrongNamedAssembly(targetSnk);

            // Act & Assert: correct strong-name but no Authenticode signature
            var verifyMethod = typeof(ProviderManager).GetMethod("VerifySignature", BindingFlags.NonPublic | BindingFlags.Instance);
            var ex = Assert.Throws<TargetInvocationException>(() => verifyMethod.Invoke(manager, new object[] { builtDll }));
            Assert.IsType<CryptographicException>(ex.InnerException);
        }

        // #if NET7_WINDOWS
        //         [Fact(DisplayName = "VerifySignature_FullySigned_DoesNotThrow [Windows]")]
        // #else
        //         [Fact(DisplayName = "VerifySignature_FullySigned_DoesNotThrow [Core]")]
        // #endif
        //         public void VerifySignature_FullySigned_DoesNotThrow()
        //         {
        //             if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        //                 throw new SkipException("Authenticode and strong-name tests require Windows");

        // var manager = ProviderManager.Instance;
        //             // Locate scripts relative to test assembly
        //             var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        //             var solutionDir = Path.GetFullPath(Path.Combine(assemblyDir, "../../../../.."));
        //             var authScriptPath = Path.Combine(solutionDir, "tools", "Sign-Authenticode.ps1");
        //             var snkScriptPath = Path.Combine(solutionDir, "tools", "Sign-StrongNames.ps1");
        //
        //             // Ensure Windows SDK tools are on PATH
        //             var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
        //             if (!string.IsNullOrEmpty(programFilesX86))
        //             {
        //                 var sdkRoot = Path.Combine(programFilesX86, "Windows Kits", "10", "bin");
        //                 if (Directory.Exists(sdkRoot))
        //                 {
        //                     var dirs = Directory.GetDirectories(sdkRoot);
        //                     Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
        //                     var latest = dirs.Length > 0 ? dirs[dirs.Length - 1] : null;
        //                     if (latest != null)
        //                     {
        //                         var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        //                         var x64Path = Path.Combine(latest, "x64");
        //                         var x86Path = Path.Combine(latest, "x86");
        //                         var newPath = pathEnv;
        //                         if (Directory.Exists(x64Path)) newPath += ";" + x64Path;
        //                         if (Directory.Exists(x86Path)) newPath += ";" + x86Path;
        //                         Environment.SetEnvironmentVariable("PATH", newPath);
        //                     }
        //                 }
        //             }
        //
        //             // Ensure SNK file exists: use committed or generate via script
        //             var tempDir = Path.GetTempPath();
        //             var sourceSnk = Path.Combine(solutionDir, "signing.snk");
        //             if (!File.Exists(sourceSnk))
        //             {
        //                 var psi = new ProcessStartInfo("pwsh", $"-NoProfile -ExecutionPolicy Bypass -File \"{snkScriptPath}\" -Generate")
        //                 {
        //                     WorkingDirectory = tempDir,
        //                     RedirectStandardOutput = true,
        //                     RedirectStandardError = true,
        //                     UseShellExecute = false
        //                 };
        //                 var proc = Process.Start(psi);
        //                 proc.WaitForExit();
        //                 _output.WriteLine("Generate SNK STDOUT:\n" + proc.StandardOutput.ReadToEnd());
        //                 _output.WriteLine("Generate SNK STDERR:\n" + proc.StandardError.ReadToEnd());
        //                 Assert.Equal(0, proc.ExitCode);
        //             }
        //             // Copy SNK to test assembly folder
        //             var targetSnk = Path.Combine(assemblyDir, "signing.snk");
        //             File.Copy(sourceSnk, targetSnk, true);

        // // Build strong-named dummy via MSBuild
        //             var builtDll = BuildStrongNamedAssembly(targetSnk);

        // // Generate test PFX and sign with Authenticode
        //             var pfxFile = Path.Combine(tempDir, "test-signing.pfx");
        //             var startInfo1 = new ProcessStartInfo("pwsh", $"-NoProfile -ExecutionPolicy Bypass -File \"{authScriptPath}\" -Generate -Password testpw")
        //             {
        //                 WorkingDirectory = solutionDir,
        //                 RedirectStandardOutput = true,
        //                 RedirectStandardError = true,
        //                 UseShellExecute = false
        //             };
        //             // Point PFX output to temp file
        //             startInfo1.Arguments += $" -PfxPath \"{pfxFile}\"";
        //             var proc1 = Process.Start(startInfo1);
        //             proc1.WaitForExit();
        //             var stdout1 = proc1.StandardOutput.ReadToEnd();
        //             var stderr1 = proc1.StandardError.ReadToEnd();
        //             _output.WriteLine("Generate PFX STDOUT:\n" + stdout1);
        //             _output.WriteLine("Generate PFX STDERR:\n" + stderr1);
        //             Assert.Equal(0, proc1.ExitCode);

        // // Sign with Authenticode
        //             SignAuthenticode(builtDll, pfxFile, "testpw");
        //             // Debug: verify signature via signtool
        //             _output.WriteLine("----- signtool verify on Dummy.dll -----");
        //             var sigTool = FindExecutable("signtool.exe");
        //             if (!string.IsNullOrEmpty(sigTool))
        //             {
        //                 var psiV = new ProcessStartInfo(sigTool, $"verify /pa \"{builtDll}\"")
        //                 {
        //                     RedirectStandardOutput = true,
        //                     RedirectStandardError = true,
        //                     UseShellExecute = false
        //                 };
        //                 var procV = Process.Start(psiV);
        //                 procV.WaitForExit();
        //                 _output.WriteLine("Signtool STDOUT:\n" + procV.StandardOutput.ReadToEnd());
        //                 _output.WriteLine("Signtool STDERR:\n" + procV.StandardError.ReadToEnd());
        //                 _output.WriteLine($"ExitCode: {procV.ExitCode}");
        //             }
        //             else
        //             {
        //                 _output.WriteLine("Signtool not found");
        //             }
        //             // Create a copy of the host assembly and sign that instead, since the original is locked
        //             var providerAssemblyPath = typeof(ProviderManager).Assembly.Location;
        //             var providerAssemblyCopy = Path.Combine(Path.GetDirectoryName(builtDll), Path.GetFileName(providerAssemblyPath));
        //             File.Copy(providerAssemblyPath, providerAssemblyCopy, true);
        //             SignAuthenticode(providerAssemblyCopy, pfxFile, "testpw");
        //             // Debug: verify signature via signtool on ProviderManager.dll
        //             _output.WriteLine("----- signtool verify on ProviderManager.dll -----");
        //             if (!string.IsNullOrEmpty(sigTool))
        //             {
        //                 var psiV2 = new ProcessStartInfo(sigTool, $"verify /pa \"{providerAssemblyCopy}\"")
        //                 {
        //                     RedirectStandardOutput = true,
        //                     RedirectStandardError = true,
        //                     UseShellExecute = false
        //                 };
        //                 var procV2 = Process.Start(psiV2);
        //                 procV2.WaitForExit();
        //                 _output.WriteLine("Signtool STDOUT:\n" + procV2.StandardOutput.ReadToEnd());
        //                 _output.WriteLine("Signtool STDERR:\n" + procV2.StandardError.ReadToEnd());
        //                 _output.WriteLine($"ExitCode: {procV2.ExitCode}");
        //             }
        //             else
        //             {
        //                 _output.WriteLine("Signtool not found");
        //             }
        //             try
        //             {
        //                 // Act & Assert: fully signed DLL should not throw using signed copy of provider assembly
        //                 var loadedProviderAssembly = Assembly.LoadFrom(providerAssemblyCopy);
        //                 var loadedManagerType = loadedProviderAssembly.GetType(typeof(ProviderManager).FullName);
        //                 var loadedManagerInstance = loadedManagerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        //                 var verifyMethod = loadedManagerType.GetMethod("VerifySignature", BindingFlags.NonPublic | BindingFlags.Instance);
        //                 var ex2 = Record.Exception(() => verifyMethod.Invoke(loadedManagerInstance, new object[] { builtDll }));
        //                 Assert.Null(ex2);
        //             }
        //             finally
        //             {
        //                 if (File.Exists(builtDll)) File.Delete(builtDll);
        //                 if (File.Exists(pfxFile)) File.Delete(pfxFile);
        //                 if (File.Exists(targetSnk)) File.Delete(targetSnk);
        //                 if (File.Exists(providerAssemblyCopy)) File.Delete(providerAssemblyCopy);
        //             }
        //         }

        // Helper: build a simple project signed with SNK and return path to Dummy.dll
        private string BuildStrongNamedAssembly(string keyFile)
        {
            var projDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(projDir);
            var projFile = Path.Combine(projDir, "Dummy.csproj");
            var csproj = $@"<Project Sdk=""Microsoft.NET.Sdk""><PropertyGroup><TargetFramework>net7.0</TargetFramework><SignAssembly>true</SignAssembly><AssemblyOriginatorKeyFile>{keyFile}</AssemblyOriginatorKeyFile></PropertyGroup></Project>";
            File.WriteAllText(projFile, csproj);
            File.WriteAllText(Path.Combine(projDir, "Dummy.cs"), "public class Dummy { }");

            // Restore project to generate assets
            var psiRestore = new ProcessStartInfo("dotnet", $"restore \"{projFile}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            var procRestore = Process.Start(psiRestore);
            procRestore.WaitForExit();
            this.output.WriteLine("Restore STDOUT:\n" + procRestore.StandardOutput.ReadToEnd());
            this.output.WriteLine("Restore STDERR:\n" + procRestore.StandardError.ReadToEnd());
            Assert.Equal(0, procRestore.ExitCode);

            // Build signed assembly
            var psi = new ProcessStartInfo(
                "dotnet",
                $"build \"{projFile}\" -c Debug -o \"{projDir}\" /p:SignAssembly=true /p:AssemblyOriginatorKeyFile=\"{keyFile}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            var proc = Process.Start(psi);
            proc.WaitForExit();
            this.output.WriteLine("MSBuild STDOUT:\n" + proc.StandardOutput.ReadToEnd());
            this.output.WriteLine("MSBuild STDERR:\n" + proc.StandardError.ReadToEnd());
            Assert.Equal(0, proc.ExitCode);
            return Path.Combine(projDir, "Dummy.dll");
        }

        /// <summary>
        /// Ensures signtool from Windows SDK is on PATH.
        /// </summary>
        private static void EnsureWindowsKitsOnPath()
        {
            var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            if (!string.IsNullOrEmpty(programFilesX86))
            {
                var sdkRoot = Path.Combine(programFilesX86, "Windows Kits", "10", "bin");
                if (Directory.Exists(sdkRoot))
                {
                    var dirs = Directory.GetDirectories(sdkRoot);
                    Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
                    var latest = dirs.Length > 0 ? dirs[dirs.Length - 1] : null;
                    if (latest != null)
                    {
                        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                        var x64Path = Path.Combine(latest, "x64");
                        var x86Path = Path.Combine(latest, "x86");
                        var newPath = path;
                        if (Directory.Exists(x64Path))
                        {
                            newPath += ";" + x64Path;
                        }

                        if (Directory.Exists(x86Path))
                        {
                            newPath += ";" + x86Path;
                        }

                        Environment.SetEnvironmentVariable("PATH", newPath);
                    }
                }
            }
        }

        /// <summary>
        /// Runs a PowerShell script with provided arguments and asserts success.
        /// Uses -Command so that any expressions in <paramref name="args"/> (for example
        /// ConvertTo-SecureString) are evaluated before binding to script parameters.
        /// </summary>
        private void RunPowerShell(string scriptPath, string args)
        {
            var workingDir = Path.GetDirectoryName(scriptPath);
            var escapedScriptPath = scriptPath.Replace("'", "''");
            var command = $"& '{escapedScriptPath}' {args}";

            var psi = new ProcessStartInfo("pwsh", $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"")
            {
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            var proc = Process.Start(psi);
            proc.WaitForExit();
            this.output.WriteLine(proc.StandardOutput.ReadToEnd());
            this.output.WriteLine(proc.StandardError.ReadToEnd());
            Assert.Equal(0, proc.ExitCode);
        }

        /// <summary>
        /// Check if certificate creation is supported in current environment.
        /// </summary>
        private bool IsCertificateCreationSupported()
        {
            try
            {
                // Try to create a temporary certificate to test permissions
                using (var tempCert = new System.Security.Cryptography.X509Certificates.X509Certificate2())
                {
                    // This is a lightweight check - we don't actually create a cert,
                    // just check if we're in an environment that typically supports it
                    var isCI = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ||
                              !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")) ||
                              !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD"));

                    // In CI environments, certificate creation often fails
                    return !isCI;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Generates a PFX file via Sign-Authenticode.ps1.
        /// </summary>
        private void GeneratePfx(string scriptPath, string pfxPath, string password)
        {
            var escapedPassword = password.Replace("'", "''");
            var passwordExpression = $"(ConvertTo-SecureString '{escapedPassword}' -AsPlainText -Force)";
            var escapedPfxPath = pfxPath.Replace("'", "''");

            this.RunPowerShell(scriptPath, $"-Generate -Password {passwordExpression} -PfxPath '{escapedPfxPath}'");
        }

        /// <summary>
        /// Signs a DLL with Authenticode via PowerShell script.
        /// </summary>
        private void SignAuthenticode(string targetFile, string pfxPath, string password)
        {
            // Sign DLL using PowerShell Sign-Authenticode.ps1 script
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var solutionDir = Path.GetFullPath(Path.Combine(assemblyDir, "../../../../.."));
            var scriptPath = Path.Combine(solutionDir, "tools", "Sign-Authenticode.ps1");

            var escapedPassword = password.Replace("'", "''");
            var passwordExpression = $"(ConvertTo-SecureString '{escapedPassword}' -AsPlainText -Force)";
            var escapedTargetFile = targetFile.Replace("'", "''");
            var escapedPfxPath = pfxPath.Replace("'", "''");

            var previous = Environment.GetEnvironmentVariable("SMARTHOPPER_ALLOW_TEST_ASSEMBLY_SIGNING");
            try
            {
                Environment.SetEnvironmentVariable("SMARTHOPPER_ALLOW_TEST_ASSEMBLY_SIGNING", "1");
                this.RunPowerShell(scriptPath, $"-Sign '{escapedTargetFile}' -Password {passwordExpression} -PfxPath '{escapedPfxPath}'");
            }
            finally
            {
                Environment.SetEnvironmentVariable("SMARTHOPPER_ALLOW_TEST_ASSEMBLY_SIGNING", previous);
            }
        }

        private static string? FindExecutable(string exeName)
        {
            EnsureWindowsKitsOnPath();

            var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var paths = path.Split(Path.PathSeparator);

            foreach (var p in paths)
            {
                try
                {
                    var fullPath = Path.Combine(p, exeName);
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
                catch (Exception ex)
                {
                    // Ignore invalid paths
                    Debug.WriteLine($"Error checking path: {ex.Message}");
                }
            }

            return null;
        }
    }
}
