// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: ApplicationExtention.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Extension methods for the WPF Application dispatcher. Provides a DoEvents
//     equivalent that allows the UI dispatcher to process pending messages during
//     long-running synchronous operations without freezing the UI thread.
//
// Architecture Notes:
//     Uses Dispatcher.Invoke with DispatcherPriority.Background to yield execution.
//     Should be used sparingly; prefer async/await for new code.
//
// ==========================================================

using System.Windows;
using System.Windows.Threading;

namespace WpfHexEditor.Core.Extensions
{
    /// <summary>
    /// DoEvents when control is in long task. Control do not freeze the dispatcher.
    /// </summary>
    public static class ApplicationExtention
    {
        private static readonly DispatcherOperationCallback ExitFrameCallback = ExitFrame;

        public static void DoEvents(this Application _, DispatcherPriority priority = DispatcherPriority.Background)
        {
            var nestedFrame = new DispatcherFrame();
            var exitOperation = Dispatcher.CurrentDispatcher.BeginInvoke(priority, ExitFrameCallback, nestedFrame);

            try
            {
                //execute all next message
                Dispatcher.PushFrame(nestedFrame);

                //If not completed, will stop it
                if (exitOperation.Status != DispatcherOperationStatus.Completed)
                    exitOperation.Abort();
            }
            catch
            {
                exitOperation.Abort();
            }
        }

        private static object ExitFrame(object f)
        {
            (f as DispatcherFrame).Continue = false;
            return null;
        }
    }
}
