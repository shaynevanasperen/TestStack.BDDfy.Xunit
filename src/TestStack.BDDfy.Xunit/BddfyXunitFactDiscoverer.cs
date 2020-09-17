using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace TestStack.BDDfy.Xunit
{
	[XunitTestCaseDiscoverer("TestStack.BDDfy.Xunit.BddfyXunitFactDiscoverer", "TestStack.BDDfy.Xunit")]
	public class BddfyFactAttribute : FactAttribute { }

	public class BddfyXunitFactDiscoverer : FactDiscoverer
	{
		readonly IMessageSink _diagnosticMessageSink;

		public BddfyXunitFactDiscoverer(IMessageSink diagnosticMessageSink) : base(diagnosticMessageSink)
		{
			_diagnosticMessageSink = diagnosticMessageSink;
		}

		protected override IXunitTestCase CreateTestCase(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo factAttribute)
			=> new BddfyXunitTestCase(_diagnosticMessageSink, discoveryOptions.MethodDisplayOrDefault(), discoveryOptions.MethodDisplayOptionsOrDefault(), testMethod);
	}
}