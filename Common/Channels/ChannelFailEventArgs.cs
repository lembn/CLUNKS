using System;

namespace Common.Channels
{
    public class ChannelFailEventArgs : EventArgs
    {
        public string Message;

        public ChannelFailEventArgs(string message) => this.Message = message;
    }
}
