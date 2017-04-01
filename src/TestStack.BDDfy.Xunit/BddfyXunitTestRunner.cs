using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace TestStack.BDDfy.Xunit
{
	public class BddfyXunitTestRunner : XunitTestRunner
	{
		public BddfyXunitTestRunner(ITest test,
									IMessageBus messageBus,
									Type testClass,
									object[] constructorArguments,
									MethodInfo testMethod,
									object[] testMethodArguments,
									string skipReason,
									IReadOnlyList<BeforeAfterTestAttribute> beforeAfterAttributes,
									ExceptionAggregator aggregator,
									CancellationTokenSource cancellationTokenSource)
			: base(test, messageBus, testClass, constructorArguments, testMethod, testMethodArguments, skipReason, beforeAfterAttributes, aggregator, cancellationTokenSource) { }

		protected override async Task<Tuple<decimal, string>> InvokeTestAsync(ExceptionAggregator aggregator)
		{
			BddfyInilializer.EnsureInitialized();
			TestOutputHelper testOutputHelper = null;
			foreach (var obj in ConstructorArguments)
			{
				testOutputHelper = obj as TestOutputHelper;
				if (testOutputHelper != null)
					break;
			}

			if (testOutputHelper == null)
				testOutputHelper = new TestOutputHelper();

			Xunit2BddfyTextReporter.Instance.RegisterOutput(testOutputHelper);
			testOutputHelper.Initialize(MessageBus, Test);

			var executionTime = await InvokeTestMethodAsync(aggregator);

			var output = testOutputHelper.Output;
			testOutputHelper.Uninitialize();

			return Tuple.Create(executionTime, output);
		}
	}
}