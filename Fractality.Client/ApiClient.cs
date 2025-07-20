using Fractality.Shared;
using OpenFractality.Shared;
using System.Diagnostics;
using System.Net;

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
            catch (ApiException ex) when (ex.StatusCode == (int) HttpStatusCode.NoContent)
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

        public async Task<ImageObjInfo> UploadImageAsync(FileParameter file, bool copy = false)
        {
            try
            {
                return await this.internalClient.Upload2Async(copy, file);
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
                return await this.internalClient.Remove2Async(guid);
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error removing image: " + exception);
                return false;
            }
        }

        public async Task<ImageData> GetBase64(Guid guid)
        {
            try
            {
                return await this.internalClient.Base642Async(guid);
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
                return await this.internalClient.DevicesAsync();
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

        public async Task<IEnumerable<OpenClKernelInfo>> GetOpenClKernelsAsync(string wildcard = "")
        {
            IEnumerable<OpenClKernelInfo> result = [];

            try
            {
                result = (await this.internalClient.KernelsAsync()).ToList().Where(k => string.IsNullOrEmpty(wildcard) || k.FunctionName.Contains(wildcard, StringComparison.OrdinalIgnoreCase));
				Console.WriteLine($"Found [{result.Count()}] kernels" + (string.IsNullOrEmpty(wildcard) ? "" : $" with wildcard '{wildcard}' matching"));
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error getting OpenCL kernels: " + exception);
            }

            return result;
        }

        public async Task<ImageObjInfo> ExecuteMandelbrotAsync(string kernel = "mandelbrotPrecise",
            string version = "01", int width = 1920, int height = 1080, double zoom = 1.0, double x = 0.0,
            double y = 0.0, int coeff = 8, int r = 0, int g = 0, int b = 0, bool copyGuid = true,
            bool allowTempSession = true)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            var obj = new ImageObjInfo(null);

            try
            {
                obj = await this.internalClient.ExecuteImageAsync(kernel, version, width, height, zoom, x, y, coeff,
                    r, g, b, copyGuid, allowTempSession);

                obj.ProcessingTime = (int) stopwatch.ElapsedMilliseconds;
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error executing Mandelbrot: " + exception);
            }
            finally
            {
                stopwatch.Stop();
            }

            return obj;
        }

        public async Task<IEnumerable<AudioObjInfo>> GetAudiosAsync()
        {
            try
            {
                return await this.internalClient.AudiosAsync();
            }
            catch (ApiException ex) when (ex.StatusCode == (int) HttpStatusCode.NoContent)
            {
                return [];
            }
            catch (ApiException ex)
            {
                Console.WriteLine("Client: ApiException: " + ex);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Client: Exception: " + ex);
            }

            return [];
        }

        public async Task<AudioObjInfo> UploadAudioAsync(FileParameter file, bool copyGuid = true)
        {
            try
            {
                return await this.internalClient.UploadAsync(copyGuid, file);
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error uploading audio: " + exception);
                return new AudioObjInfo(null);
            }
        }

        public async Task<bool> RemoveAudioAsync(Guid guid)
        {
            try
            {
                return await this.internalClient.RemoveAsync(guid);
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error removing audio: " + exception);
                return false;
            }
        }

        public async Task<AudioData> GetAudioBase64(Guid guid)
        {
            try
            {
                return await this.internalClient.Base64Async(guid);
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error getting audio base64 string: " + exception);
                var result = new AudioData(null);
                result.WaveformBase64 = exception.Message + " (" + exception.InnerException + ").";
                return result;
            }
        }

        public async Task PlayAudioAsync(Guid guid, float volume = 0.66f)
        {
            try
            {
                await this.internalClient.PlayAsync(guid, volume);
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error playing audio: " + exception);
            }
        }

        public async Task StopAudioAsync(Guid guid)
        {
            try
            {
                await this.internalClient.StopAsync(guid);
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error stopping audio: " + exception);
            }
        }
        
        public async Task<AudioObjInfo> StopAudio(Guid guid)
        {
            try
            {
                return await this.internalClient.StopAsync(guid);
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error getting audio info: " + exception);
                return new AudioObjInfo(null);
            }
        }

        public async Task<int> StopAllAudioAsync()
        {
            try
            {
                return await this.internalClient.StopAllAsync();
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error stopping all audios: " + exception);
                return -1;
			}
		}

        public async Task<AudioObjInfo> ExecuteTimestretchAsync(Guid guid, string kernel = "timestretch_double", string version = "03", double factor = 0.75d, int chunkSize = 16384, float overlap = 0.5f, bool copyGuid = true, bool allowTempSession = true)
        {
            var result = new AudioObjInfo(null);

            // Stopwatch
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                result = await this.internalClient.ExecuteAudioAsync(guid, kernel, version, factor, chunkSize, overlap, copyGuid, allowTempSession);

				result.LastProcessingTime = (int) stopwatch.ElapsedMilliseconds;
			}
            catch (Exception exception)
            {
                Console.WriteLine("Error executing timestretch: " + exception);
            }
            finally
            {
                stopwatch.Stop();
			}

			return result;
		}

    }
}
