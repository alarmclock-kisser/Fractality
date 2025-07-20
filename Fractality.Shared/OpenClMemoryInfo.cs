using Fractality.OpenCl;
using System.Runtime.InteropServices;

namespace Fractality.Shared
{
	public class OpenClMemoryInfo
	{
		public List<long> Pointers { get; set; } = [];
		public List<long> Lengths { get; set; } = [];
		private Type DataType = typeof(object);

		public long IndexPointer { get; set; }
		public long IndexLength { get; set; }
		public long Count { get; set; }
		public long TotalLength { get; set; }

		public int DataTypeSize { get; set; }
		public string DataTypeName { get; set; } = string.Empty;
		public long TotalSize { get; set; }


		public OpenClMemoryInfo(ClMem? obj = null)
		{
            if (obj == null)
            {
				return;
            }

			this.Pointers = obj.Buffers.Select(b => (long) b).ToList();
			this.Lengths = obj.Lengths.Select(l => (long) l).ToList();
			this.DataType = obj.ElementType ?? typeof(object);

			this.IndexPointer = this.Pointers.FirstOrDefault();
			this.IndexLength = this.Lengths.FirstOrDefault();
			this.Count = this.Pointers.LongCount();
			this.TotalLength = this.Lengths.Sum();
			this.DataTypeSize = Marshal.SizeOf(this.DataType);
			this.DataTypeName = this.DataType.Name;
			this.TotalSize = this.Lengths.Sum(length => length * this.DataTypeSize);
		}
	}
}
