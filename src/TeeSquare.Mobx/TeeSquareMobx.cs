using System;
using TeeSquare.Reflection;

namespace TeeSquare.Mobx
{
    public class TeeSquareMobx
    {
        public static void ConfigureMobxWriter(ReflectiveWriterOptions options) =>
            ConfigureMobxWriter(new MobxOptions())(options);

        public static Action<ReflectiveWriterOptions> ConfigureMobxWriter(IMobxOptions mobxOptions) =>
            (options) =>
            {
                var mobxWriterFactory = new MobxModelWriterFactory(mobxOptions);
                options.ComplexTypeStrategy = (writer, typeInfo) =>
                    writer.WriteSnippet(mobxWriterFactory.BuildModel(typeInfo));

                options.Types.AddLiteralImport("mobx-state-tree", "types");
                options.Types.AddLiteralImport("mobx-state-tree", "Instance");
            };
    }
}
