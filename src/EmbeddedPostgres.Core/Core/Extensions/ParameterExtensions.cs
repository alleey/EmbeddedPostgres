using System;
using System.Collections.Generic;

namespace EmbeddedPostgres.Core.Extensions;

/// <summary>
/// Provides extension methods for retrieving parameters from a dictionary of string keys to object values.
/// </summary>
public static class ParameterExtensions
{
    /// <summary>
    /// Retrieves a boolean parameter from the specified dictionary. If the parameter is not found or cannot be 
    /// converted to a boolean, the <paramref name="defaultValue"/> is returned.
    /// </summary>
    /// <param name="parameters">The dictionary containing parameters.</param>
    /// <param name="key">The key of the parameter to retrieve.</param>
    /// <param name="defaultValue">The default value to return if the parameter is not found or invalid.</param>
    /// <returns>The boolean value of the parameter or the <paramref name="defaultValue"/>.</returns>
    public static bool GetBoolParameter(this IDictionary<string, object> parameters, string key, bool defaultValue = false)
    {
        if (parameters.TryGetValue(key, out var value))
        {
            if (value is bool boolValue)
            {
                return boolValue;
            }
            if (value is string strValue && bool.TryParse(strValue, out var result))
            {
                return result;
            }
        }
        return defaultValue;
    }

    /// <summary>
    /// Retrieves an integer parameter from the specified dictionary. If the parameter is not found or cannot be 
    /// converted to an integer, the <paramref name="defaultValue"/> is returned.
    /// </summary>
    /// <param name="parameters">The dictionary containing parameters.</param>
    /// <param name="key">The key of the parameter to retrieve.</param>
    /// <param name="defaultValue">The default value to return if the parameter is not found or invalid.</param>
    /// <returns>The integer value of the parameter or the <paramref name="defaultValue"/>.</returns>
    public static int GetIntParameter(this IDictionary<string, object> parameters, string key, int defaultValue = 0)
    {
        if (parameters.TryGetValue(key, out var value))
        {
            if (value is int intValue)
            {
                return intValue;
            }
            if (value is string strValue && int.TryParse(strValue, out var result))
            {
                return result;
            }
        }
        return defaultValue;
    }

    /// <summary>
    /// Retrieves an enum parameter of type <typeparamref name="T"/> from the specified dictionary. If the 
    /// parameter is not found or cannot be converted to the specified enum type, the <paramref name="defaultValue"/>
    /// is returned.
    /// </summary>
    /// <typeparam name="T">The enum type to convert the parameter to.</typeparam>
    /// <param name="parameters">The dictionary containing parameters.</param>
    /// <param name="key">The key of the parameter to retrieve.</param>
    /// <param name="defaultValue">The default value to return if the parameter is not found or invalid.</param>
    /// <returns>The enum value of the parameter or the <paramref name="defaultValue"/>.</returns>
    public static T GetEnumParameter<T>(this IDictionary<string, object> parameters, string key, T defaultValue = default) where T : struct
    {
        if (parameters.TryGetValue(key, out var value))
        {
            if (value is T enumValue)
            {
                return enumValue;
            }
            if (value is string strValue && Enum.TryParse(strValue, out T result))
            {
                return result;
            }
        }
        return defaultValue;
    }

    /// <summary>
    /// Retrieves a string parameter from the specified dictionary. If the parameter is not found, the 
    /// <paramref name="defaultValue"/> is returned.
    /// </summary>
    /// <param name="parameters">The dictionary containing parameters.</param>
    /// <param name="key">The key of the parameter to retrieve.</param>
    /// <param name="defaultValue">The default value to return if the parameter is not found.</param>
    /// <returns>The string value of the parameter or the <paramref name="defaultValue"/>.</returns>
    public static string GetStringParameter(this IDictionary<string, object> parameters, string key, string defaultValue = "")
    {
        if (parameters.TryGetValue(key, out var value))
        {
            return value?.ToString() ?? defaultValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// Retrieves a parameter of type <typeparamref name="T"/> from the specified dictionary. If the 
    /// parameter is not found or cannot be converted to the specified type, the <paramref name="defaultValue"/>
    /// is returned.
    /// </summary>
    /// <typeparam name="T">The type of the parameter to convert to.</typeparam>
    /// <param name="parameters">The dictionary containing parameters.</param>
    /// <param name="key">The key of the parameter to retrieve.</param>
    /// <param name="defaultValue">The default value to return if the parameter is not found or invalid.</param>
    /// <returns>The value of the parameter or the <paramref name="defaultValue"/>.</returns>
    public static T GetParameter<T>(this IDictionary<string, object> parameters, string key, T defaultValue = default)
    {
        if (parameters.TryGetValue(key, out var value))
        {
            if (value is T objectValue)
            {
                return objectValue;
            }
        }
        return defaultValue;
    }
}
