using System;

namespace Common.Channels
{
    public class DataStream
    {
        public byte[] buffer;
        private int capacity = 0;
        private int bytesRead = 0;

        #region Methods

        public byte[] New(int capacity)
        {
            this.capacity = capacity;
            buffer = new byte[capacity];
            bytesRead = 0;
            return buffer;
        }

        public byte[] Get()
        {
            byte[] output = (byte[])buffer.Clone();
            Array.Clear(buffer, 0, buffer.Length);
            return output;
        }

        public void Update(int bytesRead) => this.bytesRead += bytesRead; 

        public int FreeBytes() => capacity - bytesRead;

        #endregion
    }
}
