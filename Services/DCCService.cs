using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Y0daiiIRC.Models;

namespace Y0daiiIRC.Services
{
    public class DCCService
    {
        private readonly Dictionary<string, DCCTransfer> _activeTransfers = new();
        private readonly Dictionary<string, TcpListener> _listeners = new();
        private readonly Dictionary<string, CancellationTokenSource> _cancellationTokens = new();

        public event EventHandler<DCCTransfer>? TransferStarted;
        public event EventHandler<DCCTransfer>? TransferProgress;
        public event EventHandler<DCCTransfer>? TransferCompleted;
        public event EventHandler<DCCTransfer>? TransferFailed;

        public IReadOnlyDictionary<string, DCCTransfer> ActiveTransfers => _activeTransfers;

        public async Task<string> InitiateSendAsync(string target, string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            var fileInfo = new FileInfo(filePath);
            var transfer = new DCCTransfer
            {
                Sender = "Me", // TODO: Get from IRC client
                Receiver = target,
                FileName = fileInfo.Name,
                FilePath = filePath,
                FileSize = fileInfo.Length,
                Type = DCCTransferType.Send,
                Status = DCCTransferStatus.Pending,
                StartTime = DateTime.Now
            };

            _activeTransfers[transfer.Id] = transfer;
            TransferStarted?.Invoke(this, transfer);

            // Start listening for connection
            var listener = new TcpListener(IPAddress.Any, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            transfer.Port = port;
            transfer.IPAddress = GetLocalIPAddress();

            _listeners[transfer.Id] = listener;

            // Start accepting connection
            _ = Task.Run(() => AcceptConnectionAsync(transfer, listener));

            return transfer.Id;
        }

        public async Task<string> InitiateReceiveAsync(string sender, string fileName, long fileSize, string ipAddress, int port, string? token = null)
        {
            var transfer = new DCCTransfer
            {
                Sender = sender,
                Receiver = "Me", // TODO: Get from IRC client
                FileName = fileName,
                FileSize = fileSize,
                Type = DCCTransferType.Receive,
                Status = DCCTransferStatus.Pending,
                StartTime = DateTime.Now,
                IPAddress = ipAddress,
                Port = port,
                Token = token
            };

            _activeTransfers[transfer.Id] = transfer;
            TransferStarted?.Invoke(this, transfer);

            // Start receiving
            _ = Task.Run(() => ReceiveFileAsync(transfer));

            return transfer.Id;
        }

        public void CancelTransfer(string transferId)
        {
            if (_activeTransfers.TryGetValue(transferId, out var transfer))
            {
                transfer.Status = DCCTransferStatus.Cancelled;
                transfer.EndTime = DateTime.Now;

                if (_cancellationTokens.TryGetValue(transferId, out var cts))
                {
                    cts.Cancel();
                }

                if (_listeners.TryGetValue(transferId, out var listener))
                {
                    listener.Stop();
                    _listeners.Remove(transferId);
                }

                TransferCompleted?.Invoke(this, transfer);
            }
        }

        private async Task AcceptConnectionAsync(DCCTransfer transfer, TcpListener listener)
        {
            try
            {
                transfer.Status = DCCTransferStatus.Connecting;
                var client = await listener.AcceptTcpClientAsync();
                listener.Stop();
                _listeners.Remove(transfer.Id);

                await SendFileAsync(transfer, client);
            }
            catch (Exception ex)
            {
                transfer.Status = DCCTransferStatus.Failed;
                transfer.ErrorMessage = ex.Message;
                transfer.EndTime = DateTime.Now;
                TransferFailed?.Invoke(this, transfer);
            }
        }

        private async Task SendFileAsync(DCCTransfer transfer, TcpClient client)
        {
            var cts = new CancellationTokenSource();
            _cancellationTokens[transfer.Id] = cts;

            try
            {
                transfer.Status = DCCTransferStatus.InProgress;
                using var stream = client.GetStream();
                using var fileStream = new FileStream(transfer.FilePath, FileMode.Open, FileAccess.Read);
                
                var buffer = new byte[8192];
                int bytesRead;

                while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length, cts.Token)) > 0)
                {
                    await stream.WriteAsync(buffer, 0, bytesRead, cts.Token);
                    transfer.BytesTransferred += bytesRead;
                    TransferProgress?.Invoke(this, transfer);

                    if (cts.Token.IsCancellationRequested)
                        break;
                }

                if (!cts.Token.IsCancellationRequested)
                {
                    transfer.Status = DCCTransferStatus.Completed;
                    transfer.EndTime = DateTime.Now;
                    TransferCompleted?.Invoke(this, transfer);
                }
            }
            catch (Exception ex)
            {
                transfer.Status = DCCTransferStatus.Failed;
                transfer.ErrorMessage = ex.Message;
                transfer.EndTime = DateTime.Now;
                TransferFailed?.Invoke(this, transfer);
            }
            finally
            {
                client.Close();
                _cancellationTokens.Remove(transfer.Id);
            }
        }

        private async Task ReceiveFileAsync(DCCTransfer transfer)
        {
            var cts = new CancellationTokenSource();
            _cancellationTokens[transfer.Id] = cts;

            try
            {
                transfer.Status = DCCTransferStatus.Connecting;
                using var client = new TcpClient();
                await client.ConnectAsync(transfer.IPAddress, transfer.Port);
                
                transfer.Status = DCCTransferStatus.InProgress;
                using var stream = client.GetStream();
                
                // Create downloads directory if it doesn't exist
                var downloadsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Y0daiiIRC");
                Directory.CreateDirectory(downloadsDir);
                
                var filePath = Path.Combine(downloadsDir, transfer.FileName);
                transfer.FilePath = filePath;

                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                var buffer = new byte[8192];
                int bytesRead;

                while (transfer.BytesTransferred < transfer.FileSize && !cts.Token.IsCancellationRequested)
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
                    if (bytesRead == 0) break;

                    await fileStream.WriteAsync(buffer, 0, bytesRead, cts.Token);
                    transfer.BytesTransferred += bytesRead;
                    TransferProgress?.Invoke(this, transfer);
                }

                if (!cts.Token.IsCancellationRequested)
                {
                    transfer.Status = DCCTransferStatus.Completed;
                    transfer.EndTime = DateTime.Now;
                    TransferCompleted?.Invoke(this, transfer);
                }
            }
            catch (Exception ex)
            {
                transfer.Status = DCCTransferStatus.Failed;
                transfer.ErrorMessage = ex.Message;
                transfer.EndTime = DateTime.Now;
                TransferFailed?.Invoke(this, transfer);
            }
            finally
            {
                _cancellationTokens.Remove(transfer.Id);
            }
        }

        private string GetLocalIPAddress()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                var endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint?.Address.ToString() ?? "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        public void Dispose()
        {
            foreach (var listener in _listeners.Values)
            {
                listener.Stop();
            }
            _listeners.Clear();

            foreach (var cts in _cancellationTokens.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }
            _cancellationTokens.Clear();
        }
    }
}
