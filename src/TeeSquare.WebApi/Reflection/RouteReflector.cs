﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using TeeSquare.Reflection;
using TeeSquare.TypeMetadata;
using TeeSquare.Writers;

namespace TeeSquare.WebApi.Reflection
{
    public class RouteReflector
    {
        private readonly IRouteReflectorOptions _options;

        private readonly List<RequestInfo> _requests = new List<RequestInfo>();

        private readonly List<Type> _additionalTypes = new List<Type>();

        public RouteReflector(IRouteReflectorOptions options)
        {
            _options = options;
        }

        public void AddAdditionalTypes(Type[] types)
        {
            _additionalTypes.AddRange(types);
        }

        public void AddAssembly(Assembly assembly, Type baseController = null)
        {
            var controllers = assembly.GetExportedTypes()
                .Where(t => (baseController ?? typeof(Controller)).IsAssignableFrom(t));

            foreach (var controller in controllers)
            {
                AddController(controller);
            }
        }

        public void AddController(Type controller)
        {
            foreach (var action in controller
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod)
                .Where(a => a.IsAction()))
            {
                var route = _options.BuildRouteStrategy(controller, action);

                var (factory, method) = _options.GetHttpMethodAndRequestFactoryStrategy(controller, action);
                var requestParams = GetRequestParams(action, route, method);

                var returnType = _options.GetApiReturnTypeStrategy(controller, action);
                _requests.Add(factory(_options.RouteNamer.RouteName(controller, action, route, method),
                    route,
                    returnType,
                    requestParams
                ));
            }
        }

        internal static Type DefaultApiReturnTypeStrategy(Type controller, MethodInfo action)
        {
            return action.ReturnType;
        }

        private ParamInfo[] GetRequestParams(MethodInfo action, string route, HttpMethod method)
        {
            return action.GetParameters()
                .Select(p => new ParamInfo
                {
                    Kind = _options.GetParameterKindStrategy(p, route, method),
                    Name = p.Name,
                    Type = p.ParameterType
                }).ToArray();
        }

        internal static ParameterKind GetParameterKind(ParameterInfo parameterInfo, string route, HttpMethod method)
        {
            if (parameterInfo.GetCustomAttributes<FromBodyAttribute>().Any())
                return ParameterKind.Body;
            if (parameterInfo.GetCustomAttributes<FromQueryAttribute>().Any())
                return ParameterKind.Query;
            if (parameterInfo.GetCustomAttributes<FromRouteAttribute>().Any())
                return ParameterKind.Route;

            if (route.Contains($"{{{parameterInfo.Name}}}"))
            {
                return ParameterKind.Route;
            }

            if ((method == HttpMethod.Post || method == HttpMethod.Put)
                && IsPossibleDto(parameterInfo.ParameterType))
            {
                return ParameterKind.Body;
            }

            return ParameterKind.Query;
        }

        private static bool IsPossibleDto(Type type)
        {
            return !type.IsPrimitive
                   && type != typeof(string)
                   && !type.IsEnum
                   && !type.IsValueType;
        }

        internal static (RequestFactory factory, HttpMethod method) DefaultGetHttpMethodAndRequestFactory(
            Type controller, MethodInfo action)
        {
            if (action.GetCustomAttributes<HttpPutAttribute>().Any())
                return (RequestInfo.Put, HttpMethod.Put);
            if (action.GetCustomAttributes<HttpPostAttribute>().Any())
                return (RequestInfo.Post, HttpMethod.Post);
            if (action.GetCustomAttributes<HttpDeleteAttribute>().Any())
                return (RequestInfo.Delete, HttpMethod.Delete);
            return (RequestInfo.Get, HttpMethod.Get);
        }

        internal static string DefaultBuildRouteStrategy(Type controller, MethodInfo action)
        {
            var controllerRouteTemplate = controller.GetCustomAttributes<RouteAttribute>()
                .Select(r => r.Template)
                .FirstOrDefault();
            var methodRouteTemplate = action.GetCustomAttributes<RouteAttribute>()
                .Select(r => r.Template)
                .FirstOrDefault();
            var httpMethodTemplate = action.GetCustomAttributes()
                .OfType<HttpMethodAttribute>()
                .Select(r => r.Template)
                .FirstOrDefault();

            var parts = new List<string>();

            if (methodRouteTemplate?.StartsWith("/") != true && controllerRouteTemplate != null)
                parts.Add(controllerRouteTemplate);

            if (methodRouteTemplate != null)
                parts.Add(methodRouteTemplate);
            if (httpMethodTemplate != null)
            {
                if (httpMethodTemplate.StartsWith("/"))
                    parts = new List<string> {httpMethodTemplate};
                else
                    parts.Add(httpMethodTemplate);
            }


            return string.Join("/", parts)
                .Replace("[controller]", controller.Name.Replace("Controller", "").ToLower())
                .Trim('/');
        }

