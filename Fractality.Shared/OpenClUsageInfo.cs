using Fractality.OpenCl;

namespace Fractality.Shared
{
	public class OpenClUsageInfo
	{
		public long TotalMemory { get; set; }
		public long UsedMemory { get; set; }
		public long FreeMemory { get; set; }
		public float UsagePercentage { get; set; }

		public OpenClUsageInfo(OpenClMemoryRegister? register)
		{
            if (register == null)
            {
				return;
            }

			this.TotalMemory = register.GetMemoryTotal();
			this.UsedMemory = register.GetMemoryUsed();
			this.FreeMemory = register.GetMemoryFree();
			this.UsagePercentage = this.TotalMemory > 0 ? (float)this.UsedMemory / this.TotalMemory * 100 : 0f;
		}

	}
}
