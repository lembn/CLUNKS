using System.Collections.Generic;

namespace Common.Channels
{
    public class DataStream
    {
        #region Public Members

        public int bufferSize;
        public List<byte[]> bufferList;

        #endregion

        #region Methods

        public DataStream(int bufferSize)
        {
            bufferList = new List<byte[]>();
            this.bufferSize = bufferSize;
        }

        public byte[] New()
        {
            byte[] buffer = new byte[bufferSize];
            bufferList.Add(buffer);
            return buffer;
        }

        public byte[] Get()
        {
            List<byte> output = new List<byte>();
            foreach (byte[] buffer in bufferList)
            {
                output.AddRange(buffer);
            }
            bufferList.Clear();
            return output.ToArray();
        }

        #endregion
    }
}
