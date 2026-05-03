using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace NoOverlayPlugin.JsonRpc
{
    /// <summary>
    /// Represents a procedure that can be added to a <see cref="JsonRpcProcessor"/><br/>
    /// Note: Currently late binding is used, performance will not be ideal when used in high volume environment
    /// </summary>
    public class JsonRpcProcedure
    {
        /// <summary>
        /// Create an instance of <see cref="JsonRpcProcedure"/> that wraps the method of provided delegate
        /// </summary>
        /// <param name="delegate">Delegate that will be called when procedure is invoked</param>
        public JsonRpcProcedure(Delegate @delegate)
        {
            Method = @delegate.Method;
            Parameters = @delegate.Method.GetParameters();
            Target = @delegate.Target;
            IsStatic = @delegate.Method.IsStatic;
        }

        /// <summary>
        /// Gets the method wrapped by this procedure
        /// </summary>
        public MethodInfo Method { get; }

        /// <summary>
        /// Gets the class instance on which the wrapped method will be invoked with.
        /// </summary>
        public object Target { get; }

        /// <summary>
        /// Gets a list of parameter infos of wrapped .
        /// </summary>
        public ParameterInfo[] Parameters { get; }

        /// <summary>
        /// Gets a boolean value indicating whether the wrapped method is static.
        /// </summary>
        public bool IsStatic { get; }

        /// <summary>
        /// Invokes wrapped method with specified <see cref="JsonRpcRequestParams"/>.
        /// </summary>
        /// <param name="requestParams">An params object that will be converted to an array of objects with appropriate type for invoked method</param>
        /// <returns>A <see cref="JToken"/> representing the result of invoction</returns>
        public JToken Invoke(JsonRpcRequestParams requestParams)
        {
            var rslt = Method.Invoke(Method.IsStatic ? null : Target, requestParams.ToParamArray(Parameters));
            return rslt is null ? JValue.CreateNull()
                : rslt is JToken ? rslt as JToken
                : JToken.FromObject(rslt);
        }
    }

    //[AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    //public class JsonRpcProcedureAttribute : Attribute {}
}
