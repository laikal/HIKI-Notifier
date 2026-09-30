using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using HikiNotifier.Models;

namespace HikiNotifier.Services
{
    internal sealed class PlatformRegistry : IDisposable
    {
        private static readonly Lazy<PlatformRegistry> current = new Lazy<PlatformRegistry>(() =>
            Discover(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Providers")));
        public static PlatformRegistry Default => current.Value;
        private readonly List<IPlatformProvider> providers = new List<IPlatformProvider>();
        public IEnumerable<IPlatformProvider> Providers => providers;
        private readonly Action<string> log;
        internal PlatformRegistry(IEnumerable<IPlatformProvider> candidates, Action<string> log = null)
        {
            this.log = log ?? (message => Trace.WriteLine("[Provider] " + message));
            foreach (var provider in candidates) Register(provider);
        }
        public static PlatformRegistry Discover(string directory, Action<string> log = null)
        {
            var registry = new PlatformRegistry(new IPlatformProvider[0], log);
            try
            {
                if (!Directory.Exists(directory)) { registry.Log("directory missing: " + directory); return registry; }
                foreach (var file in Directory.GetFiles(directory, "*.dll").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    registry.Log("discovered " + Path.GetFileName(file));
                    try
                    {
                        var assembly = Assembly.LoadFrom(Path.GetFullPath(file));
                        Type[] types;
                        try { types = assembly.GetTypes(); }
                        catch (ReflectionTypeLoadException ex)
                        {
                            registry.Log("type load failure " + Path.GetFileName(file) + ": " + ex.GetType().Name);
                            types = ex.Types.Where(t => t != null).ToArray();
                        }
                        var found = false;
                        foreach (var type in types.Where(t => t.IsClass && !t.IsAbstract && t.IsPublic && typeof(IPlatformProvider).IsAssignableFrom(t)))
                        {
                            found = true;
                            try { registry.Register((IPlatformProvider)Activator.CreateInstance(type)); }
                            catch (Exception ex) { registry.Log("activation failed " + type.FullName + ": " + ex.GetType().Name); }
                        }
                        if (!found) registry.Log("no IPlatformProvider implementation: " + Path.GetFileName(file));
                    }
                    catch (Exception ex) { registry.Log("load failed " + Path.GetFileName(file) + ": " + ex.GetType().Name); }
                }
            }
            catch (Exception ex) { registry.Log("discovery failed: " + ex.GetType().Name); }
            return registry;
        }
        private void Register(IPlatformProvider provider)
        {
            try
            {
                var metadata = provider?.Metadata;
                if (metadata == null || string.IsNullOrWhiteSpace(metadata.Id) ||
                    string.IsNullOrWhiteSpace(metadata.DisplayName) || string.IsNullOrWhiteSpace(metadata.UrlTemplate) ||
                    (int)metadata.Platform < 0 || metadata.Platform != provider.Platform ||
                    metadata.PollInterval <= TimeSpan.Zero || metadata.PollInterval != provider.PollInterval)
                    throw new InvalidDataException("Invalid provider metadata");
                if (providers.Any(p => string.Equals(p.Metadata.Id, metadata.Id, StringComparison.OrdinalIgnoreCase) ||
                    p.Platform == metadata.Platform))
                { Log("duplicate provider ID/storage code: " + metadata.Id); provider.Dispose(); return; }
                providers.Add(provider);
                providers.Sort((a, b) => a.Metadata.Order.CompareTo(b.Metadata.Order));
                Log("loaded " + metadata.Id);
            }
            catch (Exception ex)
            {
                Log("registration failed: " + ex.GetType().Name);
                try { provider?.Dispose(); } catch { }
            }
        }
        private void Log(string message) { try { log(message); } catch { } }
        public IPlatformProvider ForUrl(string url)
        {
            foreach (var provider in providers)
            {
                try { if (provider.CanHandleUrl(url)) return provider; }
                catch (Exception ex) { Log("URL parser failed " + provider.Metadata.Id + ": " + ex.GetType().Name); }
            }
            return null;
        }
        public IPlatformProvider ForPlatform(PlatformType platform) => providers.FirstOrDefault(p => p.Platform == platform);
        public string DisplayName(PlatformType platform) => ForPlatform(platform)?.Metadata.DisplayName ?? platform.ToString().ToUpperInvariant();
        public PlatformCapabilities Capabilities(PlatformType platform) => ForPlatform(platform)?.Metadata.Capabilities ?? PlatformCapabilities.None;
        public void Dispose()
        {
            foreach (var provider in providers)
                try { provider.Dispose(); } catch (Exception ex) { Log("dispose failed: " + ex.GetType().Name); }
        }
    }
}
