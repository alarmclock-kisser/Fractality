
using ManagedCuda;
using ManagedCuda.BasicTypes;
using System.Runtime.InteropServices;

namespace AcceleratedAudio.Cuda
{
	public class CudaMemoryHandling
	{
		// ----- ----- ATTRIBUTES ----- ----- \\
		private string Repopath;
		private PrimaryContext Context;

		public List<CudaMem> Buffers = [];


		// ----- ----- CONSTRUCTORS ----- ----- \\
		public CudaMemoryHandling(string repopath, PrimaryContext context)
		{
			this.Repopath = repopath;
			this.Context = context;
		}





		// ----- ----- METHODS ----- ----- \\
		public string Log(string message = "", string inner = "", int indent = 0)
		{
			string indentString = new string('~', indent);
			string logMessage = $"[Mem] {indentString}{message} ({inner})";
			Console.WriteLine(logMessage);
			return logMessage;
		}




		public void Dispose()
		{
			// Free buffers
			foreach (CudaMem buffer in this.Buffers)
			{
				foreach (IntPtr pointer in buffer.Pointers)
				{
					this.FreeBuffer(pointer);
				}
			}

			this.Buffers.Clear();
			GC.Collect();
		}

		public CudaMem? GetBuffer(IntPtr pointer)
		{
			// Find buffer obj by pointer
			CudaMem? obj = this.Buffers.FirstOrDefault(x => x.IndexPointer == pointer);
			if (obj == null)
			{
				// Log
				this.Log($"Couldn't find", "<" + pointer + ">", 1);
				
				return null;
			}

			return obj;
		}

		public long FreeBuffer(IntPtr pointer, bool readable = false)
		{
			// Get buffer
			CudaMem? obj = this.GetBuffer(pointer);
			if (obj == null)
			{
				return 0;
			}

			// Get size
			long size = this.GetBufferSize(pointer, readable);

			// Get device ptr
			CUdeviceptr ptr = new CUdeviceptr(pointer);

			// Free buffer
			this.Context.FreeMemory(ptr);

			// Remove from dict
			this.Buffers.Remove(obj);

			return size;
		}

		public Type GetBufferType(IntPtr pointer)
		{
			Type defaultType = typeof(void);

			// Get buffer
			CudaMem? obj = this.GetBuffer(pointer);
			if (obj == null)
			{
				return defaultType;
			}

			return obj.Type;
		}

		public long GetBufferSize(IntPtr pointer, bool readable = false)
		{
			// Get buffer
			CudaMem? obj = this.GetBuffer(pointer);
			if (obj == null)
			{
				return 0;
			}

			// Get length in bytes
			long length = (long) obj.Size;

			// Make readable
			if (readable)
			{
				length /= 1024 * 1024;
			}

			return length;
		}

		public IntPtr PushData<T>(IEnumerable<T> data, bool silent = false) where T : unmanaged
		{
			// Check data
			if (data == null || !data.Any())
			{
				if (!silent)
				{
					this.Log("No data to push", "", 1);
				}
				return 0;
			}

			// Get length pointer
			IntPtr length = (nint) data.LongCount();

			// Allocate buffer & copy data
			CudaDeviceVariable<T> buffer = new(length);
			buffer.CopyToDevice(data.ToArray());

			// Get pointer
			IntPtr pointer = buffer.DevicePointer.Pointer;

			// Log
			if (!silent)
			{
				this.Log($"Pushed {length / 1024} kB", "<" + pointer + ">", 1);
			}

			// Create obj
			CudaMem obj = new(
				pointer: pointer,
				length: length,
				type: typeof(T)
			);

			// Add to dict
			this.Buffers.Add(obj);

			// Return pointer
			return pointer;
		}

		public T[] PullData<T>(IntPtr pointer, bool free = false, bool silent = false) where T : unmanaged
		{
			// Get buffer
			CudaMem? obj = this.GetBuffer(pointer);
			if (obj == null || obj.Count == 0)
			{
				return [];
			}

			// Create array with long count
			T[] data = new T[obj.TotalLength];

			// Get device pointer
			CUdeviceptr ptr = new(pointer);

			// Copy data to host from device pointer
			this.Context.CopyToHost(data, ptr);

			// Log
			if (!silent)
			{
				this.Log($"Pulled {obj.Size / 1024} kB", "<" + pointer + ">", 1);
			}

			// Free buffer
			if (free)
			{
				this.FreeBuffer(pointer);
			}

			// Return data
			return data;
		}

