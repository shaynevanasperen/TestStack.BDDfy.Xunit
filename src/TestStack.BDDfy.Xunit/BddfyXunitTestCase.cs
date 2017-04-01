using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace TestStack.BDDfy.Xunit
{
	public class BddfyXunitTestCase : XunitTestCase
	{
		/// <summary/>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
		public BddfyXunitTestCase() { }

		public BddfyXunitTestCase(IMessageSink diagnosticMessageSink, TestMethodDisplay defaultMethodDisplay, ITestMethod testMethod, object[] testMethodArguments = null)
			: base(diagnosticMessageSink, defaultMethodDisplay, testMethod, testMethodArguments) { }

		public override Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink,
												  IMessageBus messageBus,
												  object[] constructorArguments,
												  ExceptionAggregator aggregator,
												  CancellationTokenSource cancellationTokenSource)
			=> new BddfyXunitTestCaseRunner(this, DisplayName, SkipReason, constructorArguments, TestMethodArguments, messageBus, aggregator, cancellationTokenSource).RunAsync();
	}

	public class BddfyXunitTestCaseRunner : XunitTestCaseRunner
	{
		public BddfyXunitTestCaseRunner(IXunitTestCase testCase,
										string displayName,
										string skipReason,
										object[] constructorArguments,
										object[] testMethodArguments,
										IMessageBus messageBus,
										ExceptionAggregator aggregator,
										CancellationTokenSource cancellationTokenSource)
			: base(testCase, displayName, skipReason, constructorArguments, testMethodArguments, messageBus, aggregator, cancellationTokenSource) { }

		protected override Task<RunSummary> RunTestAsync() => new BddfyXunitTestRunner(
			new XunitTest(TestCase, DisplayName), MessageBus, TestClass, ConstructorArguments, TestMethod, TestMethodArguments, SkipReason, BeforeAfterAttributes, new ExceptionAggregator(Aggregator), CancellationTokenSource).RunAsync();
	}
}