using System;
using System.Security.Cryptography;

namespace Common.Helpers
{
    /// <summary>
    /// A class used to hold preset configurations of encryption settings
    /// </summary>
    public class EncryptionConfig
    {
        /// <summary>
        /// Represents the number of messages sent between the server and client (per party) during a
        /// handshake until certificates are calculated. This ensures that the saltlist is never too large to 
        /// be signed by the key
        /// </summary>
        private const int saltDivider = 3;

        #region Public Members

        /// <summary>
        /// Represents the different strengths of encryption
        /// </summary>
        public enum Strength
        {
            Light,
            Medium,
            Strong,
            None
        }

        public readonly Strength strength;
        public readonly int AES_KEY_LENGTH; //AES key size (in bytes)
        public readonly int AES_KEY_BITS; //AES key size (in bits)        
        public readonly int AES_IV_LENGTH = 16; //AES iv size (in bytes)        
        public readonly int RSA_KEY_BITS; //RSA key size (in bits)
        public readonly int RSA_OUTPUT; //length of encryptionData (size of output arr from RSA encryption) (in bytes)
        public readonly int SALT_SIZE; //size of body salt (in bytes)
        public RSAParameters pub;
        public RSAParameters priv;
        public RSAParameters recipient;
        public bool useCrpyto = false;
        public bool captureSalts = true;

        #endregion

        #region Methods

        /// <summary>
        /// EncryptionConfig constructor to create EncrpytionConfig objects with attributes based from the provided
        /// strength value
        /// </summary>
        /// <param name="strength">The strength of the EncrpytionConfig object to create</param>
        public EncryptionConfig(Strength strength)
        {
            this.strength = strength;

            switch (strength)
            {
                case Strength.Light:     
                    AES_KEY_LENGTH = 16;
                    RSA_KEY_BITS = 512;
                    SALT_SIZE = (int)Math.Floor((decimal)(RSA_KEY_BITS / (2*saltDivider)));
                    break;
                case Strength.Medium:
                    AES_KEY_LENGTH = 16;
                    RSA_KEY_BITS = 1024;
                    SALT_SIZE = (int)Math.Floor((decimal)(RSA_KEY_BITS / saltDivider));
                    break;
                case Strength.Strong:
                    AES_KEY_LENGTH = 32;
                    RSA_KEY_BITS = 2048;
                    SALT_SIZE = (int)Math.Floor((decimal)(RSA_KEY_BITS / saltDivider));
                    break;
                case Strength.None:
                    captureSalts = false;
                    AES_KEY_LENGTH = 0;
                    RSA_KEY_BITS = 0;
                    SALT_SIZE = 0;
                    break;
                default:
                    captureSalts = false;
                    this.strength = Strength.None;
                    AES_KEY_LENGTH = 0;
                    RSA_KEY_BITS = 0;
                    SALT_SIZE = 0;
                    break;
            }

            AES_KEY_BITS = AES_KEY_LENGTH * 8;
            RSA_OUTPUT = RSA_KEY_BITS / 8;
        }

        #endregion
    }
}