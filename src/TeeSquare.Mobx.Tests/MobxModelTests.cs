using System;
using BlurkCompare;
using NUnit.Framework;
using TeeSquare.Tests.Reflection.FakeDomain;

namespace TeeSquare.Mobx.Tests
{
    [TestFixture]
    public class MobxModelTests
    {
        private const bool CreateFileForActual = false;

        [Test]
        public void LeafClass()
        {
            var res = TeeSquareFluent.ReflectiveWriter()
                .Configure(TeeSquareMobx.ConfigureMobxWriter)
                .AddTypes(typeof(Location))
                .WriteToString();

            Blurk.CompareImplicitFile("ts")
                .To(res, CreateFileForActual)
                .AssertAreTheSame(Assert.Fail);
        }


        [Test]
        public void SmallTree()
        {
            var res = TeeSquareFluent.ReflectiveWriter()
                .Configure(TeeSquareMobx.ConfigureMobxWriter)
                .AddTypes(typeof(Name))
                .WriteToString();

            Blurk.CompareImplicitFile("ts")
                .To(res, CreateFileForActual)
                .AssertAreTheSame(Assert.Fail);
        }

        [Test]
        public void NullableProperties()
        {
            var res = TeeSquareFluent.ReflectiveWriter()
                .Configure(TeeSquareMobx.ConfigureMobxWriter)
                .AddTypes(typeof(Book))
                .WriteToString();

            Blurk.CompareImplicitFile("ts")
                .To(res, CreateFileForActual)
                .AssertAreTheSame(Assert.Fail);
        }

        [Test]
        public void AlternativeNullableProperties()
        {
            var res = TeeSquareFluent.ReflectiveWriter()
                .Configure(TeeSquareMobx.ConfigureMobxWriter(new MobxOptions()
                {
                    OptionalType = "types.maybeNull({0})"
                }))
                .AddTypes(typeof(Book))
                .WriteToString();

            Blurk.CompareImplicitFile("ts")
                .To(res, CreateFileForActual)
                .AssertAreTheSame(Assert.Fail);
        }

        [Test]
        public void EntireTree()
        {
            var res = TeeSquareFluent.ReflectiveWriter()
                .Configure(TeeSquareMobx.ConfigureMobxWriter)
                .AddTypes(typeof(Library))
                .WriteToString();

            Blurk.CompareImplicitFile("ts")
                .To(res, CreateFileForActual)
                .AssertAreTheSame(Assert.Fail);
        }

        [Test]
        public void OverrideNames()
        {
            var res = TeeSquareFluent.ReflectiveWriter()
                .Configure(TeeSquareMobx.ConfigureMobxWriter(new MobxOptions
                {
                    EmitInstanceType = false
                }, new CustomMobxTypeConverter()))
                .AddTypes(typeof(Library))
                .WriteToString();

            Blurk.CompareImplicitFile("ts")
                .To(res, CreateFileForActual)
                .AssertAreTheSame(Assert.Fail);
        }

        class CustomMobxTypeConverter: MobxTypeConverter
        {
            public override string TypeName(Type type)
            {
                return base.TypeName(type) + "BaseModel";
            }
        }
    }
}
