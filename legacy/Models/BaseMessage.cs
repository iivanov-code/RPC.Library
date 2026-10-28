using System.Text.Json.Serialization;

namespace NetworkCommunicator.Models
{
    internal class BaseMessage
    {
        [JsonPropertyName("methodName")]
        public string MethodName { get; set; }

        [JsonPropertyName("data")]
        public string Data { get; set; }

        [JsonPropertyName("hmac")]
        public string HMAC { get; set; }
    }
}