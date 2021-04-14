using System;

namespace Common.Channels
{
    public class RemoveClientEventArgs : EventArgs
    {
        public int ID;
        public ClientModel Client;

        public RemoveClientEventArgs(int ID, ClientModel Client)
        {
            this.ID = ID;
            this.Client = Client;
        }
    }
}
