
using ManagedCuda;
using ManagedCuda.BasicTypes;
using ManagedCuda.NVRTC;
using ManagedCuda.VectorTypes;
using System.Diagnostics;

namespace AcceleratedAudio.Cuda
{
	public class CudaKernelHandling
	{
		// ----- ----- ATTRIBUTES ----- ----- \\
		private string Repopath;
		private PrimaryContext Context;
		private CudaMemoryHandling MemoryH;

		public CudaKernel? Kernel = null;
		public string? KernelName = null;
		public string? KernelFile = null;
		public string? KernelCode = null;


		public List<string> SourceFiles => this.GetCuFiles();
		public List<string> CompiledFiles => this.GetPtxFiles();


		private string KernelPath => Path.Combine(this.Repopath, "Kernels");

		// ----- ----- CONSTRUCTORS ----- ----- \\
		public CudaKernelHandling(string repopath, PrimaryContext context, CudaMemoryHandling memoryH)
		{
			this.Repopath = repopath;
			this.Context = context;
			this.MemoryH = memoryH;

			// Register events
			// this.KernelsCombo.SelectedIndexChanged += (s, e) => this.LoadKernel(this.KernelsCombo.SelectedItem?.ToString() ?? "");

			// Compile all kernels
			this.CompileAll(true, true);
		}




		// ----- ----- METHODS ----- ----- \\
		public string Log(string message = "", string inner = "", int indent = 0)
		{
			string indentString = new string('~', indent);
			string logMessage = $"[Krn] {indentString}{message} ({inner})";
			Console.WriteLine(logMessage);
			return logMessage;
		}


		public void Dispose()
		{
			// Dispose of kernels
			this.UnloadKernel();
		}

		public List<string> GetPtxFiles(string? path = null)
		{
			path ??= Path.Combine(this.KernelPath, "PTX");

			// Get all PTX files in kernel path
			string[] files = Directory.GetFiles(path, "*.ptx").Select(f => Path.GetFullPath(f)).ToArray();

			// Return files
			return files.ToList();
		}

		public List<string> GetCuFiles(string? path = null)
		{
			path ??= Path.Combine(this.KernelPath, "CU");

			// Get all CU files in kernel path
			string[] files = Directory.GetFiles(path, "*.cu").Select(f => Path.GetFullPath(f)).ToArray();

			// Return files
			return files.ToList();
		}

		public void CompileAll(bool silent = false, bool logErrors = false)
		{
			List<string> sourceFiles = this.SourceFiles;

			// Compile all source files
			foreach (string sourceFile in sourceFiles)
			{
				string? ptx = this.CompileKernel(sourceFile, silent);
				if (string.IsNullOrEmpty(ptx) && logErrors)
				{
					this.Log("Compilation failed: ", Path.GetFileNameWithoutExtension(sourceFile), 1);
				}
			}
		}

		public CudaKernel? LoadKernel(string kernelName, bool silent = false)
		{
			if (this.Context == null)
			{
				this.Log("No CUDA context available", "", 1);
				return null;
			}

			// Unload?
			if (this.Kernel != null)
			{
				this.UnloadKernel();
			}

			// Get kernel path
			string kernelPath = Path.Combine(this.KernelPath, "PTX", kernelName + ".ptx");

			// Get log path
			string logpath = Path.Combine(this.KernelPath, "Logs", kernelName + ".log");

			// Log
			Stopwatch sw = Stopwatch.StartNew();
			if (!silent)
			{
				this.Log("Started loading kernel " + kernelName);
			}

			// Try to load kernel
			try
			{
				// Load ptx code
				byte[] ptxCode = File.ReadAllBytes(kernelPath);

				string cuPath = Path.Combine(this.KernelPath, "CU", kernelName + ".cu");

				// Load kernel
				this.Kernel = this.Context.LoadKernelPTX(ptxCode, kernelName);
				this.KernelName = kernelName;
				this.KernelFile = kernelPath;
				this.KernelCode = File.ReadAllText(cuPath);
			}
			catch (Exception ex)
			{
				if (!silent)
				{
					this.Log("Failed to load kernel " + kernelName, ex.Message, 1);
					string logMsg = ex.Message + Environment.NewLine + Environment.NewLine + ex.InnerException?.Message ?? "";
					File.WriteAllText(logpath, logMsg);
				}
				this.Kernel = null;
			}

			// Log
			sw.Stop();
			long deltaMicros = sw.ElapsedTicks / (Stopwatch.Frequency / (1000L * 1000L));
			if (!silent)
			{
				this.Log("Kernel loaded within " + deltaMicros.ToString("N0") + " µs", "", 2);
			}

			return this.Kernel;
		}

