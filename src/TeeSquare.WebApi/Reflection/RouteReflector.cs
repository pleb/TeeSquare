﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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
            CheckConfigured();
            var controllers = assembly.GetExportedTypes()
                .Where(t => (baseController ?? StaticConfig.Instance.ControllerType).IsAssignableFrom(t));

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
                var route = _options.BuildRouteStrategy(controller, action, _options.DefaultRoute);

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
            if (parameterInfo.GetCustomAttributes(StaticConfig.Instance.FromBodyAttribute).Any())
                return ParameterKind.Body;
            if (parameterInfo.GetCustomAttributes(StaticConfig.Instance.FromQueryAttribute).Any())
                return ParameterKind.Query;
            if (parameterInfo.GetCustomAttributes(StaticConfig.Instance.FromRouteAttribute).Any())
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
            if (action.GetCustomAttributes(StaticConfig.Instance.HttpPutAttribute).Any())
                return (RequestInfo.Put, HttpMethod.Put);
            if (action.GetCustomAttributes(StaticConfig.Instance.HttpPostAttribute).Any())
                return (RequestInfo.Post, HttpMethod.Post);
            if (action.GetCustomAttributes(StaticConfig.Instance.HttpDeleteAttribute).Any())
                return (RequestInfo.Delete, HttpMethod.Delete);
            return (RequestInfo.Get, HttpMethod.Get);
        }

        internal static string DefaultBuildRouteStrategy(Type controller, MethodInfo action, string defaultRoute)
        {
            var controllerRouteTemplate = controller.GetCustomAttributes(StaticConfig.Instance.RouteAttribute)
                .Select(StaticConfig.Instance.GetTemplateFromRouteAttribute)
                .FirstOrDefault();
            var methodRouteTemplate = action.GetCustomAttributes(StaticConfig.Instance.RouteAttribute)
                .Select(StaticConfig.Instance.GetTemplateFromRouteAttribute)
                .FirstOrDefault();
            var httpMethodTemplate = action.GetCustomAttributes()
                .Where(a => StaticConfig.Instance.HttpMethodBaseAttribute.IsInstanceOfType(a))
                .Select(StaticConfig.Instance.GetTemplateFromHttpMethodAttribute)
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

            var routeTemplate = parts.Any() ? string.Join("/", parts) : defaultRoute;


            return ReplaceRoutePlaceholders(routeTemplate, controller, action)
                .Trim('/');
        }

        public static string ReplaceRoutePlaceholders(string routeTemplate, Type controller, MethodInfo action)
        {
            var partRegexSquare = new Regex(@"\[(?<part>[^]=?]+)[?]?(=[^]]+)?\]");
            var partRegexBraces = new Regex(@"\{(?<part>[^}=?]+)[?]?(=[^}]+)?\}");

            string MatchReplacer(Match match) => match.Groups["part"].Value switch
            {
                "controller" => controller.Name.Replace("Controller", "").ToLower(),
                "action" => action.Name.ToLower(),
                _ => action.GetParameters().Any(p => p.Name.Equals(match.Groups["part"].Value))
                    ? $"{{{match.Groups["part"].Value}}}"
                    : ""
            };


            return partRegexSquare.Replace(
                partRegexBraces.Replace(routeTemplate, MatchReplacer),
                MatchReplacer);
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
                    w.WriteLine(".map(x => Array.isArray(x.v)");
                    w.Indent();
                    w.WriteLine("? x.v.map(v => `${encodeURIComponent(x.k)}=${encodeURIComponent(v)}`).join('&')");
                    w.WriteLine(": `${encodeURIComponent(x.k)}=${encodeURIComponent(x.v)}`)");
                    w.Deindent();
                    w.WriteLine(".join('&');");
                    w.Deindent();
                    w.WriteLine("return q ? `?${q}` : '';");
                });
        }

        private void CheckConfigured()
        {
            if (StaticConfig.Instance.ControllerType == null)
            {
                throw new Exception(
                    "WebApi reflector has not been configured. Please include a TeeSquare.WebApi.{Platform} library or provide a manual configuration.");
            }
        }

        public void WriteTo(TypeScriptWriter writer)
        {
            CheckConfigured();
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
                                        p.Param(rp.Name,
                                            _options.TypeConverter.Convert(rp.Type)
                                                .MakeOptional(rp.Kind == ParameterKind.Query));
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
