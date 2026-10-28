using System.Text.Json.Serialization;
using NetworkCommunicator.Utils;

namespace NetworkCommunicator.Models
{
    internal class Message<T> : BaseMessage
    {
        public Message(BaseMessage message)
        {
            Data = message.Data;
            model = NetworkUtils.Deserialize<T>(message.Data);
            MethodName = message.MethodName;
            HMAC = message.HMAC;
        }

        public Message(T data)
        {
            Model = data;
        }

        private T model;

        [JsonIgnore]
        public T Model
        {
            get
            {
                return model;
            }
            set
            {
                model = value;
                Data = NetworkUtils.Serialize(model);
            }
        }
    }
}