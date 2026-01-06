using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace mattno.Plugins
{

    internal class FileWatcher
    {
        private class DebounceDispatcher
        {
            private readonly Dictionary<string, CancellationTokenSource> _timers = new Dictionary<string, CancellationTokenSource>();
            private readonly TimeSpan _debounceTime;

            public DebounceDispatcher(TimeSpan debounceTime)
            {
                _debounceTime = debounceTime;
            }

            public void Debounce(string key, Func<Task> action)
            {
                if (_timers.TryGetValue(key, out var cts))
                {
                    cts.Cancel();
                    _timers.Remove(key);
                }

                cts = new CancellationTokenSource();
                _timers[key] = cts;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(_debounceTime, cts.Token);
                        await action();
                    }
                    catch (TaskCanceledException) { }
                });
            }

            internal void Dispose()
            {
                foreach (var cts in _timers.Values)
                {
                    cts.Cancel();
                }
                _timers.Clear();
            }
        }

        private readonly ILog _logger;

        private readonly IDictionary<DirectoryPath, FileSystemWatcher> _watchers = new ConcurrentDictionary<DirectoryPath, FileSystemWatcher>();
        private readonly IDictionary<FilePath, Func<FilePath, Task>> _filesWatched = new ConcurrentDictionary<FilePath, Func<FilePath, Task>>();
        private readonly DebounceDispatcher _debouncer = new DebounceDispatcher(TimeSpan.FromSeconds(1.25));

        public FileWatcher(ILog logger)
        {
            _logger = logger;
        }

        public void Register(FilePath fileToWatch, Func<FilePath, Task> action)

        {
            if (_filesWatched.ContainsKey(fileToWatch))
                return;

            if (!_watchers.TryGetValue(fileToWatch.Directory, out var watcher))
            {
                watcher = new FileSystemWatcher
                {
                    Path = fileToWatch.Directory,
                    IncludeSubdirectories = false,
                    Filter = "*",
                    EnableRaisingEvents = true,
                };
                watcher.Changed += OnFileChanged;
                watcher.Created += OnFileChanged;
                watcher.Renamed += OnFileChanged;

                _watchers[fileToWatch.Directory] = watcher;

            }
            _filesWatched[fileToWatch] = action;
        }

        internal void Unregister(FilePath playerJsonFile)
        {
            _filesWatched.Remove(playerJsonFile);
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            var file = new FilePath(e.FullPath);
            if (!_filesWatched.Keys.Contains(file))
                return;

            _debouncer.Debounce(file, async () =>
            {
                // in case dispose was called while debouncing
                if (!_filesWatched.Keys.Contains(file))
                    return;

                await _filesWatched[file](file);
            });
        }

        internal void Dispose()
        {
            _debouncer.Dispose();
            _filesWatched.Clear();
            foreach (var watcher in _watchers.Values)
            {
                watcher.Dispose();
            }
            _watchers.Clear();
        }

    }

}
