using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace mattno.Plugins
{


    internal static class IEnumarableExtensions
    {
        public static string FormatListForLog<T>(this IEnumerable<T> list, int maxItemsToShow = 5)
        {
            if (list == null)
            {
                return "[null]";
            }
            if (list.Count() == 0)
            {
                return "[]";
            }
            var shownItems = list.Take(maxItemsToShow).Select(item => item?.ToString() ?? "null");
            var result = string.Join(", ", shownItems);

            if (list.Count() > maxItemsToShow)
            {
                result += ", ...";
            }
            return $"[{result} ({list.Count()} items)]";
        }
    }


    internal class Vehicle : IEquatable<Vehicle>
    {
        internal IList<string> Raw
        {
            get; private set;
        }
        internal int ID
        {
            get; private set;
        }
        internal VehicleFile VehicleFile
        {
            get; private set;
        }
        public FilePath File
        {
            get;
            private set;
        }
        public bool IsInstalled => !File.DirectoryMissing && File.Directory.Exists;

        private Vehicle()
        {
        }

        private Vehicle(IEnumerable<string> raw, int id, FilePath file)
        {
            Raw = new List<string>(raw);
            ID = id;
            File = file;
            VehicleFile = VehicleFile.Of(file);
        }

        internal static Vehicle From(IEnumerable<string> rawLines)
        {
            if (rawLines == null)
            {
                throw new ArgumentNullException(nameof(rawLines));
            }
            return new Vehicle(
                raw: rawLines,
                id: Fetch(rawLines, "ID", IntRegex, int.Parse),
                file: Fetch(rawLines, "File", StringRegex, s => new FilePath(s))
            );
        }
        private static readonly Regex IntRegex = new Regex("(?<field>.+?)=(?<value>\\d+)", RegexOptions.Compiled);
        private static readonly Regex StringRegex = new Regex("(?<field>.+?)=\"(?<value>[^\"]*)\"", RegexOptions.Compiled);

        private static T Fetch<T>(IEnumerable<string> rawLines, string field, Regex fieldMatcher, Func<string, T> asT)
        {
            return rawLines
                .Select(line => fieldMatcher.Match(line))
                .Where(m => m.Success)
                .Where(m => m.Groups["field"].Value.Equals(field))
                .Select(m => m.Groups["value"].Value)
                .Select(asT)
                .First();
        }




        /// <summary>
        /// Create a new Vehicle, coping certain fields from other Vehicle
        /// </summary>
        /// <param name="other"></param> The other Vehicle to use  fields from
        /// <param name="fieldNames"></param> The actual fields/data to use from the other Vehicle
        /// <returns></returns>
        internal Vehicle WithOther(Vehicle other, params string[] fieldNames)
        {
            var regs = fieldNames.Select(f => new Regex($"{f}=(?<value>.*)$", RegexOptions.IgnoreCase));
            return From(Raw.Select(line => SwapFieldLine(line, other, regs)));
        }

        private static string SwapFieldLine(string lineToSwap, Vehicle fromOther, IEnumerable<Regex> whenFieldMatch)
        {
            Regex matchThis = whenFieldMatch.FirstOrDefault(m => m.IsMatch(lineToSwap));
            if (matchThis == default)
            {
                return lineToSwap;
            }

            // Find the first matching line from fromOther.Raw, otherwise return lineToSwap
            var swappedLine = fromOther.Raw
                .Select(line => matchThis.Match(line))
                .Where(m => m.Success)
                .Select(m => m.Groups[0].Value)
                .FirstOrDefault();

            return swappedLine ?? lineToSwap;
        }

        public override string ToString()
        {
            return $"{{{nameof(Vehicle)}{{{nameof(ID)}={ID}}}}}";
        }

        public static bool operator ==(Vehicle left, Vehicle right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null || right is null)
            {
                return false;
            }

            return left.Equals(right);
        }
        public static bool operator !=(Vehicle left, Vehicle right)
        {
            return !(left == right);
        }
        public bool Equals(Vehicle other) => other != null && Raw.SequenceEqual(other.Raw);

        public override int GetHashCode()
        {
            int hash = 19; // A prime number as a starting seed
            foreach (var item in Raw)
            {
                hash = hash * 31 + (item?.GetHashCode() ?? 0);
            }
            return hash;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Vehicle);
        }
    }

    internal struct VehicleFile
    {
        private const string Separator = "\\";

        string Value { get; set; }


        internal static VehicleFile Of(string value)
        {
            if (value.Equals(default))
            {
                throw new ArgumentNullException(nameof(value), "Value must not be null!");
            }
            if (value == "")
            {
                throw new ArgumentNullException(nameof(value), "Value must not be empty!");
            }
            return new VehicleFile() { Value = RemoveNoise(value) };
        }

        private static string RemoveNoise(string value)
        {
            var splits = value.Split(Separator.First());
            var skip = Array.FindIndex(splits, p => p.Equals("Vehicles", StringComparison.OrdinalIgnoreCase));
            return string.Join(Separator, splits.Skip(skip + 1));
        }

        internal bool MatchesExact(VehicleFile other)
        {
            return Value.Equals(other.Value);
        }

        internal bool IsSimilar(VehicleFile other, bool excludeVersion = true)
        {
            var thisDistinctParts = DistinctParts(excludeVersion);
            var otherDistinctParts = other.DistinctParts(excludeVersion);

            return thisDistinctParts.Equals(otherDistinctParts, StringComparison.OrdinalIgnoreCase);
        }

        private string DistinctParts(bool excludeVersion)
        {
            var splits = Value.Split(Separator.First());
            var parts = splits.Length;
            var take = parts - 1 /* skip last */  - (excludeVersion ? 1 : 0);
            return string.Join(Separator, splits.Take(Math.Max(1, take)));
        }

        public override string ToString() => $"{{{nameof(VehicleFile)}{{{Value}}}}}";
    }


    internal class UniqueStack<T>
    {
        private readonly LinkedList<T> _items = new LinkedList<T>();

        /// <summary>
        /// Add an item at top top of the queue.
        /// 
        /// </summary>
        /// <param name="item"> to push.</param>
        /// <returns>Returns true if item was added, false if already existed and just repushed.</returns>
        internal bool Repush(T item)
        {
            var requeud = _items.Remove(item);
            _items.AddLast(item);
            return !requeud;
        }

        public bool TryPop(out T item)
        {
            if (_items.Count == 0)
            {
                item = default;
                return false;
            }

            item = _items.Last.Value;
            _items.RemoveLast();
            return true;
        }

        internal int Count()
        {
            return _items.Count;
        }
    }

    internal class Rf2AllVehicles
    {
        private const int MAX_BACKUPS = 10;
        private readonly ILog _logger;
        public readonly FilePath _playerJsonFile;
        private readonly FilePath _allVehiclesIniFile;
        private readonly UniqueStack<VehicleFile> queue = new UniqueStack<VehicleFile>();
        private static readonly string[] fieldNames = new string[] { "FOV", "Seat", "SeatPitch", "RearViewSize", "Mirror", "MirrorPhysical", "MirrorLeft", "MirrorCenter", "MirrorRight", "FFBSteeringTorqueMult" };

        internal Rf2AllVehicles(ILog logger, System.Diagnostics.Process proc)
        {
            _logger = logger;

            var rF2Home = new FilePath(proc.GetMainModuleFileName()).Directory.Parent;
            _playerJsonFile = new FilePath(rF2Home, @"UserData\player\player.JSON");
            _allVehiclesIniFile = new FilePath(rF2Home, @"UserData\player\all_vehicles.ini");

            logger.Info($"[{nameof(RF2AllVehiclesPlugin)}] Will update '{_allVehiclesIniFile}' when {proc.ProcessName} ends. Vehicles are added for processing by tracking changes in '{_playerJsonFile}'.");
        }

        internal void EnqueueVehicle()
        {
            using (var reader = new JsonTextReader(new StreamReader(_playerJsonFile.FullName)))
            {
                while (reader.Read())
                {
                    if (reader.Path == "DRIVER['Vehicle File']")
                    {
                        var vehicleFile = VehicleFile.Of(reader.ReadAsString());
                        if (queue.Repush(vehicleFile))
                        {
                            _logger.Info($"[{nameof(RF2AllVehiclesPlugin)}] {vehicleFile} tracked.");
                        }
                        return;
                    }
                }
            }
            _logger.Info($"Jsonpath 'DRIVER.Vehicle File' missing in '{_playerJsonFile.FullName}'!");
        }

        internal void ProcessQueue()
        {
            if (queue.Count() == 0)
            {
                return;
            }
            var lines = File.ReadAllLines(_allVehiclesIniFile.FullName, Encoding.ASCII);
            var outLines = lines.Take(1).ToList();
            _logger.Info($"[{nameof(RF2AllVehiclesPlugin)}] {lines.Length} lines read from '{_allVehiclesIniFile}'.");

            var vehicles = LoadVehicles(lines);
            _logger.Info($"[{nameof(RF2AllVehiclesPlugin)}] {vehicles.Count} vehicles loaded.");

            var numberOfVehiclesUpdated = 0;
            while (queue.TryPop(out var vehicleFile))
            {
                numberOfVehiclesUpdated += CopyToSimilar(vehicleFile, vehicles);
            }
            if (numberOfVehiclesUpdated == 0)
            {
                _logger.Info($"[{nameof(RF2AllVehiclesPlugin)}] No vehicles needed update, skipping writing '{_allVehiclesIniFile}'.");
                return;
            }
            outLines.AddRange(vehicles.SelectMany(v => v.Raw));

            Write(outLines, numberOfVehiclesUpdated);
        }

        private void Write(List<string> outLines, int numberOfVehiclesUpdated)
        {
            try
            {
                BackupRotate();
                File.WriteAllLines(_allVehiclesIniFile.FullName, outLines, Encoding.ASCII);
                _logger.Info($"[{nameof(RF2AllVehiclesPlugin)}] {numberOfVehiclesUpdated} vehicles updated - {outLines.Count} lines written to '{_allVehiclesIniFile}'.");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{nameof(RF2AllVehiclesPlugin)}] Unable to write '{_allVehiclesIniFile}': {ex}");
            }
        }

        private void BackupRotate()
        {
            var matcher = new Regex(Regex.Escape(_allVehiclesIniFile.Name) + @"\.(\d+)$");
            var backupFiles = _allVehiclesIniFile.Directory.ToDirectoryInfo().GetFiles(_allVehiclesIniFile.Name + ".*")
                                     .Where(f => matcher.IsMatch(f.Name))
                                     .OrderBy(f => int.Parse(matcher.Match(f.Name).Groups[1].Value))
                                     .ToList();
            backupFiles.Skip(MAX_BACKUPS - 1).ToList().ForEach(f =>
            {
                try
                {
                    f.Delete();
                }
                catch (Exception ex)
                {
                    _logger.Error($"[{nameof(RF2AllVehiclesPlugin)}] Unable to delete old backup '{f.FullName}': {ex}");
                }
            });
            backupFiles.Take(MAX_BACKUPS - 1).Reverse().ToList().ForEach(f =>
            {
                var match = matcher.Match(f.Name);
                if (match.Success && int.TryParse(match.Groups[1].Value, out int num))
                {
                    var newNum = num + 1;
                    var newName = _allVehiclesIniFile.Name + "." + newNum;
                    var newPath = Path.Combine(_allVehiclesIniFile.Directory, newName);
                    try
                    {
                        if (File.Exists(newPath))
                        {
                            File.Delete(newPath);
                        }
                        f.MoveTo(newPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[{nameof(RF2AllVehiclesPlugin)}] Unable to rotate '{f.FullName}' to '{newPath}': {ex}");
                    }
                }
            });
            _allVehiclesIniFile.ToFileInfo().MoveTo(_allVehiclesIniFile.FullName +".1");
        }

        private int CopyToSimilar(VehicleFile fromVehicleFile, List<Vehicle> toAllVehicles)
        {
            var fromVehicle = toAllVehicles.FirstOrDefault(veh => veh.VehicleFile.Equals(fromVehicleFile));
            if (fromVehicle == default)
            {
                _logger.Error($"[{nameof(RF2AllVehiclesPlugin)}]: Unable to locate Vehicle matching {fromVehicleFile}.");
                return 0;
            }

            var similar = toAllVehicles
                .Where(predicate: veh => veh.ID != fromVehicle.ID)
                .Where(predicate: veh => fromVehicle.VehicleFile.IsSimilar(veh.VehicleFile));

            var touched = similar
                .Select(s => s.WithOther(fromVehicle, fieldNames));

            var updated = touched
                .Where(predicate: t =>
                {
                    var u = similar.First(s => t.ID == s.ID && t.VehicleFile.Equals(s.VehicleFile));
                    return !t.Equals(u);
                })
                .ToList();

            updated.ToList()
                .ForEach(upd =>
                {
                    var index = toAllVehicles.FindIndex(v => v.ID == upd.ID);
                    if (index == -1)
                    {
                        _logger.Error($"[{nameof(RF2AllVehiclesPlugin)}] Unable to find index for updated {upd}");
                    }
                    else
                    {
                        toAllVehicles[index] = upd;

                    }
                });
            _logger.Info($"[{nameof(RF2AllVehiclesPlugin)}] {fromVehicle.VehicleFile} {fromVehicle} => {similar.FormatListForLog(20)} similar vehicles: {updated.FormatListForLog(20)} updated.");
            return updated.Count();
        }

        private List<Vehicle> LoadVehicles(string[] lines)
        {
            var vehicles = new List<Vehicle>();
            var beginRow = -1;
            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if ("[VEHICLE]".Equals(line))
                {
                    if (beginRow != -1)
                    {
                        var endRow = i - 1;
                        vehicles.Add(Vehicle.From(lines.Skip(beginRow).Take(endRow - beginRow + 1)));
                    }
                    beginRow = i;
                }
            }
            if (beginRow != -1)
            {
                vehicles.Add(Vehicle.From(lines.Skip(beginRow)));
            }

            var neededBeginning = _allVehiclesIniFile.Directory.Parts();

            return vehicles
                .Where(v => v.File.DirectoryMissing || v.IsInstalled)
                .ToList();
        }
    }

}
