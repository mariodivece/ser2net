namespace Unosquare.Ser2Net;

internal sealed class SerialPortClient : IDisposable
{
    private bool isDisposed;
    private readonly IMemoryOwner<byte> readBuffer;
    private bool _IsConnected = true;

    private SerialPortClient(SerialPortServer server, TcpClient client)
    {
        NetworkClient = client;
        Server = server;
        Logger = server.Logger;

        // Configure the network client
        var bufferSize = Math.Max(4096, Server.Settings.BaudRate / 8);
        NetworkClient.NoDelay = true;
        NetworkClient.ReceiveBufferSize = bufferSize;
        NetworkClient.SendBufferSize = bufferSize;
        NetworkClient.Client.Blocking = false;
        readBuffer = MemoryPool<byte>.Shared.Rent(bufferSize);
        RemoteEndPoint = NetworkClient.Client.RemoteEndPoint ?? Constants.EmptyEndPoint;
    }

    public EndPoint RemoteEndPoint { get; }

    public bool IsConnected
    {
        get
        {
            var isConnectedState = _IsConnected
                && !isDisposed
                && NetworkClient.Connected
                && NetworkClient.Client.Connected;

            if (!isConnectedState)
                return false;

            try
            {
                var isDisconnected = NetworkClient.Client.Poll(1000, SelectMode.SelectRead)
                    && NetworkClient.Client.Available <= 0;

                _IsConnected = !isDisconnected;
            }
            catch
            {
                _IsConnected = false;
            }

            return _IsConnected;
        }
    }

    private SerialPortServer Server { get; set; }

    private TcpClient NetworkClient { get; set; }

    private ILogger<SerialPortServer> Logger { get; set; }

    public static async Task<SerialPortClient> WaitForClientAsync(SerialPortServer server, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(server);
        if (server.TcpServer is null)
            throw new InvalidOperationException($"{nameof(server)}.{nameof(server.TcpServer)} cannot be null.");

        var client = await server.TcpServer.AcceptTcpClientAsync(token).ConfigureAwait(false);
        return new SerialPortClient(server, client);
    }

    public async ValueTask WriteAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (buffer.Length <= 0)
            return;

        try
        {
            if (!IsConnected)
                throw new SocketException((int)SocketError.NotConnected);

            await NetworkClient.Client.SendAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError("Client [{EndPoint}] Could not write to network stream.\r\n{ErrorMessage}", RemoteEndPoint, ex.Message);
            Dispose(alsoManaged: true);
            throw;
        }
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var length = 0;

            if (!IsConnected)
                throw new SocketException((int)SocketError.NotConnected);

            while (NetworkClient.Available > 0 && !cancellationToken.IsCancellationRequested)
            {
                var bytesRead = await NetworkClient.Client.ReceiveAsync(readBuffer.Memory[length..], cancellationToken);
                length += bytesRead;

                if (bytesRead <= 0 || length >= readBuffer.Memory.Length)
                    break;
            }

            return length == 0
                ? ReadOnlyMemory<byte>.Empty
                : readBuffer.Memory[..length];
        }
        catch (Exception ex)
        {
            Logger.LogError("Client [{EndPoint}] Could not read from network stream.\r\n{ErrorMessage}", RemoteEndPoint, ex.Message);
            Dispose(alsoManaged: true);
            throw;
        }
    }

    public void Dispose()
    {
        Dispose(alsoManaged: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool alsoManaged)
    {
        if (isDisposed) return;
        _IsConnected = false;
        isDisposed = true;

        if (alsoManaged)
        {
            _IsConnected = false;
            NetworkClient.Close();
            readBuffer.Dispose();
            Logger.LogInformation("Client [{EndPoint}] Disconnected.", RemoteEndPoint);
        }
    }

}
