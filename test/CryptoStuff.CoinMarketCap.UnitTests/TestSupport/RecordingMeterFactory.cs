using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace CryptoStuff.CoinMarketCap.UnitTests.TestSupport;

/// <summary>
/// An <see cref="IMeterFactory"/> that records every <see cref="long"/>
/// measurement taken on the meters it created, and only those. Each meter is
/// stamped with this factory as its <see cref="Meter.Scope"/>, and the
/// listener enables an instrument only when its meter's scope is this
/// factory, so tests running in parallel — each with its own factory, all
/// using the same meter and instrument names — never see one another's
/// measurements. Measurements are delivered synchronously on the recording
/// thread, so they are visible as soon as the call under test returns.
/// </summary>
internal sealed class RecordingMeterFactory : IMeterFactory
{
    private readonly MeterListener _listener = new();
    private readonly ConcurrentQueue<RecordedMeasurement> _measurements = new();
    private readonly ConcurrentQueue<Meter> _meters = new();

    public RecordingMeterFactory()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter.Scope, this))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            _measurements.Enqueue(new RecordedMeasurement(
                instrument.Meter.Name,
                instrument.Name,
                instrument.Unit,
                value,
                tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value))));
        _listener.Start();
    }

    /// <summary>Every measurement recorded so far, in recording order.</summary>
    public IReadOnlyList<RecordedMeasurement> Measurements => [.. _measurements];

    public Meter Create(MeterOptions options)
    {
        var meter = new Meter(new MeterOptions(options.Name)
        {
            Version = options.Version,
            Tags = options.Tags,
            Scope = this,
        });
        _meters.Enqueue(meter);
        return meter;
    }

    public void Dispose()
    {
        _listener.Dispose();
        foreach (var meter in _meters)
        {
            meter.Dispose();
        }
    }
}

/// <summary>One measurement as <see cref="RecordingMeterFactory"/> saw it.</summary>
internal sealed record RecordedMeasurement(
    string MeterName,
    string InstrumentName,
    string? Unit,
    long Value,
    IReadOnlyDictionary<string, object?> Tags);
