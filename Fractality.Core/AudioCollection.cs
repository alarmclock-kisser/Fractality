using NAudio.Wave;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using Color = SixLabors.ImageSharp.Color;

namespace Fractality.Core
{
	public class AudioCollection : IDisposable
	{
		// Event Definitions for UI communication
		public event EventHandler<string>? LogMessage;
		public event EventHandler<IReadOnlyList<AudioObj>>? TracksChanged;
		public event EventHandler<AudioObj?>? CurrentTrackChanged;
		public event EventHandler<PlaybackState>? PlaybackStateChanged;
		public event EventHandler<double>? PlaybackPositionChanged;

		// Audio Data
		private readonly List<AudioObj> _tracks = [];
		private AudioObj? _currentTrack;

		// Properties
		public string Repopath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..");
		public IReadOnlyList<AudioObj> Tracks => this._tracks.AsReadOnly();

		public AudioObj? CurrentTrack
		{
			get => this._currentTrack;
			private set
			{
				if (this._currentTrack != value)
				{
					this._currentTrack = value;
					CurrentTrackChanged?.Invoke(this, this._currentTrack);
				}
			}
		}

		private CancellationTokenSource _playbackCancellationTokenSource = new();


		public AudioCollection()
		{
			
		}

		public AudioObj? this[Guid guid]
		{
			get => this._tracks.FirstOrDefault(t => t.Id == guid);
		}

		// --- Public Methods for Audio Logic ---
		public AudioObj AddTrack(string filePath)
		{
			if (!File.Exists(filePath))
			{
				throw new FileNotFoundException("Audio file not found", filePath);
			}

			AudioObj track = new AudioObj(filePath);
			this._tracks.Add(track);
			this.CurrentTrack = track; // Set newly added track as current

			TracksChanged?.Invoke(this, this.Tracks); // Notify UI about track list change
			LogMessage?.Invoke(this, $"Added track: {track.Name}");

			return track;
		}

		public AudioObj CreateEmptyTrack(long length, int samplerate = 44100, int channels = 1, int bitdepth = 16)
		{
			float[] data = new float[length];
			Array.Fill(data, 0.0f);

			int number = this.Tracks.Count;
			AudioObj obj = new(data, samplerate, channels, bitdepth, number);

			this._tracks.Add(obj);
			this.CurrentTrack = obj;

			TracksChanged?.Invoke(this, this.Tracks);
			LogMessage?.Invoke(this, $"Created empty track: {obj.Name}");
			return obj;
		}

		public AudioObj CreateWaveform(string wave = "sin", int lengthSec = 1, int samplerate = 44100, int channels = 1, int bitdepth = 16)
		{
			if (lengthSec <= 0 || samplerate <= 0 || channels <= 0 || bitdepth <= 0)
			{
				throw new ArgumentException("Invalid parameters for waveform creation.");
			}

			long length = (long) lengthSec * samplerate * channels; // Cast to long to prevent overflow
			float[] data = new float[length];
			double frequency = 440.0;
			double increment = (2 * Math.PI * frequency) / samplerate;
			Random rand = new Random();

			float amplitude = 0.8f;

			switch (wave.ToLower())
			{
				case "sin":
					for (long i = 0; i < length; i += channels)
					{
						float sample = amplitude * (float) Math.Sin(i * increment);
						for (int c = 0; c < channels; c++)
						{
							data[i + c] = sample;
						}
					}
					break;
				case "square":
					for (long i = 0; i < length; i += channels)
					{
						float sample = amplitude * ((i % (samplerate / frequency) < (samplerate / frequency) / 2) ? 1.0f : -1.0f);
						for (int c = 0; c < channels; c++)
						{
							data[i + c] = sample;
						}
					}
					break;
				case "saw":
					for (long i = 0; i < length; i += channels)
					{
						float sample = amplitude * (float) ((i % (samplerate / frequency)) / (samplerate / frequency) * 2 - 1);
						for (int c = 0; c < channels; c++)
						{
							data[i + c] = sample;
						}
					}
					break;
				case "noise":
					for (long i = 0; i < length; i += channels)
					{
						float sample = amplitude * (float) (rand.NextDouble() * 2 - 1);
						for (int c = 0; c < channels; c++)
						{
							data[i + c] = sample;
						}
					}
					break;
				default:
					throw new ArgumentException("Unsupported waveform type.");
			}

			AudioObj obj = new AudioObj(data, samplerate, channels, bitdepth, this.Tracks.Count)
			{
				Filepath = $"Generated_{wave}_{lengthSec}s_{samplerate}Hz_{channels}ch_{bitdepth}bit.wav"
			};

			this._tracks.Add(obj);
			this.CurrentTrack = obj;

			TracksChanged?.Invoke(this, this.Tracks);
			LogMessage?.Invoke(this, $"Created waveform: {obj.Filepath}");
			Debug.WriteLine($"Sample range: {data.Min()} to {data.Max()}");

			return obj;
		}

