using Microsoft.AspNetCore.Mvc;
using Fractality.Core;
using Fractality.Shared;
using TextCopy;

namespace Fractality.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImageController : ControllerBase
    {
        private readonly ImageCollection imageCollection;
        private readonly IClipboard clipboard;

		public ImageController(ImageCollection imageCollection, IClipboard clipboard)
        {
            this.imageCollection = imageCollection;
            this.clipboard = clipboard;
		}

        [HttpGet("images")]
        [ProducesResponseType(typeof(IEnumerable<ImageObjInfo>), 200)]
        [ProducesResponseType(204)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<IEnumerable<ImageObjInfo>>> GetImages()
        {
            try
            {
                var infos = await Task.Run(() =>
                    this.imageCollection.Images.Select(i => new ImageObjInfo(i))
                );

                if (!infos.Any())
                {
                    return this.NoContent();
                }

                return this.Ok(infos);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error getting 'api/image/images': " + ex);
                return this.StatusCode(500, ex);
            }
        }

        [HttpDelete("remove/{guid}")]
        [ProducesResponseType(typeof(bool), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<bool>> RemoveImage(Guid guid)
        {
            try
            {
                var obj = await Task.Run(() => this.imageCollection[guid]);

                if (obj == null)
                {
                    return this.NotFound($"No image found with Guid '{guid}'");
                }

                await Task.Run(() => this.imageCollection.Remove(guid));

                var result = this.imageCollection[guid] == null;

                if (!result)
                {
                    return this.BadRequest($"Couldn't remove image with Guid '{guid}'");
                }

                return this.Ok(result);
            }
            catch (Exception ex)
            {
                return this.StatusCode(500, $"Error deleting api/image/remove/{guid}: {ex.Message}");
            }
        }

        [HttpPost("empty/{width}x{height}")]
        [ProducesResponseType(typeof(ImageObjInfo), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<ImageObjInfo>> AddEmptyImage(int width = 1920, int height = 1080)
        {
            try
            {
                var obj = await Task.Run(() => this.imageCollection.PopEmpty(new(width, height)));

                if (obj == null)
                {
                    return this.BadRequest($"Failed to create empty image with size {width}x{height}");
                }

                var info = new ImageObjInfo(obj);

                var result = this.imageCollection[info.Guid] != null;

                if (!result)
                {
                    return this.NotFound("Couldn't get image in collection after creating");
                }

                return this.Ok(info);
            }
            catch (Exception ex)
            {
                return this.StatusCode(500, $"Error posting empty image: {ex.Message}");
            }
        }

        [HttpPost("upload")]
        [ProducesResponseType(typeof(ImageObjInfo), 200)]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<ImageObjInfo?>> UploadImage(IFormFile file, bool copyGuid = true)
        {
            if (file.Length == 0)
            {
                return this.NoContent();
            }

            // Temp dir for storing uploaded files
            var tempDir = Path.Combine(Path.GetTempPath(), "image_uploads");
            Directory.CreateDirectory(tempDir);

            // Store original file name and path
            var originalFileName = Path.GetFileName(file.FileName);
            var tempPath = Path.Combine(tempDir, originalFileName);

            try
            {
                // Save file with original name
                await using (var stream = System.IO.File.Create(tempPath))
                {
                    await file.CopyToAsync(stream);
                }

                // Load the image into the collection
                var imgObj = await Task.Run(() => this.imageCollection.LoadImage(tempPath));

                // Keep the original file name in the ImgObj
                if (imgObj != null)
                {
                    imgObj.Name = imgObj.Name ?? Path.GetFileNameWithoutExtension(originalFileName);
                    imgObj.FilePath = originalFileName;
                    this.imageCollection.Add(imgObj);
                }
                else
                {
                    return this.BadRequest("Failed to load image from uploaded file.");
                }

                var info = this.imageCollection.Images.Contains(imgObj) ? await Task.Run(() => new ImageObjInfo(imgObj)) : null;

                if (info == null)
                {
                    return this.NotFound("Failed to retrieve image information after upload.");
                }

				await this.clipboard.SetTextAsync(info.Guid.ToString());
				return this.Ok(info);
            }
            catch (Exception ex)
            {
                return this.BadRequest($"Error uploading image: {ex.Message}");
            }
            finally
            {
                try
                {
                    if (System.IO.File.Exists(tempPath))
                    {
                        System.IO.File.Delete(tempPath);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deleting temporary file: {ex.Message}");
                }
                finally
                {
					await Task.Yield();
                }
            }
        }

        [HttpGet("download/{guid}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> DownloadImage(Guid guid)
        {
            string? filePath = null;
            string? format = null;
            byte[] fileBytes = [];

            try
            {
                var imgObj = await Task.Run(() => this.imageCollection[guid]);

                if (imgObj == null)
                {
                    return this.NotFound($"Image with GUID {guid} not found.");
                }

                var info = new ImageObjInfo(imgObj);

                if (imgObj.Img == null)
                {
                    return this.NoContent();
                }

                // Download image as file
                format = imgObj.FilePath?.Split('.').Last() ?? "png";
                filePath = await Task.Run(() => imgObj.ExportToFile(format: format));
                if (string.IsNullOrEmpty(filePath))
                {
                    return this.BadRequest("Failed to export image to file.");
                }

                fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);

                return this.File(fileBytes, "application/octet-stream", Path.GetFileName(filePath));
            }
            catch (Exception ex)
            {
                return this.StatusCode(500, $"Error downloading image: {ex.Message}");
            }
        }

        [HttpGet("{guid}/base64")]
        [ProducesResponseType(typeof(ImageData), 200)]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<ImageData>> GetBase64(Guid guid)
        {
            try
            {
                var obj = this.imageCollection[guid];
                if (obj == null)
                {
                    return this.NotFound($"No image found with Guid '{guid}'");
                }

                var code = await Task.Run(() => obj.AsBase64());

                if (string.IsNullOrEmpty(code))
                {
                    return this.NoContent();
                }

                var data = new ImageData(code, obj.Width, obj.Height);

                return this.Ok(data);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error getting base64 string: " + ex.Message);
                return this.StatusCode(500, ex);
            }
        }
    }
}
