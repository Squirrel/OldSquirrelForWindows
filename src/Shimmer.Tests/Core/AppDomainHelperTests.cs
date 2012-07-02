using System;
using Shimmer.Core;
using Xunit;

namespace Shimmer.Tests.Core
{
    public class AppDomainHelperTests
    {
        public class TheExecuteInNewAppDomainMethod
        {
            [Fact]
            public void ExecutesCodeInNewAppDomain()
            {
                int currentAppDomainId = AppDomain.CurrentDomain.Id;

                int newAppDomainId = AppDomainHelper.ExecuteInNewAppDomain(0, _ => AppDomain.CurrentDomain.Id);

                Assert.NotEqual(currentAppDomainId, newAppDomainId);
            }
        }

        public class TheExecuteActionInNewAppDomainMethod
        {
            [Fact]
            public void ExecutesCodeInNewAppDomain()
            {
                int currentAppDomainId = AppDomain.CurrentDomain.Id;

                bool isDifferent = AppDomainHelper.ExecuteInNewAppDomain(currentAppDomainId, currentId => AppDomain.CurrentDomain.Id != currentId);

                Assert.True(isDifferent);
            }
        }
    }
}