using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Y0daiiIRC.Models;
using Y0daiiIRC.Services;

namespace Y0daiiIRC
{
    public partial class DCCTransferDialog : Window
    {
        private readonly DCCService _dccService;
        private readonly ObservableCollection<DCCTransfer> _transfers = new();

        public DCCTransferDialog(DCCService dccService)
        {
            InitializeComponent();
            _dccService = dccService;
            
            TransferList.ItemsSource = _transfers;
            
            // Subscribe to DCC events
            _dccService.TransferStarted += OnTransferStarted;
            _dccService.TransferProgress += OnTransferProgress;
            _dccService.TransferCompleted += OnTransferCompleted;
            _dccService.TransferFailed += OnTransferFailed;
            
            // Load existing transfers
            foreach (var transfer in _dccService.ActiveTransfers.Values)
            {
                _transfers.Add(transfer);
            }
        }

        public void SetRecipient(string nickname)
        {
            // Store the recipient for file sending
            _targetUser = nickname;
        }

        public void SetFileToSend(string filePath, string fileName, long fileSize)
        {
            // Automatically initiate the file send
            _ = Task.Run(async () =>
            {
                try
                {
                    await _dccService.InitiateSendAsync(_targetUser, filePath);
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"Failed to initiate file transfer: {ex.Message}", "Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            });
        }

        private string _targetUser = "";

        private void OnTransferStarted(object? sender, DCCTransfer transfer)
        {
            Dispatcher.Invoke(() =>
            {
                _transfers.Add(transfer);
                StatusText.Text = $"Transfer started: {transfer.FileName}";
            });
        }

        private void OnTransferProgress(object? sender, DCCTransfer transfer)
        {
            Dispatcher.Invoke(() =>
            {
                // Find and update the transfer in the collection
                var existingTransfer = _transfers.FirstOrDefault(t => t.Id == transfer.Id);
                if (existingTransfer != null)
                {
                    var index = _transfers.IndexOf(existingTransfer);
                    _transfers[index] = transfer;
                }
                
                StatusText.Text = $"Transferring: {transfer.FileName} ({transfer.ProgressPercentage:F1}%)";
            });
        }

        private void OnTransferCompleted(object? sender, DCCTransfer transfer)
        {
            Dispatcher.Invoke(() =>
            {
                var existingTransfer = _transfers.FirstOrDefault(t => t.Id == transfer.Id);
                if (existingTransfer != null)
                {
                    var index = _transfers.IndexOf(existingTransfer);
                    _transfers[index] = transfer;
                }
                
                StatusText.Text = $"Transfer completed: {transfer.FileName}";
            });
        }

        private void OnTransferFailed(object? sender, DCCTransfer transfer)
        {
            Dispatcher.Invoke(() =>
            {
                var existingTransfer = _transfers.FirstOrDefault(t => t.Id == transfer.Id);
                if (existingTransfer != null)
                {
                    var index = _transfers.IndexOf(existingTransfer);
                    _transfers[index] = transfer;
                }
                
                StatusText.Text = $"Transfer failed: {transfer.FileName} - {transfer.ErrorMessage}";
            });
        }

        private async void SendFileButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select File to Send",
                Filter = "All Files (*.*)|*.*",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                // TODO: Get target user from UI or context
                var targetUser = "SomeUser"; // This should come from the current chat context
                
                try
                {
                    await _dccService.InitiateSendAsync(targetUser, openFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to initiate file transfer: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CancelTransferButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement selection mechanism for transfers
            // For now, cancel the first active transfer
            var activeTransfer = _transfers.FirstOrDefault(t => 
                t.Status == DCCTransferStatus.Pending || 
                t.Status == DCCTransferStatus.Connecting || 
                t.Status == DCCTransferStatus.InProgress);
                
            if (activeTransfer != null)
            {
                _dccService.CancelTransfer(activeTransfer.Id);
            }
            else
            {
                MessageBox.Show("No active transfers to cancel.", "No Active Transfers", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ClearCompletedButton_Click(object sender, RoutedEventArgs e)
        {
            var completedTransfers = _transfers.Where(t => 
                t.Status == DCCTransferStatus.Completed || 
                t.Status == DCCTransferStatus.Failed || 
                t.Status == DCCTransferStatus.Cancelled).ToList();
                
            foreach (var transfer in completedTransfers)
            {
                _transfers.Remove(transfer);
            }
            
            StatusText.Text = $"Cleared {completedTransfers.Count} completed transfers";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            // Unsubscribe from events
            _dccService.TransferStarted -= OnTransferStarted;
            _dccService.TransferProgress -= OnTransferProgress;
            _dccService.TransferCompleted -= OnTransferCompleted;
            _dccService.TransferFailed -= OnTransferFailed;
            
            base.OnClosed(e);
        }
    }
}
