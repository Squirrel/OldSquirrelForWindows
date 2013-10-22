using System;
using NuGet;
using Squirrel.Core;
using Xunit;

namespace Squirrel.Tests.Core
{
    public class VersionComparerTests
    {
        public class TheCompareMethod
        {
            Func<string, IVersionSpec> convert
                 = VersionUtility.ParseVersionSpec;

            [Fact]
            public void TestWithBoundaryCase()
            {
                var version = new SemanticVersion("1.0");

                // from http://docs.nuget.org/docs/reference/versioning
                // 1.0  = 1.0 ≤ x
                Assert.True(VersionComparer.Matches(convert("1.0"), version));

                // (,1.0]  = x ≤ 1.0
                Assert.True(VersionComparer.Matches(convert("(,1.0]"), version));

                // (,1.0)  = x < 1.0
                Assert.False(VersionComparer.Matches(convert("(,1.0)"), version));

                // [1.0] = x == 1.0
                Assert.True(VersionComparer.Matches(convert("[1.0]"), version));
                
                // (1.0,) = 1.0 < x
                Assert.False(VersionComparer.Matches(convert("(1.0,)"), version));

                // (1.0,2.0) = 1.0 < x < 2.0
                Assert.False(VersionComparer.Matches(convert("(1.0,2.0)"), version));

                // [1.0,2.0] = 1.0 ≤ x ≤ 2.0
                Assert.True(VersionComparer.Matches(convert("[1.0,2.0]"), version));

                // empty = latest version.
                Assert.True(VersionComparer.Matches(null, version));
            }

            [Fact]
            public void TestWithPreReleaseVersion()
            {
                var version = new SemanticVersion("0.1");

                // from http://docs.nuget.org/docs/reference/versioning
                // 1.0  = 1.0 ≤ x
                Assert.False(VersionComparer.Matches(convert("1.0"), version));

                // (,1.0]  = x ≤ 1.0
                Assert.True(VersionComparer.Matches(convert("(,1.0]"), version));

                // (,1.0)  = x < 1.0
                Assert.True(VersionComparer.Matches(convert("(,1.0)"), version));

                // [1.0] = x == 1.0
                Assert.False(VersionComparer.Matches(convert("[1.0]"), version));

                // (1.0,) = 1.0 < x
                Assert.False(VersionComparer.Matches(convert("(1.0,) "), version));

                // (1.0,2.0) = 1.0 < x < 2.0
                Assert.False(VersionComparer.Matches(convert("(1.0,2.0) "), version));

                // [1.0,2.0] = 1.0 ≤ x ≤ 2.0
                Assert.False(VersionComparer.Matches(convert("[1.0,2.0]"), version));

                // empty = latest version.
                Assert.True(VersionComparer.Matches(null, version));
            }

            [Fact]
            public void TestWithInBetweenVersion()
            {
                var version = new SemanticVersion("1.5");

                // from http://docs.nuget.org/docs/reference/versioning
                // 1.0  = 1.0 ≤ x
                Assert.True(VersionComparer.Matches(convert("1.0"), version));

                // (,1.0]  = x ≤ 1.0
                Assert.False(VersionComparer.Matches(convert("(,1.0]"), version));

                // (,1.0)  = x < 1.0
                Assert.False(VersionComparer.Matches(convert("(,1.0)"), version));

                // [1.0] = x == 1.0
                Assert.False(VersionComparer.Matches(convert("[1.0]"), version));

                // (1.0,) = 1.0 < x
                Assert.True(VersionComparer.Matches(convert("(1.0,) "), version));

                // (1.0,2.0) = 1.0 < x < 2.0
                Assert.True(VersionComparer.Matches(convert("(1.0,2.0) "), version));

                // [1.0,2.0] = 1.0 ≤ x ≤ 2.0
                Assert.True(VersionComparer.Matches(convert("[1.0,2.0]"), version));

                // empty = latest version.
                Assert.True(VersionComparer.Matches(null, version));
            }
        }
    }
}
