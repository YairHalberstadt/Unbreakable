using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AshMind.Extensions;
using Unbreakable.Policy;
using Unbreakable.Policy.Internal;
using Unbreakable.Policy.Rewriters;
using Xunit;

namespace Unbreakable.Tests.Unit {
    public class DefaultApiPolicyFactoryTests {
        [Fact]
        public void CreateSafeDefaultPolicy_IncludesAddCallRewriter_ForMethodsNamedAddInsertEnqueueAndPush() {
            var methodNames = new HashSet<string> {
                "Add",
                "Insert",
                "Enqueue",
                "Push"
            };
            var excludedTypes = new HashSet<Type> {
                typeof(DateTime),
                typeof(DateTimeOffset),
                typeof(Decimal),
                typeof(string),
                typeof(TimeSpan)
            };
            AssertEachMatchingMethodHasRewriterOfType<AddCallRewriter>(
                m => methodNames.Contains(m.Name) && !excludedTypes.Contains(m.DeclaringType)
            );
        }

        [Fact]
        public void CreateSafeDefaultPolicy_IncludesCountArgumentRewriter_ForMethodsWithParamerNamedCountOrCapacity() {
            var excluded = new HashSet<(Type, string)> {
                (typeof(string), nameof(string.Join)),
                (typeof(string), nameof(string.Split))
            };
            AssertEachMatchingMethodHasRewriterOfType<CountArgumentRewriter>(
                m => m.GetParameters().Any(p => p.Name == "count" || p.Name == "capacity")
                  && !m.GetParameters().Any(p => Regex.IsMatch(p.Name, "^(.+Index|index)$"))
                  && !excluded.Contains((m.DeclaringType, m.Name))
            );
        }

        [Fact]
        public void CreateSafeDefaultPolicy_IncludesDisposableReturnRewriter_ForMethodsReturningIDiposable() {
            AssertEachMatchingMethodHasRewriterOfType<DisposableReturnRewriter>(
                m => (m.IsConstructor ? m.DeclaringType : ((MethodInfo)m).ReturnType).IsAssignableTo<IDisposable>()
                  && m.Name != "GetEnumerator"
            );
        }

        private static void AssertEachMatchingMethodHasRewriterOfType<TApiMemberRewriter>(Func<MethodBase, bool> matcher)
            where TApiMemberRewriter : IMemberRewriter
        {
            var namespaceRules = new DefaultApiPolicyFactory().CreateSafeDefaultPolicy();
            foreach (var namespaceRule in namespaceRules.Namespaces) {
                if (namespaceRule.Value.Access == ApiAccess.Denied)
                    continue;

                foreach (var typeRule in namespaceRule.Value.Types) {
                    if (typeRule.Value.Access != ApiAccess.Allowed)
                        continue;

                    var type = FindType(namespaceRule.Key + "." + typeRule.Key);
                    foreach (var method in type.GetMembers().OfType<MethodBase>()) {
                        if (!matcher(method))
                            continue;

                        var rule = typeRule.Value.Members.GetValueOrDefault(method.Name);
                        Assert.True(
                            rule?.Rewriters.OfType<TApiMemberRewriter>().Any(),
                            $"Method {method} on type {type} does not have an explicit {typeof(TApiMemberRewriter).Name}."
                        );
                    }
                }
            }
        }

        private static Type FindType(string fullName) {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(a => a.GetType(fullName))
                .Where(t => t != null)
                .FirstOrDefault() ?? throw new Exception($"Type {fullName} was not found.");
        }
    }
}
