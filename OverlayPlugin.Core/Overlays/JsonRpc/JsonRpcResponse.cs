using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NoOverlayPlugin.JsonRpc
{
    [JsonConverter(typeof(JsonRpcResponseJsonConverter))]
    public class JsonRpcResponse
    {
        public const string JsonRpc = "2.0";

        private JsonRpcResponse() {}

        public JsonRpcResponse(object id, JToken result)
        {
            Id = id;
            Result = result;
            Error = null;
        }

        public JsonRpcResponse(object id, JsonRpcErrorObject error)
        {
            Id = id;
            Result = null;
            Error = error;
        }

        public object Id { get; private set; }

        public JToken Result { get; private set; }
        public JsonRpcErrorObject Error { get; private set; }

        public string Serialize()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static JsonRpcResponse FromJson(string json)
        {
            return JsonConvert.DeserializeObject<JsonRpcResponse>(json);
        }

        public static JsonRpcResponse MethodNotFound(JsonRpcRequest request)
        {
            return new JsonRpcResponse(request.Id, new JsonRpcErrorObject(
                JsonRpcError.MethodNotFound,
                $"Method '{request.Method}' does not exist"
            ));
        }

        public static JsonRpcResponse InternalError(JsonRpcRequest request)
        {
            return new JsonRpcResponse(request.Id, new JsonRpcErrorObject(
                JsonRpcError.InternalError,
                $"Unexpected error when calling method '{request.Method}'"
            ));
        }
    }

    /// <summary>
    /// Json Rpc Error object as defined in section 5.1
    /// </summary>
    public class JsonRpcErrorObject
    {
        public JsonRpcErrorObject(JsonRpcError errorCode, string message, object data = null)
        {
            Code = errorCode;
            Message = message;
            Data = data;
        }

        public JsonRpcError Code { get; }
        public string Message { get; }
        public object Data { get; }
    }

    public enum JsonRpcError
    {
        // JSON deserialization error
        ParseError = -32700,

        // Received JSON does not conform JSON RPC specification
        // e.g. Missing method key, jsonrpc value is not supported.
        InvalidRequest = -32600,

        // Cannot find requested method when processing the request
        MethodNotFound = -32601,

        // Invalid parameters, e.g. mismatched number of parameters, wrong parameter type
        InvalidParams = -32602,

        // If registered procedure throws an exception without an error code, it means error is unexpected
        // Then we throw InternalError, just like HTTP Error 500, Internal Server Error.
        // TODO: Verify the meaning of InternalError and ensure correct understanding
        InternalError = -32603,

        // -32000 to -32099 are server implementation specific errors, not user code errors.
    }


    public class JsonRpcResponseJsonConverter : JsonConverter<JsonRpcResponse>
    {
        public override JsonRpcResponse ReadJson(JsonReader reader, Type objectType, JsonRpcResponse existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var jObject = JObject.Load(reader);
            var jsonRpcVersion = jObject["jsonrpc"];
            jObject.ThrowIfMissingKey("jsonrpc");
            jObject.ThrowIfNull("jsonrpc");
            jObject.ThrowIfMissingKey("id");
            jObject.ThrowIfNotType("id", JTokenType.Integer, JTokenType.String);
            if (jsonRpcVersion.Value<string>() != "2.0")
            {
                throw new ApplicationException("Unsupported JSON RPC version " + jsonRpcVersion);
            }

            var jMessageId = jObject["id"];
            object messageId = null;
            if(jMessageId.Type == JTokenType.Integer)
            {
                messageId = jMessageId.Value<int>();
            } else if(jMessageId.Type == JTokenType.String)
            {
                messageId = jMessageId.Value<string>();
            }

            if (jObject.ContainsKey("error"))
            {
                jObject.ThrowIfNotType("error", JTokenType.Object);
                return new JsonRpcResponse(messageId, jObject["error"].ToObject<JsonRpcErrorObject>());
            }

            // Key 'error' does not exist, then this response must be a success response
            // According to spec, Expecting existence of key 'result'
            jObject.ThrowIfMissingKey("result");
            return new JsonRpcResponse(messageId, jObject["result"]);
        }

        public override void WriteJson(JsonWriter writer, JsonRpcResponse value, JsonSerializer serializer)
        {
            var jObject = new JObject()
            {
                { "jsonrpc", "2.0" },
                { "id", JToken.FromObject(value.Id) }
            };

            if (value.Error == null)
            {
                jObject.Add("result", JToken.FromObject(value.Result));
            }
            else
            {
                jObject.Add("error", JToken.FromObject(value.Error));
            }

            serializer.Serialize(writer, jObject);
        }
    }
}
