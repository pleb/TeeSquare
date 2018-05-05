﻿using System;
using System.ComponentModel;
using TeeSquare.TypeMetadata;

namespace TeeSquare.Writers
{
    public class ClassWriter : ICodePart
    {
        private readonly string _name;
        private readonly string[] _genericTypeParams;
        private readonly TypeConfigurer _config;
        private string _abstract = string.Empty;

        public ClassWriter(string name, string[] genericTypeParams)
        {
            _name = name;
            _genericTypeParams = genericTypeParams;
            _config = new TypeConfigurer();
        }

        public ClassWriter Abstract()
        {
            _abstract = "abstract ";
            return this;
        }

        public void With(Action<ITypeConfigurer> configure)
        {
            configure(_config);
        }


        public void WriteTo(ICodeWriter writer)
        {
            writer.Write($"export {_abstract}class ");
            writer.WriteType(_name, _genericTypeParams);
            writer.OpenBrace();
            foreach (var prop in _config.Properties)
            {
                writer.Write($"{prop.Name}: ", true);
                writer.WriteType(prop.Type, prop.GenericTypeParams);
                writer.WriteLine(";", false);
            }

            foreach (var method in _config.Methods)
            {
                writer.Write(method.IsStatic ? "static " : string.Empty, true);
                writer.Write($"{method.Id.Name}(");
                bool first = true;
                foreach (var param in method.Params)
                {
                    if (!first)
                    {
                        writer.Write(", ");
                    }

                    writer.Write($"{param.Name}: ");
                    writer.WriteType(param.Type, param.GenericTypeParams);
                    first = false;
                }

                writer.Write("): ");
                writer.WriteType(method.Id.Type, method.Id.GenericTypeParams);
                writer.OpenBrace();

                method.WriteBody(writer);

                writer.CloseBrace();
            }

            writer.CloseBrace();
        }
    }
}