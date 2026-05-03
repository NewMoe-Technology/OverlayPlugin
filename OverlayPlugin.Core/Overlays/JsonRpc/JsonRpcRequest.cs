using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace NoOverlayPlugin.JsonRpc
{
    [JsonConverter(typeof(JsonRpcRequestJsonConverter))]
    public class JsonRpcRequest
    {
        public JsonRpcRequest(int id, string method, JsonRpcRequestParams parameters = null)
        {
            Id = id;
            Method = method;
            Params = parameters ?? new JsonRpcRequestParams();
        }
        public JsonRpcRequest(string id, string method, JsonRpcRequestParams parameters = null)
        {
            Id = id;
            Method = method;
            Params = parameters ?? new JsonRpcRequestParams();
        }
        public JsonRpcRequest(string method, JsonRpcRequestParams parameters = null)
            : this(null, method, parameters) { }
        public JsonRpcRequest(int id, string method, IEnumerable<JToken> args = null)
            : this(id, method, new JsonRpcRequestParams(new List<JToken>(args))) { }
        public JsonRpcRequest(string id, string method, IEnumerable<JToken> args = null)
            : this(id, method, new JsonRpcRequestParams(new List<JToken>(args))) { }
        public JsonRpcRequest(int id, string method, IDictionary<string, JToken> kwargs = null)
            : this(id, method, new JsonRpcRequestParams(new Dictionary<string, JToken>(kwargs))) { }
        public JsonRpcRequest(string id, string method, IDictionary<string, JToken> kwargs = null)
            : this(id, method, new JsonRpcRequestParams(new Dictionary<string, JToken>(kwargs))) { }
        public JsonRpcRequest(string method, IEnumerable<JToken> args = null)
            : this(null, method, args) { }
        public JsonRpcRequest(string method, IDictionary<string, JToken> kwargs = null)
            : this(null, method, kwargs) { }

        public string Method { get; set; }

        // TODO: Rename this to "Arguments"
        public JsonRpcRequestParams Params { get; set; }

        public object Id { get; set; }

        public bool IsNotification { get => Id == null; }
        
        public string Serialize()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static JsonRpcRequest FromJson(string json) {
            return JsonConvert.DeserializeObject<JsonRpcRequest>(json);
        }
    }

    // TODO: Rename this to JsonRpcRequestArgs
    public class JsonRpcRequestParams
    {
        public JsonRpcRequestParams()
        {
            Kwargs = null;
            Args = null;
        }

        public JsonRpcRequestParams(Dictionary<string, JToken> kwargs)
        {
            Kwargs = kwargs;
            Args = null;
        }

        public JsonRpcRequestParams(List<JToken> args)
        {
            Kwargs = null;
            Args = args;
        }

        public Dictionary<string, JToken> Kwargs { get; private set; }
        
        public List<JToken> Args { get; private set; }

        public int Length { get => Kwargs?.Count ?? Args?.Count ?? 0; }

        public bool IsKwargs { get => Kwargs != null; }

        public void Add(JToken value)
        {
            this[Args?.Count ?? 0] = value;
        }

        public void Add(string key, JToken value)
        {
            this[key] = value;
        }

        public JToken this[int key]
        {
            get => Args[key];
            set
            {
                if(Kwargs != null)
                {
                    throw new InvalidOperationException("Cannot modify parameter by numerical position in keyword params");
                }
                if(Args == null)
                {
                    Args = new List<JToken>();
                }
                //ThrowIfValueHasWrongType(value);

                if (key == Args.Count)
                {
                    Args.Add(value);
                }
                else
                {
                    Args[key] = value;
                }
            }
        }

        public JToken this[string key]
        {
            get => Kwargs[key];
            set
            {
                if (Args != null)
                {
                    throw new InvalidOperationException("Cannot modify parameter by key name in positional params");
                }
                if (Kwargs == null)
                {
                    Kwargs = new Dictionary<string, JToken>();
                }
                //ThrowIfValueHasWrongType(value);

                Kwargs.Add(key, value);
            }
        }

        // TODO: Remove this method, since we are now storing param as JToken
        //private static void ThrowIfValueHasWrongType(object value)
        //{
        //    if (!(value is null || value is string || value is int || value is double || value is bool))
        //    {
        //        throw new ArgumentException("params must be integer, string, double, boolean or null");
        //    }
        //}

        public object[] ToParamArray(ParameterInfo[] methodParameters)
        {
            var rslt = new List<object>();

            if (Length > methodParameters.Length)
            {
                throw new ApplicationException($"Too many arguments");
            }

            Func<int, ParameterInfo, JToken> _GetJTokenForParam;
            if (IsKwargs)
            {
                _GetJTokenForParam = new Func<int, ParameterInfo, JToken>((_, methodParam) =>
                {
                    JToken jToken;
                    if (!Kwargs.TryGetValue(methodParam.Name, out jToken))
                    {
                        if (methodParam.IsOptional) return null;
                        throw new ApplicationException("Missing required parameter: " + methodParam.Name);
                    }
                    return jToken;
                });
            }
            else
            {
                _GetJTokenForParam = new Func<int, ParameterInfo, JToken>((idx, methodParam) =>
                {
                    if (idx >= Args.Count) {
                        if (methodParam.IsOptional) return null;
                        throw new ApplicationException("Missing required parameter: " + methodParam.Name);
                    }
                    return Args[idx];
                });
            }
            for (var i = 0; i < methodParameters.Length; ++i)
            {
                var methodParam = methodParameters[i];
                var jToken = _GetJTokenForParam(i, methodParam);
                rslt.Add(jToken.ToObject(methodParam.ParameterType));
            }

            return rslt.ToArray();
        }

        public static JsonRpcRequestParams FromJToken(JToken jToken)
        {
            // TODO: Remove this method, since we are now storing param as JToken
            //object ParseItem(JToken jItem)
            //{
            //    // Eventually wants to use constant pattern and discard pattern switch statement in C# 7.0 and 8.0 respectively
            //    switch (jItem.Type)
            //    {
            //        case JTokenType.Null:
            //            return null;
            //        case JTokenType.String:
            //            return jItem.Value<string>();
            //        case JTokenType.Integer:
            //            return jItem.Value<int>();
            //        case JTokenType.Float:
            //            return jItem.Value<double>();
            //        case JTokenType.Boolean:
            //            return jItem.Value<bool>();
            //        case JTokenType.Object:
            //            return jItem.Value<JObject>();
            //        case JTokenType.Array:
            //            return jItem.Value<JArray>();
            //        default:
            //            throw new ArgumentException("Unexpected parameter JToken type: " + jItem.Type.ToString());
            //    }
            //}

            JsonRpcRequestParams rslt = new JsonRpcRequestParams();
            if (jToken.Type == JTokenType.Array)
            {
                var jArray = jToken as JArray;
                foreach(var jItem in jArray)
                {
                    rslt.Add(jItem);
                }

            }
            else if (jToken.Type == JTokenType.Object)
            {
                var jObject = jToken as JObject;
                foreach (var item in jObject)
                { 
                    rslt.Add(item.Key, item.Value);
                }
            }
            else
            {
                throw new ArgumentException("JObject must be array or object");
            }

            return rslt;
        }
    }

    public class JsonRpcRequestJsonConverter : JsonConverter<JsonRpcRequest>
    {
        public override JsonRpcRequest ReadJson(JsonReader reader, Type objectType, JsonRpcRequest existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var jObject = JObject.Load(reader);
            var jsonRpcVersion = jObject["jsonrpc"];
            jObject.ThrowIfMissingKey("jsonrpc");
            jObject.ThrowIfMissingKey("method");
            jObject.ThrowIfNull("jsonrpc");
            // JSON RPC 2.0 discourages using null as message id
            // But I find my life easier when not accepting null as message id
            jObject.ThrowIfNull("id");
            jObject.ThrowIfNotType("id", JTokenType.Integer, JTokenType.String);
            jObject.ThrowIfNull("method");
            jObject.ThrowIfNotType("method", JTokenType.String);

            if (jsonRpcVersion.Value<string>() != "2.0")
            {
                throw new ApplicationException("Unsupported JSON RPC version " + jsonRpcVersion);
            }

            var jMessageId = jObject["id"];
            var jMethodName = jObject["method"];
            var jParams = jObject["params"];
            JsonRpcRequestParams parameters = null;
            if(jParams != null)
            {
                jObject.ThrowIfNull("params");
                jObject.ThrowIfNotType("params", JTokenType.Array, JTokenType.Object);
                parameters = JsonRpcRequestParams.FromJToken(jParams);
            }

            var rslt = jMessageId.Type == JTokenType.Integer
                ? new JsonRpcRequest(jMessageId.Value<int>(), jMethodName.Value<string>(), parameters)
                : new JsonRpcRequest(jMessageId.Value<string>(), jMethodName.Value<string>(), parameters);

            return rslt;
        }

        public override void WriteJson(JsonWriter writer, JsonRpcRequest value, JsonSerializer serializer)
        {
            var jObject = new JObject()
            {
                { "jsonrpc", "2.0" },
                { "method", value.Method }
            };

            if (value.Params != null)
            {
                jObject.Add("params", value.Params.IsKwargs
                    ? JToken.FromObject(value.Params.Kwargs)
                    : JToken.FromObject(value.Params.Args)
                );
            }
            if (value.Id != null)
            {
                jObject.Add("id", JToken.FromObject(value.Id));
            }

            serializer.Serialize(writer, jObject);
        }
    }

    // TODO: Create JsonRpcRequestParamsJsonConverter

    internal static class JsonRpcJObjectExtension
    {
        internal static void ThrowIfMissingKey(this JObject jObject, string key)
        {
            if (!jObject.ContainsKey(key))
            {
                throw new ApplicationException($"Missing key '{key}'");
            }
        }
        internal static void ThrowIfNull(this JObject jObject, string key)
        {
            if (jObject[key].Type == JTokenType.Null)
            {
                throw new ApplicationException($"Null value in key '{key}'");
            }
        }
        internal static void ThrowIfNotType(this JObject jObject, string key, params JTokenType[] types)
        {
            if(!types.Any(type => type == jObject[key].Type))
            {
                throw new ApplicationException($"Unexpected type for value in key '{key}': " + jObject[key].Type.ToString());
            }
        }
    }
}
