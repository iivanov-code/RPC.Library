using System;
using System.Text;
using NetworkCommunicator.Models;
using NetworkCommunicator.Utils;

namespace NetworkCommunicator.Network
{
    internal static class MessagePipeline
    {
        private const string KEY = "secretKey";

        public static byte[] Wrap(string data, string methodName)
        {
            return Wrap(data, methodName, false, false);
        }

        private static byte[] Wrap(string data, string methodName, bool sign, bool encrypt)
        {
            string hmac = null;

            if (sign && !string.IsNullOrEmpty(data))
            {
                byte[] hmacArray = NetworkUtils.Sign(Encoding.UTF8.GetBytes(data), KEY);
                hmac = Convert.ToBase64String(hmacArray);
            }

            string json = NetworkUtils.Serialize(new BaseMessage
            {
                Data = data,
                MethodName = methodName,
                HMAC = hmac
            });

            if (encrypt)
            {
                return NetworkUtils.Encrypt(json, KEY);
            }
            else
            {
                return Encoding.UTF8.GetBytes(json);
            }
        }

        public static BaseMessage Unwrap(byte[] message)
        {
            return Unwrap(message, false, false);
        }

        private static BaseMessage Unwrap(byte[] message, bool signed, bool encrypted)
        {
            string json;

            if (encrypted)
            {
                json = NetworkUtils.Decrypt(message, KEY);
            }
            else
            {
                json = Encoding.UTF8.GetString(message);
            }

            BaseMessage msg = NetworkUtils.Deserialize<BaseMessage>(json);

            if (signed && !string.IsNullOrEmpty(msg.Data))
            {
                byte[] hmacArray = Convert.FromBase64String(msg.HMAC);
                bool valid = NetworkUtils.ValidateSignature(Encoding.UTF8.GetBytes(msg.Data), hmacArray, KEY);

                if (!valid) throw new InvalidOperationException();
            }

            return msg;
        }
    }
}