		public void LoadResourcesAudios()
		{
			string[] audioFiles = Directory.GetFiles(Path.Combine(this.Repopath, "Resources"), "*.*", SearchOption.AllDirectories)
				.Where(f => f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
				.ToArray();

			if (audioFiles.Length == 0)
			{
				LogMessage?.Invoke(this, "No audio files found in Resources/Audios directory.");
				return;
			}

			foreach (string file in audioFiles)
			{
				try
				{
					this.AddTrack(file);
				}
				catch (Exception ex)
				{
					LogMessage?.Invoke(this, $"Error loading audio file '{file}': {ex.Message}");
				}
			}
		}

		public void RemoveTrackAt(int index)
		{
			if (index < 0 || index >= this._tracks.Count)
			{
				return;
			}

			this._tracks[index].Dispose();
			this._tracks.RemoveAt(index);

			// Adjust CurrentTrack if it was the one removed or if list is empty
			if (this._tracks.Count == 0)
			{
				this.CurrentTrack = null;
			}
			else if (this.CurrentTrack == null || index == this._tracks.Count) // If removed last item or current was removed
			{
				this.CurrentTrack = this._tracks[Math.Min(index, this._tracks.Count - 1)];
			}

			TracksChanged?.Invoke(this, this.Tracks);
			LogMessage?.Invoke(this, $"Removed track at index {index}");
		}

		public void SetCurrentTrack(int trackIndex)
		{
			if (trackIndex >= 0 && trackIndex < this._tracks.Count)
			{
				this.CurrentTrack = this._tracks[trackIndex];
			}
			else
			{
				this.CurrentTrack = null;
			}
		}

		public void TogglePlayback(float volume = 1.0f)
		{
			if (this.CurrentTrack == null || this.CurrentTrack.Data.LongLength == 0)
			{
				return;
			}

			if (this.CurrentTrack.Player.PlaybackState == PlaybackState.Playing)
			{
				this._playbackCancellationTokenSource.Cancel();
				this.CurrentTrack.Stop();
				PlaybackStateChanged?.Invoke(this, PlaybackState.Stopped);
			}
			else
			{
				this._playbackCancellationTokenSource = new CancellationTokenSource();
				this.CurrentTrack.Play(this._playbackCancellationTokenSource.Token, () =>
				{
					// Callback when playback stops naturally
					PlaybackStateChanged?.Invoke(this, PlaybackState.Stopped);
					// Reset position after stopping
					PlaybackPositionChanged?.Invoke(this, 0);
				}, volume);

				PlaybackStateChanged?.Invoke(this, PlaybackState.Playing);
				// Start a background task to update playback position
				Task.Run(async () =>
				{
					while (this.CurrentTrack.Player.PlaybackState == PlaybackState.Playing && !this._playbackCancellationTokenSource.IsCancellationRequested)
					{
						PlaybackPositionChanged?.Invoke(this, this.CurrentTrack.CurrentTime);
						await Task.Delay(30); // Update every 30ms
					}
				}, this._playbackCancellationTokenSource.Token);
			}
		}

		public async Task<int> StopAll()
		{
			int stopped = 0;

			try
			{
				await Task.Run(() =>
				{
					foreach (var track in this._tracks)
					{
						if (track.Playing)
						{
							track.Stop();
							stopped++;
						}
					}
				});
			}
			catch (Exception ex)
			{
				LogMessage?.Invoke(this, $"Error stopping all tracks: {ex.Message}");
				stopped = -1;
			}
			finally
			{
				await Task.Yield();
			}

			return stopped;
		}

		public void Dispose()
		{
			this._playbackCancellationTokenSource.Cancel();
			foreach (var track in this._tracks)
			{
				track.Dispose();
			}
			this._tracks.Clear();
			GC.SuppressFinalize(this);
		}
	}