		public void UnloadKernel()
		{
			// Unload kernel
			if (this.Kernel != null)
			{
				this.Context.UnloadKernel(this.Kernel);
				this.Kernel = null;
			}
		}

		public string? CompileKernel(string filepath, bool silent = false)
		{
			if (this.Context == null)
			{
				if (!silent)
				{
					this.Log("No CUDA available", "", 1);
				}
				return null;
			}

			// If file is not a .cu file, but raw kernel string, compile that
			if (Path.GetExtension(filepath) != ".cu")
			{
				return this.CompileString(filepath, silent);
			}

			string kernelName = Path.GetFileNameWithoutExtension(filepath);

			string logpath = Path.Combine(this.KernelPath, "Logs", kernelName + ".log");

			Stopwatch sw = Stopwatch.StartNew();
			if (!silent)
			{
				this.Log("Compiling kernel '" + kernelName + "'");
			}

			// Load kernel file
			string kernelCode = File.ReadAllText(filepath);


			CudaRuntimeCompiler rtc = new(kernelCode, kernelName);

			try
			{
				// Compile kernel
				rtc.Compile([]);
				string log = rtc.GetLogAsString();

				if (log.Length > 0)
				{
					// Count double \n
					int count = log.Split(["\n\n"], StringSplitOptions.None).Length - 1;
					if (!silent)
					{
						this.Log("Compiled with warnings", count.ToString(), 1);
					}
					File.WriteAllText(logpath, rtc.GetLogAsString());
				}

				sw.Stop();
				long deltaMicros = sw.ElapsedTicks / (Stopwatch.Frequency / (1000L * 1000L));
				if (!silent)
				{
					this.Log("Compiled within " + deltaMicros + " µs", "Repo\\" + Path.GetRelativePath(this.Repopath, logpath), 1);
				}

				// Get ptx code
				byte[] ptxCode = rtc.GetPTX();

				// Export ptx
				string ptxPath = Path.Combine(this.KernelPath, "PTX", kernelName + ".ptx");
				File.WriteAllBytes(ptxPath, ptxCode);

				if (!silent)
				{
					this.Log("PTX exported", ptxPath, 1);
				}

				return ptxPath;
			}
			catch (Exception ex)
			{
				File.WriteAllText(logpath, rtc.GetLogAsString());
				this.Log(ex.Message, ex.InnerException?.Message ?? "", 1);

				return null;
			}

		}

		public string? CompileString(string kernelString, bool silent = false)
		{
			if (this.Context == null)
			{
				if (!silent)
				{
					this.Log("No CUDA available", "", 1);
				}
				return null;
			}

			string kernelName = kernelString.Split("void ")[1].Split("(")[0];

			string logpath = Path.Combine(this.KernelPath, "Logs", kernelName + ".log");

			Stopwatch sw = Stopwatch.StartNew();
			if (!silent)
			{
				this.Log("Compiling kernel '" + kernelName + "'");
			}

			// Load kernel file
			string kernelCode = kernelString;

			// Save also the kernel string as .c file
			string cPath = Path.Combine(this.KernelPath, "CU", kernelName + ".cu");
			File.WriteAllText(cPath, kernelCode);


			CudaRuntimeCompiler rtc = new(kernelCode, kernelName);

			try
			{
				// Compile kernel
				rtc.Compile([]);
				string log = rtc.GetLogAsString();

				if (log.Length > 0)
				{
					// Count double \n
					int count = log.Split(["\n\n"], StringSplitOptions.None).Length - 1;
					if (!silent)
					{
						this.Log("Compiled with warnings", count.ToString(), 1);
					}
					File.WriteAllText(logpath, rtc.GetLogAsString());
				}


				sw.Stop();
				long deltaMicros = sw.ElapsedTicks / (Stopwatch.Frequency / (1000L * 1000L));
				if (!silent)
				{
					this.Log("Compiled within " + deltaMicros + " µs", "Repo\\" + Path.GetRelativePath(this.Repopath, logpath), 1);
				}


				// Get ptx code
				byte[] ptxCode = rtc.GetPTX();

				// Export ptx
				string ptxPath = Path.Combine(this.KernelPath, "PTX", kernelName + ".ptx");
				File.WriteAllBytes(ptxPath, ptxCode);

				if (!silent)
				{
					this.Log("PTX exported", ptxPath, 1);
				}

				return ptxPath;
			}
			catch (Exception ex)
			{
				File.WriteAllText(logpath, rtc.GetLogAsString());
				this.Log(ex.Message, ex.InnerException?.Message ?? "", 1);

				return null;
			}
		}

