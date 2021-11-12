using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NetworkCommunicator.Utils
{
    internal static class NetworkUtils
    {
        private static object hashLock = new object();
        private static HashAlgorithm hashAlgorithm;
        public static HashAlgorithm HashAlgorithm
        {
            get
            {
                if (hashAlgorithm == null)
                {
                    lock (hashLock)
                    {
                        if (hashAlgorithm == null)
                        {
                            hashAlgorithm = SHA256.Create();
                        }
                    }
                }

                return hashAlgorithm;
            }
        }

        private static object keyedHashLock = new object();
        private static KeyedHashAlgorithm keyedHashAlgorithm;
        public static KeyedHashAlgorithm KeyedHashAlgorithm(string key)
        {
            if (keyedHashAlgorithm == null)
            {
                lock (keyedHashLock)
                {
                    if (keyedHashAlgorithm == null)
                    {
                        keyedHashAlgorithm = HMAC.Create("HMACSHA256");
                        keyedHashAlgorithm.Key = HashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(key));
                    }
                }
            }

            return keyedHashAlgorithm;
        }

        private static object encryptionLock = new object();
        private static SymmetricAlgorithm symmetricAlgorithm;

        public static SymmetricAlgorithm SymmetricAlgorithm(string key)
        {
            if (symmetricAlgorithm == null)
            {
                lock (encryptionLock)
                {
                    if (symmetricAlgorithm == null)
                    {
                        symmetricAlgorithm = Aes.Create();
                        symmetricAlgorithm.Key = HashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(key));
                        symmetricAlgorithm.GenerateIV();
                    }
                }
            }

            return symmetricAlgorithm;
        }

        public static string Serialize<T>(T data)
        {
            return JsonSerializer.Serialize(data);
        }

        public static T Deserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json);
        }

        public static byte[] Sign(byte[] data, string key)
        {
            byte[] signature = KeyedHashAlgorithm(key).ComputeHash(data);
            return signature;
        }

        public static bool ValidateSignature(byte[] data, byte[] hmac, string key)
        {
            byte[] signature = Sign(data, key);

            for (int i = 0; i < hmac.Length; i++)
            {
                if (signature[i] != hmac[i])
                {
                    return false;
                }
            }

            return true;
        }

        public static byte[] Encrypt(string data, string key)
        {
            using (MemoryStream msEncrypt = new MemoryStream())
            {
                var algorithm = SymmetricAlgorithm(key);
                using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, algorithm.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    csEncrypt.Write(algorithm.IV);

                    using (StreamWriter swEncrypt = new StreamWriter(csEncrypt, Encoding.UTF8))
                    {
                        swEncrypt.Write(data);
                    }

                    return msEncrypt.ToArray();
                }
            }
        }

        public static string Decrypt(byte[] data, string key)
        {
            byte[] iv = new byte[16];
            Buffer.BlockCopy(data, 0, iv, 0, iv.Length);
            Span<byte> buffer = new Span<byte>(data, iv.Length, data.Length - iv.Length);

            using (MemoryStream msDecrypt = new MemoryStream())
            {
                msDecrypt.Write(buffer);
                msDecrypt.Position = 0;
                var algorithm = SymmetricAlgorithm(key);
                algorithm.IV = iv;
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, algorithm.CreateDecryptor(), CryptoStreamMode.Read))
                {
                    using (StreamReader srDecrypt = new StreamReader(csDecrypt, Encoding.UTF8))
                    {
                        return srDecrypt.ReadToEnd();
                    }
                }
            }
        }

        private static Random rand = new Random(new Random((int)DateTime.Now.Ticks).Next(0, int.MaxValue));

        public static ushort GetRandomPort(ushort from = 1024, ushort to = 5000)
        {
            if (rand == null) rand = new Random();

            return (ushort)rand.Next(from, to);
        }

        public static bool IsPortAvailable(int port, ProtocolType protocol = ProtocolType.Tcp)
        {
            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            TcpConnectionInformation[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();

            switch (protocol)
            {
                case ProtocolType.Tcp:
                    return !ipGlobalProperties.GetActiveTcpConnections().Any(x => x.LocalEndPoint.Port == port)
                        && !ipGlobalProperties.GetActiveTcpListeners().Any(x => x.Port == port);
                case ProtocolType.Udp:
                    return !ipGlobalProperties.GetActiveUdpListeners().Any(x => x.Port == port);
                default:
                    return true;
            }
        }
    }
}
