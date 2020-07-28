using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace TestStack.BDDfy.Xunit
{
	public class BddfyXunitTheoryTestCase : XunitTheoryTestCase
	{
		/// <summary/>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
		public BddfyXunitTheoryTestCase() { }

		public BddfyXunitTheoryTestCase(IMessageSink diagnosticMessageSink, TestMethodDisplay defaultMethodDisplay, ITestMethod testMethod)
			: base(diagnosticMessageSink, defaultMethodDisplay, testMethod) { }

		public override Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink,
												  IMessageBus messageBus,
												  object[] constructorArguments,
												  ExceptionAggregator aggregator,
												  CancellationTokenSource cancellationTokenSource)
			=> new BddfyXunitTheoryTestCaseRunner(this, DisplayName, SkipReason, constructorArguments, diagnosticMessageSink, messageBus, aggregator, cancellationTokenSource).RunAsync();
	}

	public class BddfyXunitTheoryTestCaseRunner : XunitTestCaseRunner
	{
		static readonly object[] NoArguments = new object[0];

		readonly ExceptionAggregator _cleanupAggregator = new ExceptionAggregator();
		Exception _dataDiscoveryException;
		readonly IMessageSink _diagnosticMessageSink;
		readonly List<BddfyXunitTestRunner> _testRunners = new List<BddfyXunitTestRunner>();
		readonly List<IDisposable> _toDispose = new List<IDisposable>();

		public BddfyXunitTheoryTestCaseRunner(IXunitTestCase testCase,
											  string displayName,
											  string skipReason,
											  object[] constructorArguments,
											  IMessageSink diagnosticMessageSink,
											  IMessageBus messageBus,
											  ExceptionAggregator aggregator,
											  CancellationTokenSource cancellationTokenSource)
			: base(testCase, displayName, skipReason, constructorArguments, NoArguments, messageBus, aggregator, cancellationTokenSource)
		{
			_diagnosticMessageSink = diagnosticMessageSink;
		}

		protected override async Task AfterTestCaseStartingAsync()
		{
			await base.AfterTestCaseStartingAsync().ConfigureAwait(false);

			try
			{
				var dataAttributes = TestCase.TestMethod.Method.GetCustomAttributes(typeof(DataAttribute));

				foreach (var dataAttribute in dataAttributes)
				{
					var discovererAttribute = dataAttribute.GetCustomAttributes(typeof(DataDiscovererAttribute)).First();
					var args = discovererAttribute.GetConstructorArguments().Cast<string>().ToList();
					var discovererType = SerializationHelper.GetType(args[1], args[0]);
					if (discovererType == null)
					{
						if (dataAttribute is IReflectionAttributeInfo reflectionAttribute)
							Aggregator.Add(new InvalidOperationException($"Data discoverer specified for {reflectionAttribute.Attribute.GetType()} on {TestCase.TestMethod.TestClass.Class.Name}.{TestCase.TestMethod.Method.Name} does not exist."));
						else
							Aggregator.Add(new InvalidOperationException($"A data discoverer specified on {TestCase.TestMethod.TestClass.Class.Name}.{TestCase.TestMethod.Method.Name} does not exist."));

						continue;
					}

					IDataDiscoverer discoverer;
					try
					{
						discoverer = ExtensibilityPointFactory.GetDataDiscoverer(_diagnosticMessageSink, discovererType);
					}
					catch (InvalidCastException)
					{
						if (dataAttribute is IReflectionAttributeInfo reflectionAttribute)
							Aggregator.Add(new InvalidOperationException($"Data discoverer specified for {reflectionAttribute.Attribute.GetType()} on {TestCase.TestMethod.TestClass.Class.Name}.{TestCase.TestMethod.Method.Name} does not implement IDataDiscoverer."));
						else
							Aggregator.Add(new InvalidOperationException($"A data discoverer specified on {TestCase.TestMethod.TestClass.Class.Name}.{TestCase.TestMethod.Method.Name} does not implement IDataDiscoverer."));

						continue;
					}

					var data = discoverer.GetData(dataAttribute, TestCase.TestMethod.Method);
					if (data == null)
					{
						Aggregator.Add(new InvalidOperationException($"Test data returned null for {TestCase.TestMethod.TestClass.Class.Name}.{TestCase.TestMethod.Method.Name}. Make sure it is statically initialized before this test method is called."));
						continue;
					}

					foreach (var dataRow in data)
					{
						_toDispose.AddRange(dataRow.OfType<IDisposable>());

						ITypeInfo[] resolvedTypes = null;
						var methodToRun = TestMethod;
						var convertedDataRow = methodToRun.ResolveMethodArguments(dataRow);

						if (methodToRun.IsGenericMethodDefinition)
						{
							resolvedTypes = TestCase.TestMethod.Method.ResolveGenericTypes(convertedDataRow);
							methodToRun = methodToRun.MakeGenericMethod(resolvedTypes.Select(t => ((IReflectionTypeInfo)t).Type).ToArray());
						}

						var parameterTypes = methodToRun.GetParameters().Select(p => p.ParameterType).ToArray();
						convertedDataRow = Reflector.ConvertArguments(convertedDataRow, parameterTypes);

						var theoryDisplayName = TestCase.TestMethod.Method.GetDisplayNameWithArguments(DisplayName, convertedDataRow, resolvedTypes);
						var test = new XunitTest(TestCase, theoryDisplayName);
						var skipReason = SkipReason ?? dataAttribute.GetNamedArgument<string>("Skip");
						_testRunners.Add(new BddfyXunitTestRunner(test, MessageBus, TestClass, ConstructorArguments, methodToRun, convertedDataRow, skipReason, BeforeAfterAttributes, Aggregator, CancellationTokenSource));
					}
				}
			}
			catch (Exception ex)
			{
				// Stash the exception so we can surface it during RunTestAsync
				_dataDiscoveryException = ex;
			}
		}

		protected override Task BeforeTestCaseFinishedAsync()
		{
			Aggregator.Aggregate(_cleanupAggregator);

			return base.BeforeTestCaseFinishedAsync();
		}

		protected override async Task<RunSummary> RunTestAsync()
		{
			if (_dataDiscoveryException != null)
				return RunTest_DataDiscoveryException();

			var runSummary = new RunSummary();
			foreach (var testRunner in _testRunners)
				runSummary.Aggregate(await testRunner.RunAsync().ConfigureAwait(false));

			// Run the cleanup here so we can include cleanup time in the run summary,
			// but save any exceptions so we can surface them during the cleanup phase,
			// so they get properly reported as test case cleanup failures.
			var timer = new ExecutionTimer();
			foreach (var disposable in _toDispose)
				timer.Aggregate(() => _cleanupAggregator.Run(disposable.Dispose));

			runSummary.Time += timer.Total;
			return runSummary;
		}

		private RunSummary RunTest_DataDiscoveryException()
		{
			var test = new XunitTest(TestCase, DisplayName);

			if (!MessageBus.QueueMessage(new TestStarting(test)))
				CancellationTokenSource.Cancel();
			else if (!MessageBus.QueueMessage(new TestFailed(test, 0, null, _dataDiscoveryException.Unwrap())))
				CancellationTokenSource.Cancel();
			if (!MessageBus.QueueMessage(new TestFinished(test, 0, null)))
				CancellationTokenSource.Cancel();

			return new RunSummary { Total = 1, Failed = 1 };
		}
	}

	internal static class SerializationHelper
	{
		/// <summary>
		/// Converts an assembly name + type name into a <see cref="Type"/> object.
		/// </summary>
		/// <param name="assemblyName">The assembly name.</param>
		/// <param name="typeName">The type name.</param>
		/// <returns>The instance of the <see cref="Type"/>, if available; <c>null</c>, otherwise.</returns>
		public static Type GetType(string assemblyName, string typeName)
		{
			if (assemblyName.EndsWith(ExecutionHelper.SubstitutionToken, StringComparison.OrdinalIgnoreCase))
				assemblyName = assemblyName.Substring(0, assemblyName.Length - ExecutionHelper.SubstitutionToken.Length + 1) + ExecutionHelper.PlatformSuffix;

#if NETSTANDARD1_5
			Assembly assembly = null;
			try
			{
				// Make sure we only use the short form
				var an = new AssemblyName(assemblyName);
				assembly = Assembly.Load(new AssemblyName { Name = an.Name, Version = an.Version });

			}
			catch
			{
				// ignored
			}
#else
			// Support both long name ("assembly, version=x.x.x.x, etc.") and short name ("assembly")
			var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == assemblyName || a.GetName().Name == assemblyName);
			if (assembly == null)
			{
				try
				{
					assembly = Assembly.Load(assemblyName);
				}
				catch
				{
					// ignored
				}
			}
#endif
			return assembly?.GetType(typeName);
		}
	}

	internal static class ExecutionHelper
	{
		static readonly string executionAssemblyNamePrefix = "xunit.execution.";
		static string platformSuffix = "__unknown__";

		public static string PlatformSuffix
		{
			get
			{
				lock (executionAssemblyNamePrefix)
				{
					if (platformSuffix == "__unknown__")
					{
						platformSuffix = null;

#if NETSTANDARD1_5
						foreach (var suffix in new[] { "dotnet", "MonoAndroid", "MonoTouch", "iOS-Universal", "universal", "win8", "wp8" })
							try
							{
								Assembly.Load(new AssemblyName { Name = executionAssemblyNamePrefix + suffix });
								platformSuffix = suffix;
								break;
							}
							catch { }
#else
						foreach (var name in AppDomain.CurrentDomain.GetAssemblies().Select(a => a?.GetName()?.Name))
							if (name != null && name.StartsWith(executionAssemblyNamePrefix, StringComparison.Ordinal))
							{
								platformSuffix = name.Substring(executionAssemblyNamePrefix.Length);
								break;
							}
#endif
					}
				}

				if (platformSuffix == null)
					throw new InvalidOperationException($"Could not find any xunit.execution.* assembly loaded in the current context");

				return platformSuffix;
			}
		}

		/// <summary>
		/// Gets the substitution token used as assembly name suffix to indicate that the assembly is
		/// a generalized reference to the platform-specific assembly.
		/// </summary>
		public static readonly string SubstitutionToken = ".{Platform}";
	}

	internal static class ExceptionExtensions
	{
		/// <summary>
		/// Unwraps an exception to remove any wrappers, like <see cref="TargetInvocationException"/>.
		/// </summary>
		/// <param name="ex">The exception to unwrap.</param>
		/// <returns>The unwrapped exception.</returns>
		public static Exception Unwrap(this Exception ex)
		{
			while (true)
			{
				var tiex = ex as TargetInvocationException;
				if (tiex == null)
					return ex;

				ex = tiex.InnerException;
			}
		}
	}
}