		public string? PrecompileKernelString(string kernelString, bool silent = false)
		{
			// Check contains "extern c"
			if (!kernelString.Contains("extern \"C\""))
			{
				if (!silent)
				{
					this.Log("Kernel string does not contain 'extern \"C\"'", "", 1);
				}
				return null;
			}

			// Check contains "__global__ "
			if (!kernelString.Contains("__global__"))
			{
				if (!silent)
				{
					this.Log("Kernel string does not contain '__global__'", "", 1);
				}
				return null;
			}

			// Check contains "void "
			if (!kernelString.Contains("void "))
			{
				if (!silent)
				{
					this.Log("Kernel string does not contain 'void '", "", 1);
				}
				return null;
			}

			// Check contains int
			if (!kernelString.Contains("int ") && !kernelString.Contains("long "))
			{
				if (!silent)
				{
					this.Log("Kernel string does not contain 'int ' (for array length)", "", 1);
				}
				return null;
			}

			// Check if every bracket is closed (even amount) for {} and () and []
			int open = kernelString.Count(c => c == '{');
			int close = kernelString.Count(c => c == '}');
			if (open != close)
			{
				if (!silent)
				{
					this.Log("Kernel string has unbalanced brackets", " { } ", 1);
				}
				return null;
			}
			open = kernelString.Count(c => c == '(');
			close = kernelString.Count(c => c == ')');
			if (open != close)
			{
				if (!silent)
				{
					this.Log("Kernel string has unbalanced brackets", " ( ) ", 1);
				}
				return null;
			}
			open = kernelString.Count(c => c == '[');
			close = kernelString.Count(c => c == ']');
			if (open != close)
			{
				if (!silent)
				{
					this.Log("Kernel string has unbalanced brackets", " [ ] ", 1);
				}
				return null;
			}

			// Check if kernel contains "blockIdx.x" and "blockDim.x" and "threadIdx.x"
			if (!kernelString.Contains("blockIdx.x") || !kernelString.Contains("blockDim.x") || !kernelString.Contains("threadIdx.x"))
			{
				if (!silent)
				{
					this.Log("Kernel string should contain 'blockIdx.x', 'blockDim.x' and 'threadIdx.x'", "", 2);
				}
			}

			// Get name between "void " and "("
			int start = kernelString.IndexOf("void ") + "void ".Length;
			int end = kernelString.IndexOf("(", start);
			string name = kernelString.Substring(start, end - start);

			// Trim every line ends from empty spaces (split -> trim -> aggregate)
			kernelString = kernelString.Split("\n").Select(x => x.TrimEnd()).Aggregate((x, y) => x + "\n" + y);

			// Log name
			if (!silent)
			{
				this.Log("Succesfully precompiled kernel string", "Name: " + name, 1);
			}

			return name;
		}

		public IntPtr ExecuteKernel(IntPtr pointer, int width, int height, int channels, int bitdepth, object[] arguments, bool silent = false)
		{
			// Check if kernel is loaded
			if (this.Kernel == null)
			{
				if (!silent)
				{
					this.Log("Kernel not loaded", this.KernelName ?? "N/A", 1);
				}

				return pointer;
			}

			// Get arguments
			Dictionary<string, Type> args = this.GetArguments(null, silent);

			// Get pointer
			CUdeviceptr devicePtr = new(pointer);

			// Allocate output buffer
			CUdeviceptr outputPtr = new(this.MemoryH.AllocateBuffer<byte>([width * height * ((channels * bitdepth) / 8)], silent));

			// Merge arguments with invariables
			object[] kernelArgs = this.MergeArguments(devicePtr, outputPtr, width, height, channels, bitdepth, arguments, silent);

			// Für ein 4-Kanal-Bild (RGBA): pixelIndex = (y * width + x) * 4;
			int totalThreadsX = width;
			int totalThreadsY = height;

			// Blockgröße (z. B. 16×16 Threads pro Block)
			int blockSizeX = 8;
			int blockSizeY = 8;

			// Gridgröße = Gesamtgröße / Blockgröße (aufrunden)
			int gridSizeX = (totalThreadsX + blockSizeX - 1) / blockSizeX;
			int gridSizeY = (totalThreadsY + blockSizeY - 1) / blockSizeY;

			this.Kernel.BlockDimensions = new dim3(blockSizeX, blockSizeY, 1);  // 2D-Block
			this.Kernel.GridDimensions = new dim3(gridSizeX, gridSizeY, 1);     // 2D-Grid


			// Run with arguments
			this.Kernel.Run(kernelArgs);

			if (!silent)
			{
				this.Log("Kernel executed", this.KernelName ?? "N/A", 1);
			}

			// Free input buffer if outputPointer != 0
			if (outputPtr.Pointer != 0)
			{
				this.MemoryH.FreeBuffer(devicePtr.Pointer);
			}

			// Synchronize
			this.Context.Synchronize();

			// Return pointer
			return outputPtr.Pointer != 0 ? outputPtr.Pointer : pointer;
		}

