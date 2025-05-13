/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

namespace SmartHopper.Config.Tests
{
    public class ProviderManagerSignatureTests
    {
        private readonly ITestOutputHelper _output;
        public ProviderManagerSignatureTests(ITestOutputHelper output) => _output = output;

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
                    verifyMethod.Invoke(manager, new object[] { tempFile })
                );

                // Inner exception must be a CryptographicException
                Assert.IsType<CryptographicException>(ex.InnerException);
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "VerifySignature_AuthenticodeSigned_NoStrongName_ThrowsSecurityException [Windows]")]
#else
        [Fact(DisplayName = "VerifySignature_AuthenticodeSigned_NoStrongName_ThrowsSecurityException [Core]")]
#endif
        public void VerifySignature_AuthenticodeSigned_NoStrongName_ThrowsSecurityException()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new SkipException("Authenticode tests require Windows and signtool.exe");

            var manager = ProviderManager.Instance;
            var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".dll");
            
            // Emit a tiny valid DLL via Roslyn
            var syntaxTree = CSharpSyntaxTree.ParseText("public class Dummy {};");
            var references = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
            var compilation = CSharpCompilation.Create(
                Path.GetFileNameWithoutExtension(tempFile),
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var result = compilation.Emit(tempFile);
            Assert.True(result.Success, "Failed to compile dummy assembly");

            // Locate scripts relative to test assembly
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var solutionDir = Path.GetFullPath(Path.Combine(assemblyDir, "../../../../.."));
            var authScriptPath = Path.Combine(solutionDir, "Sign-Authenticode.ps1");

            // Generate test PFX to a temp file to avoid overwriting default signing.pfx
            var tempDir = Path.GetDirectoryName(tempFile);
            var pfxFile = Path.Combine(tempDir, "test-signing.pfx");

            // Generate test PFX for Authenticode
            var startInfo1 = new ProcessStartInfo("pwsh", $"-NoProfile -ExecutionPolicy Bypass -File \"{authScriptPath}\" -Generate -Password testpw")
            {
                WorkingDirectory = solutionDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            // Point PFX output to temp file
            startInfo1.Arguments += $" -PfxPath \"{pfxFile}\"";
            var proc1 = Process.Start(startInfo1);
            proc1.WaitForExit();
            var stdout1 = proc1.StandardOutput.ReadToEnd();
            var stderr1 = proc1.StandardError.ReadToEnd();
            _output.WriteLine("Generate PFX STDOUT:
" + stdout1);
            _output.WriteLine("Generate PFX STDERR:
" + stderr1);
            Assert.Equal(0, proc1.ExitCode);

            // Sign the dummy DLL with Authenticode using test PFX
            var startInfo2 = new ProcessStartInfo("pwsh", $"-NoProfile -ExecutionPolicy Bypass -File \"{authScriptPath}\" -Sign \"{tempFile}\" -Password testpw")
            {
                WorkingDirectory = solutionDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            // Use the same test PFX for signing
            startInfo2.Arguments += $" -PfxPath \"{pfxFile}\"";
            var proc2 = Process.Start(startInfo2);
            proc2.WaitForExit();
            var stdout2 = proc2.StandardOutput.ReadToEnd();
            var stderr2 = proc2.StandardError.ReadToEnd();
            _output.WriteLine("SIGN DLL STDOUT:
" + stdout2);
            _output.WriteLine("SIGN DLL STDERR:
" + stderr2);
            Assert.Equal(0, proc2.ExitCode);

            try
            {
                // Act & Assert: signed DLL passes Authenticode but fails strong-name check
                var verifyMethod = typeof(ProviderManager).GetMethod("VerifySignature", BindingFlags.NonPublic | BindingFlags.Instance);
                var ex2 = Assert.Throws<TargetInvocationException>(() => verifyMethod.Invoke(manager, new object[] { tempFile }));
                Assert.IsType<SecurityException>(ex2.InnerException);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
                if (File.Exists(pfxFile)) File.Delete(pfxFile);
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
                throw new SkipException("Strong-name tests require Windows");
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VSINSTALLDIR")))
                throw new SkipException("Please run tests in Visual Studio Developer PowerShell (VSINSTALLDIR not found)");

            var manager = ProviderManager.Instance;
            var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".dll");
            
            // Locate scripts relative to test assembly
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var solutionDir = Path.GetFullPath(Path.Combine(assemblyDir, "../../../../.."));
            var snkScriptPath = Path.Combine(solutionDir, "Sign-StrongNames.ps1");
            
            // Generate temp SNK file for strong-name signing
            var tempDir = Path.GetDirectoryName(tempFile);
            
            // Generate new SNK
            var startSnk = new ProcessStartInfo("pwsh", $"-NoProfile -ExecutionPolicy Bypass -File \"{snkScriptPath}\" -Generate")
            {
                WorkingDirectory = tempDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            var procSnk = Process.Start(startSnk);
            procSnk.WaitForExit();
            var stdoutSnk = procSnk.StandardOutput.ReadToEnd();
            var stderrSnk = procSnk.StandardError.ReadToEnd();
            _output.WriteLine("Generate SNK STDOUT:
" + stdoutSnk);
            _output.WriteLine("Generate SNK STDERR:
" + stderrSnk);
            Assert.Equal(0, procSnk.ExitCode);
            
            // Copy SNK to assembly directory to ensure same strong-name for tests
            var targetSnk = Path.Combine(assemblyDir, "signing.snk");
            File.Copy(Path.Combine(tempDir, "signing.snk"), targetSnk, true);
            
            // Now create assembly with that SNK
            var syntaxTree = CSharpSyntaxTree.ParseText("public class Dummy {};");
            var references = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
            var compilation = CSharpCompilation.Create(
                Path.GetFileNameWithoutExtension(tempFile),
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithCryptoKeyFile(targetSnk));
            var result = compilation.Emit(tempFile);
            Assert.True(result.Success, "Failed to compile strong-named dummy assembly");
            
            try
            {
                // Act & Assert: DLL has correct strong-name but no Authenticode signature, should fail Authenticode check
                var verifyMethod = typeof(ProviderManager).GetMethod("VerifySignature", BindingFlags.NonPublic | BindingFlags.Instance);
                var ex = Assert.Throws<TargetInvocationException>(() => verifyMethod.Invoke(manager, new object[] { tempFile }));
                Assert.IsType<CryptographicException>(ex.InnerException);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
                if (File.Exists(targetSnk)) File.Delete(targetSnk);
            }
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "VerifySignature_FullySigned_DoesNotThrow [Windows]")]
#else
        [Fact(DisplayName = "VerifySignature_FullySigned_DoesNotThrow [Core]")]
#endif
        public void VerifySignature_FullySigned_DoesNotThrow()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new SkipException("Authenticode and strong-name tests require Windows");

            var manager = ProviderManager.Instance;
            var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".dll");
            
            // Locate scripts relative to test assembly
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var solutionDir = Path.GetFullPath(Path.Combine(assemblyDir, "../../../../.."));
            var authScriptPath = Path.Combine(solutionDir, "Sign-Authenticode.ps1");
            var snkScriptPath = Path.Combine(solutionDir, "Sign-StrongNames.ps1");
            
            // Generate temp SNK file for strong-name signing
            var tempDir = Path.GetDirectoryName(tempFile);
            var snkFile = Path.Combine(tempDir, "test-signing.snk");
            
            // Generate new SNK
            var startSnk = new ProcessStartInfo("pwsh", $"-NoProfile -ExecutionPolicy Bypass -File \"{snkScriptPath}\" -Generate")
            {
                WorkingDirectory = tempDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            var procSnk = Process.Start(startSnk);
            procSnk.WaitForExit();
            var stdoutSnk = procSnk.StandardOutput.ReadToEnd();
            var stderrSnk = procSnk.StandardError.ReadToEnd();
            _output.WriteLine("Generate SNK STDOUT:
" + stdoutSnk);
            _output.WriteLine("Generate SNK STDERR:
" + stderrSnk);
            Assert.Equal(0, procSnk.ExitCode);
            
            // Copy SNK to assembly directory to ensure same strong-name for tests
            var targetSnk = Path.Combine(assemblyDir, "signing.snk");
            File.Copy(Path.Combine(tempDir, "signing.snk"), targetSnk, true);
            
            // Now create assembly with that SNK
            var syntaxTree = CSharpSyntaxTree.ParseText("public class Dummy {};");
            var references = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
            var compilation = CSharpCompilation.Create(
                Path.GetFileNameWithoutExtension(tempFile),
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithCryptoKeyFile(targetSnk));
            var result = compilation.Emit(tempFile);
            Assert.True(result.Success, "Failed to compile strong-named dummy assembly");
            
            // Generate test PFX for Authenticode
            var pfxFile = Path.Combine(tempDir, "test-signing.pfx");
            var startInfo1 = new ProcessStartInfo("pwsh", $"-NoProfile -ExecutionPolicy Bypass -File \"{authScriptPath}\" -Generate -Password testpw")
            {
                WorkingDirectory = solutionDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            // Point PFX output to temp file
            startInfo1.Arguments += $" -PfxPath \"{pfxFile}\"";
            var proc1 = Process.Start(startInfo1);
            proc1.WaitForExit();
            var stdout1 = proc1.StandardOutput.ReadToEnd();
            var stderr1 = proc1.StandardError.ReadToEnd();
            _output.WriteLine("Generate PFX STDOUT:
" + stdout1);
            _output.WriteLine("Generate PFX STDERR:
" + stderr1);
            Assert.Equal(0, proc1.ExitCode);

            // Sign with Authenticode
            var startInfo2 = new ProcessStartInfo("pwsh", $"-NoProfile -ExecutionPolicy Bypass -File \"{authScriptPath}\" -Sign \"{tempFile}\" -Password testpw")
            {
                WorkingDirectory = solutionDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            // Use the same test PFX for signing
            startInfo2.Arguments += $" -PfxPath \"{pfxFile}\"";
            var proc2 = Process.Start(startInfo2);
            proc2.WaitForExit();
            var stdout2 = proc2.StandardOutput.ReadToEnd();
            var stderr2 = proc2.StandardError.ReadToEnd();
            _output.WriteLine("SIGN DLL STDOUT:
" + stdout2);
            _output.WriteLine("SIGN DLL STDERR:
" + stderr2);
            Assert.Equal(0, proc2.ExitCode);

            try
            {
                // Act & Assert: fully signed DLL should not throw
                var verifyMethod = typeof(ProviderManager).GetMethod("VerifySignature", BindingFlags.NonPublic | BindingFlags.Instance);
                var ex2 = Record.Exception(() => verifyMethod.Invoke(manager, new object[] { tempFile }));
                Assert.Null(ex2);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
                if (File.Exists(pfxFile)) File.Delete(pfxFile);
                if (File.Exists(targetSnk)) File.Delete(targetSnk);
            }
        }
    }
}