		public CudaMem? PushChunks<T>(IEnumerable<T[]> chunks, bool silent = false) where T : unmanaged
		{
			// Check chunks
			if (chunks == null || !chunks.Any())
			{
				if (!silent)
				{
					this.Log("No chunks to push", "", 1);
				}
				return null;
			}
			
			// Get lengths
			IntPtr[] lengths = chunks.Select(chunk => (nint) chunk.LongCount()).ToArray();
			
			// Allocate buffer
			IntPtr pointer = this.AllocateBuffer<T>(lengths, silent);
			
			// Get device pointer
			CUdeviceptr devPtr = new(pointer);
			
			// Copy data to device
			int i = 0;
			foreach (T[] chunk in chunks)
			{
				CudaDeviceVariable<T> buffer = new(lengths[i]);
				buffer.CopyToDevice(chunk);
				this.Context.CopyToDevice(buffer.DevicePointer, devPtr + i * Marshal.SizeOf<T>());
				i++;
			}

			// Create obj
			CudaMem obj = new(
				pointers: [devPtr.Pointer],
				lengths: lengths,
				type: typeof(T));
			
			// Add to dict
			this.Buffers.Add(obj);
			return obj;
		}

		public IEnumerable<T[]> PullChunks<T>(IntPtr pointer, bool free = false, bool silent = false) where T : unmanaged
		{
			// Get buffer
			CudaMem? obj = this.GetBuffer(pointer);
			if (obj == null || obj.Count == 0)
			{
				return [];
			}

			// Get device pointer
			CUdeviceptr[] ptrs = obj.Pointers
				.Select(p => new CUdeviceptr(p))
				.ToArray();

			// Create array for chunks
			T[][] chunks = new T[obj.Count][];
			
			// Pull data from device
			for (int i = 0; i < obj.Count; i++)
			{
				IntPtr length = obj.Lengths[i];
				T[] chunk = new T[length.ToInt64()];
				this.Context.CopyToHost(chunk, ptrs[i]);
				chunks[i] = chunk;
			}
			
			// Log
			if (!silent)
			{
				this.Log($"Pulled {obj.Size / 1024} kB", "<" + pointer + ">", 1);
			}

			// Free buffer if requested
			if (free)
			{
				this.FreeBuffer(pointer);
			}

			return chunks;
		}

		public IntPtr AllocateBuffer<T>(IntPtr[] lengths, bool silent = false) where T : unmanaged
		{
			// Allocate buffer
			CudaDeviceVariable<T>[] buffers = lengths
				.Select(length => new CudaDeviceVariable<T>(length))
				.ToArray();
			
			// Create obj
			CudaMem obj = new(
				buffers.Select(b => (nint) b.DevicePointer.Pointer).ToArray(),
				lengths,
				typeof(T));

			// Log
			if (!silent)
			{
				this.Log($"Allocated {obj.Size / 1024} kB", "<" + obj.IndexPointer + ">", 1);
			}

			// Add to dict
			this.Buffers.Add(obj);

			return (nint) obj.IndexPointer;
		}

		public long GetTotalMemoryUsage(bool actual = false, bool asMegabytes = false)
		{
			// Sum up all buffer sizes * sizeof(type)
			long totalSize = this.Buffers.Sum(x => x.Size);

			// Get total memory
			long totalAvailable = this.GetTotalMemory() - this.Context.GetFreeDeviceMemorySize();
			if (actual)
			{
				totalSize = totalAvailable;
			}

			// Convert to megabytes
			if (asMegabytes)
			{
				totalSize /= 1024 * 1024;
			}

			return totalSize;
		}

		public long GetTotalMemory(bool asMegabytes = false)
		{
			// Get total memory
			long totalSize = this.Context.GetTotalDeviceMemorySize();
			
			// Convert to megabytes
			if (asMegabytes)
			{
				totalSize /= 1024 * 1024;
			}

			return totalSize;
		}
	}



	public class CudaMem
	{
		// ----- ----- ATTRIBUTES ----- ----- \\
		public IntPtr[] Pointers { get; set; } = [];
		public IntPtr[] Lengths { get; set; } = [];
		public Type Type { get; set; } = typeof(void);

		public long IndexPointer => this.Pointers.Length > 0 ? this.Pointers[0].ToInt64() : 0;
		public long IndexLength => this.Lengths.Length > 0 ? this.Lengths[0].ToInt64() : 0;


		public IntPtr TotalLength => this.Lengths.Length > 0 ? (nint) this.Lengths.Sum(l => l) : IntPtr.Zero;
		public long Size => this.Lengths.Sum(l => (long) l * Marshal.SizeOf(this.Type));
		public long Count => this.Lengths.LongLength == this.Pointers.LongLength ? this.Lengths.LongLength : 0;



		public CudaMem(IntPtr[] pointers, IntPtr[] lengths, Type type)
		{
			this.Pointers = pointers;
			this.Lengths = lengths;
			this.Type = type;
		}

		public CudaMem(IntPtr pointer, IntPtr length, Type type)
		{
			this.Pointers = [pointer];
			this.Lengths = [length];
			this.Type = type;
		}



	}
}