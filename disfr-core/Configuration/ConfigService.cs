using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace disfr.Configuration
{
    public sealed class ConfigService : IConfigService
    {
        private SerializableConfig _config;

        private static readonly object _syncSingleton = new object();
        private static volatile ConfigService _coreObject;

        /// <summary></summary>
        public static ConfigService Instance
        {
            get
            {
                if (_coreObject == null)
                {
                    lock (_syncSingleton)
                    {
                        if (_coreObject == null)
                        {
                            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "disfr");
                            _coreObject = new ConfigService(path);
                        }
                    }
                }

                return _coreObject;
            }
        }


        private ConfigService(string configsDirectoryPath)
        {
            if (Directory.Exists(configsDirectoryPath) == false)
            {
                Directory.CreateDirectory(configsDirectoryPath);
            }
            
            ConfigFilePath = Path.Combine(configsDirectoryPath, "disfr-config.xml");
            _config = SerializableConfig.Load(ConfigFilePath);
        }

        public bool Save()
        {
            return _config.Save(ConfigFilePath);
        }

        public string ConfigFilePath { get; }

        public bool QuickFilter
        {
            get => _config.QuickFilter;
            set
            {
                if (_config.QuickFilter != value)
                {
                    _config.QuickFilter = value;
                    Save();
                }
            }
        }

        public bool ShowSpecials
        {
            get => _config.ShowSpecials;
            set
            {
                if (_config.ShowSpecials != value)
                {
                    _config.ShowSpecials = value;
                    Save();
                }
            }
        }

        public bool ShowLocalSerial
        {
            get => _config.ShowLocalSerial;
            set
            {
                if (_config.ShowLocalSerial != value)
                {
                    _config.ShowLocalSerial = value;
                    Save();
                }
            }
        }

        public bool ShowLongAssetName
        {
            get => _config.ShowLongAssetName;
            set
            {
                if (_config.ShowLongAssetName != value)
                {
                    _config.ShowLongAssetName = value;
                    Save();
                }
            }
        }

        public bool ShowAll
        {
            get => _config.ShowAll;
            set
            {
                if (_config.ShowAll != value)
                {
                    _config.ShowAll = value;
                    Save();
                }
            }
        }

        public int TagShowing
        {
            get => _config.TagShowing;
            set
            {
                if (_config.TagShowing != value)
                {
                    _config.TagShowing = value;
                    Save();
                }
            }
        }

        public bool ShowChanges
        {
            get => _config.ShowChanges;
            set
            {
                if (_config.ShowChanges != value)
                {
                    _config.ShowChanges = value;
                    Save();
                }
            }
        }

        public void AddColumn(ColumnInfo column)
        {
            if (_config.Columns == null)
            {
                _config.Columns = new List<ColumnInfo>();
            }

            var c = _config.Columns.FirstOrDefault(x => x.Id == column.Id);
            if (c != null)
            {
                c.IsVisible = column.IsVisible;
            }
            else
            {
                _config.Columns.Add(column);
            }

            Save();
        }

        public ColumnInfo GetColumn(string id)
        {
            if (_config.Columns == null)
            {
                _config.Columns = new List<ColumnInfo>();
                Save();
            }

            return _config.Columns.FirstOrDefault(x => x.Id == id);
        }

        public FontConfiguration Font
        {
            get => _config.Font;
            set
            {
                if (_config.Font != value)
                {
                    _config.Font = value;
                    Save();
                }
            }
        }
    }
}
