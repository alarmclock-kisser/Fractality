namespace Fractality.Shared
{
    public class ImageData
    {
        public string Base64 { get; set; } = string.Empty;
        public int Width { get; set; } = 0;
        public int Height { get; set; } = 0;

        public ImageData(string base64, int width = 0, int height = 0)
        {
            this.Base64 = base64;
            this.Width = width;
            this.Height = height;
        }
    }
}
