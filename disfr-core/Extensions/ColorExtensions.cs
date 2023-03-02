using System.Windows.Media;

namespace disfr.Extensions
{
    public static class ColorExtensions
    {
        public static Color FromHexString(this string colorString)
        {
           return (Color)ColorConverter.ConvertFromString(colorString);
        }

        public static string ToHexString(this Color color)
        {
            byte[] bytes = new byte[3];
            bytes[0] = color.R;
            bytes[1] = color.G;
            bytes[2] = color.B;
            char[] chars = new char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                int b = bytes[i];
                chars[i * 2] = _hexDigits[b >> 4];
                chars[i * 2 + 1] = _hexDigits[b & 0xF];
            }
            return $"#{new string(chars)}";
        }

        #region -- Data Members --
        static char[] _hexDigits = {
            '0', '1', '2', '3', '4', '5', '6', '7',
            '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'};
        #endregion
    }
}