		public Type GetArgumentType(string typeName)
		{
			// Pointers are always IntPtr (containing *)
			if (typeName.Contains("*"))
			{
				return typeof(IntPtr);
			}

			string typeIdentifier = typeName.Split(' ').LastOrDefault()?.Trim() ?? "void";
			Type type = typeIdentifier switch
			{
				"int" => typeof(int),
				"float" => typeof(float),
				"double" => typeof(double),
				"char" => typeof(char),
				"bool" => typeof(bool),
				"void" => typeof(void),
				"byte" => typeof(byte),
				_ => typeof(void)
			};

			return type;
		}

		public Dictionary<string, Type> GetArguments(string? kernelCode = null, bool silent = false)
		{
			kernelCode ??= this.KernelCode;
			if (string.IsNullOrEmpty(kernelCode) || this.Kernel == null)
			{
				if (!silent)
				{
					this.Log("Kernel code is empty", this.KernelName ?? "N/A", 1);
				}
				return [];
			}

			Dictionary<string, Type> arguments = [];

			int index = kernelCode.IndexOf("__global__ void");
			if (index == -1)
			{
				if (!silent)
				{
					this.Log($"'__global__ void' not found", this.KernelName ?? "N/A", 1);
				}
				return [];
			}

			index = kernelCode.IndexOf("(", index);
			if (index == -1)
			{
				if (!silent)
				{
					this.Log($"'(' not found", this.KernelName ?? "N/A", 1);
				}
				return [];
			}

			int endIndex = kernelCode.IndexOf(")", index);
			if (endIndex == -1)
			{
				if (!silent)
				{
					this.Log($"')' not found", this.KernelName ?? "N/A", 1);
				}
				return [];
			}

			string[] args = kernelCode.Substring(index + 1, endIndex - index - 1).Split(',').Select(x => x.Trim()).ToArray();

			// Get loaded kernels function args
			for (int i = 0; i < args.Length; i++)
			{
				string name = args[i].Split(' ').LastOrDefault() ?? "N/A";
				string typeName = args[i].Replace(name, "").Trim();
				Type type = this.GetArgumentType(typeName);

				// Add to dictionary
				arguments.Add(name, type);
			}

			return arguments;
		}

		public object[] MergeArguments(CUdeviceptr inputPointer, CUdeviceptr outputPointer, int width, int height, int channels, int bitdepth, object[] arguments, bool silent = false)
		{
			// Get kernel argument definitions
			Dictionary<string, Type> args = this.GetArguments(null, silent);

			// Create array for kernel arguments
			object[] kernelArgs = new object[args.Count];

			int pointersCount = 0;
			// Integrate invariables if name fits (contains)
			for (int i = 0; i < kernelArgs.Length; i++)
			{
				string name = args.ElementAt(i).Key;
				Type type = args.ElementAt(i).Value;

				if (pointersCount == 0 && type == typeof(IntPtr))
				{
					kernelArgs[i] = inputPointer;
					pointersCount++;

					if (!silent)
					{
						this.Log($"In-pointer: <{inputPointer}>", "", 1);
					}
				}
				else if (pointersCount == 1 && type == typeof(IntPtr))
				{
					kernelArgs[i] = outputPointer;
					pointersCount++;

					if (!silent)
					{
						this.Log($"Out-pointer: <{outputPointer}>", "", 1);
					}
				}
				else if (name.Contains("width") && type == typeof(int))
				{
					kernelArgs[i] = width;
					
					if (!silent)
					{
						this.Log($"Width: [{width}]", "", 1);
					}
				}
				else if (name.Contains("height") && type == typeof(int))
				{
					kernelArgs[i] = height;

					if (!silent)
					{
						this.Log($"Height: [{height}]", "", 1);
					}
				}
				else if (name.Contains("chan") && type == typeof(int))
				{
					kernelArgs[i] = channels;

					if (!silent)
					{
						this.Log($"Channels: [{channels}]", "", 1);
					}
				}
				else if (name.Contains("bit") && type == typeof(int))
				{
					kernelArgs[i] = bitdepth;

					if (!silent)
					{
						this.Log($"Bits: [{bitdepth}]", "", 1);
					}
				}
				else
				{
					// Check if argument is in arguments array
					for (int j = 0; j < arguments.Length; j++)
					{
						if (name == args.ElementAt(j).Key)
						{
							kernelArgs[i] = arguments[j];
							break;
						}
					}

					// If not found, set to 0
					if (kernelArgs[i] == null)
					{
						kernelArgs[i] = 0;
					}
				}
			}

			// DEBUG LOG
			//this.Log("Kernel arguments: " + string.Join(", ", kernelArgs.Select(x => x.ToString())), "", 1);

			// Return kernel arguments
			return kernelArgs;
		}



		


	}
}