using System.Runtime.InteropServices;
using BMDSwitcherAPI;

namespace AtemKey;

/// <summary>Thin wrapper over the official ATEM Switchers SDK (COM).</summary>
sealed class Atem
{
    IBMDSwitcher? _switcher;
    IBMDSwitcherMixEffectBlock? _me;
    SwitcherCallback? _switcherCb;
    MixEffectCallback? _meCb;

    /// <summary>Raised (on an SDK thread) when program/preview changes.</summary>
    public event Action? StateChanged;
    /// <summary>Raised (on an SDK thread) when the connection drops.</summary>
    public event Action? Disconnected;

    public bool IsConnected => _me != null;
    public string ProductName { get; private set; } = "";
    public Dictionary<long, string> InputNames { get; } = new();

    /// <summary>Blocking connect. Returns null on success, or an error message.</summary>
    public string? Connect(string ip)
    {
        Close();
        try
        {
            var discovery = new CBMDSwitcherDiscovery();
            _BMDSwitcherConnectToFailure failure = 0;
            discovery.ConnectTo(ip, out var switcher, out failure);
            _switcher = switcher;
        }
        catch (COMException)
        {
            return "接続できませんでした（IPアドレス・ケーブル・電源を確認してください）";
        }

        _switcher.GetProductName(out var name);
        ProductName = name;

        _me = First<IBMDSwitcherMixEffectBlockIterator, IBMDSwitcherMixEffectBlock>(
            _switcher.CreateIterator, (IBMDSwitcherMixEffectBlockIterator it, out IBMDSwitcherMixEffectBlock m) => it.Next(out m));
        if (_me == null) { Close(); return "M/Eブロックが見つかりません"; }

        LoadInputNames();

        _switcherCb = new SwitcherCallback(this);
        _switcher.AddCallback(_switcherCb);
        _meCb = new MixEffectCallback(this);
        _me.AddCallback(_meCb);
        return null;
    }

    public void Close()
    {
        try
        {
            if (_me != null && _meCb != null) _me.RemoveCallback(_meCb);
            if (_switcher != null && _switcherCb != null) _switcher.RemoveCallback(_switcherCb);
        }
        catch (COMException) { }
        _me = null; _switcher = null; _meCb = null; _switcherCb = null;
    }

    public long Program { get { long v = 0; _me?.GetProgramInput(out v); return v; } }
    public long Preview { get { long v = 0; _me?.GetPreviewInput(out v); return v; } }

    public void SetProgram(long id) => Run(() => _me!.SetProgramInput(id));
    public void SetPreview(long id) => Run(() => _me!.SetPreviewInput(id));
    public void Cut() => Run(() => _me!.PerformCut());
    public void Auto() => Run(() => _me!.PerformAutoTransition());

    void Run(Action a)
    {
        if (_me == null) return;
        try { a(); }
        catch (COMException) { /* e.g. input does not exist on this model */ }
    }

    void LoadInputNames()
    {
        InputNames.Clear();
        var guid = typeof(IBMDSwitcherInputIterator).GUID;
        _switcher!.CreateIterator(ref guid, out var ptr);
        var it = (IBMDSwitcherInputIterator)Marshal.GetObjectForIUnknown(ptr);
        Marshal.Release(ptr);
        while (true)
        {
            it.Next(out var input);
            if (input == null) break;
            input.GetPortType(out var type);
            if (type != _BMDSwitcherPortType.bmdSwitcherPortTypeExternal) continue;
            input.GetInputId(out var id);
            input.GetShortName(out var shortName);
            InputNames[id] = shortName;
        }
    }

    delegate void CreateIter(ref Guid g, out IntPtr p);
    delegate void NextFn<TIt, T>(TIt it, out T item);

    static T? First<TIt, T>(CreateIter create, NextFn<TIt, T> next) where T : class
    {
        var guid = typeof(TIt).GUID;
        create(ref guid, out var ptr);
        var it = (TIt)Marshal.GetObjectForIUnknown(ptr);
        Marshal.Release(ptr);
        next(it, out var item);
        return item;
    }

    sealed class SwitcherCallback(Atem owner) : IBMDSwitcherCallback
    {
        public void Notify(_BMDSwitcherEventType eventType, _BMDSwitcherVideoMode coreVideoMode)
        {
            if (eventType == _BMDSwitcherEventType.bmdSwitcherEventTypeDisconnected)
                owner.Disconnected?.Invoke();
        }
    }

    sealed class MixEffectCallback(Atem owner) : IBMDSwitcherMixEffectBlockCallback
    {
        public void Notify(_BMDSwitcherMixEffectBlockEventType eventType)
        {
            if (eventType is _BMDSwitcherMixEffectBlockEventType.bmdSwitcherMixEffectBlockEventTypeProgramInputChanged
                          or _BMDSwitcherMixEffectBlockEventType.bmdSwitcherMixEffectBlockEventTypePreviewInputChanged)
                owner.StateChanged?.Invoke();
        }
    }
}
