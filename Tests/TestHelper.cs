using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Unbreakable.Tests {
    public static class TestHelper {
        public delegate object Invoke(params object[] args);

        public static MemoryStream Compile(string code, bool allowUnsafe = false) {
            var compilation = CSharpCompilation.Create(
                "_",
                new[] { CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest)) },
                GetReferencesForCompile(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: allowUnsafe)
            );

            var stream = new MemoryStream();
            var result = compilation.Emit(stream);
            Assert.True(result.Success, string.Join("\r\n", result.Diagnostics));
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        private static IEnumerable<PortableExecutableReference> GetReferencesForCompile() {
            PortableExecutableReference AssemblyOf(Type type) => MetadataReference.CreateFromFile(type.Assembly.Location);

            yield return AssemblyOf(typeof(object));
            yield return AssemblyOf(typeof(Stack<>));
            yield return AssemblyOf(typeof(Enumerable));

            #if NETCORE
            var trustedAssemblyPaths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator);
            yield return MetadataReference.CreateFromFile(trustedAssemblyPaths.Single(p => p.EndsWith("mscorlib.dll")));
            yield return MetadataReference.CreateFromFile(trustedAssemblyPaths.Single(p => p.EndsWith("System.Runtime.dll")));

            yield return AssemblyOf(typeof(Console));
            yield return AssemblyOf(typeof(Regex));
            #endif

            yield return AssemblyOf(typeof(TestHelper));
        }

        public static Invoke RewriteAndGetMethodWrappedInScope(string code, string typeName, string methodName, AssemblyGuardSettings? guardSettings = null) {
            var assemblySourceStream = Compile(code);
            var assemblyTargetStream = new MemoryStream();

            var token = AssemblyGuard.Rewrite(assemblySourceStream, assemblyTargetStream, guardSettings);

            return args => {
                using (token.Scope(new RuntimeGuardSettings { TimeLimit = TimeSpan.FromMilliseconds(100) })) {
                    var method = GetInstanceMethod(assemblyTargetStream, typeName, methodName);
                    return method(args);
                }
            };
        }

        private static Invoke GetInstanceMethod(MemoryStream assemblyStream, string typeName, string methodName) {
            var assembly = Assembly.Load(assemblyStream.ToArray());
            var type = assembly.GetType(typeName);
            var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var instance = !method.IsStatic ? Activator.CreateInstance(type) : null;

            return args => method.Invoke(instance, args);
        }
    }
}
