using Fractality.OpenCl;

namespace Fractality.Shared
{
	public class OpenClDeviceInfo
	{
		public int DeviceId { get; set; } = -1;
		public string DeviceName { get; set; } = string.Empty;
		public string DeviceType { get; set; } = string.Empty;
		public string PlatformName { get; set; } = string.Empty;

		public OpenClDeviceInfo(OpenClService? service, int index = -1)
        {
            this.DeviceId = index;

            if (service == null)
            {
                return;
            }

            if (index < 0)
            {
                index = service.INDEX;
                this.DeviceId = index;
            }

			var device = service.Devices.ElementAtOrDefault(index).Key;
			var platform = service.Devices.ElementAtOrDefault(index).Value;

			this.DeviceId = index;
			this.DeviceName = service.GetDeviceInfo(device, OpenTK.Compute.OpenCL.DeviceInfo.Name) ?? "N/A";
			this.DeviceType = service.GetDeviceInfo(device, OpenTK.Compute.OpenCL.DeviceInfo.Type)?.ToString() ?? "N/A";
			this.PlatformName = service.GetPlatformInfo(platform, OpenTK.Compute.OpenCL.PlatformInfo.Name) ?? "N/A";
		}
	}
}
