using Fractality.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Fractality.Client
{
    public class ApiClient
    {
        private readonly InternalClient internalClient;
        public ApiClient(HttpClient httpClient)
        {
            this.internalClient = new InternalClient(httpClient.BaseAddress?.ToString() ?? "https://localhost:44330/api", httpClient);
        }

        public async Task<IEnumerable<ImageObjInfo>> GetImagesAsync()
        {
            try
            {
                return await this.internalClient.ImagesAsync();
            }
            catch (ApiException ex) when (ex.StatusCode == (int)HttpStatusCode.NoContent)
            {
                return [];
            }
            catch (ApiException ex)
            {
                Console.WriteLine("Client: ApiException: " + ex);
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Client: Exception: " + ex);
                throw;
            }
        }

        public async Task<ImageObjInfo> UploadImageAsync(FileParameter file)
        {
            try
            {
                return await this.internalClient.UploadAsync(file);
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error uploading image:" + exception);
                return new ImageObjInfo(null);
            }
        }

        public async Task<bool> RemoveImageAsync(Guid guid)
        {
            try
            {
                return await this.internalClient.RemoveAsync(guid);
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error downloading image: " + exception);
                return false;
            }
        }

        public async Task<ImageData> GetBase64(Guid guid)
        {
            try
            {
                return await this.internalClient.Base64Async(guid);
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error getting base64 string: " + exception);
                return new ImageData(exception.Message + " (" + exception.InnerException + ").");
            }
        }

        public async Task<ICollection<OpenClDeviceInfo>> GetOpenClDevicesAsync()
        {
            try
            {
                return await this.internalClient.DevicesAsync() ;
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error getting OpenCL service info: " + exception);
                return [];
            }
		}

        public async Task<OpenClServiceInfo> GetOpenClServiceInfoAsync()
        {
            try
            {
                return await this.internalClient.StatusAsync();
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error getting OpenCL service info: " + exception);
                return new OpenClServiceInfo(null);
            }
		}

        public async Task<OpenClServiceInfo> InitializeOpenClServiceAsync(int deviceId = 2)
        {
            try
            {
                return await this.internalClient.InitializeAsync(deviceId);
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error initializing OpenCL service: " + exception);
                return new OpenClServiceInfo(null);
            }
		}

        public async Task<OpenClServiceInfo> DisposeOpenClAsync()
        {
            try
            {
                return await this.internalClient.DisposeAsync();
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error disposing OpenCL service: " + exception);
                return new OpenClServiceInfo(null);
            }
        }

        public async Task<ICollection<OpenClMemoryInfo>> GetOpenClMemoryInfosAsync()
        {
            try
            {
                return await this.internalClient.MemoryAsync();
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error resetting OpenCL service: " + exception);
                return [];
            }
		}

        public async Task<OpenClUsageInfo> GetOpenClUsageInfoAsync()
        {
            try
            {
                return await this.internalClient.UsageAsync();
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error resetting OpenCL service: " + exception);
                return new OpenClUsageInfo(null);
            }
		}

		public async Task<IEnumerable<OpenClKernelInfo>> GetOpenClKernelsAsync()
        {
            try
            {
                return await this.internalClient.KernelsAsync();
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error getting OpenCL kernels: " + exception);
                return [];
            }
        }

        public async Task<ImageObjInfo> ExecuteMandelbrotAsync(string kernel = "mandelbrotPrecise", string version = "01", int width = 1920, int height = 1080, double zoom = 1.0, double x = 0.0, double y = 0.0, int coeff = 8, int r = 0, int g = 0, int b = 0, bool copyGuid = true, bool allowTempSession = true)
        {
            try
            {
                return await this.internalClient.ExecuteImageAsync(kernel, version, width, height, zoom, x, y, coeff, r, g, b, copyGuid, allowTempSession);
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error executing Mandelbrot: " + exception);
                return new ImageObjInfo(null);
            }
		}
	}
}
