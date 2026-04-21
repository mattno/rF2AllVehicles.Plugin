using GameReaderCommon;
using SimHub.Plugins;
using System.Diagnostics;
using System.Linq;

namespace mattno.Plugins
{
    [PluginDescription("SimHub plugin for rFactor 2, keeping changes in seat, mirror and FFB sychronized in between same type of vehicle, i.e. liveries.")]
    [PluginAuthor("mattno")]
    [PluginName("rF2 AllVehicles Plugin")]
    public class RF2AllVehiclesPlugin : IDataPlugin
    {
        private bool _rf2Running = false;
        private Rf2AllVehicles _rf2AllVehicles;

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
                if (!_rf2Running && data.RunningGameProcessDetected)
                {
                    _rf2Running = true;
                    OnGameProcessStarted();
                }
                else if (_rf2Running && !data.RunningGameProcessDetected)
                {
                    _rf2Running = false;
                    OnGameProcessStopped();
                }
            }
            else if (_rf2Running)
            {
                SimHub.Logging.Current.Warn($"[{nameof(RF2AllVehiclesPlugin)}] Still running, without process existing detected!");
                _rf2Running = false;
            }
        }

        private void OnGameProcessStarted()
        {

            Process rf2Process = Process.GetProcessesByName("rFactor2").FirstOrDefault();
            if (rf2Process == default)
            {
                SimHub.Logging.Current.Error($"[{nameof(RF2AllVehiclesPlugin)}] 'rFactor2' process not found");
                return;
            }
            _rf2AllVehicles = new Rf2AllVehicles(SimHub.Logging.Current, rf2Process);
        }

        private void OnGameProcessStopped()
        {
            _rf2AllVehicles?.ProcessQueue();
            End(this.PluginManager); // free resources, will be re-created at next start
        }

        /// <summary>
        /// Called at plugin manager stop, close/dispose anything needed here !
        /// Plugins are rebuilt at game change
        /// </summary>
        /// <param name="pluginManager"></param>
        public void End(PluginManager pluginManager)
        {
            _rf2AllVehicles?.Dispose();
            _rf2AllVehicles = null;

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