	// AudioObject class (Modified to provide raw waveform data)
	public class AudioObj : IDisposable
	{
		// ----- ----- ----- ATTRIBUTES ----- ----- ----- \\
		public Guid Id { get; } = Guid.Empty;
		public string Filepath { get; set; }
		public string Name { get; set; }
		public float[] Data { get; set; } = [];
		public Vector2[] ComplexData { get; set; } = [];
		public int Samplerate { get; set; } = -1;
		public int Bitdepth { get; set; } = -1;
		public int Channels { get; set; } = -1;
		public long Length { get; set; } = -1;

		public IntPtr Pointer { get; set; } = 0;
		public int ChunkSize { get; set; } = 0;
		public int OverlapSize { get; set; } = 0;
		public char Form { get; set; } = 'f';
		public double StretchFactor { get; set; } = 1.0;

		public string MetaString => this.GetMetaString();
		public float Bpm { get; set; } = 0.0f;

		public WaveOutEvent Player { get; set; } = new WaveOutEvent();
		public bool Playing => this.Player.PlaybackState == PlaybackState.Playing;
		public long SizeInBytes => this.Data.Length * (this.Bitdepth / 8) * this.Channels;
		public double Duration => (this.Samplerate > 0 && this.Channels > 0) ? (double) this.Length / (this.Samplerate * this.Channels) : 0;

		// ----- ----- ----- PROPERTIES ----- ----- ----- \\
		public long Position
		{
			get
			{
				return this.Player == null || this.Player.PlaybackState != PlaybackState.Playing
					? 0
					: this.Player.GetPosition() / (this.Channels * (this.Bitdepth / 8));
			}
		}

		public double CurrentTime
		{
			get
			{
				return this.Samplerate <= 0 ? 0 : (double) this.Position / this.Samplerate;
			}
		}

		public bool OnHost => (this.Data.Length > 0 || this.ComplexData.Length > 0) && this.Pointer == 0;
		public bool OnDevice => (this.Data.Length == 0 && this.ComplexData.Length == 0) && this.Pointer != 0;

		public Image<Rgba32> WaveformImage { get; set; } = new Image<Rgba32>(100, 100);
		public Image<Rgba32> WaveformImageSimple { get; set; } = new Image<Rgba32>(100, 100);
		public SixLabors.ImageSharp.Size WaveformSize { get; set; } = new SixLabors.ImageSharp.Size(1000, 200);

		private System.Timers.Timer WaveformUpdateTimer
		{
			get
			{
				return this.SetupTimer(this.WaveformUpdateFrequency);
			}
			set
			{
				if (value != null)
				{
					value.Interval = 1000.0 / this.WaveformUpdateFrequency;
				}
			}
		}
		public int WaveformUpdateFrequency { get; set; } = 100;

		public string WaveformBase64
		{
			get
			{
				if (this.WaveformImage == null || this.WaveformImage.Width <= 0 || this.WaveformImage.Height <= 0)
				{
					return string.Empty;
				}

				return Task.Run(() =>
				{
					using var ms = new MemoryStream();
					this.WaveformImage.SaveAsPng(ms);
					return Convert.ToBase64String(ms.ToArray());
				}).GetAwaiter().GetResult();
			}
		}


		// ----- ----- ----- CONSTRUCTOR ----- ----- ----- \\
		public AudioObj(string filepath)
		{
			this.Id = Guid.NewGuid();
			this.Filepath = filepath;
			this.Name = Path.GetFileNameWithoutExtension(filepath);
			this.LoadAudioFile();
		}

		public AudioObj(float[] data, int samplerate = 44100, int channels = 1, int bitdepth = 16, int number = 0)
		{
			this.Id = Guid.NewGuid();
			this.Data = data;
			this.Name = "Empty_" + number.ToString("000");
			this.Filepath = "No file supplied";
			this.Samplerate = samplerate;
			this.Channels = channels;
			this.Bitdepth = bitdepth;
			this.Length = data.LongLength;
		}

		public System.Timers.Timer SetupTimer(int hz = 100)
		{
			hz = Math.Clamp(hz, 1, 144);

			var timer =  new System.Timers.Timer(1000.0 / hz);

			// Register tick event
			timer.Elapsed += (sender, e) =>
			{
				if (this.Data != null && this.Data.Length > 0)
				{
					this.WaveformImage = this.GetWaveformImageAsync(this.Data, this.WaveformSize.Width, this.WaveformSize.Height).Result;
					this.WaveformImageSimple = this.GetWaveformImageAsync(this.Data, 1000, 200).Result;
				}
			};

			timer.AutoReset = true;
			timer.Enabled = true;

			return timer;
		}

