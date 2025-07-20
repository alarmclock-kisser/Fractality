using Fractality.Core;
using Fractality.Shared;
using Microsoft.AspNetCore.Mvc;
using OpenFractality.Shared;
using System.Collections.Concurrent;
using TextCopy;

namespace Fractality.Api.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class AudioController : ControllerBase
	{
		private readonly AudioCollection audioCollection;
		private readonly IClipboard clipboard;

		private ConcurrentDictionary<Guid, CancellationTokenSource> cancellationTokens = [];

		public AudioController(AudioCollection audioCollection, IClipboard clipboard)
		{
			this.audioCollection = audioCollection;
			this.clipboard = clipboard;
		}

		[HttpGet("audios")]
		[ProducesResponseType(typeof(IEnumerable<AudioObjInfo>), 200)]
		[ProducesResponseType(204)]
		[ProducesResponseType(500)]
		public async Task<ActionResult<IEnumerable<AudioObjInfo>>> GetAudios()
		{
			try
			{
				var infos = await Task.Run(() =>
					this.audioCollection.Tracks.Select(i => new AudioObjInfo(i))
				);

				if (!infos.Any())
				{
					return this.NoContent();
				}

				return this.Ok(infos);
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error getting 'api/audio/audios': " + ex);
				return this.StatusCode(500, ex);
			}
		}

		[HttpDelete("remove/{guid}")]
		[ProducesResponseType(typeof(bool), 200)]
		[ProducesResponseType(404)]
		[ProducesResponseType(400)]
		[ProducesResponseType(500)]
		public async Task<ActionResult<bool>> RemoveAudio(Guid guid)
		{
			try
			{
				var obj = await Task.Run(() => this.audioCollection[guid]);

				if (obj == null)
				{
					return this.NotFound($"No audio found with Guid '{guid}'");
				}

				await Task.Run(() => this.audioCollection.RemoveTrackAt(this.audioCollection.Tracks.ToList().IndexOf(obj)));

				var result = this.audioCollection[guid] == null;

				if (!result)
				{
					return this.BadRequest($"Couldn't remove audio with Guid '{guid}'");
				}

				return this.Ok(result);
			}
			catch (Exception ex)
			{
				return this.StatusCode(500, $"Error deleting api/audio/remove/{guid}: {ex.Message}");
			}
		}

		[HttpPost("upload")]
		[ProducesResponseType(typeof(AudioObjInfo), 200)]
		[ProducesResponseType(204)]
		[ProducesResponseType(404)]
		[ProducesResponseType(400)]
		[ProducesResponseType(500)]
		public async Task<ActionResult<AudioObjInfo?>> UploadAudio(IFormFile file, bool copyGuid = true)
		{
			if (file.Length == 0)
			{
				return this.NoContent();
			}

			// Temp dir for storing uploaded files
			var tempDir = Path.Combine(Path.GetTempPath(), "audio_uploads");
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

				// Load the audio into the collection
				var obj = await Task.Run(() => this.audioCollection.AddTrack(tempPath));

				// Keep the original file name in the ImgObj
				if (obj != null)
				{
					obj.Name = obj.Name ?? Path.GetFileNameWithoutExtension(originalFileName);
					obj.Filepath = originalFileName;
				}
				else
				{
					return this.BadRequest("Failed to load audio from uploaded file.");
				}

				var info = this.audioCollection.Tracks.Contains(obj) ? await Task.Run(() => new AudioObjInfo(obj)) : null;

				if (info == null)
				{
					return this.NotFound("Failed to retrieve audio information after upload.");
				}

				await this.clipboard.SetTextAsync(info.Guid.ToString());
				return this.Ok(info);
			}
			catch (Exception ex)
			{
				return this.BadRequest($"Error uploading audio: {ex.Message}");
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
		public async Task<IActionResult> DownloadAudio(Guid guid)
		{
			string? filePath = null;
			string? format = null;
			byte[] fileBytes = [];

			try
			{
				var obj = await Task.Run(() => this.audioCollection[guid]);

				if (obj == null)
				{
					return this.NotFound($"Audio with GUID {guid} not found.");
				}

				var info = new AudioObjInfo(obj);

				if (obj.Data.LongLength <= 0)
				{
					return this.NoContent();
				}

				// Download audio as file
				format = obj.Filepath?.Split('.').Last() ?? "wav";
				filePath = await Task.Run(() => obj.Export());
				if (string.IsNullOrEmpty(filePath))
				{
					return this.BadRequest("Failed to export audio to file.");
				}

				fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);

				return this.File(fileBytes, "application/octet-stream", Path.GetFileName(filePath));
			}
			catch (Exception ex)
			{
				return this.StatusCode(500, $"Error downloading audio: {ex.Message}");
			}
		}

		[HttpGet("{guid}/base64")]
		[ProducesResponseType(typeof(AudioData), 200)]
		[ProducesResponseType(204)]
		[ProducesResponseType(404)]
		[ProducesResponseType(500)]
		public async Task<ActionResult<AudioData>> GetBase64Waveform(Guid guid)
		{
			try
			{
				var obj = this.audioCollection[guid];
				if (obj == null)
				{
					return this.NotFound($"No audio found with Guid '{guid}'");
				}

				var data = await Task.Run(() => new AudioData(obj));

				if (string.IsNullOrEmpty(data.WaveformBase64))
				{
					return this.NoContent();
				}

				return this.Ok(data);
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error getting base64 string: " + ex.Message);
				return this.StatusCode(500, ex);
			}
		}

		[HttpPost("play/{guid}/{volume}")]
		[ProducesResponseType(200)]
		[ProducesResponseType(204)]
		[ProducesResponseType(404)]
		[ProducesResponseType(500)]
		public async Task<IActionResult> PlayAudio(Guid guid, float volume = 0.66f)
		{
			// Verify volume in range
			volume = Math.Clamp(volume, 0.0f, 1.0f);

			// Get cancelation token
			var cancelationToken = this.HttpContext.RequestAborted;

			try
			{
				var obj = await Task.Run(() => this.audioCollection[guid]);
				if (obj == null)
				{
					return this.NotFound($"No audio found with Guid '{guid}'");
				}

				// Try-add a cancellation token for this audio
				if (!this.cancellationTokens.TryAdd(guid, new CancellationTokenSource()))
				{
					return this.BadRequest($"Audio with Guid '{guid}' is already playing.");
				}

				await Task.Run(() => obj.Play(cancelationToken, null, volume));
				return this.Ok();
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error playing audio: " + ex.Message);
				return this.StatusCode(500, ex);
			}
		}

		[HttpPost("stop/{guid}")]
		[ProducesResponseType(typeof(AudioObjInfo), 200)]
		[ProducesResponseType(400)]
		[ProducesResponseType(404)]
		[ProducesResponseType(500)]
		public async Task<ActionResult<AudioObjInfo>> StopAudio(Guid guid)
		{
			var result = new AudioObjInfo(null);

			// Try to cancel the audio playback if cancellation token exists
			if (this.cancellationTokens.TryRemove(guid, out var cancellationTokenSource))
			{
				cancellationTokenSource.Cancel();
				cancellationTokenSource.Dispose();

				Console.WriteLine($"Cancelled audio playback for Guid '{guid}'");

				var obj = this.audioCollection[guid];
				if (obj == null || obj.Id == Guid.Empty)
				{
					return this.NotFound($"No audio found with Guid '{guid}'");
				}

				if (obj.Playing)
				{
					return this.BadRequest($"Audio with Guid '{guid}' is still playing.");
				}

				// Return the audio info
				result = new AudioObjInfo(obj);
			}

			try
			{
				result = await Task.Run(() =>
				{
					var obj = this.audioCollection[guid];

					if (obj == null || obj.Id == Guid.Empty)
					{
						return null;
					}

					obj.Stop();

					return new AudioObjInfo(obj);
				});

				if (result == null || result.Guid == Guid.Empty)
				{
					return this.NotFound($"No audio found with Guid '{guid}'");
				}

				if (result.Playing)
				{
					return this.BadRequest($"Audio with Guid '{guid}' is still playing.");
				}

				return this.Ok(result);
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error stopping audio: " + ex.Message);
				return this.StatusCode(500, ex);
			}
		}

		[HttpPost("stopAll")]
		[ProducesResponseType(typeof(int), 200)]
		[ProducesResponseType(204)]
		[ProducesResponseType(400)]
		[ProducesResponseType(500)]
		public async Task<ActionResult<int>> StopAllAudio()
		{
			int result = 0;

			try
			{
				result = await this.audioCollection.StopAll();

				if (result < 0)
				{
					return this.BadRequest("Some tracks are still playing");
				}

				if (result == 0)
				{
					return this.NoContent();
				}

				return this.Ok(result);
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error stopping all audio: " + ex.Message);
				return this.StatusCode(500, ex);
			}
		}
	}
}
