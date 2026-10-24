using System.Buffers.Binary;
using HIDMaestro;

namespace Sunshine.Ds5Sidecar;

/// <summary>
/// A virtual Steam Controller (2026) driven by the raw HID reports of the client's
/// physical controller. Unlike <see cref="ControllerSession"/> nothing is translated:
/// the persona carries the controller's own frames, so Steam Input sees both trackpads,
/// the grips and the capacitive sticks, and Steam's output reports (trackpad haptics,
/// rumble) go back to the client verbatim.
/// </summary>
internal sealed class SteamControllerSession : IDisposable
{
    internal const string ProfileId = "steam-controller-2";
    private const byte StateReportId = 0x42;

    private readonly object _submitLock = new();
    private readonly HMController _controller;
    private readonly Action<Protocol.Message> _emit;
    private int _disposed;

    internal SteamControllerSession(byte deviceId,
                                    byte clientControllerNumber,
                                    HMController controller,
                                    Action<Protocol.Message> emit)
    {
        DeviceId = deviceId;
        ClientControllerNumber = clientControllerNumber;
        _controller = controller;
        _emit = emit;
        _controller.OutputReceived += OnOutputReceived;
    }

    internal byte DeviceId { get; }
    internal byte ClientControllerNumber { get; }

    /// <summary>Submits one client report. Payload: id:u8, reserved:u8, length:u16, report.</summary>
    internal void SubmitRaw(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 4)
            throw new InvalidDataException("Invalid raw input payload");
        var length = BinaryPrimitives.ReadUInt16LittleEndian(payload[2..]);
        if (length == 0 || payload.Length != 4 + length)
            throw new InvalidDataException("Invalid raw input payload");

        // Only the controller state reaches the persona. It repeats its latest frame on
        // the device's own clock, so a battery, wireless-status or lizard-mode report
        // would take the state's place instead of passing through.
        var report = payload.Slice(4, length);
        if (report[0] != StateReportId)
            return;

        lock (_submitLock)
        {
            if (_disposed == 0)
                _controller.SubmitRawExtendedReport(report);
        }
    }

    private void OnOutputReceived(HMController controller, HMOutputPacket packet)
    {
        if (packet.Source != HMOutputSource.HidOutput)
            return;

        // id:u8, controller:u8, length:u16, report (report ID first)
        var payload = new byte[5 + packet.Data.Length];
        payload[0] = DeviceId;
        payload[1] = ClientControllerNumber;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2), (ushort)(1 + packet.Data.Length));
        payload[4] = packet.ReportId;
        packet.Data.Span.CopyTo(payload.AsSpan(5));
        _emit(new Protocol.Message(Protocol.MessageType.RawOutput, 0, payload));
    }

    public void Dispose()
    {
        lock (_submitLock)
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
        }
        _controller.OutputReceived -= OnOutputReceived;
        _controller.Dispose();
    }
}
