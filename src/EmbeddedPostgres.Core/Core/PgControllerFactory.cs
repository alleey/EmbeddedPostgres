using EmbeddedPostgres.Core.Interfaces;
using System;
using System.Collections.Generic;

namespace EmbeddedPostgres.Core;

internal class PgControllerFactory
{
    private readonly Dictionary<Type, Func<string, PgInstanceConfiguration, object>> _controllerFactories;

    public PgControllerFactory(Dictionary<Type, Func<string, PgInstanceConfiguration, object>> controllerFactories)
    {
        _controllerFactories = controllerFactories;
    }

    public T GetController<T>(string pathOrFilename, PgInstanceConfiguration instance)
        where T : class
    {
        return _controllerFactories[typeof(T)](pathOrFilename, instance) as T;
    }
}
