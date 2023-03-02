using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml.Serialization;

namespace disfr.Configuration
{
    [Serializable]
    public class ColumnInfo
    {
        [XmlElement("id")]
        public string Id { get; set; }

        [XmlElement("is-visible")]
        public bool IsVisible { get; set; }
    }

    [XmlRoot("config")]
    public class SerializableConfig
    {
        [XmlElement("quick-filter")]
        public bool QuickFilter { get; set; }

        [XmlElement("show-specials")]
        public bool ShowSpecials { get; set; }

        [XmlElement("font")]
        public FontConfiguration Font { get; set; }

        [XmlElement("show-local-serial")]
        public bool ShowLocalSerial { get; set; }

        [XmlElement("show-long-asset")]
        public bool ShowLongAssetName { get; set; }

        [XmlElement("show-all")]
        public bool ShowAll { get; set; }

        [XmlElement("tag-showing")]
        public int TagShowing { get; set; }

        [XmlElement("show-changes")]
        public bool ShowChanges { get; set; }

        [XmlArray("columns")]
        [XmlArrayItem("column")]
        public List<ColumnInfo> Columns { get; set; }

        public static SerializableConfig Load(string filePath)
        {
            SerializableConfig restoredObject;
            switch (ObjectStateSaver.LoadObject<SerializableConfig>(filePath, out restoredObject))
            {
                case LoadState.IsNew:
                    restoredObject = new SerializableConfig()
                    {
                        QuickFilter = true,
                        ShowSpecials = true,
                        Columns = new List<ColumnInfo>()
                    };
                    restoredObject.Save(filePath);
                    break;
                case LoadState.Corrupt:
                    string destFileName = string.Format("{0}.{1:yyyyMMdd_HHmmsszz}", filePath, DateTime.Now);
                    try
                    {
                        File.Copy(filePath, destFileName, true);
                        Debug.WriteLine(string.Format("Config file {0} is corrupted, backup to {1}", filePath, destFileName));
                        goto case LoadState.IsNew;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(string.Format("Config file {0} is corrupted, backup to {1} failed: {2}", filePath, destFileName, ex));
                        goto case LoadState.IsNew;
                    }
            }
            return restoredObject;
        }

        public bool Save(string filePath)
        {
            return ObjectStateSaver.SaveObjectState<SerializableConfig>(filePath, this);
        }


    }
}