		public void LoadAudioFile()
		{
			if (string.IsNullOrEmpty(this.Filepath))
			{
				throw new FileNotFoundException("File path is empty");
			}

			using AudioFileReader reader = new(this.Filepath);
			this.Samplerate = reader.WaveFormat.SampleRate;
			this.Bitdepth = reader.WaveFormat.BitsPerSample;
			this.Channels = reader.WaveFormat.Channels;
			this.Length = reader.Length / 4; // Length in bytes (for float, 4 bytes per sample)

			long numSamples = reader.Length / (reader.WaveFormat.BitsPerSample / 8);
			this.Data = new float[numSamples];

			int read = reader.Read(this.Data, 0, (int) numSamples);
			if (read != numSamples)
			{
				float[] resizedData = new float[read];
				Array.Copy(this.Data, resizedData, read);
				this.Data = resizedData;
			}

			// Read bpm metadata if available
			float bpm = 0.0f;
			try
			{
				if (!string.IsNullOrEmpty(this.Filepath) && File.Exists(this.Filepath))
				{
					using (var file = TagLib.File.Create(this.Filepath))
					{
						if (file.Tag.BeatsPerMinute > 0)
						{
							bpm = (float) file.Tag.BeatsPerMinute;
						}
						if (file.TagTypes.HasFlag(TagLib.TagTypes.Id3v2))
						{
							var id3v2Tag = (TagLib.Id3v2.Tag) file.GetTag(TagLib.TagTypes.Id3v2);

							var tbpmTextFrame = TagLib.Id3v2.TextInformationFrame.Get(id3v2Tag, "TBPM", false);

							if (tbpmTextFrame != null && tbpmTextFrame.Text.Any())
							{
								string bpmString = tbpmTextFrame.Text.FirstOrDefault() ?? "0,0";
								if (!string.IsNullOrEmpty(bpmString))
								{
									bpmString = bpmString.Replace(',', '.');

									if (float.TryParse(bpmString, NumberStyles.Any, CultureInfo.InvariantCulture, out float parsedBpmFromTbpm))
									{
										bpm = parsedBpmFromTbpm;
									}
								}
							}
							else
							{
								bpm = 0.0f;
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Fehler beim Lesen der BPM: {ex.Message}");
			}

			this.Bpm = bpm > 0 ? bpm : 0.0f;
		}

		public byte[] GetBytes()
		{
			int bytesPerSample = this.Bitdepth / 8;
			byte[] bytes = new byte[this.Data.Length * bytesPerSample];

			Parallel.For(0, this.Data.Length, i =>
			{
				switch (this.Bitdepth)
				{
					case 8:
						bytes[i] = (byte) (this.Data[i] * 127);
						break;
					case 16:
						short sample16 = (short) (this.Data[i] * short.MaxValue);
						Buffer.BlockCopy(BitConverter.GetBytes(sample16), 0, bytes, i * bytesPerSample, bytesPerSample);
						break;
					case 24:
						int sample24 = (int) (this.Data[i] * 8388607);
						byte[] temp = BitConverter.GetBytes(sample24);
						bytes[i * bytesPerSample] = temp[0];
						bytes[i * bytesPerSample + 1] = temp[1];
						bytes[i * bytesPerSample + 2] = temp[2];
						break;
					case 32:
						Buffer.BlockCopy(BitConverter.GetBytes(this.Data[i]), 0, bytes, i * bytesPerSample, bytesPerSample);
						break;
				}
			});
			return bytes;
		}

		public List<float[]> GetChunks(int size = 2048, float overlap = 0.5f, bool keepData = false)
		{
			if (this.Data == null || this.Data.Length == 0)
			{
				return [];
			}
			if (size <= 0 || overlap < 0 || overlap >= 1)
			{
				return [];
			}

			this.ChunkSize = size;
			this.OverlapSize = (int) (size * overlap);
			int step = size - this.OverlapSize;
			int numChunks = (this.Data.Length - size) / step + 1;

			float[][] chunks = new float[numChunks][];
			Parallel.For(0, numChunks, i =>
			{
				int sourceOffset = i * step;
				float[] chunk = new float[size];
				Array.Copy(this.Data, sourceOffset, chunk, 0, size);
				chunks[i] = chunk;
			});

			if (!keepData)
			{
				this.Data = [];
			}

			return chunks.ToList();
		}

		public List<Vector2[]> GetCompexChunks(int size = 2048, float overlap = 0.5f)
		{
			if (this.ComplexData == null || this.ComplexData.Length == 0)
			{
				return [];
			}
			if (size <= 0 || overlap < 0 || overlap >= 1)
			{
				return [];
			}

			this.ChunkSize = size;
			this.OverlapSize = (int) (size * overlap);
			int step = size - this.OverlapSize;
			int numChunks = (this.ComplexData.Length - size) / step + 1;

			List<Vector2[]> chunks = new List<Vector2[]>(numChunks);
			for (int i = 0; i < numChunks; i++)
			{
				Vector2[] chunk = new Vector2[size];
				int sourceOffset = i * step;
				Array.Copy(this.ComplexData, sourceOffset, chunk, 0, size);
				chunks.Add(chunk);
			}
			return chunks;
		}

		public void AggregateChunks(List<float[]> chunks, bool keepPointer = false)
		{
			if (chunks == null || chunks.Count == 0)
			{
				return;
			}

			int size = this.ChunkSize;
			int step = size - this.OverlapSize;
			int outputLength = (chunks.Count - 1) * step + size;

			float[] output = new float[outputLength];
			float[] weightSum = new float[outputLength];

			Parallel.For(0, outputLength, i => { output[i] = 0f; weightSum[i] = 0f; });

			Parallel.ForEach(chunks, () => (new float[outputLength], new float[outputLength]),
				(chunk, loopState, localData) =>
				{
					(float[] localOutput, float[] localWeightSum) = localData;
					int chunkIndex = chunks.IndexOf(chunk); // This might be slow in a Parallel.ForEach, consider using Parallel.For for index
					int offset = chunkIndex * step;

					for (int j = 0; j < Math.Min(size, chunk.Length); j++)
					{
						int idx = offset + j;
						if (idx < outputLength)
						{
							localOutput[idx] += chunk[j];
							localWeightSum[idx] += 1f;
						}
					}
					return localData;
				},
				localData =>
				{
					lock (output)
					{
						for (int i = 0; i < outputLength; i++)
						{
							output[i] += localData.Item1[i];
							weightSum[i] += localData.Item2[i];
						}
					}
				}
			);

			Parallel.For(0, outputLength, i =>
			{
				if (weightSum[i] > 0f)
				{
					output[i] /= weightSum[i];
				}
			});

			this.Data = output;
			this.Length = output.LongLength;
			
			if (!keepPointer)
			{
				this.Pointer = IntPtr.Zero;
			}
		}

		public void AggregateStretchedChunks(List<float[]> chunks, bool keepPointer = false)
		{
			if (chunks == null || chunks.Count == 0)
			{
				return;
			}
			double stretchFactor = this.StretchFactor;

			int chunkSize = this.ChunkSize;
			int overlapSize = this.OverlapSize;

			int originalHopSize = chunkSize - overlapSize;
			int stretchedHopSize = (int) Math.Round(originalHopSize * stretchFactor);

			int outputLength = (chunks.Count - 1) * stretchedHopSize + chunkSize;

			double[] outputAccumulator = new double[outputLength];
			double[] weightSum = new double[outputLength];

			double[] window = new double[chunkSize];
			for (int i = 0; i < chunkSize; i++)
			{
				window[i] = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (double) (chunkSize - 1)));
			}

			Parallel.For(0, outputLength, i => { outputAccumulator[i] = 0.0; weightSum[i] = 0.0; });


			Parallel.ForEach(chunks, () => (new double[outputLength], new double[outputLength]),
				(chunk, loopState, localData) =>
				{
					double[] localOutput = localData.Item1;
					double[] localWeight = localData.Item2;

					int chunkIndex = chunks.IndexOf(chunk); // This might be slow in a Parallel.ForEach, consider Parallel.For
					int offset = chunkIndex * stretchedHopSize;

					for (int j = 0; j < Math.Min(chunkSize, chunk.Length); j++)
					{
						int idx = offset + j;
						if (idx >= outputLength)
						{
							break;
						}

						double windowedSample = (double) chunk[j] * window[j];
						localOutput[idx] += windowedSample;
						localWeight[idx] += window[j];
					}
					return localData;
				},
				localData =>
				{
					lock (outputAccumulator)
					{
						for (int i = 0; i < outputLength; i++)
						{
							outputAccumulator[i] += localData.Item1[i];
							weightSum[i] += localData.Item2[i];
						}
					}
				}
			);

			float[] finalOutput = new float[outputLength];
			Parallel.For(0, outputLength, i =>
			{
				if (weightSum[i] > 1e-6)
				{
					finalOutput[i] = (float) (outputAccumulator[i] / weightSum[i]);
				}
				else
				{
					finalOutput[i] = 0.0f;
				}
			});

			this.Data = finalOutput;
			this.Length = finalOutput.Length;
			this.Pointer = keepPointer ? this.Pointer : IntPtr.Zero;
		}


		public void AggregateComplexes(List<Vector2[]> complexChunks)
		{
			this.ComplexData = new Vector2[complexChunks.Count * complexChunks[0].Length];
			int index = 0;
			foreach (var chunk in complexChunks)
			{
				foreach (var value in chunk)
				{
					this.ComplexData[index++] = value;
				}
			}
			this.Length = this.ComplexData.LongLength; // Update length for complex data
			this.Pointer = IntPtr.Zero;
			this.Form = 'c'; // Indicate complex form
		}

		public void Play(CancellationToken cancellationToken, Action? onPlaybackStopped = null, float initialVolume = 1.0f)
		{
			this.WaveformUpdateTimer.Stop();

			if (this.Data == null || this.Data.Length == 0)
			{
				return;
			}

			this.Player.Stop(); // Ensure any previous playback is stopped
			this.Player.Dispose(); // Dispose old player
			this.Player = new WaveOutEvent
			{
				Volume = initialVolume
			};

			byte[] bytes = this.GetBytes();
			WaveFormat waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(this.Samplerate, this.Channels);
			RawSourceWaveStream stream = new(new MemoryStream(bytes), waveFormat);

			this.Player.PlaybackStopped += (s, e) =>
			{
				onPlaybackStopped?.Invoke();
				stream.Dispose();
				this.Player.Dispose();
			};

			this.WaveformUpdateTimer.Start();

			this.Player.Init(stream);
			this.Player.Play();
		}

		public void Stop()
		{
			this.WaveformUpdateTimer.Stop();
			this.Player.Stop();
		}

		public void Reload()
		{
			this.Pointer = 0;
			this.Form = 'f';
			this.ChunkSize = 0;
			this.OverlapSize = 0;
			this.LoadAudioFile();
		}

		public void Normalize(float maxAmplitude = 1.0f)
		{
			if (this.Data == null || this.Data.Length == 0)
			{
				return;
			}

			float globalMax = 0f;
			object lockObj = new object();

			Parallel.For(0, this.Data.Length, () => 0f, (i, _, localMax) =>
			{
				float abs = Math.Abs(this.Data[i]);
				return abs > localMax ? abs : localMax;
			},
			localMax =>
			{
				lock (lockObj)
				{
					if (localMax > globalMax)
					{
						globalMax = localMax;
					}
				}
			});

			if (globalMax == 0f)
			{
				return;
			}

			float scale = maxAmplitude / globalMax;
			Parallel.For(0, this.Data.Length, i =>
			{
				this.Data[i] *= scale;
			});
		}

		public async Task<Image<Rgba32>> GetWaveformImageAsync(float[]? data, int width = 720, int height = 480,
			int samplesPerPixel = 128, float amplifier = 1.0f, long offset = 0,
			Color? graphColor = null, Color? backgroundColor = null, bool smoothEdges = true, int workerCount = -2)
		{
			// Normalize image dimensions
			width = Math.Max(100, width);
			height = Math.Max(100, height);

			// Normalize colors & get rgba values
			graphColor ??= Color.BlueViolet;
			backgroundColor ??= Color.White;

			// New result image + color fill
			Image<Rgba32> image = new(width, height);
			await Task.Run(() =>
			{
				image.Mutate(ctx => ctx.BackgroundColor(backgroundColor.Value));
			});

			// Verify data
			data ??= this.Data;
			if (data  == null || data.LongLength <= 0)
			{
				return image;
			}

			// Adjust offset if necessary
			if (data.Length <= offset)
			{
				offset = 0;
			}

			// Adjust workers
			if (workerCount == 0)
			{
				workerCount = Environment.ProcessorCount;
			}
			if (workerCount < 0)
			{
				// Negative means subtract from total (to have free workers left)
				workerCount = Environment.ProcessorCount + workerCount;
			}
			if (workerCount <= 0)
			{
				// Fallback to single worker if invalid
				workerCount = 1;
			}

			// Calculate the number of samples to process -> take array from data
			long totalSamples = Math.Min(data.Length - offset, width * samplesPerPixel);
			if (totalSamples <= 0)
			{
				return image;
			}

			float[] samples = new float[totalSamples];
			Array.Copy(data, offset, samples, 0, totalSamples);

			// Split into concurrent chunks for each worker (even count, last chunk fills up with zeros)
			int chunkSize = (int) Math.Ceiling((double) totalSamples / workerCount);
			ConcurrentDictionary<int, float[]> chunks = await Task.Run(() =>
			{
				ConcurrentDictionary<int, float[]> chunkDict = new();
				Parallel.For(0, workerCount, i =>
				{
					int start = i * chunkSize;
					int end = Math.Min(start + chunkSize, (int) totalSamples);
					if (start < totalSamples)
					{
						float[] chunk = new float[chunkSize];
						Array.Copy(samples, start, chunk, 0, end - start);
						chunkDict[i] = chunk;
					}
				});
				return chunkDict;
			});

			// Draw each chunk at the corresponding position with amplification per worker on bitmap
			Parallel.ForEach(chunks, parallelOptions: new ParallelOptions { MaxDegreeOfParallelism = workerCount }, kvp =>
			{
				int workerIndex = kvp.Key;
				float[] chunk = kvp.Value;
				int xStart = workerIndex * (width / workerCount);
				int xEnd = (workerIndex + 1) * (width / workerCount);
				if (xEnd > width)
				{
					xEnd = width;
				}
				for (int x = xStart; x < xEnd; x++)
				{
					int sampleIndex = (x - xStart) * samplesPerPixel;
					if (sampleIndex < chunk.Length)
					{
						float sampleValue = chunk[sampleIndex] * amplifier;
						int yPos = (int) ((sampleValue + 1.0f) / 2.0f * height);
						yPos = Math.Clamp(yPos, 0, height - 1);

						// Draw vertical line for this sample
						for (int y = 0; y < height; y++)
						{
							if (y == yPos)
							{
								image[x, y] = graphColor.Value;
							}
							else
							{
								image[x, y] = backgroundColor.Value; 
							}

							// Optionally: Apply anti-aliasing for smoother edges
							if (smoothEdges && y > 0 && y < height - 1)
							{
								float edgeFactor = (float) (1.0 - Math.Abs(y - yPos) / (height / 2.0));
								if (edgeFactor > 0.1f)
								{
									image[x, y] = new Rgba32(
										(byte) (graphColor.Value.ToPixel<Rgba32>().R * edgeFactor + backgroundColor.Value.ToPixel<Rgba32>().R * (1 - edgeFactor)),
										(byte) (graphColor.Value.ToPixel<Rgba32>().G * edgeFactor + backgroundColor.Value.ToPixel<Rgba32>().G * (1 - edgeFactor)),
										(byte) (graphColor.Value.ToPixel<Rgba32>().B * edgeFactor + backgroundColor.Value.ToPixel<Rgba32>().B * (1 - edgeFactor)),
										(byte) (graphColor.Value.ToPixel<Rgba32>().A * edgeFactor + backgroundColor.Value.ToPixel<Rgba32>().A * (1 - edgeFactor))
									);
								}
							}
						}
					}
				}
			});

			// Wait for all tasks to complete
			await Task.Yield();

			// Return the generated waveform image
			return image;
		}

		public async Task<Image<Rgba32>> GetWaveformImageSimpleAsync(float[]? data, int width = 720, int height = 480,
			int samplesPerPixel = 128, float amplifier = 1.0f, long offset = 0,
			Color? graphColor = null, Color? backgroundColor = null, bool smoothEdges = true)
		{
			// Normalize image dimensions
			width = Math.Max(100, width);
			height = Math.Max(100, height);

			// Normalize colors & get rgba values
			graphColor ??= Color.BlueViolet;
			backgroundColor ??= Color.White;

			// New result image + color fill
			Image<Rgba32> image = new(width, height);
			await Task.Run(() =>
			{
				image.Mutate(ctx => ctx.BackgroundColor(backgroundColor.Value));
			});

			// Verify data
			data ??= this.Data;
			if (data == null || data.LongLength <= 0)
			{
				return image;
			}

			// Adjust offset if necessary
			if (data.Length <= offset)
			{
				offset = 0;
			}

			// Calculate the number of samples to process -> take array from data
			long totalSamples = Math.Min(data.Length - offset, width * samplesPerPixel);
			if (totalSamples <= 0)
			{
				return image;
			}

			float[] samples = new float[totalSamples];
			Array.Copy(data, offset, samples, 0, totalSamples);

			// Draw the waveform on the image async anfd in parallel
			await Task.Run(() =>
			{
				Parallel.For(0, width, x =>
				{
					int sampleIndex = (int) ((float) x / width * totalSamples);
					if (sampleIndex < samples.Length)
					{
						float sampleValue = samples[sampleIndex] * amplifier;
						int yPos = (int) ((sampleValue + 1.0f) / 2.0f * height);
						yPos = Math.Clamp(yPos, 0, height - 1);
						
						// Draw vertical line for this sample
						for (int y = 0; y < height; y++)
						{
							if (y == yPos)
							{
								image[x, y] = graphColor.Value;
							}
							else
							{
								image[x, y] = backgroundColor.Value;
							}

							// Optionally: Apply anti-aliasing for smoother edges
							if (smoothEdges && y > 0 && y < height - 1)
							{
								float edgeFactor = (float) (1.0 - Math.Abs(y - yPos) / (height / 2.0));
								if (edgeFactor > 0.1f)
								{
									image[x, y] = new Rgba32(
										(byte) (graphColor.Value.ToPixel<Rgba32>().R * edgeFactor + backgroundColor.Value.ToPixel<Rgba32>().R * (1 - edgeFactor)),
										(byte) (graphColor.Value.ToPixel<Rgba32>().G * edgeFactor + backgroundColor.Value.ToPixel<Rgba32>().G * (1 - edgeFactor)),
										(byte) (graphColor.Value.ToPixel<Rgba32>().B * edgeFactor + backgroundColor.Value.ToPixel<Rgba32>().B * (1 - edgeFactor)),
										(byte) (graphColor.Value.ToPixel<Rgba32>().A * edgeFactor + backgroundColor.Value.ToPixel<Rgba32>().A * (1 - edgeFactor))
									);
								}
							}
						}
					}
				});
			});

			// Wait for all tasks to complete
			await Task.Yield();

			// Return the generated waveform image
			return image;
		}

		public string? Export(string outPath = "")
		{
			string baseFileName = $"{this.Name} [{this.Bpm:F1}]";

			if (!Directory.Exists(outPath))
			{
				outPath = Path.GetDirectoryName(outPath) ?? string.Empty;
			}

			if (string.IsNullOrEmpty(outPath) || !Directory.Exists(outPath))
			{
				outPath = Path.Combine(Path.GetTempPath(), $"{this.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
			}

			string filePath;
			if (!string.IsNullOrEmpty(outPath))
			{
				if (Path.HasExtension(outPath))
				{
					filePath = outPath;
				}
				else
				{
					filePath = Path.Combine(outPath, baseFileName + ".wav");
				}
			}
			else
			{
				return null;
			}

			try
			{
				byte[] bytes = this.GetBytes();
				WaveFormat waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(this.Samplerate, this.Channels);

				using (RawSourceWaveStream stream = new(new MemoryStream(bytes), waveFormat))
				using (FileStream fileStream = new(filePath, FileMode.Create))
				{
					WaveFileWriter.WriteWavFileToStream(fileStream, stream);
				}

				if (this.Bpm > 0.0f)
				{
					try
					{
						using (var file = TagLib.File.Create(filePath))
						{
							file.Tag.BeatsPerMinute = (uint) (this.Bpm * 100);
							file.Save();
						}
					}
					catch (Exception ex)
					{
						Debug.WriteLine($"Fehler beim Hinzufügen der BPM-Tags für '{Path.GetExtension(filePath)}': {ex.Message}");
					}
				}
				return filePath;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Fehler beim Exportieren der Audiodatei: {ex.Message}");
				return null;
			}
		}
		public void Dispose()
		{
			this.Player?.Dispose();
			this.Data = [];
			this.ComplexData = []; // Clear complex data too
			this.Pointer = 0;
			GC.SuppressFinalize(this);
		}

		public string GetMetaString()
		{
			return $"{this.Samplerate} Hz, {this.Bitdepth} bits, {this.Channels} ch., {(this.Length / (this.Bitdepth / 8) / 1024):N0} KSamples, BPM: {this.Bpm}, Form: '{this.Form}' at <{this.Pointer.ToString("X16")}>";
		}
	}

	// You might need to add `using System.Numerics;` if Vector2 is from there
	// If Vector2 is a custom struct, ensure it's defined or remove this comment.
	// Given previous context, it's likely a custom struct or from ManagedCuda.VectorTypes.
	// For general .NET, System.Numerics.Vector2 is common.
	// I'll assume System.Numerics.Vector2 for this example.
	public struct Vector2
	{
		public float X;
		public float Y;

		public Vector2(float x, float y)
		{
			this.X = x;
			this.Y = y;
		}

		public float Length()
		{
			return (float) Math.Sqrt(this.X * this.X + this.Y * this.Y);
		}
	}
}