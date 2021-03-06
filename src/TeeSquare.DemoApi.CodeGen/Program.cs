﻿using System;
using System.Linq;
using TeeSquare.DemoApi.Controllers;
using TeeSquare.DemoApi.Hubs;
using TeeSquare.WebApi;
using TeeSquare.WebApi.Core22;

namespace TeeSquare.DemoApi.CodeGen
{
    class Program
    {
        static void Main(string[] args)
        {
            Core22Configurator.Configure();
            var outputPath = args
                .SkipWhile(a => a != "-o")
                .Skip(1)
                .FirstOrDefault();
            if (string.IsNullOrEmpty(outputPath))
            {
                throw new ArgumentException("Must provide value for output file '-o <output file path>'");
            }
            Console.WriteLine(outputPath);
            TeeSquareWebApi.GenerateForAssemblies(typeof(OtherController).Assembly)
                .Configure(options =>
                {
                    options.ReflectMethods = type =>
                        type.IsInterface && (type.Namespace?.StartsWith("TeeSquare") ?? false);})
                .AddTypes(typeof(IApplicationHubClient), typeof(IApplicationHubServer))
                .WriteToFile(outputPath);

        }
    }
}
