using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace mattno.Plugins
{

    internal class FileWatcher
    {
        private class DebounceDispatcher
        {
            private readonly ILog _logger;
            private readonly TimeSpan _debounceTime;
            private readonly ConcurrentDictionary<FilePath, CancellationTokenSource> _timers = new ConcurrentDictionary<FilePath, CancellationTokenSource>();

            public DebounceDispatcher(ILog logger, TimeSpan debounceTime)
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger)); 
                _debounceTime = debounceTime;
            }

            internal async Task DebounceAsync(FilePath key, Func<Task> action)
            {
                var cts = new CancellationTokenSource();
                _ = _timers.AddOrUpdate(key, cts, (_, old) =>
                {
                    try { old.Cancel(); } catch (ObjectDisposedException) { }
                    try { old.Dispose(); } catch { }
                    return cts;

                });

                try
                {
                    await Task.Delay(_debounceTime, cts.Token);
                    if (_timers.TryGetValue(key, out var current) && ReferenceEquals(current, cts))
                    {
                        await action().ConfigureAwait(false);
                    }
                    else
                    {
                        _logger.Info($"[{nameof(DebounceDispatcher)}] Timer for {key} was removed before action could execute.");
                    }
                }
                catch (TaskCanceledException) { }
                catch (ObjectDisposedException) { }
                catch (Exception ex)
                {
                    _logger.Error($"[{nameof(DebounceDispatcher)}] Error executing debounced action for {key}: {ex.Message}", ex);
                }
                finally
                {
                    // Remove and dispose only if our CTS is still the registered one.
                    if (_timers.TryGetValue(key, out var current) && ReferenceEquals(current, cts))
                    {
                        if (_timers.TryRemove(key, out var removed) && ReferenceEquals(removed, cts))
                        {
                            try { removed.Dispose(); } catch { }
                        }
                    }
                    else
                    {
                        // If we've been replaced already, ensure our CTS is disposed to avoid leaks.
                        try { cts.Dispose(); } catch { }
                    }
                }
            }



            internal void Info(string prefix, FilePath key)
            {
                _logger.Debug($"[{nameof(DebounceDispatcher)}] {prefix} => Timer for {key}: {(_timers.ContainsKey(key) ? "EXISTS" : "MISSING")}.");
            }

            internal void Dispose()
            {
                foreach (var cts in _timers.Values)
                {
                    try { cts.Cancel(); } catch (ObjectDisposedException) { }
                    try { cts.Dispose(); } catch { }
                }
                _timers.Clear();
            }

        }

        private readonly ILog _logger;

        private readonly ConcurrentDictionary<DirectoryPath, FileSystemWatcher> _watchers = new ConcurrentDictionary<DirectoryPath, FileSystemWatcher>();
        private readonly ConcurrentDictionary<FilePath, Func<FilePath, Task>> _filesWatched = new ConcurrentDictionary<FilePath, Func<FilePath, Task>>();
        private readonly DebounceDispatcher _debouncer;

        public FileWatcher(ILog logger)
        {
            _logger = logger;
            _debouncer = new DebounceDispatcher(logger,TimeSpan.FromSeconds(3.0));
        }

        public void Register(FilePath fileToWatch, Func<FilePath, Task> action)
        {
            if (fileToWatch == null) throw new ArgumentNullException(nameof(fileToWatch));
            if (action == null) throw new ArgumentNullException(nameof(action));

            if (!_filesWatched.TryAdd(fileToWatch, action))
                return;

            _watchers.GetOrAdd(fileToWatch.Directory, dir =>
            {
                var watcher = new FileSystemWatcher
                {
                    Path = fileToWatch.Directory,
                    IncludeSubdirectories = false,
                    Filter = "*.*",
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true,
                };
                watcher.Changed += OnFileChanged;
                watcher.Created += OnFileChanged;

                return watcher;
            });
        }

        internal void Unregister(FilePath file)
        {
            _filesWatched.TryRemove(file, out _);
            if (_filesWatched.Keys.Any(f => f.Directory == file.Directory))
            {
                if (_watchers.TryRemove(file.Directory, out var removed)) {
                    Dispose(removed);
                }
                
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            var file = new FilePath(e.FullPath);
            if (!_filesWatched.TryGetValue(file, out var action))
                return;

            _debouncer.Info(e.ChangeType.ToString(), file);

            _ = _debouncer.DebounceAsync(file, async () =>
            {
                // Ensure the action still exists for this file; prefer re-resolving the delegate to handle updates.
                if (!_filesWatched.TryGetValue(file, out var latestAction))
                    return;

                await latestAction(file).ConfigureAwait(false);
            });
        }

        internal void Dispose()
        {
            _debouncer.Dispose();
            _filesWatched.Clear();
            foreach (var watcher in _watchers.Values)
            {
                Dispose(watcher);
            }
            _watchers.Clear();
        }

        private void Dispose(FileSystemWatcher watcher)
        {
            try
            {
                watcher.Changed -= OnFileChanged;
                watcher.Created -= OnFileChanged;
                watcher.Renamed -= OnFileChanged;
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            catch { }
        }

    }

}
