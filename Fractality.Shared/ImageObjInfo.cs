using Fractality.Core;

namespace Fractality.Shared
{
    public class ImageObjInfo
    {
        public Guid Guid { get; set; } = Guid.Empty;
        public string Name { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public int Height { get; set; } = 0;
        public int Width { get; set; } = 0;
        public int Bitdepth { get; set; } = 0;
        public int Channels { get; set; } = 0;
        public long SizeInBytes { get; set; } = 0;
        public long Pointer { get; set; } = 0;
        public bool OnHost { get; set; } = false;


        public string Entry { get; set; } = string.Empty;

        public int ProcessingTime { get; set; } = -1;


        public ImageObjInfo(ImageObj? obj)
        {
            if (obj == null)
            {
                return;
            }

            this.Guid = obj.Id;
            this.Name = obj.Name;
            this.FilePath = obj.FilePath;
            this.Height = obj.Height;
            this.Width = obj.Width;
            this.Bitdepth = obj.Bitdepth;
            this.Channels = obj.Channels;
            this.SizeInBytes = obj.SizeInBytes;
            this.Pointer = obj.Pointer;
            this.OnHost = obj.OnHost;

            this.Entry = $"'{this.Name}' ({this.Width}x{this.Height}, {(this.SizeInBytes / 1024)} kB) <{(this.Pointer != 0 ? this.Pointer : "")}>";
        }
    }
}
