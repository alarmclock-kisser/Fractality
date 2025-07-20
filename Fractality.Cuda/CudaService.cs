using ManagedCuda;
using ManagedCuda.BasicTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fractality.Cuda
{
	public class CudaService
	{
		// ~~~~~ ~~~~~ Attributes ~~~~~ ~~~~~
		public string Repopath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "AcceleratedAudio.Cuda");
		public int Index = -1;
		public CUdevice? Device = null;
		public PrimaryContext? Context = null;

		public CudaMemoryHandling? MemoryH;
		public CudaFourierHandling? FourierH;
		public CudaKernelHandling? KernelH;



		// ~~~~~ ~~~~~ Constructor ~~~~~ ~~~~~




		// ~~~~~ ~~~~~ Methods ~~~~~ ~~~~~
		public string Log(string message = "", string inner = "", int indent = 0)
		{
			string indentString = new string('~', indent);
			string logMessage = $"[Ctx] {indentString}{message} ({inner})";
			Console.WriteLine(logMessage);
			return logMessage;
		}

		public int GetDeviceCount()
		{
			int deviceCount = 0;

			try
			{
				deviceCount = CudaContext.GetDeviceCount();
			}
			catch (CudaException ex)
			{
				this.Log("Couldn't get device count", ex.Message, 1);
			}

			return deviceCount;
		}

		public List<CUdevice> GetDevices()
		{
			List<CUdevice> devices = [];
			int deviceCount = this.GetDeviceCount();

			for (int i = 0; i < deviceCount; i++)
			{
				// Trycatch
				try
				{
					CUdevice device = new(i);
					devices.Add(device);
				}
				catch (CudaException ex)
				{
					this.Log("Couldn't get device # " + i, ex.Message, 1);
				}
				catch (Exception ex)
				{
					this.Log("Couldn't get device # " + i, ex.Message, 1);
				}
				finally
				{
					if (devices.Count == 0)
					{
						this.Log("No devices found", "", 1);
					}
				}
			}

			return devices;
		}

		public Version GetCapability(int index = -1)
		{
			index = index == -1 ? this.Index : index;

			Version ver = new(0, 0);

			try
			{
				ver = CudaContext.GetDeviceComputeCapability(index);
			}
			catch (CudaException ex)
			{
				this.Log("Couldn't get device capability", ex.Message, 1);
			}

			return ver;
		}

		public int GetProcessorCount(int index = -1)
		{
			index = index == -1 ? this.Index : index;
			int count = 0;
			try
			{
				count = CudaContext.GetDeviceInfo(index).MultiProcessorCount;
			}
			catch (CudaException ex)
			{
				this.Log("Couldn't get device processor count", ex.Message, 1);
			}
			return count;
		}

		public string GetName(int index = -1)
		{
			index = index == -1 ? this.Index : index;

			string name = "N/A";

			if (index < 0 || index >= this.GetDeviceCount())
			{
				this.Log("Invalid device id", "Out of range");
				return name;
			}

			try
			{
				name = CudaContext.GetDeviceName(index);
			}
			catch (CudaException ex)
			{
				this.Log("Couldn't get device name", ex.Message, 1);
			}

			return name;
		}

		public void InitDevice(int index = -1)
		{
			this.Dispose();

			index = index == -1 ? this.Index : index;
			if (index < 0 || index >= this.GetDeviceCount())
			{
				this.Log("Invalid device id", "Out of range");
				return;
			}

			this.Index = index;
			this.Device = new CUdevice(index);
			this.Context = new PrimaryContext(this.Device.Value);
			this.Context.SetCurrent();
			this.MemoryH = new CudaMemoryHandling(this.Repopath, this.Context);
			this.FourierH = new CudaFourierHandling(this.Repopath, this.Context, this.MemoryH);
			this.KernelH = new CudaKernelHandling(this.Repopath, this.Context, this.MemoryH);

			this.Log($"Initialized #{index}", this.GetName().Split(' ').FirstOrDefault() ?? "N/A");

		}

		public void Dispose()
		{
			this.Context?.Dispose();
			this.Context = null;
			this.Device = null;
			this.MemoryH?.Dispose();
			this.MemoryH = null;
			this.FourierH?.Dispose();
			this.FourierH = null;
			this.KernelH?.Dispose();
			this.KernelH = null;
		}
	}
}
