using System;
using System.Linq;

namespace Content.UIPreviewer;

public static class ConstrHelper
{
    public delegate bool ConstrPredictionDelegate(Type type, out object? result);
    public static object? CreateDefaultValue(Type type, ConstrPredictionDelegate predict)
    {
        if (predict(type, out var result))
        {
            return result;
        }

        if(type.IsValueType)
            return Activator.CreateInstance(type);

        var ctor = type.GetConstructors().First();
        var parameters = ctor.GetParameters()
            .Select(p => CreateDefaultValue(p.ParameterType, predict))
            .ToArray();

        return ctor.Invoke(parameters);
    }
}
