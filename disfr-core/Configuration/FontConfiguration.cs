using System;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;
using disfr.Extensions;
using WpfColorFontDialog;

namespace disfr.Configuration
{
    [Serializable]
    public class FontConfiguration
    {
        [XmlAttribute("size")]
        public double Size { get; set; }

        [XmlAttribute("color")]
        public string Color { get; set; }

        [XmlAttribute("family")]
        public string Family { get; set; }

        [XmlAttribute("style")]
        public int Style { get; set; }

        [XmlAttribute("weight")]
        public int Weight { get; set; }

        [XmlAttribute("stretch")]
        public int Stretch { get; set; }


        public FontInfo GetFontInfo()
        {
            
            FontInfo fontInfo = new FontInfo();

            fontInfo.Size = Size;
            if (string.IsNullOrEmpty(Color) == false)
            {
                Color color = Color.FromHexString();
                fontInfo.BrushColor = new SolidColorBrush(color);
            }
            if (string.IsNullOrEmpty(Family) == false)
            {
                fontInfo.Family = new FontFamily(Family);
            }

            if (Weight >= 0)
            {
                fontInfo.Weight = FontWeight.FromOpenTypeWeight(Weight);
            }

            if (Style >= 0)
            {
                
            }

            if (Stretch >= 0)
            {
                fontInfo.Stretch = FontStretch.FromOpenTypeStretch(Stretch);
            }

            return fontInfo;
        }

        public static FontConfiguration GetFontConfiguration(FontInfo fontInfo)
        {
            FontConfiguration configuration = new FontConfiguration();
            configuration.Size = fontInfo.Size;
            configuration.Color = fontInfo.BrushColor.Color.ToHexString();
            configuration.Family = fontInfo.Family.Source;
            configuration.Weight = fontInfo.Weight.ToOpenTypeWeight();
            configuration.Stretch = fontInfo.Stretch.ToOpenTypeStretch();
            configuration.Style = fontInfo.Style.GetHashCode();
            return configuration; }

        
    }
}
