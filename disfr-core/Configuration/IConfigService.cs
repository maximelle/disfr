namespace disfr.Configuration
{
    public interface IConfigService
    {
        bool Save();

        bool QuickFilter { get; set; }
        bool ShowSpecials { get; set; }
        bool ShowLocalSerial { get; set; }
        bool ShowLongAssetName { get; set; }

        bool ShowAll { get; set; }
        int TagShowing { get; set; }

        bool ShowChanges { get; set; }

        void AddColumn(ColumnInfo column);
        ColumnInfo GetColumn(string id);

        FontConfiguration Font { get; set; }
    }
}
