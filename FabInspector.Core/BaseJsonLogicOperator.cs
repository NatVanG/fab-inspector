using System;
using System.Text.Json.Serialization;

namespace FabInspector.Core
{
    /// <summary>
    /// Base class for JSON Logic operators, providing common registration logic.
    /// </summary>
    public abstract class BaseJsonLogicOperator : IJsonLogicOperator
    {
        public abstract string OperatorName { get; }
        public abstract Type RuleType { get; }

        public virtual void Register(JsonSerializerContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (Json.Logic.RuleRegistry.GetRule(OperatorName) == null)
            {
                // Use reflection to call the generic AddRule<T> method with the correct RuleType
                var method = typeof(Json.Logic.RuleRegistry).GetMethod("AddRule", new[] { typeof(JsonSerializerContext) });
                if (method == null)
                    throw new InvalidOperationException("Could not find  method on RuleRegistry.");

                var generic = method.MakeGenericMethod(RuleType);
                generic.Invoke(null, new object[] { context });
            }
            else
            {
                //TODO: handle already-registered case (no-op or log)
            }
        }
    }
}