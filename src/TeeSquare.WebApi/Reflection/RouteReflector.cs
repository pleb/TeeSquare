﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using TeeSquare.Reflection;
using TeeSquare.Writers;

namespace TeeSquare.WebApi.Reflection
{
    public class RouteReflector
    {
        private readonly RouteReflectorOptions _options;

        private readonly List<RequestInfo> _requests;


        public RouteReflector(RouteReflectorOptions options)
        {
            _options = options;
            _requests = new List<RequestInfo>();
        }

        public void AddAssembly(Assembly assembly)
        {
            var controllers = assembly.GetExportedTypes()
                .Where(t => typeof(Controller).IsAssignableFrom(t));

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
                var route = BuildRoute(controller, action);

                var factory = GetRequestFactory(action);
                var requestParams = GetRequestParams(action);


                _requests.Add(factory(_options.Namer.RouteName(controller, action, route),
                    route,
                    action.ReturnType,
                    requestParams
                ));
            }
        }

        private ParamInfo[] GetRequestParams(MethodInfo action)
        {
            return action.GetParameters()
                .Select(p => new ParamInfo
                {
                    Kind = GetParameterKind(p),
                    Name = p.Name,
                    Type = p.ParameterType
                }).ToArray();
        }

        public ParameterKind GetParameterKind(ParameterInfo parameterInfo)
        {
            if (parameterInfo.GetCustomAttributes<FromBodyAttribute>().Any())
                return ParameterKind.Body;
            if (parameterInfo.GetCustomAttributes<FromQueryAttribute>().Any())
                return ParameterKind.Query;
            if (parameterInfo.GetCustomAttributes<FromRouteAttribute>().Any())
                return ParameterKind.Route;
            // TODO: Check if in route, else use query
            return ParameterKind.Route;
        }

        private RequestFactory GetRequestFactory(MethodInfo action)
        {
            if (action.GetCustomAttributes<HttpPutAttribute>().Any())
                return RequestInfo.Put;
            if (action.GetCustomAttributes<HttpPostAttribute>().Any())
                return RequestInfo.Post;
            if (action.GetCustomAttributes<HttpDeleteAttribute>().Any())
                return RequestInfo.Delete;
            return RequestInfo.Get;
        }

        private string BuildRoute(Type controller, MethodInfo action)
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

        public void WriteTo(TypeScriptWriter writer)
        {
            writer.WriteInterface("GetRequest", "TResponse")
                .Configure(i =>
                {
                    i.AddProperty("url", "string");
                    i.AddProperty("method", "'GET'");
                });
            writer.WriteInterface("DeleteRequest", "TResponse")
                .Configure(i =>
                {
                    i.AddProperty("url", "string");
                    i.AddProperty("method", "'DELETE'");
                });

            writer.WriteInterface("PostRequest", "TRequest", "TResponse")
                .Configure(i =>
                {
                    i.AddProperty("data", "TRequest");
                    i.AddProperty("url", "string");
                    i.AddProperty("method", "'POST'");
                });
            writer.WriteInterface("PutRequest", "TRequest", "TResponse")
                .Configure(i =>
                {
                    i.AddProperty("data", "TRequest");
                    i.AddProperty("url", "string");
                    i.AddProperty("method", "'PUT'");
                });


            writer.WriteClass("RequestFactory")
                .Configure(c =>
                {
                    c.MakeAbstract();
                    c.AddMethod("toQuery")
                        .WithReturnType("string")
                        .WithParams(p => p.Param("o", "{[key: string]: any}"))
                        .Static()
                        .WithBody(w =>
                        {
                            w.WriteLine("let q = Object.keys(o)");
                            w.Indent();
                            w.WriteLine(".map(k => ({k, v: o[k]}))");
                            w.WriteLine(".filter(x => x.v !== undefined && x.v !== null)");
                            w.WriteLine(".map(x => `${encodeURIComponent(x.k)}=${encodeURIComponent(x.v)}`)");
                            w.WriteLine(".join('&');");
                            w.Deindent();
                            w.WriteLine("return q && `?${q}` || '';");
                        });


                    foreach (var req in _requests)
                    {
                        var methodBuilder = c.AddMethod($"{req.Method.GetName()}{req.Name}")
                            .Static();

                        if (req.Method.HasRequestBody())
                        {
                            methodBuilder
                                .WithReturnType($"{req.Method.GetName()}Request",
                                    _options.Namer.TypeName(req.GetRequestBodyType()),
                                    _options.Namer.TypeName(req.ResponseType));
                        }
                        else
                        {
                            methodBuilder
                                .WithReturnType($"{req.Method.GetName()}Request",
                                    _options.Namer.TypeName(req.ResponseType));
                        }

                        methodBuilder.WithParams(p =>
                            {
                                foreach (var rp in req.RequestParams.Where(x => x.Kind != ParameterKind.Body))

                                {
                                    if (rp.Type.IsNullable(out var underlyingType))
                                    {
                                        p.Param(rp.Name + "?", _options.Namer.TypeName(underlyingType));
                                        continue;
                                    }

                                    p.Param(rp.Name, _options.Namer.TypeName(rp.Type));
                                }

                                if (req.Method.HasRequestBody())
                                    p.Param("data", _options.Namer.TypeName(req.GetRequestBodyType()));
                            })
                            .WithBody(w =>
                            {
                                var queryParams = req.RequestParams.Where(x => x.Kind == ParameterKind.Query).ToArray();
                                if (queryParams.Any())
                                {
                                    w.Write("let query = RequestFactory.toQuery({", true);
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
            var types = _requests.Select(r => r.ResponseType)
                .Union(_requests.SelectMany(r => r.RequestParams.Select(p => p.Type)));

            var rWriter = new ReflectiveWriter(_options.BuildWriterOptions());
            rWriter.AddTypes(types.ToArray());
            rWriter.WriteTo(writer);
        }
    }
}
