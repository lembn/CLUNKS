﻿using System.Collections.Generic;

namespace Common.Channels
{
    public class DataStream
    {
        #region Public Members

        public int bufferSize;
        public List<byte[]> bufferList;

        #endregion

        #region Methods

        /// <summary>
        /// A class used for buffering incoming data from a socket
        /// </summary>
        /// <param name="bufferSize">The size of the segments of the buffer</param>
        public DataStream(int bufferSize)
        {
            bufferList = new List<byte[]>();
            this.bufferSize = bufferSize;
        }

        /// <summary>
        /// A method to get a new segment for the buffer to use for buffering
        /// </summary>
        /// <returns>The new segment</returns>
        public byte[] New()
        {
            byte[] buffer = new byte[bufferSize];
            lock (bufferList)
                bufferList.Add(buffer);
            return buffer;
        }

        /// <summary>
        /// A method to get all the data currently held in the buffer(s)
        /// </summary>
        /// <returns>A byte array containg the data from the buffer(s)</returns>
        public byte[] Get()
        {
            List<byte> output = new List<byte>();
            lock (bufferList)
            {
                foreach (byte[] buffer in bufferList)
                    output.AddRange(buffer);
                bufferList.Clear();
            }
            return output.ToArray();
        }

        #endregion
    }
}