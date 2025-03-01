using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using Jellyfin.Plugin.FileTransformation.Helpers;
using Jellyfin.Plugin.FileTransformation.Library;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Jellyfin.Plugin.FileTransformation.Models;
using MediaBrowser.Controller;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.FileTransformation.Infrastructure;

public class NamedPipeService : IHostedService
{
    private readonly IWebFileTransformationWriteService m_writeService;
    private bool m_running = false;
    
    private ConcurrentBag<NamedPipeServerStream> m_activeStreams = new ConcurrentBag<NamedPipeServerStream>();
    private readonly ILogger m_logger;
    private readonly IServerApplicationHost m_serverApplicationHost;

    public NamedPipeService(IWebFileTransformationWriteService writeService, IServerApplicationHost serverApplicationHost, IFileTransformationLogger logger)
    {
        m_writeService = writeService;
        m_logger = logger;
        m_serverApplicationHost = serverApplicationHost;
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        m_running = true;
        ListenForConnections();

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        m_running = false;
        
        while (m_activeStreams.Any(x => x.IsConnected))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
        }

        foreach (NamedPipeServerStream stream in m_activeStreams)
        {
            await stream.DisposeAsync();
        }
        
        m_activeStreams.Clear();
    }

    private async void ListenForConnections()
    {
        while (m_running)
        {
            NamedPipeServerStream pipeStream = new NamedPipeServerStream("Jellyfin.Plugin.FileTransformation.NamedPipe", 
                PipeDirection.InOut, 
                254);
            await pipeStream.WaitForConnectionAsync();
            
            m_activeStreams.Add(pipeStream);
            
            HandlePipeConnection(pipeStream);
        }
    }

    private async void HandlePipeConnection(NamedPipeServerStream pipeStream)
    {
        if (!m_running)
        {
            await pipeStream.DisposeAsync();
            return;
        }

        MemoryStream outputStream = new MemoryStream();
        using (StreamWriter streamWriter = new StreamWriter(outputStream))
        {
            byte[] messageLengthBuffer = new byte[8];
            int bytesRead = await pipeStream.ReadAsync(messageLengthBuffer, 0, messageLengthBuffer.Length);
            if (bytesRead != messageLengthBuffer.Length)
            {
                // problem
            }
            
            long messageLength = BitConverter.ToInt64(messageLengthBuffer, 0);

            while (messageLength > 0)
            {
                byte[] buffer = new byte[Math.Min(1024, messageLength)];
                bytesRead = await pipeStream.ReadAsync(buffer, 0, buffer.Length);
                await streamWriter.WriteAsync(buffer.Select(x => (char)x).ToArray(), 0, bytesRead);
                
                messageLength -= bytesRead;
            }
        }
        
        pipeStream.WriteByte(1);
        
        byte[] bytes = outputStream.ToArray();
        string rawJson = Encoding.UTF8.GetString(bytes);
        TransformationRegistrationPayload? payload = JsonConvert.DeserializeObject<TransformationRegistrationPayload>(rawJson);
        
        if (payload != null)
        {
            m_writeService.AddTransformation(payload.Id, payload.FileNamePattern, async (path, contents) =>
            {
                await TransformationHelper.ApplyTransformation(path, contents, payload, m_logger, m_serverApplicationHost);
            });
        }
        
        
        await pipeStream.DisposeAsync();
    }
}