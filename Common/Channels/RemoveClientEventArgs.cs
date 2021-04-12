using System;

namespace Common.Channels
{
    public class RemoveClientEventArgs : EventArgs
    {
        public int ID { get; set; }
        public ClientModel Client { get; set; }
    }
}
