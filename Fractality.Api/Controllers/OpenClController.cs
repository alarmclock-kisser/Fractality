using Fractality.Core;
using Fractality.OpenCl;
using Fractality.Shared;
using Microsoft.AspNetCore.Mvc;
using OpenFractality.Shared;
using OpenTK.Compute.OpenCL;
using TextCopy;

namespace Fractality.Api.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class OpenClController : ControllerBase
	{
		private readonly OpenClService openClService;
		private readonly ImageCollection imageCollection;
		private readonly AudioCollection audioCollection;

		private readonly IClipboard clipboard;

        public OpenClController(OpenClService openClService, ImageCollection imageCollection, AudioCollection audioCollection, IClipboard clipboard)
		{
			this.openClService = openClService;
			this.imageCollection = imageCollection;
			this.audioCollection = audioCollection;
			this.clipboard = clipboard;
		}

		[HttpGet("devices")]
		[ProducesResponseType(typeof(IEnumerable<OpenClDeviceInfo>), 200)]
		[ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<IEnumerable<OpenClDeviceInfo>>> GetDevices()
		{
			try
			{
				var infos = await Task.Run(() =>
				{
					return this.openClService.GetDevices()
						.Select((device, index) => new OpenClDeviceInfo(this.openClService, index))
						.ToList();
				});

				if (infos.Count == 0)
				{
					return this.NotFound("No OpenCL devices found.");
				}

				return this.Ok(infos);
			}
			catch (Exception ex)
			{
				return this.StatusCode(500, $"Internal server error: {ex.Message}");
			}
		}

		[HttpPost("initialize/{deviceId}")]
        [ProducesResponseType(typeof(OpenClServiceInfo), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<OpenClServiceInfo>> Initialize(int deviceId = 2)
		{
			try
			{
				if (deviceId < 0 || deviceId >= this.openClService.Devices.Count)
				{
					return this.BadRequest("Invalid device ID.");
				}

				await Task.Run(() => this.openClService.Initialize(deviceId));

				var info = new OpenClServiceInfo(this.openClService);
				if (!info.Initialized)
				{
					return this.NotFound("Failed to initialize OpenCL service. Device might not be available.");
				}

				return this.Ok(info);
			}
			catch (Exception ex)
			{
				return this.StatusCode(500, $"Internal server error: {ex.Message}");
			}
		}

		[HttpDelete("dispose")]
        [ProducesResponseType(typeof(OpenClServiceInfo), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<OpenClServiceInfo>> Dispose()
		{
			try
			{
				await Task.Run(() => this.openClService.Dispose());

				var info = new OpenClServiceInfo(this.openClService);
				if (info.Initialized)
				{
					return this.BadRequest("OpenCL service is still initialized. Please ensure it is properly disposed.");
				}

				return this.Ok(info);
			}
			catch (Exception ex)
			{
				return this.StatusCode(500, $"Internal server error: {ex.Message}");
			}
		}

		[HttpGet("status")]
        [ProducesResponseType(typeof(OpenClServiceInfo), 200)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<OpenClServiceInfo>> GetStatus()
		{
			try
			{
				var info = await Task.Run(() => new OpenClServiceInfo(this.openClService));
				
				return this.Ok(info);
			}
			catch (Exception ex)
			{
				return this.StatusCode(500, $"Internal server error: {ex.Message}");
			}
		}

		[HttpGet("usage")]
        [ProducesResponseType(typeof(OpenClUsageInfo), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<OpenClUsageInfo>> GetUsage()
		{
			if (this.openClService.MemoryRegister == null)
			{
				return this.NotFound("OpenCL memory register is not initialized.");
			}

			try
			{
				var info = await Task.Run(() => new OpenClUsageInfo(this.openClService.MemoryRegister));

				return this.Ok(info);
			}
			catch (Exception ex)
			{
				return this.StatusCode(500, $"Internal server error: {ex.Message}");
			}
		}

		[HttpGet("memory")]
        [ProducesResponseType(typeof(IEnumerable<OpenClMemoryInfo>), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(204)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<IEnumerable<OpenClMemoryInfo>>> GetMemoryObjects()
		{
			if (this.openClService.MemoryRegister == null)
			{
				return this.NotFound("OpenCL memory register is not initialized.");
			}

			try
			{
				var infos = await Task.Run(() => this.openClService.MemoryRegister.Memory
					.Select((memory, index) => new OpenClMemoryInfo(memory)));

				if (infos.LongCount() == 0)
				{
					return this.NoContent();
				}

				return this.Ok(infos);
			}
			catch (Exception ex)
			{
				return this.StatusCode(500, $"Internal server error: {ex.Message}");
			}
		}

		[HttpGet("kernels")]
        [ProducesResponseType(typeof(IEnumerable<OpenClKernelInfo>), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(204)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<IEnumerable<OpenClKernelInfo>>> GetKernels()
		{
			if (this.openClService.KernelCompiler == null)
			{
				return this.NotFound("OpenCL memory register is not initialized.");
			}
			try
			{
				var infos = await Task.Run(() => this.openClService.KernelCompiler.Files
					.Select((file, index) => new OpenClKernelInfo(this.openClService.KernelCompiler, index))
					.ToList());

				if (infos.Count == 0)
				{
					return this.NoContent();
				}

				return this.Ok(infos);
			}
			catch (Exception ex)
			{
				return this.StatusCode(500, $"Internal server error: {ex.Message}");
			}
		}

		[HttpGet("executeImage/{kernel}/{version}/{width}/{height}/{zoom}/{x}/{y}/{coeff}/{r}/{g}/{b}/{copyGuid}/{allowTempSession}")]
        [ProducesResponseType(typeof(ImageObjInfo), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<ImageObjInfo>> ExecuteMandelbrot(string kernel = "mandelbrotPrecise", string version = "01", int width = 1920, int height = 1080, double zoom = 1.0, double x = 0.0, double y = 0.0, int coeff = 8, int r = 0, int g = 0, int b = 0, bool copyGuid = true, bool allowTempSession = true)
		{
            bool temp = false;

            try
            {
                // Get status
                var status = await Task.Run(() => new OpenClServiceInfo(this.openClService));
                if (!status.Initialized)
                {
                    if (allowTempSession)
                    {
						int count = this.openClService.Devices.Count;
						await Task.Run(() => this.openClService.Initialize(count - 1));
                        status = await Task.Run(() => new OpenClServiceInfo(this.openClService));
                    }

                    if (!status.Initialized)
                    {
                        return this.BadRequest("OpenCL service is not initialized. Please initialize it first.");
                    }
                }

                // Create an empty image
                var obj = await Task.Run(() => this.imageCollection.PopEmpty(new(width, height)));
                if (obj == null || !this.imageCollection.Images.Contains(obj))
                {
                    return this.NotFound("Failed to create empty image or couldnt add it to the collection.");
                }

                // Build variable arguments
                object[] variableArgs =
                    [
                        0, 0,
                        width, height,
                        zoom, x, y,
                        coeff,
                        r, g, b
                    ];

                // Call service accessor
                var result = await Task.Run(() =>
                    this.openClService.ExecuteImageKernel(obj, kernel, version, variableArgs));

                // Get image obj info
                var info = await Task.Run(() => new ImageObjInfo(obj));
                if (!info.OnHost)
                {
                    return this.NotFound(
                        "Failed to execute OpenCL kernel or image is not on the host after execution call.");
                }

                // Optionally copy guid to clipboard
                if (copyGuid)
                {
                    await this.clipboard.SetTextAsync(info.Guid.ToString());
                }

                return this.Ok(info);
            }
            catch (Exception ex)
            {
                return this.StatusCode(500, $"Internal server error: {ex.Message}");
            }
            finally
            {
                if (temp)
                {
                    await Task.Run(() => this.openClService.Dispose());
                }
            }
		}

		[HttpGet("executeAudio/{kernel}/{version}/{factor}/{chunkSize}/{overlap}/{copyGuid}/{allowTempSession}")]
		[ProducesResponseType(typeof(AudioObjInfo), 200)]
		[ProducesResponseType(204)]
		[ProducesResponseType(404)]
		[ProducesResponseType(400)]
		[ProducesResponseType(500)]
		public async Task<ActionResult<AudioObjInfo>> ExecuteTimestretch(Guid guid, string kernel = "timestretch_double", string version = "03", double factor = 0.75d, int chunkSize = 16384, float overlap = 0.5f,  bool copyGuid = true, bool allowTempSession = true)
		{
			bool temp = false;
			var result = new AudioObjInfo(null);

			try
			{
				// Get status
				var status = await Task.Run(() => new OpenClServiceInfo(this.openClService));
				if (!status.Initialized)
				{
					if (allowTempSession)
					{
						// Initialize last device (mostly CPU)
						int count = this.openClService.Devices.Count;
						await Task.Run(() => this.openClService.Initialize(count - 1));
						status = await Task.Run(() => new OpenClServiceInfo(this.openClService));
					}

					if (!status.Initialized)
					{
						return this.BadRequest("OpenCL service is not initialized. Please initialize it first.");
					}
				}

				// Find audio object by guid
				var obj = await Task.Run(() => this.audioCollection[guid]);
				if (obj == null || !this.audioCollection.Tracks.Contains(obj) || obj.Id == Guid.Empty)
				{
					return this.NotFound($"No audio found with Guid '{guid}'");
				}

				// Build optional arguments
				Dictionary<string, object> optionalArgs = [];

				// Get kernel factor type by name -> float or double
				if (kernel.Contains("double", StringComparison.OrdinalIgnoreCase))
				{
					optionalArgs.Add("factor", factor);
				}
				else
				{
					optionalArgs.Add("factor", (float)factor);
				}

				// Call service accessor
				var pointer = await Task.Run(() =>
				{
					return this.openClService.ExecuteAudioKernel(obj, kernel, version, chunkSize, overlap, optionalArgs, true);
				});

				// Get audio obj info
				var info = await Task.Run(() => new AudioObjInfo(obj));
				if (!info.OnHost)
				{
					return this.NoContent();
				}

				// Optionally copy guid to clipboard
				if (copyGuid)
				{
					await this.clipboard.SetTextAsync(info.Guid.ToString());
				}

				return this.Ok(info);
			}
			catch (Exception ex)
			{
				return this.StatusCode(500, $"Internal server error: {ex.Message}");
			}
			finally
			{
				if (temp)
				{
					await Task.Run(() => this.openClService.Dispose());
				}
			}
		}

	}
}
