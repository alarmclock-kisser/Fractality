using Fractality.OpenCl;

namespace Fractality.Shared
{
	public class OpenClServiceInfo
	{
		public int DeviceId { get; set; } = -1;
		public string DeviceName { get; set; } = string.Empty;
		public string PlatformName { get; set; } = string.Empty;
		public bool Initialized { get; set; } = false;
		public string Status { get; set; } = "Disposed.";

		public OpenClServiceInfo(OpenClService? service)
		{
            if (service == null)
            {
				return;
            }

			this.Initialized = service.MemoryRegister != null && service.KernelCompiler != null && service.KernelExecutioner != null;
			if (!this.Initialized)
			{
				return;
			}

			var device = service.Devices.ElementAt(service.INDEX).Key;
			var platform = service.Devices.ElementAt(service.INDEX).Value;

			this.DeviceId = service.INDEX;
			this.DeviceName = service.GetDeviceInfo(device, OpenTK.Compute.OpenCL.DeviceInfo.Name) ?? "N/A";
			this.PlatformName = service.GetPlatformInfo(platform, OpenTK.Compute.OpenCL.PlatformInfo.Name) ?? "N/A";

			this.Status = this.Initialized ? $"Initialized [{this.DeviceId}]" : "Disposed.";
		}
	}
}
