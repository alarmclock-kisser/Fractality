﻿using Fractality.Core;
using OpenTK.Compute.OpenCL;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Fractality.OpenCl
{
	public class OpenClService
	{
		public string Repopath => Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Fractality.OpenCl"));


		public Dictionary<CLDevice, CLPlatform> Devices => this.GetDevices();

		public int INDEX { get; set; } = -1;
		public CLDevice? DEV { get; set; } = null;
		public CLPlatform? PLAT { get; set; } = null;
		public CLContext? CTX { get; set; } = null;



		private CLResultCode lastError = CLResultCode.Success;
		public string LastErrorMessage => this.lastError == CLResultCode.Success ? "No errors" : $"Last OpenCL error: {this.lastError}";


		// Event for UI updates
		public event Action? OnChange;


		// Current information
		public Dictionary<string, object> CurrentInfo => this.GetCurrentInfoAsync().Result;
		public List<ClMem> MemoryObjects => this.GetMemoryObjectsAsync().Result;
		public Dictionary<string, string> Kernels => this.GetKernelsAsync().Result;
		public Dictionary<string, IntPtr> MemoryStats => this.GetMemoryStatsAsync().Result;
		public  float MemoryUsagePercentage =>  this.GetMemoryUsagePercentageAsync().Result;


		// Objects
		public OpenClMemoryRegister? MemoryRegister { get; private set; }
		public OpenClKernelCompiler? KernelCompiler { get; private set; }
		public OpenClKernelExecutioner? KernelExecutioner { get; private set; }



		// Dispose
		public void Dispose(bool silent = false)
		{
			// Dispose context
			if (this.CTX != null)
			{
				CL.ReleaseContext(this.CTX.Value);
				this.PLAT = null;
				this.DEV = null;
				this.CTX = null;
			}

			// Dispose memory handling
			this.MemoryRegister?.Dispose();
			this.MemoryRegister = null; // Clear reference

			// Dispose kernel handling
			this.KernelExecutioner?.Dispose();
			this.KernelExecutioner = null; // Clear reference
			this.KernelCompiler?.Dispose();
			this.KernelCompiler = null; // Clear reference

			// Log
			if (!silent)
			{
				Console.WriteLine("Disposed OpenCL context and resources.");
			}
			OnChange?.Invoke();
		}




		// GET Devices & Platforms
		private CLPlatform[] GetPlatforms()
		{
			CLPlatform[] platforms = [];

			try
			{
				this.lastError = CL.GetPlatformIds(out platforms);
				if (this.lastError != CLResultCode.Success)
				{
					Console.WriteLine($"Error retrieving OpenCL platforms: {this.lastError}");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error retrieving OpenCL platforms: {ex.Message}");
			}

			return platforms;
		}

		public Dictionary<CLDevice, CLPlatform> GetDevices()
		{
			Dictionary<CLDevice, CLPlatform> devices = [];

			CLPlatform[] platforms = this.GetPlatforms();
			foreach (CLPlatform platform in platforms)
			{
				try
				{
					CLDevice[] platformDevices = [];
					this.lastError = CL.GetDeviceIds(platform, DeviceType.All, out platformDevices);
					if (this.lastError != CLResultCode.Success)
					{
						Console.WriteLine($"Error retrieving devices for platform {this.GetPlatformInfo<string>(platform)}: {this.lastError}");
						continue;
					}
					foreach (CLDevice device in platformDevices)
					{
						devices.Add(device, platform);
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error retrieving devices for platform {this.GetPlatformInfo<string>(platform)}: {ex.Message}");
				}
			}
			return devices;
		}

		public Dictionary<string, string> Names => this.GetNames();








		// Device & Platform info
		public string? GetDeviceInfo(CLDevice? device = null, DeviceInfo info = DeviceInfo.Name, bool silent = true)
		{
			// Verify device
			device ??= this.DEV;
			if (device == null)
			{
				if (!silent)
				{
					Console.WriteLine("No OpenCL device specified or currently initialized.");
				}
				return null;
			}

			this.lastError = CL.GetDeviceInfo(device.Value, info, out byte[] infoCode);
			if (this.lastError != CLResultCode.Success || infoCode == null || infoCode.LongLength == 0)
			{
				if (!silent)
				{
					Console.WriteLine($"Failed to get device info for '{info}': {this.lastError}. {(infoCode == null || infoCode.LongLength == 0 ? "No data returned." : "")}");
				}
				return null;
			}

			return Encoding.UTF8.GetString(infoCode).Trim('\0');
		}

		public string? GetPlatformInfo(CLPlatform? platform = null, PlatformInfo info = PlatformInfo.Name, bool silent = true)
		{
			// Verify platform
			platform ??= this.PLAT;
			if (platform == null)
			{
				if (!silent)
				{
					Console.WriteLine("No OpenCL platform specified or currently initialized.");
				}
				return null;
			}

			this.lastError = CL.GetPlatformInfo(platform.Value, info, out byte[] infoCode);
			if (this.lastError != CLResultCode.Success || infoCode == null || infoCode.LongLength == 0)
			{
				if (!silent)
				{
					Console.WriteLine($"Failed to get platform info for '{info}': {this.lastError}. {(infoCode == null || infoCode.LongLength == 0 ? "No data returned." : "")}");
				}
				return null;
			}
			
			return Encoding.UTF8.GetString(infoCode).Trim('\0');
		}

		public T? GetDeviceInfo<T>(CLDevice? device = null, DeviceInfo info = DeviceInfo.Name, bool silent = true)
		{
			// Verify device
			device ??= this.DEV;
			if (device == null)
			{
				if (!silent)
				{
					Console.WriteLine("No OpenCL device specified or currently initialized.");
				}
				return default;
			}

			this.lastError = CL.GetDeviceInfo(device.Value, info, out byte[] infoCode);
			if (this.lastError != CLResultCode.Success || infoCode == null || infoCode.LongLength == 0) // infoCode == null hinzugefügt
			{
				if (!silent)
				{
					Console.WriteLine($"Failed to get device info for '{info}': {this.lastError}. {(infoCode == null || infoCode.LongLength == 0 ? "No data returned." : "")}");
				}
				return default;
			}

			// Try-catch dynamic conversion from byte[] to T
			try
			{
				Type targetType = typeof(T);
				dynamic? result = null; // result initialisieren

				// 1. Versuch: Statische FromBytes(byte[]) Methode
				MethodInfo? fromBytesMethod = targetType.GetMethod(
					"FromBytes",
					BindingFlags.Public | BindingFlags.Static,
					null,
					[typeof(byte[])],
					null
				);

				if (fromBytesMethod != null && fromBytesMethod.ReturnType.IsAssignableTo(targetType))
				{
					try
					{
						result = fromBytesMethod.Invoke(null, [infoCode]);
					}
					catch (TargetInvocationException tie)
					{
						if (!silent)
						{
							Console.WriteLine($"Error calling FromBytes on '{targetType.Name}': {tie.InnerException?.Message ?? tie.Message}");
						}
					}
					catch (Exception ex)
					{
						if (!silent)
						{
							Console.WriteLine($"Error during FromBytes invocation for '{targetType.Name}': {ex.Message}");
						}
					}
				}
				else if (!silent && fromBytesMethod == null)
				{
					Console.WriteLine($"No static public 'FromBytes(byte[])' method found on type '{targetType.Name}'.");
				}
				else if (!silent && fromBytesMethod != null)
				{
					Console.WriteLine($"Warning: FromBytes method found for '{targetType.Name}' but its return type '{fromBytesMethod.ReturnType.Name}' is not assignable to '{targetType.Name}'.");
				}

				if (result == null) // Wenn FromBytes nicht erfolgreich war oder nicht existiert
				{
					// 2. Versuch: Statische TryParse(byte[], out T) Methode
					MethodInfo? tryParseMethod = targetType.GetMethod(
						"TryParse",
						BindingFlags.Public | BindingFlags.Static,
						null,
						[typeof(byte[]), targetType.MakeByRefType()],
						null
					);

					if (tryParseMethod != null && tryParseMethod.ReturnType == typeof(bool))
					{
						object?[] parameters = [infoCode, null]; // Parameters for TryParse
						try
						{
							bool success = (bool) (tryParseMethod.Invoke(null, parameters) ?? false);
							if (success)
							{
								result = parameters[1]; // Das out-Parameter-Ergebnis
							}
							else if (!silent)
							{
								Console.WriteLine($"TryParse method on '{targetType.Name}' returned false.");
							}
						}
						catch (TargetInvocationException tie)
						{
							if (!silent)
							{
								Console.WriteLine($"Error calling TryParse (byte[]) on '{targetType.Name}': {tie.InnerException?.Message ?? tie.Message}");
							}
						}
						catch (Exception ex)
						{
							if (!silent)
							{
								Console.WriteLine($"Error during TryParse (byte[]) invocation for '{targetType.Name}': {ex.Message}");
							}
						}
					}
					else if (!silent && tryParseMethod == null)
					{
						Console.WriteLine($"No static public 'TryParse(byte[], out T)' method found on type '{targetType.Name}'.");
					}

					if (result == null) // Wenn FromBytes und TryParse(byte[]) nicht erfolgreich waren
					{
						// 3. Versuch: Fallback: Konvertierung der Bytes zu string, dann Parse(string) oder TryParse(string)
						string strValue = Encoding.UTF8.GetString(infoCode).Trim('\0');

						// Spezielle Behandlung für Extensions
						if (info == DeviceInfo.Extensions)
						{
							strValue = string.Join(", ", strValue.Split('\0', StringSplitOptions.RemoveEmptyEntries));
						}

						if (string.IsNullOrEmpty(strValue))
						{
							if (!silent)
							{
								Console.WriteLine("Converted byte array to empty or null string; cannot parse.");
							}
							return default; // Keine Daten zum Parsen
						}

						// Versuch, eine statische Parse(string) oder TryParse(string, out T) Methode zu finden
						MethodInfo? parseMethod = targetType.GetMethods(BindingFlags.Public | BindingFlags.Static)
							.FirstOrDefault(m => m.Name == "Parse" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));

						MethodInfo? tryParseStringMethod = targetType.GetMethod(
							"TryParse",
							BindingFlags.Public | BindingFlags.Static,
							null,
							[typeof(string), targetType.MakeByRefType()],
							null
						);

						if (tryParseStringMethod != null && tryParseStringMethod.ReturnType == typeof(bool))
						{
							object?[] parameters = [strValue, null];
							try
							{
								bool success = (bool) (tryParseStringMethod.Invoke(null, parameters) ?? false);
								if (success)
								{
									result = parameters[1];
								}
								else if (!silent)
								{
									Console.WriteLine($"TryParse (string) method on '{targetType.Name}' returned false for string: '{strValue}'.");
								}
							}
							catch (TargetInvocationException tie)
							{
								if (!silent)
								{
									Console.WriteLine($"Error calling TryParse (string) on '{targetType.Name}': {tie.InnerException?.Message ?? tie.Message}");
								}
							}
							catch (Exception ex)
							{
								if (!silent)
								{
									Console.WriteLine($"Error during TryParse (string) invocation for '{targetType.Name}': {ex.Message}");
								}
							}
						}
						else if (parseMethod != null && parseMethod.GetParameters().Length == 1 && parseMethod.GetParameters()[0].ParameterType == typeof(string) && parseMethod.ReturnType.IsAssignableTo(targetType))
						{
							try
							{
								result = parseMethod.Invoke(null, [strValue]);
							}
							catch (TargetInvocationException tie)
							{
								if (!silent)
								{
									Console.WriteLine($"Error calling Parse on '{targetType.Name}': {tie.InnerException?.Message ?? tie.Message}");
								}
							}
							catch (Exception ex)
							{
								if (!silent)
								{
									Console.WriteLine($"Error during Parse invocation for '{targetType.Name}': {ex.Message}");
								}
							}
						}
						else if (!silent)
						{
							Console.WriteLine($"No suitable static public 'Parse(string)' or 'TryParse(string, out T)' method found on type '{targetType.Name}'.");

							// Letzter Fallback: Convert.ChangeType für primitive Typen, die direkt von string konvertierbar sind
							try
							{
								// Spezielle Behandlung für string und byte[] direkt (um Reflection zu vermeiden)
								if (targetType == typeof(string))
								{
									result = strValue;
								}
								else if (targetType == typeof(byte[]))
								{
									// Dies sollte bereits oben behandelt worden sein, aber zur Sicherheit
									result = infoCode;
								}
								else if (strValue != null)
								{
									result = Convert.ChangeType(strValue, targetType, System.Globalization.CultureInfo.InvariantCulture);
								}
							}
							catch (Exception ex)
							{
								if (!silent)
								{
									Console.WriteLine($"Error during Convert.ChangeType fallback for '{targetType.Name}': {ex.Message}");
								}
							}
						}
					}
				}

				return (T?) result;
			}
			catch (Exception ex)
			{
				if (!silent)
				{
					Console.WriteLine($"Unhandled error in GetDeviceInfo<T> for '{info}' to type '{typeof(T).Name}': {ex.Message}");
				}
				return default;
			}
		}

		public T? GetPlatformInfo<T>(CLPlatform? platform = null, PlatformInfo info = PlatformInfo.Name, bool silent = true)
		{
			// Verify platform
			platform ??= this.PLAT;
			if (platform == null)
			{
				if (!silent)
				{
					Console.WriteLine("No OpenCL platform specified or currently initialized.");
				}
				return default;
			}

			this.lastError = CL.GetPlatformInfo(platform.Value, info, out byte[] infoCode);
			if (this.lastError != CLResultCode.Success || infoCode == null || infoCode.LongLength == 0)
			{
				if (!silent)
				{
					Console.WriteLine($"Failed to get platform info for '{info}': {this.lastError}. {(infoCode == null || infoCode.LongLength == 0 ? "No data returned." : "")}");
				}
				return default;
			}

			try
			{
				Type targetType = typeof(T);
				dynamic? result = null;

				// 0. Spezielle Behandlung für string und byte[] als direkte Typen (Performance-Optimierung)
				if (targetType == typeof(string))
				{
					string stringResult = Encoding.UTF8.GetString(infoCode).Trim('\0');
					// Spezielle Behandlung für Extensions (Plattform-Extensions funktionieren ähnlich wie Geräte-Extensions)
					if (info == PlatformInfo.Extensions)
					{
						stringResult = string.Join(", ", stringResult.Split('\0', StringSplitOptions.RemoveEmptyEntries));
					}
					return (T) (object) stringResult;
				}
				if (targetType == typeof(byte[]))
				{
					return (T) (object) infoCode;
				}

				// 1. Versuch: Statische FromBytes(byte[]) Methode auf T
				MethodInfo? fromBytesMethod = targetType.GetMethod(
					"FromBytes",
					BindingFlags.Public | BindingFlags.Static,
					null,
					new Type[] { typeof(byte[]) },
					null
				);

				if (fromBytesMethod != null && fromBytesMethod.ReturnType.IsAssignableTo(targetType))
				{
					try
					{
						result = fromBytesMethod.Invoke(null, new object[] { infoCode });
					}
					catch (TargetInvocationException tie)
					{
						if (!silent)
						{
							Console.WriteLine($"Error calling FromBytes on '{targetType.Name}': {tie.InnerException?.Message ?? tie.Message}");
						}
					}
					catch (Exception ex)
					{
						if (!silent)
						{
							Console.WriteLine($"Error during FromBytes invocation for '{targetType.Name}': {ex.Message}");
						}
					}
				}
				else if (!silent && fromBytesMethod == null)
				{
					Console.WriteLine($"No static public 'FromBytes(byte[])' method found on type '{targetType.Name}'.");
				}
				else if (!silent && fromBytesMethod != null)
				{
					Console.WriteLine($"Warning: FromBytes method found for '{targetType.Name}' but its return type '{fromBytesMethod.ReturnType.Name}' is not assignable to '{targetType.Name}'.");
				}

				if (result == null) // Wenn FromBytes nicht erfolgreich war oder nicht existiert
				{
					// 2. Versuch: Statische TryParse(byte[], out T) Methode auf T
					MethodInfo? tryParseMethod = targetType.GetMethod(
						"TryParse",
						BindingFlags.Public | BindingFlags.Static,
						null,
						new Type[] { typeof(byte[]), targetType.MakeByRefType() },
						null
					);

					if (tryParseMethod != null && tryParseMethod.ReturnType == typeof(bool))
					{
						object?[] parameters = new object?[] { infoCode, null }; // Parameters for TryParse
						try
						{
							bool success = (bool) (tryParseMethod.Invoke(null, parameters) ?? false);
							if (success)
							{
								result = parameters[1]; // Das out-Parameter-Ergebnis
							}
							else if (!silent)
							{
								Console.WriteLine($"TryParse method on '{targetType.Name}' returned false.");
							}
						}
						catch (TargetInvocationException tie)
						{
							if (!silent)
							{
								Console.WriteLine($"Error calling TryParse (byte[]) on '{targetType.Name}': {tie.InnerException?.Message ?? tie.Message}");
							}
						}
						catch (Exception ex)
						{
							if (!silent)
							{
								Console.WriteLine($"Error during TryParse (byte[]) invocation for '{targetType.Name}': {ex.Message}");
							}
						}
					}
					else if (!silent && tryParseMethod == null)
					{
						Console.WriteLine($"No static public 'TryParse(byte[], out T)' method found on type '{targetType.Name}'.");
					}

					if (result == null) // Wenn FromBytes und TryParse(byte[]) nicht erfolgreich waren
					{
						// 3. Versuch: Fallback: Konvertierung der Bytes zu string, dann Parse(string) oder TryParse(string, out T)
						string strValue = Encoding.UTF8.GetString(infoCode).Trim('\0');

						// Spezielle Behandlung für Extensions (falls noch nicht oben bei typeof(string) behandelt)
						if (info == PlatformInfo.Extensions)
						{
							strValue = string.Join(", ", strValue.Split('\0', StringSplitOptions.RemoveEmptyEntries));
						}

						if (string.IsNullOrEmpty(strValue))
						{
							if (!silent)
							{
								Console.WriteLine("Converted byte array to empty or null string; cannot parse for platform info.");
							}
							return default; // Keine Daten zum Parsen
						}

						// Versuch, eine statische Parse(string) oder TryParse(string, out T) Methode zu finden
						MethodInfo? tryParseStringMethod = targetType.GetMethod(
							"TryParse",
							BindingFlags.Public | BindingFlags.Static,
							null,
							new Type[] { typeof(string), targetType.MakeByRefType() },
							null
						);

						MethodInfo? parseMethod = targetType.GetMethods(BindingFlags.Public | BindingFlags.Static)
							.FirstOrDefault(m => m.Name == "Parse" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));

						if (tryParseStringMethod != null && tryParseStringMethod.ReturnType == typeof(bool))
						{
							object?[] parameters = new object?[] { strValue, null };
							try
							{
								bool success = (bool) (tryParseStringMethod.Invoke(null, parameters) ?? false);
								if (success)
								{
									result = parameters[1];
								}
								else if (!silent)
								{
									Console.WriteLine($"TryParse (string) method on '{targetType.Name}' returned false for string: '{strValue}'.");
								}
							}
							catch (TargetInvocationException tie)
							{
								if (!silent)
								{
									Console.WriteLine($"Error calling TryParse (string) on '{targetType.Name}': {tie.InnerException?.Message ?? tie.Message}");
								}
							}
							catch (Exception ex)
							{
								if (!silent)
								{
									Console.WriteLine($"Error during TryParse (string) invocation for '{targetType.Name}': {ex.Message}");
								}
							}
						}
						else if (parseMethod != null && parseMethod.GetParameters().Length == 1 && parseMethod.GetParameters()[0].ParameterType == typeof(string) && parseMethod.ReturnType.IsAssignableTo(targetType))
						{
							try
							{
								result = parseMethod.Invoke(null, new object[] { strValue });
							}
							catch (TargetInvocationException tie)
							{
								if (!silent)
								{
									Console.WriteLine($"Error calling Parse on '{targetType.Name}': {tie.InnerException?.Message ?? tie.Message}");
								}
							}
							catch (Exception ex)
							{
								if (!silent)
								{
									Console.WriteLine($"Error during Parse invocation for '{targetType.Name}': {ex.Message}");
								}
							}
						}
						else if (!silent)
						{
							Console.WriteLine($"No suitable static public 'Parse(string)' or 'TryParse(string, out T)' method found on type '{targetType.Name}'.");

							try
							{
								if (strValue != null)
								{
									result = Convert.ChangeType(strValue, targetType, CultureInfo.InvariantCulture);
								}
							}
							catch (Exception ex)
							{
								if (!silent)
								{
									Console.WriteLine($"Error during Convert.ChangeType fallback for '{targetType.Name}': {ex.Message}");
								}
							}
						}
					}
				}

				return (T?) result;
			}
			catch (Exception ex)
			{
				if (!silent)
				{
					Console.WriteLine($"Unhandled error in GetPlatformInfo<T> for '{info}' to type '{typeof(T).Name}': {ex.Message}");
				}
				return default;
			}
		}

		public Dictionary<string, string> GetNames()
		{
			// Get all OpenCL devices & platforms
			Dictionary<CLDevice, CLPlatform> devicesPlatforms = this.Devices;

			// Create dictionary for device names and platform names
			Dictionary<string, string> names = [];

			// Iterate over devices
			foreach (CLDevice device in devicesPlatforms.Keys)
			{
				// Get device name
				string deviceName = this.GetDeviceInfo<string>(device, DeviceInfo.Name, true) ?? "N/A";

				// Get platform name
				string platformName = this.GetPlatformInfo<string>(devicesPlatforms[device], PlatformInfo.Name, true) ?? "N/A";

				// Add to dictionary
				names.Add(deviceName, platformName);
			}

			// Return names
			return names;
		}

		public Dictionary<string, object> GetFullDeviceInfo()
		{
			List<object> infoList = [];
			List<string> desc =
				[
					"Name", "Vendor", "Vendor id", "Address Bits", "Global memory size", "Local memory size",
					"Cache memory size",
					"Compute units", "Clock frequency", "Max. buffer size", "OpenCLC version", "Version",
					"Driver version"
				];

			if (this.DEV == null || this.PLAT == null)
			{
				Console.WriteLine("No OpenCL device or platform initialized.");
				return [];
			}

			infoList.Add(this.GetDeviceInfo<string>(this.DEV.Value, DeviceInfo.Name) ?? "N/A");
			infoList.Add(this.GetDeviceInfo<string>(this.DEV.Value, DeviceInfo.Vendor) ?? "N/A");
			infoList.Add(this.GetDeviceInfo<int>(this.DEV.Value, DeviceInfo.VendorId));
			infoList.Add(this.GetDeviceInfo<int>(this.DEV.Value, DeviceInfo.AddressBits));
			infoList.Add(this.GetDeviceInfo<long>(this.DEV.Value, DeviceInfo.GlobalMemorySize));
			infoList.Add(this.GetDeviceInfo<long>(this.DEV.Value, DeviceInfo.LocalMemorySize));
			infoList.Add(this.GetDeviceInfo<long>(this.DEV.Value, DeviceInfo.GlobalMemoryCacheSize));
			infoList.Add(this.GetDeviceInfo<int>(this.DEV.Value, DeviceInfo.MaximumComputeUnits));
			infoList.Add(this.GetDeviceInfo<long>(this.DEV.Value, DeviceInfo.MaximumClockFrequency));
			infoList.Add(this.GetDeviceInfo<long>(this.DEV.Value, DeviceInfo.MaximumConstantBufferSize));
			infoList.Add(this.GetDeviceInfo<Version>(this.DEV.Value, DeviceInfo.OpenClCVersion) ?? new());
			infoList.Add(this.GetDeviceInfo<Version>(this.DEV.Value, DeviceInfo.Version) ?? new());
			infoList.Add(this.GetDeviceInfo<Version>(this.DEV.Value, DeviceInfo.DriverVersion) ?? new());

			// Create dictionary with device info
			return desc.Zip(infoList, (key, value) => new KeyValuePair<string, object>(key, value)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
		}

		public Dictionary<string, object> GetFullPlatformInfo()
		{
			List<object> infoList = [];
			List<string> desc = ["Name", "Vendor", "Version", "Profile", "Extensions"];
			
			if (this.PLAT == null)
			{
				Console.WriteLine("No OpenCL platform initialized.");
				return [];
			}
			
			infoList.Add(this.GetPlatformInfo<string>(this.PLAT.Value, PlatformInfo.Name) ?? "N/A");
			infoList.Add(this.GetPlatformInfo<string>(this.PLAT.Value, PlatformInfo.Vendor) ?? "N/A");
			infoList.Add(this.GetPlatformInfo<Version>(this.PLAT.Value, PlatformInfo.Version) ?? new());
			infoList.Add(this.GetPlatformInfo<string>(this.PLAT.Value, PlatformInfo.Profile) ?? "N/A");
			infoList.Add(this.GetPlatformInfo<string>(this.PLAT.Value, PlatformInfo.Extensions) ?? "N/A");
			
			// Create dictionary with platform info
			return desc.Zip(infoList, (key, value) => new KeyValuePair<string, object>(key, value)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
		}


		// Initialize
		public void Initialize(int index = 0, bool silent = false)
		{
			this.Dispose(true);

			Dictionary<CLDevice, CLPlatform> devicesPlatforms = this.Devices;

			if (index < 0 || index >= devicesPlatforms.Count)
			{
				if (!silent)
				{
					Console.WriteLine("Invalid index for OpenCL device selection");
				}

				OnChange?.Invoke();
				return;
			}

			this.DEV = devicesPlatforms.Keys.ElementAt(index);
			this.PLAT = devicesPlatforms.Values.ElementAt(index);

			this.CTX = CL.CreateContext(0, [this.DEV.Value], 0, IntPtr.Zero, out this.lastError);
			if (this.lastError != CLResultCode.Success || this.CTX == null)
			{
				if (!silent)
				{
					Console.WriteLine($"Failed to create OpenCL context: {this.lastError}");
				}

				OnChange?.Invoke();
				return;
			}
			// Assuming CLCommandQueue is created within OpenClMemoryRegister constructor
			this.MemoryRegister = new OpenClMemoryRegister(this.Repopath, this.CTX.Value, this.DEV.Value, this.PLAT.Value);
			this.KernelCompiler = new OpenClKernelCompiler(this.Repopath, this.MemoryRegister, this.CTX.Value, this.DEV.Value, this.PLAT.Value, this.MemoryRegister.QUE);
			this.KernelExecutioner = new OpenClKernelExecutioner(this.Repopath, this.MemoryRegister, this.CTX.Value, this.DEV.Value, this.PLAT.Value, this.MemoryRegister.QUE, this.KernelCompiler);

			this.INDEX = index;

			if (!silent)
			{
				Console.WriteLine($"Initialized OpenCL context for device {this.GetDeviceInfo(this.DEV, DeviceInfo.Name) ?? "N/A"} on platform {this.GetPlatformInfo(this.PLAT, PlatformInfo.Name) ?? "N/A"}");
			}

			OnChange?.Invoke();
		}



		// Accessible methods
		public async Task<Dictionary<string, object>> GetCurrentInfoAsync()
		{
			Dictionary<string, object> info = [];

			try
			{
				info = this.GetFullDeviceInfo().ToArray().Concat(this.GetFullPlatformInfo().ToArray())
					.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error retrieving current OpenCL info: {ex.Message}");
			}
			finally
			{
				await Task.Yield();
			}

			return info;
		}

		public async Task<List<ClMem>> GetMemoryObjectsAsync(bool silent = true)
		{
			if (this.MemoryRegister == null)
			{
				if (!silent)
				{
					Console.WriteLine("Memory register is not initialized.");
				}
				return [];
			}
			try
			{
				return await Task.Run(() => this.MemoryRegister.Memory);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error retrieving memory objects: {ex.Message}");
				return [];
			}
		}

		public async Task<Dictionary<string, string>> GetKernelsAsync(bool silent = true)
		{
			if (this.KernelCompiler == null)
			{
				if (!silent)
				{
					Console.WriteLine("Kernel compiler is not initialized.");
				}
				return [];
			}
			try
			{
				return await Task.Run(() => this.KernelCompiler.Files);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error retrieving kernels: {ex.Message}");
				return [];
			}
		}

		public async Task<Dictionary<string, IntPtr>> GetMemoryStatsAsync(bool silent = true)
		{
			if (this.MemoryRegister == null)
			{
				if (!silent)
				{
					Console.WriteLine("Memory register is not initialized.");
				}
				return [];
			}
			try
			{
				// Get memory stats
				List<string> keys = ["Total", "Used", "Free"];
				List<IntPtr> values = [(nint) this.MemoryRegister.GetMemoryTotal(), (nint) this.MemoryRegister.GetMemoryUsed(), (nint) this.MemoryRegister.GetMemoryFree()];
				return keys.Zip(values, (key, value) => new KeyValuePair<string, IntPtr>(key, value)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error retrieving memory stats: {ex.Message}");
				return [];
			}
			finally
			{
				await Task.Yield();
			}
		}

		public async Task<float> GetMemoryUsagePercentageAsync(bool silent = true)
		{
			if (this.MemoryRegister == null)
			{
				if (!silent)
				{
					Console.WriteLine("Memory register is not initialized.");
				}
				return 0f;
			}
			try
			{
				// Get memory usage percentage
				var usage = await this.GetMemoryStatsAsync();
				if (usage.TryGetValue("Total", out IntPtr total) && usage.TryGetValue("Used", out IntPtr used) && total != IntPtr.Zero)
				{
					return (float)used.ToInt64() / total.ToInt64();
				}
				else
				{
					if (!silent)
					{
						Console.WriteLine("Memory stats are not available or invalid.");
					}
					return 0.0f;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error calculating memory usage percentage: {ex.Message}");
				return 0f;
			}
			finally
			{
				await Task.Yield();
			}
		}

		public async Task<long> GetDeviceScore(CLDevice? device = null, bool silent = true)
			{
			// Verify device
			device ??= this.DEV;
			if (device == null)
			{
				if (!silent)
				{
					Console.WriteLine("No OpenCL device specified or currently initialized.");
				}
				return 0;
			}
			try
			{
				int computeUnits = this.GetDeviceInfo<int>(device, DeviceInfo.MaximumComputeUnits, true);
				long clockFrequency = this.GetDeviceInfo<long>(device, DeviceInfo.MaximumClockFrequency, true);

				return computeUnits * clockFrequency;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error calculating device score: {ex.Message}");
				return -1;
			}
			finally
			{
				await Task.Yield();
			}
		}

		public async Task<int?> GetStrongestDeviceIndexAsync(DeviceType deviceType = DeviceType.All, bool silent = true)
		{
			var devices = this.Devices.Keys.ToList();
			if (devices.Count == 0)
			{
				if (!silent)
				{
					Console.WriteLine("No OpenCL devices found.");
				}
				return null;
			}

			try
			{
				Dictionary<CLDevice, DeviceType> deviceTypes = devices.ToList()
					.ToDictionary(d => d, d => this.GetDeviceInfo<DeviceType>(d, DeviceInfo.Type, true));

				// Filter devices by type
				List<CLDevice> filteredDevices;
				if (deviceType == DeviceType.All)
				{
					filteredDevices = devices;
				}
				else
				{
					filteredDevices = devices.Where(d => deviceTypes[d] == deviceType).ToList();
				}

				List<long> scores = filteredDevices.Select(d => this.GetDeviceScore(d).Result).ToList();

				if (scores.Count == 0)
				{
					if (!silent)
					{
						Console.WriteLine($"No devices found for type {deviceType}.");
					}
					return null;
				}

				// Find the index of the device with the highest score
				int strongestIndex = scores.IndexOf(scores.Max());

				return devices.IndexOf(filteredDevices[strongestIndex]);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error finding strongest device: {ex.Message}");
				return null;
			}
			finally
			{
				await Task.Yield();
			}
		}



		public async Task<IntPtr> MoveImage(ImageObj obj)
		{
			if (this.MemoryRegister == null)
			{
				Console.WriteLine("Memory register is not initialized.");
				return IntPtr.Zero;
			}
			try
			{
				// -> Device
				if (obj.OnHost)
				{
					byte[] pixels = obj.GetBytes(false);

					obj.Pointer = this.MemoryRegister.PushData<byte>(pixels)?.IndexHandle ?? obj.Pointer;
				}
				else if (obj.OnDevice)
				{
					byte[] bytes = this.MemoryRegister.PullData<byte>(obj.Pointer);
					
					obj.SetImage(bytes, false);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error moving image to OpenCL memory: {ex.Message}");
				return IntPtr.Zero;
			}
			finally
			{
				await Task.Yield();
			}

			return obj.Pointer;
		}

		public async Task<IntPtr> MoveAudio(AudioObj obj, int chunkSize = 16384, float overlap = 0.5f)
		{
			if (this.MemoryRegister == null)
			{
				Console.WriteLine("Memory register is not initialized.");
				return IntPtr.Zero;
			}
			try
			{
				List<float[]> chunks = [];

				// -> Device
				if (obj.OnHost)
				{
					chunks = obj.GetChunks(chunkSize, overlap, false);

					obj.Pointer = this.MemoryRegister.PushChunks<float>(chunks)?.IndexHandle ?? IntPtr.Zero;
				}
				else if (obj.OnDevice)
				{
					chunks = this.MemoryRegister.PullChunks<float>(obj.Pointer);

					obj.AggregateStretchedChunks(chunks, false);
				}
				else
				{
					Console.WriteLine("Error: AudioObj is neither on Host nor on Device.");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error moving audio to OpenCL memory: {ex.Message}");
				return IntPtr.Zero;
			}
			finally
			{
				await Task.Yield();
			}

			return obj.Pointer;
		}

		public async Task<IntPtr> ExecuteAudioKernel(AudioObj obj, string kernelBaseName = "normalize", string kernelVersion = "00", int chunkSize = 0, float overlap = 0.0f, Dictionary<string, object>? optionalArguments = null, bool log = false)
		{
			// Check executioner
			if (this.KernelExecutioner == null)
			{
				Console.WriteLine("Kernel executioner not initialized", "Cannot execute audio kernel");
				return IntPtr.Zero;
			}

			// Take time
			Stopwatch sw = Stopwatch.StartNew();

			// Optionally move audio to device
			bool moved = false;
			if (obj.OnHost)
			{
				await this.MoveAudio(obj, chunkSize, overlap);
				moved = true;
			}
			if (!obj.OnDevice)
			{
				if (log)
				{
					Console.WriteLine("Audio object is not on device", "Pointer=" + obj.Pointer.ToString("X16"), 1);
				}
				return IntPtr.Zero;
			}

			// Execute kernel on device
			obj.Pointer = this.KernelExecutioner.ExecuteAudioKernel(obj.Pointer, out double factor, obj.Length, kernelBaseName + kernelVersion, chunkSize, overlap, obj.Samplerate, obj.Bitdepth, obj.Channels, optionalArguments, log);
			if (obj.Pointer == IntPtr.Zero && log)
			{
				Console.WriteLine("Failed to execute audio kernel", "Pointer=" + obj.Pointer.ToString("X16"), 1);
			}

			// Reload kernel
			this.KernelCompiler?.LoadKernel(kernelBaseName + kernelVersion, "");

			// Log factor & set new bpm
			if (factor != 1.00f)
			{
				// IMPORTANT: Set obj Factor
				obj.StretchFactor = factor;
				obj.Bpm = (float) (obj.Bpm / factor);
				Console.WriteLine("Factor for audio kernel: " + factor, "Pointer=" + obj.Pointer.ToString("X16") + " BPM: " + obj.Bpm, 1);
			}

			// Move back optionally
			if (moved && obj.OnDevice && obj.Form == 'f')
			{
				await this.MoveAudio(obj, chunkSize, overlap);
			}

			if (log)
			{
				sw.Stop();
				Console.WriteLine("Executed audio kernel", "Pointer=" + obj.Pointer.ToString("X16") + ", Time: " + sw.ElapsedMilliseconds + "ms", 1);
			}

			return obj.Pointer;
		}

		public IntPtr ExecuteImageKernel(ImageObj obj, string kernelBaseName = "mandelbrot", string kernelVersion = "00", object[]? variableArguments = null, bool log = false)
		{
			// Verify obj on device
			bool moved = false;
			if (obj.OnHost)
			{
				if (log)
				{
					Console.WriteLine("Image was on host, pushing ...", obj.Width + " x " + obj.Height, 2);
				}

				// Get pixel bytes
				byte[] pixels = obj.GetBytes(false);
				if (pixels.LongLength == 0)
				{
					Console.WriteLine("Couldn't get byte[] from image object", "Aborting", 1);
					return IntPtr.Zero;
				}

				// Push pixels -> pointer
				obj.Pointer = this.MemoryRegister?.PushData<byte>(pixels)?.IndexHandle ?? IntPtr.Zero;
				if (obj.OnHost || obj.Pointer == IntPtr.Zero)
				{
					if (log)
					{
						Console.WriteLine("Couldn't get pointer after pushing pixels to device", pixels.LongLength.ToString("N0"), 1);
					}
					return IntPtr.Zero;
				}

				moved = true;
			}

			// Get parameters for call
			IntPtr pointer = obj.Pointer;
			int width = obj.Width;
			int height = obj.Height;
			int channels = obj.Channels;
			int bitdepth = obj.Bitdepth;

			// Call exec on image
			IntPtr outputPointer = this.KernelExecutioner?.ExecuteImageKernel(pointer, kernelBaseName + kernelVersion, width, height, channels, bitdepth, variableArguments, log) ?? IntPtr.Zero;
			if (outputPointer == IntPtr.Zero)
			{
				if (log)
				{
					Console.WriteLine("Couldn't get output pointer after kernel execution", "Aborting", 1);
				}
				return outputPointer;
			}

			// Set obj pointer
			obj.Pointer = outputPointer;

			// Optionally: Move back to host
			if (obj.OnDevice && moved)
			{
				// Pull pixel bytes
				byte[] pixels = this.MemoryRegister?.PullData<byte>(obj.Pointer) ?? [];
				if (pixels == null || pixels.LongLength == 0)
				{
					if (log)
					{
						Console.WriteLine("Couldn't pull pixels (byte[]) from device", "Aborting", 1);
					}
					return IntPtr.Zero;
				}

				// Aggregate image
				obj.SetImage(pixels, false);
			}

			return outputPointer;
		}

		public async Task<IntPtr> PerformFFT(AudioObj obj, int chunkSize = 0, float overlap = 0.0f, bool log = false)
		{
			// Optionally move audio to device
			if (obj.OnHost)
			{
				await this.MoveAudio(obj, chunkSize, overlap);
			}
			if (!obj.OnDevice)
			{
				if (log)
				{
					Console.WriteLine("Couldn't move audio object to device", "Pointer=" + obj.Pointer.ToString("X16"), 1);
				}
				return IntPtr.Zero;
			}

			// Perform FFT on device
			obj.Pointer = this.KernelExecutioner?.ExecuteFFT(obj.Pointer, obj.Form, chunkSize, overlap, true, log) ?? obj.Pointer;

			if (obj.Pointer == IntPtr.Zero && log)
			{
				Console.WriteLine("Failed to perform FFT", "Pointer=" + obj.Pointer.ToString("X16"), 1);
			}
			else
			{
				if (log)
				{
					Console.WriteLine("Performed FFT", "Pointer=" + obj.Pointer.ToString("X16"), 1);
				}
				obj.Form = obj.Form == 'f' ? 'c' : 'f';
			}

			return obj.Pointer;
		}

		public async Task<AudioObj> TimeStretch(AudioObj obj, string kernelName = "timestretch_double03", double factor = 1.000d, int chunkSize = 16384, float overlap = 0.5f)
		{
			if (this.KernelExecutioner == null)
			{
				Console.WriteLine("Kernel executioner is not initialized.");
				return obj;
			}
			try
			{
				// Optionally move obj to device
				bool moved = false;
				if (obj.OnHost)
				{
					IntPtr pointer = await this.MoveAudio(obj, chunkSize, overlap);
					if (pointer == IntPtr.Zero)
					{
						Console.WriteLine("Failed to move audio to device memory.");
						return obj;
					}
					moved = true;
				}

				// Get optional args
				Dictionary<string, object> optionalArgs;
				if (kernelName.ToLower().Contains("double"))
				{
					// Double kernel
					optionalArgs = new()
						{
							{ "factor", (double) factor }
						};
				}
				else
				{
					optionalArgs = new()
						{
							{ "factor", (float) factor }
						};
				}

				// Execute time stretch kernel
				var ptr  = await this.ExecuteAudioKernel(obj, kernelName, "", chunkSize, overlap, optionalArgs, true);
				if (ptr == IntPtr.Zero)
				{
					Console.WriteLine("Failed to execute time stretch kernel.", "Pointer=" + ptr.ToString("X16"));
					return obj;
				}

				// Optionally move obj back to host
				if (moved && obj.OnDevice)
				{
					IntPtr resultPointer = await this.MoveAudio(obj, chunkSize, overlap);
					if (resultPointer != IntPtr.Zero)
					{
						Console.WriteLine("Failed to move audio back to host memory.");
						return obj;
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error during time stretch: {ex.Message}");
			}
			finally
			{
				await Task.Yield();
			}

			return obj;
		}
	}
}
