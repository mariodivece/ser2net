namespace Unosquare.Ser2Net.Services;

internal sealed class NetworkClient : IDisposable
{
    private readonly IMemoryOwner<byte> ReadBuffer;
    private readonly SemaphoreSlim AsyncRoot = new(1, 1);

    private bool _IsDisposed;
    private bool _IsConnected = true;

    public NetworkClient(ILogger<NetworkClient> logger, ServiceSettings settings, Socket socket)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(socket);
        ArgumentNullException.ThrowIfNull(settings);

        NetSocket = socket;
        Settings = settings;
        Logger = logger;

        // Configure the network client
        BufferSize = Math.Max(4096, Settings.BaudRate / 8);
        ReadBuffer = MemoryPool<byte>.Shared.Rent(BufferSize);
        NetSocket.NoDelay = true;
        NetSocket.ReceiveBufferSize = BufferSize;
        NetSocket.SendBufferSize = BufferSize;
        NetSocket.Blocking = false;
        RemoteEndPoint = NetSocket.RemoteEndPoint ?? Constants.EmptyEndPoint;
    }

    public EndPoint RemoteEndPoint { get; }

    public int BufferSize { get; }

    public bool IsConnected
    {
        get
        {
            var isConnectedState = _IsConnected
                && !_IsDisposed
                && NetSocket.Connected;

            if (!isConnectedState)
                return false;

            try
            {
                var isDisconnected = NetSocket.Poll(1000, SelectMode.SelectRead)
                    && NetSocket.Available <= 0;

                _IsConnected = !isDisconnected;
            }
            catch
            {
                _IsConnected = false;
            }

            return _IsConnected;
        }
    }

    private Socket NetSocket { get; }

    private ILogger<NetworkClient> Logger { get; }

    private ServiceSettings Settings { get; }

    public async ValueTask WriteAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (buffer.Length <= 0)
            return;

        var hasErrors = false;

        try
        {
            await AsyncRoot.WaitAsync(cancellationToken).ConfigureAwait(false);

            if (!IsConnected)
                throw new SocketException((int)SocketError.NotConnected);

            await NetSocket.SendAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            hasErrors = true;
            Logger.LogErrorWriting(RemoteEndPoint, ex.Message);
            Dispose(alsoManaged: true);
            throw;
        }
        finally
        {
            if (!hasErrors)
                AsyncRoot.Release();
        }
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken cancellationToken)
    {
        var hasErrors = false;
        try
        {
            await AsyncRoot.WaitAsync(cancellationToken).ConfigureAwait(false);
            var length = 0;

            if (!IsConnected)
                throw new SocketException((int)SocketError.NotConnected);

            while (NetSocket.Available > 0 && !cancellationToken.IsCancellationRequested)
            {
                var bytesRead = await NetSocket.ReceiveAsync(ReadBuffer.Memory[length..], cancellationToken);
                length += bytesRead;

                if (bytesRead <= 0 || length >= ReadBuffer.Memory.Length)
                    break;
            }

            return length == 0
                ? ReadOnlyMemory<byte>.Empty
                : ReadBuffer.Memory[..length];
        }
        catch (Exception ex)
        {
            hasErrors = true;
            Logger.LogErrorReading(RemoteEndPoint, ex.Message);
            Dispose(alsoManaged: true);
            throw;
        }
        finally
        {
            if (!hasErrors)
                AsyncRoot.Release();
        }
    }

    public void Dispose()
    {
        Dispose(alsoManaged: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool alsoManaged)
    {
        if (_IsDisposed) return;
        _IsConnected = false;
        _IsDisposed = true;

        if (alsoManaged)
        {
            _IsConnected = false;
            NetSocket.Close();
            ReadBuffer.Dispose();
            AsyncRoot.Dispose();
            Logger.LogClientDisconnected(RemoteEndPoint);
        }
    }

}
