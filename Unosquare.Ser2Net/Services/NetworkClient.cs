namespace Unosquare.Ser2Net.Services;


/// <summary>
/// A network client that can send and receive data using TCP sockets.
/// This class cannot be inherited.
/// </summary>
internal sealed class NetworkClient : IDisposable
{
    private const int DefaultReadBufferSize = 8192;
    private readonly MemoryBlock<byte> ReadBuffer;
    private long _IsDisposed;
    private bool _IsConnected = true;
    private readonly SemaphoreSlim AsyncRoot = new(1, 1);

    public NetworkClient(ILogger<NetworkClient> logger, ServiceSettings settings, Socket socket)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(socket);
        ArgumentNullException.ThrowIfNull(settings);

        NetSocket = socket;
        Settings = settings;
        Logger = logger;

        // Configure the network client
        BufferSize = Math.Max(DefaultReadBufferSize, Settings.BaudRate / 8);
        ReadBuffer = new(BufferSize);
        NetSocket.ReceiveBufferSize = BufferSize;
        NetSocket.SendBufferSize = BufferSize;
        NetSocket.NoDelay = true;
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
                && Interlocked.Read(ref _IsDisposed) == 0
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

    public async ValueTask SendAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var pendingWriteCount = buffer.Length;
        if (pendingWriteCount <= 0)
            return;

        try
        {
            var maxChunkSize = Math.Min(NetSocket.SendBufferSize, pendingWriteCount);

            while (pendingWriteCount > 0)
            {
                try
                {
                    await AsyncRoot.WaitAsync(cancellationToken).ConfigureAwait(false);
                    if (!IsConnected)
                        throw new SocketException((int)SocketError.NotConnected);

                    var writtenCount = await NetSocket.SendAsync(buffer[..maxChunkSize], cancellationToken).ConfigureAwait(false);
                    pendingWriteCount -= writtenCount;
                }
                finally
                {
                    AsyncRoot.Release();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogErrorWriting(RemoteEndPoint, ex.Message);
            Dispose();
            throw;
        }
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken)
    {
        try
        {
            var length = 0;

            if (!IsConnected)
                throw new SocketException((int)SocketError.NotConnected);

            while (NetSocket.Available > 0 && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await AsyncRoot.WaitAsync(cancellationToken).ConfigureAwait(false);
                    var bytesRead = await NetSocket.ReceiveAsync(ReadBuffer.Memory[length..], cancellationToken);
                    length += bytesRead;

                    if (bytesRead <= 0 || length >= ReadBuffer.Length)
                        break;
                }
                finally
                {
                    AsyncRoot.Release();
                }
            }

            return length == 0
                ? ReadOnlyMemory<byte>.Empty
                : ReadBuffer.Memory[..length];
        }
        catch (Exception ex)
        {
            Logger.LogErrorReading(RemoteEndPoint, ex.Message);
            Dispose();
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
        if (Interlocked.Increment(ref _IsDisposed) > 1)
            return;

        _IsConnected = false;

        if (!alsoManaged)
            return;

        _IsConnected = false;
        NetSocket.Close();
        ReadBuffer.Dispose();
        AsyncRoot.Dispose();
        Logger.LogClientDisconnected(RemoteEndPoint);
    }

}
