using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Skua.Core.Interfaces;
using System.Collections.ObjectModel;

namespace Skua.Core.ViewModels;

public partial class PacketLoggerViewModel : BotControlViewModelBase
{
    public PacketLoggerViewModel(IEnumerable<PacketLogFilterViewModel> filters, IFlashUtil flash, IFileDialogService fileDialog)
        : base("Packet Logger")
    {
        _flash = flash;
        _fileDialog = fileDialog;
        _packetFilters = filters.ToList();
    }

    private readonly IFlashUtil _flash;
    private readonly IFileDialogService _fileDialog;

    [ObservableProperty]
    private ObservableCollection<string> _packetLogs = new();

    [ObservableProperty]
    private List<PacketLogFilterViewModel> _packetFilters;

    private bool _isReceivingPackets;

    public bool IsReceivingPackets
    {
        get => _isReceivingPackets;
        set
        {
            if (SetProperty(ref _isReceivingPackets, value))
                ToggleLogger();
        }
    }

    [RelayCommand]
    private void SavePacketLogs()
    {
        _fileDialog.SaveText(PacketLogs);
    }

    [RelayCommand]
    private void ClearFilters()
    {
        PacketFilters.ForEach(f => f.IsChecked = false);
    }

    [RelayCommand]
    private void ClearPacketLogs()
    {
        PacketLogs.Clear();
    }

    private void ToggleLogger()
    {
        if (_isReceivingPackets)
            _flash.FlashCall += LogPackets;
        else
            _flash.FlashCall -= LogPackets;
    }

    private bool _filterEnabled
    {
        get
        {
            foreach (PacketLogFilterViewModel filter in PacketFilters)
            {
                if (!filter.IsChecked)
                    return true;
            }
            return false;
        }
    }

    private void LogPackets(string function, object[] args)
    {
        if (function != "packet")
            return;

        if (!_filterEnabled)
        {
            PacketLogs.Add(args[0].ToString()!);
            return;
        }

        string[] packet = args[0].ToString()!.Split(new[] { '%' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (PacketLogFilterViewModel filterVM in PacketFilters)
        {
            if (!filterVM.IsChecked && filterVM.Filter.Invoke(packet))
                return;
        }

        PacketLogs.Add(args[0].ToString()!);
    }
}