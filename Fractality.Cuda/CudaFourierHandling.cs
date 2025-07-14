using ManagedCuda;
using ManagedCuda.BasicTypes;
using ManagedCuda.CudaFFT;
using ManagedCuda.VectorTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcceleratedAudio.Cuda
{
	public class CudaFourierHandling
	{
		public string Repopath { get; private set; }
		public PrimaryContext Context { get; private set; }
		public CudaMemoryHandling MemoryH { get; private set; }


		public CudaFourierHandling(string repopath, PrimaryContext context, CudaMemoryHandling memoryH)
		{
			// Set attributes
			this.Repopath = repopath;
			this.Context = context;
			this.MemoryH = memoryH;
		}

		public string Log(string message = "", string inner = "", int indent = 0)
		{
			string indentString = new string('~', indent);
			string logMessage = $"[Fourier] {indentString}{message} ({inner})";
			Console.WriteLine(logMessage);
			return logMessage;
		}

		public void Dispose()
		{
			// Dispose plans etc.
		}

		public CudaMem? PerformFft(IntPtr inputPointer)
		{
			// Get memory obj by pointer
			CudaMem? inputObj = this.MemoryH.GetBuffer(inputPointer);
			if (inputObj == null || inputObj.Count <= 0)
			{
				// Log error
				this.Log("Couldn't find input buffer", "<" + inputPointer + ">", 1);
				return null;
			}

			// Get direction by type
			var direction = inputObj.Type == typeof(float) ? cufftType.R2C : inputObj.Type == typeof(float2) ? cufftType.C2R : cufftType.Z2Z;

			// Get plan
			CudaFFTPlan1D plan = new(
				(int)inputObj.IndexLength,
				direction,
				1
			);

			// Create output memory obj
			CudaMem? outputObj;
			if (inputObj.Type == typeof(float))
			{
				// Complex output for R2C
				outputObj = this.MemoryH.GetBuffer(this.MemoryH.AllocateBuffer<float2>(inputObj.Lengths));
			}
			else if (inputObj.Type == typeof(float2))
			{
				// Real output for C2R
				outputObj = this.MemoryH.GetBuffer(this.MemoryH.AllocateBuffer<float>(inputObj.Lengths));
			}
			else
			{
				// Abort for unsupported types
				this.Log("Unsupported type for FFT", inputObj.Type.Name, 1);
				return inputObj;
			}

			// Check outputObj
			if (outputObj == null || outputObj.Count <= 0)
			{
				this.Log("Couldn't create output buffer", "<" + inputPointer + ">", 1);
				return inputObj;
			}

			// Perform FFT on each pointer (inputObj -> outputObj)
			for (int i = 0; i < inputObj.Count; i++)
			{
				CUdeviceptr inputPtr = new(inputObj.Pointers[i]);
				CUdeviceptr outputPtr = new(outputObj.Pointers[i]);
				
				// Execute FFT
				plan.Exec(inputPtr, outputPtr);
			}

			// Dispose plan
			plan.Dispose();

			// Log success
			this.Log("FFT performed successfully", "<" + inputPointer + ">", 1);

			return outputObj;
		}
	}
}
