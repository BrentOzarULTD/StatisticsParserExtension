using System;

namespace StatisticsParser.Vsix.Capture
{
    public enum MessagesCaptureStatus
    {
        Ok,
        NoActiveWindow,
        EmptyMessages,
        ContractsAssemblyMissing,
        ProxyUnavailable,
        Failed
    }

    public readonly struct MessagesCaptureResult
    {
        public MessagesCaptureStatus Status { get; }
        public string Text { get; }
        public Exception Error { get; }

        private MessagesCaptureResult(MessagesCaptureStatus status, string text, Exception error)
        {
            Status = status;
            Text = text;
            Error = error;
        }

        public static MessagesCaptureResult Ok(string text) =>
            new MessagesCaptureResult(MessagesCaptureStatus.Ok, text ?? string.Empty, null);

        public static MessagesCaptureResult NoActiveWindow() =>
            new MessagesCaptureResult(MessagesCaptureStatus.NoActiveWindow, null, null);

        public static MessagesCaptureResult EmptyMessages() =>
            new MessagesCaptureResult(MessagesCaptureStatus.EmptyMessages, string.Empty, null);

        public static MessagesCaptureResult ContractsAssemblyMissing(Exception error) =>
            new MessagesCaptureResult(MessagesCaptureStatus.ContractsAssemblyMissing, null, error);

        public static MessagesCaptureResult ProxyUnavailable(Exception error) =>
            new MessagesCaptureResult(MessagesCaptureStatus.ProxyUnavailable, null, error);

        public static MessagesCaptureResult Failed(Exception error) =>
            new MessagesCaptureResult(MessagesCaptureStatus.Failed, null, error);
    }
}