        private void WriteRequestTypesAndHelpers(TypeScriptWriter writer)
        {
            writer.WriteInterface("GetRequest", new TypeReference("TResponse"))
                .Configure(i =>
                {
                    i.AddProperty("url", new TypeReference("string"));
                    i.AddProperty("method", new TypeReference("'GET'"));
                });
            writer.WriteInterface("DeleteRequest", new TypeReference("TResponse"))
                .Configure(i =>
                {
                    i.AddProperty("url", new TypeReference("string"));
                    i.AddProperty("method", new TypeReference("'DELETE'"));
                });

            writer.WriteInterface("PostRequest", new TypeReference("TRequest"), new TypeReference("TResponse"))
                .Configure(i =>
                {
                    i.AddProperty("data", new TypeReference("TRequest"));
                    i.AddProperty("url", new TypeReference("string"));
                    i.AddProperty("method", new TypeReference("'POST'"));
                });
            writer.WriteInterface("PutRequest", new TypeReference("TRequest"), new TypeReference("TResponse"))
                .Configure(i =>
                {
                    i.AddProperty("data", new TypeReference("TRequest"));
                    i.AddProperty("url", new TypeReference("string"));
                    i.AddProperty("method", new TypeReference("'PUT'"));
                });
            writer.WriteFunction("toQuery")
                .WithReturnType(new TypeReference("string"))
                .WithParams(p => p.Param("o", new TypeReference("{[key: string]: any}")))
                .Static()
                .WithBody(w =>
                {
                    w.WriteLine("const q = Object.keys(o)");
                    w.Indent();
                    w.WriteLine(".map(k => ({k, v: o[k]}))");
                    w.WriteLine(".filter(x => x.v !== undefined && x.v !== null)");
                    w.WriteLine(".map(x => `${encodeURIComponent(x.k)}=${encodeURIComponent(x.v)}`)");
                    w.WriteLine(".join('&');");
                    w.Deindent();
                    w.WriteLine("return q && `?${q}` || '';");
                });
        }

        public void WriteTo(TypeScriptWriter writer)
        {
            _options.WriteHeader(writer);

            var rWriter = new ReflectiveWriter(_options);

            if (!_options.RequestHelperTypeOption.ShouldEmitTypes)
            {
                foreach (var type in _options.RequestHelperTypeOption.Types)
                    _options.Types.AddLiteralImport(_options.RequestHelperTypeOption.ImportFrom, type);
            }

            AddTypeDependencies(rWriter);

            rWriter.WriteImports(writer);


            if (_options.RequestHelperTypeOption.ShouldEmitTypes)
            {
                WriteRequestTypesAndHelpers(writer);
            }

            if (_requests.Any())
            {
                writer.WriteClass("RequestFactory")
                    .Configure(c =>
                    {
                        c.MakeAbstract();


                        foreach (var req in _requests)
                        {
                            var methodBuilder = c.AddMethod($"{req.Name}")
                                .Static();

                            if (req.Method.HasRequestBody())
                            {
                                var requestBodyType = _options.TypeConverter.Convert(req.GetRequestBodyType(), null);
                                if (requestBodyType.Optional)
                                {
                                    requestBodyType = new TypeReference($"{requestBodyType.FullName} | undefined");
                                }
                                methodBuilder
                                    .WithReturnType(new TypeReference($"{req.Method.GetName()}Request",
                                        new[]
                                        {
                                            requestBodyType,
                                            _options.TypeConverter.Convert(req.ResponseType, null)
                                        }));
                            }
                            else
                            {
                                methodBuilder
                                    .WithReturnType(new TypeReference($"{req.Method.GetName()}Request",
                                        new[]
                                        {
                                            _options.TypeConverter.Convert(req.ResponseType, null)
                                        }));
                            }

                            methodBuilder.WithParams(p =>
                                {
                                    foreach (var rp in req.RequestParams.Where(x => x.Kind != ParameterKind.Body))

                                    {
                                        p.Param(rp.Name, _options.TypeConverter.Convert(rp.Type).MakeOptional(rp.Kind == ParameterKind.Query));
                                    }

                                    if (req.Method.HasRequestBody())
                                        p.Param("data", _options.TypeConverter.Convert(req.GetRequestBodyType(), null));
                                })
                                .WithBody(w =>
                                {
                                    var queryParams = req.RequestParams.Where(x => x.Kind == ParameterKind.Query)
                                        .ToArray();
                                    if (queryParams.Any())
                                    {
                                        w.Write("const query = toQuery({", true);
                                        w.WriteDelimited(queryParams,
                                            (p, wr) => wr.Write(p.Name), ", ");
                                        w.WriteLine("});", false);
                                    }

                                    w.WriteLine("return {");
                                    w.Indent();
                                    w.WriteLine($"method: '{req.Method.GetName().ToUpper()}',");
                                    if (req.Method.HasRequestBody())
                                        w.WriteLine("data,");
                                    w.WriteLine(
                                        $"url: `{req.Path.Replace("{", "${")}{(queryParams.Any() ? "${query}" : "")}`");
                                    w.Deindent();
                                    w.WriteLine("};");
                                });
                        }
                    });
            }


            rWriter.WriteTo(writer, false);
        }

        private void AddTypeDependencies(ReflectiveWriter rWriter)
        {
            var types = _requests.Select(r => r.ResponseType)
                .Union(_requests.SelectMany(r => r.RequestParams.Select(p => p.Type)))
                .Union(_additionalTypes);

            rWriter.AddTypes(types.ToArray());
        }
    }
}
