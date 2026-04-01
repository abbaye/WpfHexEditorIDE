// ==========================================================
// Project: WpfHexEditor.Core
// File: IByteControl.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic), Janus Tida
// Created: 2026-03-06
// Description:
//     Interface that all byte control UI elements must implement, providing a
//     uniform contract for the HexEditor to manipulate byte display controls
//     regardless of their concrete type (hex cell, ASCII cell, etc.).
//
// Architecture Notes:
//     Originally authored by Janus Tida. Extends IByte interactions with WPF
//     event handling (ByteEventArgs). Reduces coupling between HexEditor host
//     and individual byte control implementations.
//
// ==========================================================

using System;
using WpfHexEditor.Core.Events;

namespace WpfHexEditor.Core.Interfaces
{
    /// <summary>
    /// All byte control inherit from this interface.
    /// This interface is used to reduce the code when manipulate byte control
    /// </summary>
    internal interface IByteControl
    {
        //Properties
        long BytePositionInStream { get; set; }
        ByteAction Action { get; set; }
        IByte Byte { get; set; }
        bool IsHighLight { get; set; }
        bool IsSelected { get; set; }
        bool InternalChange { get; set; }
        bool IsMouseOverMe { get; }
        bool IsEnabled { get; set; }

        //Methods
        void UpdateVisual();
        void Clear();

        //Events
        event EventHandler<ByteEventArgs> ByteModified;
        event EventHandler MouseSelection;
        event EventHandler Click;
        event EventHandler DoubleClick;
        event EventHandler RightClick;
        event EventHandler<ByteEventArgs> MoveNext;
        event EventHandler<ByteEventArgs> MovePrevious;
        event EventHandler<ByteEventArgs> MoveRight;
        event EventHandler<ByteEventArgs> MoveLeft;
        event EventHandler<ByteEventArgs> MoveUp;
        event EventHandler<ByteEventArgs> MoveDown;
        event EventHandler<ByteEventArgs> MovePageDown;
        event EventHandler<ByteEventArgs> MovePageUp;
        event EventHandler ByteDeleted;
        event EventHandler EscapeKey;
        event EventHandler CtrlzKey;
        event EventHandler CtrlvKey;
        event EventHandler CtrlcKey;
        event EventHandler CtrlaKey;
        event EventHandler CtrlyKey;
    }
}
