using Fractality.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fractality.Shared
{
	public class AudioData
	{
		public Guid Guid { get; set; } = Guid.Empty;
		public string Filepath { get; set; } = string.Empty;

		public float[] Data = [];
		public int Samplerate = 0;
		public int Channels = 0;
		public int Bitdepth = 0;
		public string Format = string.Empty;
		public long Length { get; set; } = 0;

		public long Pointer { get; set; } = 0;

		public string WaveformBase64 { get; set; } = string.Empty;
		public int WaveformWidth { get; set; } = 0;
		public int WaveformHeight { get; set; } = 0;


		public AudioData(AudioObj? obj)
		{
			if (obj == null)
			{
				return;
			}

			this.Guid = obj.Id;
			this.Filepath = obj.Filepath;
			this.Data = obj.Data;
			this.Samplerate = obj.Samplerate;
			this.Channels = obj.Channels;
			this.Bitdepth = obj.Bitdepth;
			this.Format = Path.GetExtension(this.Filepath) ?? ".raw";

			this.Length = obj.Data.LongLength;
			this.Pointer = obj.Pointer;

			this.WaveformBase64 = obj.WaveformBase64;
			this.WaveformWidth = obj.WaveformSize.Width;
			this.WaveformHeight = obj.WaveformSize.Height;
		}
	}
}
