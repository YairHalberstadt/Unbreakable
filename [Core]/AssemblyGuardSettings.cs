using System;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Unbreakable.Internal;

namespace Unbreakable {
    public class AssemblyGuardSettings {
        [NotNull] internal static AssemblyGuardSettings Default { get; } = new AssemblyGuardSettings();

        private ApiFilter _apiFilter;

        public AssemblyGuardSettings() {
            _apiFilter = new ApiFilter(ApiPolicy.SafeDefault());
            MethodLocalsSizeLimit = IntPtr.Size * 32;
            MethodStackPushSizeLimit = 64;
        }

        [NotNull] public IApiFilter ApiFilter => _apiFilter;

        [NotNull]
        public ApiPolicy ApiPolicy {
            get => _apiFilter.Policy;
            set => _apiFilter.Policy = Argument.NotNull(nameof(value), value);
        }

        public int MethodLocalsSizeLimit { get; set; }
        public int MethodStackPushSizeLimit { get; set; }
        [CanBeNull] public Regex AllowExplicitLayoutInTypesMatchingPattern { get; set; }
        [CanBeNull] public Regex AllowPointerOperationsInTypesMatchingPattern { get; set; }

        public static AssemblyGuardSettings DefaultForCSharpAssembly() {
            return new AssemblyGuardSettings {
                // Array initializers for constant arrays use those
                AllowExplicitLayoutInTypesMatchingPattern = new Regex("<PrivateImplementationDetails>"),
                // Anonymous types use pointer operations in ToString()
                AllowPointerOperationsInTypesMatchingPattern = new Regex("^<>f__AnonymousType.+$")
            };
        }
    }
}