using System;
using System.Collections.Generic;
using System.Text;

namespace Common.Channels
{
    public class ChannelFailEventArgs : EventArgs
    {
        public string Message { get; set; }
    }
}
