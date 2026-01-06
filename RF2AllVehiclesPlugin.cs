using GameReaderCommon;
using SimHub.Plugins;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace mattno.Plugins
{
    [PluginDescription("SimHub plugin for rFactor 2, keeping changes in seat, mirror and FFB sychronized in between same type of vehicle, i.e. liveries.")]
    [PluginAuthor("mattno")]
    [PluginName("rF2 AllVehicles Plugin")]
    public class RF2AllVehiclesPlugin : IDataPlugin
    {
        private bool _gameRunning = false;
        private Rf2AllVehicles _rf2AllVehicles;
        private FileWatcher _fileWatcher;
        private Task _processQueue;

        /// <summary>
        /// Instance of the current plugin manager
        /// </summary>
        public PluginManager PluginManager { private get; set; }

        /// <summary>
        /// Gets the left menu icon. Icon must be 24x24 and compatible with black and white display.
        /// </summary>
        //public ImageSource PictureIcon => this.ToIcon(Properties.Resources.sdkmenuicon);

        /// <summary>
        /// Gets a short plugin title to show in left menu. Return null if you want to use the title as defined in PluginName attribute.
        /// </summary>
        public string LeftMenuTitle => "rF2 AllVehicles";

        /// <summary>
        /// Called one time per game data update, contains all normalized game data,
        /// raw data are intentionally "hidden" under a generic object type (A plugin SHOULD NOT USE IT)
        ///
        /// This method is on the critical path, it must execute as fast as possible and avoid throwing any error
        ///
        /// </summary>
        /// <param name="pluginManager"></param>
        /// <param name="data">Current game data, including current and previous data frame.</param>
        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            // --- DETECT GAME STARTED/STOPPED (Session Active) ---
            if (data.GameName == "RFactor2")
            {
                if (!_gameRunning && data.RunningGameProcessDetected)
                {
                    OnGameProcessStarted();
                    _gameRunning = true;
                }
                else if (_gameRunning && !data.RunningGameProcessDetected)
                {
                    OnGameProcessStopped();
                    _gameRunning = false;
                }
            }
            else if (_gameRunning)
            {
                SimHub.Logging.Current.Warn($"[{nameof(RF2AllVehiclesPlugin)}] Still running, without process existing detected!");
                _gameRunning = false;
            }
        }

        // --- Custom Methods to keep code clean ---
        private void OnGameProcessStarted()
        {
            Process rf2Process = Process.GetProcessesByName("rFactor2").FirstOrDefault();
            if (rf2Process == default)
            {
                SimHub.Logging.Current.Error($"[{nameof(RF2AllVehiclesPlugin)}] 'rFactor2' process not found");
                return;
            }
            _rf2AllVehicles = new Rf2AllVehicles(SimHub.Logging.Current, rf2Process);
            if (_fileWatcher == null)
            {
                _fileWatcher = new FileWatcher(SimHub.Logging.Current);
            }
            _fileWatcher.Register(_rf2AllVehicles._playerJsonFile, (filePath) =>
            {
                _rf2AllVehicles?.EnqueueVehicle();
                return Task.CompletedTask;
            });

        }

        private void OnGameProcessStopped()
        {
            _fileWatcher?.Unregister(_rf2AllVehicles._playerJsonFile);
            _processQueue = Task.Run(() =>
            {
                _rf2AllVehicles?.ProcessQueue();
            });
        }

        /// <summary>
        /// Called at plugin manager stop, close/dispose anything needed here !
        /// Plugins are rebuilt at game change
        /// </summary>
        /// <param name="pluginManager"></param>
        public void End(PluginManager pluginManager)
        {
            _fileWatcher?.Dispose();
            _fileWatcher = null;
            if (_processQueue != null && !_processQueue.IsCompleted)
            {
                SimHub.Logging.Current.Info($"[{nameof(RF2AllVehiclesPlugin)}] Wating to finish processing of tracked vehicles.");
                _processQueue.Wait();
            }
        }

        /// <summary>
        /// Called once after plugins startup
        /// Plugins are rebuilt at game change
        /// </summary>
        /// <param name="pluginManager"></param>
        public void Init(PluginManager pluginManager)
        {
        }
    }
}