﻿using System.Reflection;
using BlurkCompare;
using NUnit.Framework;
using TeeSquare.DemoApi.Controllers;

namespace TeeSquare.WebApi.Tests
{
    [TestFixture]
    public class WhenGeneratingTypeScriptForWebApi
    {
        public Assembly WebApiAssembly => typeof(ValuesController).Assembly;


        [Test]
        public void AllRoutesAndDtosAreOutput()
        {
            var res = TeeSquareWebApi.GenerateForAssemblies(WebApiAssembly)
                .WriteToString();

            Blurk.CompareImplicitFile("ts")
                .To(res)
                .AssertAreTheSame(Assert.Fail);
        }
    }
}
