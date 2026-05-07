using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NoOverlayPlugin.JsonRpc
{
    // TODO: Create unit tests
    // TODO: Add error handling
    // TODO: Add documentations

    public interface IJsonRpcProcessor
    {
        void AddMethod(string methodName, Delegate @delegate);

        bool RemoveMethod(string methodName, Delegate @delegate);

        JsonRpcResponse Process(JsonRpcRequest request);

        JsonRpcResponse Process(string json);
    }

    public class JsonRpcProcessor : IJsonRpcProcessor
    {
        private Dictionary<string, JsonRpcProcedure> procedureByName = new Dictionary<string, JsonRpcProcedure>();

        public void AddMethod(string methodName, Delegate @delegate)
        {
            var key = GetMethodKey(methodName, @delegate);
            procedureByName.Add(key, new JsonRpcProcedure(@delegate));
        }

        public bool RemoveMethod(string methodName, Delegate @delegate)
        {
            var key = GetMethodKey(methodName, @delegate);
            return procedureByName.Remove(key);
        }

        public JsonRpcResponse Process(JsonRpcRequest request)
        {
            JsonRpcProcedure procedure;
            if (!procedureByName.TryGetValue(GetMethodKey(request), out procedure))
            {
                return JsonRpcResponse.MethodNotFound(request);
            }
            try
            {
                return new JsonRpcResponse(request.Id, procedure.Invoke(request.Params));
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
                return JsonRpcResponse.InternalError(request);
            }
        }

        public JsonRpcResponse Process(string json)
        {
            // TODO: Handle JsonRpcResponse here
            return Process(JsonRpcRequest.FromJson(json));
        }

        internal static string GetMethodKey(string methodName, Delegate @delegate)
        {
            var numOfParams = @delegate.Method.GetParameters().Count();
            return methodName + "/" + numOfParams.ToString();
        }

        internal static string GetMethodKey(JsonRpcRequest request)
        {
            var numOfParams = request.Params.Length;
            return request.Method + "/" + numOfParams.ToString();
        }
    }
}

