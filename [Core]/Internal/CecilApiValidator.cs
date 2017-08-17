﻿using System;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unbreakable.Policy;

namespace Unbreakable.Internal {
    internal class CecilApiValidator {
        private readonly IApiFilter _filter;
        private readonly AssemblyDefinition _userAssembly;

        public CecilApiValidator(IApiFilter filter, AssemblyDefinition userAssembly) {
            _filter = filter;
            _userAssembly = userAssembly;
        }

        public void ValidateDefinition(ModuleDefinition definition) {
            ValidateCustomAttributes(definition);
        }

        public void ValidateDefinition(IMemberDefinition definition) {
            ValidateCustomAttributes(definition);
            // TODO: validate attributes on parameters/return values
        }

        private void ValidateCustomAttributes(ICustomAttributeProvider provider) {
            if (!provider.HasCustomAttributes)
                return;
            foreach (var attribute in provider.CustomAttributes) {
                ValidateMemberReference(attribute.AttributeType);
                // TODO: Validate attribute arguments
            }
        }

        public MemberPolicy ValidateInstructionAndGetRule(Instruction instruction) {
            if (!(instruction.Operand is MemberReference reference))
                return null;

            return ValidateMemberReference(reference);
        }

        private MemberPolicy ValidateMemberReference(MemberReference reference) {
            var type = reference.DeclaringType;
            switch (reference) {
                case MethodReference m: {
                    var memberRule = EnsureAllowed(m.DeclaringType, m.Name);
                    EnsureAllowed(m.ReturnType);
                    return memberRule;
                }
                case FieldReference f: {
                    var memberRule = EnsureAllowed(f.DeclaringType, f.Name);
                    EnsureAllowed(f.FieldType);
                    return memberRule;
                }
                case TypeReference t:
                    EnsureAllowed(t);
                    return null;
                default:
                    throw new NotSupportedException($"Unexpected member type '{reference.GetType()}'.");
            }
        }

        private MemberPolicy EnsureAllowed(TypeReference type, string memberName = null) {
            if (type.IsGenericParameter)
                return null;

            if ((type is GenericInstanceType generic)) {
                var rule = EnsureAllowed(generic.ElementType, memberName);
                foreach (var argument in generic.GenericArguments) {
                    EnsureAllowed(argument);
                }
                return rule;
            }

            var typeKind = ApiFilterTypeKind.External;
            if (type.IsArray) {
                EnsureAllowed(type.GetElementType());
                if (memberName == null)
                    return null;
                typeKind = ApiFilterTypeKind.Array;
            }
            else if ((type is TypeDefinition typeDefinition) && type.Module.Assembly == _userAssembly) {
                if (!IsDelegateDefinition(typeDefinition))
                    return null;
                typeKind = ApiFilterTypeKind.CompilerGeneratedDelegate;
            }

            var @namespace = GetNamespace(type);
            var typeName = !type.IsNested ? type.Name : (type.FullName.Substring(@namespace.Length + 1).Replace("/", "+"));
            var result = _filter.Filter(@namespace, typeName, typeKind, memberName);
            switch (result.Kind) {
                case ApiFilterResultKind.DeniedNamespace:
                    throw new AssemblyGuardException($"Namespace {@namespace} is not allowed.");
                case ApiFilterResultKind.DeniedType:
                    throw new AssemblyGuardException($"Type {type.FullName} is not allowed.");
                case ApiFilterResultKind.DeniedMember:
                    throw new AssemblyGuardException($"Member {type.FullName}.{memberName} is not allowed.");
                case ApiFilterResultKind.Allowed:
                    return result.MemberRule;
                default:
                    throw new NotSupportedException($"Unknown filter result {result}.");
            }
        }

        private string GetNamespace(TypeReference type) {
            string @namespace = null;
            while (string.IsNullOrEmpty(@namespace) && type != null) {
                @namespace = type.Namespace;
                type = type.DeclaringType;
            }
            return @namespace;
        }

        private static bool IsDelegateDefinition(TypeDefinition type) {
            var baseType = type.BaseType;
            return baseType != null
                && (
                    baseType.FullName == "System.MulticastDelegate"
                    ||
                    baseType.FullName == "System.Delegate"
                );
        }
    }
}
