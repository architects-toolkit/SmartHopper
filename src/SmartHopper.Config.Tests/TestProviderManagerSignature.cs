using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Diagnostics;
using SmartHopper.Config.Managers;
using Xunit;

namespace SmartHopper.Config.Tests
{
    public class TestProviderManagerSignature
    {
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
        [Fact(DisplayName = "VerifySignature_SignedDummyDll_DoesNotThrow [Windows]")]
#else
        [Fact(DisplayName = "VerifySignature_SignedDummyDll_DoesNotThrow [Core]")]
#endif
        public void VerifySignature_SignedDummyDll_DoesNotThrow()
        {
            // Arrange: generate and sign a dummy DLL at runtime
            var manager = ProviderManager.Instance;
            var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".dll");
            File.WriteAllText(tempFile, "dummy content");

            // Locate script relative to test assembly
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var solutionDir = Path.GetFullPath(Path.Combine(assemblyDir, "../../../../.."));
            var scriptPath = Path.Combine(solutionDir, "Sign-Authenticode.ps1");

            // Generate test PFX to a temp file to avoid overwriting default signing.pfx
            var tempDir = Path.GetDirectoryName(tempFile);
            var pfxFile = Path.Combine(tempDir, "test-signing.pfx");

            // Generate test PFX
            var startInfo1 = new ProcessStartInfo("pwsh", $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Generate -Password testpw")
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
            Assert.Equal(0, proc1.ExitCode);

            // Sign the dummy DLL using test PFX
            var startInfo2 = new ProcessStartInfo("pwsh", $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Sign \"{tempDir}\" -Password testpw")
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
            Assert.Equal(0, proc2.ExitCode);

            try
            {
                // Act & Assert: signed DLL should not throw
                var verifyMethod = typeof(ProviderManager).GetMethod("VerifySignature", BindingFlags.NonPublic | BindingFlags.Instance);
                var ex2 = Record.Exception(() => verifyMethod.Invoke(manager, new object[] { tempFile }));
                Assert.Null(ex2);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
                if (File.Exists(pfxFile)) File.Delete(pfxFile);
            }
        }
    }
}
