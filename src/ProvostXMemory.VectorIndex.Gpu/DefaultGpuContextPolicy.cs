using System;
using System.Collections.Generic;

using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;

namespace ProvostXMemory.VectorIndex.Gpu;/// <summary>
/// Default policy: GPU-first discovery (CUDA/OpenCL per options), optional CPU fallback
/// acceptance (off by default — availability must reflect real GPUs only). Device
/// selection prefers CUDA > OpenCL and never throws when none exist.
/// </summary>
public sealed class DefaultGpuContextPolicy : IGpuContextPolicy
{
    public static readonly DefaultGpuContextPolicy Instance = new();

    public DefaultGpuContextPolicy()
        : this(allowCpuFallback: false)
    {
    }

    public DefaultGpuContextPolicy(bool allowCpuFallback)
    {
        AllowCpuFallback = allowCpuFallback;
    }

    /// <summary>Whether the CPU accelerator simulators count as available devices.</summary>
    public bool AllowCpuFallback { get; }

    public bool Accepts(AcceleratorType acceleratorType)
    {
        return acceleratorType switch
        {
            AcceleratorType.Cuda => true,
            AcceleratorType.OpenCL => true,
            AcceleratorType.CPU => AllowCpuFallback,
            _ => false,
        };
    }

    public Context CreateContext(GpuDiscoveryOptions discovery)
    {
        return Context.Create(builder => Configure(builder, discovery));
    }

    private Context.Builder Configure(Context.Builder builder, GpuDiscoveryOptions discovery)
    {
        if (discovery.TryCuda)
        {
            builder = builder.Cuda();
        }

        if (discovery.TryOpenCl)
        {
            builder = builder.OpenCL();
        }

        if (AllowCpuFallback)
        {
            builder = builder.CPU();
        }

        return builder;
    }

    public Device? SelectDevice(Context context, GpuDiscoveryOptions discovery)
    {
        if (discovery.TryCuda)
        {
            foreach (var device in SafeDevices(() => DowncastDevices<CudaDevice>(context.GetCudaDevices())))
            {
                return device;
            }
        }

        if (discovery.TryOpenCl)
        {
            foreach (var device in SafeDevices(() => DowncastDevices<CLDevice>(context.GetCLDevices())))
            {
                return device;
            }
        }

        if (AllowCpuFallback)
        {
            foreach (var device in SafeDevices(() => DowncastDevices<CPUDevice>(context.GetCPUDevices())))
            {
                return device;
            }
        }

        return null;
    }

    /// <summary>Iterates any ILGPU DeviceCollection into a base-typed list (struct enumerator, no covariance).</summary>
    private static List<Device> DowncastDevices<TDevice>(ILGPU.Context.DeviceCollection<TDevice> devices)
        where TDevice : Device
    {
        var result = new List<Device>(devices.Count);
        for (var i = 0; i < devices.Count; i++)
        {
            result.Add(devices[i]);
        }

        return result;
    }

    private static IEnumerable<Device> SafeDevices(Func<IEnumerable<Device>> enumerate)
    {
        IEnumerable<Device> devices;
        try
        {
            devices = enumerate();
        }
        catch (Exception)
        {
            // No driver / no ICD on this machine: treat as 'no devices' (availability=false).
            yield break;
        }

        foreach (var device in devices)
        {
            yield return device;
        }
    }
}