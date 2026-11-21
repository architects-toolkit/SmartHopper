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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.FileSystem;
using Xunit;

namespace SmartHopper.Infrastructure.Tests
{
    /// <summary>Filesystem-free permission, persistence and boundary regressions for the local-file gate.</summary>
    public sealed class LocalFileAccessTests : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "sh-file-tests-" + Guid.NewGuid().ToString("N"));
        private readonly LocalFileAllowlist store;

        /// <summary>Initializes an isolated permissions file, never the user's real settings.</summary>
        public LocalFileAccessTests()
        {
            Directory.CreateDirectory(this.directory);
            this.store = new LocalFileAllowlist(Path.Combine(this.directory, "permissions.json"));
        }

        /// <summary>Cleans up the temporary permission store.</summary>
        public void Dispose()
        {
            try
            {
                Directory.Delete(this.directory, recursive: true);
            }
            catch
            {
                // Best effort cleanup.
            }
        }

        private static Task<LocalFileAccessDecision> Decide(LocalFileAccessDecision decision) =>
            Task.FromResult(decision);

        private string Target(string name = "document.gh")
        {
            var path = Path.Combine(this.directory, name);
            File.WriteAllText(path, "test");
            return path;
        }

        /// <summary>Always-allow persists an exact file grant that survives reopen and can be revoked.</summary>
        [Fact]
        public async Task AlwaysAllow_PersistsExactFile()
        {
            var path = this.Target();
            await LocalFileAccess.RequireAsync(path, LocalFileCapability.Read, "test", this.store,
                (_, _) => Decide(LocalFileAccessDecision.AlwaysAllow), default);

            var reopened = new LocalFileAllowlist(Path.Combine(this.directory, "permissions.json"));
            Assert.True(reopened.IsGranted(path, LocalFileCapability.Read));
            Assert.False(reopened.IsGranted(path, LocalFileCapability.Write));
            Assert.False(reopened.IsGranted(this.Target("other.gh"), LocalFileCapability.Read));

            reopened.Revoke(reopened.GetGrants());
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                LocalFileAccess.RequireAsync(path, LocalFileCapability.Read, "test", reopened, null, default));
        }

        /// <summary>Folder grants cover descendants but not sibling paths sharing a prefix.</summary>
        [Fact]
        public async Task AlwaysAllowFolder_CoversSubtreeOnly()
        {
            var sub = Path.Combine(this.directory, "workspace");
            var nested = Path.Combine(sub, "nested");
            Directory.CreateDirectory(nested);
            var file = Path.Combine(nested, "model.gh");
            File.WriteAllText(file, "test");

            await LocalFileAccess.RequireAsync(file, LocalFileCapability.Write, "test", this.store,
                (_, _) => Decide(LocalFileAccessDecision.AlwaysAllowFolder), default);

            // The grant lands on the file's parent directory and covers deeper paths.
            var grant = this.store.GetGrants().Single();
            Assert.Equal(LocalFileGrantScope.Directory, grant.Scope);
            Assert.True(this.store.IsGranted(file, LocalFileCapability.Write));
            var deeper = Path.Combine(nested, "deeper", "another.gh");
            Assert.True(this.store.IsGranted(deeper, LocalFileCapability.Write));

            // A sibling directory whose name only shares the prefix is not covered.
            var sibling = Path.Combine(this.directory, "workspace-sibling", "other.gh");
            Assert.False(this.store.IsGranted(sibling, LocalFileCapability.Write));
            Assert.False(this.store.IsGranted(file, LocalFileCapability.Read));
        }

        /// <summary>One-call grants do not persist and are shared by nested scopes.</summary>
        [Fact]
        public async Task AllowOnce_IsScopedToOneInvocation()
        {
            var count = 0;
            Task<LocalFileAccessDecision> Prompt(LocalFileAccessRequest _, CancellationToken __)
            {
                count++;
                return Decide(LocalFileAccessDecision.AllowOnce);
            }

            var path = this.Target();
            using (LocalFileAccess.BeginScope())
            {
                await LocalFileAccess.RequireAsync(path, LocalFileCapability.Read, "test", this.store, Prompt, default);
                using (LocalFileAccess.BeginScope())
                {
                    await LocalFileAccess.RequireAsync(path, LocalFileCapability.Read, "test", this.store, Prompt, default);
                }
            }

            Assert.Equal(1, count);
            using (LocalFileAccess.BeginScope())
            {
                await LocalFileAccess.RequireAsync(path, LocalFileCapability.Read, "test", this.store, Prompt, default);
            }

            Assert.Equal(2, count);
            Assert.Empty(this.store.GetGrants());
        }

        /// <summary>The denied tier refuses without ever consulting the prompt.</summary>
        [Fact]
        public async Task DeniedTier_NeverPrompts()
        {
            var prompted = false;
            Task<LocalFileAccessDecision> Prompt(LocalFileAccessRequest _, CancellationToken __)
            {
                prompted = true;
                return Decide(LocalFileAccessDecision.AlwaysAllow);
            }

            var ssh = Path.Combine(this.directory, ".ssh", "id_rsa");
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                LocalFileAccess.RequireAsync(ssh, LocalFileCapability.Read, "test", this.store, Prompt, default));
            Assert.False(prompted);

            var own = Path.Combine(LocalPath.SmartHopperDataDirectory, "providers.json");
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                LocalFileAccess.RequireAsync(own, LocalFileCapability.Read, "test", this.store, Prompt, default));
            Assert.False(prompted);
        }

        /// <summary>An unanswered prompt and a missing presenter both fail closed.</summary>
        [Fact]
        public async Task NoResponse_And_NoPresenter_Deny()
        {
            var path = this.Target();
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                LocalFileAccess.RequireAsync(path, LocalFileCapability.Read, "test", this.store,
                    (_, _) => Decide(LocalFileAccessDecision.NoResponse), default));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                LocalFileAccess.RequireAsync(path, LocalFileCapability.Read, "test", this.store, null, default));
            Assert.Empty(this.store.GetGrants());
        }

        /// <summary>A denied path does not prompt again inside the same scope.</summary>
        [Fact]
        public async Task Deny_IsRememberedForTheScope()
        {
            var count = 0;
            Task<LocalFileAccessDecision> Prompt(LocalFileAccessRequest _, CancellationToken __)
            {
                count++;
                return Decide(LocalFileAccessDecision.Deny);
            }

            var path = this.Target();
            using (LocalFileAccess.BeginScope())
            {
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                    LocalFileAccess.RequireAsync(path, LocalFileCapability.Read, "test", this.store, Prompt, default));
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                    LocalFileAccess.RequireAsync(path, LocalFileCapability.Read, "test", this.store, Prompt, default));
            }

            Assert.Equal(1, count);
        }

        /// <summary>Relative paths are rejected before the prompt.</summary>
        [Fact]
        public async Task RelativePath_IsRejected()
        {
            var prompted = false;
            Task<LocalFileAccessDecision> Prompt(LocalFileAccessRequest _, CancellationToken __)
            {
                prompted = true;
                return Decide(LocalFileAccessDecision.AlwaysAllow);
            }

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                LocalFileAccess.RequireAsync("docs/model.gh", LocalFileCapability.Read, "test", this.store, Prompt, default));
            Assert.False(prompted);
        }

        /// <summary>Pre-authorized roots never prompt: Screenshots writes and smarthopper temp files.</summary>
        [Fact]
        public async Task PreAuthorized_NeverPrompts()
        {
            var screenshots = Path.Combine(LocalPath.ScreenshotsDirectory, "canvas.png");
            await LocalFileAccess.RequireAsync(screenshots, LocalFileCapability.Write, "test", this.store, null, default);

            var temp = Path.Combine(Path.GetTempPath(), "smarthopper-attachment-test.pdf");
            await LocalFileAccess.RequireAsync(temp, LocalFileCapability.Read, "test", this.store, null, default);
        }

        /// <summary>Pre-authorization never covers Execute: opening a sandbox or temp file still prompts.</summary>
        [Fact]
        public async Task PreAuthorized_NeverCoversExecute()
        {
            var screenshots = Path.Combine(LocalPath.ScreenshotsDirectory, "canvas.png");
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                LocalFileAccess.RequireAsync(screenshots, LocalFileCapability.Execute, "test", this.store, null, default));

            var temp = Path.Combine(Path.GetTempPath(), "smarthopper-runme.bat");
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                LocalFileAccess.RequireAsync(temp, LocalFileCapability.Execute, "test", this.store, null, default));
        }

        /// <summary>Browser credential stores are denied on every platform's profile layout.</summary>
        [Fact]
        public async Task DeniedTier_CoversAllBrowserLayouts()
        {
            var prompted = false;
            Task<LocalFileAccessDecision> Prompt(LocalFileAccessRequest _, CancellationToken __)
            {
                prompted = true;
                return Decide(LocalFileAccessDecision.AlwaysAllow);
            }

            var layouts = new[]
            {
                // Windows: %LOCALAPPDATA%\Google\Chrome\User Data\Default\
                Path.Combine(this.directory, "Google", "Chrome", "User Data", "Default", "Login Data"),
                // macOS: ~/Library/Application Support/Google/Chrome/Default/
                Path.Combine(this.directory, "Library", "Application Support", "Google", "Chrome", "Default", "Login Data"),
                // Linux: ~/.mozilla/firefox/<profile>/
                Path.Combine(this.directory, ".mozilla", "firefox", "abc.default", "logins.json"),
                // macOS: ~/Library/Application Support/Firefox/Profiles/
                Path.Combine(this.directory, "Library", "Application Support", "Firefox", "Profiles", "abc.default", "logins.json"),
            };

            foreach (var path in layouts)
            {
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                    LocalFileAccess.RequireAsync(path, LocalFileCapability.Read, "test", this.store, Prompt, default));
            }

            Assert.False(prompted);
        }

        /// <summary>Extended-length device prefixes cannot spell around the denied tier.</summary>
        [Fact]
        public async Task DeniedTier_DevicePrefixCannotEvade()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            var prompted = false;
            Task<LocalFileAccessDecision> Prompt(LocalFileAccessRequest _, CancellationToken __)
            {
                prompted = true;
                return Decide(LocalFileAccessDecision.AlwaysAllow);
            }

            var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var escaped = @"\\?\" + Path.Combine(windir, "system.ini");
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                LocalFileAccess.RequireAsync(escaped, LocalFileCapability.Read, "test", this.store, Prompt, default));
            Assert.False(prompted);
        }

        /// <summary>Trusted internal flows can grant a path for the rest of the scope.</summary>
        [Fact]
        public async Task GrantForCurrentScope_CoversLaterRequire()
        {
            var path = this.Target();
            using (LocalFileAccess.BeginScope())
            {
                LocalFileAccess.GrantForCurrentScope(path, LocalFileCapability.Read);
                await LocalFileAccess.RequireAsync(path, LocalFileCapability.Read, "test", this.store, null, default);
            }

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                LocalFileAccess.RequireAsync(path, LocalFileCapability.Read, "test", this.store, null, default));
        }

        /// <summary>The permission file stores readable scope, path and capability entries.</summary>
        [Fact]
        public async Task PersistedFile_StoresReadableEntries()
        {
            var path = this.Target();
            await LocalFileAccess.RequireAsync(path, LocalFileCapability.Write, "test", this.store,
                (_, _) => Decide(LocalFileAccessDecision.AlwaysAllow), default);

            var saved = File.ReadAllText(Path.Combine(this.directory, "permissions.json"));
            Assert.Contains("\"File\"", saved);
            Assert.Contains("\"Write\"", saved);
            Assert.Contains(path.Replace("\\", "\\\\"), saved);
        }
    }
}
