using System;

using ILGPU;
using ILGPU.Runtime;

namespace ProvostXMemory.VectorIndex.Gpu;

/// <summary>
/// Owns the ILGPU Context and the resolved accelerator. The GPU hand-shake runs once,
/// lazily, on first access, inside a lock, and every failure mode (builder exception,
/// missing driver, no acceptable device, accelerator-creation failure) collapses to
/// IsAvailable=false. Nothing here throws for 'no GPU present'.
/// </summary>
public sealed class GpuAccelerator : IGpuAccelerator
{
    private readonly IGpuContextPolicy _contextPolicy;
    private readonly GpuDiscoveryOptions _discovery;
    private readonly GpuLaunchOptions _launchOptions;
    private readonly object _lock = new();

    private Context? _context;
    private Accelerator? _accelerator;
    private string _name = string.Empty;
    private string _deviceDescription = string.Empty;
    private bool _resolved;
    private bool _disposed;

    public GpuAccelerator()
        : this(DefaultGpuContextPolicy.Instance, new GpuDiscoveryOptions(), new GpuLaunchOptions())
    {
    }

    public GpuAccelerator(
        IGpuContextPolicy contextPolicy,
        GpuDiscoveryOptions discovery,
        GpuLaunchOptions launchOptions)
    {
        ArgumentNullException.ThrowIfNull(contextPolicy);
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(launchOptions);
        _contextPolicy = contextPolicy;
        _discovery = discovery;
        _launchOptions = launchOptions;
    }

    public GpuLaunchOptions LaunchOptions => _launchOptions;

    public bool IsAvailable
    {
        get
        {
            Resolve();
            return _accelerator is not null;
        }
    }

    public string Name
    {
        get
        {
            Resolve();
            return _name;
        }
    }

    public string DeviceDescription
    {
        get
        {
            Resolve();
            return _deviceDescription;
        }
    }

    public Accelerator RequireAccelerator()
    {
        Resolve();
        return _accelerator
            ?? throw new InvalidOperationException(
                "No GPU accelerator is available on this machine (IsAvailable=false).");
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _accelerator?.Dispose();
            _accelerator = null;
            _context?.Dispose();
            _context = null;
            _resolved = true;
            _disposed = true;
        }
    }

    private void Resolve()
    {
        lock (_lock)
        {
            if (_resolved || _disposed)
            {
                return;
            }

            _resolved = true;
            try
            {
                var context = _contextPolicy.CreateContext(_discovery);
                var device = _contextPolicy.SelectDevice(context, _discovery);
                if (device is null || !_contextPolicy.Accepts(device.AcceleratorType))
                {
                    context.Dispose();
                    _context = null;
                    return;
                }

                var accelerator = device.CreateAccelerator(context);
                _accelerator = accelerator;
                _context = context;
                _name = accelerator.AcceleratorType.ToString().ToLowerInvariant();
                _deviceDescription = $"{accelerator.AcceleratorType}: {accelerator.Name}";
            }
            catch (Exception)
            {
                // Driver/ICD/handshake failure: collapse to 'not available', never throw.
                _accelerator?.Dispose();
                _accelerator = null;
                _context?.Dispose();
                _context = null;
                _name = string.Empty;
                _deviceDescription = string.Empty;
            }
        }
    }
}