using System;
using System.Collections.Generic;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.CSharp;
using HikiNotifier;
using HikiNotifier.Models;
using HikiNotifier.Services;
using HikiNotifier.UI;

internal static class ModuleChecks
{
    public static void Run(Action<bool, string> check)
    {
        var root = Path.Combine(Path.GetTempPath(), "hiki-module-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        // Loaded .NET Framework assemblies cannot be unloaded individually. The caller's
        // isolated test directory may retain DLL fixtures until this process has exited.
        var log = new List<string>();
        using (var missing = PlatformRegistry.Discover(Path.Combine(root, "missing"), log.Add))
            check(!missing.Providers.Any() && log.Any(x => x.Contains("directory missing")), "missing plugin folder isolated");
        var plugins = Path.Combine(root, "Providers"); Directory.CreateDirectory(plugins);
        File.WriteAllText(Path.Combine(plugins, "broken.dll"), "not a PE file");
        File.Copy(typeof(ProductVersion).Assembly.Location, Path.Combine(plugins, "ordinary.dll"));
        using (var compiler = new CSharpCodeProvider())
        {
            var options = new CompilerParameters(new[] { "System.dll", "System.Core.dll", typeof(IPlatformProvider).Assembly.Location },
                Path.Combine(plugins, "Fixture.dll")) { GenerateExecutable = false, CompilerOptions = "/platform:x64" };
            var result = compiler.CompileAssemblyFromSource(options, Fixture);
            if (result.Errors.HasErrors) throw new Exception(string.Join("\n", result.Errors.Cast<CompilerError>()));
        }
        using (var registry = PlatformRegistry.Discover(plugins, log.Add))
        {
            check(registry.Providers.Count() == 1, "valid plugin survives corrupt/duplicate/invalid plugins");
            check(log.Any(x => x.Contains("load failed")) && log.Any(x => x.Contains("duplicate")) &&
                log.Any(x => x.Contains("activation failed")) && log.Any(x => x.Contains("registration failed")) &&
                log.Any(x => x.Contains("no IPlatformProvider")), "plugin failure reasons logged");
            var provider = registry.ForUrl("https://fixture.invalid/channel");
            check(provider != null && (int)provider.Platform == 77, "new numeric platform resolves without enum change");
            foreach (var language in new[] { "ko", "en", "ja" })
            using (var form = new ProfileEditForm(new LanguageService(language), registry))
            {
                var add = form.Controls.Find("addChannelButton", true).Single();
                check(add.ContextMenuStrip.Items.Count == 1 && add.ContextMenuStrip.Items[0].Text == "Fixture",
                    language + " metadata-driven extension menu");
                ((ToolStripMenuItem)add.ContextMenuStrip.Items[0]).PerformClick();
                check(form.Controls.Find("newChannelUrl", true).Single().Text == provider.Metadata.UrlTemplate,
                    language + " extension URL template");
            }
            var path = Path.Combine(root, "state", "settings.json");
            var storage = new SettingsService(path, registry);
            var settings = storage.Load();
            var profile = new ChannelProfile { Platform = (PlatformType)77, ChannelId = "channel", LiveUrl = "https://fixture.invalid/channel", AutoOpenLive = true,
                ProviderState = new ProviderState { LastSeenContentId = "baseline", RecentContentIds = new List<string> { "baseline" }, LastLiveSessionId = "session" } };
            var streamer = new StreamerProfile { DisplayName = "Fixture", Channels = new List<ChannelProfile> { profile } };
            settings.Streamers.Add(streamer); storage.Save(settings);
            using (var none = new PlatformRegistry(new IPlatformProvider[0]))
            {
                var absentStorage = new SettingsService(path, none); var absent = absentStorage.Load();
                check(absentStorage.LoadErrors.Count == 0 && absent.Streamers.Single(x => !x.IsBuiltIn).Channels.Single().ProviderState.LastSeenContentId == "baseline",
                    "missing DLL preserves stored channel and baseline");
                absentStorage.Save(absent);
                using (var edit = new ProfileEditForm(new LanguageService("en"), none, absent.Streamers.Single(x => !x.IsBuiltIn)))
                {
                    ((Button)edit.Controls.Find("saveProfileButton", true).Single()).PerformClick();
                    // Hidden buttons do not receive PerformClick on every WinForms runtime.
                    typeof(ProfileEditForm).GetMethod("SaveDraft", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(edit, null);
                    check(edit.Channels.Single().AutoOpenLive, "editing missing plugin preserves AutoOpen setting");
                }
            }
            var restored = storage.Load().Streamers.Single(x => !x.IsBuiltIn).Channels.Single();
            check((int)restored.Platform == 77 && restored.ProviderState.LastLiveSessionId == "session" && restored.ProviderState.RecentContentIds.Contains("baseline"),
                "plugin restored with compatible persisted state");
            profile.State = LiveState.Live; profile.LiveSessionId = "session";
            using (var polling = new PollingService(registry, () => new[] { profile }))
            {
                polling.Start(); polling.StopAsync().GetAwaiter().GetResult();
                check(profile.State == LiveState.Unknown && profile.LiveSessionId == "session", "plugin polling exception isolated without session reset");
            }
        }
        var actual = PlatformRegistry.Default.Providers.ToArray();
        check(actual.Length == 7, "all seven release modules discovered");
        check(actual.All(x => x.Metadata.LogoPng != null && x.Metadata.LogoPng.Length > 0), "seven embedded provider logos");
        check(actual.All(x => x.GetType().Assembly.GetReferencedAssemblies().All(a => a.Name != "HIKI Notifier")), "providers do not depend on Main EXE");
        foreach (var assembly in actual.Select(x => x.GetType().Assembly).Concat(new[] { typeof(ProductVersion).Assembly, typeof(IPlatformProvider).Assembly }))
        {
            var info = FileVersionInfo.GetVersionInfo(assembly.Location);
            check(info.ProductVersion == ProductVersion.Current && info.FileVersion == ProductVersion.NumericVersion, assembly.GetName().Name + " centralized version");
        }
        // Fresh process: no provider references have been JIT-loaded, no existing registry singleton.
        foreach (var damaged in new[] { false, true })
        {
            var stage = Path.Combine(root, damaged ? "damaged-startup" : "empty-startup"); Directory.CreateDirectory(stage);
            var executable = Assembly.GetExecutingAssembly().Location;
            foreach (var file in new[] { executable, executable + ".config", typeof(IPlatformProvider).Assembly.Location, typeof(ProductVersion).Assembly.Location })
                File.Copy(file, Path.Combine(stage, Path.GetFileName(file)));
            Directory.CreateDirectory(Path.Combine(stage, "lang"));
            foreach (var file in Directory.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lang"), "*.ini")) File.Copy(file, Path.Combine(stage, "lang", Path.GetFileName(file)));
            if (damaged) { Directory.CreateDirectory(Path.Combine(stage, "Providers")); File.WriteAllText(Path.Combine(stage, "Providers", "broken.dll"), "bad DLL"); }
            var start = new ProcessStartInfo(Path.Combine(stage, Path.GetFileName(executable)), "--module-startup \"" + Path.Combine(stage, "settings.json") + "\"") {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var child = Process.Start(start))
            {
                if (!child.WaitForExit(20000)) { child.Kill(); throw new Exception("Isolated module GUI smoke timed out"); }
                var error = child.StandardError.ReadToEnd();
                check(child.ExitCode == 0, (damaged ? "corrupt" : "missing") + " modules Main/Tray startup: " + error);
                check(File.Exists(Path.Combine(stage, "settings.json")) && Directory.Exists(Path.Combine(stage, "profiles")), "clean startup creates isolated settings/profiles");
            }
        }
        Console.WriteLine("Module fixture evidence: " + root);
    }

    public static void Smoke(string settingsPath)
    {
        Application.EnableVisualStyles();
        using (var form = new MainForm(false, new SettingsService(settingsPath)))
        {
            form.Show(); Application.DoEvents();
            if (!form.IsHandleCreated || !form.Text.Contains(ProductVersion.Current)) throw new Exception("Main initialization failed");
            var list = form.Controls.OfType<ListView>().Single();
            if (list.Items.Count != 3) throw new Exception("Built-in HIKI rows missing");
            form.Close(); Application.DoEvents();
            if (form.IsDisposed || form.Visible) throw new Exception("Close-to-tray behavior changed");
            ((Task)typeof(MainForm).GetMethod("ExitAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null)).GetAwaiter().GetResult();
        }
    }

    private const string Fixture = @"
using System; using System.Collections.Generic; using System.Threading; using System.Threading.Tasks;
using HikiNotifier.Models; using HikiNotifier.Services;
public class FixtureProvider : IPlatformProvider {
 public virtual ProviderMetadata Metadata { get { return new ProviderMetadata { Id=""fixture"", Platform=Platform, DisplayName=""Fixture"", UrlTemplate=""https://fixture.invalid/"", PollInterval=PollInterval, Capabilities=Capabilities }; } }
 public PlatformType Platform { get { return (PlatformType)77; } }
 public PlatformCapabilities Capabilities { get { return PlatformCapabilities.LiveStatus; } }
 public TimeSpan PollInterval { get { return TimeSpan.FromSeconds(30); } }
 public bool CanHandleUrl(string url) { return url.StartsWith(""https://fixture.invalid/""); }
 public void NormalizeStoredProfile(ChannelProfile p) {} public void PrepareForSave(ChannelProfile p) {}
 public ReplayableNotification GetReplayableNotification(ChannelProfile p) { return null; }
 public Task<ChannelProfile> CreateAsync(string url,CancellationToken t) { return Task.FromResult(new ChannelProfile { Platform=Platform, LiveUrl=url }); }
 public Task PollAsync(IList<ChannelProfile> p,Action<ChannelProfile,LiveState,ContentEntry> u,CancellationToken t) { throw new InvalidOperationException(""fixture poll failure""); }
 public void Dispose() {}
}
public class DuplicateProvider : FixtureProvider {}
public class InvalidProvider : FixtureProvider { public override ProviderMetadata Metadata { get { return null; } } }
public class ThrowingProvider : FixtureProvider { public ThrowingProvider() { throw new InvalidOperationException(); } }
";
}
