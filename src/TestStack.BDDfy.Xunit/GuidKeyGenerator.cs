using System;
using TestStack.BDDfy.Configuration;

namespace TestStack.BDDfy.Xunit
{
	public class GuiKeyGenerator : IKeyGenerator
	{
		public string GetScenarioId()
		{
			return Guid.NewGuid().ToString();
		}

		public string GetStepId()
		{
			return Guid.NewGuid().ToString();
		}

		public void Reset() { }
	}
}