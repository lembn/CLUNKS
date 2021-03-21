using System;
using System.Collections.Generic;
using System.Linq;

namespace Common.Channels
{
    public class DataStream
    {
        #region Public Members

        public int chunkSize;
        public bool useNew = false;
        public bool attemptedToFill = false;
        public List<byte[]> chunkList;
        private byte[] flattenChunks;
        private int flattenSize = 0;
        private int bytesToRead = 0;
        private int bytesRead = 0;


        #endregion

        #region Methods

        /// <summary>
        /// A class used for buffering incoming data from a socket
        /// </summary>
        /// <param name="bufferSize">The size of the segments of the buffer</param>
        public DataStream(int chunkSize)
        {
            chunkList = new List<byte[]>();
            this.chunkSize = chunkSize;
        }

        /// <summary>
        /// A method to get a new chunk for the buffer to use for buffering
        /// </summary>
        /// <returns>The new chunk</returns>
        public byte[] New()
        {
            if (chunkList.Count == 1)
                if (chunkList[0].All(item => item == 0))
                    return chunkList[0];
            byte[] buffer = new byte[chunkSize];
            chunkList.Add(buffer);
            return buffer;
        }

        /// <summary>
        /// A method to get all the data currently held in the buffer(s)
        /// </summary>
        /// <returns>A byte array containg the data from the buffer(s)</returns>
        public byte[] Get()
        {
            List<byte> output = new List<byte>();
            foreach (byte[] buffer in chunkList)
                output.AddRange(buffer);
            chunkList.Clear();
            return output.ToArray();
        }

        public byte[] CreateFlattenChunks(int numchunks, int bytesToRead)
        {
            this.bytesToRead = bytesToRead;
            flattenChunks = new byte[numchunks * chunkSize];
            flattenSize = numchunks * chunkSize;
            bytesRead = 0;

            return flattenChunks;
        }

        public byte[] GetFlattenChunks()
        {
            return flattenChunks;
        }

        public int GetFlattenSize()
        {
            return flattenSize;
        }

        public void AddNumBytesStored(int bytesRead)
        {
            this.bytesRead += bytesRead;
        }

        public int GetNumStoredBytes()
        {
            return this.bytesRead;
        }

        public bool isBufferFull()
        {
            return (this.bytesRead >= this.bytesToRead);
        }
        #endregion
    }
}
